# Game 7 — Fold (GDD)

**Pitch:** a sheet of paper with a different colour on each side of every square, and a small target pattern. Fold the sheet along its creases until the top layer shows the target. Every fold flips the folded part, so its back colours come up. It's a pure mental-folding puzzle, with a satisfying paper flip on each move.

## 1. Rules (normative; Python `tools/levelgen/fold`, JS `web/fold.js`, C# `Core/Fold` must match the golden vectors)

- The sheet starts as `W × H` squares (2–6 each way). Square *s* has a **front** colour and a **back** colour (0 = white, 1–6 colours). All squares start front side up.
- The state is a grid of **stacks** (top first). Each entry is (square id, face-up flag). The visible colour of a cell = the top square's front colour if it's face up, otherwise its back colour.
- **Fold** `(axis, k, side)`: the crease lies between column (or row) *k*−1 and *k* of the current sheet.
  - `side 0`: the left (or top) part folds over onto the right (or bottom) part. Needs part size ≤ remaining size.
  - `side 1`: the right (or bottom) part folds over onto the left (or top) part. Needs part size ≤ remaining size.
  - Each folded cell lands mirrored across the crease on top of the cell it covers. Its whole stack is **reversed and every face flipped**, then placed above that cell's stack.
  - The sheet shrinks to the remaining part (re-indexed from 0).
- **Win:** the sheet has the target's shape and every visible colour matches.
- Each fold = 1 move. Move limit = par + slack. Lose, continue (+3 moves) and stars work as in the other games. Undo restores the previous sheet.

## 2. Solver and generation
- BFS over sheet states (stacks plus shape) gives par, the number of shortest solutions and dead-end states. The tree is small because sheets only shrink.
- **Generation:** random colours (some squares one-colour, some two-tone), then a random sequence of 2–6 folds. Its visible result becomes the target, and BFS finds the real par (often shorter). Levels with too many equivalent solutions or trivial targets are rejected.

## 3. Mechanic schedule
| Levels | Beat |
|---|---|
| 1–10 | 1–2 folds, one axis, single-sided colours |
| 11–40 | two-sided squares |
| 41–80 | both axes |
| 81–120 | longer sequences (4–5 folds) |
| 121–160 | bigger sheets (5×5, 6×4) |
| 161–200 | review |
| 201+ | holes (show what's underneath), stickers that must end on top, mirror-image targets (post-MVP) |

## 5. Feel
- Creases are dashed lines with small arrows. Tapping one folds the smaller side over; for the middle crease, the side you tapped folds.
- **Fold:** the flap swings over the crease. Its width follows cos θ, and past 90° its back colours appear. A paper swish plays, then a soft thump as it lands.
- The target sits in a framed card above the sheet. Cells that already match glow faintly.
