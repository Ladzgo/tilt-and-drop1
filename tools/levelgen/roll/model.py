"""Roll & Print level model. Tiles are single-character codes; the cube is (x, y, faces[T,B,N,S,E,W])."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

FLOOR, WALL, TARGET, POOL, ERASER, ICE = range(6)
T, B, N, S, E, W = range(6)
DX = (1, 0, -1, 0)
DY = (0, 1, 0, -1)
DIR_NAMES = "ESWN"


def parse_tile(ch: str) -> tuple[int, int]:
    if ch == ".":
        return FLOOR, 0
    if ch == "#":
        return WALL, 0
    if ch in "123456":
        return TARGET, int(ch)
    if ch in "abcdef":
        return POOL, "abcdef".index(ch) + 1
    if ch == "*":
        return ERASER, 0
    if ch == "~":
        return ICE, 0
    raise ValueError(f"bad tile {ch!r}")


def tile_char(t: tuple[int, int]) -> str:
    k, c = t
    return {FLOOR: ".", WALL: "#", ERASER: "*", ICE: "~"}.get(k) or (str(c) if k == TARGET else "abcdef"[c - 1])


@dataclass
class RollLevel:
    w: int
    h: int
    tiles: list[tuple[int, int]]
    cube: tuple[int, int]
    faces: tuple[int, ...]           # T, B, N, S, E, W
    moves: int = 0
    par: int = 0
    id: str = ""
    tier: str = "N"
    meta: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not (3 <= self.w <= 8 and 3 <= self.h <= 8) or len(self.tiles) != self.w * self.h:
            raise ValueError("bad roll level size")
        if len(self.faces) != 6 or not all(0 <= f <= 6 for f in self.faces):
            raise ValueError("faces must be 6 colours 0..6")
        x, y = self.cube
        if not (0 <= x < self.w and 0 <= y < self.h) or self.tiles[y * self.w + x][0] == WALL:
            raise ValueError("cube must start on an open tile")

    @property
    def targets(self) -> list[int]:
        return [i for i, t in enumerate(self.tiles) if t[0] == TARGET]

    def rows(self) -> list[str]:
        return ["".join(tile_char(self.tiles[y * self.w + x]) for x in range(self.w)) for y in range(self.h)]

    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {"id": self.id, "w": self.w, "h": self.h, "grid": self.rows(),
                             "cube": {"x": self.cube[0], "y": self.cube[1], "faces": "".join(map(str, self.faces))},
                             "moves": self.moves, "par": self.par, "tier": self.tier}
        if self.meta:
            d["meta"] = self.meta
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "RollLevel":
        tiles = []
        for r in d["grid"]:
            if len(r) != d["w"]:
                raise ValueError(f"row {r!r} length != w")
            tiles += [parse_tile(ch) for ch in r]
        if len(d["grid"]) != d["h"]:
            raise ValueError("row count != h")
        c = d["cube"]
        return cls(d["w"], d["h"], tiles, (c["x"], c["y"]), tuple(int(ch) for ch in c["faces"]),
                   d.get("moves", 0), d.get("par", 0), d.get("id", ""), d.get("tier", "N"), dict(d.get("meta", {})))

    @classmethod
    def from_rows(cls, rows: list[str], cube=(0, 0), faces="010000", moves: int = 0) -> "RollLevel":
        return cls.from_json({"w": len(rows[0]), "h": len(rows), "grid": rows, "cube": {"x": cube[0], "y": cube[1], "faces": faces}, "moves": moves})
