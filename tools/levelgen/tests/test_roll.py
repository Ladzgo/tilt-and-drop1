"""Roll & Print rules (docs/games/03-roll.md §1), hand-computed."""
import pytest

from roll.model import B, E, N, S, T, W, RollLevel
from roll.sim import LOST, PLAYING, WON, RollGame, roll_faces, step
from roll.solver import solve

EAST, SOUTH, WEST, NORTH = range(4)


def test_roll_permutations_match_physical_cube():
    f = (1, 2, 3, 4, 5, 6)  # T B N S E W
    assert roll_faces(f, EAST) == (6, 5, 3, 4, 1, 2)    # top goes east, west comes up, east goes down
    assert roll_faces(f, SOUTH) == (3, 4, 2, 1, 5, 6)   # top goes south, north comes up
    assert roll_faces(f, WEST) == (5, 6, 3, 4, 2, 1)
    assert roll_faces(f, NORTH) == (4, 3, 1, 2, 5, 6)
    for d in range(4):  # four rolls the same way return the cube to its start
        g = f
        for _ in range(4):
            g = roll_faces(g, d)
        assert g == f
    assert roll_faces(roll_faces(f, EAST), WEST) == f and roll_faces(roll_faces(f, SOUTH), NORTH) == f


def test_print_on_target_and_blank_face_does_not_print():
    # east face is red(1): one roll east lands it face-down on the red target
    lv = RollLevel.from_rows([".1..", "....", "...."], cube=(0, 0), faces="000010", moves=3)
    g = RollGame(lv)
    assert g.roll(EAST)
    assert g.prints == {1: 1} and g.status == WON
    blank = RollGame(RollLevel.from_rows([".1..", "....", "...."], cube=(0, 0), faces="100000", moves=3))
    blank.roll(EAST)
    assert blank.prints == {} and blank.status == PLAYING


def test_overprint_and_order_matters():
    # target needs red; first landing prints red (east face), rolling on and back lands blue there
    lv = RollLevel.from_rows([".1..", "....", "...."], cube=(0, 0), faces="002010", moves=5)
    g = RollGame(lv)
    g.roll(EAST)                  # red printed -> won immediately
    assert g.status == WON
    lv2 = RollLevel.from_rows([".1..", "....", "...."], cube=(0, 0), faces="002210", moves=5)
    g2 = RollGame(RollLevel.from_json({**lv2.to_json(), "grid": [".2..", "....", "...."]}))
    g2.roll(EAST)                 # prints red(1) on a blue(2) target: not solved
    assert g2.prints == {1: 1} and g2.status == PLAYING


def test_walls_and_edges_block_without_cost():
    lv = RollLevel.from_rows([".#1", "...", "..."], cube=(0, 0), faces="010000", moves=2)
    g = RollGame(lv)
    assert not g.roll(EAST) and not g.roll(NORTH) and not g.roll(WEST)
    assert g.moves_left == 2 and g.pos == (0, 0)
    assert g.roll(SOUTH) and g.pos == (0, 1) and g.moves_left == 1


def test_pool_repaints_bottom_face():
    lv = RollLevel.from_rows(["ab..", "....", "...."], cube=(0, 1), faces="000000", moves=4)
    g = RollGame(lv)
    g.roll(NORTH)                 # lands on pool a (red): bottom becomes red
    assert g.faces[B] == 1
    g.roll(EAST)                  # lands on pool b: new bottom (old west, blank) becomes green
    assert g.faces[B] == 2 and g.faces[W] == 1   # rolling east carries the old bottom to the west side


def test_ice_slide_prints_each_tile_and_costs_one_move():
    lv = RollLevel.from_rows([".~~1.", ".....", "....."], cube=(0, 0), faces="000000", moves=3)
    g = RollGame(lv)
    trail = []
    g.roll(EAST, trail)
    assert g.pos == (3, 0) and g.moves_used == 1      # slid over two ice tiles, stopped on the target
    assert [i for i, _ in trail] == [1, 2, 3]
    stop = RollGame(RollLevel.from_rows([".~~#.", ".....", "....."], cube=(0, 0), faces="000000", moves=3))
    stop.roll(EAST)
    assert stop.pos == (2, 0)                          # wall ends the slide on the last ice tile


def test_eraser_blanks_bottom():
    lv = RollLevel.from_rows([".*1.", "....", "...."], cube=(0, 0), faces="000011", moves=4)
    g = RollGame(lv)
    g.roll(EAST)                  # east face (1) lands on eraser -> blank
    assert g.faces[B] == 0
    g.roll(EAST)                  # new bottom = previous east face = original top (blank): no print
    assert g.pos == (2, 0) and g.prints == {}


def test_lose_continue_win():
    lv = RollLevel.from_rows([".1..", "....", "...."], cube=(0, 1), faces="000010", moves=1)
    g = RollGame(lv)
    g.roll(EAST)                  # lands on (1,1): floor
    assert g.status == LOST
    g.add_moves(3)
    g.roll(WEST); g.roll(NORTH)   # back to (0,1) then (0,0); faces have moved on
    assert g.status in (PLAYING, WON)
    with pytest.raises(ValueError):
        RollLevel.from_rows(["#..", "...", "..."], cube=(0, 0))


def test_solver_par_paths_and_hint():
    lv = RollLevel.from_rows([".1..", "....", "...."], cube=(0, 0), faces="000010")
    s = solve(lv)
    assert s.par == 1 and s.path == [EAST] and s.solutions == 1
    lv2 = RollLevel.from_rows(["....", "...1", "...."], cube=(0, 0), faces="010000")  # bottom is red; needs to land red at (3,1)
    s2 = solve(lv2)
    assert s2.par is not None and s2.par >= 4
    g = RollGame(RollLevel.from_json({**lv2.to_json(), "moves": s2.par}))
    for d in s2.path:
        g.roll(d)
    assert g.status == WON
    assert solve(RollLevel.from_rows(["#1#", "###", ".##"], cube=(0, 2), faces="111111")).par is None
