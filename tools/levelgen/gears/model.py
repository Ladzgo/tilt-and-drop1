"""Gear Works level model. Cells are single characters (see docs/games/05-gears.md §1.1)."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

GEAR_CELLS = set("gGMRL")
VALID = set(".ogGMRL")


@dataclass
class GearLevel:
    w: int
    h: int
    cells: list[str]
    inventory: int
    moves: int = 0
    par: int = 0
    id: str = ""
    tier: str = "N"
    meta: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not (3 <= self.w <= 8 and 3 <= self.h <= 8) or len(self.cells) != self.w * self.h:
            raise ValueError("bad gear level size")
        if any(c not in VALID for c in self.cells):
            raise ValueError("bad cell")
        if sum(1 for c in self.cells if c == "g") > self.inventory:
            raise ValueError("more removable gears than inventory")

    @property
    def pegs(self) -> list[int]:
        """Cells that can hold a removable gear (empty or occupied pegs), row-major. Peg k is bit k of a layout."""
        return [i for i, c in enumerate(self.cells) if c in "og"]

    @property
    def outputs(self) -> list[int]:
        return [i for i, c in enumerate(self.cells) if c in "RL"]

    def start_layout(self) -> int:
        return sum(1 << k for k, i in enumerate(self.pegs) if self.cells[i] == "g")

    def rows(self) -> list[str]:
        return ["".join(self.cells[y * self.w : (y + 1) * self.w]) for y in range(self.h)]

    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {"id": self.id, "w": self.w, "h": self.h, "grid": self.rows(), "inventory": self.inventory,
                             "moves": self.moves, "par": self.par, "tier": self.tier}
        if self.meta:
            d["meta"] = self.meta
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "GearLevel":
        if len(d["grid"]) != d["h"] or any(len(r) != d["w"] for r in d["grid"]):
            raise ValueError("grid shape mismatch")
        return cls(d["w"], d["h"], list("".join(d["grid"])), d["inventory"], d.get("moves", 0), d.get("par", 0),
                   d.get("id", ""), d.get("tier", "N"), dict(d.get("meta", {})))

    @classmethod
    def from_rows(cls, rows: list[str], inventory: int = 3, moves: int = 0) -> "GearLevel":
        return cls.from_json({"w": len(rows[0]), "h": len(rows), "grid": rows, "inventory": inventory, "moves": moves})
