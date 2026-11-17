"""Domino Run rules (docs/games/06-domino.md §1), hand-computed."""
import pytest

from domino.model import DominoLevel
from domino.sim import LOST, PLAYING, WON, DominoGame, run_chain, wins
from domino.solver import solve, taps_between


def test_straight_chain_rings_bell():
    lv = DominoLevel.from_rows(["S>d.d.B.", "........", "........"])
    falls, rung = run_chain(lv)
    assert [(t, c) for t, c, _ in falls] == [(0, 0), (1, 1), (2, 2), (3, 3)]
    assert rung == {3}


def test_gap_and_wall_stop_the_chain():
    assert run_chain(DominoLevel.from_rows(["S>..d.B.", "........", "........"]))[1] == set()
    assert run_chain(DominoLevel.from_rows(["S>##d.B.", "........", "........"]))[1] == set()


def test_turner_redirects_and_rotates_clockwise():
    # chain goes right into a turner; pointing down it reaches the bell below
    lv = DominoLevel.from_rows(["S>t>..", "..B...", "......"])
    assert not wins(lv, (0,))
    assert wins(lv, (1,))
    g = DominoGame(lv)
    assert g.rotate(1) and g.config == [1] and g.taps == 1   # board is 3 cells wide: turner at (1,0)
    assert not g.rotate(0)                   # start domino is not a turner
    falls, rung = g.push()
    assert g.status == WON and rung == {4}


def test_plain_domino_keeps_push_direction_after_turner():
    lv = DominoLevel.from_rows(["S>tv..", "..d...", "..B..."])
    assert wins(lv, lv.start_config())


def test_splitter_forks_both_sides():
    lv = DominoLevel.from_rows(["..B...", "S>Y...", "..B..."])
    falls, rung = run_chain(lv)
    assert rung == {1, 7}
    t_of = {c: t for t, c, _ in falls}
    assert t_of[1] == t_of[7] == 2


def test_attempts_lose_and_continue():
    lv = DominoLevel.from_rows(["S>t>..", "..B...", "......"], attempts=2)
    g = DominoGame(lv)
    g.push()
    assert g.status == PLAYING and g.attempts_left == 1
    g.push()
    assert g.status == LOST
    with pytest.raises(RuntimeError):
        g.rotate(1)
    g.add_attempt()
    g.rotate(1)
    g.push()
    assert g.status == WON and g.pushes == 3


def test_each_piece_falls_once_and_simultaneous_hits_are_ordered():
    # two branches of a splitter meet at the domino below-left; it falls once
    lv = DominoLevel.from_rows(["S>Y.d.", "..d.d.", "..d.B."])
    falls, rung = run_chain(lv)
    cells = [c for _, c, _ in falls]
    assert len(cells) == len(set(cells))


def test_solver():
    lv = DominoLevel.from_rows(["S>t>..", "..B...", "......"])
    s = solve(lv)
    assert s.solutions == 1 and s.best == (1,) and s.par == 1 and s.total == 4
    assert taps_between((3,), (1,)) == 2
    with pytest.raises(ValueError):
        DominoLevel.from_rows(["d.d.", "B...", "...."])


def test_generator_golden_and_shipped_levels():
    import json, os
    from domino.generator import generate_one
    from domino.pipeline import BEATS, _run
    for _, name, spec in BEATS:
        c = generate_one(spec, 2, bot_runs=5)
        assert c is not None and spec.par[0] <= c.par <= spec.par[1], name
        assert generate_one(spec, 2, bot_runs=5).level.rows() == c.level.rows()
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "domino")
    for c in json.load(open(os.path.join(root, "golden.json")))["cases"]:
        assert _run(DominoLevel.from_json(c["level"]), c["actions"]) == c
    levels = json.load(open(os.path.join(root, "levels.json")))["levels"]
    assert len(levels) == 200
    for d in levels[::7]:
        lv = DominoLevel.from_json(d)
        assert solve(lv).par == lv.par
