# Tilt & Drop

A hybrid-casual puzzle game for Android and iOS. Rotate the board, let gravity cascade the blocks, and drop every block out through the gate of its own color.

| Path | What |
|---|---|
| [docs/GDD.md](docs/GDD.md) | Rules (normative), loops, economy, ads, pacing |
| [docs/TECH.md](docs/TECH.md) | Architecture, folders, data formats, testing, CI |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Status, 6-week plan, post-launch, manual steps to start now |
| [docs/COMPLIANCE.md](docs/COMPLIANCE.md) | GDPR/TCF, ATT, age screen, CPC principles, DFA commitments |
| [docs/SETUP.md](docs/SETUP.md) | **Install, open, build (Android/iOS), SDKs, CI secrets, store accounts** |
| [docs/LEVELS.md](docs/LEVELS.md) | Level pipeline, difficulty targets, curation workflow |
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
