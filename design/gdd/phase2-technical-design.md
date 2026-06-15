# Phase 2 — Technical Design Document (Levels 51–200 + Procedural 201+)

> **Status:** Design only. No code, scene, prefab, or data file is modified by this document.
> Levels 1–50 in `Assets/Resources/levels.json` are frozen and MUST NOT be touched.
> **Engine:** Unity 6.3 LTS · URP 2D · C# · Mobile (tap-only).
> **Author intent:** Implementation-ready. A programmer should be able to build each system
> from this document without further clarification.

---

## 0. Current Implementation Baseline (read first)

These are the load-bearing facts the design builds on. Field/identifier names below match
the shipping code exactly so the design integrates without renaming.

| Subsystem | File | Key facts |
|---|---|---|
| Level schema | `LevelData/LevelRecord.cs` | Newtonsoft.Json, `[JsonObject(OptIn)]`, snake_case keys. Fields: `level_id, display_name, difficulty_tier, schema_version, color_count, stack_depth, color_stacks (int[][]), temp_slot_count, temp_slot_depth, is_tutorial, daily_challenge_eligible, hint_override (int?), added_version, par_moves`. |
| Catalogue file | `Assets/Resources/levels.json` | `{ "catalogue_version": 2, "levels": [ … ] }`. 50 entries. |
| Move rules | `SortMechanic.cs` → `IsLegalMove(held, dest, cap)` | Empty dest = legal; full = reject (`DestinationFull`); top==held = legal; else `ColorMismatch`. Order: empty → full → color. |
| Win rule | `SortMechanic.IsWon()` | Every column is EMPTY **or** (full to its capacity AND monochrome). Temp slots included. |
| Column model | `SortMechanic` | Flat index: `0..colorCount-1` = color stacks, `colorCount..colorCount+tempSlotCount-1` = temp slots. Bottom = index 0, top = last. |
| Column cap | `SortMechanic.MaxColumnCount = 8` (ADR-0013) | `color_count + temp_slot_count ≤ 8`. **This is the single biggest constraint on multi-row layouts — see §1.5.** |
| Layout | `GameplayBoardLayout.cs` | Already row-aware: `RowsForColumnCount`: ≤6→1, 7–10→2, 11–14→3, 15+→4. Computes uniform bolt diameter so bolts stay round. `Compute()` already accepts mixed counts. |
| Tube sprites | `GameAssets.TubeSprite(capacity, selected)` | Already maps capacity→sprite: ≤3 short, 4 normal, 5 large, 6 extra_large, 7+ XXL. |
| Ball sprites | `GameAssets.BallSprite(colorId)` | 1-based, 11 named colors, wraps modulo. |
| Solver | `Editor/LevelSolver.cs` | BFS, optimal min-move. Per-column `caps[]` array **already supports asymmetric/variable capacity**. Symmetry-reduced canonical key (sorts `"cap:contents"` strings). `DefaultStateLimit = 1_500_000`. Prunes finished tubes + interchangeable empties. |
| Equivalence | `Editor/LevelEquivalence.cs` | `CanonicalSignature` invariant under color relabel + tube reorder. O(colorCount!). Folds `stack_depth.temp_slot_count.temp_slot_depth.colorCount` into the prefix. |
| Level select | `Gameplay/LevelSelectController.cs` | `MaxLevels = 50`, `Columns = 3`, single vertical scroll, procedurally built per `Start()`. Reads completion from `SaveSystem`. |
| Save model | `SaveSystem/SaveData.cs` | `level_progress.current_level_id`, `completion_record[] {level_id, best_stars(1-3), completion_version}`. JsonUtility, schema_version=1. |
| Runtime nav state | `LevelSelectController.LoadLevel` | Uses `PlayerPrefs.SetInt("bs.next_level", id)` — **PlayerPrefs is the established channel for ephemeral UI nav state; reuse it, do not extend the save schema for page state.** |

**Design principle that follows from the baseline:** the solver and layout engine are already
data-driven over per-column capacity. Mechanics 4, 5, 6 are therefore mostly *schema + UI* work,
not *algorithm* work. Mechanics 1, 2, 3 require genuine new rule/solver logic.

---

## SECTION 1 — NEW MECHANICS DESIGN

### Schema versioning umbrella

All Phase-2 mechanics raise `schema_version` from `1` to **`2`** on any level that uses them.
Levels 1–50 stay at `schema_version: 1` and are read by the unchanged v1 path. The loader
branches on `schema_version`; v1 records never see any new field. `catalogue_version` bumps to `3`
when the first v2 level ships. New fields are **all optional** — absent ⇒ mechanic not present ⇒
identical behaviour to v1.

Reserved `color_id` domain for v2 (the single source of truth for every mechanic below):

| Value | Meaning | Renders as |
|---|---|---|
| `1 … color_count` | Normal color | `BallSprite(id)` |
| `-(1 … color_count)` | **Mystery ball**, hidden color = `abs(value)`, not yet revealed | `Mystery_ball` sprite |
| `0` | **Multicolor wildcard** | `bal_multicolor` sprite |

This keeps `color_stacks` a single `int[][]` (no parallel arrays for per-ball mechanics), preserves
the bolt-count invariant (`sum(lengths) == color_count * stack_depth`), and means the existing
`OnDeserialized`/IL2CPP AOT path is unchanged. Only the **validator domain** widens (see each
mechanic's Solver/Validation note).

---

### MECHANIC 1 — MYSTERY BALL

**A) Visual representation**
- Sprite (covered state): `assets_admin/Sprites_objets/New/Balls/Mistery_ball.png` → import to
  `Resources/Sprites/Balls/ball_mystery.png`. New accessor `GameAssets.BallMystery`.
- Covered ball renders the mystery sprite regardless of its hidden color.
- **Reveal animation** (fires the instant the ball above it leaves, i.e. it becomes the column top):
  1. 0.00s: mystery sprite at scale 1.0.
  2. 0.00–0.12s: scale punch to 1.18 (`EaseOutBack`) + white flash overlay alpha 0→0.6→0.
  3. 0.06s: cross-fade swap mystery sprite → `BallSprite(hiddenColor)` over 0.10s.
  4. 0.06–0.40s: one-shot particle burst (12–16 sparks, tinted to the revealed color, radial,
     gravity 0). Reuse the existing win-FX particle pool style; new emitter config only.
  5. 0.18s: settle scale → 1.0 (`EaseInOutQuad`). Play SFX `mystery_reveal` (new key).
- Reveal is **permanent**: once positive, the ball never reverts even if covered again.

**B) Data model (`levels.json`)**
- Encoded inline in `color_stacks` as a **negative** value: `-3` = mystery ball hiding color 3.
- Example: `"color_stacks": [[1,-2,2],[2,1,1], …]` — slot 1 of stack 0 is a mystery hiding color 2.
- No separate field. Reveal state is runtime-only (negative→positive flip in GSM board state),
  never persisted (a level always loads with its authored mystery balls covered).

**C) Gameplay rules**
- A mystery ball is **revealed** the moment it becomes the top of its column (nothing above it).
  On reveal, GSM flips the cell `-c → +c` and BoardView plays the reveal animation.
- **Can it be moved before revealed?** No — and the rule is self-enforcing: a covered mystery ball
  is by definition never the column top, and only the top is selectable. No extra guard needed in
  `DispatchIdleIndexedTap` (it reads `column[Count-1]`, which is always positive once selectable).
- **Two mystery balls stacked?** Allowed in data. Revealing the upper one (when it becomes top)
  does not reveal the lower one; the lower reveals only when the upper is moved away. Authoring
  rule: legal but capped at **2 mystery balls per level** to limit memory burden (§1F).
- **Mystery as the only ball in a tube?** Then it is already the top at load → it reveals
  immediately on level load (reveal animation plays during the entrance settle). Authoring rule:
  disallow a mystery ball at the top of its starting stack (slot index == top) — it carries no
  hidden-information value. Validator warns.

**D) Solver impact** — **None to the algorithm.** The solver has full information: it reads
`abs(value)` for every cell and treats a mystery ball as its true color. Reveal is a *player-facing
information event*, not a board-state change, so it does not branch the search. Implementation: a
one-line normalization `int color = Mathf.Abs(raw);` when loading `ColorStacks` into the solver's
initial state. Min-move count and solvability are computed on the fully-known board — exactly what
we want, because the puzzle is genuinely solvable for a player who has deduced/revealed colors.

**E) Reveal / introduction range:** first appears **level 161**. Tutorial at 161 (see §6).

**F) Frequency rules:**
- Levels 161–200: at most **1 mystery ball per level**, appearing on average once per **10 levels**
  (≈4 levels in 161–200 carry it).
- Procedural 201+: ≤2 per level, probability scales with difficulty (§4).
- Never in levels 1–160.

**G) Combination rules:** see matrix §2. Summary: + large/asymmetric tubes OK from 161;
+ frozen tube from 181; + multicolor from 191. Never two *different* exotic mechanics before 181.

**H) Tutorial prerequisite:** player must already understand base sorting + temp slots (levels 1–50)
and large/multi-row boards (81+). No mechanic-specific prerequisite beyond the 161 tutorial itself.

---

### MECHANIC 2 — MULTICOLOR BALL (wildcard)

**A) Visual representation**
- Sprite: `assets_admin/Sprites_objets/New/Balls/bal_multicolor.png` → `Resources/Sprites/Balls/ball_multicolor.png`. Accessor `GameAssets.BallMulticolor`.
- **Future animation spec (documented, NOT implemented now):**
  - *Pulsing glow:* additive halo sprite behind the ball, alpha 0.3↔0.7, period 1.4s, cosine.
  - *Slow color cycling:* HSV hue sweep 0→360° over 6s applied as a tint to a greyscale variant,
    OR a 6-frame rainbow flipbook at 8 fps. Pick flipbook for URP-2D batching friendliness.
  - Gated behind a quality-tier check (off on low tier) when implemented. For now the static
    sprite is final art.

**B) Data model (`levels.json`)**
- Encoded inline in `color_stacks` as the reserved value **`0`**. Example: `[[1,0,2], …]`.
- Optional sibling sanity field for authoring tools (not required by runtime):
  `"wildcard_count": 1`. Loader ignores it; the equivalence/validator tools read it to enforce
  "max 1". **Hard rule: at most one `0` across the entire `color_stacks` of a level.**

**C) Gameplay rules**
- Placing the wildcard onto any column: always legal (empty, or any non-full top).
- Placing any ball onto a column whose top is a wildcard: always legal (not full).
- Win/match: a column counts as complete if it is full and **every ball is either a single color C
  or the wildcard** — i.e. ≤1 wildcard does not break monochrome. Because max 1 wildcard exists,
  this reduces to: full column is complete iff all non-wildcard balls share one color.
- The wildcard is itself movable like any ball when it is the top.

**D) Solver impact** — wildcard match logic, small and contained:
- `IsLegalMove`: `held == 0` ⇒ legal onto any non-full column. `top == 0` ⇒ legal for any held
  onto non-full.
- `IsWon`: a column is "complete" if `Length==cap` and the set of non-zero colors in it has size ≤1.
- `IsMono` prune in move-gen: treat a full column as "finished" only if its non-wildcard colors are
  uniform (then never used as source).
- State space: the wildcard is one extra distinguishable token; with max 1 per level the branching
  factor barely moves. Budget (1.5M) holds comfortably. Canonical key already serializes raw ints,
  so `0` participates naturally — no key change.

**E) Introduction range:** first appears **level 171** (after mystery is established). Tutorial at 171.

**F) Frequency rules:**
- **Maximum 1 multicolor ball per level**, ever.
- Levels 171–200: average once per **20 levels** (≈1–2 levels in range). Reserved as a difficulty
  *relief valve* — only placed on a level whose unmitigated difficulty score would exceed the band
  ceiling (§3). Never used to pad easy levels.
- Never in the first 100 levels (in practice never before 171 here).
- Procedural 201+: rare, only when the generator's difficulty estimate overshoots the target band by
  >1 tier (§4 fallback), still capped at 1.

**G) Combination rules:** + mystery from 191. **Multicolor + frozen tube = NEVER** (an
easier-mechanic and a harder-mechanic together produce confusing, hard-to-read difficulty — banned
in §2). + large/asymmetric tubes OK from 171.

**H) Tutorial prerequisite:** base game. The 171 tutorial teaches it directly.

---

### MECHANIC 3 — FROZEN TUBE

**A) Visual representation**
- Composited on top of the existing tube sprite (no new tube art):
  - **Frozen** (`counter ≥ 2`): blue tint multiply (≈`#7FB0FF` at 0.55) over the tube body +
    snowflake icon centered on the rim + bold counter number on/below the snowflake.
  - **Thawing** (`counter == 1`): same, snowflake + "1" pulsing scale 1.0↔1.12 at 0.8s, tint
    lightened to ≈0.35.
  - **Unfrozen** (`counter == 0`): tint and snowflake fade out over 0.25s; tube returns to normal.
- Snowflake + counter are **shown from move 0** (level load), so the player is never surprised.
- New sprite: `Resources/Sprites/Tubes/snowflake.png`. New accessor `GameAssets.IconSnowflake`.

**B) Data model (`levels.json`)** — new optional field:
```json
"frozen_tubes": [ { "tube_index": 4, "freeze_turns": 3 } ]
```
- `tube_index` is the **flat column index** (color stacks first, then temp slots).
- `freeze_turns` = number of moves the tube stays deposit-locked.
- Absent ⇒ no frozen tubes. **Max 1 entry per level** (authoring rule §3/§F).

**C) Gameplay rules**
- While `freeze_remaining > 0`: the tube **rejects all deposits** (new reject reason
  `DestinationFrozen`); removals from it are always allowed.
- **Turn-counting rule (chosen for clearest UX):** the counter decrements by 1 on **every committed
  move anywhere on the board** (`OnMoveCommitted`), not only moves touching that tube. Rationale:
  the displayed number is a literal "moves remaining" countdown the player can plan against; a
  tube-local rule would make the number unpredictable. Counter never goes below 0 and never
  re-freezes.
- The freeze counter is **not** decremented by cancelled or invalid moves (only committed moves).
- Undo: undoing a committed move increments the counter back by 1 (freeze state is part of the GSM
  snapshot / undo entry — see §7 state note).

**D) Solver impact** — the only mechanic that adds a real state dimension. See §7 for the full
state-encoding and budget analysis. Summary: the solver tracks `freezeRemaining[tube]`, forbids
deposits where `>0`, and decrements all active counters by 1 on every applied move. Because counters
are monotonically non-increasing and tiny (`≤ ~12`), and there is ≤1 frozen tube, the canonical key
grows by at most one small integer until thaw, after which it collapses to the standard key.
Solvability requires a win reachable **within the freeze constraints**; par is computed on the
constrained search.

**E) Introduction range:** first appears **level 121**. Tutorial at 121.

**F) Frequency rules:**
- **Max 1 frozen tube per level.**
- Levels 121–160: at most once per **15 levels** (≈2–3 levels in range). Every level that carries a
  frozen tube is followed by a "breather" level (lower difficulty score, no exotic mechanic) per §3.
- Never before level 121.
- Procedural 201+: scales per §4; `freeze_turns` grows with difficulty.

**G) Combination rules:** + asymmetric/large tubes OK from 121. **+ multicolor = NEVER.**
+ mystery from 181 (matrix §2).

**H) Tutorial prerequisite:** player must understand temp slots and multi-row boards (≤120). The 121
tutorial teaches the freeze countdown explicitly.

---

### MECHANIC 4 — LARGE CAPACITY TUBES (6, 7, 8, 9 balls)

**A) Visual** — sprites already wired in `GameAssets.TubeSprite(capacity, selected)`:
`3→short, 4→normal, 5→large, 6→extra_large, 7+→XXL`. Capacities 8 and 9 reuse XXL (largest art).
Confirm `Tube_unselected_XXL.png` / `Tube_unselected_extra_large.png` are imported to
`Resources/Sprites/Tubes/`.

**B) Data model** — capacity per tube is **not currently representable per-column**; the v1 schema
has one `stack_depth` (all color stacks) and one `temp_slot_depth`. For uniform large tubes, simply
raise `stack_depth` (e.g. `6`). For *mixed* sizes use the asymmetric field in §6
(`tube_capacities`). The bolt-count invariant generalizes to
`sum(color_stacks[i].Length) == sum(capacities of color stacks)`.

**C) Layout / scaling rules** — `GameplayBoardLayout.Compute()` already derives a single uniform
bolt diameter from the *deepest* tube (`maxDepth = max(stackDepth, tempSlotDepth)`) and the widest
row, then bottom-aligns all tubes to a shared baseline. Deeper tubes therefore automatically shrink
the bolt diameter so the tallest tube still fits the play band. No new layout code for depth; verify
that at depth 9 with the minimum band the `MinBoltDiameter` clamp (0.05) is not hit on a 9:16 phone
(it is not for ≤9 within the column budget — confirm in a layout smoke test).

**D) Max balls per tube per range:**
| Range | Max stack_depth |
|---|---|
| 51–80 | 5 |
| 81–120 | 6 |
| 121–160 | 7 |
| 161–200 | 8 (9 only on procedural 201+ spikes) |

**E) Asymmetric handling** — see §6.

**F) Solver impact** — none beyond what exists. `caps[]` is already per-column; `IsWon` already
checks `col.Length == caps[i]` per column. Edge cases to **confirm with a test**, not change:
deeper tubes inflate the per-color permutation count → keep within the 8-column cap so the symmetry
reduction stays effective. A depth-9 / 8-color board can approach the 1.5M budget — flag to authoring
that depth×colors is the cost driver (see §7 budget table).

---

### MECHANIC 5 — MULTI-ROW LAYOUTS (3 and 4 rows)

**A) Layout rules** — already implemented in `GameplayBoardLayout.RowsForColumnCount`
(≤6→1, 7–10→2, 11–14→3, 15+→4) and `DistributeColumns` (even split, earlier rows take remainder so
the last row is equal-or-shorter — keeps the trailing empty/temp tubes on a clean final row). Rows are
vertically centered in the play band with `RowGap = 0.5` world units between them.

**B) Minimum tube width / spacing for mobile** — uniform diameter is width-constrained by the widest
row: `diaW = (availW / maxColsInRow) * 0.82 - 0.12`. On a 720-wide reference at 5 tubes/row this
yields a comfortably tappable tube (>48dp). Authoring constraint: **max 5 tubes per row** for
readability on the iPhone-SE width, even though the math allows more.

**C) Max tubes per row per layout:**
| Rows | Tubes/row (recommended max) | Total tubes |
|---|---|---|
| 1 | 6 | ≤6 |
| 2 | 5 | 7–10 |
| 3 | 5 | 11–14 |
| 4 | 5 | 15–18 |

**D) Camera/scroll** — board fits a single static screen (no scroll) for ≤18 tubes by design; the
layout shrinks bolts to fit. **No scrolling play area** — that would break tap resolution via
`Physics2D.OverlapPoint`. If a level ever needs >18 tubes (not planned for 51–200), it is rejected at
authoring.

**E) Introduction range:** 3-row layouts from **level 81**. 4-row reserved for 161+ / procedural.

**⚠ BLOCKER — column cap conflict (must resolve before any multi-row level ships):**
`SortMechanic.MaxColumnCount = 8` (ADR-0013, Assertion 4) hard-rejects any board with
`color_count + temp_slot_count > 8` as a corrupt-board load failure. Multi-row layouts only make
sense above 8 columns. **Required change (documented, not applied here):**
raise `MaxColumnCount` to **18** and revise/supersede ADR-0013. The original 8-column rationale
(44pt/48dp tap targets on a single 375pt row) is *resolved by the multi-row layout itself* — multiple
rows restore tap-target size at high tube counts. A new ADR should record: "column cap raised to 18;
tap-target compliance is now guaranteed by `GameplayBoardLayout` row distribution + `MinBoltDiameter`
floor, not by a flat column count." Until that ADR + the constant change land, every 81+ level will
fail `AssertColumnCapValid`. This is the gating dependency for §3 ranges 81+.

---

### MECHANIC 6 — ASYMMETRIC TUBE SIZES

**A) Concept** — a single level with tubes of differing capacities (e.g. three depth-4 + one depth-8).

**B) Data model** — new optional field, length = `color_count + temp_slot_count` (flat order):
```json
"tube_capacities": [4,4,4,8, 4,4]   // 4 color stacks (last is the depth-8), then 2 temp slots
```
- When present, it **overrides** `stack_depth`/`temp_slot_depth` per column. `stack_depth` and
  `temp_slot_depth` remain present as the *fallback / dominant* value (used by anything that hasn't
  read the array, and for the equivalence prefix).
- Invariant: `sum(color_stacks[i].Length) == sum(tube_capacities[0..color_count-1])`.
- Win is unchanged: each non-empty tube must be full **to its own capacity** and monochrome — exactly
  what `IsWon` (runtime) and the solver already do via per-column caps.

**C) Visual balance rules** — `TubeSprite(capacity,…)` already picks the right body per tube, so a
depth-8 tube renders as XXL beside depth-4 normals. Because the layout uses one *uniform bolt
diameter* across the board (driven by the deepest tube), the depth-4 tubes look "short and fat" only
if their body sprite is stretched. **Rule:** keep tube body sprites at native aspect; bottom-align all
tubes to the shared baseline (`slot0Y`) so the tops differ but the bases line up — visually reads as a
deliberate "tower" rather than broken. Add ≥1 empty slot of headroom above the tallest stack.

**D) Difficulty implications / ranges** — asymmetry concentrates flexibility into the big tube,
usually *raising* difficulty (more permutations) while looking inviting. Introduce from **level 121**
alongside frozen tubes. Recommended pattern: one oversized "overflow" tube + several small tubes.

**E) Solver impact** — none structurally; the solver's `caps[]` already supports it. The loader must
populate `caps[i]` from `tube_capacities` when present instead of the flat depths. The canonical key
already prefixes each column with `caps[i]:` so unequal capacities are never wrongly merged. Confirm
the equivalence prefix is extended to include the capacity vector when `tube_capacities` is set
(otherwise two structurally different asymmetric levels could collide) — see §2 note.

---

## SECTION 2 — MECHANIC COMBINATION MATRIX

Legend: ✅ allowed (from level / restriction) · 🚫 never · — n/a (self).
"Max" = per-level cap regardless of combination.

|  | Large cap | Multi-row | Asymmetric | Frozen tube | Mystery ball | Multicolor |
|---|---|---|---|---|---|---|
| **Large cap** (≤9) | — | ✅ 81+ | ✅ 121+ | ✅ 121+ | ✅ 161+ | ✅ 171+ |
| **Multi-row** (≤4 rows) | ✅ 81+ | — | ✅ 121+ | ✅ 121+ | ✅ 161+ | ✅ 171+ |
| **Asymmetric** | ✅ 121+ | ✅ 121+ | — | ✅ 121+ | ✅ 161+ | ✅ 171+ |
| **Frozen tube** (max 1) | ✅ 121+ | ✅ 121+ | ✅ 121+ | — | ✅ **181+** | 🚫 **NEVER** |
| **Mystery ball** (max 2) | ✅ 161+ | ✅ 161+ | ✅ 161+ | ✅ **181+** | — (≤2) | ✅ **191+** |
| **Multicolor** (max 1) | ✅ 171+ | ✅ 171+ | ✅ 171+ | 🚫 **NEVER** | ✅ **191+** | — |

**Rules expressed by the matrix:**
1. Layout/capacity mechanics (large, multi-row, asymmetric) are "free" structural variations and
   combine with anything from their own intro level.
2. **No two *exotic* rule mechanics (frozen / mystery / multicolor) coexist before level 181.**
   181+ permits frozen+mystery; 191+ permits mystery+multicolor.
3. **Multicolor never combines with frozen** at any level — pairing a difficulty-reducer with a
   difficulty-raiser on the same board produces unreadable difficulty and contradictory tutorial
   messaging.
4. Triple exotic combinations (frozen+mystery+multicolor) are 🚫 in 51–200; reserved for procedural
   201+ at very high difficulty only, and still respecting rule 3 (so effectively never with
   multicolor).
5. Per-level hard caps always apply: ≤1 frozen, ≤2 mystery, ≤1 multicolor, ≤18 columns, depth ≤9.

**Equivalence-tool note:** when `tube_capacities` or any exotic field is present, extend
`LevelEquivalence.CanonicalSignature`'s shape prefix to include the capacity vector and a mechanic
fingerprint (`frozen@idx:turns`, `mystery@positions`, `wild@1`) so two levels that differ only by a
frozen tube are not flagged as duplicates and two genuinely identical exotic levels still are.

---

## SECTION 3 — LEVEL PROGRESSION PLAN (51–200)

Difficulty score `D` is the planning target (not yet a formula in code). Define it for authoring as:

```
D = solver.MinMoves
  + 2.0 * color_count
  + 1.5 * (avg_stack_depth - 3)
  + 3.0 * frozen_tubes
  + 2.0 * mystery_balls
  - 4.0 * multicolor_balls          // relief valve lowers D
  + 1.0 * extra_rows                 // (rowCount - 1)
```
`MinMoves` comes from `LevelSolver.Solve`. Author to the target band; if `D` overshoots the band
ceiling, either simplify or (171+) spend the one allowed multicolor as relief. A breather level after
any spike sits ≥3 points below its predecessor.

| Range | Colors | Tubes (color+temp) | Capacities | Mechanics active | Par range | D target |
|---|---|---|---|---|---|---|
| **51–80** Traditional+ | 3→5 | 5→7 (so ≤2 temp) | depth 4→5 uniform | none | 8–22 | 10–22 |
| **81–120** Space opens | 4→6 | 7→11 (2–3 rows) | depth 4→6 uniform | multi-row, large cap | 16–34 | 20–38 |
| **121–160** Pressure | 4→6 | 8→14 (2–3 rows) | depth 5→7, asymmetric allowed | + asymmetric, + frozen (≤1, ≤1/15 lvls) | 22–46 | 28–52 |
| **161–200** Hidden info | 5→7 | 10→16 (2–4 rows) | depth 6→8, asymmetric | + mystery(161, ≤2, ≤1/10), + multicolor(171, ≤1, ≤1/20), combos per §2 | 30–60 | 36–66 |

Per-range notes:
- **51–80:** pure skill ramp. Stay ≤6 columns so no ADR-0013 change is needed here — these can ship
  *before* the column-cap fix. Tighten par toward `MinMoves` (3-star = par; see §0 win/par).
- **81–120:** first range that needs the **MaxColumnCount→18 change** (>8 tubes). Gate accordingly.
- **121–160:** frozen tube cadence = at most one every 15 levels, each followed by a breather.
  Asymmetric tubes introduce the "overflow tube" pattern.
- **161–200:** mystery cadence ≤1/10, multicolor ≤1/20, both also breather-followed. Combinations only
  per the matrix (181 frozen+mystery, 191 mystery+multicolor).
- Every range: each level must pass `LevelSolver.Solve` (solvable, within budget) **and**
  `LevelEquivalence` (no duplicate of any 1–200 level). Par ≥ `MinMoves`; 3-star = `MinMoves` (tune in
  level-progression GDD if a buffer is wanted).

---

## SECTION 4 — PROCEDURAL GENERATION DESIGN (Level 201+)

**Determinism:** `seed = levelId`. `level 5000` always yields the identical puzzle. All randomness
flows through a single seeded PRNG (`System.Random(levelId)` or a small xorshift for cross-platform
determinism — **use a custom xorshift**, because `System.Random`'s algorithm is not contractually
stable across .NET runtimes/IL2CPP and must not drive a "same seed = same level forever" promise).

**Difficulty scaling (`L = levelId`, `t = L - 200`):**
```
color_count   = clamp( 4 + floor(t / 60),  4, 8 )
stack_depth   = clamp( 4 + floor(t / 120), 4, 9 )
temp_count    = clamp( 2 + floor(t / 200), 1, 3 )
extra_tubes   = clamp( floor(t / 40),      0, 4 )      // empty/overflow tubes beyond color_count
target_D      = 24 + 0.08 * t                          // soft target; band ±6
```
(All clamps respect §1 caps and the 18-column / depth-9 ceilings.)

**Mechanic unlock thresholds (procedural):**
| Mechanic | Unlocks at | Probability (grows with t, capped) |
|---|---|---|
| Multi-row / large cap / asymmetric | 201 | structural, applied whenever counts exceed 1 row / vary |
| Frozen tube | 240 | `min(0.35, t/1500)`, ≤1 |
| Mystery ball | 300 | `min(0.40, t/1200)`, count = `1 + (rand<0.15?1:0)` ≤2 |
| Multicolor (relief) | 320 | only as fallback overshoot relief (see below), ≤1 |
- Combination gating mirrors §2 but shifted: no two exotics until procedural difficulty is "high"
  (`target_D ≥ 45`), and multicolor+frozen remains 🚫 forever.

**Generation algorithm (reverse-construction, guarantees solvability):**
1. Build a **solved** board: each color stack filled monochrome to its capacity; temp/extra tubes
   empty.
2. Apply `K` random **legal reverse moves** (pop from a full/partial tube, push onto a legal tube),
   where `K` scales with `target_D`. Reverse moves keep the board in the solvable basin by
   construction.
3. Optionally inject mechanics:
   - **Mystery:** pick `m` non-top, non-bottom cells; record their colors; store as negatives.
   - **Frozen:** pick a tube unlikely to be needed early; set `freeze_turns` from `2 + floor(t/300)`,
     capped 12.
   - **Multicolor:** only if step 4 rejects for overshoot — replace one ball with `0`.
4. **Validate with the solver** (`LevelSolver.Solve`, full v2 rules incl. freeze):
   - Must be `IsSolvable` within `DefaultStateLimit`.
   - Estimated difficulty `D` within `target_D ± 6`.
5. **Fallback strategy on validation failure:**
   - a) If unsolvable (should be rare given reverse construction — only freeze can break it): reduce
     `freeze_turns` by 1 and re-validate; repeat to 0; if still failing, drop the frozen tube.
   - b) If `D` too high: add an empty tube **or** (≥320) inject the one multicolor relief ball;
     re-validate.
   - c) If `D` too low: apply more reverse moves (increase `K`) and re-validate.
   - d) If `explored > stateLimit` (timeout): the board is too large — reduce `color_count` or
     `stack_depth` by 1 and regenerate from step 1 with a **sub-seeded** PRNG
     (`seed = levelId * 31 + attempt`) so determinism per attempt is preserved.
   - e) Hard cap of `N` attempts (e.g. 12). If exhausted, fall back to a curated "safe template" table
     indexed by `target_D` band — guarantees a servable level. Log telemetry.
- All attempts derive from `seed = levelId` deterministically, so the *final* served level is still a
  pure function of `levelId`.

**Mystery hidden-color seeding:** colors chosen by the same seeded PRNG during step 3, so the hidden
color behind every mystery ball is deterministic per `levelId` and identical on every device/replay.

**Frozen turn scaling:** `freeze_turns = clamp(2 + floor((L-240)/300), 2, 12)`, reduced by the
fallback loop if it makes the level unsolvable.

**Where this runs:** generation + validation is **offline-pre-bakeable** (run the generator for a
range, store results) or **on-device at level entry**. The solver is Editor-only today
(`namespace BoltSort.Editor`). To run on device, the solver core must move into a runtime assembly
(it has no Editor dependencies — pure C#). Recommended: **pre-bake procedural batches** in CI using
the existing Editor solver and ship them as additional `levels_2xx.json` chunks loaded via
Addressables, keeping the 1.5M-state search off the player's phone. Document this as the chosen path;
on-device generation is a later option if infinite-without-download is required.

---

## SECTION 5 — LEVELSELECTSCREEN REDESIGN

Replaces the single-scroll, `MaxLevels=50`, 3-column build in `LevelSelectController`.

**Paging model**
- **50 levels per page**, fixed **5 columns × 10 rows** grid.
- `pageCount = ceil(totalLevels / 50)`. `totalLevels` comes from `LevelDataSystem` (curated) and,
  for procedural, from a known max or "infinite" sentinel (then pageCount is large but bounded by a
  configured ceiling, e.g. 1000 pages = 50k levels, expandable).
- Navigation bar (bottom or under header): `‹‹ Prev | Page X of Y | Next ››`. Prev disabled on page 1,
  Next disabled on last page.
- **"Go to level"**: numeric input field + `GO` button. On GO: clamp to `[1, totalLevels]`, jump to
  `page = (level-1)/50`, and briefly highlight the target cell.

**Grid sizing (5×10)** — reuse the existing canvas-width-derived cell math but with `Columns = 5`:
`cellSize = (canvasWidth - padX*2 - cellGap*(5-1)) / 5`. The vertical extent of 10 rows fits the
safe-area band; if it overflows on very short screens, allow gentle vertical scroll **within the
page** (page still = 50). No cross-page scroll — paging replaces it.

**Level tile states** (drive by completion + unlock, as today):
- Locked & incomplete: dark tile (×0.4) + `LevelLock` icon, tap → `bolt_invalid` SFX + shake.
- Unlocked & incomplete: dark tile + lock overlay (semi-alpha), tappable.
- Completed: full-color tile, gold number, `AddStarRow` (1–3 stars).

**Number display rules (font sizing + abbreviation)** — applied to the tile's `Num` label
(`resizeTextForBestFit` currently 24–72; replace with explicit sizing by digit count for predictable
readability):

| Level value | Display | Font (at 5-col cellSize ≈ S) | Notes |
|---|---|---|---|
| 1–9 | `N` | **0.55·S** (largest) | centered |
| 10–99 | `NN` | **0.42·S** | centered |
| 100–999 | `NNN` | **0.32·S** | centered |
| 1000–9999 | `"1.2K"`,`"9.9K"` | **0.30·S** | `value/1000` to 1 decimal; drop trailing `.0` → `"5K"` |
| 10000–99999 | `"10K"`,`"25K"` | **0.30·S** | `round(value/1000)` + `K`, no decimal |
| 100000+ | `"100K"`,`"250K"` | **0.26·S** | `round(value/1000)` + `K` |

Abbreviation thresholds: decimal-`K` only in 1000–9999; integer-`K` at ≥10000. (No `M` tier planned;
add at ≥1,000,000 later if needed: `"1.2M"`.) Font ratios are fractions of the computed `cellSize`,
so they scale across devices. Always center; keep the existing shadow.

**Page-state persistence** — save the **last viewed page** so the player returns to it:
- Channel: **PlayerPrefs** key `bs.ls_page` (int), mirroring the existing `bs.next_level` pattern.
  **Do not** extend `SaveData`/`save.json` for this — it's ephemeral UI state, and the save schema is
  versioned/migrated (keeping it out avoids a schema bump).
- On open: if `bs.ls_page` unset, default to the page containing `current_level_id`
  (`(current_level_id-1)/50`) so a returning player lands on their next level. On Prev/Next/GO,
  write `bs.ls_page`.

**Performance** — build only the 50 tiles of the current page (not all levels). Page change rebuilds
the grid (or pools 50 reusable cells and rebinds — preferred for 10k+ levels to avoid GC churn). This
is the key scalability change vs. today's "build all" loop.

---

## SECTION 6 — TUTORIAL DESIGN

**Reusable overlay system** (`TutorialOverlay`, new MonoBehaviour, design only):
- A full-screen dimmed overlay (alpha ~0.6) with a **cutout/highlight** around the relevant board
  element, an **animated pointing hand** sprite, a **text panel**, and a **"Got it" / tap-to-dismiss**.
- Triggered by `levelId` via a small data table:
  `TutorialStep { id, triggerLevelId, highlightTarget, text, blocksGameplay, skippable }`.
- One-shot per mechanic: a `PlayerPrefs` flag `bs.tut.<key>` (e.g. `bs.tut.mystery`) is set on first
  view so it never re-shows. (Same ephemeral-PlayerPrefs rationale as §5.)
- Hand animation: bob/tap loop pointing at the highlighted tube/ball; reuse `TweenUtility` easing.
- `blocksGameplay = true` → input to the board is suspended (reuse the existing `SetGamePaused`
  pause channel in `SortMechanic`) until dismissed. `skippable = true` shows a small "Skip" that sets
  the flag and closes.
- Localization: all text via localization keys (project rule — no hardcoded strings).

**Per-mechanic tutorials:**

| Mechanic | Trigger level | Hand shows | Text (key → English) | Skippable | Blocks until dismissed |
|---|---|---|---|---|---|
| **Mystery ball** | 161 | Points at the mystery ball, then at the ball above it, then mimes lifting the top ball | `tut.mystery` → "This ball hides its color. Move the ball above it to reveal it!" | Yes | Yes (until first tap on the covering ball) |
| **Multicolor ball** | 171 | Points at the multicolor ball, then sweeps across several tubes | `tut.multicolor` → "This special ball matches any color. Use it wisely!" | Yes | Yes |
| **Frozen tube** | 121 | Points at the snowflake + counter on the frozen tube | `tut.frozen` → "This tube is frozen for {0} moves. Plan around it!" ({0} = freeze_turns) | Yes | No (informational; board stays interactive, overlay is dismissible) |
| **Large/multi-row** (soft) | 81 | Quick sweep over the multi-row board | `tut.bigboard` → "More tubes now — take your time!" | Yes | No |

Design note: frozen-tube tutorial is **non-blocking** because the freeze state is already visible from
move 0 (§1.3) — the overlay reinforces rather than gates. Mystery/multicolor block briefly because the
mechanic is non-obvious from the sprite alone.

---

## SECTION 7 — SOLVER UPGRADE REQUIREMENTS

**What changes and what doesn't:**

| Mechanic | Solver change |
|---|---|
| Mystery ball | **None.** Normalize cell to `abs(value)` on load; reveal is player-only info. |
| Multicolor (`0`) | Wildcard match in `IsLegalMove`, `IsWon`, and the finished-tube prune. ≤1 token. |
| Frozen tube | **New state dimension:** per-tube `freezeRemaining`, decremented each applied move; deposits forbidden where `>0`. Included in canonical key until thawed. |
| Large capacity | None — `caps[]` already per-column. Confirm depth×colors budget (table below). |
| Asymmetric | None — populate `caps[]` from `tube_capacities`; key already prefixes `caps[i]:`. |

**New state representation (solver):**
```
State = {
  int[][] columns;            // per-column contents, bottom→top; values: +color | 0 wildcard
                              //   (mystery already normalized to +color at load)
  int     moveCount;          // BFS depth (already tracked in the queue tuple)
}
// Static per level (not per state):
int[]  caps;                  // per-column capacity (from tube_capacities or flat depths)
int[]  freezeInit;            // initial freeze turns per column (0 if none)

// Derived per state for legality:
freezeRemaining[i] = max(0, freezeInit[i] - moveCount)
```
Because freeze decrements on **every** move (chosen UX rule §1.3), `freezeRemaining` is a pure
function of `moveCount` — it does **not** need separate storage, but BFS currently dedups by board
alone, so two states with equal boards but different `moveCount` could have different legal deposits
while a frozen tube is still active. **Resolution:** include the active-freeze vector in the canonical
key **only while any tube is frozen**:
```
key = standardCanonicalKey(columns, caps)
    + (anyFrozen ? "#F" + join(freezeRemaining where >0) : "")
```
Once all tubes thaw (`anyFrozen == false`), the suffix disappears and the key collapses to today's
symmetry-reduced key — so late-game dedup (the bulk of the search) is unaffected. With ≤1 frozen tube
and `freeze ≤ 12`, this adds at most a factor of ~12 to the *early* layer of the search only.

**Move generation additions:**
- Skip `dst` if `freezeRemaining[dst] > 0` (deposit forbidden); removal from a frozen tube is allowed
  (it's a source, unaffected).
- Wildcard: `held == 0` ⇒ legal onto any non-full `dst`; `top(dst) == 0` ⇒ legal for any `held`.
- Finished-tube prune (`IsMono`): a full column counts as finished iff its non-`0` colors are uniform.

**Win check additions:** column complete iff `Length == caps[i]` and `|{nonzero colors}| ≤ 1`.

**State-space growth & 1.5M budget:**

| Config | Approx. complexity driver | Budget verdict |
|---|---|---|
| 6 colors, depth 5, ≤8 cols (today's max) | baseline; symmetry reduction keeps explored ≪ 1.5M | ✅ holds |
| 7–8 colors, depth 6–7 | colors! permutation symmetry + depth ↑ → explored rises ~5–20× | ✅ holds with reduction; monitor |
| 8 colors, depth 9 | worst case in scope; can approach budget | ⚠ flag at authoring; prefer depth ≤8 |
| + frozen (≤1) | ×≤12 on early layers only | ✅ negligible after thaw |
| + wildcard (≤1) | +1 token, branching ~flat | ✅ negligible |
| + mystery | zero (normalized) | ✅ |

**Recommended optimizations (in priority order) if any range trips the budget:**
1. **Keep the 8-column logical cap for the *solver*** even though the *layout* cap rises to 18 — i.e.
   distinct *colors* + temp slots that the search permutes should stay ≤8; extra tubes are empties of
   shared capacity which the existing "interchangeable empties" prune already collapses. (Multi-row is
   a display concern; search cost is driven by colors×depth, not tube count, thanks to the prune.)
2. Replace the `HashSet<string>` canonical key with a **packed `ulong`/`UInt128` or `byte[]` hash**
   (FNV-1a over the per-column bytes) to cut allocation and speed dedup — biggest constant-factor win.
3. Raise `DefaultStateLimit` to 3M for offline pre-bake only (CI has the RAM); keep 1.5M for any
   on-device path.
4. Add a cheap **admissible heuristic** and switch BFS→A* / IDA* if optimal-move proof is needed at
   8×9 (heuristic = count of out-of-place colors / depth). Only if (1)+(2) prove insufficient.
5. For procedural validation specifically, a *solvability-only* check (DFS with the visited set,
   no min-move requirement) is far cheaper than optimal BFS — use it for the validation gate and only
   run full BFS when an exact par is needed.

**Action items captured for implementation phase (not done here):**
- A1. Raise `MaxColumnCount` 8→18 + new/superseding ADR (gates §3 ranges 81+). *(§1.5 blocker)*
- A2. Move `LevelSolver`/`LevelEquivalence` solver core to a runtime assembly if on-device procedural
  is chosen; otherwise keep Editor-only and pre-bake. *(§4)*
- A3. Extend loader: `schema_version==2` branch reads `frozen_tubes`, `tube_capacities`, negative =
  mystery, `0` = wildcard; populate solver/GSM `caps[]` and freeze state.
- A4. Extend `LevelEquivalence` prefix with capacity vector + mechanic fingerprint. *(§2)*
- A5. Extend GSM board snapshot/undo to carry per-tube freeze counters and mystery reveal flags. *(§1.3, §7)*
- A6. New `MoveRejectedReason.DestinationFrozen`.
- A7. Import `Mistery_ball`, `bal_multicolor`, `snowflake` to `Resources/Sprites/...` + `GameAssets`
  accessors.
- A8. `LevelSelectController` rework to 5×10 paging + `bs.ls_page`. *(§5)*
- A9. `TutorialOverlay` system + `bs.tut.*` flags + localization keys. *(§6)*

---

*End of Phase 2 Technical Design Document.*
