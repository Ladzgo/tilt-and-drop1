import pytest

from tiltdrop.pacing import BEATS, TIER_SLACK, beat_for_level, is_intro, schedule, spec_for_level, tier_for_level, world_of


def spikes(levels):
    return [n for n, t, _ in levels if t in ("H", "SH")]


def test_sawtooth_rules_hold_for_2000_levels():
    sch = schedule(2000)
    tiers = {n: t for n, t, _ in sch}
    for n in range(40, 2001, 20):
        assert tiers[n] == "SH"
    sp = [n for n in spikes(sch) if n > 20]
    gaps = [b - a for a, b in zip(sp, sp[1:])]
    assert min(gaps) >= 5 and max(gaps) <= 8
    for n in sp:
        follow = 3 if tiers[n] == "SH" else 2
        for k in range(1, follow + 1):
            if n + k <= 2000 and not is_intro(n + k):
                assert tiers[n + k] == "E", (n, k)


def test_onboarding_and_intros_are_easy():
    assert all(tier_for_level(n) == "E" for n in range(1, 6))
    assert all(tier_for_level(n) != "SH" for n in range(1, 21))
    for b in BEATS:
        if b.intro:
            assert tier_for_level(b.start) == "E"


def test_beats_and_worlds():
    assert beat_for_level(1).name == "basics"
    assert beat_for_level(30).name == "two_colors"
    assert beat_for_level(91).name == "anchor"
    assert beat_for_level(5000).name == "pool"
    assert world_of(50) == 1 and world_of(51) == 2
    assert "anchor" in spec_for_level(95).require
    assert spec_for_level(400, 0) is not None
    with pytest.raises(ValueError):
        tier_for_level(0)
    with pytest.raises(ValueError):
        beat_for_level(0)


def test_slack_ordering():
    assert TIER_SLACK["E"] > TIER_SLACK["N"] > TIER_SLACK["H"] >= TIER_SLACK["SH"]


def test_difficulty_ramps_gradually():
    from tiltdrop.pacing import min_par, target_fail
    for t in ("E", "N", "H", "SH"):
        vals = [target_fail(n, t) for n in range(1, 2001)]
        assert vals == sorted(vals) and vals[-1] <= 0.95
    assert target_fail(1, "N") < target_fail(150, "N") < target_fail(300, "N")
    assert target_fail(300, "N") < target_fail(300, "SH")
    assert min_par(5) == 1 and min_par(120) == 5 and min_par(280) == 9
