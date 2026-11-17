# Game 3 — Roll & Print (GDD)

**Pitch:** a soft cube with wet paint on some of its faces. Swipe to tip it over, one tile at a time. Wherever it lands, the bottom face prints its colour on the tile. Paint every marked tile in its colour. Getting the right face to land on the right tile at the right time is the puzzle, and a careless roll can overprint a tile you already finished.

## 1. Rules (normative; Python `tools/levelgen/roll`, JS `web/roll.js` and C# `Core/Roll` must match the golden vectors)

### 1.1 Board and cube
- Grid `W × H` (4–7), `x` right and `y` down. Directions: 0 east (+x), 1 south (+y), 2 west, 3 north.
- The cube sits on one tile. Its six faces are tracked in world frame: `T` top, `B` bottom, `N`, `S`, `E`, `W`. Each holds a colour 0 (blank) or 1–6.
- Rolling tips the cube over the edge in the move direction:
  - east: T←W, E←T, B←E, W←B
  - west: T←E, W←T, B←W, E←B
  - south: T←N, S←T, B←S, N←B
  - north: T←S, N←T, B←N, S←B

### 1.2 Tiles
| Tile | Token | Effect when the cube lands on it |
|---|---|---|
| Floor | `.` | none |
| Wall | `#` | can't be entered |
| Target (required colour c) | `1`–`6` | if `B` ≠ 0, the tile takes colour `B` (overprints) |
| Paint pool (colour c) | `a`–`f` | the bottom face takes colour c (pools re-paint the cube) |
| Eraser (post-MVP beat) | `*` | the bottom face becomes blank |
| Ice | `~` | the cube keeps rolling in the same direction until the next tile isn't ice or is blocked; effects apply on every ice tile it lands on |

### 1.3 Moves, win, lose
- A swipe toward a wall or the board edge does nothing and costs no move. Every successful roll costs 1 move (ice slides included).
- After a roll, landing effects apply: pool, then target stamp, then eraser, in tile order along an ice slide.
- **Win:** every target shows its required colour.
- **Lose:** the move limit reaches 0 first. Continue (+3 moves), stars and slack work as in the other games.

## 2. Generation
1. Build the board (walls, pools, ice) and place the cube with 1–3 painted faces.
2. Random-walk the cube for `L` rolls, applying the rules as if every floor tile were a target. Record the last colour printed on each tile.
3. Pick `k` tiles with a non-blank final print as targets, with that colour as the required colour. The walk is a valid solution by construction.
4. BFS (state = position, the six faces, target colours) gives the exact par, the number of shortest solutions and the dead-end ratio. The noisy-greedy bot gives the fail rate. Curation uses the shared tier ramp (`pacing.target_fail`).

## 3. Mechanic schedule
| Levels | Beat |
|---|---|
| 1–10 | one painted face, 1–2 targets |
| 11–40 | two or three colours on the cube |
| 41–80 | paint pools (the cube re-paints itself) |
| 81–120 | walls and tight corridors |
| 121–160 | ice tiles |
| 161–200 | pools + ice + more targets (review) |
| 201+ | erasers, pre-printed wrong tiles, two cubes (post-MVP) |

## 4. Feel
- **Roll:** top-down projection of a real tip-over. The top face narrows to cos θ while the incoming side face grows to sin θ, over 0.16 s, with a small lift and squash on landing.
- **Print:** a paint splat burst in the face colour, the tile fills with a wet sheen, and a soft "thup". A correct print adds a chime and a check ring. Overprinting a finished tile plays a dull note and the ring breaks.
- **Cube net:** the HUD shows a live net of the cube (top in the middle, the four sides around it, bottom marked), so hidden faces are always readable. Each colour also has a symbol for colour-blind players.
