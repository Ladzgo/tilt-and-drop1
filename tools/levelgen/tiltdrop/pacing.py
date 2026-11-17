"""Sawtooth difficulty pacing and the mechanic schedule (docs/GDD.md §7).

Tiers: E (easy), N (normal), H (hard), SH (super-hard).
Per block of 20 levels (offset 1..20):
  * SH at offset 20 (levels 20, 40, 60, ...)
  * H at offset h1 in {6,7,8} and h2 in {13,14} (deterministic jitter)
    -> spikes are 5..8 levels apart
  * 3 easy levels after SH, 2 easy levels after H
  * level 1..20: spikes softened (H->N, SH->H), first 5 levels easy
  * the first level of each mechanic beat is an easy tutorial
"""
from __future__ import annotations

import zlib
from dataclasses import dataclass

from .generator import GenSpec

TIERS = ("E", "N", "H", "SH")
TIER_SLACK = {"E": 4, "N": 2, "H": 1, "SH": 0}
WORLD_SIZE = 50


def _jitter(block: int, salt: str, choices: tuple[int, ...]) -> int:
    return choices[zlib.crc32(f"{salt}:{block}".encode()) % len(choices)]


@dataclass(frozen=True)
class Beat:
    start: int
    name: str
    intro: str | None  # mechanic introduced (tutorial level), or None
    spec: GenSpec


# ---- generator specs per beat ----------------------------------------------------
S = GenSpec
BEATS: tuple[Beat, ...] = (
    Beat(1, "basics", "colors", S("basics", sizes=((4, 4), (4, 5), (5, 5)), colors=(1, 1), blocks_per_color=(1, 3), walls=(0.0, 0.1), par=(1, 4))),
    Beat(11, "two_colors", "two_colors", S("two_colors", sizes=((5, 5), (5, 6)), colors=(2, 2), blocks_per_color=(1, 3), par=(2, 6))),
    Beat(31, "stone", "stone", S("stone", sizes=((5, 6), (6, 6)), colors=(1, 2), stones=(1, 3), par=(3, 8), require=("stone",))),
    Beat(61, "multi_gate", "multi_gate", S("multi_gate", sizes=((6, 6),), colors=(2, 3), gates_per_color=(1, 2), stones=(0, 2), par=(3, 9))),
    Beat(91, "anchor", "anchor", S("anchor", sizes=((6, 6), (6, 7)), colors=(2, 3), anchors=(1, 2), stones=(0, 2), par=(3, 9), require=("anchor",))),
    Beat(121, "ice", "ice", S("ice", sizes=((6, 6), (6, 7)), colors=(2, 3), ice=(1, 2), anchors=(0, 1), stones=(0, 2), par=(3, 10), require=("ice",))),
    Beat(151, "four_colors", "four_colors", S("four_colors", sizes=((6, 7), (7, 7)), colors=(3, 4), blocks_per_color=(1, 2), gates_per_color=(1, 2), stones=(0, 2), anchors=(0, 1), ice=(0, 1), par=(4, 11))),
    Beat(181, "stone_maze", None, S("stone_maze", sizes=((6, 7), (7, 7)), colors=(2, 3), blocks_per_color=(1, 2), stones=(3, 6), walls=(0.04, 0.14), par=(4, 11))),
    Beat(211, "big_boards", "big_boards", S("big_boards", sizes=((7, 8), (8, 8)), colors=(2, 3), blocks_per_color=(1, 2), stones=(1, 3), walls=(0.0, 0.08), anchors=(0, 1), ice=(0, 1), par=(5, 12))),
    Beat(241, "five_colors", "five_colors", S("five_colors", sizes=((7, 7),), colors=(4, 5), blocks_per_color=(1, 1), gates_per_color=(1, 1), stones=(0, 2), walls=(0.0, 0.08), par=(5, 12))),
    Beat(271, "review", None, S("review")),  # mixes all earlier specs
    # post-launch mechanics (sticky, one-way, bombs, ...) are not simulated yet:
    # levels 301+ come from the mixed pool and are marked provisional until
    # those mechanics ship (shipped levels never change afterwards).
    Beat(301, "pool", None, S("pool")),
)

MIX_SPECS = tuple(b.spec for b in BEATS if b.name in ("multi_gate", "anchor", "ice", "four_colors", "stone_maze", "big_boards", "five_colors"))


def beat_for_level(n: int) -> Beat:
    if n < 1:
        raise ValueError("levels start at 1")
    cur = BEATS[0]
    for b in BEATS:
        if b.start <= n:
            cur = b
    return cur


def spec_for_level(n: int, k: int = 0) -> GenSpec:
    """Generator spec for level n (k = candidate index, varies mixed specs)."""
    b = beat_for_level(n)
    if b.name in ("review", "pool"):
        pool = MIX_SPECS if b.name == "pool" else tuple(x.spec for x in BEATS if x.start < b.start and x.name != "basics")
        return pool[zlib.crc32(f"mix:{n}:{k}".encode()) % len(pool)]
    return b.spec


def is_intro(n: int) -> bool:
    b = beat_for_level(n)
    return b.start == n and b.intro is not None


def tier_for_level(n: int) -> str:
    if n < 1:
        raise ValueError("levels start at 1")
    if n <= 5 or is_intro(n):
        return "E"
    block, off = divmod(n - 1, 20)
    off += 1  # 1..20
    h1 = _jitter(block, "h1", (6, 7, 8))
    h2 = _jitter(block, "h2", (13, 14))
    if off == 20:
        t = "SH"
    elif off in (h1, h2):
        t = "H"
    elif off <= 3 and block > 0:  # after previous SH
        t = "E"
    elif h1 < off <= h1 + 2 or h2 < off <= h2 + 2:
        t = "E"
    else:
        t = "N"
    if block == 0:  # first 20 levels: softer spikes
        t = {"SH": "H", "H": "N"}.get(t, t)
    return t


def world_of(n: int) -> int:
    return (n - 1) // WORLD_SIZE + 1


def schedule(n_levels: int) -> list[tuple[int, str, str]]:
    """[(level, tier, beat)] for levels 1..n_levels."""
    return [(n, tier_for_level(n), beat_for_level(n).name) for n in range(1, n_levels + 1)]
