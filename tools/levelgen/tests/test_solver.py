import pytest

from tiltdrop.model import CCW, CW, Level
from tiltdrop.sim import WON, Game
from tiltdrop.solver import effective_branching, explore, min_moves, solve

TWO_COLOR = Level.from_rows(["....", "....", "11.2"], {"L": "..1", "R": "..2"})


def test_known_answers_hand_enumerated():
    r = solve(TWO_COLOR, slack=0)
    assert r.solvable and r.min_moves == 3
    assert r.solutions_at_min == 2  # CW,CCW,CCW and CCW,CW,CW
    assert r.random_win_rate == pytest.approx(0.25)
    assert r.dead_end_ratio == pytest.approx(2 / 6)
    assert r.limit == 3


def test_solution_actually_wins():
    r = solve(TWO_COLOR)
    lv = Level.from_json({**TWO_COLOR.to_json(), "moves": r.min_moves})
    g = Game(lv)
    for rot in r.solution:
        g.rotate(rot)
    assert g.status == WON
    assert r.to_meta()["solution"] in ("RLL", "LRR")


def test_unsolvable():
    lv = Level.from_rows(["....", "....", "11.2"], {"L": "..1"})
    r = solve(lv, max_depth=12)
    assert not r.solvable and r.min_moves is None


def test_already_won_and_min_moves_helper():
    lv = Level.from_rows(["....", "....", "@..."])
    assert solve(lv).min_moves == 0
    mw, first = min_moves(TWO_COLOR)
    assert mw == 3 and first in (CW, CCW)
    assert min_moves(Level.from_rows(["1..", "#..", "#.."]), max_depth=6) == (None, None)


def test_node_cap_truncates():
    gr = explore(TWO_COLOR, node_cap=3)
    assert gr.truncated


def test_limit_from_level_moves():
    lv = Level.from_json({**TWO_COLOR.to_json(), "moves": 9})
    r = solve(lv)
    assert r.limit == 9 and r.random_win_rate > 0.25


def test_effective_branching():
    assert effective_branching(10, 0) == 1.0
    assert effective_branching(2**4 - 1, 3) == pytest.approx(2.0, abs=1e-3)
    assert 1.0 < effective_branching(10, 3) < 2.0
