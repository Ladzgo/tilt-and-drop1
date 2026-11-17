"""Normative flood: breadth-first from all sources at once (in source order), leaks, contamination."""
from __future__ import annotations

from .model import DX, DY, FlowLevel

PLAYING, WON, LOST = "playing", "won", "lost"


def flood(level: FlowLevel, rot: dict | None = None, on_rotatable=None):
    """Returns (colour mask per cell, leaks as sorted list of (cell, dir), order of first arrival [(cell, step)]),
    or None if on_rotatable(i) asks to abort (solver branching)."""
    w, h = level.w, level.h
    colour = [0] * (w * h)
    dist = [-1] * (w * h)
    leaks = set()
    queue = []
    for x, y, c, d in level.sources:
        i = y * w + x
        colour[i] |= 1 << c
        dist[i] = 0
        queue.append((i, c))
    head = 0
    mask_cache: dict[int, int] = {}

    def m(i):
        if i not in mask_cache:
            p = level.pipes[i]
            if p is not None and p[2] and (rot is None or i not in rot) and on_rotatable is not None:
                if on_rotatable(i):
                    raise _Abort
            mask_cache[i] = level.mask(i, rot)
        return mask_cache[i]

    try:
        while head < len(queue):
            i, c = queue[head]
            head += 1
            mi = m(i)
            x, y = i % w, i // w
            for d in range(4):
                if not mi >> d & 1:
                    continue
                nx, ny = x + DX[d], y + DY[d]
                if not (0 <= nx < w and 0 <= ny < h):
                    leaks.add((i, d))
                    continue
                n = ny * w + nx
                if not m(n) >> ((d + 2) & 3) & 1:
                    leaks.add((i, d))
                    continue
                if colour[n] >> c & 1:
                    continue
                colour[n] |= 1 << c
                if dist[n] < 0:
                    dist[n] = dist[i] + 1
                queue.append((n, c))
    except _Abort:
        return None
    return colour, sorted(leaks), dist


class _Abort(Exception):
    pass


def evaluate(level: FlowLevel, rot: dict | None = None):
    colour, leaks, dist = flood(level, rot)
    contaminated = [i for i, cm in enumerate(colour) if cm & (cm - 1)]
    cups_ok = [colour[y * level.w + x] == 1 << c for x, y, c, d in level.cups]
    win = all(cups_ok) and not leaks and not contaminated
    return {"colour": colour, "leaks": leaks, "contaminated": contaminated, "cups": cups_ok, "win": win, "dist": dist}


class FlowGame:
    def __init__(self, level: FlowLevel):
        self.level = level
        self.rot = {i: level.pipes[i][1] for i in level.rotatables}
        self.moves_left = level.moves
        self.moves_used = 0
        self._update()

    def _update(self) -> None:
        self.state = evaluate(self.level, self.rot)
        self.status = WON if self.state["win"] else LOST if self.moves_left <= 0 else PLAYING

    def tap(self, cell: int) -> bool:
        if self.status != PLAYING:
            raise RuntimeError(f"cannot tap: {self.status}")
        if cell not in self.rot:
            return False
        self.rot[cell] = (self.rot[cell] + 1) & 3
        self.moves_left -= 1
        self.moves_used += 1
        self._update()
        return True

    def add_moves(self, n: int) -> None:
        if self.status == WON:
            raise RuntimeError("won")
        self.moves_left += n
        self._update()
