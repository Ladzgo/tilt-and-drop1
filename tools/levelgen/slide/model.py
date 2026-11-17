"""Slide Out level model. Cells: None (empty), "#" (wall) or (colour, dir) blocks."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

DX = (1, 0, -1, 0)
DY = (0, 1, 0, -1)
ARROWS = ">v<^"
WALL = "#"


@dataclass
class SlideLevel:
    w: int
    h: int
    cells: list            # None | "#" | (colour, dir)
    capacity: int = 5
    id: str = ""
    tier: str = "N"
    meta: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not (3 <= self.w <= 8 and 3 <= self.h <= 8) or len(self.cells) != self.w * self.h:
            raise ValueError("bad slide level size")
        if not 3 <= self.capacity <= 9:
            raise ValueError("tray capacity must be 3..9")

    @property
    def blocks(self) -> list[int]:
        """Cell indices of blocks, row-major. Block k = blocks[k] (the k-th bit of the removed mask)."""
        return [i for i, c in enumerate(self.cells) if isinstance(c, tuple)]

    def rows(self) -> list[str]:
        out = []
        for y in range(self.h):
            r = ""
            for x in range(self.w):
                c = self.cells[y * self.w + x]
                r += ".." if c is None else "##" if c == WALL else f"{c[0]}{ARROWS[c[1]]}"
            out.append(r)
        return out

    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {"id": self.id, "w": self.w, "h": self.h, "grid": self.rows(), "capacity": self.capacity, "tier": self.tier}
        if self.meta:
            d["meta"] = self.meta
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "SlideLevel":
        cells: list = []
        if len(d["grid"]) != d["h"]:
            raise ValueError("row count != h")
        for r in d["grid"]:
            if len(r) != 2 * d["w"]:
                raise ValueError(f"row {r!r} must have 2*w characters")
            for x in range(d["w"]):
                t = r[2 * x : 2 * x + 2]
                if t == "..":
                    cells.append(None)
                elif t == "##":
                    cells.append(WALL)
                elif t[0] in "123456" and t[1] in ARROWS:
                    cells.append((int(t[0]), ARROWS.index(t[1])))
                else:
                    raise ValueError(f"bad token {t!r}")
        return cls(d["w"], d["h"], cells, d.get("capacity", 5), d.get("id", ""), d.get("tier", "N"), dict(d.get("meta", {})))

    @classmethod
    def from_rows(cls, rows: list[str], capacity: int = 5) -> "SlideLevel":
        return cls.from_json({"w": len(rows[0]) // 2, "h": len(rows), "grid": rows, "capacity": capacity})
