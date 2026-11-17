"""Fold player: sometimes folds correctly (knows the distance to the target), otherwise picks folds that
seem to bring matching colours to the top."""
from __future__ import annotations

import math
import random

from .model import FoldLevel
from .sim import fold, initial_sheet, legal_folds, solved, visible


def _dist_map(level: FoldLevel, limit: int):
    start = initial_sheet(level)
    order, depth, kids = [start], {start: 0}, {}
    head = 0
    while head < len(order):
        s = order[head]
        head += 1
        if depth[s] >= limit or solved(level, s):
            continue
        kids[s] = []
        for mv in legal_folds(s):
            ns = fold(s, *mv)
            kids[s].append(ns)
            if ns not in depth:
                depth[ns] = depth[s] + 1
                order.append(ns)
    dist = {s: 0 for s in order if solved(level, s)}
    changed = True
    while changed:
        changed = False
        for s, ks in kids.items():
            d = min((dist[k] + 1 for k in ks if k in dist), default=None)
            if d is not None and dist.get(s, 99) > d:
                dist[s] = d
                changed = True
    return dist


def _score(level: FoldLevel, sheet) -> float:
    v = visible(level, sheet)
    tgt = level.target
    th, tw = len(tgt), len(tgt[0])
    h, w = len(v), len(v[0])
    tcols = "".join(tgt)
    have = "".join(v)
    common = sum(min(tcols.count(c), have.count(c)) for c in set(tcols))
    shape = -abs(h * w - th * tw) * 0.3
    exact = sum(1 for y in range(min(h, th)) for x in range(min(w, tw)) if v[y][x] == tgt[y][x]) if (h, w) == (th, tw) else 0
    return common * 0.6 + shape + exact * 1.5


def fail_rate(level: FoldLevel, limit: int, runs: int = 60, seed: int = 0, skill: float = 0.62, tau: float = 0.6) -> float:
    rng = random.Random(seed)
    dist = _dist_map(level, limit)
    fails = 0
    for _ in range(runs):
        s = initial_sheet(level)
        for left in range(limit, 0, -1):
            if solved(level, s):
                break
            opts = [fold(s, *m) for m in legal_folds(s)]
            if not opts:
                break
            if rng.random() < skill:
                good = [o for o in opts if dist.get(o, 99) <= left - 1]
                if good:
                    s = min(good, key=lambda o: dist[o])
                    continue
            sc = [_score(level, o) for o in opts]
            m = max(sc)
            ws = [math.exp((x - m) / tau) for x in sc]
            r = rng.random() * sum(ws)
            s = opts[-1]
            for o, wgt in zip(opts, ws):
                r -= wgt
                if r <= 0:
                    s = o
                    break
        if not solved(level, s):
            fails += 1
    return fails / runs
