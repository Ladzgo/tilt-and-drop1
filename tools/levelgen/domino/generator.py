"""Domino Run generator: grow the chain from the start (straight runs, turners at corners, splitter forks,
bells at branch ends), add decoys, scramble turner arrows."""
from __future__ import annotations

import random
from dataclasses import dataclass

from .model import DX, DY, DominoLevel
from .sim import wins
from .solver import solve


@dataclass(frozen=True)
class DSpec:
    name: str = "basic"
    sizes: tuple = ((5, 5),)
    turns: tuple = (1, 2)
    splits: tuple = (0, 0)
    run: tuple = (1, 3)
    decoy_turners: tuple = (0, 1)
    fixed_p: float = 0.0
    walls: tuple = (0.0, 0.0)
    max_turners: int = 7
    max_solutions: int = 64
    par: tuple = (1, 4)


def build(spec: DSpec, rng: random.Random) -> DominoLevel | None:
    w, h = rng.choice(spec.sizes)
    n = w * h
    cells = [(".", 0)] * n
    for i in rng.sample(range(n), int(round(rng.uniform(*spec.walls) * n))):
        cells[i] = ("#", 0)
    free = [i for i in range(n) if cells[i][0] == "."]
    s = rng.choice(free)
    d0 = rng.randrange(4)
    cells[s] = ("S", d0)
    turns_left, splits_left = rng.randint(*spec.turns), rng.randint(*spec.splits)
    branches = [(s, d0)]
    intended: dict[int, int] = {}
    chain = {s}
    while branches:
        c, d = branches.pop()
        run_len = rng.randint(*spec.run)
        steps = 0
        last = None
        while True:
            x, y = c % w, c // w
            nx, ny = x + DX[d], y + DY[d]
            if not (0 <= nx < w and 0 <= ny < h) or cells[ny * w + nx][0] != "." or ny * w + nx in chain:
                break
            nc = ny * w + nx
            # look ahead: is there room for at least one more cell?
            steps += 1
            chain.add(nc)
            last = nc
            if steps >= run_len and splits_left > 0 and rng.random() < 0.5:
                cells[nc] = ("Y", 0)
                splits_left -= 1
                branches.append((nc, (d + 1) & 3))
                branches.append((nc, (d + 3) & 3))
                last = None
                break
            if steps >= run_len and turns_left > 0:
                nd = (d + rng.choice((1, 3))) & 3
                fixed = rng.random() < spec.fixed_p
                cells[nc] = ("T" if fixed else "t", nd)
                if not fixed:
                    intended[nc] = nd
                turns_left -= 1
                d, c, steps, run_len = nd, nc, 0, rng.randint(*spec.run)
                continue
            cells[nc] = ("d", 0)
            c = nc
            if steps >= run_len + 2:
                break
        if last is not None:
            k, _ = cells[last]
            if k in "tT":
                return None
            cells[last] = ("B", 0)
    if not any(k == "B" for k, _ in cells):
        return None
    # decoy turners beside the chain
    near = [j for i in chain for j in (i - 1, i + 1, i - w, i + w)
            if 0 <= j < n and cells[j][0] == "." and abs(j % w - i % w) <= 1]
    rng.shuffle(near)
    for j in near[: rng.randint(*spec.decoy_turners)]:
        cells[j] = ("t", rng.randrange(4))
    try:
        lv = DominoLevel(w, h, cells)
    except ValueError:
        return None
    if len(lv.turners) > spec.max_turners or not lv.turners:
        return None
    cfg = tuple(intended.get(i, lv.cells[i][1]) for i in lv.turners)
    if not wins(lv, cfg):
        return None
    # scramble every turner
    scr = [rng.randrange(4) for _ in lv.turners]
    if wins(lv, tuple(scr)):
        k = rng.randrange(len(scr))
        scr[k] = (scr[k] + 1) & 3
    for i, v in zip(lv.turners, scr):
        lv.cells[i] = ("t", v)
    return lv


@dataclass
class DCandidate:
    level: DominoLevel
    par: int
    solutions: int
    share: float
    fail: float
    spec: str
    seed: int


def generate_one(spec: DSpec, seed: int, bot_runs: int = 60, max_attempts: int = 400) -> DCandidate | None:
    from .bot import fail_rate
    rng = random.Random(seed)
    for _ in range(max_attempts):
        lv = build(spec, rng)
        if lv is None:
            continue
        s = solve(lv)
        if s.par is None or not spec.par[0] <= s.par <= spec.par[1] or s.solutions > spec.max_solutions:
            continue
        lv.par = s.par
        return DCandidate(lv, s.par, s.solutions, s.solutions / s.total, fail_rate(lv, s.winning, runs=bot_runs, seed=seed), spec.name, seed)
    return None
