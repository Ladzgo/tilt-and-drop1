# Game 4 — Slide Out (GDD)

**Pitch:** a tray-and-arrow tidy-up. Every block on the board points somewhere. Tap one and it zooms out along its arrow, as long as nothing is in the way. Blocks that leave land in a small tray, and three of a colour pop. Clear the board without letting the tray fill up. Order is everything: the block you want first is often stuck behind one you'd rather keep for later.

## 1. Rules (normative; Python `tools/levelgen/slide`, JS `web/slide.js`, C# `Core/Slide` must match the golden vectors)

- Grid `W × H` (4–8), `x` right, `y` down. Directions: 0 right, 1 down, 2 left, 3 up.
- Cells: empty, wall, or a **block** with a colour (1–6) and an arrow direction. Text tokens are two characters per cell: `..` empty, `##` wall, `1>` / `2v` / `3<` / `4^` block (colour + arrow).
- **Tap a block:** if every cell from the block to the board edge along its arrow is empty, the block leaves the board and is appended to the **tray**. Otherwise nothing happens and no move is spent (a "bump"; the first blocking cell is highlighted).
- **Tray:** capacity `C` (5 by default, 4 in the "tight tray" beat). Right after a block enters, if the tray holds 3 blocks of that colour, those 3 are removed (a match).
- **Win:** the board and the tray are both empty. The generator guarantees every colour count is a multiple of 3.
- **Lose:** after a tap (and its match), the tray holds `C` blocks.
- **Continue (rewarded ad or coins):** the tray grows by 2 slots for the rest of this attempt.
- **Stars:** there is no move limit, because every level takes exactly *n* taps. Stars measure clean play: ★★★ with no undo, hint or continue; ★★ with at most one; ★ otherwise.

## 2. Solver and difficulty
- State = (removed-block mask, tray as colour counts). Tray order doesn't matter for matching. DFS with memoisation decides solvability and counts reachable and dead states up to a node cap. The hint is a tap that keeps the position winnable.
- Difficulty features: dead-end ratio (share of reachable positions that can no longer be won), the number of tappable blocks per position, and the noisy-greedy bot's fail rate. The bot taps free blocks, preferring ones that complete or extend a pair in the tray. That greedy pull is exactly what the levels are built to punish.

## 3. Generation (solvable by construction)
1. Choose a removal order: shuffle *k* triples of colours and keep the order only if replaying it never fills the tray (orders whose peak tray is close to `C` make harder levels).
2. Place blocks in **reverse** order. Each block gets a random empty cell and an arrow whose path to the edge is clear of the blocks already placed (those leave *after* it in the real game). The intended order is therefore always a valid solution.
3. Add walls first (they shape lanes), then measure dead-end ratio and bot fail rate, and curate with the shared tier ramp.

## 4. Mechanic schedule
| Levels | Beat |
|---|---|
| 1–10 | 2 colours, 6–9 blocks |
| 11–40 | 3–4 colours |
| 41–80 | walls |
| 81–120 | crowded boards (15–18 blocks) |
| 121–160 | tight tray (4 slots) |
| 161–200 | review: walls + crowds + tight tray |
| 201+ | frozen blocks (2 taps), one-way arrows that turn after each tap, linked pairs (post-MVP) |

## 5. Feel
- **Exit:** the block whooshes out along its arrow with ease-in, then arcs into its tray slot with a soft click.
- **Match:** the three blocks squeeze, pop into confetti, and the remaining tray slides together. A rising chime plays.
- **Bump:** the tapped block nudges toward the blocker, the blocker flashes, and a dull tick plays. No move is spent.
- **Danger:** the tray glows orange when one slot is left. The interface never adds timers or pressure prompts.
