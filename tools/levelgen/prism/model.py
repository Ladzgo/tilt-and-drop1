"""Prism level model and text format.

Cells are tuples (kind, a): EMPTY/WALL (a unused), SOURCE (a = color*4 + dir), TARGET (a = required mask),
MIRROR/FIXED/SPLITTER (a = state: 0 '/', 1 '\\'), FILTER (a = color mask).
Colours: R=1, G=2, B=4 (mixes Y=3, M=5, C=6, W=7). Directions (y down): 0 right, 1 down, 2 left, 3 up.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

EMPTY, WALL, SOURCE, TARGET, MIRROR, FIXED, FILTER, SPLITTER = range(8)
ROTATABLE = (MIRROR, SPLITTER)
DX = (1, 0, -1, 0)
DY = (0, 1, 0, -1)
DIR_CH = ">v<^"
COLOR_CH = {1: "R", 2: "G", 4: "B", 3: "Y", 5: "M", 6: "C", 7: "W"}
CH_COLOR = {v: k for k, v in COLOR_CH.items()}

Cell = tuple  # (kind, a)


def cell_token(c: Cell) -> str:
    k, a = c
    if k == EMPTY:
        return "."
    if k == WALL:
        return "#"
    if k == SOURCE:
        return f"[{COLOR_CH[a >> 2]}{DIR_CH[a & 3]}]"
    if k == TARGET:
        return f"({COLOR_CH[a]})"
    if k == MIRROR:
        return "/" if a == 0 else "\\"
    if k == FIXED:
        return "{/}" if a == 0 else "{\\}"
    if k == FILTER:
        return f"<{COLOR_CH[a]}>"
    if k == SPLITTER:
        return "[/]" if a == 0 else "[\\]"
    raise ValueError(c)


def parse_row(row: str) -> list[Cell]:
    out: list[Cell] = []
    i = 0
    closing = {"[": "]", "(": ")", "{": "}", "<": ">"}
    while i < len(row):
        ch = row[i]
        if ch in closing:
            j = row.index(closing[ch], i + 1) if ch != "<" else i + 2
            tok = row[i + 1 : j]
            if ch == "[" and tok in ("/", "\\"):
                out.append((SPLITTER, 0 if tok == "/" else 1))
            elif ch == "[":
                if len(tok) != 2 or tok[0] not in "RGB" or tok[1] not in DIR_CH:
                    raise ValueError(f"bad source {tok!r}")
                out.append((SOURCE, CH_COLOR[tok[0]] * 4 + DIR_CH.index(tok[1])))
            elif ch == "(":
                out.append((TARGET, CH_COLOR[tok]))
            elif ch == "{":
                if tok not in ("/", "\\"):
                    raise ValueError(f"bad fixed mirror {tok!r}")
                out.append((FIXED, 0 if tok == "/" else 1))
            else:
                if row[j] != ">" or tok not in ("R", "G", "B"):
                    raise ValueError(f"bad filter in {row!r}")
                out.append((FILTER, CH_COLOR[tok]))
            i = j + 1
            continue
        if ch == ".":
            out.append((EMPTY, 0))
        elif ch == "#":
            out.append((WALL, 0))
        elif ch == "/":
            out.append((MIRROR, 0))
        elif ch == "\\":
            out.append((MIRROR, 1))
        else:
            raise ValueError(f"bad cell {ch!r} in {row!r}")
        i += 1
    return out


@dataclass
class PrismLevel:
    w: int
    h: int
    cells: list[Cell]
    moves: int = 0
    par: int = 0
    id: str = ""
    tier: str = "N"
    meta: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not (3 <= self.w <= 8 and 3 <= self.h <= 8) or len(self.cells) != self.w * self.h:
            raise ValueError("bad prism level size")

    @property
    def rotatables(self) -> list[int]:
        return [i for i, c in enumerate(self.cells) if c[0] in ROTATABLE]

    @property
    def targets(self) -> list[int]:
        return [i for i, c in enumerate(self.cells) if c[0] == TARGET]

    @property
    def sources(self) -> list[int]:
        return [i for i, c in enumerate(self.cells) if c[0] == SOURCE]

    def start_states(self) -> tuple[int, ...]:
        return tuple(self.cells[i][1] for i in self.rotatables)

    def rows(self) -> list[str]:
        return ["".join(cell_token(self.cells[y * self.w + x]) for x in range(self.w)) for y in range(self.h)]

    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {"id": self.id, "w": self.w, "h": self.h, "grid": self.rows(), "moves": self.moves, "par": self.par, "tier": self.tier}
        if self.meta:
            d["meta"] = self.meta
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "PrismLevel":
        cells: list[Cell] = []
        for r in d["grid"]:
            row = parse_row(r)
            if len(row) != d["w"]:
                raise ValueError(f"row {r!r} has {len(row)} cells, expected {d['w']}")
            cells += row
        if len(d["grid"]) != d["h"]:
            raise ValueError("row count != h")
        return cls(d["w"], d["h"], cells, d.get("moves", 0), d.get("par", 0), d.get("id", ""), d.get("tier", "N"), dict(d.get("meta", {})))

    @classmethod
    def from_rows(cls, rows: list[str], moves: int = 0) -> "PrismLevel":
        w = len(parse_row(rows[0]))
        return cls.from_json({"w": w, "h": len(rows), "grid": rows, "moves": moves})
