"""Breadth-first solver and puzzle metrics.

State = (cells bytes, gravity). Piece ids are not part of the state, and ice HP
is encoded in the cell byte, so `cells + gravity` is a canonical hash key.

`explore()` builds the state graph layer by layer (first-visit BFS) and keeps
the children of each expanded state, so all metrics are computed without
re-simulating:

* min_moves          - depth of the shallowest win
* solutions_at_min   - number of move sequences of length min_moves that win
                       (shortest-path counting: every prefix of an optimal
                       sequence is itself a shortest path)
* dead_end_ratio     - share of non-won states at depth 1..L-1 from which no
                       win is reachable within the remaining L-depth moves
* random_win_rate    - probability that a uniformly random player wins within L
* effective_branching- b* with sum_{i<=par} b*^i = #states up to depth par
"""
from __future__ import annotations

from dataclasses import dataclass, field
from functools import lru_cache

from .model import CCW, CW, DOWN, GRAVITY_EDGE, Level, rotate_gravity
from .sim import has_colored, settle, topology

ROTS = (CW, CCW)


@dataclass
class Graph:
    level: Level
    cells: list[bytes] = field(default_factory=list)
    grav: list[int] = field(default_factory=list)
    depth: list[int] = field(default_factory=list)
    win: list[bool] = field(default_factory=list)
    children: list[tuple[int, int] | None] = field(default_factory=list)
    exits: list[tuple[int, int] | None] = field(default_factory=list)
    index: dict[bytes, int] = field(default_factory=dict)
    expanded_depth: int = -1  # all states with depth <= this have been expanded
    truncated: bool = False  # node cap hit

    def __len__(self) -> int:
        return len(self.cells)

    def add(self, cells: bytes, g: int, d: int) -> int:
        key = cells + bytes((g,))
        i = self.index.get(key)
        if i is not None:
            return i
        i = len(self.cells)
        self.index[key] = i
        self.cells.append(cells)
        self.grav.append(g)
        self.depth.append(d)
        self.win.append(not has_colored(cells))
        self.children.append(None)
        self.exits.append(None)
        return i

    def min_win_depth(self) -> int | None:
        best = None
        for i, w in enumerate(self.win):
            if w and (best is None or self.depth[i] < best):
                best = self.depth[i]
        return best


def _expand(gr: Graph, i: int, topo, gates) -> None:
    cells, g, d = gr.cells[i], gr.grav[i], gr.depth[i]
    kids, exs = [], []
    for rot in ROTS:
        ng = rotate_gravity(g, rot)
        buf = bytearray(cells)
        ex = settle(buf, topo, ng, gates[ng])
        kids.append(gr.add(bytes(buf), ng, d + 1))
        exs.append(ex)
    gr.children[i] = (kids[0], kids[1])
    gr.exits[i] = (exs[0], exs[1])


def explore(
    level: Level,
    max_depth: int = 30,
    extra_after_win: int = 0,
    node_cap: int = 300_000,
    start_cells: bytes | None = None,
    start_gravity: int = DOWN,
) -> Graph:
    """First-visit BFS. Stops at `max_depth`, at the node cap, or `extra_after_win`
    layers after the first win layer (wins are terminal and not expanded)."""
    gr = Graph(level)
    topo = topology(level.w, level.h)
    gates = [level.gates[GRAVITY_EDGE[g]] for g in range(4)]
    gr.add(bytes(start_cells if start_cells is not None else level.cells), start_gravity, 0)
    frontier = [0]
    win_depth = 0 if gr.win[0] else None
    d = 0
    while frontier and d < max_depth:
        if win_depth is not None and d >= win_depth + extra_after_win:
            break
        if len(gr) > node_cap:
            gr.truncated = True
            break
        nxt_start = len(gr)
        for i in frontier:
            if not gr.win[i]:
                _expand(gr, i, topo, gates)
        gr.expanded_depth = d
        frontier = list(range(nxt_start, len(gr)))
        d += 1
        if win_depth is None and any(gr.win[i] for i in frontier):
            win_depth = d
    return gr


def count_solutions(gr: Graph, min_moves: int) -> int:
    paths = [0] * len(gr)
    paths[0] = 1
    order = sorted(range(len(gr)), key=gr.depth.__getitem__)
    for i in order:
        if gr.depth[i] >= min_moves or paths[i] == 0:
            continue
        ch = gr.children[i]
        if ch is None:
            continue
        for c in ch:
            if gr.depth[c] == gr.depth[i] + 1:
                paths[c] += paths[i]
    return sum(paths[i] for i in range(len(gr)) if gr.win[i] and gr.depth[i] == min_moves)


def win_distance(gr: Graph) -> list[int]:
    """Moves-to-win for every state inside the explored graph (large = unknown)."""
    inf = 1 << 30
    dist = [0 if w else inf for w in gr.win]
    parents: list[list[int]] = [[] for _ in range(len(gr))]
    for i, ch in enumerate(gr.children):
        if ch:
            for c in ch:
                parents[c].append(i)
    queue = [i for i, w in enumerate(gr.win) if w]
    head = 0
    while head < len(queue):
        c = queue[head]
        head += 1
        for p in parents[c]:
            if dist[p] == inf:
                dist[p] = dist[c] + 1
                queue.append(p)
    return dist


def first_solution(gr: Graph) -> list[str]:
    dist = win_distance(gr)
    if dist[0] >= 1 << 30:
        return []
    path, i = [], 0
    while not gr.win[i]:
        ch = gr.children[i]
        assert ch is not None
        for rot, c in zip(ROTS, ch):
            if dist[c] == dist[i] - 1:
                path.append(rot)
                i = c
                break
    return path


def dead_end_ratio(gr: Graph, limit: int, dist: list[int] | None = None) -> float:
    dist = dist or win_distance(gr)
    total = dead = 0
    for i in range(len(gr)):
        d = gr.depth[i]
        if 1 <= d <= limit - 1 and not gr.win[i]:
            total += 1
            if dist[i] > limit - d:
                dead += 1
    return dead / total if total else 0.0


def random_win_rate(gr: Graph, limit: int) -> float:
    @lru_cache(maxsize=None)
    def p(i: int, r: int) -> float:
        if gr.win[i]:
            return 1.0
        if r == 0:
            return 0.0
        ch = gr.children[i]
        if ch is None:  # outside explored region: treat as loss (conservative)
            return 0.0
        return 0.5 * (p(ch[0], r - 1) + p(ch[1], r - 1))

    try:
        return p(0, limit)
    finally:
        p.cache_clear()


def effective_branching(n_states: int, depth: int) -> float:
    if depth <= 0 or n_states <= depth + 1:
        return 1.0
    lo, hi = 1.0, 2.0
    for _ in range(50):
        mid = (lo + hi) / 2
        total = sum(mid**k for k in range(depth + 1))
        if total < n_states:
            lo = mid
        else:
            hi = mid
    return round((lo + hi) / 2, 4)


@dataclass
class SolveResult:
    solvable: bool
    min_moves: int | None
    solutions_at_min: int
    states: int
    truncated: bool
    solution: list[str]
    dead_end_ratio: float = 0.0
    random_win_rate: float = 0.0
    effective_branching: float = 1.0
    limit: int = 0

    def to_meta(self) -> dict:
        return {
            "solutions_at_par": self.solutions_at_min,
            "dead_end_ratio": round(self.dead_end_ratio, 4),
            "random_win": round(self.random_win_rate, 4),
            "branching": self.effective_branching,
            "states": self.states,
            "solution": "".join("R" if r == CW else "L" for r in self.solution),
        }


def solve(level: Level, max_depth: int = 30, slack: int | None = None, node_cap: int = 300_000) -> SolveResult:
    """Find par and metrics. `slack` sets the analysis limit L = par + slack
    (default: level.moves if set, else par + 2)."""
    pre = 4 if slack is None else slack
    gr = explore(level, max_depth=max_depth, extra_after_win=pre, node_cap=node_cap)
    mw = gr.min_win_depth()
    if mw is None:
        return SolveResult(False, None, 0, len(gr), gr.truncated, [])
    if slack is not None:
        limit = mw + slack
    elif level.moves:
        limit = max(level.moves, mw)
    else:
        limit = mw + 2
    if gr.expanded_depth < limit - 1 and not gr.truncated:
        gr = explore(level, max_depth=limit, extra_after_win=limit, node_cap=node_cap)
    dist = win_distance(gr)
    n_par = sum(1 for d in gr.depth if d <= mw)
    return SolveResult(
        solvable=True,
        min_moves=mw,
        solutions_at_min=count_solutions(gr, mw),
        states=len(gr),
        truncated=gr.truncated,
        solution=first_solution(gr),
        dead_end_ratio=dead_end_ratio(gr, limit, dist),
        random_win_rate=random_win_rate(gr, limit),
        effective_branching=effective_branching(n_par, mw),
        limit=limit,
    )


def min_moves(level: Level, cells: bytes | None = None, gravity: int = DOWN, max_depth: int = 30, node_cap: int = 300_000):
    """Just par (used for hints / quick checks). Returns (moves, first rotation) or (None, None)."""
    gr = explore(level, max_depth=max_depth, node_cap=node_cap, start_cells=cells, start_gravity=gravity)
    mw = gr.min_win_depth()
    if mw is None:
        return None, None
    sol = first_solution(gr)
    return mw, (sol[0] if sol else None)
