"""BFS over sheets: par, shortest-solution count, one solution, dead-end ratio."""
from __future__ import annotations

from dataclasses import dataclass

from .model import FoldLevel
from .sim import fold, initial_sheet, legal_folds, solved


@dataclass
class FoldSolution:
    par: int | None
    solutions: int
    path: list
    states: int
    dead_end_ratio: float


def solve(level: FoldLevel, start=None, max_depth: int = 12) -> FoldSolution:
    start = start or initial_sheet(level)
    if solved(level, start):
        return FoldSolution(0, 1, [], 1, 0.0)
    depth, paths, parent = {start: 0}, {start: 1}, {start: None}
    order, kids = [start], {}
    head, win_depth, wins = 0, None, []
    while head < len(order):
        s = order[head]
        head += 1
        if depth[s] >= max_depth or (win_depth is not None and depth[s] >= win_depth):
            continue
        kids[s] = []
        for mv in legal_folds(s):
            ns = fold(s, *mv)
            kids[s].append(ns)
            if ns not in depth:
                depth[ns] = depth[s] + 1
                paths[ns] = paths[s]
                parent[ns] = (s, mv)
                order.append(ns)
                if solved(level, ns):
                    win_depth = win_depth or depth[ns]
                    wins.append(ns)
            elif depth[ns] == depth[s] + 1:
                paths[ns] += paths[s]
    if win_depth is None:
        return FoldSolution(None, 0, [], len(order), 1.0)
    path, s = [], wins[0]
    while parent[s]:
        ps, mv = parent[s]
        path.append(list(mv))
        s = ps
    # dead ends: expanded states (depth < par) whose subtree contains no win
    good = set(w for w in wins)
    for s in reversed(order):
        if any(k in good for k in kids.get(s, ())):
            good.add(s)
    pool = [s for s in order if 1 <= depth[s] < win_depth]
    dead = sum(1 for s in pool if s not in good)
    return FoldSolution(win_depth, sum(paths[w] for w in wins if depth[w] == win_depth), path[::-1], len(order), dead / len(pool) if pool else 0.0)
