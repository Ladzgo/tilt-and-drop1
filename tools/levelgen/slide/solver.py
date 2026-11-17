"""Slide Out solver: memoised DFS over (removed mask, tray colour counts)."""
from __future__ import annotations

from dataclasses import dataclass

from .model import SlideLevel
from .sim import WON, PLAYING, apply_tap, status_of


@dataclass
class SlideSolution:
    solvable: bool
    states: int
    dead_end_ratio: float
    avg_free: float
    order: list[int]       # one winning sequence of block indices
    truncated: bool


def solve(level: SlideLevel, removed: int = 0, tray: tuple = (), capacity: int | None = None, node_cap: int = 400_000) -> SlideSolution:
    cap = capacity or level.capacity
    n = len(level.blocks)
    memo: dict = {}
    free_sum = [0, 0]
    truncated = [False]

    def key(rm, tr):
        return rm, tuple(sorted(tr))

    def win(rm, tr) -> bool:
        k = key(rm, tr)
        if k in memo:
            return memo[k]
        st = status_of(level, rm, tr, cap)
        if st != PLAYING:
            memo[k] = st == WON
            return memo[k]
        if len(memo) > node_cap:
            truncated[0] = True
            return False
        memo[k] = False  # cycle guard (cannot happen: mask only grows)
        ok = False
        free = 0
        for b in range(n):
            r = apply_tap(level, rm, tr, b)
            if r is None:
                continue
            free += 1
            if win(r[0], r[1]):
                ok = True
        free_sum[0] += free
        free_sum[1] += 1
        memo[k] = ok
        return ok

    ok = win(removed, tray)
    order: list[int] = []
    if ok:
        rm, tr = removed, tray
        while status_of(level, rm, tr, cap) == PLAYING:
            for b in range(n):
                r = apply_tap(level, rm, tr, b)
                if r is not None and memo.get(key(r[0], r[1]), False) or (r is not None and status_of(level, r[0], r[1], cap) == WON):
                    order.append(b)
                    rm, tr = r[0], r[1]
                    break
            else:
                break
    nonterm = [k for k in memo if status_of(level, k[0], k[1], cap) == PLAYING]
    dead = sum(1 for k in nonterm if not memo[k])
    return SlideSolution(ok, len(memo), dead / len(nonterm) if nonterm else 0.0,
                         free_sum[0] / free_sum[1] if free_sum[1] else 0.0, order, truncated[0])
