# Game 5 — Gear Works (GDD)

**Pitch:** a motor, a few output dials and a pegboard. Drop gears onto pegs to carry the motor's spin to every dial. Meshing gears turn in opposite directions, so the path length decides which way a dial turns. Close a loop with an odd number of gears and the whole train jams.

## 1. Rules (normative; Python `tools/levelgen/gears`, JS `web/gears.js`, C# `Core/Gears` must match the golden vectors)

### 1.1 Board
The board is a **hex grid** with odd rows shifted right by half a cell. Real gears pack this way, and it makes direction a real puzzle: on a square grid every gear's direction would be fixed by its checkerboard colour, and loops could never jam. Neighbour order for cell (x, y): even rows (x+1,y), (x−1,y), (x−1,y−1), (x,y−1), (x−1,y+1), (x,y+1); odd rows (x+1,y), (x−1,y), (x,y−1), (x+1,y−1), (x,y+1), (x+1,y+1).

| Token | Cell | Notes |
|---|---|---|
| `.` | nothing | no gear can go here |
| `o` | empty peg | tap to place a gear (uses one from the inventory) |
| `g` | gear on a peg | tap to remove it (returns to the inventory) |
| `G` | fixed gear | can't be removed |
| `M` | motor | a gear that always turns clockwise |
| `R` / `L` | output dial | a gear that must turn **clockwise** (`R`) or **counter-clockwise** (`L`) |

The level also states the **inventory**: how many removable gears may be on the board at once.

### 1.2 Spin
- Every `g`, `G`, `M`, `R` and `L` cell is a gear. Neighbouring gears (6 neighbours) mesh. Three gears that touch pairwise form a triangle, an odd loop, so the group jams.
- For each connected group of meshed gears that contains a motor: the motors turn clockwise (+1), and every meshing neighbour turns the opposite way. If that's impossible (an odd loop, or two motors forcing opposite spins), the **whole group jams** and nothing in it turns.
- Groups without a motor don't turn.

### 1.3 Moves, win, lose
- Tapping `o` with inventory left places a gear; tapping `g` removes it. Each tap = 1 move. Taps on anything else, or on `o` with an empty inventory, do nothing and cost nothing.
- **Win:** every output dial turns in its required direction (so none is jammed or still).
- **Lose:** the move limit reaches 0 first. Continue (+3 moves), stars and slack work as in the other games.

### 1.4 Par
Taps commute (only which pegs hold gears matters), so `par` = the fewest toggles from the start position to any winning layout within the inventory. The solver enumerates layouts.

## 2. Generation
1. Place the motor, outputs, walls and pegs.
2. Lay random paths of pegs from the motor to each output. These form the intended layout; some path cells become fixed gears.
3. Each output's required direction = the direction it actually turns in the intended layout. Jams are rejected.
4. Add decoy pegs, including ones that would close odd loops. Scramble: remove some intended gears and place some decoys. Inventory = intended removable gears (+0 or +1).
5. Exact par and the number of winning layouts come from the solver; the fail rate comes from the bot. Curation uses the shared ramp.

## 3. Mechanic schedule
| Levels | Beat |
|---|---|
| 1–10 | one output, a straight chain |
| 11–40 | two outputs, direction matters |
| 41–80 | jams: decoy pegs that close odd loops |
| 81–120 | fixed gears and tight inventory |
| 121–160 | two motors (their spins must agree) |
| 161–200 | review |
| 201+ | belts (two pulleys turn the same way at a distance), clutches, big 2×2 gears (post-MVP) |

## 4. Feel
- Every gear spins continuously at its speed and direction (teeth drawn procedurally). New spin flows outward from the motor in about 0.15 s waves.
- **Placing a gear:** it drops onto the peg with a bounce and a metallic clack, then catches the spin.
- **Jam:** the group grinds. It shakes, tints red, plays a low buzz, and its outputs flash.
- **Correct dial:** a light comes on and a bell rings, pitched per dial. Win: every gear speeds up for one second, then the stars appear.
