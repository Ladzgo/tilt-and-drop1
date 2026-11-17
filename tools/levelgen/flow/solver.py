"""Exact solver: branch on each rotatable pipe the first time water reaches it (distinct masks only,
cheapest clockwise distance), prune leaks/contamination as soon as they appear."""
from __future__ import annotations

from dataclasses import dataclass

from .model import BASE, FlowLevel, rot_mask
from .sim import evaluate, flood


@dataclass
class FlowSolution:
    par: int | None
    solutions: int
    rot: dict | None          # one optimal rotation assignment for reached pipes
    nodes: int


def options(level: FlowLevel, i: int, start_r: int) -> list[tuple[int, int]]:
    """(rotation, cost) for each distinct mask, cheapest rotation first, in order of cost then rotation."""
    seen: dict[int, tuple[int, int]] = {}
    for k in range(4):
        r = (start_r + k) & 3
        mk = rot_mask(BASE[level.pipes[i][0]], r)
        if mk not in seen:
            seen[mk] = (r, k)
    return sorted(seen.values(), key=lambda t: (t[1], t[0]))


def solve(level: FlowLevel, start: dict | None = None, node_cap: int = 300_000) -> FlowSolution:
    start = start if start is not None else {i: level.pipes[i][1] for i in level.rotatables}
    best: list = [None, None]
    count = 0
    nodes = 0

    def rec(assign: dict, cost: int):
        nonlocal count, nodes
        nodes += 1
        if nodes > node_cap:
            return
        pending = []

        def br(i):
            pending.append(i)
            return True

        r = flood(level, assign, on_rotatable=br)
        if r is None:
            i = pending[0]
            for rr, c in options(level, i, start[i]):
                assign[i] = rr
                # prune: a fully known flood with a leak/contamination can't recover, but only check after branching
                rec(assign, cost + c)
                del assign[i]
            return
        colour, leaks, _ = r
        if leaks or any(cm & (cm - 1) for cm in colour):
            return
        if all(colour[y * level.w + x] == 1 << c for x, y, c, d in level.cups):
            count += 1
            if best[0] is None or cost < best[0]:
                best[0], best[1] = cost, dict(assign)

    rec({}, 0)
    return FlowSolution(best[0], count, best[1], nodes)


def taps_for(level: FlowLevel, start: dict, target: dict) -> list[int]:
    out = []
    for i, r in sorted(target.items()):
        out += [i] * ((r - start[i]) & 3)
    return out
