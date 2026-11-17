"""Feature clustering to avoid serving similar-feeling levels back to back."""
from __future__ import annotations

import math
import random

from .model import ANCHOR, ICE1, ICE2, STONE, WALL, Level


def feature_vector(lv: Level, par: int, solutions: int) -> list[float]:
    kinds = [c & 0xF0 for c in lv.cells]
    area = lv.w * lv.h
    n_gates = sum(1 for e in "TRBL" for v in lv.gates[e] if v)
    return [
        par / 14,
        len(lv.colors()) / 6,
        area / 64,
        kinds.count(STONE) / 8,
        kinds.count(ANCHOR) / 3,
        (kinds.count(ICE2) + kinds.count(ICE1)) / 3,
        kinds.count(WALL) / area,
        n_gates / 12,
        math.log2(max(1, solutions)) / max(1, par),
        abs(lv.w - lv.h) / 4,
    ]


def _d2(a: list[float], b: list[float]) -> float:
    return sum((x - y) ** 2 for x, y in zip(a, b))


def kmeans(vectors: list[list[float]], k: int, seed: int = 0, iters: int = 30) -> list[int]:
    """Plain k-means++ (deterministic for a seed). Returns a label per vector."""
    n = len(vectors)
    if n == 0:
        return []
    k = max(1, min(k, n))
    rng = random.Random(seed)
    centers = [vectors[rng.randrange(n)]]
    while len(centers) < k:
        d = [min(_d2(v, c) for c in centers) for v in vectors]
        total = sum(d)
        if total == 0:
            break
        r = rng.random() * total
        acc = 0.0
        for v, dv in zip(vectors, d):
            acc += dv
            if acc >= r:
                centers.append(v)
                break
    labels = [0] * n
    for _ in range(iters):
        new = [min(range(len(centers)), key=lambda j: _d2(v, centers[j])) for v in vectors]
        if new == labels and _ > 0:
            break
        labels = new
        for j in range(len(centers)):
            members = [vectors[i] for i in range(n) if labels[i] == j]
            if members:
                centers[j] = [sum(col) / len(members) for col in zip(*members)]
    return labels
