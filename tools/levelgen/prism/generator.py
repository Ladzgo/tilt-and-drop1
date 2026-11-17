"""Prism level generator: place pieces, trace the intended light, put targets where light ends or
crosses (crossings make mixed colours), then scramble mirrors on the light path."""
from __future__ import annotations

import math
import random
from dataclasses import dataclass

from .model import EMPTY, FILTER, FIXED, MIRROR, ROTATABLE, SOURCE, SPLITTER, TARGET, WALL, PrismLevel
from .sim import is_win, trace
from .solver import solve


@dataclass(frozen=True)
class PSpec:
    name: str = "basic"
    sizes: tuple = ((5, 5),)
    sources: tuple = (1, 1)
    mirrors: tuple = (2, 4)
    decoys: tuple = (0, 1)
    fixed: tuple = (0, 0)
    filters: tuple = (0, 0)
    splitters: tuple = (0, 0)
    walls: tuple = (0.0, 0.08)
    mix_prob: float = 0.0
    scramble: tuple = (1, 3)
    par: tuple = (1, 3)
    max_solutions: int = 3
    require_mix: bool = False


def _cells_on_segments(level: PrismLevel, segs) -> list[tuple[int, int]]:
    """(cell index, colour) for every cell a beam passes through or ends in (excluding its start cell)."""
    out = []
    for color, x0, y0, x1, y1 in segs:
        dx = (x1 > x0) - (x1 < x0)
        dy = (y1 > y0) - (y1 < y0)
        x, y = x0, y0
        steps = int(max(abs(x1 - x0), abs(y1 - y0)) + 1e-9)
        for _ in range(steps):
            x, y = x + dx, y + dy
            if 0 <= x < level.w and 0 <= y < level.h:
                out.append((int(y) * level.w + int(x), color))
    return out


def _ri(rng, r):
    return rng.randint(r[0], r[1])


def build(spec: PSpec, rng: random.Random) -> PrismLevel | None:
    w, h = rng.choice(spec.sizes)
    n = w * h
    cells = [(EMPTY, 0)] * n
    free = lambda: [i for i in range(n) if cells[i][0] == EMPTY]  # noqa: E731
    border = [i for i in range(n) if (i % w in (0, w - 1)) != (i // w in (0, h - 1))]  # edges, no corners
    k = _ri(rng, spec.sources)
    colors = rng.sample([1, 2, 4], k) if k <= 3 else [rng.choice([1, 2, 4]) for _ in range(k)]
    rng.shuffle(border)
    for col in colors:
        while border:
            i = border.pop()
            if cells[i][0] != EMPTY:
                continue
            x, y = i % w, i // w
            d = 0 if x == 0 else 2 if x == w - 1 else 1 if y == 0 else 3
            cells[i] = (SOURCE, col * 4 + d)
            break
        else:
            return None
    interior = [i for i in range(n) if 0 < i % w < w - 1 and 0 < i // w < h - 1] or list(range(n))

    def place(kind, count, value):
        spots = [i for i in interior if cells[i][0] == EMPTY]
        rng.shuffle(spots)
        for i in spots[:count]:
            cells[i] = (kind, value())

    place(MIRROR, _ri(rng, spec.mirrors), lambda: rng.randrange(2))
    place(FIXED, _ri(rng, spec.fixed), lambda: rng.randrange(2))
    place(SPLITTER, _ri(rng, spec.splitters), lambda: rng.randrange(2))
    place(FILTER, _ri(rng, spec.filters), lambda: rng.choice(colors))
    walls = int(round(rng.uniform(*spec.walls) * n))
    for i in rng.sample(free(), min(walls, len(free()))):
        cells[i] = (WALL, 0)
    lv = PrismLevel(w, h, list(cells))

    # intended light
    segs: list = []
    trace(lv, segments=segs)
    visits = _cells_on_segments(lv, segs)
    by_cell: dict[int, int] = {}
    for i, col in visits:
        if lv.cells[i][0] == EMPTY:
            by_cell[i] = by_cell.get(i, 0) | col
    crossings = [i for i, m in by_cell.items() if bin(m).count("1") >= 2]
    targets: set[int] = set()
    if crossings and (spec.require_mix or rng.random() < spec.mix_prob):
        targets.add(rng.choice(crossings))
    # every beam that still flies ends in a target at its last empty cell
    for _ in range(4):
        tl = PrismLevel(w, h, [((TARGET, 7) if i in targets else c) for i, c in enumerate(lv.cells)])
        segs = []
        trace(tl, segments=segs)
        changed = False
        for color, x0, y0, x1, y1 in segs:
            ex, ey = int(round(x1)), int(round(y1))
            if not (0 <= ex < w and 0 <= ey < h) or (x1 != int(x1) or y1 != int(y1)):  # left the board
                ex, ey = int(x1 - (x1 > x0) * 0.5 + (x1 < x0) * 0.5), int(y1 - (y1 > y0) * 0.5 + (y1 < y0) * 0.5)
                cand = [c for c, col in _cells_on_segments(tl, [(color, x0, y0, ex, ey)]) if tl.cells[c][0] == EMPTY]
                if cand:
                    targets.add(cand[-1])
                    changed = True
        if not changed:
            break
    final = PrismLevel(w, h, [((TARGET, 7) if i in targets else c) for i, c in enumerate(lv.cells)])
    rec = trace(final)
    cells2 = list(final.cells)
    for i in targets:
        cells2[i] = (TARGET, rec[i]) if rec[i] else (EMPTY, 0)
    final = PrismLevel(w, h, cells2)
    if not final.targets:
        return None
    if spec.require_mix and not any(bin(final.cells[i][1]).count("1") >= 2 for i in final.targets):
        return None
    # scramble mirrors that the intended light touches
    segs = []
    trace(final, segments=segs)
    on_path = sorted({i for i, _ in _cells_on_segments(final, segs) if final.cells[i][0] in ROTATABLE}
                     | {int(y0) * w + int(x0) for _, x0, y0, _, _ in segs if 0 <= x0 < w and 0 <= y0 < h and final.cells[int(y0) * w + int(x0)][0] in ROTATABLE})
    if not on_path:
        return None
    flip = rng.sample(on_path, min(len(on_path), _ri(rng, spec.scramble)))
    cells3 = list(final.cells)
    for i in flip:
        cells3[i] = (cells3[i][0], cells3[i][1] ^ 1)
    # decoys: extra rotatable mirrors away from the light, random state
    level = PrismLevel(w, h, cells3)
    spots = [i for i in range(n) if level.cells[i][0] == EMPTY and i not in by_cell]
    rng.shuffle(spots)
    # decoys next to the light path catch mis-routed beams (that is what makes them tempting)
    near = lambda i: any(j in by_cell for j in (i - 1, i + 1, i - w, i + w) if 0 <= j < n)  # noqa: E731
    spots.sort(key=lambda i: 0 if near(i) else 1)
    for i in spots[:_ri(rng, spec.decoys)]:
        level.cells[i] = (MIRROR, rng.randrange(2))
    if is_win(level, trace(level)):
        return None
    return level


@dataclass
class PCandidate:
    level: PrismLevel
    par: int
    solutions: int
    fail_by_slack: dict
    rotatables: int
    mixed: int
    spec: str
    seed: int

    @property
    def difficulty(self) -> float:
        return round(100 * (0.35 * min(1, self.par / 8) + 0.35 * self.fail_by_slack.get(2, 0)
                            + 0.15 * min(1, self.rotatables / 12) + 0.15 * min(1, self.mixed / 3)), 2)


def generate_one(spec: PSpec, seed: int, bot_runs: int = 60, max_attempts: int = 300) -> PCandidate | None:
    from .bot import fail_rate
    rng = random.Random(seed)
    for _ in range(max_attempts):
        lv = build(spec, rng)
        if lv is None:
            continue
        s = solve(lv, node_cap=50_000)
        if s.par is None or not spec.par[0] <= s.par <= spec.par[1] or s.solutions > spec.max_solutions:
            continue
        lv.par = s.par
        fails = {k: fail_rate(lv, s.par + k, runs=bot_runs, seed=seed) for k in (0, 1, 2, 3)}
        mixed = sum(1 for i in lv.targets if bin(lv.cells[i][1]).count("1") >= 2)
        return PCandidate(lv, s.par, s.solutions, fails, len(lv.rotatables), mixed, spec.name, seed)
    return None
