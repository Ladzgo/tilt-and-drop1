"""Flow Fill generator: disjoint random routes from each source to its cup(s) (tees for a second cup),
pipe types from the route geometry, decoy pipes elsewhere, scrambled rotations."""
from __future__ import annotations

import random
from dataclasses import dataclass

from .model import BASE, DX, DY, FlowLevel, rot_mask
from .sim import evaluate
from .solver import solve


@dataclass(frozen=True)
class WSpec:
    name: str = "basic"
    sizes: tuple = ((4, 4),)
    colours: tuple = (1, 1)
    tee_p: float = 0.0
    fixed_p: float = 0.0
    fill: tuple = (0.2, 0.5)
    cross_p: float = 0.0
    par: tuple = (1, 4)
    max_solutions: int = 4


def _shape(mask: int):
    for t in ("i", "l", "t", "x"):
        for r in range(4):
            if rot_mask(BASE[t], r) == mask:
                return t, r
    return None


def _dir(a: int, b: int, w: int) -> int:
    ax, ay, bx, by = a % w, a // w, b % w, b // w
    return {(1, 0): 0, (0, 1): 1, (-1, 0): 2, (0, -1): 3}[(bx - ax, by - ay)]


def _route(w, h, rng, src: int, dst: int, blocked: set[int]) -> list[int] | None:
    prev = {src: None}
    frontier = [src]
    while frontier:
        rng.shuffle(frontier)
        nxt = []
        for i in frontier:
            x, y = i % w, i // w
            nb = [(x + DX[d], y + DY[d]) for d in range(4)]
            rng.shuffle(nb)
            for nx, ny in nb:
                if not (0 <= nx < w and 0 <= ny < h):
                    continue
                j = ny * w + nx
                if j in prev or (j in blocked and j != dst):
                    continue
                prev[j] = i
                if j == dst:
                    path = [j]
                    while prev[path[-1]] is not None:
                        path.append(prev[path[-1]])
                    return path[::-1]
                nxt.append(j)
        frontier = nxt
    return None


def build(spec: WSpec, rng: random.Random) -> FlowLevel | None:
    w, h = rng.choice(spec.sizes)
    n = w * h
    masks = [0] * n
    used: set[int] = set()
    sources, cups = [], []
    colours = rng.sample(range(1, 7), rng.randint(*spec.colours))
    for col in colours:
        free = [i for i in range(n) if i not in used]
        if len(free) < 3:
            return None
        s = rng.choice(free)
        c = rng.choice([i for i in free if i != s and abs(i % w - s % w) + abs(i // w - s // w) >= 3] or [None])
        if c is None:
            return None
        path = _route(w, h, rng, s, c, used | {s})
        if path is None or len(path) < 3:
            return None
        for a, b in zip(path, path[1:]):
            masks[a] |= 1 << _dir(a, b, w)
            masks[b] |= 1 << _dir(b, a, w)
        used.update(path)
        sources.append([s % w, s // w, col, _dir(s, path[1], w)])
        cups.append([c % w, c // w, col, _dir(c, path[-2], w)])
        if rng.random() < spec.tee_p:
            mids = path[1:-1]
            rng.shuffle(mids)
            for m in mids:
                free2 = [i for i in range(n) if i not in used]
                cands = [i for i in free2 if abs(i % w - m % w) + abs(i // w - m // w) >= 2]
                if not cands:
                    break
                c2 = rng.choice(cands)
                p2 = _route(w, h, rng, m, c2, used - {m})
                if p2 is None or len(p2) < 3 or bin(masks[m] | (1 << _dir(m, p2[1], w))).count("1") != 3:
                    continue
                for a, b in zip(p2, p2[1:]):
                    masks[a] |= 1 << _dir(a, b, w)
                    masks[b] |= 1 << _dir(b, a, w)
                used.update(p2)
                cups.append([c2 % w, c2 // w, col, _dir(c2, p2[-2], w)])
                break
    ends = {y * w + x for x, y, _, _ in sources + cups}
    pipes: list = [None] * n
    for i in range(n):
        if i in ends:
            continue
        if masks[i]:
            t, r = _shape(masks[i])
            pipes[i] = (t, r, not (rng.random() < spec.fixed_p))
        elif rng.random() < rng.uniform(*spec.fill):
            if rng.random() < spec.cross_p:
                pipes[i] = ("x", 0, False)
            else:
                pipes[i] = (rng.choice("iilt"), rng.randrange(4), True)
    # scramble rotatable pipes
    pipes = [(p[0], rng.randrange(4), True) if p is not None and p[2] else p for p in pipes]
    try:
        lv = FlowLevel(w, h, pipes, sources, cups)
    except ValueError:
        return None
    if evaluate(lv)["win"]:
        return None
    return lv


@dataclass
class WCandidate:
    level: FlowLevel
    par: int
    solutions: int
    fail_by_slack: dict
    spec: str
    seed: int


def generate_one(spec: WSpec, seed: int, bot_runs: int = 60, max_attempts: int = 300) -> WCandidate | None:
    from .bot import fail_rate
    rng = random.Random(seed)
    for _ in range(max_attempts):
        lv = build(spec, rng)
        if lv is None:
            continue
        s = solve(lv, node_cap=100_000)
        if s.par is None or not spec.par[0] <= s.par <= spec.par[1] or s.solutions > spec.max_solutions:
            continue
        lv.par = s.par
        fails = {k: fail_rate(lv, s.par + k, s.rot, runs=bot_runs, seed=seed) for k in (0, 1, 2, 3)}
        return WCandidate(lv, s.par, s.solutions, fails, spec.name, seed)
    return None
