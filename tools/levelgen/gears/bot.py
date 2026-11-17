"""Gear Works player: sometimes knows the way (moves toward the nearest winning layout), otherwise
greedily chases lit dials and avoids jams it can see."""
from __future__ import annotations

import math
import random

from .model import GearLevel
from .sim import spins


def _score(level: GearLevel, layout: int) -> float:
    spin, jam = spins(level, layout)
    s = 0.0
    for o in level.outputs:
        want = 1 if level.cells[o] == "R" else -1
        if spin[o] == want:
            s += 4
        elif spin[o] == -want:
            s -= 0.5
        elif jam[o]:
            s -= 2
    return s - (1.5 if any(jam) else 0.0)


def fail_rate(level: GearLevel, limit: int, wins: list[int], runs: int = 60, seed: int = 0, skill: float = 0.55, tau: float = 0.7) -> float:
    rng = random.Random(seed)
    pegs = level.pegs
    cache: dict = {}
    win_set = set(wins)

    def score(l):
        if l not in cache:
            cache[l] = _score(level, l)
        return cache[l]

    def dist(l):
        return min(bin(l ^ wl).count("1") for wl in wins) if wins else 99

    fails = 0
    for _ in range(runs):
        lay, seen = level.start_layout(), set()
        seen.add(lay)
        for _ in range(limit):
            if lay in win_set:
                break
            in_hand = level.inventory - bin(lay).count("1")
            opts = [lay ^ (1 << k) for k in range(len(pegs)) if (lay >> k & 1) or in_hand > 0]
            if not opts:
                break
            if rng.random() < skill:
                d0 = dist(lay)
                better = [o for o in opts if dist(o) < d0]
                if better:
                    opts = better
            sc = [score(o) - (2.0 if o in seen else 0.0) for o in opts]
            m = max(sc)
            ws = [math.exp((x - m) / tau) for x in sc]
            r = rng.random() * sum(ws)
            lay = opts[-1]
            for o, wgt in zip(opts, ws):
                r -= wgt
                if r <= 0:
                    lay = o
                    break
            seen.add(lay)
        if lay not in win_set:
            fails += 1
    return fails / runs
