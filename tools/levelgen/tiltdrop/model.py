"""Level model, cell encoding and JSON (de)serialization.

Cell byte encoding (docs/TECH.md §3.1): high nibble = kind, low nibble = color (1..6).
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

# ---- cell kinds (high nibble) -------------------------------------------------
EMPTY = 0x00
WALL = 0x10
BLOCK = 0x20  # | color
STONE = 0x30
ANCHOR = 0x40  # | color
ICE2 = 0x50  # | color, 2 HP
ICE1 = 0x60  # | color, 1 HP (state only)

MAX_COLORS = 6
MIN_SIZE, MAX_SIZE = 3, 8

# ---- directions ----------------------------------------------------------------
DOWN, RIGHT, UP, LEFT = 0, 1, 2, 3
DIR_NAMES = "DRUL"
DX = (0, 1, 0, -1)
DY = (1, 0, -1, 0)
EDGES = "TRBL"
# edge a gravity direction points into
GRAVITY_EDGE = {DOWN: "B", RIGHT: "R", UP: "T", LEFT: "L"}

CW, CCW = "CW", "CCW"


def rotate_gravity(g: int, rot: str) -> int:
    """CW: Down->Right->Up->Left->Down (board coordinates). CCW is the reverse."""
    if rot == CW:
        return (g + 1) & 3
    if rot == CCW:
        return (g + 3) & 3
    raise ValueError(f"unknown rotation {rot!r}")


def kind(c: int) -> int:
    return c & 0xF0


def color(c: int) -> int:
    return c & 0x0F


def is_movable(c: int) -> bool:
    k = c & 0xF0
    return k == BLOCK or k == STONE


def is_colored(c: int) -> bool:
    """Counts toward the win condition (block, anchor, ice)."""
    k = c & 0xF0
    return k == BLOCK or k == ANCHOR or k == ICE2 or k == ICE1


def is_piece(c: int) -> bool:
    """Anything that gets a piece id (everything except empty and wall)."""
    return c != EMPTY and c != WALL


# ---- text tokens ----------------------------------------------------------------
_TOKEN_PREFIX = {ANCHOR: "a", ICE2: "i", ICE1: "j"}
_PREFIX_KIND = {v: k for k, v in _TOKEN_PREFIX.items()}


def cell_token(c: int) -> str:
    k, col = kind(c), color(c)
    if c == EMPTY:
        return "."
    if c == WALL:
        return "#"
    if c == STONE:
        return "@"
    if k == BLOCK and 1 <= col <= MAX_COLORS:
        return str(col)
    if k in _TOKEN_PREFIX and 1 <= col <= MAX_COLORS:
        return f"[{_TOKEN_PREFIX[k]}{col}]"
    raise ValueError(f"invalid cell byte 0x{c:02x}")


def parse_row(row: str) -> list[int]:
    out: list[int] = []
    i = 0
    while i < len(row):
        ch = row[i]
        if ch == "[":
            end = row.index("]", i)
            tok = row[i + 1 : end]
            if len(tok) != 2 or tok[0] not in _PREFIX_KIND or not tok[1].isdigit():
                raise ValueError(f"bad token [{tok}] in row {row!r}")
            col = int(tok[1])
            if not 1 <= col <= MAX_COLORS:
                raise ValueError(f"bad color in [{tok}]")
            out.append(_PREFIX_KIND[tok[0]] | col)
            i = end + 1
            continue
        if ch == ".":
            out.append(EMPTY)
        elif ch == "#":
            out.append(WALL)
        elif ch == "@":
            out.append(STONE)
        elif ch.isdigit() and 1 <= int(ch) <= MAX_COLORS:
            out.append(BLOCK | int(ch))
        else:
            raise ValueError(f"bad cell {ch!r} in row {row!r}")
        i += 1
    return out


def grid_rows(cells: bytes | list[int], w: int, h: int) -> list[str]:
    return ["".join(cell_token(cells[y * w + x]) for x in range(w)) for y in range(h)]


def parse_gate_string(s: str, n: int) -> bytes:
    if len(s) != n:
        raise ValueError(f"gate string {s!r} must have length {n}")
    out = bytearray()
    for ch in s:
        if ch == ".":
            out.append(0)
        elif ch.isdigit() and 1 <= int(ch) <= MAX_COLORS:
            out.append(int(ch))
        else:
            raise ValueError(f"bad gate {ch!r}")
    return bytes(out)


def gate_string(g: bytes) -> str:
    return "".join("." if v == 0 else str(v) for v in g)


# ---- level ------------------------------------------------------------------------
@dataclass
class Level:
    w: int
    h: int
    cells: bytes
    gates: dict[str, bytes]  # 'T','B': len w (by x); 'L','R': len h (by y)
    moves: int = 0
    par: int = 0
    id: str = ""
    tier: str = "N"
    mechanics: list[str] = field(default_factory=list)
    meta: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not (MIN_SIZE <= self.w <= MAX_SIZE and MIN_SIZE <= self.h <= MAX_SIZE):
            raise ValueError(f"board size {self.w}x{self.h} out of range")
        if len(self.cells) != self.w * self.h:
            raise ValueError("cells length mismatch")
        self.cells = bytes(self.cells)
        for e in EDGES:
            n = self.w if e in "TB" else self.h
            g = bytes(self.gates.get(e, bytes(n)))
            if len(g) != n:
                raise ValueError(f"gate edge {e} length {len(g)} != {n}")
            self.gates[e] = g
        for c in self.cells:
            cell_token(c)  # validates

    # -- helpers
    def gate(self, edge: str, index: int) -> int:
        return self.gates[edge][index]

    def colors(self) -> set[int]:
        return {color(c) for c in self.cells if is_colored(c)}

    def gate_colors(self) -> set[int]:
        return {v for e in EDGES for v in self.gates[e] if v}

    def count_colored(self) -> int:
        return sum(1 for c in self.cells if is_colored(c))

    def rows(self) -> list[str]:
        return grid_rows(self.cells, self.w, self.h)

    def detected_mechanics(self) -> list[str]:
        ks = {kind(c) for c in self.cells}
        out = []
        if STONE in ks:
            out.append("stone")
        if ANCHOR in ks:
            out.append("anchor")
        if ICE2 in ks or ICE1 in ks:
            out.append("ice")
        return out

    # -- JSON
    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {
            "id": self.id,
            "w": self.w,
            "h": self.h,
            "grid": self.rows(),
            "gates": {e: gate_string(self.gates[e]) for e in EDGES},
            "moves": self.moves,
            "par": self.par,
            "tier": self.tier,
            "mechanics": list(self.mechanics),
        }
        if self.meta:
            d["meta"] = self.meta
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "Level":
        w, h = int(d["w"]), int(d["h"])
        rows = d["grid"]
        if len(rows) != h:
            raise ValueError("grid row count != h")
        cells: list[int] = []
        for r in rows:
            pr = parse_row(r)
            if len(pr) != w:
                raise ValueError(f"row {r!r} has {len(pr)} cells, expected {w}")
            cells.extend(pr)
        g = d.get("gates", {})
        gates = {e: parse_gate_string(g.get(e, "." * (w if e in "TB" else h)), w if e in "TB" else h) for e in EDGES}
        return cls(
            w=w,
            h=h,
            cells=bytes(cells),
            gates=gates,
            moves=int(d.get("moves", 0)),
            par=int(d.get("par", 0)),
            id=str(d.get("id", "")),
            tier=str(d.get("tier", "N")),
            mechanics=list(d.get("mechanics", [])),
            meta=dict(d.get("meta", {})),
        )

    @classmethod
    def from_rows(cls, rows: list[str], gates: dict[str, str] | None = None, moves: int = 0, **kw: Any) -> "Level":
        """Convenience constructor for tests: gates given as strings."""
        h = len(rows)
        w = len(parse_row(rows[0]))
        g = gates or {}
        return cls.from_json({"w": w, "h": h, "grid": rows, "gates": g, "moves": moves, **kw})
