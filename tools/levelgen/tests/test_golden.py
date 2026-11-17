import json
import os

import pytest

from tiltdrop.golden import HAND_CASES, dumps, hand_cases, random_cases, run_case
from tiltdrop.model import Level

GOLDEN = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "golden")


def test_hand_cases_cover_all_event_types():
    events = "".join(e for c in hand_cases() for s in c["steps"] for e in s["events"])
    for prefix in ("M", "X", "H", "R", "K"):
        assert prefix in events
    assert len(HAND_CASES) >= 12


def test_random_cases_deterministic():
    assert dumps(random_cases(20)) == dumps(random_cases(20))


@pytest.mark.parametrize("name", ["rules.json", "random.json"])
def test_committed_golden_vectors_match_simulator(name):
    path = os.path.join(GOLDEN, name)
    if not os.path.exists(path):
        pytest.skip("golden vectors not generated yet")
    with open(path) as f:
        data = json.load(f)
    for case in data["cases"]:
        lv = Level.from_json(case["level"])
        inputs = [s["input"] for s in case["steps"]]
        full = name == "rules.json"
        assert run_case(case["name"], lv, inputs, full_every_step=full) == case, case["name"]
