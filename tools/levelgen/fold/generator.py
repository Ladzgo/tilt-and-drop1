"""Fold generator: random two-sided colours, a random fold sequence defines the target; BFS gives real par."""
from __future__ import annotations

import random
from dataclasses import dataclass

from .model import FoldLevel
from .sim import fold, initial_sheet, legal_folds, visible
from .solver import solve


@dataclass(frozen=True)
class FSpec:
    name: str = "basic"
    sizes: tuple = ((3, 1), (4, 1))
    colors: tuple = (2, 3)
    two_sided: float = 0.0       # chance a square's back differs from white
    axes: str = "V"
    folds: tuple = (1, 2)
    par: tuple = (1, 2)
    max_solutions: int = 6


def build(spec: FSpec, rng: random.Random) -> FoldLevel | None:
    w, h = rng.choice(spec.sizes)
    if rng.random() < 0.5 and "H" in spec.axes:
        w, h = h, w
    n = w * h
    pal = rng.sample(range(1, 7), rng.randint(*spec.colors))
    front = [rng.choice(pal) for _ in range(n)]
    back = [rng.choice(pal) if rng.random() < spec.two_sided else 0 for _ in range(n)]
    probe = FoldLevel(w, h, front, back, ["0"])
    sheet = initial_sheet(probe)
    for _ in range(rng.randint(*spec.folds)):
        opts = [m for m in legal_folds(sheet) if m[0] in spec.axes]
        if not opts:
            break
        sheet = fold(sheet, *rng.choice(opts))
    target = visible(probe, sheet)
    if len(set("".join(target))) == 1 and len(target) * len(target[0]) > 1:
        return None  # flat single colour targets are dull
    return FoldLevel(w, h, front, back, target)


@dataclass
class FCandidate:
    level: FoldLevel
    par: int
    solutions: int
    dead_end: float
    fail_by_slack: dict
    spec: str
    seed: int


def generate_one(spec: FSpec, seed: int, bot_runs: int = 60, max_attempts: int = 300) -> FCandidate | None:
    from .bot import fail_rate
    rng = random.Random(seed)
    for _ in range(max_attempts):
        lv = build(spec, rng)
        if lv is None:
            continue
        s = solve(lv)
        if s.par is None or not spec.par[0] <= s.par <= spec.par[1] or s.solutions > spec.max_solutions:
            continue
        lv.par = s.par
        fails = {k: fail_rate(lv, s.par + k, runs=bot_runs, seed=seed) for k in (0, 1, 2, 3)}
        return FCandidate(lv, s.par, s.solutions, s.dead_end_ratio, fails, spec.name, seed)
    return None
