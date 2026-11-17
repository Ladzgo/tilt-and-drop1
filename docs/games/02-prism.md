# Game 2 — Prism Paths (GDD)

**Pitch:** coloured lasers, a board of mirrors, and targets that each want one exact colour. Tap mirrors to flip them and bend the beams. A red and a blue beam meeting in one target make **magenta**. When every target glows the right colour, the board lights up.

## 1. Rules (normative; Python `tools/levelgen/prism` and C#/JS ports must match)

### 1.1 Board
- Grid `W × H`, 4 ≤ W, H ≤ 8, coordinates `x` right and `y` down (screen convention).
- Colours are a 3-bit mask: R = 1, G = 2, B = 4. Mixes: Y = R|G = 3, M = R|B = 5, C = G|B = 6, W = 7.

### 1.2 Cells
| Cell | Token | Behaviour when a beam enters |
|---|---|---|
| Empty | `.` | passes straight on |
| Wall | `#` | absorbs (beam stops) |
| Source `S` (colour c ∈ R,G,B, direction d) | `[R>]`, `[G^]`, `[Bv]`, `[R<]` | emits one beam of colour c toward d at level start; a beam entering a source stops |
| Target (required mask m ≠ 0) | `(R)`, `(M)`, `(W)` … | absorbs, adding the beam colour to the target's received mask |
| Mirror, rotatable, state `/` or `\` | `/` `\` | reflects; **tap toggles it** |
| Fixed mirror | `{/}` `{\}` | reflects; can't be tapped |
| Filter (colour c) | `<R>` `<G>` `<B>` | passes beams of colour c; absorbs others |
| Splitter, rotatable `/` or `\` (post-MVP) | `[/]` `[\]` | beam continues straight **and** reflects |

Reflection, with direction (dx, dy) and y pointing down:
- `/` maps (dx, dy) → (−dy, −dx). A beam moving right turns up; a beam moving down turns left.
- `\` maps (dx, dy) → (dy, dx). A beam moving right turns down.

### 1.3 Tracing
- Each source's beam is traced cell by cell from the cell next to the source, in source order (row-major).
- A beam stops when it leaves the board, enters a wall, source or target, is absorbed by a filter, or revisits the same (cell, direction) for its colour (loop guard).
- Beams of different colours, and the same colour from different sources, pass through each other. Light is additive: a target's received mask is the OR of all colours that reach it.
- A **target is lit** when received == required. A target that receives a colour it doesn't want shows "muddy".

### 1.4 Moves, win, lose
- One tap on a rotatable piece = 1 move; the piece toggles state. Taps on anything else do nothing and cost nothing.
- **Win:** after a tap (or at load, which the generator forbids), every target is lit.
- **Lose:** the move limit reaches 0 with some target unlit. Same continue options as game 1 (+3 moves via rewarded ad or coins, no timer).
- Stars (same formula as game 1): ★★★ at par taps, ★★ within par + ⌈slack/2⌉, ★ any win.

### 1.5 Par
Taps commute (a piece's state depends only on how many times it was tapped), so `par` = the minimum over all winning configurations of the number of rotatable pieces whose state differs from the start. The solver enumerates configurations (2^m, m ≤ 16) with beam-path pruning.

## 2. Level generation
1. Place sources on the border (pointing inward) and walls.
2. Build each source's **intended path** by random walk; at each turn, place a mirror in the orientation that makes the turn. End each path in a target, or in a target shared with another path to create a mixed colour. The target's required mask = OR of the colours whose paths end there.
3. Add **decoys**: extra rotatable mirrors off the paths, mirrors whose correct state is "keep", and filters that block a wrong colour.
4. Scramble: flip a random subset of rotatable mirrors (always at least one on a path), reject levels already solved at the start, compute par and the number of solutions, and prefer unique solutions.
5. Difficulty features: par, rotatable count, solution count, mixed targets, beam crossings, shared mirrors (a mirror on two paths), and the noisy-greedy bot's fail rate (it taps mirrors on the currently wrong beam paths first). Pacing and tiers come from Tilt & Drop (`pacing.target_fail`, `min_par`).

## 3. Mechanic schedule
| Levels | Beat |
|---|---|
| 1–10 | 1 source, 1 target, 1–3 mirrors (tutorial: tap to flip) |
| 11–40 | 2 colours, decoy mirrors |
| 41–80 | colour mixing (Y, M, C targets) |
| 81–120 | fixed mirrors and walls |
| 121–160 | filters |
| 161–200 | white targets (all three colours) |
| 201+ | splitters, then portals, then moving sources (post-MVP) |

## 4. Feel
- **Tap:** the mirror spins 90° with a springy overshoot (0.18 s). A soft glass "tink" plays, pitched per mirror.
- **Beam retrace:** the new beam path draws outward from each source at about 40 cells/s with additive glow. Previously lit targets that lose their light dim over 0.15 s.
- **Target lights up:** bloom pulse, a chime per colour (R/G/B form a chord, mixes play two or three notes), and a light haptic.
- **Win:** all beams pulse brighter, the board flashes, particles fly from every target, then the star reveal.
- Colour-blind support: each colour has a symbol on sources and targets (R ●, G ▲, B ■, mixes combine them), and beams use a distinct dash pattern per colour.

## 5. Metrics to watch in the prototype
First-try win rate on levels 1–20 (target > 70%), taps per level vs par, how often hints are used, session length.
