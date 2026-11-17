"""Noisy-greedy player for fail-rate estimates: prefers mirrors the light touches, reads lit/partial
targets, avoids configurations it has already seen."""
from __future__ import annotations

import math
import random

from .model import PrismLevel, ROTATABLE
from .generator import _cells_on_segments
from .sim import trace


def _score(level: PrismLevel, received) -> float:
    s = 0.0
    for i in level.targets:
        req, got = level.cells[i][1], received[i]
        if got == req:
            s += 4
        elif got and got & ~req == 0:
            s += 1.5 * bin(got).count("1") / bin(req).count("1")
        elif got:
            s -= 1
    return s


def fail_rate(level: PrismLevel, limit: int, runs: int = 60, seed: int = 0, tau: float = 0.7, eps: float = 0.08) -> float:
    rng = random.Random(seed)
    rot = level.rotatables
    cache: dict = {}

    def evaluate(states: tuple):
        if states not in cache:
            segs: list = []
            rec = trace(level, dict(zip(rot, states)), segments=segs)
            touched = {i for i, _ in _cells_on_segments(level, segs) if level.cells[i][0] in ROTATABLE}
            win = all(rec[i] == level.cells[i][1] for i in level.targets)
            cache[states] = (_score(level, rec), touched, win)
        return cache[states]

    start = tuple(level.cells[i][1] for i in rot)
    fails = 0
    for _ in range(runs):
        st, seen = start, {start}
        won = evaluate(st)[2]
        for _ in range(limit):
            if won:
                break
            touched = evaluate(st)[1]
            cands = [k for k, i in enumerate(rot) if i in touched] or list(range(len(rot)))
            if rng.random() < 0.15:  # sometimes pokes a mirror the light doesn't reach
                cands = list(range(len(rot)))
            if rng.random() < eps:
                k = rng.choice(cands)
            else:
                scores = []
                for k in cands:
                    nxt = st[:k] + (st[k] ^ 1,) + st[k + 1:]
                    scores.append(evaluate(nxt)[0] - (3.0 if nxt in seen else 0.0))
                m = max(scores)
                ws = [math.exp((x - m) / tau) for x in scores]
                r = rng.random() * sum(ws)
                k = cands[-1]
                for c, wgt in zip(cands, ws):
                    r -= wgt
                    if r <= 0:
                        k = c
                        break
            st = st[:k] + (st[k] ^ 1,) + st[k + 1:]
            seen.add(st)
            won = evaluate(st)[2]
        if not won:
            fails += 1
    return fails / runs
