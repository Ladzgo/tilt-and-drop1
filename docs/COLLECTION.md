# Puzzle Collection — App Concept

The app is a **collection of 5–10 short-session logic puzzles** under one roof. They share the player profile, coins, daily habits and the level/solver pipeline. Tilt & Drop is game 1.

## Why a collection

- **Retention:** players who tire of one mechanic switch games instead of churning. Each game has its own daily puzzle, so there are several reasons to open the app each day.
- **One pipeline, many games:** every game is deterministic, solver-verified and mass-generated with the same Python tooling (simulator, solver, bot, pacing, packs, golden vectors) and the same C# core pattern.
- **UA testing:** each game's "feel" clip can be tested as a separate ad creative; the winner becomes the store icon/first game.

## Games (planned order)

| # | Name (working) | Core action | Hook | Status |
|---|---|---|---|---|
| 1 | **Tilt & Drop** | rotate the whole board; gravity cascades blocks into matching gates | chain-exit cascades | built (rules, 20k levels, C# core, Unity layer, web playtest) |
| 2 | **Prism Paths** | tap mirrors to bend coloured light beams into matching targets; colours mix additively | glowing beams, red+blue = magenta | rules (Py/JS/C#), 200 levels, web playable; Unity view next (docs/games/02-prism.md) |
| 3 | **Roll & Print** | roll a cube across tiles; each face prints its colour where it lands | spatial reasoning, satisfying stamps | rules (Py/JS/C#), 200 levels, web playable; Unity view next (docs/games/03-roll.md) |
| 4 | **Slide Out** | tap arrow blocks off the board; 3 of a colour pop in a small tray, a full tray loses | "jam"-style clears, order puzzles | rules (Py/JS/C#), 200 levels, web playable; Unity view next (docs/games/04-slide.md) |
| 5 | **Gear Works** | place gears on a hex pegboard so the motor drives every dial the right way; triangles jam | parity puzzles, spinning machinery | rules (Py/JS/C#), 200 levels, web playable; Unity view next (docs/games/05-gears.md) |
| 6 | **Domino Run** | rotate turner dominoes, then one push: every bell must ring (3 pushes) | one big chain reaction | rules (Py/JS/C#), 200 levels, web playable; Unity view next (docs/games/06-domino.md) |
| 7 | **Fold** | fold two-sided paper along creases until the top layer matches the target | mental folding | rules (Py/JS/C#), 200 levels, web playable; Unity view next (docs/games/07-fold.md) |
| 8 | **Flow Fill** | turn pipes so each colour of water reaches its cups; open ends leak, colours that meet turn muddy | visible floods, leaks | rules (Py/JS/C#), 200 levels, web playable; Unity view next (docs/games/08-flow.md) |

Each game must pass a gate before it ships: a playable web prototype, then a 50-level playtest. It ships only if the median session is longer than 4 minutes and the first-try win rate on its first 20 levels is above 70%.

## Shared systems (one implementation, used by every game)

- **Profile and economy:** one coin wallet, the boosters every game understands (Undo, Hint, +moves), stars per game, one store. Compliance rules (COMPLIANCE.md) apply app-wide.
- **Daily:** each game has a daily puzzle. A shared **daily streak** counts a day as played if *any* daily is solved. The free weekly freeze and the login calendar are shared.
- **Album:** one sticker page per game world.
- **Pacing:** shared tier system (E/N/H/SH) and ramp functions (`pacing.target_fail`, `min_par`), applied per game.
- **Ads:** interstitial pacing counts wins across all games (GDD §6.2 rules unchanged).
- **Hub screen:** game cards with progress, "today's dailies" row, streak. The last game played opens first.

## Code layout per game

```
tools/levelgen/<game>/      python: model, sim, solver, generator, golden  (reuses tiltdrop pacing/pack/canon helpers)
shared/<game>/levels, golden
game/Assets/_Project/Games/<Game>/Core   pure C# rules + solver (TiltDrop.Core-style, golden parity tests)
game/Assets/_Project/Games/<Game>/View   board view + controller implementing IPuzzleGame
web/<game>.js                            browser prototype (same rules, golden-tested with node)
```

The Unity layer gets an `IPuzzleGame` interface (`Id`, `LoadLevel(n)`, `StartSession`, `BuildView`, `Hint`, `Undo`). GameRoot hosts the hub and the shared meta; each game plugs in its own board view and controller.
