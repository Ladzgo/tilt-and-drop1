"""Slide Out player: likes to complete pairs in the tray (the natural greedy habit), sometimes plans
(chooses a still-winnable tap). Fail rate measures how hard the greedy pull is to resist."""
from __future__ import annotations

import math
import random

from .model import SlideLevel
from .sim import PLAYING, WON, apply_tap, status_of


def _winnable(level: SlideLevel, cap: int):
    memo: dict = {}
    n = len(level.blocks)

    def win(rm, tr):
        k = (rm, tuple(sorted(tr)))
        if k in memo:
            return memo[k]
        st = status_of(level, rm, tr, cap)
        if st != PLAYING:
            memo[k] = st == WON
            return memo[k]
        memo[k] = False
        ok = False
        for b in range(n):
            r = apply_tap(level, rm, tr, b)
            if r is not None and win(r[0], r[1]):
                ok = True
        memo[k] = ok
        return ok

    return win


def fail_rate(level: SlideLevel, runs: int = 60, seed: int = 0, skill: float = 0.45, tau: float = 0.7) -> float:
    rng = random.Random(seed)
    cap, n = level.capacity, len(level.blocks)
    win = _winnable(level, cap)
    fails = 0
    for _ in range(runs):
        rm, tr = 0, ()
        while status_of(level, rm, tr, cap) == PLAYING:
            opts = []
            for b in range(n):
                r = apply_tap(level, rm, tr, b)
                if r is None:
                    continue
                c = level.cells[level.blocks[b]][0]
                have = tr.count(c)
                score = {2: 5.0, 1: 2.0}.get(have, -1.0 * len(tr) / cap)
                opts.append((r, score))
            if not opts:
                break
            if rng.random() < skill:
                good = [o for o in opts if win(o[0][0], o[0][1])]
                if good:
                    opts = good
            m = max(o[1] for o in opts)
            ws = [math.exp((o[1] - m) / tau) for o in opts]
            x = rng.random() * sum(ws)
            r = opts[-1][0]
            for o, wgt in zip(opts, ws):
                x -= wgt
                if x <= 0:
                    r = o[0]
                    break
            rm, tr = r[0], r[1]
        if status_of(level, rm, tr, cap) != WON:
            fails += 1
    return fails / runs
