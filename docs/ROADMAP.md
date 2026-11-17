# Tilt & Drop — Roadmap

The MVP is 6 weeks of engineering, plus a 14-day Google Play closed test that overlaps weeks 5–6 if it starts on time.

## Start now (your manual steps; these have long lead times)

| Step | Why now | Lead time |
|---|---|---|
| Decide the legal entity (SZČO/živnosť or s.r.o.) and get a **D-U-N-S number** (free via Apple's lookup) | Apple organization account and trader status | 1–2 weeks |
| **Apple Developer Program** (€99/yr), organization preferred | TestFlight, App Store Connect, the Paid Apps agreement (needed for IAP), bank and tax forms | 1–14 days |
| **Google Play Console** ($25 one-time). An organization account skips the closed-test rule. A **personal** account created after Nov 2023 must run a closed test with **≥12 testers opted in for 14 continuous days** before production access | gating item for launch | 14+ days |
| Recruit 15–20 Android testers (friends, a Discord) | closed-test requirement | — |
| Unity ID + Unity Hub + Unity 6 LTS with Android and iOS modules | builds | 1 h |
| AppLovin MAX and AdMob accounts, Firebase project | SDK keys | 1 day (+ AdMob app review) |
| Register `tiltdrop.app` (or similar) and a privacy policy page | store listings require it | 1 day |
| DSA trader declaration on both stores (public address, phone, email) | required for EU distribution | with the accounts |

## Status (end of the build session, 2026-11-17)

| Area | State |
|---|---|
| Docs: GDD, TECH, ROADMAP, COMPLIANCE, SETUP, LEVELS | done |
| Python pipeline (sim, solver, canon, bot, difficulty, generator, pacing, curation, packs, golden) | done, 56 tests, 95% coverage |
| Content: 300 curated + 19,700 pool + 400 daily levels | done (levels 301+ provisional) |
| C# core + meta systems, golden parity | done, 25 tests passing under `dotnet test` |
| Unity layer: board + cascade feel, UI, flows, boosters, daily/streak/calendar/album/share, store | code complete, compile-checked against Unity reference assemblies; **needs the first Editor run + device feel review** |
| SDK adapters (MAX, AdMob <16, Firebase, IAP) | written behind defines; **needs SDK import + compile** |
| CI (python, dotnet, GameCI Android/iOS, fastlane) | workflows ready; **needs secrets** |
| Art, music, icon, store listing, localization | not started (placeholders) |

Next for you: SETUP.md §2 (open + play), §3 (Android build), then the week 3 feel review on a device.

## Week 1 — Rules engine and parity

- Docs approved (this phase).
- `/tools/levelgen`: model, `sim.py`, `naive_sim.py`, `solver.py`, `canon.py`. pytest with oracle fuzz.
- Hand-written golden vectors (every rule clause) plus 2,000 frozen random sequences.
- C# core (`Model`, `Sim`, `Solver`) plus `/core-tests` passing the golden vectors under `dotnet test`.
- CI: `python.yml`, `core-dotnet.yml`.
- **Exit criteria:** Python and C# traces byte-identical on all vectors; coverage ≥ 90%.

## Week 2 — Generator and Unity skeleton

- `bot.py`, `difficulty.py`, `generator.py`, `pacing.py`, `cluster.py`, `pack.py`. First 5,000-candidate run per world 1–6.
- (If approved) anchor + ice in both simulators, with golden vectors.
- Unity project: `ProjectSetup`, bootstrap, `BoardView` with static rendering of a pack level, swipe input, rotation, instant settle. EditMode tests via GameCI.
- Renderer perf decision (URP 2D vs Built-in).
- **Exit criteria:** a level is playable end to end in the Editor (by you); 20,000-level pool generated.

## Week 3 — Feel and level flow

- `CascadePlayer`: rotation easing, accelerated slides, squash/stretch, exit bursts, combo pitch, haptics, camera shake, speed setting, reduced motion.
- Level HUD (moves, goal), win/fail screens, continue (stubbed ad/coins), retry. Stars. Save system.
- World map (scrolling node path, 50 levels per world), progression.
- Recording of a 15-second capture for the marketing feel check.
- **Exit criteria:** feel review with you on device (Android build from your PC).

## Week 4 — Meta and content

- Coins, both boosters (Undo, Hammer), rewarded hint (on-device solver with a node cap), win streak.
- Daily puzzle, daily streak with free weekly freeze, login calendar, share grid (native share sheet).
- Star chests, sticker album v1 (deterministic schedule).
- Curation of levels 1–300 (review page, manual picks), tutorial scripts for levels 1–3 and beat intros.
- **Exit criteria:** a feature-complete loop playable offline with stub services.

## Week 5 — Services, compliance, store

- Neutral age screen → UMP consent (via MAX flow) → ATT; under-16 path (no personalized ads, no IAP, no analytics).
- AppLovin MAX + AdMob adapters, ad pacing policy, rewarded placements.
- Unity IAP: catalog (§5.4 of GDD), store UI with € prices and contents, restore purchases, piggy bank.
- Firebase Analytics, Remote Config, Crashlytics. Event schema verified in DebugView.
- CI: Android AAB and iOS Xcode builds. Upload to Play **internal testing → closed testing** (this **starts the 14-day clock** on a personal account) and to TestFlight.
- **Exit criteria:** signed builds on both stores' test tracks; consent flows pass the checklist.

## Week 6 — Polish and launch prep

- Performance (60 fps on mid-range Android, cold start < 4 s, APK/AAB < 150 MB), memory, battery.
- Localization scaffolding (EN at launch, then SK/CZ/DE/ES/PT/FR in an update).
- QA pass: device matrix, interrupted purchases, offline mode, clock changes, consent re-prompt.
- Store listings: screenshots, a 15–30 s preview video from the cascade feel, Data Safety form, Apple privacy labels, age ratings (IARC / Apple questionnaire), privacy policy, EULA.
- Soft-launch decision (recommended: a small market first, then EU + global).
- **Exit criteria:** release candidate submitted; closed test 14 days complete (if personal account) → apply for production.

## Post-launch (in rough order)

| When | Feature |
|---|---|
| +2 weeks | Difficulty tuning from analytics (Remote Config move overrides), bot recalibration, levels 301–400 |
| Update 1 (+4 w) | Sticky pairs, one-way gates, weekend event v1, localization pack |
| Update 2 (+8 w) | Bombs, paint gates, season pass (cosmetic + coins, no random rewards), cloud save (Play Games / Game Center) |
| Update 3 (+12 w) | Switch gates, portals, replay save and share as video clip, leaderboards for the daily puzzle |
| Ongoing | +50–100 levels every 2–4 weeks from the 20k pool, remote content packs, A/B tests of ad frequency and difficulty, album seasons |

## Key risks

| Risk | Mitigation |
|---|---|
| Python/C# rule drift | golden vectors gate CI on both sides |
| Solver too slow for 8×8 with many pieces | node cap + move limit bound; C# console solver fallback |
| "Feel" not good enough to drive UA | week 3 dedicated; device review gate |
| Ad/consent policy changes (AppLovin child-user policy, DFA) | services behind interfaces; COMPLIANCE.md re-check before submission |
| Personal Play account closed-test delay | start closed test in week 5 at the latest; consider an organization account |
