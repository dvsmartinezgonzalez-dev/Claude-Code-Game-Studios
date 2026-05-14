# Level Data System

> **Status**: Approved
> **Author**: Design session + systems-designer agent
> **Last Updated**: 2026-04-16
> **Implements Pillar**: Flow Over Friction, Respect the Session

## Summary

The Level Data System defines the serializable data format for all BoltSort levels and provides validated level records to the Game State Manager at load time. Each record specifies stack count, bolt colors, initial bolt placement, and temp slot count. Players experience this system as instant, seamless level transitions — it is infrastructure, not gameplay.

> **Quick reference** — Layer: `Foundation` · Priority: `MVP` · Key deps: `None`

## Overview

The Level Data System is the foundational data layer for BoltSort — it defines the schema, content, and loading contract for every puzzle in the game. Each level is a configuration record specifying: the number of color stacks, the number of distinct bolt colors, how many bolts occupy each stack at the start, and how many temporary overflow slots the player has available. The system provides a read-only, validated level definition to the Game State Manager at load time; once loaded, the level configuration is immutable — all gameplay changes happen in the Game State Manager, not here. For the player, this system is invisible: its job is to ensure every level loads instantly and is always solvable, so the flow state between puzzles is never interrupted.

## Player Fantasy

Players feel the game start the instant they tap — no loading screen, no hesitation, no seams between the level that was and the level that is. The Level Data System turns stored definitions into a ready-to-solve machine so quickly that the player experiences only continuity: one puzzle ends, another begins, the session keeps breathing.

This system has no player fantasy of its own. Its success is measured by its invisibility — the player's attention stays entirely on the puzzle in front of them, never on the infrastructure that delivered it.

## Detailed Design

### Core Rules

**Level Record Schema**

A level record is the complete definition of one BoltSort puzzle. Every field is required unless marked optional.

> **JSON field naming (updated 2026-05-02, per ADR-0004):** Level JSON files use **camelCase** field names to match the C# `LevelRecord` class fields. `JsonUtility` requires exact field name matching (no attribute remapping). Level authoring pipeline must produce camelCase JSON. The table below uses camelCase names — the canonical JSON format.

*Identification fields:*

| Field | Type | Range | Description |
|---|---|---|---|
| `levelId` | integer | 1–9999 | Globally unique, sequentially assigned. Canonical identifier across all systems. |
| `displayName` | `string?` | 1–32 chars | Human-readable label (e.g., "Level 42"). Optional in JSON — if absent or null, the loading interface returns `"Level {levelId}"` automatically. An empty string (`""`) is a VALIDATION_FAILED. |
| `difficultyTier` | integer | 1–5 | Authored difficulty bucket: 1 = Intro, 2 = Easy, 3 = Medium, 4 = Hard, 5 = Expert. Not related to `isTutorial` — they are orthogonal. A level with `difficultyTier = 1` is not necessarily a tutorial, and a tutorial level is not required to have `difficultyTier = 1`. |
| `schemaVersion` | integer | 1–N | Format version of this record. Required for future schema migration. **Known versions at launch: {1}.** Records with any other value are quarantined with `VERSION_MISMATCH`. |

*Layout fields:*

| Field | Type | Range | Description |
|---|---|---|---|
| `colorCount` | integer | 2–8 | Number of distinct bolt colors in this level. Must match distinct color IDs in `colorStacks`. |
| `stackDepth` | integer | 3–8 | Maximum bolt capacity of each color stack. All color stacks share this depth. |
| `colorStacks` | ordered array | length = `colorCount` | One entry per color stack in display order (left to right). Each entry is an object `{"colors": [...]}` containing an ordered array of color ID integers from bottom (index 0) to top. Length 0–`stackDepth` (partial fills allowed). |
| `tempSlotCount` | integer | 0–3 | Number of overflow slots available at level start. |
| `tempSlotDepth` | integer | 1–`stackDepth` | Maximum bolt capacity of each temp slot. Configurable independently of color stacks — allows single-bolt holding areas (depth 1) or deeper buffers. |

*Color identifier:* `colorId` is an integer 1–8 mapping to a named color in the global color palette config (not stored per-level). All color ID values in a level must form a contiguous set {1 … `colorCount`}.

*Metadata fields:*

| Field | Type | Description |
|---|---|---|
| `isTutorial` | `bool` | True = this level is part of the onboarding sequence. **Sole trigger** for Tutorial System overlay. Independent of `difficultyTier` — a level can be `isTutorial = true` at any difficulty tier, and `difficultyTier = 1` (Intro) does not imply `isTutorial = true`. |
| `dailyChallengeEligible` | `bool` | True = level may appear in Daily Challenge pool. Must be false if `isTutorial` is true. |
| `hintOverride` | `int` | Authored cap on maximum hint steps. `-1` = system default applies. `0` = zero hints available. **Design policy:** `hintOverride = 0` is only permitted when `isTutorial = false` AND `difficultyTier` ≥ 3 (Medium, Hard, or Expert). Tutorial levels must always have hints available. Intro (tier 1) and Easy (tier 2) levels must not block hints. Violating either constraint is a VALIDATION_FAILED (see validation rules). |
| `addedVersion` | `string` | Game version when this level was added to the catalogue. **Required.** Format: `"YYYY.MM"` (e.g., `"2026.01"`), zero-padded month. Zero-padding ensures lexicographic string comparison is equivalent to chronological ordering — no custom parser is required. |

*Scoring fields:*

| Field | Type | Range | Description |
|---|---|---|---|
| `parMoves` | `int` | 1–999 | Designer-authored target move count used by Level Complete UI's star rating formula. **Required** — absence in JSON is a VALIDATION_FAILED. Must be ≥ 1 (zero and negative are VALIDATION_FAILED). **Source of truth: manual, constrained by solver.** The authoring pipeline solver computes `solver_min_moves` (the true minimum moves to solve the level); `parMoves` must satisfy `solver_min_moves ≤ parMoves ≤ solver_min_moves + 10`. The lower bound ensures par is achievable — a value below `solver_min_moves` is impossible to reach. The upper bound (+ 10) prevents trivially achievable par that makes star ratings meaningless. This range constraint is enforced at authoring time (Stage 1) only; `solver_min_moves` is not shipped to the client and cannot be re-verified at runtime. |

**Bolt count invariant**: The sum of `colors.Length` across all `colorStacks` entries must equal exactly `colorCount` × `stackDepth`. Every color must have exactly `stackDepth` bolts distributed across the stacks — regardless of how those bolts are distributed between partially and fully filled stacks.

---

**Validation Rules**

Validation runs in two stages:

*Stage 1 — Authoring-time (pipeline-integrated):* Every level runs through a forward-chaining solvability solver as part of the level editor's save/export step. A level that cannot be solved is rejected and cannot be added to the catalogue. The solver outputs `solver_min_moves` (minimum move count to reach the win state) and a sample solution sequence; these are stored in a separate authoring manifest — not shipped to the client — which gates catalogue publication. `par_moves` is validated against `solver_min_moves` at this stage: `solver_min_moves ≤ par_moves ≤ solver_min_moves + 10` is required. A `par_moves` value outside this range blocks export.

*Stage 2 — Runtime (lightweight schema check):* On every load request, before handing a record to any caller, the system runs a lightweight field validation. This does **not** re-run the solvability solver — it verifies structure only. Purpose: catches corrupted device files, deprecated IDs accessed by Daily Challenge, and any records that bypassed the authoring pipeline.

| Rule | Condition | Stage |
|---|---|---|
| `levelId` unique | No two records in the active catalogue share an ID | Both |
| `levelId` range | 1 ≤ `levelId` ≤ 9999 | Both |
| `colorCount` range | 2 ≤ `colorCount` ≤ 8 | Both |
| `stackDepth` range | 3 ≤ `stackDepth` ≤ 8 | Both |
| `tempSlotCount` range | 0 ≤ `tempSlotCount` ≤ 3 | Both |
| `tempSlotDepth` range | 1 ≤ `tempSlotDepth` ≤ `stackDepth` | Both |
| Stack array length | `colorStacks` length equals `colorCount` | Both |
| Bolt count invariant | Each color ID appears exactly `stackDepth` times across all stacks | Both |
| Per-stack bolt count | Each stack's `colors` array length is 0–`stackDepth` | Both |
| Valid color IDs | Every color ID in `colorStacks` is in the set {1 … `colorCount`} | Both |
| Tutorial flag consistency | If `isTutorial` is true, `dailyChallengeEligible` must be false | Both |
| `hintOverride = 0` tutorial guard | If `isTutorial = true` and `hintOverride = 0`, record is rejected (VALIDATION_FAILED, `failing_field = hintOverride`) | Both |
| `hintOverride = 0` difficulty guard | If `hintOverride = 0` and `difficultyTier` ≤ 2 (Intro or Easy), record is rejected (VALIDATION_FAILED, `failing_field = hintOverride`) | Both |
| `parMoves` required | Field must be present in JSON; absence = VALIDATION_FAILED | Both |
| `parMoves` range | `parMoves` ≥ 1 (zero and negative are VALIDATION_FAILED) | Both |
| `parMoves` vs solver | `solver_min_moves ≤ parMoves ≤ solver_min_moves + 10`; outside this range blocks export | Authoring only |
| `addedVersion` format | Must match `"YYYY.MM"` pattern (e.g., `"2026.01"`), zero-padded month; other formats = VALIDATION_FAILED | Both |
| `displayName` not empty | If present, length ≥ 1; empty string `""` = VALIDATION_FAILED | Both |
| Solvability | At least one legal move sequence reaches the win state | Authoring only |

**Validation failure behavior:**

| Failure | Outcome |
|---|---|
| Hard rejection (field constraint) | Record not returned. Caller receives structured error (error code + failing field). Caller falls back to nearest valid ID. |
| Solvability failure | Level rejected from catalogue at authoring time. Never reaches production. |
| Catalogue corruption (>20% fail) | System enters DEGRADED state. Serves only passing records. Diagnostic flag set. |
| Level ID not found | Caller receives NOT_FOUND error. Caller owns fallback logic. |
| Schema version unknown | Record quarantined. Treated as hard rejection. |
| Addressables load failure | System enters DEGRADED. `error_code = CATALOGUE_LOAD_FAILED` in diagnostic. No records served. |

---

### Storage & Loading Contract

**Format:** The catalogue is a single UTF-8 JSON file (`levels.json`) served via Unity Addressables. The file is a **root object** — not a bare array — with the following structure:

```json
{
  "catalogue_version": 1,
  "levels": [ { "level_id": 1, ... }, { "level_id": 2, ... } ]
}
```

`catalogue_version` is an authored integer, monotonically incrementing with each catalogue publication. It is the source of truth for the catalogue's identity — it is **not** computed from record contents, **not** derived from `added_version`, and **not** a hash. The authoring pipeline owns incrementing it on every catalogue export. If the field is absent from JSON, the system records `catalogue_version = 0` as a sentinel indicating an unversioned or legacy catalogue; this does not trigger DEGRADED on its own but is noted in the diagnostic.

**Serialization library:** `Newtonsoft.Json` (Unity package: `com.unity.nuget.newtonsoft-json`). **Not** `JsonUtility` — `JsonUtility` cannot correctly deserialize jagged arrays (`int[][]`), nullable integers (`int?`), or properties with private setters. Deserialization uses `JsonConvert.DeserializeObject<LevelCatalogue>(json)` on the root object; the `levels` array is accessed via `LevelCatalogue.Levels`.

**Addressables group:** `LevelCatalogue` — **two-tier catalogue**:
- **Local bundle** (default): `levels.json` is packed into the app build in the `LevelCatalogue-Local` Addressables group. Always present. Serves all levels on first launch, with no internet connection required.
- **Remote override** (optional LiveOps): A remote `LevelCatalogue-Remote` group may be published to override the local catalogue with updated or expanded content. Checked on app launch after local is loaded; applied only if the remote catalogue is reachable and passes validation. If the remote check fails or times out, the local catalogue remains active — no DEGRADED state from a remote miss alone.

**First-launch guarantee:** The game must reach READY state on first launch with no internet connection, using only the locally bundled catalogue.

**Loading flow:**
1. Caller invokes `ILevelDataSystem.InitializeAsync()` — returns `UniTask` (or
   `Task` if UniTask is not adopted — resolve in ADR before implementation).
2. System issues `Addressables.LoadAssetAsync<TextAsset>("levels.json")` against the **local** group.
3. On load success: deserialize root object via `JsonConvert.DeserializeObject<LevelCatalogue>`, store `catalogue_version` from the root, run Stage 2 validation per record in `LevelCatalogue.Levels`, compute `failure_ratio`, transition to READY or DEGRADED.
4. On local Addressables failure (key not found, checksum mismatch): transition to DEGRADED with `loaded_count = 0`; error code `CATALOGUE_LOAD_FAILED` set in diagnostic. The system does NOT throw or crash — callers check System Readiness Query.
5. *(Optional, post-READY)* If a remote catalogue URL is configured and the device is online, `GameBootstrap` may trigger a catalogue reload using the remote group. This is a separate code path from initial boot and must not block or delay the READY state.

**Threading:** All Addressables callbacks and JSON deserialization run on the
Unity main thread. There is no background thread — "concurrent requests" in this
document means multiple callers awaiting the same `InitializeAsync()` call, not
parallel threads.

**Initialization ownership:** `GameBootstrap` (or equivalent scene entry point)
calls `InitializeAsync()` once, before any other system requests a level.
Duplicate calls while in LOADING or READY are no-ops — they return the existing
task/result.

**Catalogue reload:** Triggered by `GameBootstrap` calling `ReloadAsync()`. Valid from READY or DEGRADED states only. `ReloadAsync()` is a **separate code path** from `InitializeAsync()` — it re-runs the full load pipeline (Addressables load → deserialization → Stage 2 validation → state transition) and replaces the in-memory catalogue atomically on completion. Not self-triggered by the Level Data System.

---

### States and Transitions

```
UNINITIALIZED → LOADING → READY ──┐
                   |               │ ReloadAsync()
                   ↓               ↓
                DEGRADED ────────► LOADING
                    ReloadAsync()
```

| State | Entry | Exit | Behavior |
|---|---|---|---|
| UNINITIALIZED | Application launch | → LOADING when `InitializeAsync()` is awaited | No requests served. Calls to `GetLevel()` etc. throw `InvalidOperationException`. |
| LOADING | `InitializeAsync()` called (from UNINITIALIZED); or `ReloadAsync()` called (from READY or DEGRADED) | → READY (≥80% records pass); → DEGRADED (<80% or Addressables failure) | Load pipeline running. All getters throw `InvalidOperationException`. Duplicate `ReloadAsync()` calls while LOADING return the same in-flight task. |
| READY | Load or reload completes with ≥80% records passing | → LOADING on `ReloadAsync()` | All valid records available. All getter methods safe to call. |
| DEGRADED | >20% records fail validation, or Addressables failure | → LOADING on `ReloadAsync()` | Serves only valid records (if any). Diagnostic flag set. Getters still callable — return errors per normal contract. |

No terminal ERROR state. The system always attempts to serve the maximum available valid content rather than blocking gameplay.

**Initialization contract:** `GameBootstrap` (scene entry point) owns the single `await levelDataSystem.InitializeAsync()` call. No other system calls `InitializeAsync()`. All downstream systems call only `GetLevel()`, `GetRange()`, `GetByFilter()`, or `GetReadiness()` — and only after boot completes.

**InitializeAsync contract guarantees:**
- The method is idempotent — multiple calls return the same result without re-triggering load.
- The method cannot execute in parallel — if called while already in LOADING, it returns the same in-flight task.
- Only one initialization flow exists per app session.

**ReloadAsync contract guarantees:**
- Only callable from READY or DEGRADED. Calling from UNINITIALIZED throws `InvalidOperationException`.
- If called while LOADING (a reload already in progress), returns the same in-flight `UniTask` — no second load is started.
- On completion, the in-memory catalogue is fully replaced — `catalogue_version`, `loaded_count`, and `skipped_count` all reflect the new catalogue.
- Does **not** reuse `InitializeAsync()` internally — it is a separate method and a separate code path.
- `GameBootstrap` is the only permitted caller. No other system calls `ReloadAsync()`.

**C# interface:**

```csharp
public interface ILevelDataSystem
{
    UniTask InitializeAsync();
    UniTask ReloadAsync();
    SystemReadiness GetReadiness();
    LevelRecord GetLevel(int levelId);
    LevelRecord[] GetRange(int fromLevelId, int toLevelId);
    LevelRecord[] GetByFilter(LevelFilter filter);
}
```

---

### Interactions with Other Systems

**Loading interface — four read-only request types:**

*Get Level by ID*: Input: `level_id`. Output (success): complete validated Level Record. Output (failure): error response with `error_code` (NOT_FOUND / VALIDATION_FAILED / VERSION_MISMATCH / SYSTEM_NOT_READY), requested ID, and diagnostic detail.

*Get Level Range*: Input: `from_level_id`, `to_level_id`. Output: ordered array of valid records in that inclusive range. Missing IDs are omitted silently. Use case: Level Progression prefetching next 3–5 levels.

*Get Levels by Filter*: Input: filter object — any combination of `difficulty_tier`, `daily_challenge_eligible`, `color_count_min`/`max`, `added_version`. Output: unordered array of matching records. Use case: Daily Challenge pool selection.

*System Readiness Query*: Input: none. Output: `SystemReadiness` — fields: `ready` (bool), `loaded_count` (int, records that passed validation), `skipped_count` (int, records that failed validation), `catalogue_version` (int, sourced from the JSON root field; `0` if absent). Use case: Game State Manager boot check; `catalogue_version` enables callers to detect catalogue changes across reloads.

**Per-system interaction:**

| System | What it requests | What it receives | Notes |
|---|---|---|---|
| Game State Manager | Get Level by ID | Full record: `color_stacks`, `stack_depth`, `temp_slot_count`, `temp_slot_depth`, `color_count` | Primary consumer. Receives everything needed to instantiate board state. |
| Sort Mechanic | (indirect) | Reads `stack_depth` and `temp_slot_depth` from Game State Manager's board state | Never calls Level Data System directly. |
| Level Progression | Get Level by ID (existence check); Get Level Range (prefetch); Get Levels by Filter | `difficulty_tier`, `added_version` for pacing and version gating | Owns sequencing logic only. |
| Tutorial System | Get Level by ID (may share response with Game State Manager) | `is_tutorial` flag, `level_id` | Activates gesture overlay if `is_tutorial` is true. Does not define which IDs are tutorial — that is authored data. |
| Daily Challenge System | Get Levels by Filter (`daily_challenge_eligible = true`, optional tier/color filters) | Unordered pool of eligible records | Owns selection, scheduling, deduplication. Level Data System provides pool only. |
| Level Complete UI | Get Level by ID | `par_moves` for star rating formula | Read at `level_complete` time. `par_moves` is guaranteed present and ≥ 1 — any record without it was rejected at load. No fallback needed. |

**Cache policy**: `LevelRecord` is a `[Serializable] class` with all properties `{ get; private set; }` — callers receive an immutable reference; they cannot mutate the catalogue's internal state. Callers must not hold `LevelRecord` references across app sessions — re-request after any app relaunch. If offline level packs are introduced post-launch, held references are valid until a catalogue reload is triggered.

**`LevelRecord` type contract:**

> Private setters are supported via `[JsonProperty]` — Newtonsoft.Json respects them during deserialization. `int?` correctly preserves `null` vs `0`. `int[][]` (jagged array) deserializes correctly from a nested JSON array.

```csharp
[JsonObject(MemberSerialization.OptIn)]
public sealed class LevelRecord
{
    [JsonProperty("level_id")]       public int LevelId { get; private set; }
    [JsonProperty("display_name")]   public string DisplayName { get; private set; }  // never null — defaulted at load
    [JsonProperty("difficulty_tier")] public int DifficultyTier { get; private set; }
    [JsonProperty("schema_version")] public int SchemaVersion { get; private set; }
    [JsonProperty("color_count")]    public int ColorCount { get; private set; }
    [JsonProperty("stack_depth")]    public int StackDepth { get; private set; }
    [JsonProperty("color_stacks")]   public int[][] ColorStacks { get; private set; } // defensive copy on access
    [JsonProperty("temp_slot_count")] public int TempSlotCount { get; private set; }
    [JsonProperty("temp_slot_depth")] public int TempSlotDepth { get; private set; }
    [JsonProperty("is_tutorial")]    public bool IsTutorial { get; private set; }
    [JsonProperty("daily_challenge_eligible")] public bool DailyChallengeEligible { get; private set; }
    [JsonProperty("hint_override")]  public int? HintOverride { get; private set; }  // null = system default; 0 = zero hints authored
    [JsonProperty("added_version")]  public string AddedVersion { get; private set; }
    [JsonProperty("par_moves")]      public int ParMoves { get; private set; }
}
```

**`LevelCatalogue` type contract** (root deserialization target):

```csharp
[JsonObject(MemberSerialization.OptIn)]
public sealed class LevelCatalogue
{
    [JsonProperty("catalogue_version")] public int CatalogueVersion { get; private set; }  // 0 if absent
    [JsonProperty("levels")]            public LevelRecord[] Levels { get; private set; }
}
```

**`SystemReadiness` type contract** (returned by `GetReadiness()`):

```csharp
public readonly struct SystemReadiness
{
    public bool   Ready            { get; }   // true = READY; false = DEGRADED or LOADING
    public int    LoadedCount      { get; }   // records that passed Stage 2 validation
    public int    SkippedCount     { get; }   // records that failed Stage 2 validation
    public int    CatalogueVersion { get; }   // from JSON root; 0 = absent/unversioned
    public string DiagnosticCode   { get; }   // null when Ready; e.g. "CATALOGUE_LOAD_FAILED"
}
```

**Authoring pipeline requirement**: The solvability solver must run as an integrated step in the level editor's save/export workflow — not a separate manual check. A failed solve blocks export. This is a tooling requirement, not runtime behavior, but captured here as the primary enforcement mechanism for the solvability guarantee this system makes.

## Formulas

### Bolt Count Invariant

The Bolt Count Invariant formula is defined as:

`total_bolts = color_count × stack_depth`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Number of distinct bolt colors | `color_count` | int | 2–8 | Count of unique color IDs in this level. Equals the length of `color_stacks`. |
| Bolts per color | `stack_depth` | int | 3–8 | Maximum capacity of each color stack. Every color has exactly this many bolts distributed across stacks. |
| Total bolts across all color stacks | `total_bolts` | int | 6–64 | Sum of bolt counts across every entry in `color_stacks`. Temp slots excluded — they contain zero bolts at level start by definition. |

**Output Range:** 6 (color_count=2, stack_depth=3) to 64 (color_count=8, stack_depth=8).

**Validation uses two stacked checks** — both must pass:

*Check 1 (necessary):* `sum(len(stack) for stack in color_stacks) == color_count × stack_depth`

*Check 2 (sufficient):* For each color `c` in {1 … color_count}: `count(color_id == c across all color_stacks) == stack_depth`

Check 2 is the stronger condition — it catches cases where total bolt count is correct but one color is over-represented and another under-represented. Check 1 is a fast early-exit that avoids the per-color scan when the total is already wrong.

**Example:** `color_count = 4`, `stack_depth = 4` → invariant target: 16 bolts, 4 of each color.
- Valid: `[[1,2,3,4], [2,1,4,3], [3,4,1,2], [4,3,2,1]]` — 16 total, each color exactly 4 times. ✓
- Invalid (wrong total): `[[1,2,3], [2,1,4,3], [3,4,1,2], [4,3,2,1]]` — 15 total. Fails Check 1.
- Invalid (wrong per-color, correct total): `[[1,1,1,1], [2,2,4,3], [3,4,2,2], [4,3,3,3]]` — 16 total, color 1 appears 5 times. Fails Check 2.

---

### Catalogue Health Threshold

`failure_ratio = failed_record_count / total_record_count`

Decision rule: `system_state = DEGRADED if failure_ratio > 0.20, else READY`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Records that failed runtime validation | `failed_record_count` | int | 0–N | Records that did not pass Stage 2 schema check during LOADING. |
| Total records in loaded catalogue | `total_record_count` | int | 0–N | All records attempted during load (passing + failing). |
| Degradation threshold | `degraded_threshold_ratio` | float | fixed: 0.20 | Maximum tolerable failure ratio. Strict greater-than: exactly 0.20 resolves to READY. |
| Computed failure ratio | `failure_ratio` | float | 0.0–1.0 | Proportion of loaded records that failed validation. |

**Output Range:** `system_state` is READY or DEGRADED — binary, no intermediate state.

**Edge case — total_record_count = 0:** Division is undefined. Enter DEGRADED unconditionally. An empty catalogue cannot serve any level; this signals an authoring pipeline failure.

**Edge case — total_record_count < 5:** The 20% threshold is coarse at small N (1 bad record in 4 = 25% → DEGRADED). This is intentional and conservative — a catalogue of fewer than 5 records is not a valid production state. The threshold is not adjusted for small N.

**Examples:**

| failed | total | ratio | state |
|---|---|---|---|
| 15 | 100 | 0.15 | READY |
| 25 | 100 | 0.25 | DEGRADED |
| 20 | 100 | 0.20 | READY (exclusive threshold) |
| 1 | 4 | 0.25 | DEGRADED (small catalogue — intentional) |
| 0 | 0 | N/A | DEGRADED (empty catalogue — unconditional) |

## Edge Cases

**EC-01 — If `color_count = 2` and `stack_depth = 3` (minimum values):** System produces a valid 6-bolt level. The solvability solver must handle minimum-size boards without special-casing. Valid authored state — not degenerate.

**EC-02 — If `color_count = 8` and `stack_depth = 8` (maximum values):** `total_bolts = 64`. Runtime schema check must not cap bolt counts below 64. Authoring-time solver must complete within its time budget for 64-bolt boards; if it cannot, the level is rejected as unsolvable — not as a schema error.

**EC-03 — If `temp_slot_count = 0`:** No temp slots exist. `temp_slot_depth` is still present in the record and still validated (range 1–`stack_depth`). The Game State Manager must not instantiate any temp slot nodes and must not substitute a default slot count.

**EC-04 — If `temp_slot_depth = stack_depth`:** Temp slots are as deep as color stacks. Legal. The Sort Mechanic must not treat this as an overflow condition or clamp the depth.

**EC-05 — If `hint_override = 0` (integer zero, not null):** This is an authored cap of zero hint steps — not the absence of a cap (null). The Hint System must treat 0 as "zero hints available for this level," not as "no authored cap." Confusing 0 with null silently applies the system default; this is the Hint System's contract obligation to respect, not a Level Data System defect.

**EC-06 — If `is_tutorial = true` AND `daily_challenge_eligible = true`:** Hard rejection at both authoring time and runtime. Error code: VALIDATION_FAILED, failing field: `daily_challenge_eligible`. Level not returned. Rationale: a tutorial level in the Daily Challenge pool breaks onboarding sequencing.

**EC-07 — If a catalogue reload is triggered:** All getters (`GetLevel`, `GetRange`, `GetByFilter`) are synchronous and execute on the Unity main thread. A reload can only be initiated from READY or DEGRADED state by `GameBootstrap`. There is no in-flight overlap — the reload call transitions the system to LOADING before any getter runs again. No mid-request swap is possible.

**EC-08 — If Get Level Range is requested but every ID in the range fails runtime validation:** Caller receives an empty array — not an error code. The caller (Level Progression) must distinguish "empty range" from NOT_FOUND by checking array length, not by expecting an error code.

**EC-09 — If the initial board state is already solved (all stacks monochromatic at level start):** Valid schema, but trivially solved with 0 moves. The solvability solver must recognize this as solved-at-start (minimum moves = 0). The Game State Manager must handle receiving a pre-won board without entering an undefined transition. This is a designer error, not a system error — the authoring tool should warn but not block export.

**EC-10 — If a color stack has zero bolts at level start (`bolts = []`):** Valid — 0 is within 0–`stack_depth`. The bolt count invariant is satisfied by other stacks holding that color's bolts. The Game State Manager must render empty stacks as accessible destination slots — not skip them or treat them as temp slots.

**EC-11 — If `level_id = 9999` is requested and the catalogue only contains levels up to 500:** Returns NOT_FOUND. Callers must not interpret NOT_FOUND as a schema error — 9999 is a valid ID range that simply has no authored record yet.

**EC-12 — If records share an unknown `schema_version` (newer format deployed to an older client):** Records are quarantined (VALIDATION_FAILED / VERSION_MISMATCH). Each counts toward `failed_record_count`. A batch of next-version records can push `failure_ratio` above 0.20 alone and trigger DEGRADED. The diagnostic flag must include the unknown `schema_version` value to distinguish this from data corruption.

**EC-13 — If Get Levels by Filter returns zero results (no records match the filter):** Caller receives an empty array. Not DEGRADED, not an error. The Daily Challenge System owns fallback selection. Level Data System does not select a fallback on the caller's behalf.

**EC-14 — If any getter is called before `InitializeAsync()` completes (UNINITIALIZED or LOADING state):** Throws `InvalidOperationException` immediately. This is a programming error, not a runtime condition — `GameBootstrap` must `await InitializeAsync()` before any other system runs. No polling, no retry, no silent fallback.

**EC-15 — If `InitializeAsync()` is called a second time while LOADING is in progress:** Returns the same in-flight `UniTask` — no second load is started. If called again while READY, it is a no-op returning a completed task.

**EC-16 — If `GetRange` is called with `from_level_id > to_level_id` (inverted range):** The system returns an empty array. No error code is returned. This is treated as a valid but empty request — consistent with EC-08 and EC-13. Callers must not interpret an empty array as an error condition.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| Game State Manager | Downstream — depends on this | Data dependency — consumes full Level Record to instantiate board state | Get Level by ID → receives `color_stacks`, `stack_depth`, `temp_slot_count`, `temp_slot_depth`, `color_count` |
| Sort Mechanic | Downstream — indirect dependency | Data dependency — reads schema constants from Game State Manager's board state, not directly from this system | No direct interface. Receives `stack_depth` and `temp_slot_depth` via Game State Manager. |
| Level Progression | Downstream — depends on this | Data dependency — queries level existence, difficulty metadata, and version gating | Get Level by ID (existence check), Get Level Range (prefetch), Get Levels by Filter (difficulty/version filtering) |
| Tutorial System | Downstream — depends on this | Data dependency — reads `is_tutorial` flag per level | Get Level by ID → reads `is_tutorial` and `level_id` |
| Daily Challenge System | Downstream — depends on this | Data dependency — queries eligible level pool | Get Levels by Filter (`daily_challenge_eligible = true`, optional tier/color filters) |
| Level Complete UI | Downstream — depends on this | Data dependency — reads `par_moves` for star rating | Get Level by ID → reads `par_moves` field |

**Bidirectional consistency:** Each downstream system listed here must reference Level Data System in their own Dependencies section. This table is the authoritative record of upstream connections for those GDDs to verify against.

**Hard vs. soft dependencies:**
- Game State Manager, Level Progression: **hard** — cannot function without a loaded Level Data System
- Tutorial System, Daily Challenge System: **hard** for their specific features; the game can function without them if Level Data System is READY
- Sort Mechanic: **soft indirect** — depends on Game State Manager having already loaded the level record

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect if Too High | Effect if Too Low |
|---|---|---|---|---|
| `color_count` (per level) | 3 (Intro) → 8 (Expert) | 2–8 | Too many colors → puzzle overwhelming; players abandon. Beyond 8: color distinction breaks on small screens. | 2 colors → trivially easy. Only valid for Intro-tier levels. |
| `stack_depth` (per level) | 4 (Intro) → 8 (Expert) | 3–8 | Deep stacks → hard to read bolt order visually; UI overflow risk on small phones. | 3 is the minimum for meaningful planning. Below this, no interesting puzzle space. |
| `temp_slot_count` (per level) | 2 (default) → 0 (expert) | 0–3 | More temp slots → level too easy; challenge erodes. | 0 slots with many colors → near-impossible; creates frustration against Flow Over Friction pillar. |
| `temp_slot_depth` (per level) | 1 (holding area) → `stack_depth` (deep buffer) | 1–`stack_depth` | Full-depth temp slots make most levels trivially solvable. | Depth 1 (single bolt holding area) creates the tightest, most expert-tier constraint. |
| `degraded_threshold_ratio` | 0.20 | 0.05–0.40 | High → system stays READY with many bad records; corrupted levels reach players. | Low → aggressive DEGRADED on minor catalogue issues; blocks content unnecessarily. |
| *(request queue removed)* | N/A | N/A | Boot serialization via `await InitializeAsync()` makes a queue unnecessary. | N/A |

**Knob interactions:**
- `color_count` and `stack_depth` are multiplicatively related via the bolt count invariant. Increasing both simultaneously creates exponentially harder levels — tune them in concert.
- `temp_slot_count = 0` with `color_count > 5` requires very careful bolt distribution to remain solvable. The authoring-time solver catch rate becomes critical at this combination.
- `temp_slot_depth = 1` and `temp_slot_count = 1` is the "single holding area" design common in the genre. This combination produces the most puzzle-like feel.

**Scope boundary:** Difficulty curve pacing — which levels get which values across 200 levels — is owned by the Level Progression GDD. These knobs define the valid envelope; Level Progression defines the authored schedule within it.

## Visual/Audio Requirements

Not applicable. The Level Data System is a pure data layer — it produces no visual or audio output directly. All rendering and audio is driven by the Game State Manager and Animation System using the data this system provides.

## UI Requirements

Not applicable. The Level Data System has no player-facing UI. The System Readiness Query is consumed by the Game State Manager and is not exposed to the player.

## Cross-References

| This Document References | Target GDD | Specific Element Referenced | Nature |
|---|---|---|---|
| `stack_depth` used by Game State Manager | `design/gdd/game-state-manager.md` | Board state initialization from stack_depth field | Data dependency |
| `temp_slot_count` and `temp_slot_depth` used by Game State Manager | `design/gdd/game-state-manager.md` | Temp slot instantiation from these fields | Data dependency |
| `is_tutorial` flag consumed by Tutorial System | `design/gdd/tutorial-system.md` | Tutorial overlay activation decision | Data dependency |
| `daily_challenge_eligible` flag consumed by Daily Challenge System | `design/gdd/daily-challenge-system.md` | Level pool filter | Data dependency |
| `difficulty_tier` consumed by Level Progression | `design/gdd/level-progression.md` | Difficulty pacing and filter queries | Data dependency |
| `hint_override` consumed by Hint System | `design/gdd/hint-system.md` | Per-level hint cap | Ownership handoff |

## Acceptance Criteria

> **Test type**: Logic (schema validation, formula checks, state machine). BLOCKING is marked per AC — see individual ACs. Automated unit tests required in `tests/unit/level-data-system/` before implementing stories can be marked Done.

**AC-01 — level_id boundary: minimum valid value**
**GIVEN** a catalogue containing a record with `level_id = 1` that passes all validation rules, **WHEN** a Get Level by ID request is issued with `level_id = 1`, **THEN** the system returns the complete validated Level Record with no error code and `level_id = 1` in the response.

**AC-02 — level_id boundary: out-of-range value**
**GIVEN** the system is in READY state, **WHEN** a Get Level by ID request is issued with `level_id = 0` (below the valid range of 1–9999), **THEN** the system returns an error response with `error_code = NOT_FOUND` and does not return a Level Record.

**AC-03 — Bolt Count Invariant Check 1: wrong total bolt count** *(BLOCKING)*
**GIVEN** a record with `color_count = 4`, `stack_depth = 4`, and `color_stacks = [[1,2,3], [2,1,4,3], [3,4,1,2], [4,3,2,1]]` (15 bolts total, not 16), **WHEN** runtime schema validation runs, **THEN** the record fails validation, is not returned, counts toward `failed_record_count`, and the error response names `color_stacks` as the failing field.

**AC-04 — Bolt Count Invariant Check 2: correct total, wrong per-color distribution** *(BLOCKING)*
**GIVEN** a record with `color_count = 4`, `stack_depth = 4`, and `color_stacks = [[1,1,1,1], [2,2,4,3], [3,4,2,2], [4,3,3,3]]` (16 bolts total; color 1 appears 5 times), **WHEN** runtime schema validation runs, **THEN** the record fails Check 2, is not returned, and the error response names `color_stacks` as the failing field.

**AC-05 — Catalogue Health Threshold: exactly 0.20 failure ratio resolves to READY** *(BLOCKING)*
**GIVEN** a catalogue of 100 records where exactly 20 fail validation (ratio = 0.20 exactly) and 80 pass, **WHEN** the LOADING phase completes, **THEN** the system transitions to READY (not DEGRADED), the 80 passing records are available, and System Readiness Query returns `ready = true`.

**AC-06 — Catalogue Health Threshold: above 0.20 triggers DEGRADED** *(BLOCKING)*
**GIVEN** a catalogue of 100 records where 21 fail validation (ratio = 0.21), **WHEN** the LOADING phase completes, **THEN** the system transitions to DEGRADED, the 79 passing records are still served, and System Readiness Query returns a diagnostic flag with `skipped_count = 21`.

**AC-07 — Catalogue Health Threshold: empty catalogue triggers DEGRADED unconditionally** *(BLOCKING)*
**GIVEN** a catalogue file containing zero records, **WHEN** the LOADING phase completes, **THEN** the system transitions to DEGRADED without attempting division, and System Readiness Query returns `loaded_count = 0`, `skipped_count = 0`, with the diagnostic flag set.

**AC-08 — hint_override = 0 (integer) is distinct from null**
**GIVEN** a record with `hint_override = 0` (integer zero) that is otherwise valid, **WHEN** a Get Level by ID request retrieves this record, **THEN** the returned Level Record contains `hint_override = 0` as an integer — not null — and the system does not substitute the system default.

**AC-09 — Tutorial flag conflict: hard rejection** *(BLOCKING)*
**GIVEN** a record with `is_tutorial = true` and `daily_challenge_eligible = true` and all other fields valid, **WHEN** runtime schema validation runs, **THEN** the record is rejected with `error_code = VALIDATION_FAILED` and `failing_field = daily_challenge_eligible`; the record is not returned and counts toward `failed_record_count`.

**AC-10 — Pre-init getter throws InvalidOperationException** *(BLOCKING)*
**GIVEN** `InitializeAsync()` has not been called, **WHEN** any code calls `GetLevel()`, `GetRange()`, or `GetByFilter()`, **THEN** an `InvalidOperationException` is thrown immediately — no error code returned, no silent fallback.

**AC-11 — Duplicate InitializeAsync() calls during LOADING return the same task** *(BLOCKING)*
**GIVEN** `InitializeAsync()` has been called and is still in progress (LOADING state), **WHEN** a second caller calls `InitializeAsync()`, **THEN** the same in-flight `UniTask` is returned and no second Addressables load is issued.

**AC-12 — InitializeAsync() in READY state is a no-op** *(BLOCKING)*
**GIVEN** the system is in READY state, **WHEN** `InitializeAsync()` is called again, **THEN** it returns a completed `UniTask` immediately without re-loading the catalogue.

**AC-13 — Get Level Range: all IDs fail validation returns empty array, not error**
**GIVEN** the system is in READY state and level IDs 50–55 exist in the catalogue but all six records fail Stage 2 runtime validation, **WHEN** a Get Level Range request is issued with `from_level_id = 50` and `to_level_id = 55`, **THEN** the system returns an empty array of length 0, no `error_code` field is present in the response, and the system state remains READY.

**AC-14 — Get Levels by Filter: no matching records returns empty array, not error**
**GIVEN** the system is in READY state and the loaded catalogue contains no records matching `{daily_challenge_eligible: true, difficulty_tier: 5}`, **WHEN** that filter is issued via `GetByFilter()`, **THEN** the system returns an empty array of length 0, no `error_code` is present, the system does not substitute a fallback record or modify the filter, and the system state remains READY.

**AC-15 — Pre-solved board: already-won initial state is served without error**
**GIVEN** a record with `color_count = 2`, `stack_depth = 3`, and `color_stacks = [[1,1,1],[2,2,2]]` (all stacks monochromatic at level start) that passed authoring-time solvability validation, **WHEN** a Get Level by ID request retrieves it at runtime, **THEN** the system returns the complete validated Level Record with no `error_code`; the record is not counted in `failed_record_count` and no VALIDATION_FAILED response is generated.

**AC-16 — color_id contiguous set: non-contiguous IDs fail validation**
**GIVEN** a record with `color_count = 3` and `color_stacks` containing `color_id` values from the set {1, 2, 4} (3 is absent; set is non-contiguous; valid set for `color_count = 3` is {1, 2, 3}), **WHEN** runtime Stage 2 schema validation runs, **THEN** the record is not returned, `error_code = VALIDATION_FAILED` is set with the failing field identifying the invalid `color_id`, and the record counts toward `failed_record_count`.

**AC-17 — Addressables load failure transitions to DEGRADED, not crash** *(BLOCKING)*
**GIVEN** the Addressables key `"levels.json"` fails to resolve (key not found, checksum mismatch, or I/O error), **WHEN** `InitializeAsync()` completes, **THEN** the system is in DEGRADED state with `loaded_count = 0`, `skipped_count = 0`, and `error_code = CATALOGUE_LOAD_FAILED` present in the diagnostic; no exception propagates to the caller, and all getter methods return their normal error contracts rather than throwing.

**AC-18 — par_moves absent: VALIDATION_FAILED** *(BLOCKING)*
**GIVEN** a JSON record where the `par_moves` field is entirely absent, **WHEN** runtime Stage 2 schema validation runs, **THEN** the record is not returned, `error_code = VALIDATION_FAILED` is set with `failing_field = par_moves`, and the record counts toward `failed_record_count`.

**AC-19 — par_moves = 0: VALIDATION_FAILED** *(BLOCKING)*
**GIVEN** a JSON record where `par_moves = 0` and all other fields are valid, **WHEN** runtime Stage 2 schema validation runs, **THEN** the record is not returned, `error_code = VALIDATION_FAILED` is set with `failing_field = par_moves`, and the record counts toward `failed_record_count`.

**AC-20 — added_version wrong format: VALIDATION_FAILED**
**GIVEN** a JSON record where `added_version = "v1.0"` (does not match the required `"YYYY.MM"` zero-padded pattern; `"2026.1"` is also invalid — month must be two digits), **WHEN** runtime Stage 2 schema validation runs, **THEN** the record is not returned, `error_code = VALIDATION_FAILED` is set with `failing_field = added_version`, and the record counts toward `failed_record_count`.

**AC-21 — display_name empty string: VALIDATION_FAILED**
**GIVEN** a JSON record where `display_name = ""` (empty string — not `null` or absent), **WHEN** runtime Stage 2 schema validation runs, **THEN** the record is not returned, `error_code = VALIDATION_FAILED` is set with `failing_field = display_name`, and the record counts toward `failed_record_count`.

**AC-22 — EC-05: hint_override null preserves system-default signal**
**GIVEN** a record with `hint_override` absent from JSON or explicitly set to JSON `null`, **WHEN** a Get Level by ID request retrieves this record, **THEN** the returned `LevelRecord.HintOverride` is C# `null` (`int?`); the system does not coerce the value to `0`, and downstream consumers receive the unambiguous signal that the system default applies.

**AC-23 — EC-07: catalogue reload transitions to LOADING before any getter executes**
**GIVEN** the system is in READY state and `GameBootstrap` triggers a catalogue reload, **WHEN** the reload call is issued, **THEN** the system transitions to LOADING before any subsequent `GetLevel()`, `GetRange()`, or `GetByFilter()` call can execute; no getter returns data from the pre-reload catalogue after the transition; there is no mid-request catalogue swap.

**AC-24 — EC-12: unknown schema_version quarantines record with VERSION_MISMATCH** *(BLOCKING)*
**GIVEN** a catalogue record whose `schema_version` value is not recognized by the current client (e.g., a next-version record deployed to an older build), **WHEN** runtime Stage 2 schema validation runs, **THEN** the record is quarantined, `error_code = VERSION_MISMATCH` is set, the unknown `schema_version` value is included in the diagnostic payload (to distinguish from data corruption), and the record counts toward `failed_record_count`.

**AC-25 — ReloadAsync: full state transition and data replacement** *(BLOCKING)*
**GIVEN** the system is in READY state with `catalogue_version = 1` and 100 valid records loaded, and a new catalogue (version 2, 105 records) is available via Addressables, **WHEN** `GameBootstrap` calls `await ReloadAsync()`, **THEN** the system transitions to LOADING (getters throw `InvalidOperationException` during this window), completes the full load pipeline, transitions to READY, and `GetReadiness()` returns `loaded_count = 105` and `catalogue_version = 2`; the previous catalogue is no longer served.

**AC-26 — ReloadAsync: duplicate call while LOADING returns same task**
**GIVEN** `ReloadAsync()` has been called and is still in progress (LOADING state), **WHEN** a second caller calls `ReloadAsync()`, **THEN** the same in-flight `UniTask` is returned and no second Addressables load is issued.

**AC-27 — ReloadAsync: call from UNINITIALIZED throws InvalidOperationException**
**GIVEN** `InitializeAsync()` has never been called (UNINITIALIZED state), **WHEN** any code calls `ReloadAsync()`, **THEN** an `InvalidOperationException` is thrown immediately.

**AC-28 — ReloadAsync: callable from DEGRADED state**
**GIVEN** the system is in DEGRADED state, **WHEN** `GameBootstrap` calls `await ReloadAsync()`, **THEN** the system transitions to LOADING, re-runs the full load pipeline, and transitions to READY or DEGRADED based on the new catalogue's validation results.

**AC-29 — hint_override = 0 on tutorial level: VALIDATION_FAILED** *(BLOCKING)*
**GIVEN** a record with `is_tutorial = true`, `hint_override = 0`, and all other fields valid, **WHEN** runtime Stage 2 schema validation runs, **THEN** the record is not returned, `error_code = VALIDATION_FAILED` is set with `failing_field = hint_override`, and the record counts toward `failed_record_count`.

**AC-30 — hint_override = 0 on Intro/Easy tier: VALIDATION_FAILED**
**GIVEN** a record with `difficulty_tier = 2` (Easy), `is_tutorial = false`, `hint_override = 0`, and all other fields valid, **WHEN** runtime Stage 2 schema validation runs, **THEN** the record is not returned, `error_code = VALIDATION_FAILED` is set with `failing_field = hint_override`, and the record counts toward `failed_record_count`.

**AC-31 — hint_override = 0 on Medium/Hard/Expert tier: valid**
**GIVEN** a record with `difficulty_tier = 3` (Medium), `is_tutorial = false`, `hint_override = 0`, and all other fields valid, **WHEN** runtime Stage 2 schema validation runs, **THEN** the record passes validation and is returned; `HintOverride` in the returned `LevelRecord` is integer `0`, not `null`.

**AC-32 — par_moves below solver_min_moves: authoring rejection** *(BLOCKING)*
**GIVEN** a level where the solver computes `solver_min_moves = 8` and the authored record has `par_moves = 6` (below the solver minimum), **WHEN** the authoring pipeline runs Stage 1 validation, **THEN** the level export is blocked, the error identifies `par_moves` as below `solver_min_moves`, and the level is not added to the catalogue.

**AC-33 — par_moves exceeds solver_min_moves + 10: authoring rejection**
**GIVEN** a level where `solver_min_moves = 8` and the authored record has `par_moves = 19` (exceeds `solver_min_moves + 10 = 18`), **WHEN** the authoring pipeline runs Stage 1 validation, **THEN** the level export is blocked, the error identifies `par_moves` as exceeding the allowed ceiling, and the level is not added to the catalogue.

**AC-34 — par_moves within allowed range: valid**
**GIVEN** a level where `solver_min_moves = 8` and the authored record has `par_moves = 12` (satisfies `8 ≤ 12 ≤ 18`), **WHEN** the authoring pipeline runs Stage 1 validation, **THEN** the `par_moves` constraint passes and the level proceeds to catalogue publication subject to all other rules passing.

**AC-35 — GetRange: inverted parameters returns empty array, not error**
**GIVEN** the system is in READY state, **WHEN** `GetRange(100, 50)` is called (from_level_id > to_level_id), **THEN** the system returns an empty array of length 0, no `error_code` is present, and the system state remains READY.

**AC-36 — display_name absent from JSON defaults to "Level {level_id}"**
**GIVEN** a JSON record where the `display_name` key is entirely absent and all other fields are valid, **WHEN** a Get Level by ID request retrieves this record, **THEN** `LevelRecord.DisplayName` equals `"Level {level_id}"` as a non-null, non-empty string; the system does not return null or throw.

## Open Questions

| Question | Owner | Target Resolution | Resolution |
|---|---|---|---|
| What is the authoring-time solvability solver's time budget per level? At what board size (color_count × stack_depth) does backtracking search become too slow? | Lead Programmer | Before Beta sprint begins | Open |
| What level editor tooling will be used? (Unity Editor custom tool vs. external spreadsheet + export script vs. other) This determines where the integrated solvability validator runs. | Technical Director | Before month 2 | Open |
| Should `display_name` default to "Level [level_id]" automatically in the loading interface, or must all records have an explicit display_name? | Game Designer | Before Level Progression GDD | **Resolved** — absent or null defaults to `"Level {level_id}"` automatically (see Level Record Schema). |
| What is the minimum playable set that keeps the game functional during DEGRADED state? (Currently implied: any contiguous run starting at level 1.) Should this be a named constant? | Systems Designer | Before Save & Persistence GDD | Open |
| Should the catalogue support remote content delivery (level packs downloaded post-install)? If yes, the cache invalidation policy and `added_version` filtering become critical before Beta. | Technical Director | Before Beta planning | **Resolved** — two-tier Addressables (local bundle + optional remote override) defined in Storage & Loading Contract. |
| `UniTask` vs `Task` for `InitializeAsync()` and `ReloadAsync()` — which async library is adopted? | Technical Director | Before implementation sprint begins | Open — must be resolved via ADR before stories are started. |
