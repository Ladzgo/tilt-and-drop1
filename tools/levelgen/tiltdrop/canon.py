"""Canonical form of a board state for de-duplication.

A state is (w, h, cells, gates, gravity). Two states are the same puzzle if one
maps to the other under a D4 symmetry (rotation/mirror of the whole board,
gates included) plus a permutation of colors. Because the player always sees
gravity pointing down, every image is re-oriented so its gravity is Down; the
lexicographic minimum over all images (after color normalization) is the key.
"""
from __future__ import annotations

import hashlib

from .model import CCW, CW, DOWN, EDGES, LEFT, RIGHT, Level, is_colored
from .sim import settle, topology
from .model import GRAVITY_EDGE, rotate_gravity

State = tuple[int, int, bytes, dict[str, bytes], int]


def rot90(s: State) -> State:
    """Rotate the board image 90° clockwise: (x, y) -> (h-1-y, x)."""
    w, h, cells, gates, g = s
    nw, nh = h, w
    out = bytearray(nw * nh)
    for y in range(h):
        for x in range(w):
            out[x * nw + (h - 1 - y)] = cells[y * w + x]
    ng = {
        "R": gates["T"],
        "B": gates["R"][::-1],
        "L": gates["B"],
        "T": gates["L"][::-1],
    }
    return nw, nh, bytes(out), ng, (g + 3) & 3


def mirror(s: State) -> State:
    """Mirror left<->right: (x, y) -> (w-1-x, y)."""
    w, h, cells, gates, g = s
    out = bytearray(w * h)
    for y in range(h):
        for x in range(w):
            out[y * w + (w - 1 - x)] = cells[y * w + x]
    ng = {"T": gates["T"][::-1], "B": gates["B"][::-1], "L": gates["R"], "R": gates["L"]}
    swap = {LEFT: RIGHT, RIGHT: LEFT}
    return w, h, bytes(out), ng, swap.get(g, g)


def images(s: State) -> list[State]:
    out = []
    cur = s
    for _ in range(4):
        out.append(cur)
        out.append(mirror(cur))
        cur = rot90(cur)
    return out


def _normalize_colors(s: State) -> bytes:
    w, h, cells, gates, _ = s
    remap: dict[int, int] = {}

    def m(c: int) -> int:
        if c not in remap:
            remap[c] = len(remap) + 1
        return remap[c]

    enc = bytearray((w, h))
    for c in cells:
        enc.append((c & 0xF0) | m(c & 0x0F) if is_colored(c) else c)
    for e in EDGES:
        enc.append(0xFF)
        enc.extend(m(v) if v else 0 for v in gates[e])
    return bytes(enc)


def canonical_key(s: State) -> bytes:
    best = None
    for im in images(s):
        cur = im
        while cur[4] != DOWN:
            cur = rot90(cur)
        k = _normalize_colors(cur)
        if best is None or k < best:
            best = k
    assert best is not None
    return best


def level_state(level: Level, cells: bytes | None = None, gravity: int = DOWN) -> State:
    return level.w, level.h, bytes(cells if cells is not None else level.cells), dict(level.gates), gravity


def level_key(level: Level) -> bytes:
    return canonical_key(level_state(level))


def level_hash(level: Level) -> str:
    return hashlib.sha1(level_key(level)).hexdigest()[:16]


def neighborhood_keys(level: Level, depth: int = 2) -> set[bytes]:
    """Canonical keys of every state reachable within `depth` moves (excluding start)."""
    topo = topology(level.w, level.h)
    frontier = [(level.cells, DOWN)]
    seen = {(level.cells, DOWN)}
    out: set[bytes] = set()
    for _ in range(depth):
        nxt = []
        for cells, g in frontier:
            for rot in (CW, CCW):
                ng = rotate_gravity(g, rot)
                buf = bytearray(cells)
                settle(buf, topo, ng, level.gates[GRAVITY_EDGE[ng]])
                st = (bytes(buf), ng)
                if st not in seen:
                    seen.add(st)
                    nxt.append(st)
                    out.add(canonical_key(level_state(level, st[0], ng)))
        frontier = nxt
    return out


class Deduper:
    """Rejects exact duplicates and near-duplicates (start state within 2 moves of another level)."""

    def __init__(self, depth: int = 2):
        self.depth = depth
        self.starts: set[bytes] = set()
        self.near: set[bytes] = set()

    def is_duplicate(self, level: Level) -> bool:
        k = level_key(level)
        if k in self.starts or k in self.near:
            return True
        return any(n in self.starts for n in neighborhood_keys(level, self.depth))

    def add(self, level: Level) -> bool:
        """Add if not duplicate; returns True when added."""
        if self.is_duplicate(level):
            return False
        self.starts.add(level_key(level))
        self.near |= neighborhood_keys(level, self.depth)
        return True
