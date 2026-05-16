# Story 002: Undo System and Move Count Formula

> **Epic**: Game State Manager
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: Small (2–3h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/game-state-manager.md`
**Requirement**: `TR-GSM-002`, `TR-GSM-003`, `TR-GSM-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation
**ADR Decision Summary**: Unlimited undo stack in ACTIVE state; frozen on COMPLETE. Every undo that mutates board state increments `current_sequence_id` — never decrements it. Undo deferred during MOVE_EXECUTING is handled in Story 007. Move count formula: +1 on `move_committed`, −1 on undo (non-empty stack), 0 on all other events.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# stack and event handling. No post-cutoff Unity APIs.

**Control Manifest Rules (Core layer)**:
- Required: Monotonic sequence ID — `current_sequence_id` only ever increments, never decrements. Only GSM calls `_currentSequenceId++`.
- Required: GSM is sole writer of board state — undo reverts board state directly, no Sort Mechanic event roundtrip

---

## Acceptance Criteria

*From GDD `design/gdd/game-state-manager.md`, scoped to this story:*

- [ ] **AC-GSM-04** — `undo_requested` in ACTIVE state with undo stack depth ≥ 1: (1) top undo entry popped, (2) `color_id` removed from destination array, (3) `color_id` appended to source array, (4) `current_sequence_id` incremented (NOT decremented), (5) `move_count` decremented, (6) `board_state_changed(sequence_id, move_count)` emitted
- [ ] **AC-GSM-05** — `undo_requested` in ACTIVE state with empty undo stack: no mutation of any field, no GSM event emitted
- [ ] **AC-GSM-06** — `undo_requested` in COMPLETE state with non-empty undo stack: no board mutation, no event emitted, `current_sequence_id` and `move_count` unchanged
- [ ] **AC-GSM-07** — Starting at `current_sequence_id=0`, after 5 `move_committed` events and 3 `undo_requested` events (any interleaving): `current_sequence_id=8`; every intermediate value observed after each mutation is strictly greater than the preceding observed value
- [ ] **AC-GSM-19** — Starting at `move_count=0`, after sequence: `move_committed`, `move_committed`, `move_cancelled`, `move_rejected`, `undo_requested` (non-empty stack), `move_committed` → `move_count=2`; intermediate values after each event: 1, 2, 2, 2, 1, 2

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

```csharp
public void UndoRequested()
{
    if (_state != GsmState.Active) return;              // frozen in COMPLETE (UND-05)
    if (_undoStack.Count == 0) return;                  // no-op on empty (UND-02)
    // UND-03 deferred undo (MOVE_EXECUTING) is handled in Story 007

    var entry = _undoStack.Pop();
    // Revert: move color_id from entry.destination back to entry.source
    _stackContents[entry.Destination].RemoveAt(_stackContents[entry.Destination].Count - 1);
    _stackContents[entry.Source].Add(entry.ColorId);

    _currentSequenceId++;   // increment — never decrement (UND-06)
    _moveCount--;

    OnBoardStateChanged?.Invoke(_currentSequenceId, _moveCount);
}
```

**UND-06 stale-signal guarantee**: Every undo increments `current_sequence_id`. Any `animation_complete` signal from the undone move carries a stale ID. Sort Mechanic discards it via the sequence ID mismatch check — no additional notification needed from GSM.

**Move count formula**:
- +1: `move_committed` (BSM-01, Story 001)
- −1: undo with non-empty stack (UND-01)
- 0: `move_cancelled`, `move_rejected`, watchdog fire, foreground restore
- Frozen: on `puzzle_solved()` receipt (WIN-01, Story 003)

**Undo stack**: `Stack<UndoEntry>` (or equivalent LIFO collection). Entry struct: `{ SourceIndex: int, DestinationIndex: int, ColorId: int, SequenceId: long }`. Unlimited capacity — do not cap.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 007: Deferred undo processing during MOVE_EXECUTING (UND-03) — this story covers the simple synchronous path only
- Story 003: Undo stack frozen in COMPLETE — this story verifies AC-GSM-06 but the COMPLETE state itself is implemented in Story 003

---

## QA Test Cases

- **AC-GSM-04**: Undo reverts board and increments seqId
  - Given: GSM ACTIVE, two `move_committed` events processed (move A: src=0→dst=1, colorId=2; move B: src=1→dst=2, colorId=3); `move_count=2`, `current_sequence_id=2`
  - When: `undo_requested`
  - Then: move B reverted — colorId=3 removed from `stack_contents[2]` and appended to `stack_contents[1]`; undo stack depth=1; `current_sequence_id=3`; `move_count=1`; `board_state_changed(seqId=3, moveCount=1)` emitted
  - Edge cases: undo when undo stack has exactly 1 entry (stack empty after pop); undo after `move_committed` to temp slot

- **AC-GSM-05**: Empty stack no-op
  - Given: GSM ACTIVE, undo stack is empty (no moves committed), event spy attached
  - When: `undo_requested`
  - Then: spy records zero events; all fields (stack_contents, seqId, move_count) unchanged

- **AC-GSM-06**: COMPLETE state rejects undo
  - Given: GSM in COMPLETE (after `puzzle_solved()`), undo stack has 3 entries
  - When: `undo_requested`
  - Then: no mutation; no event; undo stack still has 3 entries; `current_sequence_id` unchanged

- **AC-GSM-07**: Strict monotonicity over mixed sequence
  - Given: GSM ACTIVE, `current_sequence_id=0`; spy records seqId after each mutation
  - When: 5 × `move_committed`, 3 × `undo_requested` (any interleaving)
  - Then: `current_sequence_id=8`; spy's recorded values are strictly increasing (each value > all preceding values)
  - Edge cases: all undos before any commits (impossible — stack empty); all commits before undos

- **AC-GSM-19**: Move count formula interleaved sequence
  - Given: GSM ACTIVE, `move_count=0`
  - When: `move_committed` → `move_committed` → `move_cancelled` → `move_rejected` → `undo_requested` → `move_committed`
  - Then: `move_count=2`; assert intermediate values 1, 2, 2, 2, 1, 2 in sequence

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/game-state-manager/UndoSystem_Test.cs` — must exist and pass

**Status**: [x] Exists — `tests/unit/game-state-manager/UndoSystem_Test.cs` (9 test methods covering AC-GSM-04/05/06/07/19)

---

## Dependencies

- Depends on: Story 001 (DONE) — undo stack is populated by `move_committed` in Story 001
- Unlocks: Story 007 (deferred undo builds on the synchronous undo path)

---

## Completion Notes
**Completed**: 2026-05-15
**Criteria**: 5/5 passing (all covered by automated unit tests)
**Deviations**: INFO — Story pseudocode used `_undoStack.Pop()`/`Source`/`Destination`; implementation correctly adapts to `List<UndoEntry>` with `From`/`To` per existing struct. Advisory S-1/S-2 from code review noted below.
**Test Evidence**: Logic — `tests/unit/game-state-manager/UndoSystem_Test.cs` (9 tests; APPROVED WITH SUGGESTIONS by /code-review 2026-05-15)
**Code Review**: Complete — APPROVED WITH SUGGESTIONS. Advisory items: (S-1) clarify commit vs undo observation mechanics in AC-GSM-07 test comment; (S-2) add explicit Assert.Greater for UND-06 "never decrement" in AC-GSM-04 test.
**Implementation**: `UndoRequested()` in GameStateManager.cs — UND-01/02/05/06 synchronous path only. UND-03 deferred undo deferred to Story 007.
