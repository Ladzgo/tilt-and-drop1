from tiltdrop.cluster import feature_vector, kmeans
from tiltdrop.curate import curate_range, segments
from tiltdrop.difficulty import analyze
from tiltdrop.generator import GenSpec, build_board, generate_one, relaxed_reachable
from tiltdrop.model import Level
from tiltdrop.pacing import BEATS
from tiltdrop.sim import Game, WON, is_stable

import random


def test_every_beat_spec_generates_valid_levels():
    for b in BEATS:
        if b.name in ("review", "pool"):
            continue
        c = generate_one(b.spec, 42, bot_runs=10)
        assert c is not None, b.name
        lv = c.level
        assert is_stable(lv)
        assert lv.colors() <= lv.gate_colors()
        assert b.spec.par[0] <= c.analysis.par <= b.spec.par[1]
        for r in b.spec.require:
            assert r in lv.mechanics
        # the stored solution really solves it
        lv.moves = c.analysis.par
        g = Game(lv)
        for ch in c.analysis.solution:
            g.rotate("CW" if ch == "R" else "CCW")
        assert g.status == WON


def test_generation_is_reproducible():
    spec = BEATS[2].spec
    a, b = generate_one(spec, 7, bot_runs=5), generate_one(spec, 7, bot_runs=5)
    assert a.level.to_json() == b.level.to_json()


def test_board_builder_and_prefilter():
    rng = random.Random(3)
    built = [build_board(GenSpec(sizes=((6, 6),), colors=(2, 3)), rng) for _ in range(50)]
    assert any(built)
    assert all(lv is None or (is_stable(lv) and relaxed_reachable(lv)) for lv in built)
    open_lane = Level.from_rows(["..#.", "#...", "....", ".1.."], {"T": "1..."})
    assert relaxed_reachable(open_lane)  # right, up, left, up
    trapped = Level.from_rows(["1#..", "##..", "....", "...."], {"R": "1..."})
    assert not relaxed_reachable(trapped)
    assert build_board(GenSpec(sizes=((3, 3),), colors=(6, 6), blocks_per_color=(3, 3)), rng) is None


def test_difficulty_monotone_on_examples():
    easy = analyze(Level.from_rows(["....", "....", "....", ".1.."], {"R": "...1"}), bot_runs=50)
    hard = generate_one(BEATS[6].spec, 1, bot_runs=50).analysis
    assert easy.score() < hard.score()
    assert set(easy.features()) == {"par", "rarity", "dead", "bot", "random", "size", "colors"}


def test_kmeans_and_features():
    assert kmeans([], 3) == []
    labels = kmeans([[0.0, 0.0]] * 5 + [[1.0, 1.0]] * 5, 2, seed=1)
    assert len(set(labels[:5])) == 1 and len(set(labels[5:])) == 1 and labels[0] != labels[5]
    lv = Level.from_rows(["1..", "@..", "..."], {"T": "1.."})
    assert len(feature_vector(lv, 2, 1)) == 10


def test_segments_split_on_beats_and_worlds():
    segs = segments(1, 120)
    assert segs[0] == list(range(1, 11)) and segs[1][0] == 11
    assert all(len(s) <= 50 for s in segs)
    assert [s[0] for s in segs] == [1, 11, 31, 51, 61, 91, 101]


def test_curate_small_range_with_handmade_tutorials():
    levels = curate_range(1, 10, k_per_slot=4, workers=1, bot_runs=10, final_bot_runs=10)
    assert [lv.id for lv in levels] == [f"L{n:05d}" for n in range(1, 11)]
    assert levels[0].meta.get("manual") and levels[0].par == 1 and levels[2].par == 3
    for lv in levels:
        assert lv.moves == lv.par + {"E": 4, "N": 2, "H": 1, "SH": 0}[lv.tier]
        assert is_stable(lv)
