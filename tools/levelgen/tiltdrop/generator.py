"""Constrained random level generation.

Pipeline per attempt (seeded, reproducible):
 1. pick a size; scatter interior walls
 2. place colored blocks (some converted to anchor/ice) and stones
 3. settle under gravity Down with NO gates -> stable by construction
 4. place gates (never facing a wall, never under a resting block of the same
    color on the bottom edge) and re-check stability
 5. solve; accept if par is in range and quality filters pass
"""
from __future__ import annotations

import random
from dataclasses import dataclass, field, replace

from .difficulty import Analysis, analyze
from .model import ANCHOR, BLOCK, DOWN, EMPTY, ICE2, STONE, WALL, Level
from .sim import is_stable, settle, topology

NO_GATES = bytes(8)


@dataclass(frozen=True)
class GenSpec:
    name: str = "basic"
    sizes: tuple[tuple[int, int], ...] = ((5, 5),)
    colors: tuple[int, int] = (1, 1)
    blocks_per_color: tuple[int, int] = (1, 3)
    gates_per_color: tuple[int, int] = (1, 1)
    stones: tuple[int, int] = (0, 0)
    walls: tuple[float, float] = (0.0, 0.12)
    anchors: tuple[int, int] = (0, 0)
    ice: tuple[int, int] = (0, 0)
    par: tuple[int, int] = (1, 4)
    min_dead_end: float = 0.0
    max_solution_ratio: float = 1.0  # solutions / 2^par upper bound (filters "anything works")
    require: tuple[str, ...] = ()
    lane_bias: float = 0.6  # chance a gate is placed in a lane holding a piece of its color
    max_depth: int = 22
    node_cap: int = 150_000

    def with_(self, **kw) -> "GenSpec":
        return replace(self, **kw)


@dataclass
class Candidate:
    level: Level
    analysis: Analysis
    spec: str
    seed: int
    attempts: int = 1
    features: dict = field(default_factory=dict)

    @property
    def difficulty(self) -> float:
        return self.analysis.score()


def _ri(rng: random.Random, lo_hi: tuple[int, int]) -> int:
    return rng.randint(lo_hi[0], lo_hi[1])


def build_board(spec: GenSpec, rng: random.Random) -> Level | None:
    w, h = rng.choice(spec.sizes)
    n = w * h
    cells = bytearray(n)
    # walls (interior + border cells allowed; they shape lanes)
    n_walls = int(round(rng.uniform(*spec.walls) * n))
    for i in rng.sample(range(n), n_walls):
        cells[i] = WALL
    free = [i for i in range(n) if cells[i] == EMPTY]
    rng.shuffle(free)
    k = _ri(rng, spec.colors)
    palette = rng.sample(range(1, 7), k)
    pieces: list[int] = []
    for col in palette:
        pieces += [BLOCK | col] * _ri(rng, spec.blocks_per_color)
    # convert some blocks to anchors / ice
    n_anchor, n_ice = _ri(rng, spec.anchors), _ri(rng, spec.ice)
    idx = list(range(len(pieces)))
    rng.shuffle(idx)
    for j in idx[:n_anchor]:
        pieces[j] = ANCHOR | (pieces[j] & 0x0F)
    for j in idx[n_anchor : n_anchor + n_ice]:
        pieces[j] = ICE2 | (pieces[j] & 0x0F)
    pieces += [STONE] * _ri(rng, spec.stones)
    if len(pieces) > len(free) - 2:
        return None
    for p, i in zip(pieces, free):
        cells[i] = p
    # settle with no gates -> stable under Down
    settle(cells, topology(w, h), DOWN, bytes(w))
    # gates
    slots = []
    for e, length in (("T", w), ("B", w), ("L", h), ("R", h)):
        for j in range(length):
            if e == "T":
                adj = j
            elif e == "B":
                adj = (h - 1) * w + j
            elif e == "L":
                adj = j * w
            else:
                adj = j * w + w - 1
            if cells[adj] == WALL:
                continue
            if e == "B" and cells[adj] & 0xF0 == BLOCK:
                continue  # a resting block of matching color would exit on load; keep it simple
            slots.append((e, j))
    rng.shuffle(slots)
    gates = {e: bytearray(w if e in "TB" else h) for e in "TRBL"}

    def in_lane(slot: tuple[str, int], col: int) -> bool:
        e, j = slot
        lane = [j + y * w for y in range(h)] if e in "TB" else [j * w + x for x in range(w)]
        return any(cells[i] & 0x0F == col and cells[i] & 0xF0 in (BLOCK, ANCHOR, ICE2) for i in lane)

    for col in palette:
        for _ in range(_ri(rng, spec.gates_per_color)):
            if not slots:
                return None
            pick = len(slots) - 1
            if rng.random() < spec.lane_bias:
                aligned = [k for k, sl in enumerate(slots) if in_lane(sl, col)]
                if aligned:
                    pick = rng.choice(aligned)
            e, j = slots.pop(pick)
            gates[e][j] = col
    lv = Level(w, h, bytes(cells), {e: bytes(v) for e, v in gates.items()})
    if not is_stable(lv) or not relaxed_reachable(lv):
        return None
    return lv


def relaxed_reachable(lv: Level) -> bool:
    """Necessary condition: every colored piece, moving ALONE (walls only), can
    slide out through a gate of its color. Cheap pre-filter before the solver."""
    w, h = lv.w, lv.h
    walls = [c == WALL for c in lv.cells]
    cache: dict[int, set[int]] = {}

    def exits_from(start: int) -> set[int]:
        if start in cache:
            return cache[start]
        seen, stack, colors = {start}, [start], set()
        while stack:
            p = stack.pop()
            for g, (dx, dy) in enumerate(((0, 1), (1, 0), (0, -1), (-1, 0))):
                x, y = p % w, p // w
                while True:
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < w and 0 <= ny < h):
                        edge = "BRTL"[g]
                        gate = lv.gates[edge][x if g in (0, 2) else y]
                        if gate:
                            colors.add(gate)
                        break
                    if walls[ny * w + nx]:
                        break
                    x, y = nx, ny
                q = y * w + x
                if q not in seen:
                    seen.add(q)
                    stack.append(q)
        cache[start] = colors
        return colors

    for i, c in enumerate(lv.cells):
        if c & 0xF0 in (BLOCK, ANCHOR, ICE2) and (c & 0x0F) not in exits_from(i):
            return False
    return True


def _passes_filters(lv: Level, a: Analysis, spec: GenSpec) -> bool:
    if a.truncated:
        return False
    if not spec.par[0] <= a.par <= spec.par[1]:
        return False
    if a.dead_end < spec.min_dead_end:
        return False
    if a.par > 0 and a.solutions / (2**a.par) > spec.max_solution_ratio:
        return False
    mech = set(lv.detected_mechanics())
    return all(r in mech for r in spec.require)


def generate_one(spec: GenSpec, seed: int, max_attempts: int = 400, bot_runs: int = 100) -> Candidate | None:
    rng = random.Random(seed)
    for attempt in range(1, max_attempts + 1):
        lv = build_board(spec, rng)
        if lv is None:
            continue
        a = analyze(lv, max_depth=spec.par[1], node_cap=spec.node_cap, bot_runs=bot_runs, seed=seed)
        if a is None or not _passes_filters(lv, a, spec):
            continue
        lv.par = a.par
        lv.mechanics = lv.detected_mechanics()
        return Candidate(lv, a, spec.name, seed, attempt)
    return None
