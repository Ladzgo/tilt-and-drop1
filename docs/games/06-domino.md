# Game 6 — Domino Run (GDD)

**Pitch:** a table of standing dominoes, a few bells, and one push. Rotate the arrow dominoes to steer the chain, then press **Push** and watch it run. Every bell must ring. You get three pushes, and the fewer you need, the more stars you earn.

## 1. Rules (normative; Python `tools/levelgen/domino`, JS `web/domino.js`, C# `Core/Domino` must match the golden vectors)

### 1.1 Board (two characters per cell)
| Token | Piece | Falls toward… |
|---|---|---|
| `..` | floor | (nothing stands here) |
| `##` | wall | (stops the chain) |
| `d.` | domino | the direction it was pushed |
| `t>` `tv` `t<` `t^` | turner (rotatable) | its arrow, whatever pushed it. **Tap rotates it 90° clockwise** |
| `T>` … | fixed turner | its arrow; can't be rotated |
| `Y.` | splitter | both sides perpendicular to the push (the chain forks) |
| `B.` | bell | rings and stops; it doesn't pass the chain on |
| `S>` … | start domino | its arrow, when you press Push |

Directions: 0 right, 1 down, 2 left, 3 up (y down).

### 1.2 The chain
- **Push** knocks the start domino at time 0. A piece knocked at time *t* falls; the cell next to it in each of its out-directions is reached at *t*+1.
- A standing domino, turner, splitter or bell in that cell is knocked. Floor, walls and the board edge stop that branch. Every piece falls at most once.
- Hits are processed in order of (time, cell index, incoming direction). When two hits reach a plain domino at the same time, the first in that order decides its direction.
- **Win:** every bell rang during the push.

### 1.3 Pushes, stars, lose
- Rotating turners is free. Each **Push** uses one of 3 attempts. After a failed push the dominoes stand back up and your turner settings stay.
- Stars: ★★★ on the first push, ★★ on the second, ★ on the third.
- **Lose:** the third push fails. Continue (rewarded ad or coins) gives one more push.

## 2. Solver and difficulty
- A configuration is a direction for every turner (4^t, t ≤ 7). The solver enumerates them and counts the winning ones. `par` = fewest clockwise taps from the start setting to a winning one; the hint shows the next turner to tap.
- Difficulty: turner count, the winning share of configurations, splitters, and the bot's fail rate. The bot "mentally traces" the chain and sometimes misjudges a turner. It learns from the bells each failed push rang, and fails if three pushes aren't enough.

## 3. Generation
Grow the chain from the start: straight runs of dominoes, a turner at each corner (its correct direction recorded), the occasional splitter fork, and a bell at each branch end. Then add decoy turners beside the chain and lone dominoes that lead nowhere, and scramble the turner arrows. Curation uses the shared tier ramp, with pushes playing the role of moves.

## 4. Mechanic schedule
| Levels | Beat |
|---|---|
| 1–10 | one corner, one bell |
| 11–40 | several corners, decoy turners |
| 41–80 | splitters, two or three bells |
| 81–120 | fixed turners and walls |
| 121–160 | long chains (timing matters when branches meet) |
| 161–200 | review |
| 201+ | stairs (a chain that falls twice as fast), seesaws, timed gates (post-MVP) |

## 5. Feel
This is the game's marketing clip: each domino tips with a short rotation and a clack whose pitch climbs slowly along the chain. Branches play their clacks in parallel. Bells ring with a bright chime and a ripple. Before you push, a ghost preview shows the first two tiles only. The rest is the reveal.
