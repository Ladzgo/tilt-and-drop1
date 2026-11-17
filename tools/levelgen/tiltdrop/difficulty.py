"""Feature extraction and difficulty score (0..100).

Weights are a first guess; recalibrate against analytics (first-attempt fail
rate per level) after soft launch by fitting WEIGHTS with least squares.
"""
from __future__ import annotations

import math
from dataclasses import dataclass

from .bot import DEFAULT, BotParams, run_bot
from .model import STONE, Level
from .solver import count_solutions, dead_end_ratio, effective_branching, explore, first_solution, random_win_rate, win_distance

WEIGHTS = {
    "par": 0.25,
    "rarity": 0.15,
    "dead": 0.15,
    "bot": 0.25,
    "random": 0.10,
    "size": 0.05,
    "colors": 0.05,
}

TIER_SLACK = {"E": 4, "N": 2, "H": 1, "SH": 0}
NOMINAL_SLACK = 2
SLACKS = (0, 1, 2, 4)  # bot fail rate is estimated at each of these move limits


@dataclass
class Analysis:
    par: int
    solutions: int
    dead_end: float
    random_win: float
    bot_fail: float
    branching: float
    states: int
    truncated: bool
    colors: int
    area: int
    pieces: int
    stones: int
    solution: str
    limit: int
    fail_by_slack: dict[int, float]

    def features(self) -> dict[str, float]:
        rarity = 1.0 - min(1.0, math.log2(max(1, self.solutions)) / max(1, self.par))
        return {
            "par": min(1.0, self.par / 14),
            "rarity": rarity,
            "dead": self.dead_end,
            "bot": self.bot_fail,
            "random": 1.0 - self.random_win,
            "size": max(0.0, (self.area - 16) / 48),
            "colors": (self.colors - 1) / 5,
        }

    def score(self) -> float:
        f = self.features()
        return round(100 * sum(WEIGHTS[k] * f[k] for k in WEIGHTS), 2)

    def meta(self) -> dict:
        return {
            "solutions_at_par": self.solutions,
            "dead_end_ratio": round(self.dead_end, 4),
            "random_win": round(self.random_win, 4),
            "bot_fail": round(self.bot_fail, 4),
            "branching": self.branching,
            "states": self.states,
            "difficulty": self.score(),
            "solution": self.solution,
        }


def analyze(
    level: Level,
    slack: int = NOMINAL_SLACK,
    max_depth: int = 24,
    node_cap: int = 200_000,
    bot_runs: int = 100,
    seed: int = 0,
    params: BotParams = DEFAULT,
) -> Analysis | None:
    """Returns None if unsolvable within max_depth (or truncated before a win)."""
    gr = explore(level, max_depth=max_depth, extra_after_win=max(slack, max(SLACKS)), node_cap=node_cap)
    par = gr.min_win_depth()
    if par is None:
        return None
    limit = par + slack
    dist = win_distance(gr)
    sol = first_solution(gr)
    n_par = sum(1 for d in gr.depth if d <= par)
    fails = {s: run_bot(gr, par + s, runs=bot_runs, seed=seed, params=params) for s in sorted(set(SLACKS) | {slack})}
    return Analysis(
        par=par,
        solutions=count_solutions(gr, par),
        dead_end=dead_end_ratio(gr, limit, dist),
        random_win=random_win_rate(gr, limit),
        bot_fail=fails[slack],
        branching=effective_branching(n_par, par),
        states=len(gr),
        truncated=gr.truncated,
        colors=len(level.colors()),
        area=level.w * level.h,
        pieces=sum(1 for c in level.cells if c not in (0, 0x10)),
        stones=sum(1 for c in level.cells if c == STONE),
        solution="".join("R" if r == "CW" else "L" for r in sol),
        limit=limit,
        fail_by_slack=fails,
    )
