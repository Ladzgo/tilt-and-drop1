"""Normative simulator (docs/GDD.md §2.4). Integer-only, deterministic.

The C# core (game/Assets/_Project/Core/Sim) mirrors this file line by line;
parity is enforced by the golden vectors in /shared/golden.

Events (only produced when an `events` list is passed):
    ("move",    pass, piece_id, from_index, to_index)
    ("exit",    pass, piece_id, edge, lane_index)
    ("hit",     pass, piece_id, hp_left)        # ice; hp_left == 0 means thawed
    ("release", pass, piece_id)                 # anchor -> block
"""
from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache

from .model import (
    ANCHOR,
    BLOCK,
    DOWN,
    DX,
    DY,
    EMPTY,
    GRAVITY_EDGE,
    ICE1,
    ICE2,
    STONE,
    WALL,
    Level,
    is_colored,
    rotate_gravity,
)

Event = tuple


@dataclass(frozen=True)
class Topology:
    w: int
    h: int
    order: tuple[tuple[int, ...], ...]  # per gravity: cell indices, nearest-first
    nxt: tuple[tuple[int, ...], ...]  # per gravity: neighbour index in gravity direction or -1
    lane: tuple[tuple[int, ...], ...]  # per gravity: gate index on the destination edge


@lru_cache(maxsize=None)
def topology(w: int, h: int) -> Topology:
    orders, nxts, lanes = [], [], []
    for g in range(4):
        dx, dy = DX[g], DY[g]

        def dist(i: int) -> tuple[int, int]:
            x, y = i % w, i // w
            if g == 0:  # Down
                return (h - 1 - y, x)
            if g == 1:  # Right
                return (w - 1 - x, y)
            if g == 2:  # Up
                return (y, x)
            return (x, y)  # Left

        orders.append(tuple(sorted(range(w * h), key=dist)))
        nx, ln = [], []
        for i in range(w * h):
            x, y = i % w, i // w
            tx, ty = x + dx, y + dy
            nx.append(ty * w + tx if 0 <= tx < w and 0 <= ty < h else -1)
            ln.append(x if g in (0, 2) else y)
        nxts.append(tuple(nx))
        lanes.append(tuple(ln))
    return Topology(w, h, tuple(orders), tuple(nxts), tuple(lanes))


def settle(
    cells: bytearray,
    topo: Topology,
    g: int,
    gate_line: bytes,
    ids: list[int] | None = None,
    events: list[Event] | None = None,
) -> int:
    """Settle `cells` in place under gravity `g`. Returns number of exits."""
    order, nxt, lane = topo.order[g], topo.nxt[g], topo.lane[g]
    edge = GRAVITY_EDGE[g]
    n_cells = len(cells)
    exits = 0
    pass_no = 0
    changed = True
    while changed:
        changed = False
        for p in order:
            c = cells[p]
            k = c & 0xF0
            if k != BLOCK and k != STONE:
                continue
            # slide as far as possible
            pos = p
            while True:
                n = nxt[pos]
                if n < 0 or cells[n] != EMPTY:
                    break
                pos = n
            moved = pos != p
            if moved:
                cells[pos] = c
                cells[p] = EMPTY
                changed = True
                if ids is not None:
                    ids[pos] = ids[p]
                    ids[p] = 0
                if events is not None:
                    events.append(("move", pass_no, ids[pos] if ids is not None else 0, p, pos))
            n = nxt[pos]
            if n < 0:
                col = c & 0x0F
                if k == BLOCK and gate_line[lane[pos]] == col:
                    cells[pos] = EMPTY
                    changed = True
                    exits += 1
                    pid = 0
                    if ids is not None:
                        pid = ids[pos]
                        ids[pos] = 0
                    if events is not None:
                        events.append(("exit", pass_no, pid, edge, lane[pos]))
                    anchor = ANCHOR | col
                    for i in range(n_cells):
                        if cells[i] == anchor:
                            cells[i] = BLOCK | col
                            if events is not None:
                                events.append(("release", pass_no, ids[i] if ids is not None else 0))
            elif moved:
                t = cells[n]
                tk = t & 0xF0
                if tk == ICE2 or tk == ICE1:
                    if tk == ICE2:
                        cells[n] = ICE1 | (t & 0x0F)
                        hp = 1
                    else:
                        cells[n] = BLOCK | (t & 0x0F)
                        hp = 0
                    if events is not None:
                        events.append(("hit", pass_no, ids[n] if ids is not None else 0, hp))
        pass_no += 1
    return exits


def step(level: Level, cells: bytes, g: int, rot: str) -> tuple[bytes, int, int]:
    """Pure transition used by the solver: returns (cells, gravity, exits)."""
    ng = rotate_gravity(g, rot)
    buf = bytearray(cells)
    ex = settle(buf, topology(level.w, level.h), ng, level.gates[GRAVITY_EDGE[ng]])
    return bytes(buf), ng, ex


def has_colored(cells: bytes) -> bool:
    for c in cells:
        if is_colored(c):
            return True
    return False


def count_colored(cells: bytes) -> int:
    return sum(1 for c in cells if is_colored(c))


def is_stable(level: Level, g: int = DOWN) -> bool:
    buf = bytearray(level.cells)
    settle(buf, topology(level.w, level.h), g, level.gates[GRAVITY_EDGE[g]])
    return bytes(buf) == level.cells


def initial_ids(cells: bytes) -> list[int]:
    """Piece ids: 1.. in row-major order over every non-empty, non-wall cell."""
    ids, nxt_id = [], 1
    for c in cells:
        if c != EMPTY and c != WALL:
            ids.append(nxt_id)
            nxt_id += 1
        else:
            ids.append(0)
    return ids


PLAYING, WON, LOST = "playing", "won", "lost"


class Game:
    """Stateful game session (mirrors TiltDrop.Core.GameState)."""

    def __init__(self, level: Level):
        self.level = level
        self.topo = topology(level.w, level.h)
        self.cells = bytearray(level.cells)
        self.ids = initial_ids(level.cells)
        self.gravity = DOWN
        self.moves_left = level.moves
        self.moves_used = 0
        self.status = PLAYING
        self._update_status()

    def _update_status(self) -> None:
        if not has_colored(self.cells):
            self.status = WON
        elif self.moves_left <= 0:
            self.status = LOST
        else:
            self.status = PLAYING

    def _settle(self) -> list[Event]:
        ev: list[Event] = []
        settle(self.cells, self.topo, self.gravity, self.level.gates[GRAVITY_EDGE[self.gravity]], self.ids, ev)
        return ev

    def rotate(self, rot: str) -> list[Event]:
        if self.status != PLAYING:
            raise RuntimeError(f"cannot rotate: game is {self.status}")
        self.gravity = rotate_gravity(self.gravity, rot)
        self.moves_left -= 1
        self.moves_used += 1
        ev = self._settle()
        self._update_status()
        return ev

    def hammer(self, x: int, y: int) -> list[Event]:
        if self.status != PLAYING:
            raise RuntimeError(f"cannot hammer: game is {self.status}")
        i = y * self.level.w + x
        if not (0 <= x < self.level.w and 0 <= y < self.level.h):
            raise ValueError("hammer target outside board")
        c = self.cells[i]
        if c == EMPTY or c == WALL:
            raise ValueError("hammer target must be a piece")
        removed = self.ids[i]
        self.cells[i] = EMPTY
        self.ids[i] = 0
        ev: list[Event] = [("hammer", 0, removed, i)]
        ev.extend(self._settle())
        self._update_status()
        return ev

    def add_moves(self, n: int) -> None:
        """Continue (+n moves) after a loss."""
        if self.status == WON:
            raise RuntimeError("cannot continue a won game")
        self.moves_left += n
        self._update_status()

    def state_key(self) -> bytes:
        return bytes(self.cells) + bytes((self.gravity,))
