"""Flow Fill content: beats, ramped curation, golden vectors, export."""
from __future__ import annotations

import json
import multiprocessing as mp
import os
import random
import sys
import zlib

from tiltdrop.pacing import TIER_SLACK, target_fail, tier_for_level

from .generator import WSpec, build, generate_one
from .model import FlowLevel
from .sim import FlowGame
from .solver import solve, taps_for

BEATS = [
    (1, "first_flow", WSpec("first_flow", sizes=((4, 4),), colours=(1, 1), fill=(0.1, 0.3), par=(1, 4))),
    (11, "two_colours", WSpec("two_colours", sizes=((5, 5),), colours=(2, 2), fill=(0.2, 0.5), par=(3, 7))),
    (41, "tees", WSpec("tees", sizes=((5, 5), (6, 5)), colours=(1, 2), tee_p=0.9, fill=(0.2, 0.5), par=(4, 9))),
    (81, "fixed_cross", WSpec("fixed_cross", sizes=((6, 6),), colours=(2, 2), tee_p=0.4, fixed_p=0.25, cross_p=0.3, fill=(0.3, 0.6), par=(5, 11))),
    (121, "three_colours", WSpec("three_colours", sizes=((6, 6),), colours=(3, 3), tee_p=0.2, fixed_p=0.15, fill=(0.3, 0.6), par=(6, 13))),
    (161, "review", WSpec("review", sizes=((6, 6), (7, 6)), colours=(2, 3), tee_p=0.5, fixed_p=0.2, cross_p=0.2, fill=(0.4, 0.7), par=(7, 14))),
]


def beat_for(n: int):
    cur = BEATS[0]
    for b in BEATS:
        if b[0] <= n:
            cur = b
    return cur


def min_par(n: int) -> int:
    return 1 if n <= 10 else 2 + n // 30


def _task(args):
    spec, seed, runs = args
    return generate_one(spec, seed, bot_runs=runs)


def curate(last: int = 200, k: int = 10, workers: int = 4, bot_runs: int = 50) -> list[FlowLevel]:
    out: list[FlowLevel] = []
    seen: set[str] = set()
    starts = [b[0] for b in BEATS if b[0] <= last] + [last + 1]
    with mp.get_context("fork").Pool(workers) as pool:
        for a, b in zip(starts, starts[1:]):
            spec = beat_for(a)[2]
            tasks = [(spec, zlib.crc32(f"flow:{n}:{j}".encode()), bot_runs) for n in range(a, b) for j in range(k)]
            cands = [c for c in pool.imap(_task, tasks, chunksize=2) if c is not None]
            used: set[int] = set()
            for n in range(a, b):
                tier = tier_for_level(n)
                slack = TIER_SLACK[tier]
                best, cost = None, 9e9
                for ci, c in enumerate(cands):
                    key = json.dumps(c.level.to_json(), sort_keys=True)
                    if ci in used or key in seen:
                        continue
                    f = c.fail_by_slack[slack]
                    cst = (f + 0.05 * c.par) if n == a else abs(f - target_fail(n, tier)) + 0.08 * max(0, min_par(n) - c.par)
                    if cst < cost:
                        best, cost = ci, cst
                if best is None:
                    raise RuntimeError(f"flow: no candidate for level {n}")
                used.add(best)
                c = cands[best]
                seen.add(json.dumps(c.level.to_json(), sort_keys=True))
                lv = c.level
                lv.id, lv.tier, lv.par, lv.moves = f"W{n:04d}", tier, c.par, c.par + slack
                s = solve(lv)
                start = {i: lv.pipes[i][1] for i in lv.rotatables}
                lv.meta = {"beat": beat_for(n)[1], "bot_fail": c.fail_by_slack[slack], "solutions": c.solutions, "solution": taps_for(lv, start, s.rot)}
                out.append(lv)
            print(f"flow levels {a}-{b - 1} done", flush=True)
    return out


def _run(lv: FlowLevel, taps: list[int]) -> dict:
    g = FlowGame(lv)
    steps = []
    for cell in taps:
        if g.status != "playing":
            break
        ok = g.tap(cell)
        st = g.state
        steps.append({"cell": cell, "ok": ok, "colour": st["colour"], "leaks": [list(l) for l in st["leaks"]], "dist": st["dist"],
                      "cups": [int(c) for c in st["cups"]], "movesLeft": g.moves_left, "status": g.status})
    return {"level": lv.to_json(), "taps": taps, "initialStatus": FlowGame(lv).status, "steps": steps}


def golden(n_random: int = 300, seed: int = 13) -> dict:
    rng = random.Random(seed)
    cases = []
    specs = [b[2] for b in BEATS]
    while len(cases) < n_random:
        lv = build(rng.choice(specs), rng)
        if lv is None:
            continue
        lv.moves = rng.randint(2, 10)
        taps = [rng.choice(lv.rotatables + [0]) for _ in range(rng.randint(1, 10))] if lv.rotatables else [0]
        cases.append(_run(lv, taps))
    solver = []
    for c in cases[:60]:
        s = solve(FlowLevel.from_json(c["level"]))
        solver.append({"level": c["level"], "par": s.par, "solutions": s.solutions})
    return {"schema": 1, "cases": cases, "solver": solver}


def main() -> None:
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "flow")
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
