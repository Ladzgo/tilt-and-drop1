"""Normative Slide Out rules and the stateful game."""
from __future__ import annotations

from .model import DX, DY, SlideLevel

PLAYING, WON, LOST = "playing", "won", "lost"


def blocker(level: SlideLevel, removed: int, k: int) -> int | None:
    """Cell index of the first obstacle on block k's path, or None if the path is clear."""
    blocks = level.blocks
    idx = {cell: j for j, cell in enumerate(blocks)}
    i = blocks[k]
    colour, d = level.cells[i]
    x, y = i % level.w, i // level.w
    while True:
        x, y = x + DX[d], y + DY[d]
        if not (0 <= x < level.w and 0 <= y < level.h):
            return None
        j = y * level.w + x
        c = level.cells[j]
        if c is None:
            continue
        if isinstance(c, tuple) and removed >> idx[j] & 1:
            continue
        return j


def apply_tap(level: SlideLevel, removed: int, tray: tuple, k: int):
    """Returns (removed, tray, matched) or None for a bump / already removed. Tray is an ordered tuple of colours."""
    if removed >> k & 1 or blocker(level, removed, k) is not None:
        return None
    colour = level.cells[level.blocks[k]][0]
    tray = tray + (colour,)
    matched = False
    if tray.count(colour) >= 3:
        lst, n = [], 0
        for c in tray:
            if c == colour and n < 3:
                n += 1
                continue
            lst.append(c)
        tray = tuple(lst)
        matched = True
    return removed | (1 << k), tray, matched


def status_of(level: SlideLevel, removed: int, tray: tuple, capacity: int) -> str:
    if removed == (1 << len(level.blocks)) - 1 and not tray:
        return WON
    if len(tray) >= capacity:
        return LOST
    return PLAYING


class SlideGame:
    def __init__(self, level: SlideLevel):
        self.level = level
        self.removed = 0
        self.tray: tuple = ()
        self.capacity = level.capacity
        self.taps = 0
        self.status = status_of(level, 0, (), self.capacity)

    def tap_cell(self, cell: int):
        """Tap the board cell. Returns 'exit', 'match', 'bump' or None (no block there)."""
        if self.status != PLAYING:
            raise RuntimeError(f"cannot tap: {self.status}")
        blocks = self.level.blocks
        if cell not in blocks:
            return None
        k = blocks.index(cell)
        if self.removed >> k & 1:
            return None
        r = apply_tap(self.level, self.removed, self.tray, k)
        if r is None:
            return "bump"
        self.removed, self.tray, matched = r
        self.taps += 1
        self.status = status_of(self.level, self.removed, self.tray, self.capacity)
        return "match" if matched else "exit"

    def add_slots(self, n: int = 2) -> None:
        if self.status == WON:
            raise RuntimeError("won")
        self.capacity += n
        self.status = status_of(self.level, self.removed, self.tray, self.capacity)
