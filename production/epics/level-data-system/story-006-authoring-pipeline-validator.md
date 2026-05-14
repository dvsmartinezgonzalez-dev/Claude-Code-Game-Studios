# Story 006: Authoring Pipeline Validator (Editor-Only)

> **Epic**: Level Data System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: Small (2–3h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/level-data-system.md`
**Requirement**: `TR-LDS-002` (Stage 1 authoring validation), `TR-SORT-010` (column cap)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004 (Stage 1 validation pipeline); ADR-0013 (column cap `color_count + temp_slot_count ≤ 8`)
**ADR Decision Summary**: Stage 1 validation runs at authoring time as an editor script — it is NOT shipped in the build. It catches issues before levels enter the Addressables group. The `LevelRecordValidator` editor class validates `par_moves` against the solver's computed minimum, enforces the column cap, and rejects levels that would fail at runtime before they pollute the catalogue.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Editor-only code (`#if UNITY_EDITOR` or in an `Editor/` folder). No post-cutoff APIs involved.

**Control Manifest Rules (Foundation layer)**:
- Required: Column cap `color_count + temp_slot_count ≤ 8` — hard authoring constraint (ADR-0013)
- Required: `par_moves ≥ solver_min_moves AND par_moves ≤ solver_min_moves + 10`
- Forbidden: Shipping this validator in the player build — editor-only assembly

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-system.md`, scoped to this story:*

- [ ] **AC-32** — Authoring pipeline: `par_moves = 6` when `solver_min_moves = 8` (below minimum) → export blocked; error identifies `par_moves` as below `solver_min_moves`
- [ ] **AC-33** — `par_moves = 19` when `solver_min_moves = 8` (above `solver_min_moves + 10 = 18`) → export blocked; error identifies `par_moves` as exceeding ceiling
- [ ] **AC-34** — `par_moves = 12` when `solver_min_moves = 8` (within range 8–18) → `par_moves` check passes; level proceeds to other validation
- [ ] Column cap: `color_count = 6, temp_slot_count = 3` (total = 9 > 8) → validation fails with column cap error (ADR-0013)
- [ ] Column cap: `color_count = 6, temp_slot_count = 2` (total = 8) → column cap check passes
- [ ] Validator is in an Editor assembly and does NOT compile into player builds

---

## Implementation Notes

*Derived from ADR-0004 and ADR-0013:*

```csharp
// In Assets/Editor/LevelData/LevelRecordValidator.cs
// Placed in an Editor/ folder — auto-excluded from player builds by Unity
public static class LevelRecordValidator
{
    public static ValidationResult Validate(LevelRecord record, int solverMinMoves)
    {
        // Column cap (ADR-0013)
        int columns = record.ColorCount + record.TempSlotCount;
        if (columns > 8)
            return ValidationResult.Fail(
                $"Column cap exceeded: {record.ColorCount} color + {record.TempSlotCount} temp " +
                $"= {columns} > 8. Max allowed: 8 (ADR-0013).");

        // par_moves vs solver range
        if (record.ParMoves < solverMinMoves)
            return ValidationResult.Fail(
                $"par_moves ({record.ParMoves}) is below solver_min_moves ({solverMinMoves}). " +
                $"par_moves must be ≥ {solverMinMoves}.");

        int parMovesMax = solverMinMoves + 10;
        if (record.ParMoves > parMovesMax)
            return ValidationResult.Fail(
                $"par_moves ({record.ParMoves}) exceeds solver_min_moves + 10 ({parMovesMax}). " +
                $"par_moves must be ≤ {parMovesMax}.");

        return ValidationResult.Pass();
    }
}

public readonly struct ValidationResult
{
    public bool IsValid { get; }
    public string Error { get; }

    private ValidationResult(bool valid, string error) { IsValid = valid; Error = error; }
    public static ValidationResult Pass() => new(true, null);
    public static ValidationResult Fail(string error) => new(false, error);
}
```

This validator is called from the level export step in the authoring pipeline. The `solverMinMoves` value comes from the authoring-time solvability solver output — not from the `LevelRecord` itself (the solver result is stored in a separate authoring manifest and not shipped to the client).

The validator also serves as the entry point for integrating CI-level validation: a Unity Editor batch-mode script can invoke `LevelRecordValidator.Validate()` on all candidate JSON files before they are committed to the Addressables group.

---

## Out of Scope

*Handled by neighbouring stories or future work:*

- The solvability solver itself is not implemented here — this story only validates the `par_moves` constraint against a solver result that is provided as input
- Stage 2 runtime validation (Story 002)
- Other authoring constraints (e.g., difficultyTier consistency, addedVersion format) — these are validated by Stage 2 at runtime; duplicating them in Stage 1 is optional tooling

---

## QA Test Cases

- **AC-32**: `par_moves` below solver minimum
  - Given: `par_moves=6`, `solverMinMoves=8`
  - When: `LevelRecordValidator.Validate(record, 8)`
  - Then: `IsValid == false`; `Error` mentions `par_moves` and `solver_min_moves`

- **AC-33**: `par_moves` above solver maximum
  - Given: `par_moves=19`, `solverMinMoves=8` (max = 18)
  - When: `Validate(record, 8)`
  - Then: `IsValid == false`; `Error` mentions exceeding `solver_min_moves + 10`

- **AC-34**: `par_moves` within valid range
  - Given: `par_moves=12`, `solverMinMoves=8` (valid: 8 ≤ 12 ≤ 18)
  - When: `Validate(record, 8)`
  - Then: `IsValid == true` for the `par_moves` check; other checks not blocked by this criterion

- **Column cap — fail**: `color_count=6, temp_slot_count=3` (= 9 columns)
  - When: `Validate(record, any)` 
  - Then: `IsValid == false`; `Error` mentions column cap; failure reported before `par_moves` is checked (column cap evaluated first)

- **Column cap — pass**: `color_count=6, temp_slot_count=2` (= 8 columns)
  - When: `Validate(record, 8)` (assuming par_moves valid)
  - Then: column cap check passes; validator proceeds to `par_moves` check

---

## Test Evidence

**Story Type**: Logic (editor tooling)
**Required evidence**: `tests/unit/level-data-system/authoring_validator_test.cs` — must exist and pass (can run via Unity Test Framework in Editor mode)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (DONE) — uses `LevelRecord` type
- Unlocks: Level authoring pipeline can run validation before levels are added to Addressables

---

## Completion Notes

**Completed**: 2026-05-14
**Criteria**: 5/6 verified; AC-6 (editor assembly exclusion) DEFERRED — structural property enforced by Unity's `Editor/` folder convention, not assertable at runtime
**Deviations**: None blocking. Advisory items:
  - `Validate(record, solverMinMoves)` extends ADR-0013's single-parameter signature (intentional — par_moves check is in story scope)
  - Constants `8` and `10` are architectural design constants cited to ADR-0013, not configurable tuning knobs
  - `ValidationResult.Fail(null)` has no guard (no current caller passes null — add ArgumentNullException guard in follow-up)
  - `ValidationResult` declared in same file as `LevelRecordValidator` (convention: own file — move in polish pass)
**Test Evidence**: Logic — `tests/unit/level-data-system/authoring_validator_test.cs` (8 tests, all ACs covered)
**Code Review**: Complete — APPROVED WITH SUGGESTIONS (S-1: null guard in Fail(); S-2: ADR-0013 extension comment; S-3: ValidationResult own file; S-4: pin numeric value in AC-33 assertion)
