# Story 008: App Lifecycle and Board Serialization

> **Epic**: Game State Manager
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: Medium (3–4h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/game-state-manager.md`
**Requirement**: `TR-GSM-008`, `TR-GSM-011`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0001 (DDOL lifecycle, `OnApplicationPause`); ADR-0006 (SER-01/02/03, EC-06)
**ADR Decision Summary**: On `OnApplicationPause(true)`, GSM serializes board state to Save & Persistence. On foreground restore, GSM deserializes and emits `board_state_changed`. If deserialization fails, GSM emits `session_load_failed(SAVE_CORRUPT)` and returns to UNLOADED. Undo stack is session-only — never serialized. If Sort Mechanic was in BOLT_SELECTED on background, GSM increments `current_sequence_id` on restore (EC-06).

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `OnApplicationPause` is a stable Unity lifecycle method. Stable on both iOS and Android. No post-cutoff APIs.

**Control Manifest Rules (Core layer + Foundation)**:
- Required: Subscribe-then-check pattern — `OnBoardStateChanged` subscribers must check current state on subscribe, not only event receipt — source: ADR-0001
- Forbidden: `async void OnApplicationPause()` — Unity returns control to OS at first `await`; write may never complete under iOS suspension. Use synchronous Save & Persistence methods in the pause handler — source: ADR-0003 / Foundation forbidden patterns

---

## Acceptance Criteria

*From GDD `design/gdd/game-state-manager.md` Group E (SER rules and EC-06 — integration tier):*

- [ ] **SER-01** — GSM in ACTIVE with a known board state snapshot; `OnApplicationPause(true)` fires: Save & Persistence stub receives all required fields: `stack_contents[]`, `temp_slot_contents[]`, `stack_depth`, `temp_slot_depth`, `temp_slot_count`, `color_count`, `move_count`, `current_sequence_id`, `level_id`, `gsm_state`; undo stack NOT included in serialized payload
- [ ] **SER-02** — Serialized state with `gsm_state=ACTIVE`; foreground restore (`OnApplicationPause(false)`): board state deserialized correctly; `board_state_changed(sequenceId, moveCount)` emitted; GSM in ACTIVE state
- [ ] **SER-03** — Corrupt or missing save data on foreground restore: `session_load_failed(SAVE_CORRUPT, levelId)` emitted; GSM → UNLOADED; all partial state cleared
- [ ] **EC-06** — GSM ACTIVE while Sort Mechanic is known to be in BOLT_SELECTED at background time (signal injected via test seam); foreground restore: `current_sequence_id` incremented; `board_state_changed` emitted with new seqId

- [ ] **L-08** — `ExitLevel()` received while GSM is in ACTIVE or COMPLETE: (1) `stack_contents` and `temp_slot_contents` arrays cleared; (2) undo stack depth = 0; (3) `move_count` = 0; (4) `OnLevelUnloaded(levelId)` emitted with the level's ID; (5) `GSM.LifecycleState == Unloaded`; (6) `LoadLevel` accepted again after teardown completes (test each starting state — ACTIVE and COMPLETE — as separate sub-cases)
- [ ] **L-08-LOADING** — `ExitLevel()` received while GSM is in LOADING (load cancellation): (1) partial state discarded; (2) `OnLevelUnloaded(null)` emitted (null level ID — cancelled before L-02); (3) GSM → UNLOADED; (4) `OnLevelLoaded` NOT emitted; (5) `OnSessionLoadFailed` NOT emitted

---

## Implementation Notes

*Derived from ADR-0001 and ADR-0006 SER rules:*

```csharp
private void OnApplicationPause(bool paused)
{
    if (paused)
    {
        // SER-01: serialize synchronously (never async void here — ADR-0003 forbidden)
        if (_state == GsmState.Active || _state == GsmState.Complete)
        {
            var snapshot = BuildBoardSnapshot(); // undo stack excluded
            _saveSystem.WriteBoardSnapshot(snapshot); // synchronous call
        }
    }
    else
    {
        // Foreground restore
        var snapshot = _saveSystem.ReadBoardSnapshot();
        if (snapshot == null || !snapshot.IsValid)
        {
            EmitSessionLoadFailed(GsmFailReason.SaveCorrupt, snapshot?.LevelId);
            _state = GsmState.Unloaded;
            ClearAllState();
            return;
        }
        // SER-02: restore board state
        RestoreFromSnapshot(snapshot);

        // EC-06: if Sort Mechanic was in BOLT_SELECTED, increment seqId
        if (snapshot.WasInBoltSelected)
            _currentSequenceId++;

        OnBoardStateChanged?.Invoke(_currentSequenceId, _moveCount);
    }
}
```

**Undo stack is session-only** (SER-01): Do not serialize `_undoStack`. On foreground restore, the undo stack is empty. Players lose undo history on app kill (acceptable per GDD open question — design decision deferred).

**SER-02 COMPLETE state**: If `gsm_state=COMPLETE` in the snapshot, restore to COMPLETE — do NOT re-emit `level_complete`. Level Progression received it before backgrounding.

**EC-06 implementation**: The test seam for "Sort Mechanic was in BOLT_SELECTED" can be implemented as a `bool _sortMechanicWasInBoltSelected` flag that Sort Mechanic sets via an interface call before `OnApplicationPause` returns. The SEO ordering guarantee (Sort Mechanic's pause handler fires before GSM's — from GDD EC-14) ensures the flag is set before GSM reads it.

**`async void OnApplicationPause` is forbidden** — control returns to the OS at the first `await`. Any pending I/O is cancelled. All Save & Persistence calls in this handler must be synchronous.

**Interface additions required (this story)**:

Add to `IGameStateManager`:
```csharp
/// <summary>
/// Initiates TEARDOWN: clears board state, emits OnLevelUnloaded, transitions to UNLOADED.
/// If called during LOADING, cancels the load and emits OnLevelUnloaded(null).
/// No-op in UNLOADED state. Called exclusively by Level Progression.
/// </summary>
void ExitLevel();

/// <summary>
/// Fired when TEARDOWN completes (L-08) or a LOADING cancellation completes.
/// Carries levelId (null if cancellation during LOADING). Level Progression waits
/// for this before issuing the next LoadLevel call.
/// Parameters: (int? levelId)
/// </summary>
event Action<int?> OnLevelUnloaded;
```

Also add to `IGameStateManager`:
```csharp
/// <summary>
/// Test seam for EC-06: injects the Sort Mechanic BOLT_SELECTED flag before OnApplicationPause.
/// In production, Sort Mechanic calls this before its own OnApplicationPause returns (SEO ordering).
/// </summary>
void SetSortMechanicWasInBoltSelectedForTesting(bool value);
```

**Save & Persistence interface**: This story creates a minimal `IBoardSnapshotSystem` in `src/GameStateManager/` for GSM injection:
```csharp
public interface IBoardSnapshotSystem
{
    void WriteBoardSnapshotSync(BoardSnapshot snapshot); // W-2 synchronous path
    BoardSnapshot? ReadBoardSnapshot();
}
```
The full `SaveSystem` implementation is out of scope (Save & Persistence epic). The integration test provides a stub.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 005: Normal level load pipeline (L-01 through L-07) — this story handles restoration from serialized state only
- Save & Persistence epic: the full `ISaveSystem` / `SaveSystem` implementation — this story creates only `IBoardSnapshotSystem` (minimal interface for GSM injection)
- Undo stack serialization — undo history is session-only and is never persisted (GDD SER-01 explicit)

---

## QA Test Cases

- **SER-01**: Board state serialized on background (no undo stack)
  - Given: GSM ACTIVE, specific board state with 2 moves committed, undo stack depth=2, known field values; Save stub that records calls
  - When: `OnApplicationPause(true)` fires
  - Then: stub received snapshot containing all 10 required fields matching live values; undo stack entries NOT in snapshot payload

- **SER-02**: Foreground restore from ACTIVE snapshot
  - Given: Save stub with valid ACTIVE snapshot saved from SER-01; event spy
  - When: `OnApplicationPause(false)` fires
  - Then: board state matches snapshot; `board_state_changed` in spy with (seqId, moveCount) from snapshot; GSM state = ACTIVE

- **SER-03**: Deserialization failure
  - Given: Save stub returns null (missing) or `IsValid=false` (corrupt); event spy
  - When: `OnApplicationPause(false)` fires
  - Then: `session_load_failed(SAVE_CORRUPT)` in spy; GSM state = UNLOADED; all field reads return invalid/default values

- **EC-06**: BOLT_SELECTED on background — seqId increment on restore
  - Given: GSM ACTIVE; test seam injects `_sortMechanicWasInBoltSelected=true` before pause; spy
  - When: background then foreground restore
  - Then: restored seqId = snapshot's seqId + 1; `board_state_changed` emitted with incremented seqId

- **L-08**: ExitLevel from ACTIVE clears all state
  - Given: GSM ACTIVE with known board state (2 moves committed, undo stack depth=2); event spy
  - When: `ExitLevel()` called
  - Then: `StackContents` all empty; `UndoStackDepth=0`; `MoveCount=0`; `OnLevelUnloaded(levelId)` in spy; `GSM.LifecycleState==Unloaded`; subsequent `LoadLevel(id)` accepted without error

- **L-08-LOADING**: ExitLevel during LOADING cancels load
  - Given: GSM in LOADING (state injected via seam — simulates mid-load); event spy
  - When: `ExitLevel()` called
  - Then: `OnLevelUnloaded(null)` in spy; `OnLevelLoaded` NOT in spy; `OnSessionLoadFailed` NOT in spy; `GSM.LifecycleState==Unloaded`

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/game-state-manager/app_lifecycle_test.cs` — must exist and pass (uses Save & Persistence stub)

**Status**: [x] Created — `tests/integration/game-state-manager/AppLifecycle_Test.cs`

---

## Dependencies

- Depends on: Story 005 (DONE) — ACTIVE state and level load pipeline established; Save & Persistence stub interface available
- Unlocks: None — this is the last story for the Game State Manager epic

---

## Completion Notes

**Completed**: 2026-05-16
**Criteria**: 6/6 passing
**Deviations**: ADVISORY — `GSMLifecycleState.Teardown` declared but never assigned as intermediate state; `ExitLevel` transitions directly ACTIVE→UNLOADED (synchronous teardown). Enum value reserved for future use. Tests assert `Unloaded` — functionally correct.
**Test Evidence**: Integration test at `tests/integration/game-state-manager/AppLifecycle_Test.cs` (20 tests; includes S-1/S-2 advisory tests added at review time)
**Code Review**: Complete — R-1 applied: null guard on `_levelDataSystem` in `LoadLevel` (line 212) prevents NRE when LDS is unavailable; S-1/S-2 advisory tests added
**Code Review (GetByFilter)**: `src/LevelData/LevelDataSystem.cs:265` null guard was pre-existing — no action required

**Addendum — 2026-06-08**: The "Out of Scope" deferral of the concrete `IBoardSnapshotSystem` implementation has been resolved. `BoardSnapshotSystem` (`Assets/_Project/Scripts/GameStateManager/BoardSnapshotSystem.cs`) is now implemented — a synchronous PlayerPrefs-backed store (Newtonsoft.Json, `bs.board_snapshot` key, single-use consumption) — and wired into `GameStateManager.Awake()`. SER-01/02/03 are now functional end-to-end in production (previously `_boardSnapshotSystem` was always `null` at runtime). Covered by 5 new unit tests: `Tests/unit/game-state-manager/BoardSnapshotSystem_Test.cs`. No `SaveSystem` changes required — see file header comment for rationale (sync-write constraint vs. SaveSystem's async atomic-write design).
