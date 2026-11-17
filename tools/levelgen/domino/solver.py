"""Enumerate turner configurations: winning set, par (fewest clockwise taps from the start setting)."""
from __future__ import annotations

from dataclasses import dataclass
from itertools import product

from .model import DominoLevel
from .sim import wins


@dataclass
class DominoSolution:
    par: int | None
    solutions: int
    total: int
    best: tuple[int, ...] | None
    winning: list


def taps_between(a: tuple, b: tuple) -> int:
    return sum((y - x) & 3 for x, y in zip(a, b))


def solve(level: DominoLevel, start: tuple | None = None, max_turners: int = 8) -> DominoSolution:
    t = len(level.turners)
    if t > max_turners:
        raise ValueError("too many turners")
    start = tuple(start) if start is not None else level.start_config()
    win = [cfg for cfg in product(range(4), repeat=t) if wins(level, cfg)]
    if not win:
        return DominoSolution(None, 0, 4 ** t, None, [])
    best = min(win, key=lambda c: (taps_between(start, c), c))
    return DominoSolution(taps_between(start, best), len(win), 4 ** t, best, win)
