"""Prism content: beats, pacing (shared ramp with Tilt & Drop), curation, golden vectors, export."""
from __future__ import annotations

import json
import multiprocessing as mp
import os
import random
import zlib

from tiltdrop.pacing import TIER_SLACK, target_fail, tier_for_level

from .generator import PSpec, generate_one
from .model import PrismLevel
from .sim import PrismGame, trace
from .solver import solve

BEATS = [
    (1, "tutorial", PSpec("tutorial", sizes=((4, 4), (5, 5)), sources=(1, 1), mirrors=(1, 3), decoys=(0, 1), scramble=(1, 1), par=(1, 2), max_solutions=2)),
    (11, "two_colors", PSpec("two_colors", sizes=((5, 5), (5, 6), (6, 6)), sources=(2, 2), mirrors=(3, 6), decoys=(2, 4), scramble=(2, 4), par=(2, 5))),
    (41, "mixing", PSpec("mixing", sizes=((5, 6), (6, 6)), sources=(2, 3), mirrors=(4, 7), decoys=(2, 4), mix_prob=0.9, require_mix=True, scramble=(3, 5), par=(3, 6))),
    (81, "fixed", PSpec("fixed", sizes=((6, 6), (6, 7)), sources=(2, 3), mirrors=(4, 8), fixed=(1, 3), decoys=(2, 4), walls=(0.04, 0.12), mix_prob=0.6, scramble=(3, 6), par=(3, 7))),
    (121, "filters", PSpec("filters", sizes=((6, 6), (6, 7), (7, 7)), sources=(2, 3), mirrors=(5, 8), fixed=(0, 2), filters=(1, 2), decoys=(2, 4), mix_prob=0.6, scramble=(4, 7), par=(4, 8))),
    (161, "splitters", PSpec("splitters", sizes=((6, 7), (7, 7)), sources=(2, 3), mirrors=(5, 9), splitters=(1, 2), fixed=(0, 2), decoys=(2, 4), mix_prob=0.6, scramble=(4, 7), par=(4, 9))),
]
INTROS = {11: ("Two colours", "Each target wants one exact colour."), 41: ("Mixing", "Light adds up: red + blue = magenta, red + green = yellow, green + blue = cyan."),
          81: ("Fixed mirrors", "Mirrors with a frame can't be turned."), 121: ("Filters", "A filter only lets its own colour through."),
          161: ("Splitters", "A splitter sends light both ways at once.")}


def beat_for(n: int):
    cur = BEATS[0]
    for b in BEATS:
        if b[0] <= n:
            cur = b
    return cur


def min_par(n: int) -> int:
    return 1 if n <= 10 else 1 + n // 45


def _task(args):
    spec, seed, runs = args
    return generate_one(spec, seed, bot_runs=runs)


def curate(last: int = 200, k: int = 10, workers: int = 4, bot_runs: int = 50) -> list[PrismLevel]:
    out: list[PrismLevel] = []
    seen: set[str] = set()
    starts = [b[0] for b in BEATS if b[0] <= last] + [last + 1]
    ctx = mp.get_context("fork")
    with ctx.Pool(workers) as pool:
        for a, b in zip(starts, starts[1:]):
            spec = beat_for(a)[2]
            tasks = [(spec, zlib.crc32(f"prism:{n}:{j}".encode()), bot_runs) for n in range(a, b) for j in range(k)]
            cands = [c for c in pool.imap(_task, tasks, chunksize=2) if c is not None]
            used: set[int] = set()
            for pos, n in enumerate(range(a, b)):
                tier = tier_for_level(n)
                slack = TIER_SLACK[tier]
                intro = n == a
                target = target_fail(n, tier)
                best, cost = None, 9e9
                for ci, c in enumerate(cands):
                    key = "|".join(c.level.rows())
                    if ci in used or key in seen:
                        continue
                    f = c.fail_by_slack[slack]
                    cst = (f + 0.05 * c.par) if intro else abs(f - target) + 0.12 * max(0, min_par(n) - c.par)
                    if cst < cost:
                        best, cost = ci, cst
                if best is None:
                    raise RuntimeError(f"prism: no candidate for level {n}")
                used.add(best)
                c = cands[best]
                seen.add("|".join(c.level.rows()))
                lv = c.level
                lv.id, lv.tier, lv.par, lv.moves = f"P{n:04d}", tier, c.par, c.par + slack
                lv.meta = {"beat": beat_for(n)[1], "bot_fail": c.fail_by_slack[slack], "solutions": c.solutions,
                           "solution": solve(lv).taps}
                out.append(lv)
            print(f"prism levels {a}-{b - 1} done", flush=True)
    return out


# ---------------------------------------------------------------- golden vectors
def _run(lv: PrismLevel, taps: list[int]) -> dict:
    g = PrismGame(lv)
    steps = []
    for t in taps:
        if g.status != "playing":
            break
        spent = g.tap(t)
        segs: list = []
        trace(lv, g.states, segments=segs)
        steps.append({"tap": t, "spent": spent, "movesLeft": g.moves_left, "status": g.status,
                      "received": [g.received[i] for i in lv.targets],
                      "segments": [[c, round(x0, 2), round(y0, 2), round(x1, 2), round(y1, 2)] for c, x0, y0, x1, y1 in segs]})
        if g.status != "playing":
            break
    return {"level": lv.to_json(), "taps": taps, "initialStatus": PrismGame(lv).status, "steps": steps}


def golden(n_random: int = 300, seed: int = 99) -> dict:
    rng = random.Random(seed)
    cases = []
    hand = [
        ["..(R)", "[R>].\\", "./."],
        [".[Bv].", "[R>](M).", "..."],
        ["....", "[R>]<B>.(R)", "...."],
        ["..(R).", "[R>].[/](R)", "...."],
        ["{\\}..{/}", "[R>]...", "{/}..{\\}", "...(R)"],
        [".(R)...", ".....", "[R>]..\\.", "./.\\."],
    ]
    for rows in hand:
        lv = PrismLevel.from_rows(rows, moves=5)
        cases.append(_run(lv, lv.rotatables[:3] + [0]))
    specs = [b[2] for b in BEATS]
    while len(cases) < n_random + len(hand):
        from .generator import build
        lv = build(rng.choice(specs), rng)
        if lv is None:
            continue
        lv.moves = rng.randint(2, 8)
        taps = [rng.choice(lv.rotatables + [0, 1]) for _ in range(rng.randint(1, 8))]
        cases.append(_run(lv, taps))
    solver = []
    for c in cases[: 60]:
        lv = PrismLevel.from_json(c["level"])
        s = solve(lv)
        solver.append({"level": c["level"], "par": s.par, "solutions": s.solutions})
    return {"schema": 1, "cases": cases, "solver": solver}


def main() -> None:
    root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "shared", "prism")
    os.makedirs(root, exist_ok=True)
    import sys
    if "golden" in sys.argv:
        g = golden()
        body = ",\n".join(json.dumps(c, separators=(",", ":")) for c in g["cases"])
        sol = ",\n".join(json.dumps(c, separators=(",", ":")) for c in g["solver"])
        with open(os.path.join(root, "golden.json"), "w") as f:
            f.write('{"schema":1,"cases":[\n' + body + '\n],"solver":[\n' + sol + "\n]}\n")
        print("wrote golden.json", len(g["cases"]), "cases")
    if "levels" in sys.argv:
        levels = curate()
        with open(os.path.join(root, "levels.json"), "w") as f:
            json.dump({"schema": 1, "levels": [lv.to_json() for lv in levels]}, f, indent=1)
        print("wrote levels.json", len(levels))


if __name__ == "__main__":
    main()
