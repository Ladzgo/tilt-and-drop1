# Tilt & Drop — Technical Design

## 1. Principles

1. **One ruleset, two implementations, provably identical.** The Python simulator (tools) and the C# core (game) are kept in sync by shared golden vectors. CI runs both.
2. **Pure core.** All game rules, the solver, and meta logic (economy, streaks, calendar, ad pacing, stars) live in plain C# with **no `UnityEngine` references**. They're tested with `dotnet test` outside Unity.
3. **Thin Unity layer.** Views, animation, input, audio, and SDK adapters. Scenes stay minimal: one bootstrap scene, with everything else built from code plus UI Toolkit (UXML/USS are text files that can be edited without the Editor).
4. **Every SDK behind an interface** with a stub. The project compiles and runs with zero third-party SDKs installed. Real adapters are switched on by scripting define symbols.
5. **Deterministic and integer-only** simulation. No floats, no `Random` without an explicit seed.

## 2. Repository layout

```
/docs                     GDD, TECH, ROADMAP, COMPLIANCE, SETUP (Phase 3)
/shared
  /golden                 golden test vectors (JSON), produced by Python, consumed by Python + C#
  /levels                 exported level packs (*.tdp.gz) + curated JSON
/tools/levelgen           Python 3 package "tiltdrop" (simulator, solver, generator, packer)
  pyproject.toml
  tiltdrop/
    model.py              Level / State / piece types, JSON (de)serialization
    sim.py                rotate + settle (normative, §2.4 of GDD)
    naive_sim.py          one-cell-per-step oracle used only in tests
    solver.py             BFS, canonical state hash, metrics
    canon.py              D4 symmetry + color normalization for dedupe
    bot.py                noisy-greedy "human-like" bot
    difficulty.py         feature extraction + difficulty score
    generator.py          constrained random generation per world/mechanic
    pacing.py             sawtooth tier schedule, mechanic schedule
    cluster.py            feature clustering / anti-repetition
    curate.py             pick levels for each slot from candidates
    pack.py               binary pack writer/reader
    golden.py             golden vector generation + verification
    cli.py                `python -m tiltdrop <cmd>`
  tests/                  pytest
/game                     Unity 6 LTS project
  Assets/_Project/
    Core/                 TiltDrop.Core.asmdef (noEngineReferences: true)
      Model/ Sim/ Solver/ Levels/ Meta/
    Game/                 TiltDrop.Game.asmdef (Unity)
      Bootstrap/ Board/ Feel/ Input/ UI/ Save/ Services/{Interfaces,Stubs,Adapters}
    Editor/               TiltDrop.Editor.asmdef: ProjectSetup (applies PlayerSettings), BuildScript (CI)
    Tests/EditMode/       TiltDrop.Core.Tests.asmdef (NUnit, runs golden vectors)
    UI/                   *.uxml, *.uss, PanelSettings
    Art/ Audio/           placeholders
  Assets/StreamingAssets/levels/   packs copied from /shared/levels
  Packages/manifest.json
  ProjectSettings/
/core-tests               TiltDrop.Core.Tests.csproj (net8.0, NUnit) compiling Core + Tests sources by glob, for CI without Unity
/.github/workflows        python.yml, core-dotnet.yml, unity-tests.yml, build-android.yml, build-ios.yml
```

## 3. Data formats

### 3.1 Cell encoding (shared by JSON and binary)

| Char (JSON) | Byte (binary) | Meaning |
|---|---|---|
| `.` | `0x00` | empty |
| `#` | `0x10` | wall |
| `1`–`6` | `0x21`–`0x26` | colored block (low nibble = color) |
| `@` | `0x30` | stone |
| `a1`… | `0x4c` | anchor of color c (post-MVP) |
| `i1`… | `0x5c` | ice of color c, 2 HP (HP lives in state, not the level) |
| reserved | `0x6_`–`0xF_` | sticky, one-way, bomb, … |

In JSON, two-character tokens (anchor, ice) are written inside a row with brackets: `"..[a2].."`. MVP rows use one character per cell.

### 3.2 Level JSON (curated levels, golden vectors, debugging)

```json
{
  "id": "w01-l007",
  "w": 5, "h": 6,
  "grid": [
    "#...#",
    "#.1.#",
    "#.@.#",
    "#.2.#",
    "#12.#",
    "#####"
  ],
  "gates": { "T": "..2..", "R": "......", "B": ".....", "L": "...1.." },
  "moves": 6,
  "par": 4,
  "tier": "N",
  "mechanics": ["stone"],
  "meta": { "solutions_at_par": 2, "dead_end_ratio": 0.41, "bot_fail": 0.22, "difficulty": 37.5, "hash": "9f2c…" }
}
```
Gate strings are indexed by `x` for `T`/`B` and by `y` for `L`/`R`, in board coordinates. `.` means no gate.

### 3.3 Binary level pack `*.tdp` (runtime; gzip-compressed on disk)

```
Header   : magic "TDP1" | u16 version | u16 flags | u32 count | u32 indexOffset
Records  : per level
           u8  (w<<4 | h)
           u8  moves
           u8  par
           u8  tier (0=E 1=N 2=H 3=SH) | mechanics bitmask in high bits (u16 follows if flag)
           u8[w*h] cells (byte encoding above)
           u8[ceil(2(w+h)/2)] gates, nibble-packed in order T(x) R(y) B(x) L(y); 0 = none, 1..6 = color
Index    : u32 offset per level
Footer   : u32 CRC32 of everything before
```
An 8×8 level is about 86 bytes, so 20,000 levels come to ~1.7 MB raw and ~0.4 MB gzipped. Packs are split per 1,000 levels and loaded lazily. A remote-delivered pack (post-launch) can override shipped content by `contentVersion`.

### 3.4 Golden vectors `/shared/golden/*.json`

```json
{
  "schema": 1,
  "cases": [
    {
      "name": "cascade_same_gate",
      "level": { "...": "level JSON" },
      "inputs": ["CW", "CCW", "CW"],
      "expect": [
        { "gravity": "R", "grid": ["..."], "exits": [[7,"R",2],[9,"R",2]], "movesLeft": 5, "status": "playing" }
      ]
    }
  ],
  "solver": [
    { "name": "…", "level": { }, "minMoves": 4, "solutionsAtMin": 2, "deadEndRatio": 0.41 }
  ]
}
```
Hand-written cases cover every rule clause: rotation mapping, nearest-first, cascade through the same gate, wall-acting gate, stone at gate, zero-travel exit, win on last move, lose, continue. On top of that, ~2,000 randomly generated sequences are frozen by Python and replayed by C#.

### 3.5 Save file (JSON, `persistentDataPath/save.json`, atomic write via temp + rename)

`{ version, profile:{ageBand, consentState, createdUtc}, progress:{level, stars:base64 u8[]}, coins, boosters{undo,hammer}, streaks{win, daily, freezes, lastDailyDate}, calendar{day, lastClaimDate}, daily{results}, ads{sessionCount, levelsSinceInter, lastInterUtc}, purchases{removeAds, starterBought}, piggy{coins}, settings{sound,music,haptics,reducedMotion,speed} }`. It carries a version for migrations and a light HMAC to deter casual tampering. Non-consumables are always re-validated from the store (restore purchases).

## 4. C# core API (sketch)

```csharp
namespace TiltDrop.Core {
  public enum Dir : byte { Down, Right, Up, Left }      // CW = +1 mod 4
  public enum Rot : byte { CW, CCW }
  public readonly struct Piece { Kind Kind; byte Color; byte Hp; int Id; }
  public sealed class Level { int W, H; Piece[] Cells; byte[] GateT, GateR, GateB, GateL; int Moves, Par; Tier Tier; }
  public sealed class GameState {                        // mutable, cloneable
    Dir Gravity; int MovesLeft; Status Status;
    StepResult Apply(Rot r);                             // rotate + settle, returns trace
    StateHash Hash();                                    // ignores piece ids
  }
  public struct SettleEvent { EventType Type; int PieceId; int FromX, FromY, ToX, ToY; Edge Gate; int GateIndex; int Order; }
  public static class Solver { SolveResult Solve(GameState s, int maxDepth, int nodeCap); }   // used for the hint
}
```
The view never mutates rules. It only plays back `SettleEvent` traces (move paths, exits, landings) in order.

## 5. Level pipeline (Python)

1. **Simulator:** the GDD §2.4 algorithm, verbatim. `naive_sim` (one cell per step per pass, nearest-first) is the test oracle. Fuzz tests assert both give identical results on thousands of random boards.
2. **Solver:** BFS over `(cells, gravity, hp-counters)` with a hash set. Outputs: `minMoves`; `solutionsAtMin` (number of move sequences of length `minMoves` that win, counted with BFS-layer path counts); `deadEndRatio` (share of states reachable within the limit from which no win is reachable in the remaining moves, via backward induction over the layered graph); `effectiveBranching` (unique successors / 2); `stateCount`. The search is bounded by the move limit plus a node cap.
3. **Canonical hash (dedupe):** apply the 8 D4 transforms to `(grid, gates, gravity)`, re-express each so gravity points Down, normalize colors by first appearance in row-major scan, and take the lexicographic minimum, then hash it with SHA-1 (truncated to 64 bits). Near-duplicate filter: reject a candidate if its start state equals a state within 2 moves of an existing level.
4. **Human-like bot:** each move scores both rotations with a heuristic (exited blocks, blocks in matching-gate lanes, lost access, stones in front of gates) plus optional 1-ply lookahead, then softmax with temperature τ and an ε random-move rate. It runs 200 seeded attempts at the real move limit to get `botFailRate`. τ/ε get calibrated against real analytics after soft launch.
5. **Difficulty score (0–100):** a weighted sum of normalized `par`, `log(1/solutionsAtMin)`, `deadEndRatio`, `botFailRate`, colors, board area, and mechanic count. Weights live in `difficulty.toml`. The score drives tier assignment and move slack.
6. **Generator:** per-world constraints (size range, colors, gate count, stone density, allowed mechanics). It places pieces, applies gravity Down until stable (so the start is valid by construction), places gates, then rejects levels that are unsolvable, trivial (par < 2 outside the tutorial), or duplicates. It is seeded and reproducible: `seed = hash(world, slot, attempt)`.
7. **Clustering:** each level gets a feature vector (par, colors, area, stone ratio, gate topology signature, solution "shape" = sequence of exit counts). k-means bins, with a rule against the same cluster more than 2 times in a row.
8. **Curation (levels 1–300):** for each slot, a pacing tier and mechanic beat. The top-K candidates by fit go into `curation_review.html` (rendered boards) for manual pick. Choices are saved in `curated.json` and are reproducible.
9. **Export:** `python -m tiltdrop pack --in curated.json --pool pool/ --out shared/levels/` writes packs plus a manifest with content hashes.

Performance target: generating and validating 20,000 levels in < 1 h on a laptop, using `multiprocessing` and packed `bytes` states (C-speed hashing). If Python is too slow, the solver gets a C# console port that reuses the same core.

## 6. Testing strategy

| Layer | Tool | What |
|---|---|---|
| Python sim/solver/canon/pack | pytest + pytest-cov (≥ 90% line coverage on core modules) | rule clauses, oracle fuzz (`naive_sim`), solver known answers, pack round-trip, canon invariance under D4 + color permutation, generator always yields valid levels |
| Golden vectors | pytest (regenerate and diff) + NUnit | parity: Python and C# produce byte-identical traces |
| C# core | NUnit via `dotnet test` in `/core-tests` (no Unity) | golden vectors, solver parity, meta logic (economy, ad pacing, streak, calendar with injectable `IClock`), save migrations |
| Unity EditMode | Unity Test Runner (GameCI) | the same Core tests inside Unity + adapter smoke tests |
| Content | pytest over shipped packs | every level stable at start, solvable within limit, par matches metadata, no duplicate hashes |
| Manual | device test checklist | feel, performance (60 fps on a mid-range 2021 Android), consent/ATT flows, IAP sandbox |

## 7. Unity architecture

- **Bootstrap:** `[RuntimeInitializeOnLoadMethod]` creates a `GameRoot` that builds the service container (plain constructor injection, no DI framework), loads the save, runs the consent flow, and shows the first screen. The single scene `Main.unity` is empty apart from the camera.
- **Screens:** UI Toolkit (`MapScreen`, `LevelHud`, `WinScreen`, `FailScreen`, `StoreScreen`, `DailyScreen`, `SettingsScreen`, `AgeGate`). Simple state machine `ScreenRouter`.
- **Board:** `BoardView` turns the level into `BlockView` sprites (procedurally generated rounded-rect textures, with a color symbol overlay). A rotation spins the board root. Settle traces are played back by `CascadePlayer` with a small custom tween library (`Tween.cs`, no external dependency). Pooled particle systems.
- **Feel hooks:** `IAudio` (SFX by event + combo pitch), `IHaptics` (iOS `UIImpactFeedbackGenerator`, Android `VibrationEffect`, via a small native plugin or Nice Vibrations if licensed), `IReplayRecorder` (stores inputs + seed. The "save replay" hook is a no-op for now).
- **Services (interfaces → stub → real adapter, gated by a define):**

| Interface | Stub | Real | Define |
|---|---|---|---|
| `IConsentService` | always "not required" | Google UMP via AppLovin MAX Terms & Privacy flow | `TD_MAX` |
| `IAdsService` | instant fake reward | AppLovin MAX (AdMob, Unity Ads, Meta, etc. as networks) | `TD_MAX` |
| `IIapService` | fake store | Unity IAP (`com.unity.purchasing`) | `TD_IAP` |
| `IAnalytics` | logs to console | Firebase Analytics | `TD_FIREBASE` |
| `IRemoteConfig` | defaults dictionary | Firebase Remote Config | `TD_FIREBASE` |
| `ICrashReporter` | no-op | Firebase Crashlytics | `TD_FIREBASE` |
| `IClock` | system | system + server-time sanity check (post-launch) | — |

- **Ad pacing** is pure core (`AdPacingPolicy`), fed by Remote Config values and save state, and unit-tested against the GDD §6.2 rules.

### 7.1 Analytics events

`level_start {level, world, tier, attempt, moves_limit}`, `level_fail {level, attempt, moves_used, blocks_left}`, `level_win {level, attempt, moves_used, stars, continues, duration_s}`, `level_continue {level, method: ad|coins, n}`, `booster_use {type, level}`, `ad_rewarded {placement}`, `ad_interstitial {level}`, `ad_revenue` (MAX impression-level revenue data), `iap_purchase {sku}`, `daily_puzzle {n, result, moves}`, `streak {type, value}`, `consent {result, age_band}`. Under-16 and no-consent users: analytics collection disabled (see COMPLIANCE.md §3).

### 7.2 Remote Config keys (defaults ship in code)

`ad_inter_min_level=15`, `ad_inter_level_interval=3`, `ad_inter_cooldown_s=90`, `ad_inter_first_session=false`, `continue_moves=3`, `continue_cost=100`, `max_continues=2`, `move_slack_delta=0` (global ±), `tier_slack_E/N/H/SH`, `level_reward_base=10`, `piggy_rate=0.1`, `booster_price_undo=150`, `booster_price_hammer=200`, `difficulty_overrides` (JSON map level→moves), `daily_override` (JSON date→level id), `content_pack_url`.

## 8. Build and project settings (applied by `Editor/ProjectSetup.cs` so nothing has to be clicked)

Bundle ID `com.laszlo.tiltdrop` (placeholder) · portrait only · IL2CPP · ARM64 (Android, plus ARMv7 optional) · Android: AAB, minSdk 24, targetSdk = the level Google Play currently requires (verify at submission), 16 KB page-size compatible (Unity 6 default) · iOS: min iOS 15, automatic signing off in CI · Linear color space · URP 2D renderer (or Built-in, decided in week 2 by a perf test) · managed stripping Medium · Unity 6 LTS (latest 6000.x LTS patch, pinned in `ProjectVersion.txt`).

## 9. CI (GitHub Actions + GameCI)

| Workflow | Runner | Steps |
|---|---|---|
| `python.yml` | ubuntu | pytest + coverage, golden regenerate-and-diff, content validation |
| `core-dotnet.yml` | ubuntu | `dotnet test core-tests` |
| `unity-tests.yml` | ubuntu | `game-ci/unity-test-runner` (EditMode) |
| `build-android.yml` | ubuntu | `game-ci/unity-builder` with `androidExportType: androidAppBundle`, keystore from secrets → AAB artifact (optional upload to Play internal track via `r0adkll/upload-google-play`) |
| `build-ios.yml` | ubuntu → macOS | unity-builder (iOS) → Xcode project artifact. Optional macOS job: fastlane `match` + `gym` + `pilot` to TestFlight |

Required secrets (details and how to create them go in SETUP.md in Phase 3): `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` (or `UNITY_SERIAL` for Pro); `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`; `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` (optional); `APPLE_TEAM_ID`, `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_P8_BASE64`, `MATCH_GIT_URL`, `MATCH_PASSWORD` (optional, for TestFlight); `GOOGLE_SERVICES_JSON_BASE64`, `GOOGLE_SERVICE_INFO_PLIST_BASE64` (Firebase); `APPLOVIN_SDK_KEY`, `ADMOB_APP_ID_ANDROID`, `ADMOB_APP_ID_IOS`.
