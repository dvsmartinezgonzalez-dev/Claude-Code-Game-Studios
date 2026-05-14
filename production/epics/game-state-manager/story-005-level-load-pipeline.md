# Story 005: Level Load Pipeline

> **Epic**: Game State Manager
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: Medium (3–4h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/game-state-manager.md`
**Requirement**: `TR-GSM-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0001 (singleton boot, subscribe-then-check); ADR-0006 (session lifecycle FSM)
**ADR Decision Summary**: GSM follows a strict linear load sequence (L-01 through L-07). Each step must complete before the next. `load_level` is rejected in any non-UNLOADED state. GSM's `LoadLevel(int)` is the entry point; only Level Progression (or equivalent orchestrator) calls it. Stub LDS is used in all unit tests for this pipeline.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff Unity APIs. Coroutine used for async load sequencing is not required at this stage — the pipeline can be synchronous in unit tests with a stub LDS.

**Control Manifest Rules (Core layer)**:
- Required: Subscribe-then-check pattern — mandatory for async-ready singletons. `level_loaded` subscribers must check `IsReady` after subscribing — source: ADR-0001
- Required: Level lifecycle FSM: UNLOADED → LOADING → ACTIVE — no state may be skipped

---

## Acceptance Criteria

*From GDD `design/gdd/game-state-manager.md`, scoped to this story:*

- [ ] **AC-GSM-11** — Level record whose `color_stacks` already satisfy the win condition, that passes L-03 invariants: after L-05 and L-06 complete, GSM is in ACTIVE (not COMPLETE); `level_loaded` emitted; no `level_complete` emitted
- [ ] **AC-GSM-12** — Level successfully loaded with `color_count=3`, `stack_depth=4`, `temp_slot_count=2`, `temp_slot_depth=1`: after L-05 completes, `current_sequence_id=0`; `move_count=0`; undo stack depth=0; `stack_contents` matches level record's `color_stacks` exactly; `temp_slot_contents` = 2 empty arrays of capacity 1; `level_loaded` carries `level_id`, `color_count`, `stack_depth`, `temp_slot_count`, `temp_slot_depth`, `sequence_id=0`
- [ ] **AC-GSM-13** — `load_level(levelId)` received while GSM is in LOADING, ACTIVE, or COMPLETE: no `level_loaded` emitted; GSM remains in current state (test each state as a separate sub-case)
- [ ] **AC-GSM-17** — Stub LDS returns `ready=false` on first readiness query, `ready=true` on second: first `load_level` → `session_load_failed(LEVEL_DATA_UNAVAILABLE)`; GSM → UNLOADED; second `load_level` → succeeds to `level_loaded`; stub readiness query called exactly 2 times (not cached)
- [ ] **AC-GSM-20** — `level_loaded` event carries all 6 required parameters: `level_id`, `color_count`, `stack_depth`, `temp_slot_count`, `temp_slot_depth`, `sequence_id=0` — all must match level record values; all 6 assertions must hold independently

---

## Implementation Notes

*Derived from ADR-0001 and ADR-0006 lifecycle L-01–L-07:*

```csharp
public void LoadLevel(int levelId)
{
    if (_state != GsmState.Unloaded) return; // EC-09: reject in all non-UNLOADED states

    _state = GsmState.Loading;
    _currentLevelId = levelId;

    // L-01: Boot guard — query LDS readiness (never cache this result)
    var readiness = _levelDataSystem.GetReadiness();
    if (!readiness.Ready)
    {
        EmitSessionLoadFailed(GsmFailReason.LevelDataUnavailable, levelId);
        _state = GsmState.Unloaded;
        return;
    }

    // L-02: Fetch record
    LevelRecord record;
    try { record = _levelDataSystem.GetLevel(levelId); }
    catch (LevelDataException ex)
    {
        EmitSessionLoadFailed(GsmFailReason.LevelRecordError, levelId);
        _state = GsmState.Unloaded;
        return;
    }

    // L-03: Invariant checks (Story 004)
    if (!RunInvariantChecks(record, levelId)) { _state = GsmState.Unloaded; return; }

    // L-04: Pre-won board detection — log warning but do NOT auto-win
    if (CheckWinCondition(record.ColorStacks, record.StackDepth))
        Debug.LogWarning($"[GSM] Pre-won board detected: level_id={levelId}");

    // L-05: Instantiate board state
    _stackContents = record.ColorStacks.Select(s => new List<int>(s)).ToArray();
    _tempSlotContents = new List<int>[record.TempSlotCount];
    for (int i = 0; i < record.TempSlotCount; i++)
        _tempSlotContents[i] = new List<int>(record.TempSlotDepth);
    _stackDepth = record.StackDepth;
    _tempSlotDepth = record.TempSlotDepth;
    _tempSlotCount = record.TempSlotCount;
    _colorCount = record.ColorCount;
    _currentSequenceId = 0;
    _undoStack.Clear();
    _moveCount = 0;

    // L-06: Emit level_loaded
    OnLevelLoaded?.Invoke(levelId, record.ColorCount, record.StackDepth,
                          record.TempSlotCount, record.TempSlotDepth, 0L);

    // L-07: Transition to ACTIVE
    _state = GsmState.Active;
}
```

**L-01 readiness query is never cached**: Each call to `LoadLevel` runs a fresh `GetReadiness()`. Do not store the result from a prior call.

**L-04 pre-won board**: GSM logs a warning but continues to L-05. Win detection is owned by Sort Mechanic; it will fire `puzzle_solved()` on the `level_loaded` handler if appropriate. GSM must not auto-win here.

**EC-09**: `if (_state != GsmState.Unloaded) return` — this guard handles LOADING, ACTIVE, and COMPLETE. All three must silently reject `load_level` with no event emitted.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 004: L-03 invariant check implementation (called here, tested separately)
- Story 008: TEARDOWN state, `exit_level` command, board serialization on app background (SER-01/02/03)

---

## QA Test Cases

- **AC-GSM-11**: Pre-won board does not auto-win
  - Given: Level record where all `color_stacks` are full and monochromatic (`color_count=2, stack_depth=3, stacks=[[1,1,1],[2,2,2]]`), passes L-03; event spy
  - When: `LoadLevel(levelId)` called
  - Then: spy contains `level_loaded` but NOT `level_complete`; GSM state = ACTIVE

- **AC-GSM-12**: Board initializes to exact spec
  - Given: Level record `color_count=3, stack_depth=4, temp_slot_count=2, temp_slot_depth=1, color_stacks=[[1,2,3,4],[2,1,4,3],[3,4,1,2]]`; event spy
  - When: `LoadLevel` completes
  - Then: `current_sequence_id=0`; `move_count=0`; undo stack depth=0; `stack_contents[0]=[1,2,3,4]` (exact match); `temp_slot_contents` = 2 empty lists; `level_loaded` event carries `(levelId, 3, 4, 2, 1, 0)`
  - Edge cases: `temp_slot_count=0` → `temp_slot_contents` empty collection (no arrays allocated)

- **AC-GSM-13**: load_level rejected in non-UNLOADED states
  - Test each sub-case independently: LOADING, ACTIVE, COMPLETE
  - Given: GSM in each state; event spy
  - When: `load_level` called
  - Then: spy records no `level_loaded`; GSM remains in original state

- **AC-GSM-17**: Readiness query is fresh per attempt
  - Given: Stub LDS — `GetReadiness()` call #1 returns `Ready=false`; call #2 returns `Ready=true`; call counter accessible
  - When: first `LoadLevel` → `session_load_failed`; GSM → UNLOADED; second `LoadLevel` called
  - Then: stub's call counter = 2; second load reaches `level_loaded`

- **AC-GSM-20**: level_loaded all 6 parameters
  - Given: Level record with known values for all 5 board parameters
  - When: `LoadLevel` succeeds
  - Then: `level_loaded` spy entry has all 6 fields matching (assert each field independently): `levelId`, `colorCount`, `stackDepth`, `tempSlotCount`, `tempSlotDepth`, `sequenceId=0`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/game-state-manager/level_load_pipeline_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (DONE) — invariant checks called at L-03; Story 001 (DONE) — board state structures established
- Unlocks: Story 008 (TEARDOWN and app lifecycle builds on ACTIVE state from this pipeline)
