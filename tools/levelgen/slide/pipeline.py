"""Slide Out content: beats, ramped curation, golden vectors, export."""
from __future__ import annotations

import json
import multiprocessing as mp
import os
import random
import sys
import zlib

from tiltdrop.pacing import target_fail, tier_for_level

from .generator import SSpec, build, generate_one
from .model import SlideLevel
from .sim import SlideGame
from .solver import solve

BEATS = [
    (1, "first_taps", SSpec("first_taps", sizes=((4, 4),), colors=(2, 2), per_color=(3,), capacity=5, lane_bias=0.5, min_peak=2)),
    (11, "colours", SSpec("colours", sizes=((5, 5),), colors=(3, 4), per_color=(3,), capacity=5, lane_bias=0.7, min_peak=3)),
    (41, "walls", SSpec("walls", sizes=((5, 5), (5, 6)), colors=(3, 4), per_color=(3,), capacity=5, walls=(0.06, 0.16), lane_bias=0.75, min_peak=3)),
    (81, "crowds", SSpec("crowds", sizes=((6, 6),), colors=(4, 5), per_color=(3, 3, 6), capacity=5, walls=(0.0, 0.1), lane_bias=0.8, min_peak=4, min_dead=0.05)),
    (121, "tight_tray", SSpec("tight_tray", sizes=((5, 6), (6, 6)), colors=(4, 5), per_color=(3,), capacity=4, walls=(0.0, 0.12), lane_bias=0.8, min_peak=3, min_dead=0.05)),
    (161, "review", SSpec("review", sizes=((6, 6), (6, 7)), colors=(4, 6), per_color=(3, 3, 6), capacity=5, walls=(0.04, 0.14), lane_bias=0.85, min_peak=4, min_dead=0.1)),
]


def beat_for(n: int):
    cur = BEATS[0]
    for b in BEATS:
        if b[0] <= n:
            cur = b
    return cur


def _task(args):
    spec, seed, runs = args
    return generate_one(spec, seed, bot_runs=runs)


def curate(last: int = 200, k: int = 10, workers: int = 4, bot_runs: int = 50) -> list[SlideLevel]:
    out: list[SlideLevel] = []
    seen: set[str] = set()
    starts = [b[0] for b in BEATS if b[0] <= last] + [last + 1]
    with mp.get_context("fork").Pool(workers) as pool:
        for a, b in zip(starts, starts[1:]):
            spec = beat_for(a)[2]
            tasks = [(spec, zlib.crc32(f"slide:{n}:{j}".encode()), bot_runs) for n in range(a, b) for j in range(k)]
            cands = [c for c in pool.imap(_task, tasks, chunksize=2) if c is not None]
            used: set[int] = set()
            for n in range(a, b):
                tier = tier_for_level(n)
                best, cost = None, 9e9
                for ci, c in enumerate(cands):
                    key = "|".join(c.level.rows())
                    if ci in used or key in seen:
                        continue
                    cst = (c.fail + 0.02 * c.blocks) if n == a else abs(c.fail - target_fail(n, tier)) - 0.01 * c.blocks
                    if cst < cost:
                        best, cost = ci, cst
                if best is None:
                    raise RuntimeError(f"slide: no candidate for level {n}")
                used.add(best)
                c = cands[best]
                seen.add("|".join(c.level.rows()))
                lv = c.level
                lv.id, lv.tier = f"S{n:04d}", tier
                lv.meta = {"beat": beat_for(n)[1], "bot_fail": c.fail, "dead_end": round(c.dead_end, 3), "blocks": c.blocks,
                           "solution": solve(lv).order}
                out.append(lv)
            print(f"slide levels {a}-{b - 1} done", flush=True)
    return out


def _run(lv: SlideLevel, taps: list[int]) -> dict:
    g = SlideGame(lv)
    steps = []
    for cell in taps:
        if g.status != "playing":
            break
        res = g.tap_cell(cell)
        steps.append({"cell": cell, "result": res, "removed": g.removed, "tray": list(g.tray), "status": g.status})
    return {"level": lv.to_json(), "taps": taps, "steps": steps}


def golden(n_random: int = 300, seed: int = 11) -> dict:
    rng = random.Random(seed)
    hand = [
        (["1>..1v", "......", "......"], 5, [0, 2, 0, 4]),
        (["1^1^1^", "......", "......"], 5, [0, 1, 2]),
        (["1^2^3^", "1^2^3^", "1^2^3^"], 3, [0, 1, 2]),
        (["1^1^2^", "......", "1^...."], 3, [0, 1, 6, 2]),
        (["1^2^2^", "1v....", "2>..1^"], 4, [3, 6, 2, 8, 6, 1, 0, 3]),
    ]
    cases = [_run(SlideLevel.from_rows(r, c), t) for r, c, t in hand]
    specs = [b[2] for b in BEATS]
    while len(cases) < n_random + len(hand):
        lv = build(rng.choice(specs), rng)
        if lv is None:
            continue
        taps = [rng.randrange(lv.w * lv.h) for _ in range(rng.randint(3, 20))]
        cases.append(_run(lv, taps))
    solver = []
    for c in cases[5:65]:
        s = solve(SlideLevel.from_json(c["level"]))
        solver.append({"level": c["level"], "solvable": s.solvable, "states": s.states, "order": s.order})
    return {"schema": 1, "cases": cases, "solver": solver}


def main() -> None:
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "slide")
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
