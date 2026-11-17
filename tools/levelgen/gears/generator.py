"""Gear Works generator: route gear chains from motors to dials, set each dial's required direction
from the intended layout, add tempting decoy pegs (many beside the chains, where a gear jams), scramble."""
from __future__ import annotations

import random
from dataclasses import dataclass

from .model import GearLevel
from .sim import neighbours, spins
from .solver import solve


@dataclass(frozen=True)
class GSpec:
    name: str = "basic"
    sizes: tuple = ((5, 5),)
    motors: int = 1
    outputs: tuple = (1, 1)
    fixed_p: float = 0.0
    decoys: tuple = (1, 2)
    scramble_remove: tuple = (1, 2)
    scramble_add: tuple = (0, 1)
    extra_inventory: tuple = (0, 1)
    par: tuple = (1, 3)
    max_pegs: int = 14


def _route(w, h, rng, src: set[int], dst: int, blocked: set[int]) -> list[int] | None:
    """Random-weighted BFS path from any src cell to dst avoiding blocked."""
    order = list(src)
    rng.shuffle(order)
    prev = {s: None for s in order}
    frontier = order
    while frontier:
        nxt = []
        rng.shuffle(frontier)
        for i in frontier:
            nb = neighbours(w, h, i)
            rng.shuffle(nb)
            for j in nb:
                if j in prev or (j in blocked and j != dst):
                    continue
                prev[j] = i
                if j == dst:
                    path = []
                    k = prev[j]
                    while k is not None and k not in src:
                        path.append(k)
                        k = prev[k]
                    return path[::-1]
                nxt.append(j)
        frontier = nxt
    return None


def build(spec: GSpec, rng: random.Random) -> GearLevel | None:
    w, h = rng.choice(spec.sizes)
    n = w * h
    cells = ["."] * n
    free = list(range(n))
    rng.shuffle(free)
    motors = [free.pop() for _ in range(spec.motors)]
    for m in motors:
        cells[m] = "M"
    k = rng.randint(*spec.outputs)
    outs = []
    for c in free:
        if len(outs) == k:
            break
        if all(c not in neighbours(w, h, m) and c != m for m in motors) and all(c not in neighbours(w, h, o) for o in outs):
            outs.append(c)
    if len(outs) < k:
        return None
    for o in outs:
        cells[o] = "X"  # placeholder
    network = set(motors)
    intended: set[int] = set()
    for o in outs:
        path = _route(w, h, rng, network, o, set(motors) | set(outs) - {o})
        if path is None:
            return None
        intended.update(path)
        network.update(path)
        network.add(o)
    if not intended:
        return None
    fixed = {i for i in intended if rng.random() < spec.fixed_p}
    for i in intended:
        cells[i] = "G" if i in fixed else "g"
    # required directions from the intended layout
    probe = GearLevel(w, h, [("G" if c == "X" else c) for c in cells], inventory=n)
    spin, jam = spins(probe, probe.start_layout())
    if any(jam[i] for i in range(n)) or any(spin[o] == 0 for o in outs):
        return None
    for o in outs:
        cells[o] = "R" if spin[o] == 1 else "L"
    removable = [i for i in intended if i not in fixed]
    # decoys: pegs beside the chains (jam/redirect risk) and elsewhere
    near = sorted({j for i in intended | set(outs) for j in neighbours(w, h, i) if cells[j] == "."})
    far = [i for i in range(n) if cells[i] == "." and i not in near]
    rng.shuffle(near)
    rng.shuffle(far)
    d = rng.randint(*spec.decoys)
    decoys = (near[: max(1, d * 2 // 3)] + far)[:d]
    for i in decoys:
        cells[i] = "o"
    if sum(1 for c in cells if c in "og") > spec.max_pegs:
        return None
    inventory = len(removable) + rng.randint(*spec.extra_inventory)
    # scramble: lift some intended gears, drop some decoys
    lift = rng.sample(removable, min(len(removable), rng.randint(*spec.scramble_remove))) if removable else []
    for i in lift:
        cells[i] = "o"
    on_board = sum(1 for c in cells if c == "g")
    for i in rng.sample(decoys, min(len(decoys), rng.randint(*spec.scramble_add))):
        if on_board < inventory:
            cells[i] = "g"
            on_board += 1
    lv = GearLevel(w, h, cells, inventory)
    from .sim import is_win
    if is_win(lv, lv.start_layout()):
        return None
    return lv


@dataclass
class GCandidate:
    level: GearLevel
    par: int
    solutions: int
    fail_by_slack: dict
    spec: str
    seed: int


def generate_one(spec: GSpec, seed: int, bot_runs: int = 60, max_attempts: int = 300) -> GCandidate | None:
    from .bot import fail_rate
    rng = random.Random(seed)
    for _ in range(max_attempts):
        lv = build(spec, rng)
        if lv is None:
            continue
        s = solve(lv)
        if s.par is None or not spec.par[0] <= s.par <= spec.par[1]:
            continue
        lv.par = s.par
        fails = {k: fail_rate(lv, s.par + k, s.layouts, runs=bot_runs, seed=seed) for k in (0, 1, 2, 3)}
        return GCandidate(lv, s.par, s.solutions, fails, spec.name, seed)
    return None
