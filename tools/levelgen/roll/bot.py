"""Noisy-greedy Roll & Print player: heads for unfinished targets, values correct prints, avoids repeats."""
from __future__ import annotations

import math
import random

from .model import B, RollLevel
from .sim import solved, step


def _score(level: RollLevel, pos, faces, prints) -> float:
    s = 0.0
    todo = []
    for i in level.targets:
        req, got = level.tiles[i][1], prints.get(i, 0)
        if got == req:
            s += 6
        else:
            if got:
                s -= 1.5
            todo.append(i)
    if todo:
        x, y = pos
        dist = min(abs(i % level.w - x) + abs(i // level.w - y) for i in todo)
        s -= 0.6 * dist
        if any(faces[B] == level.tiles[i][1] for i in todo):
            s += 0.5
    return s


def _win_distance(level: RollLevel, depth_cap: int, node_cap: int = 150_000):
    """Explores the state graph to depth_cap; returns {state_key: moves to win} (missing = no win known)."""
    tg = level.targets
    key = lambda p, f, pr: (p, f, tuple(pr.get(i, 0) for i in tg))  # noqa: E731
    start = (level.cube, tuple(level.faces), {})
    k0 = key(*start)
    nodes, depth, order, rev = {k0: start}, {k0: 0}, [k0], {}
    head = 0
    while head < len(order) and len(order) < node_cap:
        k = order[head]
        head += 1
        if depth[k] >= depth_cap or solved(level, nodes[k][2]):
            continue
        p, f, pr = nodes[k]
        for d in range(4):
            r = step(level, p, f, pr, d)
            if r is None:
                continue
            nk = key(*r)
            rev.setdefault(nk, []).append(k)
            if nk not in depth:
                depth[nk] = depth[k] + 1
                nodes[nk] = r
                order.append(nk)
    dist = {k: 0 for k in order if solved(level, nodes[k][2])}
    queue = list(dist)
    while queue:
        k = queue.pop(0)
        for pk in rev.get(k, ()):
            if pk not in dist:
                dist[pk] = dist[k] + 1
                queue.append(pk)
    return dist, key


def fail_rate(level: RollLevel, limit: int, runs: int = 60, seed: int = 0, skill: float = 0.72, tau: float = 0.8) -> float:
    """Each move: with probability `skill` an optimal move (planning ahead), otherwise a noisy-greedy one
    (what an unsure player does). Fail rate then measures how forgiving the level is."""
    rng = random.Random(seed)
    dist, key = _win_distance(level, limit)
    fails = 0
    for _ in range(runs):
        pos, faces, prints = level.cube, tuple(level.faces), {}
        seen = {key(pos, faces, prints)}
        won = False
        for left in range(limit, 0, -1):
            opts = []
            for d in range(4):
                r = step(level, pos, faces, prints, d)
                if r is not None:
                    k = key(*r)
                    opts.append((r, k, dist.get(k, 999), _score(level, *r) - (2.5 if k in seen else 0.0)))
            if not opts:
                break
            if rng.random() < skill:
                r, k = min(opts, key=lambda o: (o[2], -o[3]))[:2]
            else:
                m = max(o[3] for o in opts)
                ws = [math.exp((o[3] - m) / tau) for o in opts]
                x = rng.random() * sum(ws)
                r, k = opts[-1][:2]
                for o, wgt in zip(opts, ws):
                    x -= wgt
                    if x <= 0:
                        r, k = o[:2]
                        break
            pos, faces, prints = r
            seen.add(k)
            if solved(level, prints):
                won = True
                break
        if not won:
            fails += 1
    return fails / runs
