# Tilt & Drop

A hybrid-casual puzzle collection for Android and iOS: eight short-session logic games in one app. Game 1, Tilt & Drop: rotate the board, let gravity cascade the blocks, and drop every block out through the gate of its own color.

| Path | What |
|---|---|
| [docs/GDD.md](docs/GDD.md) | Rules (normative), loops, economy, ads, pacing |
| [docs/TECH.md](docs/TECH.md) | Architecture, folders, data formats, testing, CI |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Status, 6-week plan, post-launch, manual steps to start now |
| [docs/COMPLIANCE.md](docs/COMPLIANCE.md) | GDPR/TCF, ATT, age screen, CPC principles, DFA commitments |
| [docs/SETUP.md](docs/SETUP.md) | **Install, open, build (Android/iOS), SDKs, CI secrets, store accounts** |
| [docs/LEVELS.md](docs/LEVELS.md) | Level pipeline, difficulty targets, curation workflow |
| [docs/COLLECTION.md](docs/COLLECTION.md) | The app as a 5–10 game puzzle collection; shared systems |
| [docs/games/02-prism.md](docs/games/02-prism.md) | Game 2: Prism Paths rules and design |
| [docs/games/03-roll.md](docs/games/03-roll.md) | Game 3: Roll & Print rules and design |
| [docs/games/04-slide.md](docs/games/04-slide.md) | Game 4: Slide Out rules and design |
| [docs/games/05-gears.md](docs/games/05-gears.md) | Game 5: Gear Works rules and design |
| [docs/games/06-domino.md](docs/games/06-domino.md) | Game 6: Domino Run rules and design |
| [docs/games/07-fold.md](docs/games/07-fold.md) | Game 7: Fold rules and design |
| [docs/games/08-flow.md](docs/games/08-flow.md) | Game 8: Flow Fill rules and design |
| `web/` | Browser playtest build of all 8 games (`python web/build.py`, parity: `node web/test-golden.cjs`) |
| `tools/levelgen/<game>/` | Per-game simulator, solver, generator, bot, curation (`python -m prism levels golden`, likewise roll, slide, gears, domino, fold, flow) |
| `shared/<game>/` | Per-game curated levels + golden vectors |
| `tools/levelgen/` | Python simulator, solver, generator, packer (`python -m tiltdrop --help`) |
| `shared/levels/` | 20,000 levels + 400 daily puzzles (packs) |
| `shared/golden/` | Python↔C# parity vectors |
| `game/` | Unity 6 LTS project (open this folder in Unity Hub) |
| `core-tests/` | Runs the Unity-free C# core tests with `dotnet test` |

Quick checks:
```bash
cd tools/levelgen && python -m pytest
```
```bash
dotnet test core-tests
```
