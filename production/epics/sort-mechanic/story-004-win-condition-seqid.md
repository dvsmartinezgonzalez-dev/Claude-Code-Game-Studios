# Story 004: Win Condition + Sequence ID Guard + OnMoveExecutingExited

> **Epic**: Sort Mechanic
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 1.0d
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-18

## Context

**GDD**: `design/gdd/sort-mechanic.md`
**Requirement**: `TR-SORT-003`, `TR-SORT-006`, `TR-SORT-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation (primary); ADR-0002: Event and Signal Architecture (secondary for seqId guard and OnMoveExecutingExited rules)
**ADR Decision Summary**: GSM is sole owner of board arrays; Sort Mechanic reads synchronously. Typed C# events with `?.Invoke()` only. Sequence ID guard: discard `animation_complete` if `seqId != _currentMoveExecutingSeqId`. `OnMoveExecutingExited` emitted ONLY on MOVE_EXECUTING → IDLE transition — never on WIN, never on watchdog path.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `sequence_id` must be `long` (C# int64). `int32` wrapping to negative in unchecked context produces a permanent MOVE_EXECUTING softlock. All event delegates must use `long` for `sequence_id` parameter — enforce at code review.

**Control Manifest Rules (Core layer):**
- Required: `?.Invoke()` for all event invocations; named-method subscribers; seqId guard `if (seqId != _currentMoveExecutingSeqId) return;` on `animation_complete` handler; `OnMoveExecutingExited` on IDLE path only; `Debug.Assert(stack_contents.Length == color_count)` inside win check function
- Forbidden: Processing deferred undo on WIN path; emitting `OnMoveExecutingExited` on watchdog path
- Guardrail: Win condition check O(colorCount × stackDepth) ≤ 64 iterations, no allocation

---

## Acceptance Criteria

*From GDD `design/gdd/sort-mechanic.md`, scoped to this story:*

- [ ] **AC-05a** — Win condition: `color_count = 2`, `stack_depth = 3`, stack A = [1,1], stack B = [2,2,2] (B full; A has one slot); player in BOLT_SELECTED holding color 1; mock GSM configured. Player taps stack A; `animation_complete(seqId)` received → `puzzle_solved` emitted, Sort Mechanic transitions to WIN; all subsequent tap events produce zero emissions and no board state change. (move_count parameter value not verified at unit tier — see AC-05b in Story 007.)
- [ ] **AC-06** — Win condition NOT triggered when stacks are monochromatic but non-full: `color_count = 2`, `stack_depth = 3`, stack A = [1,1] (one slot remaining), stack B = [2,2,2] (complete). After placement completes and `animation_complete` received → `puzzle_solved()` NOT emitted; Sort Mechanic transitions to IDLE, not WIN.
- [ ] **AC-18a** — Win condition fires correctly when invariant holds: all color stacks satisfy `is_won = TRUE` (each full and monochromatic), `len(color_stacks) == color_count`. After final bolt placed and `animation_complete` received → `puzzle_solved` emitted; Sort Mechanic in WIN. No separate temp-slot emptiness check needed.
- [ ] **AC-24** — Buffered tap discarded on WIN exit: Sort Mechanic in MOVE_EXECUTING with one tap buffered AND committed move completes win condition. On `animation_complete(seqId)` with `is_won = TRUE` → `puzzle_solved` emitted, Sort Mechanic in WIN, buffered tap silently discarded. Event-bus spy confirms zero emissions (including `move_executing_exited`) after `puzzle_solved`.
- [ ] **AC-29a** — `move_executing_exited` NOT emitted on watchdog path: Sort Mechanic in MOVE_EXECUTING; `board_refresh_forced(seqId)` received matching current in-flight sequence → win check runs, Sort Mechanic transitions to WIN or IDLE as appropriate; `move_executing_exited` is NOT emitted. Event-bus spy confirms `move_executing_exited` emission count is zero after `board_refresh_forced` processing.

---

## Implementation Notes

*Derived from EPIC.md Key Implementation Notes and ADR-0006/ADR-0002 guidelines:*

- `is_won` formula: iterate all `color_count` stacks. For each: `(stack.Length == stackDepth) && AllSameColor(stack)`. Both must hold for ALL stacks. Temp slots excluded from evaluation entirely.
- `AllSameColor(stack)`: all elements equal `stack[0]`. No canonical per-stack color is assigned during play.
- `Debug.Assert(stackContents.Length == colorCount)` must appear INSIDE the win check function — guards against array length mutation by a defective undo implementation.
- Win check runs on `animation_complete` receipt (end of MOVE_EXECUTING), NOT on `move_committed` emission.
- MOVE_EXECUTING exit sequence (IDLE path): (1) evaluate win → (2) if no win, emit `deadlock_detected()` if shallow check fails (Story 005) → (3) fire buffered tap if present and exit is IDLE → transition to IDLE. `OnMoveExecutingExited(seqId)` emitted on this path only.
- MOVE_EXECUTING exit sequence (WIN path): emit `puzzle_solved(move_count)`; transition to WIN; discard buffered tap. `OnMoveExecutingExited` is NOT emitted.
- Watchdog path (`board_refresh_forced`): re-read board state from GSM; run win check; transition to WIN or IDLE. `OnMoveExecutingExited` NOT emitted.
- Sequence ID guard on `animation_complete` handler: `if (seqId != _currentMoveExecutingSeqId) return;`. Stale signals silently discarded. If `animation_complete` never arrives for the current seqId (undo cleared the move), EC-08 watchdog handles the timeout — Sort Mechanic does not need to be notified of undos directly.
- `puzzle_solved(move_count: int)`: Sort Mechanic calls `GSM.GetMoveCount()` once at WIN entry; returned value is the `move_count` parameter. No caching, no polling.
- `_currentMoveExecutingSeqId` increments on each MOVE_EXECUTING entry. Type must be `long`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: `deadlock_detected()` emission and ordering within MOVE_EXECUTING exit sequence
- Story 002: Buffered tap storage and discard mechanics
- Story 007: Integration test verifying win check runs correctly on watchdog path with real GSM (AC-23)

---

## QA Test Cases

*Test cases not yet defined — run /qa-plan to generate them.*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/sort-mechanic/sort_mechanic_win_condition_test.cs` — must exist and pass

**Status**: [x] `tests/unit/sort-mechanic/sort_mechanic_win_condition_test.cs` — 18 tests (AC-05a, AC-06, AC-18a, AC-24, AC-29a, TR-SORT-003/006/007)

---

## Dependencies

- Depends on: Story 001 must be DONE; Story 003 must be DONE
- Unlocks: Story 005

---

## Completion Notes
**Completed**: 2026-05-18
**Criteria**: 5/5 passing
**Deviations**: ADR-0002 event catalog updated (int seqId → long seqId) — stale doc correction, justified
**Test Evidence**: Logic — `tests/unit/sort-mechanic/sort_mechanic_win_condition_test.cs` (18 tests)
**Code Review**: Complete — APPROVED after fixes (2026-05-18)
