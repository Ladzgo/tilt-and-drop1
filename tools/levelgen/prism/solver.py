"""Exact solver. Taps commute, so a solution is a configuration; par = fewest pieces that differ from
the start. Branches only on rotatable pieces the light actually reaches (others never matter)."""
from __future__ import annotations

from dataclasses import dataclass

from .model import PrismLevel
from .sim import is_win, trace


@dataclass
class PrismSolution:
    par: int | None
    solutions: int          # distinct relevant configurations that win
    taps: list[int]         # cell indices to tap for one optimal solution
    nodes: int


def solve(level: PrismLevel, start: dict[int, int] | None = None, node_cap: int = 200_000) -> PrismSolution:
    start = start if start is not None else {i: level.cells[i][1] for i in level.rotatables}
    best: list = [None, []]
    count = 0
    nodes = 0

    def rec(assign: dict[int, int]) -> None:
        nonlocal count, nodes
        nodes += 1
        if nodes > node_cap:
            return
        pending: list[int] = []

        def branch(i: int) -> bool:
            pending.append(i)
            return True

        rec_received = trace(level, assign, on_rotatable=branch)
        if rec_received is None:
            i = pending[0]
            for st in (start[i], start[i] ^ 1):  # keep first: finds cheap solutions early
                assign[i] = st
                rec(assign)
                del assign[i]
            return
        if is_win(level, rec_received):
            count += 1
            taps = sorted(i for i, st in assign.items() if st != start[i])
            if best[0] is None or len(taps) < best[0]:
                best[0], best[1] = len(taps), taps

    rec({})
    return PrismSolution(best[0], count, best[1], nodes)
