# Story 001: Board State Mutation

> **Epic**: Game State Manager
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: Small (2–3h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/game-state-manager.md`
**Requirement**: `TR-GSM-001`, `TR-GSM-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation
**ADR Decision Summary**: GSM is the sole owner of all board state arrays. All mutations are triggered exclusively by Sort Mechanic events. `move_committed` triggers a synchronous 5-step atomic mutation. `move_cancelled` and `move_rejected` produce zero mutations. `board_state_changed` is NOT emitted after `move_committed` — only after undo and foreground restore.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# data structures and event handling. No post-cutoff Unity APIs involved.

**Control Manifest Rules (Core layer)**:
- Required: GSM is sole writer of `_stackContents` and `_tempSlotContents` — no external system may mutate these arrays
- Required: Atomic 5-step board mutation — all steps execute synchronously on main thread in a single `HandleMoveCommitted()` callback with no `await` between steps
- Required: Typed C# events (`Action<T>`) with `?.Invoke()` — source: ADR-0002

---

## Acceptance Criteria

*From GDD `design/gdd/game-state-manager.md`, scoped to this story:*

- [ ] **AC-GSM-01** — `move_committed(source=0, destination=1, color_id=3, sequence_id=N)` received in ACTIVE state with `move_count=M`, `current_sequence_id=N`, undo stack depth=D → (1) top element of `stack_contents[0]` removed, (2) `color_id=3` appended to `stack_contents[1]`, (3) undo stack depth=D+1 with entry `{source=0, dest=1, color_id=3, seq=N}`, (4) `current_sequence_id=N+1`, (5) `move_count=M+1` — all five observable immediately post-event
- [ ] **AC-GSM-02** — `move_cancelled(source=0, color_id=3)` and `move_rejected(source, dest, color_id, COLOR_MISMATCH)` each produce zero mutations: `stack_contents`, `temp_slot_contents`, `current_sequence_id`, `move_count`, and undo stack are byte-identical to a pre-event snapshot after each event; no GSM events emitted in response to either
- [ ] **AC-GSM-03** — After `move_committed` is received and board state is updated, `board_state_changed` is NOT emitted (verified with an event-bus spy)

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

```csharp
private void HandleMoveCommitted(int src, int dst, int colorId, long seqId)
{
    // Step 1: remove top bolt from source
    int top = _stackContents[src][_stackContents[src].Count - 1];
    _stackContents[src].RemoveAt(_stackContents[src].Count - 1);

    // Step 2: append to destination
    _stackContents[dst].Add(colorId);

    // Step 3: push undo entry
    _undoStack.Push(new UndoEntry(src, dst, colorId, _currentSequenceId));

    // Step 4: increment sequence ID
    _currentSequenceId++;

    // Step 5: increment move count
    _moveCount++;

    // board_state_changed is NOT emitted here — only on undo and foreground restore
    // Start watchdog timer here (Story 006)
}
```

**Index convention**: Color stacks occupy indices 0 through `colorCount - 1`. Temp slots occupy indices `colorCount` through `colorCount + tempSlotCount - 1`. This flat namespace is consistent across undo entries, the Sort Mechanic read interface, and all events.

**BSM-02/03**: `move_cancelled` and `move_rejected` are no-ops for GSM — the bolt was never committed. No mutation, no event. Subscribe to these events for completeness but return immediately.

**AC-GSM-03 rationale**: The Animation System is triggered by `OnBoardStateChanged` for snap-to-state updates (undo, foreground restore). It is triggered by `move_committed` for the bolt travel animation via a separate code path. Emitting `OnBoardStateChanged` after `move_committed` would cause the Animation System to snap the bolt visually before the travel animation plays.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 002: Undo request processing — this story establishes the undo stack data structure only
- Story 003: `puzzle_solved()` handling and WIN state transition
- Story 006: Watchdog timer start/cancel on `move_committed`
- Story 007: Deferred undo processing on MOVE_EXECUTING exit

---

## QA Test Cases

*Written by qa-lead at story creation. Implement against these — do not invent new test cases.*

- **AC-GSM-01**: Atomic 5-step mutation
  - Given: GSM in ACTIVE, `move_count=5`, `current_sequence_id=3`, undo stack depth=2, `stack_contents[0]=[1,2]`, `stack_contents[1]=[3]`, `stack_depth=4`
  - When: `move_committed(src=0, dst=1, colorId=2, seqId=3)`
  - Then: `stack_contents[0]=[1]` (length 1); `stack_contents[1]=[3,2]` (length 2); undo stack depth=3, top entry `{src=0, dst=1, colorId=2, seqId=3}`; `current_sequence_id=4`; `move_count=6`
  - Edge cases: `move_committed` with a temp slot as destination (flat index `colorCount`); source emptied to length 0

- **AC-GSM-02**: Cancelled/rejected produce zero mutations
  - Given: GSM in ACTIVE, full field snapshot taken after one `move_committed`
  - When: `move_cancelled(src=0, colorId=2)` then `move_rejected(src=1, dst=2, colorId=3, reason=COLOR_MISMATCH)`
  - Then: `stack_contents`, `temp_slot_contents`, `current_sequence_id`, `move_count`, undo stack length all equal snapshot values after each event; event spy confirms zero GSM event emissions

- **AC-GSM-03**: No board_state_changed on move_committed
  - Given: GSM in ACTIVE, event spy subscribed to all GSM events
  - When: valid `move_committed` received
  - Then: `board_state_changed` not in spy's recorded emissions; no other unexpected events recorded

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/game-state-manager/board_mutation_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None — this is the foundational GSM story
- Unlocks: Story 002 (undo uses the undo stack established here), Story 003 (win detection reads board state), Story 005 (load pipeline populates board state), Story 006 (watchdog starts on move_committed)
