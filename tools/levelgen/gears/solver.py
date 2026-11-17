"""Exact solver: enumerate peg layouts within the inventory; par = min toggles from the start layout."""
from __future__ import annotations

from dataclasses import dataclass
from itertools import combinations

from .model import GearLevel
from .sim import is_win


@dataclass
class GearSolution:
    par: int | None
    solutions: int          # winning layouts within the inventory
    taps: list[int]         # cell indices to toggle for one optimal solution
    layouts: list[int]      # all winning layouts (for hints / bot)


def solve(level: GearLevel, start: int | None = None, max_pegs: int = 18) -> GearSolution:
    pegs = level.pegs
    if len(pegs) > max_pegs:
        raise ValueError("too many pegs for exhaustive search")
    start = level.start_layout() if start is None else start
    wins = []
    for r in range(level.inventory + 1):
        for combo in combinations(range(len(pegs)), r):
            lay = sum(1 << k for k in combo)
            if is_win(level, lay):
                wins.append(lay)
    if not wins:
        return GearSolution(None, 0, [], [])
    best = min(wins, key=lambda l: (bin(l ^ start).count("1"), l))
    diff = best ^ start
    return GearSolution(bin(diff).count("1"), len(wins), [pegs[k] for k in range(len(pegs)) if diff >> k & 1], sorted(wins))


def tap_order(level: GearLevel, start: int, target: int) -> list[int]:
    """Removals first (frees inventory), then placements: always a legal sequence."""
    pegs = level.pegs
    rem = [pegs[k] for k in range(len(pegs)) if (start >> k & 1) and not (target >> k & 1)]
    add = [pegs[k] for k in range(len(pegs)) if not (start >> k & 1) and (target >> k & 1)]
    return rem + add
