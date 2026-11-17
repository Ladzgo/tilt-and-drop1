"""Domino Run player: traces the chain in its head, setting each turner it reaches; sometimes it knows the
right arrow, otherwise it aims at the nearest bell not yet rung. Learns a little after each failed push."""
from __future__ import annotations

import random

from .model import DX, DY, DominoLevel
from .sim import run_chain


def fail_rate(level: DominoLevel, winning: list, runs: int = 60, seed: int = 0, skill: float = 0.22) -> float:
    rng = random.Random(seed)
    ts = level.turners
    tidx = {c: k for k, c in enumerate(ts)}
    bells = level.bells
    w = level.w
    fails = 0
    for _ in range(runs):
        cfg = list(level.start_config())
        sk = skill
        target = rng.choice(winning) if winning else None
        won = False
        for attempt in range(level.attempts):
            # walk the chain as the player imagines it, fixing turners on the way
            for _ in range(len(ts) + 1):
                falls, rung = run_chain(level, tuple(cfg))
                changed = False
                for t, c, outs in falls:
                    if c in tidx and rng.random() < 0.85:
                        k = tidx[c]
                        if target is not None and rng.random() < sk:
                            want = target[k]
                        else:
                            todo = [b for b in bells if b not in rung] or bells
                            x, y = c % w, c // w

                            def gap(d):
                                nx, ny = x + DX[d], y + DY[d]
                                return min(abs(b % w - nx) + abs(b // w - ny) for b in todo) + rng.random() * 1.5
                            want = min(range(4), key=gap)
                        if cfg[k] != want:
                            cfg[k] = want
                            changed = True
                            break
                if not changed:
                    break
            falls, rung = run_chain(level, tuple(cfg))
            if set(bells) <= rung:
                won = True
                break
            sk = min(0.95, sk + 0.1)
        if not won:
            fails += 1
    return fails / runs
