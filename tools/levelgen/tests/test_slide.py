"""Slide Out rules (docs/games/04-slide.md §1), hand-computed."""
import pytest

from slide.model import SlideLevel
from slide.sim import LOST, PLAYING, WON, SlideGame, apply_tap, blocker
from slide.solver import solve


def test_tokens_roundtrip_and_validation():
    lv = SlideLevel.from_rows(["1>..##", "..2v3<", "4^...."])
    assert SlideLevel.from_json(lv.to_json()).rows() == lv.rows()
    assert lv.blocks == [0, 4, 5, 6]
    for bad in (["1x.."], ["1>."]):
        with pytest.raises(ValueError):
            SlideLevel.from_rows(bad * 3)


def test_exit_bump_and_blocker():
    # block A (0,0) points right but B (2,0) is in its lane; B points down and is free
    lv = SlideLevel.from_rows(["1>..1v", "......", "......"])
    g = SlideGame(lv)
    assert blocker(lv, 0, 0) == 2
    assert g.tap_cell(0) == "bump" and g.taps == 0 and g.tray == ()
    assert g.tap_cell(2) == "exit" and g.tray == (1,)
    assert blocker(lv, g.removed, 0) is None
    assert g.tap_cell(0) == "exit" and g.tray == (1, 1)
    assert g.tap_cell(4) is None      # empty cell


def test_walls_block_forever():
    lv = SlideLevel.from_rows(["1>##..", "......", "......"])
    assert apply_tap(lv, 0, (), 0) is None


def test_three_of_a_colour_pop_and_win():
    lv = SlideLevel.from_rows(["1^1^1^", "......", "......"])
    g = SlideGame(lv)
    assert g.tap_cell(0) == "exit" and g.tap_cell(1) == "exit"
    assert g.tap_cell(2) == "match" and g.tray == () and g.status == WON


def test_full_tray_loses_and_continue_adds_slots():
    lv = SlideLevel.from_rows(["1^2^3^", "1^2^3^", "1^2^3^"], capacity=3)
    g = SlideGame(lv)
    g.tap_cell(0); g.tap_cell(1)
    assert g.status == PLAYING
    g.tap_cell(2)                     # tray 1,2,3 = full, no match
    assert g.status == LOST
    with pytest.raises(RuntimeError):
        g.tap_cell(3)
    g.add_slots(2)
    assert g.status == PLAYING and g.capacity == 5


def test_match_frees_space_before_full_check():
    lv = SlideLevel.from_rows(["1^1^2^", "......", "1^...."], capacity=3)
    g = SlideGame(lv)
    g.tap_cell(0); g.tap_cell(1)      # tray 1,1
    assert g.tap_cell(6) == "match"  # third red pops: tray empty, not "full"
    assert g.tray == () and g.status == PLAYING


def test_solver_finds_order_and_detects_dead_ends():
    # lanes chain (2,0) -> (2,2) -> (0,2) -> (0,1); taking the free blues first fills the tray
    lv = SlideLevel.from_rows(["1^2^2^", "1v....", "2>..1^"], capacity=4)
    s = solve(lv)
    assert s.solvable and len(s.order) == 6
    g = SlideGame(lv)
    for k in s.order:
        g.tap_cell(lv.blocks[k])
    assert g.status == WON
    assert s.dead_end_ratio > 0
    impossible = SlideLevel.from_rows(["1>1<..", "......", "1^...."])  # two reds face each other: both stuck
    assert not solve(impossible).solvable


def test_generator_is_solvable_by_construction_and_reproducible():
    from slide.generator import build, generate_one
    from slide.pipeline import BEATS
    import random
    for _, name, spec in BEATS:
        for seed in range(3):
            lv = build(spec, random.Random(seed))
            if lv is None:
                continue
            assert solve(lv).solvable, name
            counts = {}
            for i in lv.blocks:
                counts[lv.cells[i][0]] = counts.get(lv.cells[i][0], 0) + 1
            assert all(v % 3 == 0 for v in counts.values())
        assert generate_one(spec, 4, bot_runs=5).level.rows() == generate_one(spec, 4, bot_runs=5).level.rows()


def test_golden_and_shipped_levels():
    import json, os
    from slide.pipeline import _run
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "slide")
    g = json.load(open(os.path.join(root, "golden.json")))
    for c in g["cases"]:
        assert _run(SlideLevel.from_json(c["level"]), c["taps"]) == c
    levels = json.load(open(os.path.join(root, "levels.json")))["levels"]
    assert len(levels) == 200
    for d in levels[::9]:
        assert solve(SlideLevel.from_json(d)).solvable
