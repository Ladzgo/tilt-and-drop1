"""Normative chain simulation and the stateful game."""
from __future__ import annotations

import heapq

from .model import DX, DY, DominoLevel

PLAYING, WON, LOST = "playing", "won", "lost"


def run_chain(level: DominoLevel, config: tuple[int, ...] | None = None):
    """Simulates one push. Returns (falls, rung): falls = [(time, cell, out_dirs)] in processing order,
    rung = set of bell cells that rang."""
    w, h, cells = level.w, level.h, level.cells
    tdir = dict(zip(level.turners, config if config is not None else level.start_config()))
    fallen: set[int] = set()
    falls = []
    rung: set[int] = set()
    s = level.start
    heap = [(0, s, cells[s][1])]  # (time, cell, incoming direction) ; start "comes in" along its arrow
    while heap:
        t, c, d = heapq.heappop(heap)
        if c in fallen:
            continue
        fallen.add(c)
        k, a = cells[c]
        if k == "B":
            rung.add(c)
            falls.append((t, c, ()))
            continue
        if k == "d":
            outs = (d,)
        elif k == "t":
            outs = (tdir[c],)
        elif k in "TS":
            outs = (a,)
        else:  # splitter: both perpendicular sides, clockwise side first
            outs = ((d + 1) & 3, (d + 3) & 3)
        falls.append((t, c, outs))
        x, y = c % w, c // w
        for o in outs:
            nx, ny = x + DX[o], y + DY[o]
            if not (0 <= nx < w and 0 <= ny < h):
                continue
            n = ny * w + nx
            if cells[n][0] in "dtTYBS" and n not in fallen:
                heapq.heappush(heap, (t + 1, n, o))
    return falls, rung


def wins(level: DominoLevel, config: tuple[int, ...]) -> bool:
    return set(level.bells) <= run_chain(level, config)[1]


class DominoGame:
    def __init__(self, level: DominoLevel):
        self.level = level
        self.config = list(level.start_config())
        self.attempts_left = level.attempts
        self.pushes = 0
        self.taps = 0
        self.status = PLAYING
        self.last_rung: set[int] = set()

    def rotate(self, cell: int) -> bool:
        if self.status != PLAYING:
            raise RuntimeError(f"cannot rotate: {self.status}")
        ts = self.level.turners
        if cell not in ts:
            return False
        k = ts.index(cell)
        self.config[k] = (self.config[k] + 1) & 3
        self.taps += 1
        return True

    def push(self):
        if self.status != PLAYING:
            raise RuntimeError(f"cannot push: {self.status}")
        falls, rung = run_chain(self.level, tuple(self.config))
        self.pushes += 1
        self.attempts_left -= 1
        self.last_rung = rung
        if set(self.level.bells) <= rung:
            self.status = WON
        elif self.attempts_left <= 0:
            self.status = LOST
        return falls, rung

    def add_attempt(self) -> None:
        if self.status == WON:
            raise RuntimeError("won")
        self.attempts_left += 1
        self.status = PLAYING
