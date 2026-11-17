# Tilt & Drop — Game Design Document

Version 0.1 (Phase 1 draft) · Platforms: Android + iOS (simultaneous) · Orientation: portrait · Business model: free-to-play, hybrid-casual (ads + IAP) · Developer: Slovakia (EU)

---

## 1. Pitch

You get a small board of colored blocks with colored gates around the edge. Rotate the whole board 90° and gravity takes over: blocks slide, cascade, and drop out through gates of their own color. Clear every colored block within the move limit.

Every rotation sets off a cascade, and those cascades are what we market the game with (short vertical clips of satisfying chain exits).

---

## 2. Core rules (normative)

These rules are the **single source of truth**. The Python simulator (`/tools/levelgen`) and the C# core (`/game`) implement them exactly. Golden test vectors keep the two in sync (see TECH.md §6).

### 2.1 Board

- A rectangular grid of `W × H` cells, `3 ≤ W,H ≤ 8`. Coordinates are **board coordinates**: `x` runs 0..W-1 left→right, `y` runs 0..H-1 top→bottom, both as authored with the board in its initial orientation.
- Each of the 4 edges (`T`, `R`, `B`, `L`) has one gate slot per adjacent cell (`T`/`B`: W slots, indexed by x; `L`/`R`: H slots, indexed by y). A slot is either **none** (solid edge) or a **gate of color c**.
- Gates are fixed to the board. They rotate visually with it.

### 2.2 Cell types

| Type | Moves under gravity | Can exit | Blocks others | Notes |
|---|---|---|---|---|
| Empty | — | — | no | |
| Wall | no | no | yes | fixed |
| Colored block (color 1..6) | yes | yes, through a gate of **same** color | yes | the goal: remove all |
| Stone | yes | **never** | yes | not counted for win |
| Anchor (MVP*) | no until released | after release | yes | colored block chained in place; the chain breaks the first time a block of the same color exits; from then on it is a normal colored block |
| Ice (MVP*) | no until thawed | after thaw | yes | a colored block frozen in place with 2 HP; each time a moving block (any kind) **collides** with it at the end of a slide, HP −1; at 0 it becomes a normal colored block |
| *Sticky pair* (post-MVP) | yes, as one rigid unit | each half, individually | yes | the unit stops when either half is blocked; when one half exits, the glue breaks and the other half continues as a single block in the same settle |
| *One-way gate* (post-MVP) | no | — | conditional | interior cell with an arrow: a block may pass through it only in the arrow's direction; otherwise it acts as a wall |
| *Bomb* (post-MVP) | yes | never | yes | when it comes to rest orthogonally adjacent to a stone or ice, it detonates: removes itself and all orthogonally adjacent stones and ice |

\* *Decided:* anchor and ice are **in the MVP** (levels 91 and 121). Sticky pairs, one-way gates and bombs remain drafts and get finalized (with golden vectors) before they are implemented.

### 2.3 Gravity and rotation

- State = board contents + **gravity direction** `g ∈ {Down, Right, Up, Left}` in board coordinates. Every level starts with `g = Down`.
- **Rotate CW** (the board turns 90° clockwise on screen) gives `Down → Right → Up → Left → Down`.
- **Rotate CCW** is the reverse: `Down → Left → Up → Right → Down`.
- Each rotation costs **exactly 1 move**, even when nothing moves.
- After a rotation the board **settles** (§2.4). The player cannot act until settling finishes. The view may let one input queue up or fast-forward.

### 2.4 Settling (deterministic)

```
repeat
    changed ← false
    for each movable piece p in order of increasing distance to the destination edge
        (distance = number of cells between p and the edge g points to;
         tie-break: ascending perpendicular coordinate, i.e. x for Down/Up, y for Left/Right)
        loop
            n ← cell one step from p in direction g
            if n is outside the board:
                gate ← gate slot on edge g at p's lane index
                if p is a colored block and gate.color == p.color:
                    remove p (EXIT), changed ← true
                stop                       # otherwise the edge acts as a wall
            else if n is Empty:
                move p to n, changed ← true
            else:
                stop
until not changed
```

Consequences:
- **Nearest-first:** the block closest to the destination moves first, so blocks behind it see the freed space within the same pass.
- **Cascade:** when a block exits, the blocks behind it in the same lane keep sliding and can exit through the same gate in the same settle.
- A block that is already sitting on the edge next to a matching gate **exits** as soon as gravity points into that gate (zero travel before exiting is fine).
- A gate whose color doesn't match acts as a wall. So does a gate in front of a stone.
- MVP lanes are independent (walls and gates are fixed), so one pass is enough. The `repeat` loop is there for post-MVP mechanics (pairs, bombs, thaw).
- **Pass order is fixed at the start of each pass** (a list of cell positions). Pieces only move toward smaller distances, i.e. into positions already visited, so no piece is processed twice in one pass.
- **Anchor release:** when a colored block of color *c* exits, every anchor of color *c* becomes a colored block **immediately**. If its position comes later in the current pass it moves in this pass; otherwise it moves in the next pass.
- **Ice hit:** when a piece's slide ends because the next cell is ice **and the piece moved ≥ 1 cell during this slide**, the ice loses 1 HP. At 0 HP it becomes a colored block immediately (it moves in the next pass). A piece that was already resting against the ice doesn't hit it.
- **Hammer (booster):** removes the chosen non-wall piece (no exit event, so no anchor release), then the board settles under the current gravity. It costs no move. Win is checked afterwards.
- No floating point anywhere. Pieces have stable integer IDs (used only for animation and replays; they are ignored for state hashing).

### 2.5 Win / lose

- After each settle: if **no colored blocks** remain (anchored/frozen ones count as colored), the player **wins**. The win check runs before the lose check, so clearing on the last move is a win.
- If moves left = 0 and colored blocks remain, the player **loses**. A *continue* offer appears: **+3 moves** for a rewarded ad or coins (flat price, max 2 continues per attempt), or **Retry** / **Quit**. The offer has **no countdown timer**.
- Stones never have to be removed.

### 2.6 Level validity (enforced by the generator and by a unit test over all shipped packs)

1. Initial board is **stable** under `g = Down`: settling moves nothing and nothing exits.
2. At least one colored block. Every color present has at least one gate of that color.
3. Solvable within the move limit (BFS-verified); `par` = minimum moves.
4. Move limit = `par + slack(tier)` (§7.2).

### 2.7 Stars

| Stars | Condition |
|---|---|
| ★★★ | moves used ≤ par |
| ★★ | moves used ≤ par + ⌈slack/2⌉ |
| ★ | won (including with continues) |

---

## 3. Controls and feel

- **Input:** swipe left/right anywhere on the board (or tap the ↺ ↻ buttons under it). Swipe right = CW. There's a one-handed thumb zone at the bottom.
- **Rotation:** ~0.28 s ease-out-back (≈6° overshoot), the board scales 0.97→1.0, and a soft whoosh plays.
- **Gravity:** blocks accelerate along their path (constant acceleration, capped speed). Lanes are staggered by ~15 ms, nearest-first, so the cascade reads well.
- **Landing:** squash and stretch (x1.12/y0.88, 80 ms), a dust puff, a pitch-varied thock sound, and a light haptic.
- **Exit:** the gate flashes its color, the block whooshes through and shrinks, and particles burst. Consecutive exits in one settle climb a pentatonic scale (combo). 4+ exits trigger a camera micro-shake and a medium haptic.
- **Win:** leftover moves turn into coins one by one with ticks, then the star reveal plays.
- **Accessibility:** every color has a symbol (●▲■◆★✚), matched on its gate. Colorblind-safe palette. Reduced-motion and haptics toggles. Speed setting (1×/1.5×/2×).
- **Undo** is a booster, not free (§5.3).

---

## 4. Loops

### 4.1 Core loop (30–120 s)

Map → Level start (move limit and goal shown) → rotate / cascade / clear → Win (stars, coins, streak +1) or Fail (continue offer or retry) → back to the map, possibly with an interstitial (§6.2).

### 4.2 Meta loop (days to weeks)

- **Worlds of 50 levels.** Each world has its own theme (palette, background, gate style, music stem). World map with a node path. There's a mini-celebration every 10 levels and a world finale at level 50.
- **Stars** unlock world chests at 50 / 100 / 150 stars per world. A chest's contents are **fixed and shown in advance** (coins, boosters, stickers).
- **Sticker album:** one page per world with 12 stickers. Stickers come **only from gameplay**, on a deterministic, published schedule (level milestones, star chests, daily-puzzle milestones, weekend events). They can **never** be bought with coins or money, and there are no random packs. A completed page grants a profile frame and coins.
- **Win streak:** consecutive first-try wins (no continue) fill a 3-tier streak (2/4/6 wins) that grants a free pre-placed booster at level start. A fail resets the streak. Players can't pay to protect it, and the loss is shown gently ("Streak ended — start a new one!").

### 4.3 Daily loop

- **Daily puzzle:** one hand-picked level per calendar day, the same for everyone (local date, Wordle-style numbering `#N` = days since launch). There's one scored attempt with unlimited practice retries, boosters are disabled, and the result is a share grid (§4.4).
- **Daily streak:** consecutive days with a solved daily puzzle. Players get **one free streak freeze per week**, applied automatically. Extra freezes are earned (calendar day 7, milestones), never sold.
- **Login calendar:** a fixed 7-day cycle with rewards visible in advance (D1 50 coins … D7 booster + 300 coins + freeze). Progress only moves forward: missing a day pauses the calendar and never resets it. There's no wheel, no random reward, and no ad-boosted multiplier on the calendar itself.
- **Weekend event (Fri 00:00 – Sun 23:59 local):** a themed set of 10 special levels with a fixed reward track (coins, boosters, an event sticker). The end is shown as a plain date ("Ends Sunday"); no seconds-ticking countdown, no "hurry" messages. Entry is free.

### 4.4 Share grid (no spoilers)

```
Tilt & Drop #123  ★★★
5/7 moves
⬛🟨🟧⬛🟥
tiltdrop.app/d/123
```
There's one emoji per move: ⬛ = no exit, 🟨 = 1 block exited, 🟧 = 2–3, 🟥 = 4+ (cascade). Rotation directions are deliberately left out, so the grid doesn't give away the solution.

---

## 5. Economy (single soft currency: **coins**)

### 5.1 Sources

| Source | Amount (initial, remote-tunable) |
|---|---|
| Level win | 10 + 5 per star + 2 per leftover move (cap 20) |
| Rewarded "double coins" after a win | ×2 of the level reward |
| Daily puzzle solved | 30 |
| Login calendar | 50–300 |
| Star chests / album pages | fixed, shown in advance |
| IAP coin packs / piggy bank | §5.4 |

### 5.2 Sinks

| Sink | Price |
|---|---|
| Continue (+3 moves) | 100 coins, flat, max 2 per attempt |
| Booster: **Undo** (reverts the last rotation and refunds the move) | 150 |
| Booster: **Hammer** (removes one block or stone of choice; a colored block removed this way counts as cleared) | 200 |

Hints (the next optimal rotation, from the solver) are **rewarded-ad only**.

### 5.3 Boosters (2)

- **Undo:** restores the exact previous state, including move count.
- **Hammer:** tap a target to remove it, then the board re-settles under the current gravity. The removal itself isn't a move.
- Daily free booster: 1 per day via rewarded ad.

### 5.4 Store (all prices set in **€** on the stores; other currencies localized by Apple/Google)

| Product | Type | Price | Contents (draft) |
|---|---|---|---|
| Remove Ads | non-consumable | €4.99 | no interstitials, forever. Rewarded ads stay optional and still pay out |
| Starter Pack | consumable, once | €1.99 | 1,000 coins + 2 Undo + 2 Hammer |
| No Ads + Bundle | non-consumable + consumables | €7.99 | Remove Ads + 1,500 coins + 3 + 3 boosters |
| Coins S | consumable | €1.99 | 1,000 |
| Coins M | consumable | €4.99 | 2,800 |
| Coins L | consumable | €9.99 | 6,000 |
| Coins XL | consumable | €19.99 | 13,000 |
| Coins XXL | consumable | €49.99 | 35,000 |
| Piggy Bank | consumable | €3.99 | breaks open the coins it has collected (it fills with 10% of level-win coins, cap 3,000, unlock at 1,000) |
| *Season Pass* | post-launch | — | cosmetic and coin track; no random rewards |

Every pack shows its exact contents and € price. When something costs coins, the UI also shows the approximate € value (based on the Coins S rate) per the CPC principles (COMPLIANCE.md §4). **Under-16 users see no paid purchases at all.**

---

## 6. Ads

### 6.1 Rewarded (always player-initiated and optional)

+3 moves (continue), double coins, daily free booster, hint.

### 6.2 Interstitials (rules, all values in Remote Config)

- Never in the first session, and never before level 15 (`ad_inter_min_level`).
- Only **after a win**, on the way back to the map. **Never right after a fail.**
- At most every 2–3 levels (`ad_inter_level_interval`, default 3) and at least 90 s apart (`ad_inter_cooldown_s`).
- Never before or after the daily puzzle or an IAP screen. Never for Remove-Ads owners.
- No banners at launch (protects the board and the feel). This may be revisited behind Remote Config.

---

## 7. Content and difficulty

### 7.1 Level structure

- Worlds of 50 levels. Launch with **300 curated levels (6 worlds)**, then add ~50–100 levels every 2–4 weeks from the generated pool (20,000+ validated levels).
- Boards from 4×4 (tutorial) up to 8×8. The view reserves a square safe area so rotation never needs a rescale.

### 7.2 Sawtooth pacing

Tiers and move slack: **Easy** +4, **Normal** +2…3, **Hard** +1, **Super-hard** +0…1.

- A hard level every 5–8 levels. A super-hard level every 20 (levels 20, 40, 60, …).
- 2–3 easy levels after each spike.
- The first level of a new mechanic is a tutorial (easy, scripted hand hint). The next ~5 levels ramp up.
- Target fail rates (bot estimate first, then analytics): Easy < 10%, Normal 15–30%, Hard 40–60%, Super-hard 60–80% on the first attempt.

### 7.3 Mechanic schedule (a new beat roughly every 30 levels up to 500)

| Level | Beat | Ships |
|---|---|---|
| 1 | 1 color, 1 gate, swipe tutorial | MVP |
| 11 | 2 colors | MVP |
| 31 | Stone | MVP |
| 61 | Multiple gates per color, gate pockets | MVP |
| 91 | Anchor | MVP |
| 121 | Ice | MVP |
| 151 | 4 colors + 7×7 | MVP |
| 181 | Stone mazes (mixing) | MVP |
| 211 | 8×8 boards | MVP |
| 241 | 5 colors | MVP |
| 271 | Mixed review world finale | MVP |
| 301 | Sticky pairs | Update 1 |
| 331 | One-way gates | Update 1 |
| 361 | Bombs | Update 2 |
| 391 | Paint gates (recolor a passing block) | Update 2 |
| 421 | Switch gates (gate color alternates every move) | Update 3 |
| 451 | Portals | Update 3 |
| 481 | 6 colors + combos | Update 3 |
| 500+ | Generated pool, mixing all mechanics, repeating sawtooth | ongoing |


---

## 8. Session flow and first-time user experience (FTUE)

1. Splash → **neutral age screen** (year of birth) → consent (UMP/TCF) → ATT (iOS, only if 16+ and consent allows) → straight into level 1 (no menu first).
2. Levels 1–3 are scripted hand hints. The map is revealed after level 3. Coins after level 5. Boosters are introduced at levels 8 and 12 (with free first uses).
3. Interstitials are off until level 15 and through the whole first session.

## 9. Out of scope for the MVP

PvP, guilds, chat, user-generated levels, real-money or real-world prize contests (never), loot boxes or any paid random rewards (never), energy/lives systems (none: unlimited play).
