# Tilt & Drop — Level Pipeline & Content Report

Code: `tools/levelgen/tiltdrop/` · Output: `shared/levels/` · Parity vectors: `shared/golden/`

## Pipeline

```
generator (seeded)  ->  solver (BFS) + bot  ->  curation per slot (pacing)  ->  packs (.tdp.gz) + manifest
       |                       |                         |
  stable by construction   par, solutions,          tier target fail rate,
  lane-biased gates        dead-ends, random-win,   cluster anti-repetition,
  reachability prefilter   bot fail at slack 0/1/2/4 near-duplicate dedupe
```

1. **Generate** (`generator.py`): scatter walls and pieces, settle under gravity Down **with no gates** (so the board is stable by construction), place gates (never facing a wall, never under a resting block of the same color on the bottom edge, 60% biased toward lanes that hold that color), then reject boards where a piece couldn't reach its gate even alone (cheap prefilter).
2. **Analyze** (`solver.py`, `bot.py`, `difficulty.py`): BFS state graph (≈23 µs/state); `par`, solutions at par, dead-end ratio, random-player win rate, effective branching. Then the **noisy-greedy bot** (softmax over the two rotations, line-of-sight heuristic, revisit avoidance, 60% chance of 1-ply lookahead) plays 60–200 seeded attempts at move limits par+0/1/2/4.
3. **Curate** (`curate.py`): each segment (one mechanic beat, ≤ 50 levels) gets *k* candidates per slot. Each slot takes the unused, non-duplicate candidate whose **bot fail rate at the slot tier's real move limit** is closest to the tier target, avoiding the k-means clusters of the previous two levels. Intro levels take the easiest, shortest candidate.
4. **Dedupe** (`canon.py`): canonical key = minimum over D4 images (re-oriented so gravity points down), after color normalization. A candidate is also rejected if its start position is within 2 moves of an existing level (or vice versa).
5. **Pack** (`pack.py`): 1,000 levels per gzipped `.tdp` pack (CRC32, reproducible bytes), plus `curated.json` (levels 1–300 with full metadata) and `manifest.json`.

## Tiers and targets (v2 ramp)

| Tier | Move slack | Target first-try fail, level 1 → 300 | Measured (bot, levels 1–300) | Count (1–300) |
|---|---|---|---|---|
| E | +3 | 8% → 22% | 18% | 112 |
| N | +2 | 25% → 50% | 42% | 145 |
| H | +1 | 55% → 72% | 65% | 29 |
| SH | +0 | 75% → 90% | 81% | 14 |

| World | Mean par | Mean bot fail (E+N levels) | Hardest level (bot fail) |
|---|---|---|---|
| 1 | 4.0 | 17% | 70% |
| 2 | 5.7 | 23% | 83% |
| 3 | 6.8 | 32% | 81% |
| 4 | 7.0 | 33% | 85% |
| 5 | 7.4 | 41% | 92% |
| 6 | 9.2 | 44% | 91% |

The first version (flat targets, Easy +4) played too easy: mean par went from 2.4 to 5. Targets now come from `pacing.target_fail(level, tier)`, plus a rising floor `pacing.min_par(level)`. The bot is still a proxy. After soft launch, fit `difficulty.WEIGHTS` and the bot's τ/ε to the real first-attempt fail rates, then fix outliers live with the `difficulty_overrides` Remote Config key.

## Mechanic beats (levels 1–300 at launch)

| Levels | Beat | Generator spec |
|---|---|---|
| 1–10 | basics (levels 1–3 hand-made tutorials) | 4×4–5×5, 1 color, par 1–3 |
| 11–30 | two colors | 5×5–5×6, par 2–5 |
| 31–60 | stone | +1–3 stones, par 2–7 |
| 61–90 | multiple gates | 6×6, 2–3 colors, 1–2 gates per color |
| 91–120 | anchor | 1–2 anchors |
| 121–150 | ice | 1–2 ice (+ occasional anchor) |
| 151–180 | four colors | 6×7–7×7, 3–4 colors |
| 181–210 | stone maze | 3–6 stones, more walls |
| 211–240 | big boards | 7×8–8×8 |
| 241–270 | five colors | 7×7, 4–5 colors |
| 271–300 | review | mix of all earlier specs |
| 301–20,000 | pool | mix of implemented mechanics, marked `provisional` |

Levels above 300 are **provisional**. When sticky pairs, one-way gates and bombs ship (GDD §7.3), regenerate the levels after the highest shipped level so the new mechanics appear every ~30 levels. Levels that have already shipped must never change: their IDs and contents stay stable, and `contentVersion` in the manifest goes up.

## Manual curation workflow

1. `python -m tiltdrop pack` writes `tools/levelgen/out/review.html`. Open it in a browser: each level card shows the board, tier, par, moves, difficulty and solution.
2. To replace a level, put its JSON (from any candidate or hand-made) into `shared/levels/overrides.json`: `{"57": { "w": 6, "h": 6, "grid": [...], "gates": {...} }}`. The pipeline validates stability, re-solves it, and assigns moves from the slot's tier.
3. Delete the affected segment file in `tools/levelgen/out/segments/` and re-run `levels`, `pack`, then Sync in Unity.

## Golden vectors (Python ↔ C# parity)

| File | Content |
|---|---|
| `rules.json` | 14 hand cases covering every rule clause: rotation mapping, nearest-first, cascade through the same gate, wrong gate acts as a wall, stone at a gate, zero-travel exit, win on the last move, lose → continue → win, anchor release, ice 2 hits + thaw, hammer |
| `random.json` | 1,000 random boards (incl. anchors/ice/stones) × up to 12 inputs (rotations, hammer, continue), with the event trace for every step |
| `solver.json` | 44 levels: min moves, solutions at par, dead-end ratio, random-win rate |

Event strings (`M0:2:1,2>0,2`, `X0:1:L2`, `H1:5:0`, `R0:3`, `K:4:0,2`) are compared byte for byte by `GoldenTests.cs`. `python -m tiltdrop golden --check` fails CI if the Python simulator drifts from the committed vectors.
