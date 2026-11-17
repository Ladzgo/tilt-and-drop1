"""Golden test vectors shared with the C# core (/shared/golden).

Event strings (compact, byte-comparable across languages):
    M{pass}:{id}:{fx},{fy}>{tx},{ty}   move
    X{pass}:{id}:{edge}{lane}          exit
    H{pass}:{id}:{hp}                  ice hit (hp 0 = thawed)
    R{pass}:{id}                       anchor release
    K:{id}:{x},{y}                     hammer removal
Inputs: "CW", "CCW", "H:x,y" (hammer), "+N" (continue with N moves).
"""
from __future__ import annotations

import json
import random
from typing import Any

from .generator import generate_one
from .handmade import HANDMADE
from .model import ANCHOR, BLOCK, DIR_NAMES, EMPTY, ICE2, STONE, WALL, Level, grid_rows
from .pacing import BEATS
from .sim import PLAYING, Game
from .solver import solve

SCHEMA = 1


def event_str(e: tuple, w: int) -> str:
    t = e[0]
    if t == "move":
        _, p, pid, a, b = e
        return f"M{p}:{pid}:{a % w},{a // w}>{b % w},{b // w}"
    if t == "exit":
        _, p, pid, edge, lane = e
        return f"X{p}:{pid}:{edge}{lane}"
    if t == "hit":
        _, p, pid, hp = e
        return f"H{p}:{pid}:{hp}"
    if t == "release":
        _, p, pid = e
        return f"R{p}:{pid}"
    if t == "hammer":
        _, _, pid, i = e
        return f"K:{pid}:{i % w},{i // w}"
    raise ValueError(t)


def snapshot(g: Game, full: bool = True) -> dict[str, Any]:
    d: dict[str, Any] = {"gravity": DIR_NAMES[g.gravity], "movesLeft": g.moves_left, "status": g.status}
    if full:
        d["grid"] = grid_rows(g.cells, g.level.w, g.level.h)
    return d


def apply_input(g: Game, inp: str) -> list[tuple]:
    if inp in ("CW", "CCW"):
        return g.rotate(inp)
    if inp.startswith("H:"):
        x, y = map(int, inp[2:].split(","))
        return g.hammer(x, y)
    if inp.startswith("+"):
        g.add_moves(int(inp[1:]))
        return []
    raise ValueError(inp)


def run_case(name: str, level: Level, inputs: list[str], full_every_step: bool = True) -> dict[str, Any]:
    g = Game(level)
    steps = []
    for k, inp in enumerate(inputs):
        ev = apply_input(g, inp)
        st = {"input": inp, "events": [event_str(e, level.w) for e in ev]}
        st.update(snapshot(g, full_every_step or k == len(inputs) - 1))
        steps.append(st)
    return {"name": name, "level": level.to_json(), "initial": snapshot(Game(level)), "steps": steps}


HAND_CASES: list[tuple[str, dict, list[str]]] = [
    ("rotation_no_move_costs_move", {"grid": ["....", "....", "11.2"], "gates": {"L": "..1"}, "moves": 5}, ["CW", "CCW", "CCW", "CCW"]),
    ("cascade_same_gate_wrong_gate_wall", {"grid": ["....", "....", "11.2"], "gates": {"L": "..1"}, "moves": 5}, ["CCW"]),
    ("win_on_last_move", {"grid": ["....", "....", "11.."], "gates": {"L": "..1"}, "moves": 1}, ["CCW"]),
    ("lose_then_continue_then_win", {"grid": ["....", "....", "11.."], "gates": {"L": "..1"}, "moves": 1}, ["CW", "+3", "CCW", "CCW"]),
    ("stone_never_exits", {"grid": ["....", "....", "@1.."], "gates": {"L": "..1"}, "moves": 3}, ["CCW", "CW", "CW"]),
    ("nearest_first_column_up", {"grid": ["....", "1...", "....", "1..."], "gates": {"T": "1..."}, "moves": 3}, ["CCW", "CCW"]),
    ("anchor_release_same_settle", {"grid": ["[a1]...", "....", "1..."], "gates": {"L": "1.1"}, "moves": 3}, ["CCW"]),
    ("anchor_other_color_stays", {"grid": ["[a1]...", "....", "2..."], "gates": {"L": "1.2"}, "moves": 3}, ["CCW", "CW", "CW"]),
    ("ice_two_hits_thaw_cascade", {"grid": ["....", "....", "1.[i2]."], "gates": {"R": "..2"}, "moves": 10}, ["CW", "CCW", "CCW", "CW", "CW"]),
    ("hammer_resettle_win", {"grid": ["....", "@...", "1..."], "gates": {}, "moves": 3}, ["H:0,2"]),
    ("hammer_then_rotate", {"grid": ["2...", "@...", "1..."], "gates": {"R": "..2"}, "moves": 4}, ["H:0,2", "CW"]),
    ("zero_travel_exit_corner", {"grid": ["....", "....", "1..2"], "gates": {"R": "..2", "L": "..1"}, "moves": 3}, ["CW", "CCW", "CCW"]),
    ("gate_on_every_edge", {"grid": ["#..#", ".12.", ".34.", "#..#"], "gates": {"T": ".1..", "R": ".2..", "B": "..3.", "L": "..4."}, "moves": 8}, ["CW", "CW", "CW", "CW", "CCW", "CCW"]),
    ("already_won_on_load", {"grid": ["....", "....", "@..."], "gates": {}, "moves": 3}, []),
]


def hand_cases() -> list[dict[str, Any]]:
    out = []
    for name, d, inputs in HAND_CASES:
        rows = d["grid"]
        lv = Level.from_rows(rows, d.get("gates"), moves=d["moves"], id=name)
        out.append(run_case(name, lv, inputs))
    return out


def random_level(rng: random.Random) -> Level:
    w, h = rng.randint(3, 8), rng.randint(3, 8)
    pool = [EMPTY] * 7 + [WALL, STONE, STONE] + [BLOCK | c for c in (1, 2, 3, 4)] * 2 + [ANCHOR | 1, ANCHOR | 2, ICE2 | 3, ICE2 | 1]
    cells = bytes(rng.choice(pool) for _ in range(w * h))
    gates = {e: bytes(rng.choice([0, 0, 1, 2, 3, 4]) for _ in range(w if e in "TB" else h)) for e in "TRBL"}
    return Level(w, h, cells, gates, moves=rng.randint(3, 12))


def random_cases(n: int, seed: int = 20261117) -> list[dict[str, Any]]:
    rng = random.Random(seed)
    out = []
    for i in range(n):
        lv = random_level(rng)
        lv.id = f"rnd{i:04d}"
        g = Game(lv)
        inputs: list[str] = []
        for _ in range(rng.randint(1, 12)):
            if g.status == "won":
                break
            if g.status == "lost":
                inp = "+3"
            elif rng.random() < 0.05:
                pieces = [j for j, c in enumerate(g.cells) if c not in (EMPTY, WALL)]
                if not pieces:
                    break
                j = rng.choice(pieces)
                inp = f"H:{j % lv.w},{j // lv.w}"
            else:
                inp = rng.choice(["CW", "CCW"])
            apply_input(g, inp)
            inputs.append(inp)
        out.append(run_case(lv.id, lv, inputs, full_every_step=False))
    return out


def solver_cases() -> list[dict[str, Any]]:
    levels: list[Level] = []
    for n, d in HANDMADE.items():
        lv = Level.from_json(d)
        lv.id = f"hand{n}"
        levels.append(lv)
    levels.append(Level.from_rows(["....", "....", "11.2"], {"L": "..1", "R": "..2"}, id="two_color"))
    for b in BEATS:
        if b.name in ("review", "pool"):
            continue
        for k in range(4):
            c = generate_one(b.spec, 7000 + k, bot_runs=4)
            if c:
                c.level.id = f"{b.name}_{k}"
                levels.append(c.level)
    out = []
    for lv in levels:
        r = solve(lv, slack=2)
        lv.moves = (r.min_moves or 0) + 2
        out.append({
            "name": lv.id,
            "level": lv.to_json(),
            "minMoves": r.min_moves,
            "solutionsAtMin": r.solutions_at_min,
            "limit": r.limit,
            "deadEndRatio": round(r.dead_end_ratio, 6),
            "randomWin": round(r.random_win_rate, 6),
        })
    return out


def build_all(n_random: int = 1000) -> dict[str, dict[str, Any]]:
    return {
        "rules.json": {"schema": SCHEMA, "cases": hand_cases()},
        "random.json": {"schema": SCHEMA, "cases": random_cases(n_random)},
        "solver.json": {"schema": SCHEMA, "solver": solver_cases()},
    }


def dumps(obj: Any) -> str:
    """Pretty for small files; one compact case per line for large case lists (diff-friendly)."""
    if isinstance(obj, dict) and len(obj.get("cases", [])) > 100:
        body = ",\n".join(json.dumps(c, separators=(",", ":"), ensure_ascii=True) for c in obj["cases"])
        return '{"schema":%d,"cases":[\n%s\n]}\n' % (obj["schema"], body)
    return json.dumps(obj, indent=1, sort_keys=False, ensure_ascii=True) + "\n"
