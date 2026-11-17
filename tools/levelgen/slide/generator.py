"""Slide Out generator: pick a tray-safe removal order, then place blocks in reverse so each block's
lane is clear of the blocks that leave after it. The intended order is always a solution."""
from __future__ import annotations

import random
from dataclasses import dataclass

from .model import DX, DY, WALL, SlideLevel
from .solver import solve


@dataclass(frozen=True)
class SSpec:
    name: str = "basic"
    sizes: tuple = ((4, 4),)
    colors: tuple = (2, 2)
    per_color: tuple = (3,)       # blocks per colour (multiples of 3)
    capacity: int = 5
    walls: tuple = (0.0, 0.0)
    lane_bias: float = 0.6        # chance to drop a block into a lane of an already placed block
    min_peak: int = 2             # intended order must reach this tray size (harder when close to capacity)
    min_dead: float = 0.0


def _order(rng, colors, per, cap, min_peak):
    seq = [c for c in colors for _ in range(per[c])]
    for _ in range(200):
        rng.shuffle(seq)
        tray, peak, ok = [], 0, True
        for c in seq:
            tray.append(c)
            peak = max(peak, len(tray))
            if tray.count(c) == 3:
                tray = [t for t in tray if t != c]  # at most 2 of a colour wait, so this removes exactly three
            if len(tray) >= cap:
                ok = False
                break
        if ok and peak >= min_peak:
            return list(seq)
    return None


def build(spec: SSpec, rng: random.Random) -> SlideLevel | None:
    w, h = rng.choice(spec.sizes)
    n = w * h
    cells: list = [None] * n
    for i in rng.sample(range(n), int(round(rng.uniform(*spec.walls) * n))):
        cells[i] = WALL
    k = rng.randint(*spec.colors)
    palette = rng.sample(range(1, 7), k)
    per = {c: rng.choice(spec.per_color) for c in palette}
    order = _order(rng, palette, per, spec.capacity, min(spec.min_peak, spec.capacity - 1))
    if order is None:
        return None
    lanes: set[int] = set()
    for colour in reversed(order):
        empties = [i for i in range(n) if cells[i] is None]
        rng.shuffle(empties)
        if rng.random() < spec.lane_bias:
            empties.sort(key=lambda i: 0 if i in lanes else 1)
        placed = False
        for i in empties:
            dirs = [0, 1, 2, 3]
            rng.shuffle(dirs)
            for d in dirs:
                x, y, path, clear = i % w, i // w, [], True
                while True:
                    x, y = x + DX[d], y + DY[d]
                    if not (0 <= x < w and 0 <= y < h):
                        break
                    j = y * w + x
                    if cells[j] is not None:
                        clear = False
                        break
                    path.append(j)
                if clear:
                    cells[i] = (colour, d)
                    lanes.update(path)
                    placed = True
                    break
            if placed:
                break
        if not placed:
            return None
    return SlideLevel(w, h, cells, spec.capacity)


@dataclass
class SCandidate:
    level: SlideLevel
    blocks: int
    dead_end: float
    avg_free: float
    fail: float
    spec: str
    seed: int


def generate_one(spec: SSpec, seed: int, bot_runs: int = 60, max_attempts: int = 200) -> SCandidate | None:
    from .bot import fail_rate
    rng = random.Random(seed)
    for _ in range(max_attempts):
        lv = build(spec, rng)
        if lv is None:
            continue
        s = solve(lv, node_cap=200_000)
        if not s.solvable or s.truncated or s.dead_end_ratio < spec.min_dead:
            continue
        return SCandidate(lv, len(lv.blocks), s.dead_end_ratio, s.avg_free, fail_rate(lv, runs=bot_runs, seed=seed), spec.name, seed)
    return None
