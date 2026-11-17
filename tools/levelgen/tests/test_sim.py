"""Rule tests with hand-computed expectations (docs/GDD.md §2)."""
import random

import pytest

from tiltdrop.model import (
    BLOCK,
    CCW,
    CW,
    DOWN,
    EMPTY,
    LEFT,
    RIGHT,
    STONE,
    UP,
    WALL,
    Level,
    grid_rows,
    rotate_gravity,
)
from tiltdrop.naive_sim import settle_naive
from tiltdrop.sim import LOST, PLAYING, WON, Game, is_stable, settle, step, topology


def rows(game):
    return grid_rows(game.cells, game.level.w, game.level.h)


def test_rotation_mapping():
    assert [rotate_gravity(g, CW) for g in (DOWN, RIGHT, UP, LEFT)] == [RIGHT, UP, LEFT, DOWN]
    assert [rotate_gravity(g, CCW) for g in (DOWN, LEFT, UP, RIGHT)] == [LEFT, UP, RIGHT, DOWN]
    with pytest.raises(ValueError):
        rotate_gravity(0, "X")


def test_topology_order_nearest_first():
    t = topology(3, 2)
    # Down: bottom row first, left to right
    assert t.order[DOWN] == (3, 4, 5, 0, 1, 2)
    # Left: column x=0 first, top to bottom
    assert t.order[LEFT] == (0, 3, 1, 4, 2, 5)
    assert t.nxt[DOWN][0] == 3 and t.nxt[DOWN][3] == -1
    assert t.lane[RIGHT][4] == 1 and t.lane[UP][4] == 1


CASCADE = Level.from_rows(["....", "....", "11.2"], {"L": "..1"}, moves=5)


def test_cascade_through_same_gate_and_wrong_gate_is_wall():
    g = Game(CASCADE)
    ev = g.rotate(CCW)
    assert g.gravity == LEFT
    assert rows(g) == ["....", "....", "2..."]
    assert ev == [
        ("exit", 0, 1, "L", 2),
        ("move", 0, 2, 9, 8),
        ("exit", 0, 2, "L", 2),
        ("move", 0, 3, 11, 8),
    ]
    assert g.moves_left == 4 and g.status == PLAYING


def test_rotation_costs_move_even_if_nothing_moves():
    g = Game(CASCADE)
    g.rotate(CW)  # right: 11.2 -> .112
    assert rows(g)[2] == ".112"
    g.rotate(CCW)  # back to down: nothing moves
    assert rows(g)[2] == ".112"
    assert g.moves_left == 3


def test_win_on_last_move_beats_lose():
    lv = Level.from_rows(["....", "....", "11.."], {"L": "..1"}, moves=1)
    g = Game(lv)
    g.rotate(CCW)
    assert g.status == WON and g.moves_left == 0


def test_lose_and_continue():
    lv = Level.from_rows(["....", "....", "11.."], {"L": "..1"}, moves=1)
    g = Game(lv)
    g.rotate(CW)
    assert rows(g)[2] == "..11"
    assert g.status == LOST
    with pytest.raises(RuntimeError):
        g.rotate(CW)
    g.add_moves(3)
    assert g.status == PLAYING and g.moves_left == 3
    g.rotate(CCW)  # down
    g.rotate(CCW)  # left -> both exit through L[2]
    assert g.status == WON
    with pytest.raises(RuntimeError):
        g.add_moves(3)


def test_stone_never_exits_and_blocks():
    lv = Level.from_rows(["....", "....", "@1.."], {"L": "..1"}, moves=3)
    g = Game(lv)
    g.rotate(CCW)
    assert rows(g)[2] == "@1.."


def test_nearest_first_in_column():
    # gravity Up: column x=0 holds blocks at y=1 and y=3; top gate color 1
    lv = Level.from_rows(["....", "1...", "....", "1..."], {"T": "1..."})
    buf = bytearray(lv.cells)
    ex = settle(buf, topology(4, 4), UP, lv.gates["T"])
    assert ex == 2 and bytes(buf) == bytes(16)


def test_anchor_release_same_settle():
    lv = Level.from_rows(["[a1]...", "....", "1..."], {"L": "1.1"}, moves=3)
    assert is_stable(lv)
    g = Game(lv)
    ev = g.rotate(CCW)
    assert ev == [("exit", 0, 2, "L", 2), ("release", 0, 1), ("exit", 1, 1, "L", 0)]
    assert g.status == WON


def test_anchor_not_released_by_other_color():
    lv = Level.from_rows(["[a1]...", "....", "2..."], {"L": "1.2"}, moves=3)
    g = Game(lv)
    g.rotate(CCW)
    assert rows(g) == ["[a1]...", "....", "...."]


def test_ice_two_hits_then_thaw_and_cascade():
    lv = Level.from_rows(["....", "....", "1.[i2]."], {"R": "..2"}, moves=10)
    assert is_stable(lv)
    g = Game(lv)
    assert g.rotate(CW) == [("move", 0, 1, 8, 9), ("hit", 0, 2, 1)]
    assert rows(g)[2] == ".1[j2]."
    assert g.rotate(CCW) == []  # down
    assert g.rotate(CCW) == [("move", 0, 1, 9, 8)]  # left: no hit, nothing on the left
    assert g.rotate(CW) == []  # down
    ev = g.rotate(CW)  # right
    assert ev == [
        ("move", 0, 1, 8, 9),
        ("hit", 0, 2, 0),
        ("move", 1, 2, 10, 11),
        ("exit", 1, 2, "R", 2),
        ("move", 1, 1, 9, 11),
    ]
    # block 1 is color 1, gate is color 2 -> it slides to the edge and stays
    assert rows(g)[2] == "...1"
    assert g.status == PLAYING


def test_resting_piece_does_not_hit_ice():
    lv = Level.from_rows(["....", "....", ".1[i1]."], {})
    buf = bytearray(lv.cells)
    settle(buf, topology(4, 3), RIGHT, lv.gates["R"])
    assert bytes(buf) == lv.cells


def test_hammer_removes_and_resettles():
    lv = Level.from_rows(["....", "@...", "1..."], {}, moves=3)
    g = Game(lv)
    ev = g.hammer(0, 2)
    assert ev == [("hammer", 0, 2, 8), ("move", 0, 1, 4, 8)]
    assert g.status == WON and g.moves_left == 3
    with pytest.raises(RuntimeError):
        g.hammer(0, 2)


def test_hammer_validation():
    g = Game(Level.from_rows(["#...", "....", "1..."], {}, moves=3))
    with pytest.raises(ValueError):
        g.hammer(0, 0)
    with pytest.raises(ValueError):
        g.hammer(1, 1)
    with pytest.raises(ValueError):
        g.hammer(9, 0)


def test_stability():
    assert is_stable(CASCADE)
    assert not is_stable(Level.from_rows(["1...", "....", "...."]))
    # block resting above a matching bottom gate would exit on load
    assert not is_stable(Level.from_rows(["....", "....", "1..."], {"B": "1..."}))


def test_step_is_pure():
    cells, g, ex = step(CASCADE, CASCADE.cells, DOWN, CCW)
    assert g == LEFT and ex == 2 and CASCADE.cells != cells


def random_cells(rng, w, h):
    pool = [EMPTY] * 6 + [WALL, STONE] + [BLOCK | c for c in range(1, 4)] * 2
    return [rng.choice(pool) for _ in range(w * h)]


def test_fuzz_against_naive_oracle():
    rng = random.Random(1234)
    for _ in range(3000):
        w, h = rng.randint(3, 8), rng.randint(3, 8)
        cells = random_cells(rng, w, h)
        gates = {e: bytes(rng.choice([0, 0, 0, 1, 2, 3]) for _ in range(w if e in "TB" else h)) for e in "TRBL"}
        g = rng.randrange(4)
        lv = Level(w, h, bytes(cells), gates)
        buf = bytearray(cells)
        settle(buf, topology(w, h), g, lv.gates["BRTL"[g]])
        assert list(buf) == settle_naive(cells, w, h, g, lv.gates)


def test_settle_trace_replays_to_same_cells():
    """Applying the event list to the start position reproduces the end position."""
    rng = random.Random(99)
    for _ in range(500):
        w, h = rng.randint(3, 8), rng.randint(3, 8)
        cells = random_cells(rng, w, h)
        gates = {e: bytes(rng.choice([0, 1, 2, 3]) for _ in range(w if e in "TB" else h)) for e in "TRBL"}
        lv = Level(w, h, bytes(cells), gates)
        game = Game(lv)
        if game.status != PLAYING:
            continue
        before = list(game.cells)
        ids = list(game.ids)
        ev = game.rotate(rng.choice([CW, CCW]))
        pos = {pid: i for i, pid in enumerate(ids) if pid}
        board = before[:]
        for e in ev:
            if e[0] == "move":
                _, _, pid, a, b = e
                assert pos[pid] == a
                board[b], board[a] = board[a], EMPTY
                pos[pid] = b
            elif e[0] == "exit":
                board[pos.pop(e[2])] = EMPTY
        assert board == list(game.cells)
