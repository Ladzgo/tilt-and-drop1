"""Gear Works rules (docs/games/05-gears.md §1), hand-computed."""
import pytest

from gears.model import GearLevel
from gears.sim import LOST, PLAYING, WON, GearGame, is_win, spins
from gears.solver import solve, tap_order


def test_alternating_spin_along_a_chain():
    lv = GearLevel.from_rows(["MGGR", "....", "...."])
    spin, jam = spins(lv, 0)
    assert spin[:4] == [1, -1, 1, -1] and not any(jam)
    assert not is_win(lv, 0)                       # R needs clockwise but turns counter-clockwise
    assert is_win(GearLevel.from_rows(["MGGL", "....", "...."]), 0)


def test_hex_neighbours():
    from gears.sim import neighbours
    assert sorted(neighbours(4, 3, 5)) == sorted([4, 6, 1, 2, 9, 10])   # (1,1), odd row, shifted right
    assert sorted(neighbours(4, 3, 1)) == sorted([0, 2, 4, 5])          # (1,0), even row, top edge


def test_triangle_jams_the_whole_group():
    tri = GearLevel.from_rows(["MG..", "G...", "...."])   # (0,0),(1,0),(0,1) touch pairwise on a hex grid
    spin, jam = spins(tri, 0)
    assert jam[0] and jam[1] and jam[4] and spin[0] == 0
    two = GearLevel.from_rows(["MGM.", "....", "...."])   # motors two apart agree
    spin, jam = spins(two, 0)
    assert spin[:3] == [1, -1, 1] and not any(jam)
    bad = GearLevel.from_rows(["MM..", "....", "...."])   # touching motors clash
    spin, jam = spins(bad, 0)
    assert jam[0] and jam[1] and spin[0] == 0


def test_groups_without_motor_stay_still():
    lv = GearLevel.from_rows(["M.GR", "....", "...."])
    spin, _ = spins(lv, 0)
    assert spin[2] == 0 and spin[3] == 0


def test_tap_place_remove_inventory_and_moves():
    lv = GearLevel.from_rows(["MoR.", "....", "...."], inventory=1, moves=3)
    g = GearGame(lv)
    assert g.in_hand == 1
    assert g.tap(1) and g.status == WON and g.in_hand == 0
    lv2 = GearLevel.from_rows(["Mooo", "...L", "...."], inventory=1, moves=4)
    g2 = GearGame(lv2)
    assert g2.tap(1)
    assert not g2.tap(2)                            # no gear in hand: nothing happens, no move
    assert g2.moves_used == 1
    assert g2.tap(1) and g2.in_hand == 1            # removing returns the gear
    assert not g2.tap(0)                            # motor is not a peg


def test_lose_and_continue():
    lv = GearLevel.from_rows(["MoR.", "...o", "...."], inventory=2, moves=1)
    g = GearGame(lv)
    g.tap(7)                                        # decoy peg
    assert g.status == LOST
    g.add_moves(3)
    g.tap(7); g.tap(1)
    assert g.status == WON


def test_solver_par_and_legal_order():
    # straight row M-o-o-R puts R at distance 3 (counter-clockwise); the detour through row 1 has length 4
    lv = GearLevel.from_rows(["MooR", "oooo", "...."], inventory=3)
    s = solve(lv)
    assert s.par == 3 and s.solutions == 3   # three 4-long routes reach R clockwise
    order = tap_order(lv, lv.start_layout(), sum(1 << lv.pegs.index(c) for c in s.taps))
    g = GearGame(GearLevel.from_json({**lv.to_json(), "moves": s.par}))
    for c in order:
        assert g.tap(c)
    assert g.status == WON
    assert solve(GearLevel.from_rows(["M..R", "....", "...."], inventory=2)).par is None
    with pytest.raises(ValueError):
        GearLevel.from_rows(["Mgg.", "....", "...."], inventory=1)


def test_generator_golden_and_shipped_levels():
    import json, os
    from gears.generator import generate_one
    from gears.pipeline import BEATS, _run
    for _, name, spec in BEATS:
        c = generate_one(spec, 2, bot_runs=5)
        assert c is not None and spec.par[0] <= c.par <= spec.par[1], name
        assert generate_one(spec, 2, bot_runs=5).level.rows() == c.level.rows()
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "gears")
    for c in json.load(open(os.path.join(root, "golden.json")))["cases"]:
        assert _run(GearLevel.from_json(c["level"]), c["taps"]) == c
    levels = json.load(open(os.path.join(root, "levels.json")))["levels"]
    assert len(levels) == 200
    for d in levels[::7]:
        lv = GearLevel.from_json(d)
        assert solve(lv).par == lv.par <= lv.moves
