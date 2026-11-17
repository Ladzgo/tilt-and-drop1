"""Domino Run content: beats, ramped curation, golden vectors, export."""
from __future__ import annotations

import json
import multiprocessing as mp
import os
import random
import sys
import zlib

from tiltdrop.pacing import target_fail, tier_for_level

from .generator import DSpec, build, generate_one
from .model import DominoLevel
from .sim import DominoGame
from .solver import solve

BEATS = [
    (1, "first_turn", DSpec("first_turn", sizes=((4, 4), (5, 4)), turns=(1, 1), run=(1, 2), decoy_turners=(0, 0), par=(1, 3))),
    (11, "corners", DSpec("corners", sizes=((5, 5),), turns=(2, 3), run=(1, 2), decoy_turners=(1, 2), par=(2, 5))),
    (41, "splitters", DSpec("splitters", sizes=((5, 5), (6, 6)), turns=(2, 3), splits=(1, 1), run=(1, 2), decoy_turners=(1, 2), par=(2, 6))),
    (81, "fixed_walls", DSpec("fixed_walls", sizes=((6, 6),), turns=(3, 4), splits=(0, 1), run=(1, 2), decoy_turners=(1, 2), fixed_p=0.3, walls=(0.04, 0.12), par=(3, 7))),
    (121, "long_chains", DSpec("long_chains", sizes=((6, 6), (7, 7)), turns=(3, 5), splits=(1, 2), run=(1, 3), decoy_turners=(1, 2), fixed_p=0.2, par=(3, 8))),
    (161, "review", DSpec("review", sizes=((7, 7),), turns=(4, 5), splits=(1, 2), run=(1, 3), decoy_turners=(1, 2), fixed_p=0.25, walls=(0.0, 0.1), par=(4, 9))),
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


def curate(last: int = 200, k: int = 10, workers: int = 4, bot_runs: int = 50) -> list[DominoLevel]:
    out: list[DominoLevel] = []
    seen: set[str] = set()
    starts = [b[0] for b in BEATS if b[0] <= last] + [last + 1]
    with mp.get_context("fork").Pool(workers) as pool:
        for a, b in zip(starts, starts[1:]):
            spec = beat_for(a)[2]
            tasks = [(spec, zlib.crc32(f"domino:{n}:{j}".encode()), bot_runs) for n in range(a, b) for j in range(k)]
            cands = [c for c in pool.imap(_task, tasks, chunksize=2) if c is not None]
            used: set[int] = set()
            for n in range(a, b):
                tier = tier_for_level(n)
                best, cost = None, 9e9
                for ci, c in enumerate(cands):
                    key = "|".join(c.level.rows())
                    if ci in used or key in seen:
                        continue
                    cst = (c.fail + 0.03 * c.par) if n == a else abs(c.fail - target_fail(n, tier)) - 0.005 * len(c.level.turners)
                    if cst < cost:
                        best, cost = ci, cst
                if best is None:
                    raise RuntimeError(f"domino: no candidate for level {n}")
                used.add(best)
                c = cands[best]
                seen.add("|".join(c.level.rows()))
                lv = c.level
                lv.id, lv.tier = f"D{n:04d}", tier
                s = solve(lv)
                lv.meta = {"beat": beat_for(n)[1], "bot_fail": c.fail, "solutions": c.solutions, "share": round(c.share, 4),
                           "solution": list(s.best)}
                out.append(lv)
            print(f"domino levels {a}-{b - 1} done", flush=True)
    return out


def _run(lv: DominoLevel, actions: list) -> dict:
    g = DominoGame(lv)
    steps = []
    for act in actions:
        if g.status != "playing":
            break
        if act == "P":
            falls, rung = g.push()
            steps.append({"act": "P", "falls": [[t, c, list(o)] for t, c, o in falls], "rung": sorted(rung), "attempts": g.attempts_left, "status": g.status})
        else:
            ok = g.rotate(act)
            steps.append({"act": act, "ok": ok, "config": list(g.config), "status": g.status})
    return {"level": lv.to_json(), "actions": actions, "steps": steps}


def golden(n_random: int = 300, seed: int = 3) -> dict:
    rng = random.Random(seed)
    hand = [
        (["S>d.d.B.", "........", "........"], ["P"]),
        (["S>t>..", "..B...", "......"], ["P", 1, "P"]),
        (["S>tv..", "..d...", "..B..."], ["P"]),
        (["..B...", "S>Y...", "..B..."], ["P"]),
        (["S>Y.d.", "..d.d.", "..d.B."], ["P", "P", "P"]),
    ]
    cases = [_run(DominoLevel.from_rows(r), a) for r, a in hand]
    specs = [b[2] for b in BEATS]
    while len(cases) < n_random + len(hand):
        lv = build(rng.choice(specs), rng)
        if lv is None:
            continue
        acts: list = []
        for _ in range(rng.randint(2, 10)):
            acts.append("P" if rng.random() < 0.3 else rng.choice(lv.turners + [0]))
        cases.append(_run(lv, acts))
    solver = []
    for c in cases[5:65]:
        s = solve(DominoLevel.from_json(c["level"]))
        solver.append({"level": c["level"], "par": s.par, "solutions": s.solutions})
    return {"schema": 1, "cases": cases, "solver": solver}


def main() -> None:
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "domino")
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
