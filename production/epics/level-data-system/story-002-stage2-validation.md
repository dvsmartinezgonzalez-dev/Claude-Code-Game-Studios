# Story 002: Stage 2 Runtime Validation

> **Epic**: Level Data System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/level-data-system.md`
**Requirement**: `TR-LDS-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Level Data Loading Strategy (revised 2026-05-12)
**ADR Decision Summary**: Stage 2 validation runs at load time on every deserialized `LevelRecord`. It is a structural schema check only — it does NOT re-run the authoring-time solvability solver. Records that fail validation are counted in `failed_record_count` and excluded from `_levelCache`; they are not returned to callers.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# logic — no Unity API calls. Safe on any thread (though called on main thread).

**Control Manifest Rules (Foundation layer)**:
- Required: Bolt count invariant — Check 1 (sum) then Check 2 (per-color count), both required
- Required: `failed_record_count / total_record_count > 0.20` → DEGRADED; exactly `0.20` → READY
- Forbidden: Re-running the solvability solver at runtime — Stage 2 is structural only

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-system.md`, scoped to this story:*

- [ ] **AC-03** — Bolt Count Invariant Check 1: `colorStacks = [[1,2,3],[2,1,4,3],…]` (wrong total) → `VALIDATION_FAILED`, `failing_field = color_stacks`, record excluded, counted in `failed_record_count`
- [ ] **AC-04** — Bolt Count Invariant Check 2: correct total but wrong per-color distribution → `VALIDATION_FAILED`, `failing_field = color_stacks`, record excluded
- [ ] **AC-08** — `hint_override = 0` (integer) returned as-is; system does NOT substitute system default
- [ ] **AC-09** — `is_tutorial = true` AND `daily_challenge_eligible = true` → `VALIDATION_FAILED`, `failing_field = daily_challenge_eligible`
- [ ] **AC-16** — Non-contiguous color ID set (e.g., {1,2,4} in a 3-color level) → `VALIDATION_FAILED`, failing field identifies invalid `color_id`
- [ ] **AC-18** — `par_moves` absent from JSON → `VALIDATION_FAILED`, `failing_field = par_moves`
- [ ] **AC-19** — `par_moves = 0` → `VALIDATION_FAILED`, `failing_field = par_moves`
- [ ] **AC-20** — `added_version` wrong format (not `"YYYY.MM"`, non-zero-padded month) → `VALIDATION_FAILED`, `failing_field = added_version`
- [ ] **AC-21** — `display_name = ""` (empty string, not null) → `VALIDATION_FAILED`, `failing_field = display_name`
- [ ] **AC-22** — `hint_override` null/absent → `LevelRecord.HintOverride == null`; system does not coerce to 0
- [ ] **AC-24** — Unknown `schema_version` → quarantined with `VERSION_MISMATCH`; version value included in diagnostic payload
- [ ] **AC-29** — `is_tutorial = true` AND `hint_override = 0` → `VALIDATION_FAILED`, `failing_field = hint_override`
- [ ] **AC-30** — `difficulty_tier ≤ 2` AND `hint_override = 0` AND `is_tutorial = false` → `VALIDATION_FAILED`, `failing_field = hint_override`
- [ ] **AC-31** — `difficulty_tier = 3` AND `hint_override = 0` AND `is_tutorial = false` → PASSES validation; `HintOverride == 0`

---

## Implementation Notes

*Derived from ADR-0004 and GDD validation rules:*

Validation is a single `bool ValidateRecord(LevelRecord record, out LdsValidationError error)` method. Known `schema_version` values are stored in a `HashSet<int> _knownSchemaVersions` (currently `{ 1 }`).

**Bolt Count — two-check algorithm:**
```csharp
// Check 1: total
int total = record.ColorStacks.Sum(stack => stack.Length);
if (total != record.ColorCount * record.StackDepth) → VALIDATION_FAILED

// Check 2: per-color
var colorCounts = new int[record.ColorCount + 1];
foreach (var stack in record.ColorStacks)
    foreach (var colorId in stack)
    {
        if (colorId < 1 || colorId > record.ColorCount) → VALIDATION_FAILED (invalid color ID)
        colorCounts[colorId]++;
    }
for (int c = 1; c <= record.ColorCount; c++)
    if (colorCounts[c] != record.StackDepth) → VALIDATION_FAILED
```

**`added_version` format check**: Regex `^\d{4}\.\d{2}$` (4-digit year, dot, 2-digit zero-padded month). Both `"2026.1"` and `"v1.0"` fail.

**`hint_override` guard checks** (applied in order — first match wins):
1. If `is_tutorial == true` AND `HintOverride == 0` → VALIDATION_FAILED
2. If `DifficultyTier <= 2` AND `HintOverride == 0` → VALIDATION_FAILED

**Known `schema_version`**: maintain a static `HashSet<int>` — currently `{ 1 }`. Any value not in this set → VERSION_MISMATCH (not VALIDATION_FAILED). Store the unknown value in the diagnostic payload.

---

## Estimate

Small — 3–4 hours

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 001: `LevelRecord` type definition (this story uses it, not defines it)
- Story 003: How validation failure counts affect `failure_ratio` and DEGRADED state
- Story 006: Authoring-time Stage 1 validation (`par_moves` vs solver, solvability)

---

## QA Test Cases

- **AC-03**: Bolt Count Invariant — wrong total
  - Given: `LevelRecord` with `color_count=4, stack_depth=4, color_stacks=[[1,2,3],[2,1,4,3],[3,4,1,2],[4,3,2,1]]` (15 bolts, not 16)
  - When: `ValidateRecord(record, out error)` called
  - Then: returns false; `error.Code = VALIDATION_FAILED`; `error.FailingField = "color_stacks"`
  - Edge cases: exactly the right total but wrong distribution (triggers Check 2, not Check 1)

- **AC-04**: Bolt Count Invariant — correct total, wrong per-color
  - Given: `color_stacks=[[1,1,1,1],[2,2,4,3],[3,4,2,2],[4,3,3,3]]` (16 bolts; color 1 appears 5×)
  - When: `ValidateRecord(record, out error)`
  - Then: returns false; `error.Code = VALIDATION_FAILED`; `error.FailingField = "color_stacks"`

- **AC-09**: Tutorial/daily conflict
  - Given: `is_tutorial=true, daily_challenge_eligible=true`
  - When: `ValidateRecord(record, out error)`
  - Then: returns false; `error.FailingField = "daily_challenge_eligible"`

- **AC-18/19**: `par_moves` validation
  - Given: record with `par_moves` absent (C# default 0 after deserialization); separate test with `par_moves = 0`
  - When: `ValidateRecord(record, out error)`
  - Then: both fail with `error.FailingField = "par_moves"`
  - Edge cases: `par_moves = 1` → passes

- **AC-20**: `added_version` format
  - Given: `added_version = "2026.1"` (non-zero-padded) and `added_version = "v1.0"`
  - When: `ValidateRecord(record, out error)`
  - Then: both fail with `error.FailingField = "added_version"`
  - Edge cases: `"2026.01"` → passes; `"2026.12"` → passes

- **AC-29/30**: `hint_override = 0` guards
  - Given (29): `is_tutorial=true, hint_override=0`
  - Given (30): `difficulty_tier=2, is_tutorial=false, hint_override=0`
  - When: `ValidateRecord` called for each
  - Then: both fail; `error.FailingField = "hint_override"`
  - Edge cases: `difficulty_tier=3, hint_override=0` → passes (AC-31)

- **AC-24**: Unknown schema_version
  - Given: `schema_version = 99` (not in `_knownSchemaVersions`)
  - When: `ValidateRecord(record, out error)`
  - Then: returns false; `error.Code = VERSION_MISMATCH`; `error.Payload` contains `"schema_version": 99`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/level-data-system/stage2_validation_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (DONE) — requires `LevelRecord` type
- Unlocks: Story 003 (uses `ValidateRecord` in load pipeline)

---

## Completion Notes
**Completed**: 2026-05-13
**Criteria**: 14/14 passing (all covered by automated tests)
**Deviations**: None
**Test Evidence**: Logic — `tests/unit/level-data-system/stage2_validation_test.cs` (21 NUnit tests)
**Code Review**: APPROVED — two-pass review (2026-05-13)
**Open item**: `ColorCount = 0` produces a false-pass (0×0 invariant). Requires designer ruling — flagged for Story 006 (authoring-time Stage 1 validation).
