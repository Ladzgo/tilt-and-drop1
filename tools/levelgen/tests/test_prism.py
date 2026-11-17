"""Prism Paths rules (docs/games/02-prism.md §1), hand-computed expectations."""
import pytest

from prism.model import MIRROR, SOURCE, TARGET, PrismLevel, parse_row, cell_token
from prism.sim import LOST, PLAYING, WON, PrismGame, is_win, lit, trace
from prism.solver import solve


def idx(lv, x, y):
    return y * lv.w + x


def test_tokens_roundtrip():
    row = r".#/\[R>](M){/}<G>[\][Bv](W)"
    cells = parse_row(row)
    assert "".join(cell_token(c) for c in cells) == row
    for bad in ("x", "[X>]", "{a}", "<Q>"):
        with pytest.raises(ValueError):
            parse_row(bad)


def test_mirror_reflections():
    # right-moving beam: '/' sends it up into (R); '\' sends it down out of the board
    up = PrismLevel.from_rows(["..(R)", "[R>]./", "..."])
    assert is_win(up, trace(up))
    down = PrismLevel.from_rows(["..(R)", "[R>].\\", "..."])
    assert not is_win(down, trace(down))
    # down-moving beam: '/' sends it left, '\' right
    left = PrismLevel.from_rows([".[Rv].", "(R)/.", "..."])
    assert is_win(left, trace(left))
    right = PrismLevel.from_rows([".[Rv].", ".\\(R)", "..."])
    assert is_win(right, trace(right))


def test_tap_to_win_and_move_accounting():
    lv = PrismLevel.from_rows(["..(R)", "[R>].\\", "..."], moves=2)
    g = PrismGame(lv)
    assert g.status == PLAYING
    assert not g.tap(idx(lv, 0, 0))          # empty cell: no move spent
    assert g.moves_left == 2
    assert g.tap(idx(lv, 2, 1))
    assert g.status == WON and g.moves_left == 1
    with pytest.raises(RuntimeError):
        g.tap(idx(lv, 2, 1))


def test_lose_and_continue():
    # target (2,0); the path mirror (2,1) starts wrong; (1,2) is a decoy off the light path
    lv = PrismLevel.from_rows(["..(R)", "[R>].\\", "./."], moves=1)
    g = PrismGame(lv)
    g.tap(idx(lv, 1, 2))
    assert g.status == LOST
    g.add_moves(3)
    assert g.status == PLAYING
    g.tap(idx(lv, 2, 1))
    assert g.status == WON and g.moves_used == 2


def test_additive_mixing_and_muddy():
    # red from the left and blue from above meet in a magenta target
    lv = PrismLevel.from_rows([".[Bv].", "[R>](M).", "..."])
    assert is_win(lv, trace(lv))
    only_red = PrismLevel.from_rows(["...", "[R>](M).", "..."])
    assert trace(only_red)[idx(only_red, 1, 1)] == 1 and not is_win(only_red, trace(only_red))
    # a red target that also receives blue is muddy (not lit)
    muddy = PrismLevel.from_rows([".[Bv].", "[R>](R).", "..."])
    assert trace(muddy)[idx(muddy, 1, 1)] == 5
    assert lit(muddy, trace(muddy)) == (0, 1)


def test_filters_walls_sources_absorb():
    lv = PrismLevel.from_rows(["....", "[R>]<R>.(R)", "...."])
    assert is_win(lv, trace(lv))
    blocked = PrismLevel.from_rows(["....", "[R>]<B>.(R)", "...."])
    assert not is_win(blocked, trace(blocked))
    wall = PrismLevel.from_rows(["....", "[R>]#.(R)", "...."])
    assert not is_win(wall, trace(wall))
    src = PrismLevel.from_rows(["....", "[R>].[G<](R)", "...."])
    assert not is_win(src, trace(src))  # red is stopped by the green source


def test_beams_cross_without_interacting():
    lv = PrismLevel.from_rows([".[Bv]..", "[R>]..(R)", ".(B).."])
    assert is_win(lv, trace(lv))


def test_splitter_sends_both_ways():
    lv = PrismLevel.from_rows(["..(R).", "[R>].[/](R)", "...."])
    r = trace(lv)
    assert r[idx(lv, 2, 0)] == 1 and r[idx(lv, 3, 1)] == 1 and is_win(lv, r)


def test_loop_guard_terminates():
    # fixed mirrors form a loop; the beam enters it and must stop
    lv = PrismLevel.from_rows(["{\\}..{/}", "[R>]...", "{/}..{\\}", "...(R)"])
    trace(lv)  # must not hang


def test_segments_for_rendering():
    lv = PrismLevel.from_rows(["..(R)", "[R>]./", "..."])
    seg = []
    trace(lv, segments=seg)
    assert seg == [(1, 0, 1, 2, 1), (1, 2, 1, 2, 0)]


def test_solver_par_and_uniqueness():
    lv = PrismLevel.from_rows(["..(R)", "[R>].\\", "..."])
    s = solve(lv)
    assert s.par == 1 and s.taps == [idx(lv, 2, 1)] and s.solutions == 1
    # path: source (0,2) -> '\\' (3,2) down -> (3,3) must be '/' -> left -> (1,3) must be '\\' -> up to (R) at (1,0)
    lv2 = PrismLevel.from_rows([".(R)...", ".....", "[R>]..\\.", "./.\\."])
    s2 = solve(lv2)
    assert s2.par == 2 and s2.solutions == 1
    assert s2.taps == [idx(lv2, 1, 3), idx(lv2, 3, 3)]
    g = PrismGame(PrismLevel.from_json({**lv2.to_json(), "moves": s2.par}))
    for t in s2.taps:
        g.tap(t)
    assert g.status == WON
    unsolvable = PrismLevel.from_rows(["(G)..", "[R>]./", "..."])
    assert solve(unsolvable).par is None


def test_generator_levels_valid_and_reproducible():
    from prism.generator import generate_one
    from prism.pipeline import BEATS
    for _, name, spec in BEATS:
        c = generate_one(spec, 3, bot_runs=10)
        assert c is not None, name
        lv = c.level
        assert not is_win(lv, trace(lv)) and spec.par[0] <= c.par <= spec.par[1]
        assert all(lv.cells[i][1] for i in lv.targets)
        g = PrismGame(PrismLevel.from_json({**lv.to_json(), "moves": c.par}))
        for t in solve(lv).taps:
            g.tap(t)
        assert g.status == WON
        assert generate_one(spec, 3, bot_runs=10).level.rows() == lv.rows()


def test_golden_file_matches_simulator():
    import json, os
    from prism.pipeline import _run
    path = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "prism", "golden.json")
    data = json.load(open(path))
    for c in data["cases"]:
        assert _run(PrismLevel.from_json(c["level"]), c["taps"]) == c
    for c in data["solver"][:20]:
        s = solve(PrismLevel.from_json(c["level"]))
        assert (s.par, s.solutions) == (c["par"], c["solutions"])


def test_shipped_levels_solvable_at_par():
    import json, os
    path = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "prism", "levels.json")
    levels = json.load(open(path))["levels"]
    assert len(levels) == 200
    for d in levels[::7]:
        lv = PrismLevel.from_json(d)
        assert solve(lv).par == lv.par <= lv.moves
