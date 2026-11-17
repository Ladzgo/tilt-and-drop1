"""Normative folding: sheets are tuples of rows of stacks; a stack is a tuple of (square, face_up) top first."""
from __future__ import annotations

from .model import FoldLevel

PLAYING, WON, LOST = "playing", "won", "lost"
Sheet = tuple  # tuple[tuple[tuple[tuple[int,int],...],...],...]   rows -> cells -> stack


def initial_sheet(level: FoldLevel) -> Sheet:
    return tuple(tuple(((y * level.w + x, 1),) for x in range(level.w)) for y in range(level.h))


def flip(stack):
    return tuple((s, 1 - f) for s, f in reversed(stack))


def legal_folds(sheet: Sheet) -> list[tuple[str, int, int]]:
    """All folds (axis 'V' vertical crease / 'H' horizontal crease, k, side) in fixed order."""
    h, w = len(sheet), len(sheet[0])
    out = []
    for k in range(1, w):
        if k <= w - k:
            out.append(("V", k, 0))
        if w - k <= k:
            out.append(("V", k, 1))
    for k in range(1, h):
        if k <= h - k:
            out.append(("H", k, 0))
        if h - k <= k:
            out.append(("H", k, 1))
    return out


def fold(sheet: Sheet, axis: str, k: int, side: int) -> Sheet | None:
    h, w = len(sheet), len(sheet[0])
    if axis == "V":
        if not 1 <= k < w:
            return None
        if side == 0:
            if k > w - k:
                return None
            rows = []
            for y in range(h):
                row = []
                for xn in range(w - k):
                    c = k + xn
                    m = k - 1 - xn
                    st = sheet[y][c]
                    if m >= 0:
                        st = flip(sheet[y][m]) + st
                    row.append(st)
                rows.append(tuple(row))
            return tuple(rows)
        if w - k > k:
            return None
        rows = []
        for y in range(h):
            row = []
            for xn in range(k):
                m = 2 * k - 1 - xn
                st = sheet[y][xn]
                if m < w:
                    st = flip(sheet[y][m]) + st
                row.append(st)
            rows.append(tuple(row))
        return tuple(rows)
    if axis == "H":
        t = tuple(zip(*sheet))  # transpose: columns as rows
        r = fold(t, "V", k, side)
        return None if r is None else tuple(zip(*r))
    return None


def visible(level: FoldLevel, sheet: Sheet) -> list[str]:
    return ["".join(str(level.front[st[0][0]] if st[0][1] else level.back[st[0][0]]) for st in row) for row in sheet]


def solved(level: FoldLevel, sheet: Sheet) -> bool:
    return visible(level, sheet) == level.target


class FoldGame:
    def __init__(self, level: FoldLevel):
        self.level = level
        self.sheet = initial_sheet(level)
        self.history: list = []
        self.moves_left = level.moves
        self.moves_used = 0
        self._update()

    def _update(self) -> None:
        self.status = WON if solved(self.level, self.sheet) else LOST if self.moves_left <= 0 else PLAYING

    def fold(self, axis: str, k: int, side: int) -> bool:
        if self.status != PLAYING:
            raise RuntimeError(f"cannot fold: {self.status}")
        r = fold(self.sheet, axis, k, side)
        if r is None:
            return False
        self.history.append(self.sheet)
        self.sheet = r
        self.moves_left -= 1
        self.moves_used += 1
        self._update()
        return True

    def add_moves(self, n: int) -> None:
        if self.status == WON:
            raise RuntimeError("won")
        self.moves_left += n
        self._update()
