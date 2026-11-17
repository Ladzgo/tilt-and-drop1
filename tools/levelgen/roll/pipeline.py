"""Roll & Print content: beats, ramped curation (shared tiers), golden vectors, export."""
from __future__ import annotations

import json
import multiprocessing as mp
import os
import random
import sys
import zlib

from tiltdrop.pacing import TIER_SLACK, target_fail, tier_for_level

from .generator import RSpec, build, generate_one
from .model import RollLevel
from .sim import RollGame
from .solver import solve

BEATS = [
    (1, "first_prints", RSpec("first_prints", sizes=((4, 4),), colors=(1, 1), painted=(1, 1), targets=(1, 2), walk=(2, 6), par=(1, 4))),
    (11, "colours", RSpec("colours", sizes=((4, 4), (5, 5)), colors=(2, 3), painted=(2, 3), targets=(2, 3), walk=(5, 12), par=(3, 8))),
    (41, "pools", RSpec("pools", sizes=((5, 5),), colors=(2, 3), painted=(0, 2), targets=(2, 3), pools=(1, 3), walk=(6, 14), par=(4, 10))),
    (81, "corridors", RSpec("corridors", sizes=((5, 5), (6, 6)), colors=(2, 3), painted=(1, 3), targets=(2, 4), pools=(0, 2), walls=(0.12, 0.25), walk=(8, 16), par=(5, 12))),
    (121, "ice", RSpec("ice", sizes=((5, 5), (6, 6)), colors=(2, 3), painted=(1, 3), targets=(2, 4), pools=(0, 2), ice=(2, 5), walls=(0.05, 0.15), walk=(8, 16), par=(5, 12))),
    (161, "review", RSpec("review", sizes=((6, 6),), colors=(3, 4), painted=(2, 4), targets=(3, 4), pools=(1, 3), ice=(0, 4), walls=(0.05, 0.2), walk=(10, 18), par=(6, 13))),
]


def beat_for(n: int):
    cur = BEATS[0]
    for b in BEATS:
        if b[0] <= n:
            cur = b
    return cur


def min_par(n: int) -> int:
    return 1 if n <= 10 else 2 + n // 35


def _task(args):
    spec, seed, runs = args
    return generate_one(spec, seed, bot_runs=runs)


def curate(last: int = 200, k: int = 10, workers: int = 4, bot_runs: int = 40) -> list[RollLevel]:
    out: list[RollLevel] = []
    seen: set[str] = set()
    starts = [b[0] for b in BEATS if b[0] <= last] + [last + 1]
    with mp.get_context("fork").Pool(workers) as pool:
        for a, b in zip(starts, starts[1:]):
            spec = beat_for(a)[2]
            tasks = [(spec, zlib.crc32(f"roll:{n}:{j}".encode()), bot_runs) for n in range(a, b) for j in range(k)]
            cands = [c for c in pool.imap(_task, tasks, chunksize=2) if c is not None]
            used: set[int] = set()
            for n in range(a, b):
                tier = tier_for_level(n)
                slack = TIER_SLACK[tier]
                best, cost = None, 9e9
                for ci, c in enumerate(cands):
                    key = json.dumps(c.level.to_json()["grid"]) + str(c.level.cube) + str(c.level.faces)
                    if ci in used or key in seen:
                        continue
                    f = c.fail_by_slack[slack]
                    cst = (f + 0.05 * c.par) if n == a else abs(f - target_fail(n, tier)) + 0.12 * max(0, min_par(n) - c.par)
                    if cst < cost:
                        best, cost = ci, cst
                if best is None:
                    raise RuntimeError(f"roll: no candidate for level {n}")
                used.add(best)
                c = cands[best]
                seen.add(json.dumps(c.level.to_json()["grid"]) + str(c.level.cube) + str(c.level.faces))
                lv = c.level
                lv.id, lv.tier, lv.par, lv.moves = f"R{n:04d}", tier, c.par, c.par + slack
                lv.meta = {"beat": beat_for(n)[1], "bot_fail": c.fail_by_slack[slack], "solutions": c.solutions,
                           "dead_end": round(c.dead_end, 3), "solution": "".join("ESWN"[d] for d in solve(lv).path)}
                out.append(lv)
            print(f"roll levels {a}-{b - 1} done", flush=True)
    return out


def _run(lv: RollLevel, dirs: list[int]) -> dict:
    g = RollGame(lv)
    steps = []
    for d in dirs:
        if g.status != "playing":
            break
        trail: list = []
        ok = g.roll(d, trail)
        steps.append({"dir": d, "ok": ok, "pos": list(g.pos), "faces": "".join(map(str, g.faces)),
                      "prints": [g.prints.get(i, 0) for i in lv.targets], "trail": [i for i, _ in trail],
                      "movesLeft": g.moves_left, "status": g.status})
    return {"level": lv.to_json(), "dirs": dirs, "initialStatus": RollGame(lv).status, "steps": steps}


def golden(n_random: int = 300, seed: int = 7) -> dict:
    rng = random.Random(seed)
    hand = [
        ([".1..", "....", "...."], (0, 0), "000010", [0]),
        (["ab..", "....", "...."], (0, 1), "000000", [3, 0, 1, 2]),
        ([".~~1.", ".....", "....."], (0, 0), "000000", [0, 1, 2]),
        ([".*1.", "....", "...."], (0, 0), "000011", [0, 0, 2, 2]),
        ([".#1", "...", "..."], (0, 0), "010000", [0, 3, 2, 1, 0, 0]),
    ]
    cases = []
    for rows, cube, faces, dirs in hand:
        lv = RollLevel.from_rows(rows, cube=cube, faces=faces, moves=6)
        lv.tiles  # noqa: B018
        cases.append(_run(lv, dirs))
    specs = [b[2] for b in BEATS]
    while len(cases) < n_random + len(hand):
        lv = build(rng.choice(specs), rng)
        if lv is None:
            continue
        lv.moves = rng.randint(3, 10)
        cases.append(_run(lv, [rng.randrange(4) for _ in range(rng.randint(1, 12))]))
    solver = []
    for c in cases[5:65]:
        lv = RollLevel.from_json(c["level"])
        s = solve(lv, max_depth=14)
        solver.append({"level": c["level"], "par": s.par, "solutions": s.solutions})
    return {"schema": 1, "cases": cases, "solver": solver}


def main() -> None:
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "roll")
    os.makedirs(root, exist_ok=True)
    if "golden" in sys.argv:
        g = golden()
        body = ",\n".join(json.dumps(c, separators=(",", ":")) for c in g["cases"])
        sol = ",\n".join(json.dumps(c, separators=(",", ":")) for c in g["solver"])
        with open(os.path.join(root, "golden.json"), "w") as f:
            f.write('{"schema":1,"cases":[\n' + body + '\n],"solver":[\n' + sol + "\n]}\n")
        print("wrote golden.json", len(g["cases"]))
    if "levels" in sys.argv:
        levels = curate()
        with open(os.path.join(root, "levels.json"), "w") as f:
            json.dump({"schema": 1, "levels": [lv.to_json() for lv in levels]}, f, indent=1)
        print("wrote levels.json", len(levels))


if __name__ == "__main__":
    main()
