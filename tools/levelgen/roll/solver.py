"""BFS solver for Roll & Print: par, shortest-solution count, dead-end ratio, first move (hint)."""
from __future__ import annotations

from dataclasses import dataclass

from .model import RollLevel
from .sim import solved, state_key, step


@dataclass
class RollSolution:
    par: int | None
    solutions: int
    dead_end_ratio: float
    states: int
    path: list[int]


def solve(level: RollLevel, pos=None, faces=None, prints=None, max_depth: int = 30, node_cap: int = 300_000) -> RollSolution:
    pos = pos or level.cube
    faces = tuple(faces or level.faces)
    prints = dict(prints or {})
    tg = level.targets
    start = state_key(pos, faces, prints, tg)
    nodes = {start: (pos, faces, prints)}
    depth = {start: 0}
    parents: dict = {start: []}
    paths = {start: 1}
    order = [start]
    head = 0
    win_depth = None
    wins = []
    if solved(level, prints):
        return RollSolution(0, 1, 0.0, 1, [])
    children: dict = {}
    while head < len(order):
        k = order[head]
        head += 1
        d0 = depth[k]
        if win_depth is not None and d0 >= win_depth:
            continue
        if d0 >= max_depth or len(order) > node_cap:
            break
        p, f, pr = nodes[k]
        kids = []
        for d in range(4):
            r = step(level, p, f, pr, d)
            if r is None:
                continue
            nk = state_key(r[0], r[1], r[2], tg)
            kids.append((d, nk))
            if nk not in depth:
                depth[nk] = d0 + 1
                nodes[nk] = r
                parents[nk] = [(k, d)]
                paths[nk] = paths[k]
                order.append(nk)
                if solved(level, r[2]):
                    win_depth = d0 + 1 if win_depth is None else win_depth
                    wins.append(nk)
            elif depth[nk] == d0 + 1:
                parents[nk].append((k, d))
                paths[nk] += paths[k]
        children[k] = kids
    if win_depth is None:
        return RollSolution(None, 0, 1.0, len(order), [])
    # one optimal path
    path, k = [], wins[0]
    while parents[k]:
        pk, d = parents[k][0]
        path.append(d)
        k = pk
    path.reverse()
    # dead ends: fully expanded states (depth 1..par-1) from which no win is reachable in the explored graph
    win_set = {k for k in order if solved(level, nodes[k][2])}
    rev: dict = {}
    for k, kids in children.items():
        for _, nk in kids:
            rev.setdefault(nk, []).append(k)
    good, queue = set(win_set), list(win_set)
    while queue:
        k = queue.pop()
        for pk in rev.get(k, ()):
            if pk not in good:
                good.add(pk)
                queue.append(pk)
    pool = [k for k in order if 1 <= depth[k] < win_depth and k not in win_set]
    dead = sum(1 for k in pool if k not in good)
    return RollSolution(win_depth, sum(paths[k] for k in wins if depth[k] == win_depth), dead / len(pool) if pool else 0.0, len(order), path)
