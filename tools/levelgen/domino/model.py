"""Domino Run level model: cells are (kind, dir) with kinds '.', '#', 'd', 't', 'T', 'Y', 'B', 'S'."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

DX = (1, 0, -1, 0)
DY = (0, 1, 0, -1)
ARROWS = ">v<^"
KINDS = set(".#dtTYBS")


@dataclass
class DominoLevel:
    w: int
    h: int
    cells: list            # (kind, dir) ; dir only meaningful for t, T, S
    attempts: int = 3
    par: int = 0
    id: str = ""
    tier: str = "N"
    meta: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not (3 <= self.w <= 8 and 3 <= self.h <= 8) or len(self.cells) != self.w * self.h:
            raise ValueError("bad domino level size")
        if sum(1 for k, _ in self.cells if k == "S") != 1:
            raise ValueError("exactly one start domino")
        if not any(k == "B" for k, _ in self.cells):
            raise ValueError("at least one bell")

    @property
    def turners(self) -> list[int]:
        return [i for i, (k, _) in enumerate(self.cells) if k == "t"]

    @property
    def bells(self) -> list[int]:
        return [i for i, (k, _) in enumerate(self.cells) if k == "B"]

    @property
    def start(self) -> int:
        return next(i for i, (k, _) in enumerate(self.cells) if k == "S")

    def start_config(self) -> tuple[int, ...]:
        return tuple(self.cells[i][1] for i in self.turners)

    def rows(self) -> list[str]:
        out = []
        for y in range(self.h):
            r = ""
            for x in range(self.w):
                k, d = self.cells[y * self.w + x]
                r += k + (ARROWS[d] if k in "tTS" else ("#" if k == "#" else "." ))
            out.append(r)
        return out

    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {"id": self.id, "w": self.w, "h": self.h, "grid": self.rows(), "attempts": self.attempts, "par": self.par, "tier": self.tier}
        if self.meta:
            d["meta"] = self.meta
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "DominoLevel":
        cells = []
        if len(d["grid"]) != d["h"]:
            raise ValueError("row count != h")
        for r in d["grid"]:
            if len(r) != 2 * d["w"]:
                raise ValueError(f"row {r!r} must have 2*w characters")
            for x in range(d["w"]):
                k, a = r[2 * x], r[2 * x + 1]
                if k not in KINDS:
                    raise ValueError(f"bad piece {k!r}")
                if k in "tTS":
                    if a not in ARROWS:
                        raise ValueError(f"bad arrow {a!r}")
                    cells.append((k, ARROWS.index(a)))
                else:
                    cells.append((k, 0))
        return cls(d["w"], d["h"], cells, d.get("attempts", 3), d.get("par", 0), d.get("id", ""), d.get("tier", "N"), dict(d.get("meta", {})))

    @classmethod
    def from_rows(cls, rows: list[str], attempts: int = 3) -> "DominoLevel":
        return cls.from_json({"w": len(rows[0]) // 2, "h": len(rows), "grid": rows, "attempts": attempts})
