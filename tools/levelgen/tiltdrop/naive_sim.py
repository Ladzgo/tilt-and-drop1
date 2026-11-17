"""Independent test oracle: moves pieces ONE cell per pass (nearest-first).

Valid for MVP piece types without anchor/ice (those depend on slide boundaries).
Used only by tests to cross-check sim.settle on random boards.
"""
from __future__ import annotations

from .model import BLOCK, DX, DY, EMPTY, GRAVITY_EDGE, STONE


def settle_naive(cells: list[int], w: int, h: int, g: int, gates: dict[str, bytes]) -> list[int]:
    cells = list(cells)
    line = gates[GRAVITY_EDGE[g]]
    dx, dy = DX[g], DY[g]

    def key(i: int) -> tuple[int, int]:
        x, y = i % w, i // w
        return [(h - 1 - y, x), (w - 1 - x, y), (y, x), (x, y)][g]

    order = sorted(range(w * h), key=key)
    while True:
        changed = False
        for i in order:
            c = cells[i]
            if c & 0xF0 not in (BLOCK, STONE):
                continue
            x, y = i % w, i // w
            tx, ty = x + dx, y + dy
            if 0 <= tx < w and 0 <= ty < h:
                j = ty * w + tx
                if cells[j] == EMPTY:
                    cells[j], cells[i] = c, EMPTY
                    changed = True
            else:
                lane = x if g in (0, 2) else y
                if c & 0xF0 == BLOCK and line[lane] == c & 0x0F:
                    cells[i] = EMPTY
                    changed = True
        if not changed:
            return cells
