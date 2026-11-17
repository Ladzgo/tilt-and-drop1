# Game 8 — Flow Fill (GDD)

**Pitch:** coloured water, a board of pipe pieces and a few cups. Turn pipes to route each colour from its source to the cups of the same colour. Any pipe end the water reaches must connect to something, or it **leaks**, and colours that meet muddy each other. The water floods through visibly after every turn.

## 1. Rules (normative; Python `tools/levelgen/flow`, JS `web/flow.js`, C# `Core/Flow` must match the golden vectors)

### 1.1 Board
- Grid `W × H` (4–7). Directions: 0 right, 1 down, 2 left, 3 up (y down). Each piece's openings form a 4-bit mask.
- Pipe tokens are two characters: type + rotation (`0`–`3`, clockwise quarter turns). **Lowercase = rotatable, uppercase = fixed.**

| Type | Openings at rotation 0 |
|---|---|
| `i` / `I` straight | right, left |
| `l` / `L` corner | up, right |
| `t` / `T` tee | left, up, right |
| `X` cross (fixed) | all four |
| `..` | empty |

- **Sources** `[x, y, colour, dir]` and **cups** `[x, y, colour, dir]` occupy cells with a single opening `dir`.

### 1.2 Flow
- From every source at once, water moves breadth-first. From a cell with an opening toward direction *d*: if the neighbour has the matching opening (*d*+2) mod 4, water enters it. Otherwise (empty, board edge, or no matching opening) the water **leaks** at that edge.
- A cell reached by two different colours is **contaminated**.
- A cup is **filled** when exactly its own colour reaches it.
- **Win:** every cup is filled, with no leaks and no contamination.

### 1.3 Moves
- Tapping a rotatable pipe turns it 90° clockwise (1 move). Move limit = par + slack. Lose, continue (+3 moves) and stars work as in the other games.
- `par` = the fewest taps to any winning configuration. Taps commute.

## 2. Solver
Like Prism: flood from the sources and branch on each rotatable pipe the first time water reaches it. Branches cover its *distinct* opening masks (straights have two), each costing its cheapest clockwise distance. Branches prune immediately on a leak or contamination. Pipes the water never reaches don't matter.

## 3. Generation
Random paths connect each source to its cup(s). A second cup of the same colour branches off the first path through a tee. Pipe types follow the path geometry. Every other cell gets a decoy pipe, then everything rotatable is scrambled. Exact par and solution count come from the solver, the fail rate from the bot, and curation uses the shared ramp.

## 4. Mechanic schedule
| Levels | Beat |
|---|---|
| 1–10 | one source, one cup, short path |
| 11–40 | two colours |
| 41–80 | tees: one source, two cups |
| 81–120 | fixed pipes and crosses |
| 121–160 | three colours, crowded boards |
| 161–200 | review |
| 201+ | bridges (crossing without mixing), valves, one-way pipes (post-MVP) |

## 5. Feel
- **Tap:** the pipe spins with a springy quarter turn and a metallic tick.
- **Flood:** water runs outward from each source at about 12 tiles per second. Pipes fill with colour and a wobbling surface highlight.
- **Leaks:** water sprays droplet particles from the open end, with a hiss.
- **Contamination:** the pipe turns muddy brown and glugs.
- **Cups:** they fill up with a rising pour sound. All cups full: a bubbly celebration.
