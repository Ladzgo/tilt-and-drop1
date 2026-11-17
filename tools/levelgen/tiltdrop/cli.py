"""Command line: python -m tiltdrop <command> [options]"""
from __future__ import annotations

import argparse
import glob
import hashlib
import json
import os
import sys
import time

from .canon import Deduper
from .curate import curate_daily, curate_range, segments
from .golden import build_all, dumps
from .model import Level
from .pack import read_pack, write_pack_gz
from .pacing import schedule
from .review import review_page
from .sim import is_stable
from .solver import solve

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SHARED = os.path.join(REPO, "shared")
OUT = os.path.join(REPO, "tools", "levelgen", "out")
PACK_SIZE = 1000


def _load_json_levels(path: str) -> list[Level]:
    with open(path) as f:
        return [Level.from_json(d) for d in json.load(f)["levels"]]


def _save_json_levels(path: str, levels: list[Level], **extra) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        json.dump({"schema": 1, **extra, "levels": [lv.to_json() for lv in levels]}, f, indent=1)
        f.write("\n")


def cmd_golden(a) -> int:
    out = os.path.join(SHARED, "golden")
    os.makedirs(out, exist_ok=True)
    frozen = None
    sp = os.path.join(out, "solver.json")
    if os.path.exists(sp) and not a.refresh_solver_levels:
        with open(sp) as f:
            frozen = json.load(f)["solver"]
    files = build_all(a.random, frozen)
    bad = 0
    for name, obj in files.items():
        path = os.path.join(out, name)
        text = dumps(obj)
        if a.check:
            with open(path) as f:
                if f.read() != text:
                    print(f"DRIFT: {name} differs from simulator output")
                    bad += 1
        else:
            with open(path, "w") as f:
                f.write(text)
            print(f"wrote {path}")
    return 1 if bad else 0


def cmd_levels(a) -> int:
    segdir = os.path.join(OUT, "segments")
    os.makedirs(segdir, exist_ok=True)
    overrides = {}
    ov_path = os.path.join(SHARED, "levels", "overrides.json")
    if os.path.exists(ov_path):
        with open(ov_path) as f:
            overrides = {int(k): v for k, v in json.load(f).items() if not k.startswith("_")}
    dedup = Deduper()
    for p in sorted(glob.glob(os.path.join(segdir, "seg_*.json"))):
        for lv in _load_json_levels(p):
            dedup.add(lv)
    t0 = time.time()
    for seg in segments(a.first, a.last):
        path = os.path.join(segdir, f"seg_{seg[0]:05d}_{seg[-1]:05d}.json")
        if os.path.exists(path):
            continue
        k = a.k if seg[0] <= a.curated else a.k_pool
        lv = curate_range(seg[0], seg[-1], k, workers=a.workers, bot_runs=a.bot_runs, final_bot_runs=a.final_bot_runs, deduper=dedup, overrides=overrides)
        _save_json_levels(path, lv)
        print(f"[{time.time() - t0:7.0f}s] levels {seg[0]}-{seg[-1]} done", flush=True)
    return 0


def cmd_daily(a) -> int:
    dedup = Deduper()
    for p in sorted(glob.glob(os.path.join(OUT, "segments", "seg_*.json"))):
        for lv in _load_json_levels(p):
            dedup.add(lv)
    levels = curate_daily(a.count, workers=a.workers, deduper=dedup)
    _save_json_levels(os.path.join(OUT, "daily.json"), levels)
    print(f"{len(levels)} daily levels")
    return 0


def _all_segment_levels() -> list[Level]:
    levels: list[Level] = []
    for p in sorted(glob.glob(os.path.join(OUT, "segments", "seg_*.json"))):
        levels += _load_json_levels(p)
    levels.sort(key=lambda lv: lv.id)
    for i, lv in enumerate(levels, 1):
        if lv.id != f"L{i:05d}":
            raise SystemExit(f"segments are not contiguous: expected L{i:05d}, found {lv.id}")
    return levels


def cmd_pack(a) -> int:
    out = os.path.join(SHARED, "levels")
    os.makedirs(out, exist_ok=True)
    levels = _all_segment_levels()
    _save_json_levels(os.path.join(out, "curated.json"), levels[: a.curated], note="Levels 1..N with full metadata (human review / overrides)")
    manifest = {"schema": 1, "contentVersion": a.content_version, "levelCount": len(levels), "packs": []}
    for i in range(0, len(levels), PACK_SIZE):
        chunk = levels[i : i + PACK_SIZE]
        name = f"main_{i // PACK_SIZE:03d}.tdp.gz"
        gz = write_pack_gz(chunk, os.path.join(out, name))
        manifest["packs"].append({"file": name, "first": i + 1, "count": len(chunk), "sha1": hashlib.sha1(gz).hexdigest()})
    daily_path = os.path.join(OUT, "daily.json")
    if os.path.exists(daily_path):
        daily = _load_json_levels(daily_path)
        gz = write_pack_gz(daily, os.path.join(out, "daily.tdp.gz"))
        manifest["daily"] = {"file": "daily.tdp.gz", "count": len(daily), "sha1": hashlib.sha1(gz).hexdigest(), "epoch": a.daily_epoch}
    with open(os.path.join(out, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=1)
        f.write("\n")
    with open(os.path.join(OUT, "review.html"), "w") as f:
        f.write(review_page(levels[: a.curated]))
    print(f"packed {len(levels)} levels into {len(manifest['packs'])} packs; review: {os.path.join(OUT, 'review.html')}")
    return 0


def cmd_validate(a) -> int:
    d = os.path.join(SHARED, "levels")
    with open(os.path.join(d, "manifest.json")) as f:
        man = json.load(f)
    bad = 0
    n = 0
    for p in man["packs"] + ([man["daily"]] if "daily" in man else []):
        with open(os.path.join(d, p["file"]), "rb") as f:
            data = f.read()
        if hashlib.sha1(data).hexdigest() != p["sha1"]:
            print(f"sha1 mismatch {p['file']}")
            bad += 1
        for lv in read_pack(data):
            n += 1
            if a.full or n % a.sample == 0:
                r = solve(lv, slack=lv.moves - lv.par if lv.moves >= lv.par else 0)
                ok = is_stable(lv) and r.min_moves == lv.par and lv.moves >= lv.par
                if not ok:
                    print(f"invalid level #{n} in {p['file']}: par {lv.par} solver {r.min_moves}")
                    bad += 1
    print(f"validated {n} levels, {bad} problems")
    return 1 if bad else 0


def cmd_solve(a) -> int:
    with open(a.file) as f:
        lv = Level.from_json(json.load(f))
    r = solve(lv)
    print(json.dumps({"min_moves": r.min_moves, "limit": r.limit, **r.to_meta()}, indent=1))
    return 0


def cmd_schedule(a) -> int:
    for n, tier, beat in schedule(a.levels):
        print(f"{n:5d} {tier:>2} {beat}")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="tiltdrop")
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("golden", help="write /shared/golden vectors (or --check for drift)")
    p.add_argument("--random", type=int, default=1000)
    p.add_argument("--check", action="store_true")
    p.add_argument("--refresh-solver-levels", action="store_true", help="re-pick solver vector levels from the generator")
    p.set_defaults(fn=cmd_golden)
    p = sub.add_parser("levels", help="generate + curate level segments (resumable)")
    p.add_argument("--first", type=int, default=1)
    p.add_argument("--last", type=int, default=300)
    p.add_argument("--curated", type=int, default=300, help="levels up to here use --k candidates per slot")
    p.add_argument("--k", type=int, default=12)
    p.add_argument("--k-pool", type=int, default=2)
    p.add_argument("--workers", type=int, default=os.cpu_count() or 2)
    p.add_argument("--bot-runs", type=int, default=60)
    p.add_argument("--final-bot-runs", type=int, default=200)
    p.set_defaults(fn=cmd_levels)
    p = sub.add_parser("daily", help="generate daily puzzle levels")
    p.add_argument("--count", type=int, default=400)
    p.add_argument("--workers", type=int, default=os.cpu_count() or 2)
    p.set_defaults(fn=cmd_daily)
    p = sub.add_parser("pack", help="write packs + manifest + curated.json + review.html")
    p.add_argument("--curated", type=int, default=300)
    p.add_argument("--content-version", type=int, default=1)
    p.add_argument("--daily-epoch", default="2027-01-01", help="local date of daily puzzle #1")
    p.set_defaults(fn=cmd_pack)
    p = sub.add_parser("validate", help="re-verify shipped packs")
    p.add_argument("--full", action="store_true")
    p.add_argument("--sample", type=int, default=25)
    p.set_defaults(fn=cmd_validate)
    p = sub.add_parser("solve", help="solve a level JSON file")
    p.add_argument("file")
    p.set_defaults(fn=cmd_solve)
    p = sub.add_parser("schedule", help="print the pacing schedule")
    p.add_argument("--levels", type=int, default=100)
    p.set_defaults(fn=cmd_schedule)
    a = ap.parse_args(argv)
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())
