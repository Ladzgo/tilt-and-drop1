"""Fold content: beats, ramped curation, golden vectors, export."""
from __future__ import annotations

import json
import multiprocessing as mp
import os
import random
import sys
import zlib

from tiltdrop.pacing import TIER_SLACK, target_fail, tier_for_level

from .generator import FSpec, build, generate_one
from .model import FoldLevel
from .sim import FoldGame, legal_folds, visible
from .solver import solve

BEATS = [
    (1, "first_folds", FSpec("first_folds", sizes=((3, 1), (4, 1), (2, 2)), colors=(2, 3), two_sided=0.0, axes="V", folds=(1, 2), par=(1, 2))),
    (11, "two_sided", FSpec("two_sided", sizes=((4, 1), (4, 2), (3, 2)), colors=(2, 3), two_sided=0.7, axes="V", folds=(2, 3), par=(2, 3))),
    (41, "both_axes", FSpec("both_axes", sizes=((3, 3), (4, 2), (4, 3)), colors=(3, 4), two_sided=0.7, axes="VH", folds=(2, 3), par=(2, 4))),
    (81, "long_sequences", FSpec("long_sequences", sizes=((4, 3), (4, 4)), colors=(3, 4), two_sided=0.8, axes="VH", folds=(3, 5), par=(3, 5))),
    (121, "big_sheets", FSpec("big_sheets", sizes=((5, 4), (5, 5), (6, 4)), colors=(3, 5), two_sided=0.8, axes="VH", folds=(3, 5), par=(3, 6))),
    (161, "review", FSpec("review", sizes=((4, 4), (5, 4), (6, 4), (5, 5)), colors=(4, 5), two_sided=0.9, axes="VH", folds=(4, 6), par=(4, 6))),
]


def beat_for(n: int):
    cur = BEATS[0]
    for b in BEATS:
        if b[0] <= n:
            cur = b
    return cur


def min_par(n: int) -> int:
    return 1 if n <= 10 else 2 + n // 60


def _task(args):
    spec, seed, runs = args
    return generate_one(spec, seed, bot_runs=runs)


def curate(last: int = 200, k: int = 10, workers: int = 4, bot_runs: int = 50) -> list[FoldLevel]:
    out: list[FoldLevel] = []
    seen: set[str] = set()
    starts = [b[0] for b in BEATS if b[0] <= last] + [last + 1]
    with mp.get_context("fork").Pool(workers) as pool:
        for a, b in zip(starts, starts[1:]):
            spec = beat_for(a)[2]
            tasks = [(spec, zlib.crc32(f"fold:{n}:{j}".encode()), bot_runs) for n in range(a, b) for j in range(k)]
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
                    cst = (f + 0.05 * c.par) if n == a else abs(f - target_fail(n, tier)) + 0.12 * max(0, min_par(n) - c.par)
                    if cst < cost:
                        best, cost = ci, cst
                if best is None:
                    raise RuntimeError(f"fold: no candidate for level {n}")
                used.add(best)
                c = cands[best]
                seen.add(json.dumps(c.level.to_json(), sort_keys=True))
                lv = c.level
                lv.id, lv.tier, lv.par, lv.moves = f"F{n:04d}", tier, c.par, c.par + slack
                lv.meta = {"beat": beat_for(n)[1], "bot_fail": c.fail_by_slack[slack], "solutions": c.solutions, "solution": solve(lv).path}
                out.append(lv)
            print(f"fold levels {a}-{b - 1} done", flush=True)
    return out


def _run(lv: FoldLevel, moves: list) -> dict:
    g = FoldGame(lv)
    steps = []
    for mv in moves:
        if g.status != "playing":
            break
        ok = g.fold(*mv)
        steps.append({"move": list(mv), "ok": ok, "visible": visible(lv, g.sheet),
                      "stacks": [[[list(e) for e in st] for st in row] for row in g.sheet], "movesLeft": g.moves_left, "status": g.status})
    return {"level": lv.to_json(), "moves": [list(m) for m in moves], "steps": steps}


def golden(n_random: int = 300, seed: int = 9) -> dict:
    rng = random.Random(seed)
    cases = []
    specs = [b[2] for b in BEATS]
    while len(cases) < n_random:
        lv = build(rng.choice(specs), rng)
        if lv is None:
            continue
        lv.moves = rng.randint(2, 7)
        from .sim import initial_sheet, fold
        sheet, moves = initial_sheet(lv), []
        for _ in range(rng.randint(1, 6)):
            opts = legal_folds(sheet)
            mv = rng.choice(opts + [("V", 9, 0)]) if opts else ("V", 1, 0)
            moves.append(mv)
            r = fold(sheet, *mv)
            if r is not None:
                sheet = r
        cases.append(_run(lv, moves))
    solver = []
    for c in cases[:60]:
        s = solve(FoldLevel.from_json(c["level"]))
        solver.append({"level": c["level"], "par": s.par, "solutions": s.solutions})
    return {"schema": 1, "cases": cases, "solver": solver}


def main() -> None:
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "fold")
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
