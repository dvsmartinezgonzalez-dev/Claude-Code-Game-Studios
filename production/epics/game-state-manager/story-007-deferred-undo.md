# Story 007: Deferred Undo and MOVE_EXECUTING Exit Ordering

> **Epic**: Game State Manager
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: Medium (3–4h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/game-state-manager.md`
**Requirement**: `TR-GSM-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation
**ADR Decision Summary**: `undo_requested` during MOVE_EXECUTING (animation in flight) is deferred — stored as a single pending request. It fires after MOVE_EXECUTING exits (on the IDLE path only, after `OnMoveExecutingExited`). The deferred undo fires BEFORE win evaluation. If `puzzle_solved()` arrives before exit, the deferred undo is discarded. Watchdog exit also processes the deferred undo. Queue capacity = 1; second requests are dropped.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff Unity APIs. All logic on main thread.

**Control Manifest Rules (Core layer)**:
- Required: Deferred undo during MOVE_EXECUTING: UndoRequested sets `_pendingUndo`; executed on `OnMoveExecutingExited` (IDLE path only) — source: ADR-0006
- Required: Cancel watchdog in every MOVE_EXECUTING exit including TEARDOWN and `OnDestroy`

---

## Acceptance Criteria

*From GDD `design/gdd/game-state-manager.md`, scoped to this story:*

- [ ] **AC-GSM-14** — Board where one legal move wins; `move_committed` in progress (MOVE_EXECUTING); `undo_requested` arrives (deferred); animation completion received → (1) deferred undo fires first: board reverts to pre-final-move state; (2) win evaluation runs on reverted board; (3) `puzzle_solved` NOT emitted; (4) GSM stays ACTIVE; (5) `board_state_changed` emitted from undo
- [ ] **AC-GSM-15** — MOVE_EXECUTING active, `undo_requested` deferred; `puzzle_solved()` received before exit → (1) deferred undo cleared and never executed; (2) GSM → COMPLETE; (3) `level_complete` emitted; (4) when MOVE_EXECUTING later exits, no undo fires, board unchanged
- [ ] **AC-GSM-16** — MOVE_EXECUTING active, `undo_requested` deferred, watchdog timer fires → (1) `current_sequence_id` incremented; (2) `board_refresh_forced(newSeqId)` emitted; (3) deferred undo subsequently processed: board reverts committed move, `move_count` decrements, `board_state_changed` emitted
- [ ] **AC-GSM-21** — MOVE_EXECUTING active, one `undo_requested` already deferred; second `undo_requested` arrives → queue length remains 1; after MOVE_EXECUTING exits exactly one undo fires; board reverts exactly one move

---

## Implementation Notes

*Derived from ADR-0006 UND-03 and EC-05, EC-10, EC-11, EC-17:*

```csharp
private bool _pendingUndo = false; // queue capacity = 1 (EC-17)

public void UndoRequested()
{
    if (_state != GsmState.Active) return;
    if (_undoStack.Count == 0) return;

    // UND-03: if animation is in flight, defer — do not silently drop
    if (_isAnimationInFlight)
    {
        _pendingUndo = true; // second call while already true is silently dropped (EC-17)
        return;
    }

    // Synchronous path (Story 002)
    ExecuteUndo();
}

private void HandleMoveExecutingExited(long seqId)
{
    CancelWatchdog(); // Story 006

    // EC-10: deferred undo fires BEFORE any further processing
    if (_pendingUndo)
    {
        _pendingUndo = false;
        if (_undoStack.Count > 0)
            ExecuteUndo();
    }
    _isAnimationInFlight = false;
}

private void HandlePuzzleSolved()
{
    // EC-05: discard deferred undo — COMPLETE does not process undo
    _pendingUndo = false;
    // WIN-01 transition (Story 003)
    ...
}

// Watchdog fires (Story 006) → HandleMoveExecutingExited equivalent:
// WDG-01 increments seqId and emits board_refresh_forced
// then processes _pendingUndo (same ordering as normal exit)
```

**EC-10 ordering contract**: The deferred undo fires first, then the code path that previously would evaluate the win condition is entered. In the Sort Mechanic GDD, win evaluation is Sort Mechanic's responsibility — GSM does not evaluate the win condition independently. GSM's responsibility is: flush the deferred undo before any further state transitions.

**EC-11 (watchdog + deferred undo)**: The watchdog exit path must also flush `_pendingUndo`. The GDD states: "deferred undo fires after the watchdog-induced MOVE_EXECUTING exit, consistent with EC-10's ordering rule." Implement both normal and watchdog exit through the same flush path.

**`_isAnimationInFlight` flag**: Set to `true` when `move_committed` is processed (BSM-01). Cleared in `HandleMoveExecutingExited` and in the watchdog. Used to distinguish deferred (animation in flight) from synchronous (animation complete) undo requests.

**Performance impact**: Negligible — deferred undo is a single boolean flag check on every `UndoRequested()` call and a conditional `ExecuteUndo()` call on `MOVE_EXECUTING` exit. No allocation. No per-frame cost.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 002: Synchronous undo path (`ExecuteUndo()` implementation)
- Story 003: WIN-01 COMPLETE transition — this story calls it but does not re-implement it
- Story 006: Watchdog timer — this story assumes the watchdog emits `board_refresh_forced` per Story 006

---

## QA Test Cases

- **AC-GSM-14**: Deferred undo fires before win evaluation
  - Given: `color_count=2, stack_depth=2`; board `stacks=[[1,2],[2]]`; event spy
  - When: `SimulateMoveCommitted(src=0, dst=1, colorId=2, seqId=N)`
          [board becomes `[[1],[2,2]]` — stack 1 is full+monochromatic; would trigger win if evaluated now]
          `UndoRequested()` → deferred (`_pendingUndo=true`)
          `SimulateMoveExecutingExited(N+1)`
  - Then: `board_state_changed` emitted (from deferred undo); board reverts to `[[1,2],[2]]`; `level_complete` NOT in spy; `GSM.LifecycleState == Active`
  - Edge cases: animation completes before undo arrives (synchronous path via Story 002 — `_pendingUndo` is false, deferred path not taken)

- **AC-GSM-15**: puzzle_solved discards deferred undo
  - Given: MOVE_EXECUTING active; `undo_requested` deferred (`_pendingUndo=true`); `puzzle_solved()` received
  - When: `HandleMoveExecutingExited` called (simulating later exit)
  - Then: `_pendingUndo` cleared; undo not executed; spy shows only `level_complete`; board unchanged post-exit

- **AC-GSM-16**: Watchdog exit processes deferred undo
  - Given: MOVE_EXECUTING active; `undo_requested` deferred; watchdog fires (seqId=N at time of `move_committed`)
  - When: WatchdogCoroutine yield completes
  - Then: (1) `current_sequence_id=N+1`; (2) `board_refresh_forced(N+1)` emitted; (3) deferred undo executes: board reverts committed move, `move_count` decremented, `board_state_changed` emitted

- **AC-GSM-21**: Queue capacity = 1
  - Given: MOVE_EXECUTING active; first `undo_requested` queued
  - When: second `undo_requested` arrives; then `HandleMoveExecutingExited` called
  - Then: exactly one undo fires; board reverts exactly one move; move_count decrements by exactly 1

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/game-state-manager/deferred_undo_test.cs` — must exist and pass

**Status**: [x] Exists — `tests/unit/game-state-manager/DeferredUndo_Test.cs` (14 tests covering AC-GSM-14, 15, 16, 21)

---

## Dependencies

- Depends on: Story 001 (DONE), Story 002 (DONE), Story 003 (DONE), Story 006 (DONE) — deferred undo integrates all four: board mutation, synchronous undo, COMPLETE state, watchdog
- Unlocks: None — this is the last runtime Logic story for the GSM epic

---

## Completion Notes
**Completed**: 2026-05-16
**Criteria**: 4/4 passing (AC-GSM-14, AC-GSM-15, AC-GSM-16, AC-GSM-21 — all edge cases covered)
**Deviations**:
- ADVISORY: `Test_SecondUndoRequest_OnlyOneUndoFires_BoardRevertsExactlyOneMove` unnecessarily re-creates GSM inside test body using same board as SetUp — cosmetic only.
- ADVISORY: Minor comment inaccuracy in `Test_UndoRequested_WhenNotInFlight_ExecutesSynchronously` — test logic correct, comment says "FlushDeferredUndo fires" but it's a no-op in that path.
- ADVISORY: Test file created as `DeferredUndo_Test.cs` (PascalCase per project convention); story doc had `deferred_undo_test.cs` — filename is authoritative.
**Test Evidence**: Logic — `tests/unit/game-state-manager/DeferredUndo_Test.cs` (14 tests)
**Code Review**: Complete (lean mode) — APPROVED WITH SUGGESTIONS; no required changes; `FlushDeferredUndo()` and `ExecuteUndo()` extraction validated; EC-11 event ordering test noted as excellent coverage
**Open follow-ups**: `Test_SecondUndoRequest_OnlyOneUndoFires` test body re-creates GSM unnecessarily — consider simplifying to reuse standard SetUp board in a future cleanup pass
