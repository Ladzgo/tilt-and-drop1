"""Noisy-greedy "human-like" bot used to estimate fail rates.

Each turn the bot scores both rotations:
    score(a) = EXIT_W * exits(a) + h(child)  [+ best follow-up if it looks ahead]
and picks one with softmax(score / tau); with probability eps it plays randomly.
h() rewards fewer remaining colored pieces and pieces that have a clear straight
line to a matching gate (what a human "sees"). The bot walks the solver's
explored graph, so no re-simulation is needed.
"""
from __future__ import annotations

import math
import random
from dataclasses import dataclass
from functools import lru_cache

from .model import BLOCK, DX, DY, EMPTY, GRAVITY_EDGE, Level, is_colored
from .solver import Graph


@dataclass(frozen=True)
class BotParams:
    tau: float = 0.6
    eps: float = 0.05
    lookahead_prob: float = 0.6
    lookahead_w: float = 0.7
    exit_w: float = 3.0
    remain_w: float = 4.0
    access_w: float = 1.0
    revisit_penalty: float = 3.0  # humans notice "I have been here before"


DEFAULT = BotParams()


class LineOfSight:
    """For every cell and direction: the cells up to the edge and the gate at the end."""

    def __init__(self, level: Level):
        self.w, self.h = level.w, level.h
        self.paths: list[list[tuple[tuple[int, ...], int]]] = []
        for i in range(level.w * level.h):
            x0, y0 = i % level.w, i // level.w
            per_dir = []
            for g in range(4):
                x, y, cells = x0, y0, []
                while True:
                    x, y = x + DX[g], y + DY[g]
                    if not (0 <= x < level.w and 0 <= y < level.h):
                        break
                    cells.append(y * level.w + x)
                lane = x0 if g in (0, 2) else y0
                per_dir.append((tuple(cells), level.gates[GRAVITY_EDGE[g]][lane]))
            self.paths.append(per_dir)

    def access(self, cells: bytes) -> int:
        n = 0
        for i, c in enumerate(cells):
            if c & 0xF0 != BLOCK:
                continue
            col = c & 0x0F
            for path, gate in self.paths[i]:
                if gate == col and all(cells[j] == EMPTY or cells[j] & 0xF0 == BLOCK for j in path):
                    n += 1
                    break
        return n


def run_bot(gr: Graph, limit: int, runs: int = 200, seed: int = 0, params: BotParams = DEFAULT) -> float:
    """Returns the estimated fail rate in [0, 1]."""
    los = LineOfSight(gr.level)
    rng = random.Random(seed)

    @lru_cache(maxsize=None)
    def h(i: int) -> float:
        cells = gr.cells[i]
        remaining = sum(1 for c in cells if is_colored(c))
        if remaining == 0:
            return 1000.0
        return -params.remain_w * remaining + params.access_w * los.access(cells)

    def q(i: int, a: int) -> float:
        ch = gr.children[i]
        assert ch is not None and gr.exits[i] is not None
        return params.exit_w * gr.exits[i][a] + h(ch[a])

    def best_follow(j: int) -> float:
        if gr.win[j] or gr.children[j] is None:
            return 0.0
        return max(q(j, 0), q(j, 1)) * params.lookahead_w

    fails = 0
    for _ in range(runs):
        i, left = 0, limit
        look = rng.random() < params.lookahead_prob
        visited = {0}
        while not gr.win[i] and left > 0:
            ch = gr.children[i]
            if ch is None:
                break  # outside explored region (only when the graph was truncated)
            if rng.random() < params.eps:
                a = rng.randrange(2)
            else:
                s = [
                    q(i, k)
                    + (best_follow(ch[k]) if look and left > 1 else 0.0)
                    - (params.revisit_penalty if ch[k] in visited else 0.0)
                    for k in (0, 1)
                ]
                m = max(s)
                e0, e1 = math.exp((s[0] - m) / params.tau), math.exp((s[1] - m) / params.tau)
                a = 0 if rng.random() * (e0 + e1) < e0 else 1
            i = ch[a]
            visited.add(i)
            left -= 1
        if not gr.win[i]:
            fails += 1
    h.cache_clear()
    return fails / runs
