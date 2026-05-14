# Story 003: Win Detection and COMPLETE State

> **Epic**: Game State Manager
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: Small (2h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/game-state-manager.md`
**Requirement**: `TR-GSM-008`, `TR-GSM-009`, `TR-GSM-010`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006 (COMPLETE state transition); ADR-0012 (level_complete 4-arg payload); ADR-0002 (typed C# events)
**ADR Decision Summary**: `puzzle_solved()` received from Sort Mechanic triggers GSM → COMPLETE. GSM reads `par_moves` from LDS at this moment (O(1) dict lookup) and emits `level_complete(levelId, moveCount, parMoves, sequenceId)`. `current_sequence_id` is NOT incremented on this transition. Undo stack is frozen. All subsequent `move_committed` and `puzzle_solved()` events are ignored.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff Unity APIs. LDS is accessed via `ILevelDataSystem.GetReadiness()` / `GetLevel(int)` synchronously.

**Control Manifest Rules (Core layer)**:
- Required: Typed C# events (`Action<T>`) — `OnLevelComplete` must use `event Action<int, int, int, long>` signature matching (levelId, moveCount, parMoves, sequenceId) per ADR-0012
- Required: GSM is sole writer of board state — COMPLETE state freezes undo and ignores further mutations

---

## Acceptance Criteria

*From GDD `design/gdd/game-state-manager.md`, scoped to this story:*

- [ ] **AC-GSM-08** — `puzzle_solved()` received in ACTIVE with `move_count=7`, `current_sequence_id=7`: (1) GSM transitions to COMPLETE, (2) `level_complete(levelId, moveCount=7, parMoves=X, sequenceId=7)` emitted — `sequenceId` is the value at transition time, NOT incremented, (3) subsequent `move_committed` silently ignored and board state unchanged, (4) subsequent `undo_requested` silently ignored, (5) no further GSM events emitted after `level_complete`

---

## Implementation Notes

*Derived from ADR-0006 and ADR-0012:*

```csharp
private void HandlePuzzleSolved()
{
    if (_state != GsmState.Active) return;

    _state = GsmState.Complete;
    // UND-05: undo stack frozen — stop accepting requests
    // Deferred undo is cleared (Story 007 handles EC-05)

    // ADR-0012: read par_moves from LDS at transition time
    var record = _levelDataSystem.GetLevel(_currentLevelId);
    int parMoves = record.ParMoves;

    // sequence_id is NOT incremented (WIN-01) — use current value as payload
    OnLevelComplete?.Invoke(_currentLevelId, _moveCount, parMoves, _currentSequenceId);
}
```

**WIN-01 payload contract (ADR-0012)**: The 4-arg `level_complete` signature `(levelId, moveCount, parMoves, sequenceId)` was added to avoid InGameHUD and LevelCompleteUI querying LDS independently for `parMoves`. GSM reads it once and embeds it in the payload. All downstream systems must read `parMoves` from this event, not from a direct LDS call.

**seqId not incremented**: The `sequenceId` in the `level_complete` payload equals the current `_currentSequenceId` at transition time — WIN-01 does not increment it. This is intentional: the winning `move_committed` already incremented it in BSM-01.

**Frozen state**: In COMPLETE, `HandleMoveCommitted` and `UndoRequested` both return immediately on state guard check. `puzzle_solved()` in COMPLETE is also a no-op (EC-15 from the undo story — WIN cannot be entered twice).

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 007: EC-05 — deferred undo discarded when `puzzle_solved()` arrives while MOVE_EXECUTING
- Story 005: TEARDOWN state and `exit_level` command from Level Progression

---

## QA Test Cases

- **AC-GSM-08**: WIN-01 transition and payload
  - Given: GSM ACTIVE, `move_count=7`, `current_sequence_id=7`, `level_id=42`, stub LDS returning `par_moves=10` for level 42; event spy on all GSM events
  - When: `puzzle_solved()` received
  - Then: GSM state = COMPLETE; `level_complete(42, 7, 10, 7)` in spy (seqId=7, not 8); no additional events after `level_complete`
  - When: `move_committed(src=0, dst=1, colorId=2, seqId=7)` received (post-win)
  - Then: spy records no new events; board state unchanged
  - When: `undo_requested` received (post-win)
  - Then: spy records no new events; `current_sequence_id` still 7; `move_count` still 7
  - Edge cases: `puzzle_solved()` received while GSM is in COMPLETE (second call) → no second `level_complete` emitted

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/game-state-manager/win_detection_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (DONE) — board state mutation established; ACTIVE state required for WIN transition
- Unlocks: Story 007 (EC-05: deferred undo discarded on `puzzle_solved()`)
