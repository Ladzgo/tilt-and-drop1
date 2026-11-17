"""Flow Fill rules (docs/games/08-flow.md §1), hand-computed."""
import pytest

from flow.model import FlowLevel, rot_mask
from flow.sim import LOST, PLAYING, WON, FlowGame, evaluate
from flow.solver import solve, taps_for


def lv(rows, sources, cups, moves=5):
    return FlowLevel.from_json({"w": len(rows[0]) // 2, "h": len(rows), "grid": rows, "sources": sources, "cups": cups, "moves": moves})


def test_masks_rotate_clockwise():
    assert rot_mask(0b0101, 1) == 0b1010          # straight: right/left -> down/up
    assert rot_mask(0b1001, 1) == 0b0011          # corner up+right -> right+down
    assert rot_mask(0b1101, 2) == 0b0111


def test_straight_pipe_connects_source_to_cup():
    L = lv(["..i0.."] + ["......"] * 2, [[0, 0, 1, 0]], [[2, 0, 1, 2]])
    st = evaluate(L)
    assert st["win"] and st["leaks"] == [] and st["colour"][1] == 2
    bad = lv(["..i1.."] + ["......"] * 2, [[0, 0, 1, 0]], [[2, 0, 1, 2]])
    st = evaluate(bad)
    assert not st["win"] and st["leaks"] == [(0, 0)]   # source pours into a pipe without a matching opening


def test_leak_at_open_end_and_board_edge():
    L = lv(["..l2..", "......", "......"], [[0, 0, 1, 0]], [[2, 2, 1, 3]])   # corner down+left: water turns down into empty (1,1)
    st = evaluate(L)
    assert (1, 1) in st["leaks"]                       # leak recorded as (cell 1, direction down)
    blocked = lv(["..l1..", "......", "......"], [[0, 0, 1, 0]], [[2, 2, 1, 3]])
    assert evaluate(blocked)["leaks"] == [(0, 0)]      # corner right+down has no left opening: the source itself leaks


def test_contamination_when_colours_meet():
    L = lv(["..i0..", "......", "......"], [[0, 0, 1, 0], [2, 0, 2, 2]], [[1, 2, 1, 3]])
    st = evaluate(L)
    assert 1 in st["contaminated"] and not st["win"]   # both colours also flow on into the opposite source cell


def test_tap_rotates_and_wins():
    L = lv(["..i1.."] + ["......"] * 2, [[0, 0, 1, 0]], [[2, 0, 1, 2]], moves=2)
    g = FlowGame(L)
    assert not g.tap(0)                                  # source is not a pipe
    assert g.tap(1) and g.status == WON and g.moves_used == 1
    L2 = lv(["..i1.."] + ["......"] * 2, [[0, 0, 1, 0]], [[2, 0, 1, 2]], moves=1)
    g2 = FlowGame(L2)
    g2.tap(1)
    assert g2.status == WON


def test_lose_continue_and_fixed_pipes():
    L = lv(["..l0..", "..I1..", "......"], [[0, 0, 1, 0]], [[1, 2, 1, 3]], moves=1)
    g = FlowGame(L)
    g.tap(1)                                             # l0 (up+right) -> l1 (right+down): still no left opening
    assert g.status == LOST
    g.add_moves(3)
    g.tap(1)                                             # l2 (down+left): water turns down through fixed I1 into the cup
    assert g.status == WON
    g3 = FlowGame(lv(["..l1..", "..I1..", "......"], [[0, 0, 1, 0]], [[1, 2, 1, 3]], moves=1))
    assert not g3.tap(4)                                 # fixed pipe
    g3.tap(1)
    assert g3.status == WON


def test_solver_par_count_and_tee():
    L = lv(["..l0..", "..I1..", "......"], [[0, 0, 1, 0]], [[1, 2, 1, 3]])
    s = solve(L)
    assert s.par == 2 and s.solutions == 1               # l0 -> l2 takes two clockwise turns
    # tee feeding two cups
    T = lv(["......", "..t1..", "......"], [[1, 0, 1, 1]], [[0, 1, 1, 0], [2, 1, 1, 2]])   # tee must face up, left, right
    s2 = solve(T)
    assert s2.par is not None and s2.solutions == 1
    g = FlowGame(FlowLevel.from_json({**T.to_json(), "moves": s2.par}))
    start = {i: T.pipes[i][1] for i in T.rotatables}
    for c in taps_for(T, start, s2.rot):
        g.tap(c)
    assert g.status == WON
    with pytest.raises(ValueError):
        lv(["i0...."] * 3, [[0, 0, 1, 0]], [[2, 0, 1, 2]])  # source on a pipe cell


def test_generator_golden_and_shipped_levels():
    import json, os
    from flow.generator import generate_one
    from flow.pipeline import BEATS, _run
    for _, name, spec in BEATS[:4]:
        c = generate_one(spec, 2, bot_runs=5)
        assert c is not None and spec.par[0] <= c.par <= spec.par[1], name
        assert generate_one(spec, 2, bot_runs=5).level.to_json() == c.level.to_json()
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "flow")
    for c in json.load(open(os.path.join(root, "golden.json")))["cases"]:
        assert _run(FlowLevel.from_json(c["level"]), c["taps"]) == c
    levels = json.load(open(os.path.join(root, "levels.json")))["levels"]
    assert len(levels) == 200
    for d in levels[::9]:
        L = FlowLevel.from_json(d)
        s = solve(L)
        assert s.par == d["par"]
        g = FlowGame(L)
        for cell in d["meta"]["solution"]:
            g.tap(cell)
        assert g.status == WON
