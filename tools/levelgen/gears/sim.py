"""Spin propagation (normative) and the stateful game. A layout is a bitmask over level.pegs."""
from __future__ import annotations

from .model import GearLevel

PLAYING, WON, LOST = "playing", "won", "lost"


def neighbours(w: int, h: int, i: int) -> list[int]:
    """Hex grid, odd rows shifted right ("odd-r"). Order is fixed (it is part of the normative rules)."""
    x, y = i % w, i // w
    if y % 2 == 0:
        cand = ((x + 1, y), (x - 1, y), (x - 1, y - 1), (x, y - 1), (x - 1, y + 1), (x, y + 1))
    else:
        cand = ((x + 1, y), (x - 1, y), (x, y - 1), (x + 1, y - 1), (x, y + 1), (x + 1, y + 1))
    return [ny * w + nx for nx, ny in cand if 0 <= nx < w and 0 <= ny < h]


def gear_at(level: GearLevel, layout: int, peg_index: dict[int, int], i: int) -> bool:
    c = level.cells[i]
    if c in "GMRL":
        return True
    if c in "og":
        return bool(layout >> peg_index[i] & 1)
    return False


def spins(level: GearLevel, layout: int) -> tuple[list[int], list[bool]]:
    """Returns (spin per cell: +1 CW, -1 CCW, 0 still; jammed flag per cell)."""
    w, h, n = level.w, level.h, level.w * level.h
    peg_index = {i: k for k, i in enumerate(level.pegs)}
    is_gear = [gear_at(level, layout, peg_index, i) for i in range(n)]
    spin = [0] * n
    jam = [False] * n
    seen = [False] * n
    for start in range(n):
        if not is_gear[start] or seen[start]:
            continue
        comp, stack = [], [start]
        seen[start] = True
        while stack:
            i = stack.pop()
            comp.append(i)
            for j in neighbours(w, h, i):
                if is_gear[j] and not seen[j]:
                    seen[j] = True
                    stack.append(j)
        motors = sorted(i for i in comp if level.cells[i] == "M")
        if not motors:
            continue
        val: dict[int, int] = {}
        ok = True
        queue = []
        for m in motors:
            val[m] = 1
            queue.append(m)
        head = 0
        while head < len(queue) and ok:
            i = queue[head]
            head += 1
            for j in neighbours(w, h, i):
                if not is_gear[j]:
                    continue
                if j in val:
                    if val[j] == val[i]:
                        ok = False
                        break
                else:
                    val[j] = -val[i]
                    queue.append(j)
        for i in comp:
            if ok:
                spin[i] = val[i]
            else:
                jam[i] = True
    return spin, jam


def is_win(level: GearLevel, layout: int) -> bool:
    spin, _ = spins(level, layout)
    outs = level.outputs
    return bool(outs) and all(spin[i] == (1 if level.cells[i] == "R" else -1) for i in outs)


class GearGame:
    def __init__(self, level: GearLevel):
        self.level = level
        self.layout = level.start_layout()
        self.moves_left = level.moves
        self.moves_used = 0
        self._update()

    @property
    def in_hand(self) -> int:
        return self.level.inventory - bin(self.layout).count("1")

    def _update(self) -> None:
        self.status = WON if is_win(self.level, self.layout) else LOST if self.moves_left <= 0 else PLAYING

    def tap(self, cell: int) -> bool:
        if self.status != PLAYING:
            raise RuntimeError(f"cannot tap: {self.status}")
        pegs = self.level.pegs
        if cell not in pegs:
            return False
        k = pegs.index(cell)
        if not self.layout >> k & 1 and self.in_hand <= 0:
            return False
        self.layout ^= 1 << k
        self.moves_left -= 1
        self.moves_used += 1
        self._update()
        return True

    def add_moves(self, n: int) -> None:
        if self.status == WON:
            raise RuntimeError("won")
        self.moves_left += n
        self._update()
