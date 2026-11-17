"""Beam tracing and the stateful game (normative; JS/C# ports must match the golden vectors)."""
from __future__ import annotations

from .model import DX, DY, EMPTY, FILTER, FIXED, MIRROR, SOURCE, SPLITTER, TARGET, WALL, PrismLevel

# reflections, y down: '/' (dx,dy)->(-dy,-dx); '\' (dx,dy)->(dy,dx). As direction indices 0>,1v,2<,3^:
REFLECT = ((3, 2, 1, 0), (1, 0, 3, 2))  # REFLECT[state][dir]

Segment = tuple  # (color, x0, y0, x1, y1)  path pieces in cell coordinates, for rendering


def trace(level: PrismLevel, states: dict[int, int] | None = None, segments: list | None = None,
          on_rotatable=None) -> list[int] | None:
    """Traces every source. `states` maps rotatable cell index -> state (missing = level's own state).
    Returns received mask per cell index (only targets non-zero). If `on_rotatable(index)` returns True for a
    rotatable cell with no entry in `states`, tracing aborts and returns None (used by the solver to branch)."""
    w, h, cells = level.w, level.h, level.cells
    received = [0] * (w * h)
    for s in level.sources:
        a = cells[s][1]
        color, d = a >> 2, a & 3
        stack = [(s % w, s // w, d)]
        seen: set[tuple[int, int, int]] = set()
        while stack:
            x, y, d = stack.pop()
            sx, sy = x, y
            while True:
                nx, ny = x + DX[d], y + DY[d]
                if not (0 <= nx < w and 0 <= ny < h):
                    if segments is not None:
                        segments.append((color, sx, sy, nx - DX[d] * 0.5, ny - DY[d] * 0.5))
                    break
                x, y = nx, ny
                i = y * w + x
                if (i, d) in seen:
                    if segments is not None:
                        segments.append((color, sx, sy, x, y))
                    break
                seen.add((i, d))
                k, v = cells[i]
                if k in (EMPTY,):
                    continue
                if k == FILTER:
                    if v == color:
                        continue
                    if segments is not None:
                        segments.append((color, sx, sy, x, y))
                    break
                if k in (WALL, SOURCE, TARGET):
                    if k == TARGET:
                        received[i] |= color
                    if segments is not None:
                        segments.append((color, sx, sy, x, y))
                    break
                # mirrors / splitters
                if k in (MIRROR, SPLITTER):
                    if states is not None and i in states:
                        st = states[i]
                    elif on_rotatable is not None and on_rotatable(i):
                        return None
                    else:
                        st = v
                else:  # FIXED
                    st = v
                if segments is not None:
                    segments.append((color, sx, sy, x, y))
                if k == SPLITTER:
                    stack.append((x, y, d))  # straight-through branch continues from here
                d = REFLECT[st][d]
                sx, sy = x, y
    return received


def lit(level: PrismLevel, received: list[int]) -> tuple[int, int]:
    """(number of targets lit, number of targets) ."""
    t = level.targets
    return sum(1 for i in t if received[i] == level.cells[i][1]), len(t)


def is_win(level: PrismLevel, received: list[int]) -> bool:
    n, total = lit(level, received)
    return n == total and total > 0


PLAYING, WON, LOST = "playing", "won", "lost"


class PrismGame:
    def __init__(self, level: PrismLevel):
        self.level = level
        self.states = {i: level.cells[i][1] for i in level.rotatables}
        self.moves_left = level.moves
        self.moves_used = 0
        self.received = trace(level, self.states)
        self.status = PLAYING
        self._update()

    def _update(self) -> None:
        if is_win(self.level, self.received):
            self.status = WON
        elif self.moves_left <= 0:
            self.status = LOST
        else:
            self.status = PLAYING

    def tap(self, index: int) -> bool:
        """Toggles a rotatable piece. Returns False (no move spent) for anything else."""
        if self.status != PLAYING:
            raise RuntimeError(f"cannot tap: {self.status}")
        if index not in self.states:
            return False
        self.states[index] ^= 1
        self.moves_left -= 1
        self.moves_used += 1
        self.received = trace(self.level, self.states)
        self._update()
        return True

    def add_moves(self, n: int) -> None:
        if self.status == WON:
            raise RuntimeError("won")
        self.moves_left += n
        self._update()
