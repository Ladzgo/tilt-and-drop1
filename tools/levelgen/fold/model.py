"""Fold level model: front/back colour per square (row-major), target pattern."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass
class FoldLevel:
    w: int
    h: int
    front: list[int]
    back: list[int]
    target: list[str]          # rows of digits 0..6 (final visible pattern)
    moves: int = 0
    par: int = 0
    id: str = ""
    tier: str = "N"
    meta: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not (1 <= self.w <= 6 and 1 <= self.h <= 6) or len(self.front) != self.w * self.h or len(self.back) != self.w * self.h:
            raise ValueError("bad fold level size")
        if not self.target or any(len(r) != len(self.target[0]) for r in self.target):
            raise ValueError("bad target")

    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {"id": self.id, "w": self.w, "h": self.h,
                             "front": ["".join(map(str, self.front[y * self.w:(y + 1) * self.w])) for y in range(self.h)],
                             "back": ["".join(map(str, self.back[y * self.w:(y + 1) * self.w])) for y in range(self.h)],
                             "target": self.target, "moves": self.moves, "par": self.par, "tier": self.tier}
        if self.meta:
            d["meta"] = self.meta
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "FoldLevel":
        f = [int(c) for r in d["front"] for c in r]
        b = [int(c) for r in d["back"] for c in r]
        return cls(d["w"], d["h"], f, b, list(d["target"]), d.get("moves", 0), d.get("par", 0), d.get("id", ""), d.get("tier", "N"), dict(d.get("meta", {})))
