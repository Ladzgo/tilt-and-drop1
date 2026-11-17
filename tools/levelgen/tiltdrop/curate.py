"""Candidate generation and slot assignment (pacing-aware curation).

For each segment (one beat, at most one world):
  1. generate K candidates per slot in parallel (seeded -> reproducible)
  2. cluster them (k-means on features)
  3. walk the slots in order; each takes the unused, non-duplicate candidate
     whose bot fail rate at the tier's move limit is closest to the tier's
     target, avoiding the clusters of the previous two levels
  5. finalize: moves = par + slack(tier); re-analyze at the real limit
Manual picks: `overrides` {level: level-json} replace a slot (validated).
"""
from __future__ import annotations

import multiprocessing as mp
import zlib
from typing import Callable, Iterable

from .canon import Deduper
from .cluster import feature_vector, kmeans
from .difficulty import analyze
from .generator import Candidate, GenSpec, generate_one
from .handmade import HANDMADE
from .model import Level
from .pacing import MIX_SPECS, TIER_SLACK, beat_for_level, is_intro, spec_for_level, tier_for_level, world_of
from .sim import is_stable

# target first-attempt fail rate per tier (GDD §7.2), estimated by the bot at the
# tier's real move limit (par + slack)
TARGET_FAIL = {"E": 0.05, "N": 0.22, "H": 0.50, "SH": 0.70}
RAMP = 0.06  # target drift across a beat (later levels of a beat are a bit harder)


def seed_for(n: int, k: int, salt: str = "lvl") -> int:
    return zlib.crc32(f"{salt}:{n}:{k}".encode())


def _task(args: tuple[GenSpec, int, int]) -> Candidate | None:
    spec, seed, bot_runs = args
    return generate_one(spec, seed, bot_runs=bot_runs)


def generate_candidates(tasks: list[tuple[GenSpec, int, int]], workers: int) -> list[Candidate]:
    if workers <= 1:
        res: Iterable = map(_task, tasks)
        return [c for c in res if c is not None]
    ctx = mp.get_context("fork")
    with ctx.Pool(workers) as pool:
        return [c for c in pool.imap(_task, tasks, chunksize=2) if c is not None]


def segments(first: int, last: int) -> list[list[int]]:
    segs: list[list[int]] = []
    cur: list[int] = []
    for n in range(first, last + 1):
        if cur and (beat_for_level(n) != beat_for_level(cur[0]) or world_of(n) != world_of(cur[0])):
            segs.append(cur)
            cur = []
        cur.append(n)
    if cur:
        segs.append(cur)
    return segs


def finalize(lv: Level, n: int | str, tier: str, bot_runs: int, extra_meta: dict | None = None, prefix: str = "L") -> Level:
    slack = TIER_SLACK[tier]
    a = analyze(lv, slack=slack, bot_runs=bot_runs, seed=zlib.crc32(str(n).encode()), max_depth=30, node_cap=400_000)
    if a is None:
        raise ValueError(f"level {n} is not solvable")
    out = Level(lv.w, lv.h, lv.cells, dict(lv.gates))
    out.id = f"{prefix}{n:05d}" if isinstance(n, int) else str(n)
    out.tier = tier
    out.par = a.par
    out.moves = a.par + slack
    out.mechanics = out.detected_mechanics()
    out.meta = {**a.meta(), **(extra_meta or {})}
    if a.par == 0 or not is_stable(out):
        raise ValueError(f"level {n} invalid (par 0 or unstable)")
    return out


def _from_json(d: dict) -> Level:
    lv = Level.from_json(d)
    if not is_stable(lv):
        raise ValueError(f"manual level {d.get('id')} is not stable under gravity Down")
    return lv


def curate_range(
    first: int,
    last: int,
    k_per_slot: int,
    workers: int = 4,
    bot_runs: int = 60,
    final_bot_runs: int = 200,
    deduper: Deduper | None = None,
    overrides: dict[int, dict] | None = None,
    on_segment: Callable[[list[Level]], None] | None = None,
) -> list[Level]:
    deduper = deduper or Deduper()
    overrides = overrides or {}
    out: list[Level] = []
    recent: list[int] = []  # cluster labels of the last levels
    for seg in segments(first, last):
        beat = beat_for_level(seg[0])
        need = [n for n in seg if n not in overrides and n not in HANDMADE]
        tasks = [(spec_for_level(n, k), seed_for(n, k), bot_runs) for n in need for k in range(k_per_slot)]
        cands = generate_candidates(tasks, workers)
        extra_round = 0
        while len(cands) < len(need) * 1.5 and extra_round < 3:  # top up if acceptance was low
            extra_round += 1
            more = [(spec_for_level(n, k), seed_for(n, k), bot_runs) for n in need for k in range(k_per_slot * extra_round + 100, k_per_slot * extra_round + 100 + k_per_slot)]
            cands += generate_candidates(more, workers)
        cands.sort(key=lambda c: (c.difficulty, c.seed))
        m = len(cands)
        labels_list = kmeans([feature_vector(c.level, c.analysis.par, c.analysis.solutions) for c in cands], k=max(1, min(8, m // 4)), seed=seg[0])
        label = {id(c): l for c, l in zip(cands, labels_list)}
        used: set[int] = set()
        seg_out: list[Level] = []
        for pos, n in enumerate(seg):
            tier = tier_for_level(n)
            meta = {"beat": beat.name, "world": world_of(n)}
            if n in overrides or n in HANDMADE:
                src = overrides.get(n) or HANDMADE[n]
                lv = _from_json(src)
                if "hint" in src:
                    meta["hint"] = src["hint"]
                deduper.add(lv)
                fin = finalize(lv, n, tier, final_bot_runs, {**meta, "manual": True})
                seg_out.append(fin)
                recent = (recent + [-1])[-2:]
                continue
            slack = TIER_SLACK[tier]
            intro = is_intro(n)
            target = TARGET_FAIL[tier] + (0.0 if tier in ("E", "SH") else RAMP * pos / max(1, len(seg) - 1))
            best, best_cost = None, 9e9
            for c in cands:
                if id(c) in used:
                    continue
                fail = c.analysis.fail_by_slack[slack]
                if intro:  # tutorial: easiest and shortest
                    cost = fail + 0.05 * c.analysis.par
                else:
                    cost = abs(fail - target) + (0.1 if label[id(c)] in recent else 0.0) + 0.005 * c.analysis.par
                if cost < best_cost:
                    if deduper.is_duplicate(c.level):
                        used.add(id(c))
                        continue
                    best, best_cost = c, cost
            if best is None:
                raise RuntimeError(f"no candidate left for level {n} (beat {beat.name}); raise k_per_slot")
            used.add(id(best))
            deduper.add(best.level)
            recent = (recent + [label[id(best)]])[-2:]
            meta.update({"spec": best.spec, "seed": best.seed, "cluster": label[id(best)]})
            if beat.name == "pool":
                meta["provisional"] = True
            seg_out.append(finalize(best.level, n, tier, final_bot_runs, meta))
        out += seg_out
        if on_segment:
            on_segment(seg_out)
    return out


DAILY_SPECS = tuple(MIX_SPECS) + (beat_for_level(31).spec, beat_for_level(11).spec)


def curate_daily(count: int, workers: int = 4, deduper: Deduper | None = None, bot_runs: int = 60, final_bot_runs: int = 150) -> list[Level]:
    """Daily puzzles: medium difficulty (quantile 0.4..0.75), slack of tier N."""
    deduper = deduper or Deduper()
    tasks = [(DAILY_SPECS[seed_for(d, 0, "dspec") % len(DAILY_SPECS)], seed_for(d, k, "daily"), bot_runs) for d in range(1, count + 1) for k in range(6)]
    cands = generate_candidates(tasks, workers)
    band = [c for c in cands if 0.12 <= c.analysis.fail_by_slack[2] <= 0.5] or cands
    band.sort(key=lambda c: c.seed)  # shuffle difficulty order deterministically
    out: list[Level] = []
    for c in band:
        if len(out) >= count:
            break
        if deduper.add(c.level):
            out.append(finalize(c.level, f"D{len(out) + 1:04d}", "N", final_bot_runs, {"spec": c.spec, "seed": c.seed}))
    if len(out) < count:
        raise RuntimeError(f"only {len(out)} daily levels; generate more candidates")
    return out
