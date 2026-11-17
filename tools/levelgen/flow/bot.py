"""Flow Fill player: sometimes turns the right pipe (knows a solution), otherwise turns pipes the water
touches to reduce leaks and fill cups."""
from __future__ import annotations

import math
import random

from .model import BASE, DX, DY, FlowLevel, rot_mask
from .sim import evaluate


def _score(st) -> float:
    return 4 * sum(st["cups"]) - 1.0 * len(st["leaks"]) - 2.5 * len(st["contaminated"]) + 0.15 * sum(1 for c in st["colour"] if c)


def fail_rate(level: FlowLevel, limit: int, target: dict, runs: int = 60, seed: int = 0, skill: float = 0.5, tau: float = 0.7) -> float:
    rng = random.Random(seed)
    rots = level.rotatables
    cache: dict = {}

    def ev(rot: tuple):
        if rot not in cache:
            cache[rot] = evaluate(level, dict(zip(rots, rot)))
        return cache[rot]

    tgt_mask = {i: rot_mask(BASE[level.pipes[i][0]], r) for i, r in (target or {}).items()}
    fails = 0
    for _ in range(runs):
        cur = tuple(level.pipes[i][1] for i in rots)
        seen = {cur}
        for _ in range(limit):
            st = ev(cur)
            if st["win"]:
                break
            wet = {i for i, c in enumerate(st["colour"]) if c}
            near = set()
            for i in wet:
                x, y = i % level.w, i // level.w
                for d in range(4):
                    nx, ny = x + DX[d], y + DY[d]
                    if 0 <= nx < level.w and 0 <= ny < level.h:
                        near.add(ny * level.w + nx)
            cands = [k for k, i in enumerate(rots) if i in wet or i in near] or list(range(len(rots)))
            if rng.random() < skill:
                good = [k for k in cands if rots[k] in tgt_mask and rot_mask(BASE[level.pipes[rots[k]][0]], cur[k]) != tgt_mask[rots[k]]]
                if good:
                    k = rng.choice(good)
                    cur = cur[:k] + ((cur[k] + 1) & 3,) + cur[k + 1:]
                    seen.add(cur)
                    continue
            opts = [cur[:k] + ((cur[k] + 1) & 3,) + cur[k + 1:] for k in cands]
            sc = [_score(ev(o)) - (2.0 if o in seen else 0.0) for o in opts]
            m = max(sc)
            ws = [math.exp((x - m) / tau) for x in sc]
            r = rng.random() * sum(ws)
            cur = opts[-1]
            for o, wgt in zip(opts, ws):
                r -= wgt
                if r <= 0:
                    cur = o
                    break
            seen.add(cur)
        if not ev(cur)["win"]:
            fails += 1
    return fails / runs
