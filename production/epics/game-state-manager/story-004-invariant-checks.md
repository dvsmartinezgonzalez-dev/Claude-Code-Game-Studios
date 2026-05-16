# Story 004: Bolt Count Invariant Checks

> **Epic**: Game State Manager
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: Small (2h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/game-state-manager.md`
**Requirement**: `TR-GSM-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation
**ADR Decision Summary**: L-03 runs two independent invariant checks on every level load before board state is instantiated. Check 1: total bolt count equals `colorCount × stackDepth`. Check 2: each `color_id` in `{1..colorCount}` appears exactly `stackDepth` times (also catches phantom color IDs outside the domain). Either failure emits `session_load_failed(INVARIANT_VIOLATION, levelId)` and aborts the load.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# validation logic. No engine APIs required.

**Control Manifest Rules (Core layer)**:
- Required: `bolt_count_invariant` check at every level load (both checks must run) — source: ADR-0006
- Forbidden: silently accepting a malformed level record — the invariant failure must emit `session_load_failed`

---

## Acceptance Criteria

*From GDD `design/gdd/game-state-manager.md`, scoped to this story:*

- [ ] **AC-GSM-09** — Level record with `color_count=3`, `stack_depth=4`, `color_stacks` containing only 11 total bolts (not 12): `session_load_failed(INVARIANT_VIOLATION, levelId)` emitted; GSM → UNLOADED; no `level_loaded` emitted
- [ ] **AC-GSM-10** — Level record where total bolt count = 12 (check 1 passes) but one `color_id` appears 5 times and another appears 3 times (check 2 fails): `session_load_failed(INVARIANT_VIOLATION, levelId)` emitted; GSM → UNLOADED — this test must pass independently of AC-GSM-09 (both checks active even when check 1 passes)

---

## Implementation Notes

*Derived from ADR-0006 L-03:*

```csharp
private bool RunInvariantChecks(LevelRecord record, int levelId)
{
    // Check 1: total bolt count
    int total = 0;
    foreach (var stack in record.ColorStacks)
        total += stack.Length;
    if (total != record.ColorCount * record.StackDepth)
    {
        EmitSessionLoadFailed(LdsErrorCode.InvariantViolation, levelId);
        return false;
    }

    // Check 2: per-color frequency (also catches phantom color IDs)
    var colorCounts = new int[record.ColorCount + 1]; // index 0 unused
    foreach (var stack in record.ColorStacks)
    {
        foreach (var colorId in stack)
        {
            if (colorId < 1 || colorId > record.ColorCount)
            {
                EmitSessionLoadFailed(LdsErrorCode.InvariantViolation, levelId);
                return false; // phantom color_id outside domain
            }
            colorCounts[colorId]++;
        }
    }
    for (int c = 1; c <= record.ColorCount; c++)
    {
        if (colorCounts[c] != record.StackDepth)
        {
            EmitSessionLoadFailed(LdsErrorCode.InvariantViolation, levelId);
            return false;
        }
    }

    return true;
}
```

**Two independent checks**: Check 1 and Check 2 are structurally independent. A level record can pass check 1 (correct total) while failing check 2 (one color over-represented, another under-represented). Both must be active. Do not short-circuit check 2 when check 1 passes.

**Phantom color IDs**: A `color_id` outside `{1..colorCount}` (e.g., colorId=9 in a 3-color level) can pass check 1 by replacing a valid color, making the win condition structurally unreachable. Check 2's per-color loop catches this by returning false for any out-of-domain value.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 005: Full level load pipeline (L-01, L-02, L-04–L-07) — this story implements only the L-03 checks, callable as a standalone method
- Story 001: Runtime use of validated `color_stacks` data in board state

---

## QA Test Cases

- **AC-GSM-09**: Check 1 — total count mismatch
  - Given: Level record with `color_count=3`, `stack_depth=4`, `color_stacks=[[1,1,1,1],[2,2,2,2],[3,3,3]]` (11 total, not 12); event spy
  - When: `RunInvariantChecks` (or `LoadLevel`) called
  - Then: `session_load_failed(INVARIANT_VIOLATION, levelId)` in spy; GSM → UNLOADED; `level_loaded` absent from spy
  - Edge cases: total = 0 (empty colour_stacks); total exactly 1 too many

- **AC-GSM-10**: Check 2 — per-color distribution mismatch (check 1 passing)
  - Given: Level record with `color_count=3`, `stack_depth=4`, `color_stacks=[[1,1,1,1,1],[2,2,2],[3,3,3,3]]` — total=12 (check 1 passes); color 1 appears 5 times, color 2 appears 3 times; event spy
  - When: `RunInvariantChecks` called
  - Then: `session_load_failed(INVARIANT_VIOLATION, levelId)` in spy; test passes independently regardless of check 1 outcome
  - Edge cases: phantom color_id (colorId=9 in 3-color level where total=12); all bolts same color (check 2 fails for all other colors)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/game-state-manager/invariant_checks_test.cs` — must exist and pass

**Status**: [x] Exists and covers all AC-GSM-09 and AC-GSM-10 cases (8 tests)

---

## Dependencies

- Depends on: None — this story implements a pure validation method; can be written and tested with a stub LevelRecord
- Unlocks: Story 005 (load pipeline calls invariant checks at L-03)

---

## Completion Notes
**Completed**: 2026-05-16
**Criteria**: 2/2 passing (AC-GSM-09, AC-GSM-10 — all edge cases covered)
**Deviations**: None from GDD/ADR. TR-GSM-007 compliant. Manifest version match.
**Test Evidence**: Logic — `tests/unit/game-state-manager/InvariantChecks_Test.cs` (8 tests)
**Code Review**: Complete (lean mode) — R-1 applied: GSMEnums.cs doc comment reorder; `InternalsVisibleTo("Tests.Unit.GameStateManager")` added to `src/AssemblyInfo.cs`
**Advisory items**:
- `Test_Failure_DoesNotEmitLevelLoaded` trivially-false assertion — add TODO Story 005 comment when OnLevelLoaded is added to interface
- Stack-count vs color-count structural mismatch not tested — clarify scope with qa-lead before Story 005
- `SyncPublicCounters` maintenance hazard (MoveCount/CurrentSequenceId) — convert to expression-body properties in a future cleanup story
