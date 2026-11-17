"""Fold rules (docs/games/07-fold.md §1), hand-computed."""
import pytest

from fold.model import FoldLevel
from fold.sim import LOST, PLAYING, WON, FoldGame, fold, initial_sheet, legal_folds, visible
from fold.solver import solve


def lv(front, back, target, moves=5):
    return FoldLevel(len(front[0]), len(front), [int(c) for r in front for c in r], [int(c) for r in back for c in r], target, moves)


def test_vertical_fold_flips_and_stacks():
    L = lv(["12"], ["34"], ["3"])          # square 0: front 1 / back 3 ; square 1: front 2 / back 4
    s = fold(initial_sheet(L), "V", 1, 0)   # left half over the right: square 0 lands face down on square 1
    assert s == ((((0, 0), (1, 1)),),)
    assert visible(L, s) == ["3"]
    s2 = fold(initial_sheet(L), "V", 1, 1)  # right over left: square 1 face down on square 0
    assert visible(L, s2) == ["4"]


def test_mirror_positions_and_overhang_rule():
    L = lv(["1234"], ["5555"], ["55"])
    s = fold(initial_sheet(L), "V", 2, 0)   # col1 lands on old col2 (new x0), col0 on old col3 (new x1)
    assert [st[0][0] for st in s[0]] == [1, 0]
    assert fold(initial_sheet(L), "V", 3, 0) is None   # 3 columns cannot fold over 1
    assert fold(initial_sheet(L), "V", 3, 1) is not None
    assert ("V", 1, 0) in legal_folds(initial_sheet(L)) and ("V", 1, 1) not in legal_folds(initial_sheet(L))


def test_horizontal_fold_and_double_flip():
    L = lv(["1", "2"], ["3", "4"], ["1"])
    s = fold(initial_sheet(L), "H", 1, 1)   # bottom over top: square 1 face down on square 0
    assert visible(L, s) == ["4"]
    # folding a stack again reverses it and flips faces back
    L2 = lv(["12", "34"], ["56", "66"], ["0"])
    s = fold(initial_sheet(L2), "V", 1, 0)
    s = fold(s, "H", 1, 0)
    # after fold 1: row0 = [0 down, 1 up], row1 = [2 down, 3 up]; fold 2 flips row0's stack onto row1
    assert s[0][0] == ((1, 0), (0, 1), (2, 0), (3, 1))   # square 0 has now been flipped twice: face up again


def test_game_win_lose_continue():
    L = lv(["12"], ["34"], ["3"], moves=1)
    g = FoldGame(L)
    assert not g.fold("V", 2, 0) and g.moves_used == 0
    assert g.fold("V", 1, 1) and g.status == LOST      # shows 4, wanted 3
    g.add_moves(3)
    assert g.status == PLAYING
    assert not g.fold("V", 1, 0)                        # sheet is 1 wide now: no crease
    g2 = FoldGame(L)
    g2.fold("V", 1, 0)
    assert g2.status == WON


def test_solver():
    L = lv(["12", "34"], ["56", "65"], ["6"])
    s = solve(L)
    assert s.par == 2 and s.solutions >= 1
    g = FoldGame(FoldLevel.from_json({**L.to_json(), "moves": s.par}))
    for mv in s.path:
        g.fold(*mv)
    assert g.status == WON
    assert solve(lv(["12"], ["34"], ["9"])).par is None
    with pytest.raises(ValueError):
        FoldLevel(2, 1, [1], [1, 2], ["1"])


def test_generator_golden_and_shipped_levels():
    import json, os
    from fold.generator import generate_one
    from fold.pipeline import BEATS, _run
    for _, name, spec in BEATS[:4]:
        c = generate_one(spec, 2, bot_runs=5)
        assert c is not None and spec.par[0] <= c.par <= spec.par[1], name
        assert generate_one(spec, 2, bot_runs=5).level.to_json() == c.level.to_json()
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "fold")
    for c in json.load(open(os.path.join(root, "golden.json")))["cases"]:
        assert _run(FoldLevel.from_json(c["level"]), [tuple(m) for m in c["moves"]]) == c
    levels = json.load(open(os.path.join(root, "levels.json")))["levels"]
    assert len(levels) == 200
    for d in levels[::9]:
        assert solve(FoldLevel.from_json(d)).par == d["par"]
