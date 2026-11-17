"""Normative Roll & Print rules: rolling, ice slides, landing effects, the stateful game."""
from __future__ import annotations

from .model import B, DX, DY, ERASER, ICE, POOL, TARGET, WALL, E, N, RollLevel, S, T, W

# new_faces[i] = old_faces[PERM[d][i]]   (faces order T, B, N, S, E, W)
PERM = (
    (W, E, N, S, T, B),  # east : T<-W, B<-E, E<-T, W<-B
    (N, S, B, T, E, W),  # south: T<-N, B<-S, N<-B, S<-T
    (E, W, N, S, B, T),  # west : T<-E, B<-W, E<-B, W<-T
    (S, N, T, B, E, W),  # north: T<-S, B<-N, N<-T, S<-B
)


def roll_faces(faces: tuple, d: int) -> tuple:
    p = PERM[d]
    return tuple(faces[p[i]] for i in range(6))


def step(level: RollLevel, pos: tuple[int, int], faces: tuple, prints: dict[int, int], d: int, trail: list | None = None):
    """One roll (including an ice slide). Returns (pos, faces, prints) or None if blocked.
    `prints` maps target index -> current colour (copied, not mutated). `trail` collects landings
    as (index, faces_after) for animation."""
    w, h, tiles = level.w, level.h, level.tiles
    x, y = pos
    nx, ny = x + DX[d], y + DY[d]
    if not (0 <= nx < w and 0 <= ny < h) or tiles[ny * w + nx][0] == WALL:
        return None
    prints = dict(prints)
    while True:
        x, y = nx, ny
        faces = roll_faces(faces, d)
        i = y * w + x
        k, c = tiles[i]
        if k == POOL:
            faces = faces[:B] + (c,) + faces[B + 1:]
        elif k == TARGET and faces[B]:
            prints[i] = faces[B]
        elif k == ERASER:
            faces = faces[:B] + (0,) + faces[B + 1:]
        if trail is not None:
            trail.append((i, faces))
        if k != ICE:
            break
        nx, ny = x + DX[d], y + DY[d]
        if not (0 <= nx < w and 0 <= ny < h) or tiles[ny * w + nx][0] == WALL:
            break
    return (x, y), faces, prints


def solved(level: RollLevel, prints: dict[int, int]) -> bool:
    ts = level.targets
    return bool(ts) and all(prints.get(i, 0) == level.tiles[i][1] for i in ts)


def state_key(pos, faces, prints, targets) -> tuple:
    return (pos, faces, tuple(prints.get(i, 0) for i in targets))


PLAYING, WON, LOST = "playing", "won", "lost"


class RollGame:
    def __init__(self, level: RollLevel):
        self.level = level
        self.pos = level.cube
        self.faces = tuple(level.faces)
        self.prints: dict[int, int] = {}
        self.moves_left = level.moves
        self.moves_used = 0
        self._update()

    def _update(self) -> None:
        self.status = WON if solved(self.level, self.prints) else LOST if self.moves_left <= 0 else PLAYING

    def roll(self, d: int, trail: list | None = None) -> bool:
        if self.status != PLAYING:
            raise RuntimeError(f"cannot roll: {self.status}")
        r = step(self.level, self.pos, self.faces, self.prints, d, trail)
        if r is None:
            return False
        self.pos, self.faces, self.prints = r
        self.moves_left -= 1
        self.moves_used += 1
        self._update()
        return True

    def add_moves(self, n: int) -> None:
        if self.status == WON:
            raise RuntimeError("won")
        self.moves_left += n
        self._update()
