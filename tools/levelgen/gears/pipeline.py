"""Gear Works content: beats, ramped curation, golden vectors, export."""
from __future__ import annotations

import json
import multiprocessing as mp
import os
import random
import sys
import zlib

from tiltdrop.pacing import TIER_SLACK, target_fail, tier_for_level

from .generator import GSpec, build, generate_one
from .model import GearLevel
from .sim import GearGame, spins
from .solver import solve, tap_order

BEATS = [
    (1, "first_chain", GSpec("first_chain", sizes=((4, 4), (5, 4)), outputs=(1, 1), decoys=(1, 2), scramble_remove=(1, 2), scramble_add=(0, 0), extra_inventory=(1, 1), par=(1, 2))),
    (11, "directions", GSpec("directions", sizes=((5, 5),), outputs=(2, 2), decoys=(2, 4), scramble_remove=(1, 3), scramble_add=(0, 1), par=(2, 5))),
    (41, "jams", GSpec("jams", sizes=((5, 5), (6, 5)), outputs=(2, 2), decoys=(3, 5), scramble_remove=(2, 3), scramble_add=(1, 2), par=(3, 6))),
    (81, "fixed_tight", GSpec("fixed_tight", sizes=((6, 5), (6, 6)), outputs=(2, 3), fixed_p=0.3, decoys=(3, 5), scramble_remove=(2, 4), scramble_add=(1, 2), extra_inventory=(0, 0), par=(3, 7))),
    (121, "two_motors", GSpec("two_motors", sizes=((6, 6),), motors=2, outputs=(2, 3), fixed_p=0.15, decoys=(3, 5), scramble_remove=(2, 4), scramble_add=(1, 2), extra_inventory=(0, 1), par=(3, 8))),
    (161, "review", GSpec("review", sizes=((6, 6), (7, 6)), motors=2, outputs=(3, 3), fixed_p=0.25, decoys=(4, 6), scramble_remove=(3, 4), scramble_add=(1, 2), extra_inventory=(0, 0), par=(4, 9))),
]


def beat_for(n: int):
    cur = BEATS[0]
    for b in BEATS:
        if b[0] <= n:
            cur = b
    return cur


def min_par(n: int) -> int:
    return 1 if n <= 10 else 1 + n // 40


def _task(args):
    spec, seed, runs = args
    return generate_one(spec, seed, bot_runs=runs)


def curate(last: int = 200, k: int = 10, workers: int = 4, bot_runs: int = 50) -> list[GearLevel]:
    out: list[GearLevel] = []
    seen: set[str] = set()
    starts = [b[0] for b in BEATS if b[0] <= last] + [last + 1]
    with mp.get_context("fork").Pool(workers) as pool:
        for a, b in zip(starts, starts[1:]):
            spec = beat_for(a)[2]
            tasks = [(spec, zlib.crc32(f"gears:{n}:{j}".encode()), bot_runs) for n in range(a, b) for j in range(k)]
            cands = [c for c in pool.imap(_task, tasks, chunksize=2) if c is not None]
            used: set[int] = set()
            for n in range(a, b):
                tier = tier_for_level(n)
                slack = TIER_SLACK[tier]
                best, cost = None, 9e9
                for ci, c in enumerate(cands):
                    key = "|".join(c.level.rows())
                    if ci in used or key in seen:
                        continue
                    f = c.fail_by_slack[slack]
                    cst = (f + 0.05 * c.par) if n == a else abs(f - target_fail(n, tier)) + 0.12 * max(0, min_par(n) - c.par)
                    if cst < cost:
                        best, cost = ci, cst
                if best is None:
                    raise RuntimeError(f"gears: no candidate for level {n}")
                used.add(best)
                c = cands[best]
                seen.add("|".join(c.level.rows()))
                lv = c.level
                lv.id, lv.tier, lv.par, lv.moves = f"G{n:04d}", tier, c.par, c.par + slack
                s = solve(lv)
                target = min(s.layouts, key=lambda l: (bin(l ^ lv.start_layout()).count("1"), l))
                lv.meta = {"beat": beat_for(n)[1], "bot_fail": c.fail_by_slack[slack], "solutions": c.solutions,
                           "solution": tap_order(lv, lv.start_layout(), target)}
                out.append(lv)
            print(f"gears levels {a}-{b - 1} done", flush=True)
    return out


def _run(lv: GearLevel, taps: list[int]) -> dict:
    g = GearGame(lv)
    steps = []
    for cell in taps:
        if g.status != "playing":
            break
        ok = g.tap(cell)
        spin, jam = spins(lv, g.layout)
        steps.append({"cell": cell, "ok": ok, "layout": g.layout, "spin": spin, "jam": [int(j) for j in jam],
                      "movesLeft": g.moves_left, "status": g.status})
    return {"level": lv.to_json(), "taps": taps, "initialStatus": GearGame(lv).status, "steps": steps}


def golden(n_random: int = 300, seed: int = 5) -> dict:
    rng = random.Random(seed)
    hand = [
        (["MGGR", "....", "...."], 0, [0]),
        (["MG..", "G...", "...."], 0, [0]),
        (["MoR.", "...o", "...."], 2, [7, 7, 1]),
        (["MooR", "oooo", "...."], 3, [4, 5, 6, 1, 2]),
        (["MM..", "o...", "...."], 1, [4]),
    ]
    cases = []
    for rows, inv, taps in hand:
        lv = GearLevel.from_rows(rows, inventory=inv, moves=6)
        cases.append(_run(lv, taps))
    specs = [b[2] for b in BEATS]
    while len(cases) < n_random + len(hand):
        lv = build(rng.choice(specs), rng)
        if lv is None:
            continue
        lv.moves = rng.randint(2, 9)
        taps = [rng.randrange(lv.w * lv.h) for _ in range(rng.randint(2, 12))]
        cases.append(_run(lv, taps))
    solver = []
    for c in cases[5:65]:
        s = solve(GearLevel.from_json(c["level"]))
        solver.append({"level": c["level"], "par": s.par, "solutions": s.solutions})
    return {"schema": 1, "cases": cases, "solver": solver}


def main() -> None:
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "gears")
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
