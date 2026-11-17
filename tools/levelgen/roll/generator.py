"""Roll & Print generator: random-walk the cube as if every floor tile were a target; tiles whose last
print is a colour can become targets (the walk is a solution by construction). BFS then gives exact par."""
from __future__ import annotations

import random
from dataclasses import dataclass

from .model import FLOOR, ICE, POOL, TARGET, WALL, RollLevel
from .sim import solved, step
from .solver import solve


@dataclass(frozen=True)
class RSpec:
    name: str = "basic"
    sizes: tuple = ((4, 4),)
    colors: tuple = (1, 1)        # distinct paint colours on the cube
    painted: tuple = (1, 1)       # painted faces
    targets: tuple = (1, 2)
    pools: tuple = (0, 0)
    ice: tuple = (0, 0)
    walls: tuple = (0.0, 0.1)
    walk: tuple = (3, 8)
    par: tuple = (1, 4)


def _ri(rng, r):
    return rng.randint(r[0], r[1])


def build(spec: RSpec, rng: random.Random) -> RollLevel | None:
    w, h = rng.choice(spec.sizes)
    n = w * h
    tiles = [(FLOOR, 0)] * n
    for i in rng.sample(range(n), int(round(rng.uniform(*spec.walls) * n))):
        tiles[i] = (WALL, 0)
    palette = rng.sample(range(1, 7), _ri(rng, spec.colors))
    open_cells = [i for i in range(n) if tiles[i][0] == FLOOR]
    rng.shuffle(open_cells)
    for _ in range(_ri(rng, spec.pools)):
        if open_cells:
            tiles[open_cells.pop()] = (POOL, rng.choice(palette))
    for _ in range(_ri(rng, spec.ice)):
        if open_cells:
            tiles[open_cells.pop()] = (ICE, 0)
    if not open_cells:
        return None
    start = open_cells.pop()
    faces = [0] * 6
    for f in rng.sample(range(6), _ri(rng, spec.painted)):
        faces[f] = rng.choice(palette)
    if not any(faces) and not spec.pools[1]:
        return None
    # walk with every floor tile acting as a print target
    virtual = RollLevel(w, h, [((TARGET, 0) if t[0] == FLOOR else t) for t in tiles], (start % w, start // w), tuple(faces))
    pos, f, prints, last = virtual.cube, tuple(faces), {}, None
    for _ in range(_ri(rng, spec.walk)):
        dirs = [d for d in range(4) if d != last or rng.random() < 0.3]
        rng.shuffle(dirs)
        for d in dirs:
            r = step(virtual, pos, f, prints, d)
            if r is not None:
                pos, f, prints = r
                last = (d + 2) % 4
                break
        else:
            return None
    cands = [i for i, c in prints.items() if c and i != start]
    if not cands:
        return None
    k = min(len(cands), _ri(rng, spec.targets))
    chosen = rng.sample(cands, k)
    final = list(tiles)
    for i in chosen:
        final[i] = (TARGET, prints[i])
    lv = RollLevel(w, h, final, (start % w, start // w), tuple(faces))
    if solved(lv, {}):
        return None
    return lv


@dataclass
class RCandidate:
    level: RollLevel
    par: int
    solutions: int
    dead_end: float
    fail_by_slack: dict
    spec: str
    seed: int


def generate_one(spec: RSpec, seed: int, bot_runs: int = 60, max_attempts: int = 300) -> RCandidate | None:
    from .bot import fail_rate
    rng = random.Random(seed)
    for _ in range(max_attempts):
        lv = build(spec, rng)
        if lv is None:
            continue
        s = solve(lv, max_depth=spec.par[1], node_cap=150_000)
        if s.par is None or not spec.par[0] <= s.par <= spec.par[1]:
            continue
        lv.par = s.par
        fails = {k: fail_rate(lv, s.par + k, runs=bot_runs, seed=seed) for k in (0, 1, 2, 3)}
        return RCandidate(lv, s.par, s.solutions, s.dead_end_ratio, fails, spec.name, seed)
    return None
