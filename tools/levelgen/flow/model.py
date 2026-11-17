"""Flow Fill level model."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

DX = (1, 0, -1, 0)
DY = (0, 1, 0, -1)
BASE = {"i": 0b0101, "l": 0b1001, "t": 0b1101, "x": 0b1111}   # bit d = opening toward d (0 right,1 down,2 left,3 up)


def rot_mask(mask: int, r: int) -> int:
    r &= 3
    return ((mask << r) | (mask >> (4 - r))) & 0xF


@dataclass
class FlowLevel:
    w: int
    h: int
    pipes: list            # None or (type, rotation, rotatable)
    sources: list          # [x, y, colour, dir]
    cups: list             # [x, y, colour, dir]
    moves: int = 0
    par: int = 0
    id: str = ""
    tier: str = "N"
    meta: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not (3 <= self.w <= 8 and 3 <= self.h <= 8) or len(self.pipes) != self.w * self.h:
            raise ValueError("bad flow level size")
        if not self.sources or not self.cups:
            raise ValueError("need sources and cups")
        occ = set()
        for x, y, c, d in self.sources + self.cups:
            i = y * self.w + x
            if i in occ or self.pipes[i] is not None:
                raise ValueError("sources/cups need their own empty cell")
            occ.add(i)

    @property
    def rotatables(self) -> list[int]:
        return [i for i, p in enumerate(self.pipes) if p is not None and p[2]]

    def mask(self, i: int, rot: dict | None = None) -> int:
        p = self.pipes[i]
        if p is not None:
            r = rot.get(i, p[1]) if rot is not None else p[1]
            return rot_mask(BASE[p[0]], r)
        for x, y, c, d in self.sources + self.cups:
            if y * self.w + x == i:
                return 1 << d
        return 0

    def rows(self) -> list[str]:
        out = []
        for y in range(self.h):
            r = ""
            for x in range(self.w):
                p = self.pipes[y * self.w + x]
                r += ".." if p is None else (p[0] if p[2] else p[0].upper()) + str(p[1])
            out.append(r)
        return out

    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {"id": self.id, "w": self.w, "h": self.h, "grid": self.rows(), "sources": self.sources, "cups": self.cups,
                             "moves": self.moves, "par": self.par, "tier": self.tier}
        if self.meta:
            d["meta"] = self.meta
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "FlowLevel":
        pipes = []
        if len(d["grid"]) != d["h"]:
            raise ValueError("row count != h")
        for r in d["grid"]:
            if len(r) != 2 * d["w"]:
                raise ValueError("row length != 2*w")
            for x in range(d["w"]):
                t, k = r[2 * x], r[2 * x + 1]
                if t == ".":
                    pipes.append(None)
                elif t.lower() in BASE and k in "0123":
                    pipes.append((t.lower(), int(k), t.islower() and t != "x"))
                else:
                    raise ValueError(f"bad pipe {t}{k}")
        return cls(d["w"], d["h"], pipes, [list(s) for s in d["sources"]], [list(c) for c in d["cups"]],
                   d.get("moves", 0), d.get("par", 0), d.get("id", ""), d.get("tier", "N"), dict(d.get("meta", {})))
