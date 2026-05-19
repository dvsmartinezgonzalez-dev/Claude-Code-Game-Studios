# Epic: Sort Mechanic

> **Layer**: Feature
> **GDD**: design/gdd/sort-mechanic.md
> **Architecture Module**: SortMechanic
> **Status**: Ready
> **Manifest Version**: 2026-05-12
> **Stories**: 7 stories (Ready)

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [FSM Core + Initialization](story-001-fsm-core-initialization.md) | Logic | Ready | ADR-0006, ADR-0007 |
| 002 | [Input Handling — Touch, Back Gesture, One-Tap Buffer](story-002-input-handling.md) | Logic | Ready | ADR-0007, ADR-0006 |
| 003 | [Move Validation + Column Cap Assertion](story-003-move-validation.md) | Logic | Ready | ADR-0006, ADR-0013 |
| 004 | [Win Condition + Sequence ID Guard + OnMoveExecutingExited](story-004-win-condition-seqid.md) | Logic | Ready | ADR-0006, ADR-0002 |
| 005 | [Deadlock Detection](story-005-deadlock-detection.md) | Integration | Ready | ADR-0006 |
| 006 | [App-Pause Cancellation + SEO Contract](story-006-app-pause-cancellation.md) | Integration | Ready | ADR-0006, ADR-0007 |
| 007 | [GSM Full Integration — Board Reads, Watchdog, Error Cases](story-007-gsm-integration.md) | Integration | Ready | ADR-0006, ADR-0007, ADR-0013 |

## Overview

The Sort Mechanic is BoltSort's core interactive system — the complete rule set governing how the player moves bolts between stacks and when a puzzle is solved. It owns the interaction FSM (IDLE / BOLT_SELECTED / MOVE_EXECUTING / CANCELLATION / INVALID_MOVE / WIN), the held bolt reference, a one-tap input buffer during MOVE_EXECUTING, and sequence ID tracking for stale-signal discard. It is the rule engine, not a state owner: all board state lives in the Game State Manager; Sort Mechanic reads it synchronously on demand and emits C# events that GSM, AnimationSystem, and HUD subscribe to. Touch input is received via `EnhancedTouchSupport` and dispatched to stacks through `Physics2D.OverlapPoint`. The player experiences this system as the satisfying click of a bolt seating, the immediate shake of a rejected move, and the clean silence of a solved board.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0002: Event and Signal Architecture | Typed C# events (`Action<T>`) on MonoBehaviour; named-method subscribers in Awake; `?.Invoke()` only; sequence ID guard on all seqId-carrying events | LOW |
| ADR-0006: Board State Representation | GSM sole owner of board arrays; Sort Mechanic reads via `IReadOnlyList<int>[]` synchronously; move validation uses guarded conditional (not eager bool); atomic 5-step board mutation on `move_committed` | LOW |
| ADR-0007: Input Handling Strategy | `EnhancedTouchSupport` + `Physics2D.OverlapPoint` for tap detection; `TouchPhase.Began` fires immediately on contact; `Keyboard.current?.escapeKey.wasPressedThisFrame` for Android back gesture with mandatory null guard on iOS | MEDIUM |
| ADR-0013: Level Layout Column Cap | `color_count + temp_slot_count ≤ 8` enforced at authoring time; Sort Mechanic reads `color_count` and `temp_slot_count` from GSM board state at initialization | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-SORT-001 | State machine: IDLE/BOLT_SELECTED/MOVE_EXECUTING/WIN/CANCELLATION/INVALID_MOVE | ADR-0006 ✅ |
| TR-SORT-002 | Move validation: empty destination accepts any color; full destination rejects; non-full non-empty accepts only if top bolt color matches | ADR-0006 ✅ |
| TR-SORT-003 | Win condition: all color stacks full and monochromatic; temp slots excluded from win check | ADR-0006 ✅ |
| TR-SORT-004 | One-tap input buffer during MOVE_EXECUTING; buffer discarded on WIN exit | ADR-0007 ✅ |
| TR-SORT-005 | Shallow deadlock check (depth-1) on every MOVE_EXECUTING exit → emit OnDeadlockDetected if no legal move exists | ADR-0006 ✅ |
| TR-SORT-006 | Sequence ID stale-signal guard on animation_complete: discard if seqId != currentMoveExecutingSeqId | ADR-0002 ✅ |
| TR-SORT-007 | OnMoveExecutingExited emitted only on MOVE_EXECUTING → IDLE transition; NOT on WIN, NOT on watchdog | ADR-0002 ✅ |
| TR-SORT-008 | Android back gesture → cancellation in BOLT_SELECTED state (escapeKey.wasPressedThisFrame) | ADR-0007 ✅ |
| TR-SORT-009 | Synchronous pull-on-demand read of board state from GSM (StackContents[], TempSlotContents[]) | ADR-0006, ADR-0007 ✅ |
| TR-SORT-010 | Column cap: color_count + temp_slot_count ≤ 8 (UI/Layout constraint) | ADR-0013 ✅ |

## Key Implementation Notes

- `sequence_id` must be `int64` — `int32` wrapping to a negative value produces a permanent MOVE_EXECUTING softlock; `int64` (~9.2 × 10¹⁸) eliminates this failure mode permanently. All event signatures use `long` in C#.
- Move validation formula is a guarded conditional, not a flat bool — `destination_top_color` must not be read before the empty-slot guard (undefined on empty slots).
- Win check runs on `animation_complete` receipt (end of MOVE_EXECUTING), not on `move_committed` emission.
- `board_refresh_forced` (watchdog) must trigger win check before returning to IDLE — failure to do so produces a softlock on the final winning move if the animation system crashes.
- Android back gesture null guard is a **required contract**: `if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)` — omitting it crashes iOS in BOLT_SELECTED.
- App-pause handler (`OnApplicationPause(true)`) must cancel held bolt before GSM serializes board state. Requires Sort Mechanic SEO to be lower (higher priority) than GSM in Script Execution Order.
- CANCELLATION exits to IDLE synchronously on `move_cancelled` emission — no animation handshake required.
- INVALID_MOVE buffers exactly one tap during rejection animation (60–200ms); that tap is fired as a new destination evaluation on BOLT_SELECTED re-entry.
- Initialization asserts three conditions: `len(color_stacks) == color_count`; `temp_slot_depth ≤ stack_depth`; all `color_id` values in `{1..color_count}`. Failure → emit `level_load_failed(CORRUPTED_BOARD_STATE)`, refuse all input.
- `tests/helpers/sort-mechanic-fixtures` (canonical deadlock fixture) is a required deliverable alongside implementation — referenced by AC-10, AC-22, AC-25.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/sort-mechanic.md` are verified
- All BLOCKING Logic stories have passing unit tests in `tests/unit/sort-mechanic/`
- All Integration stories have passing tests in `tests/integration/gsm-sort-mechanic/`
- `tests/helpers/sort-mechanic-fixtures` canonical deadlock fixture exists
- `Physics2D.OverlapPoint` + Input System touch coordinate on-device verification complete (Engine Risk: MEDIUM)
- Android back gesture verified on physical Android device (covers hardware back + predictive back per ADR-0007)
- App-pause cancellation integration test passes (AC-28)

## Dependencies

| System | Layer | Status | Notes |
|--------|-------|--------|-------|
| Level Data System | Foundation | Complete | Indirect — board state already loaded by GSM at level start |
| Game State Manager | Core | No epic yet | **BLOCKING** — Sort Mechanic stories cannot start implementation until GSM is implemented. Run `/create-epics game-state-manager` first. |

## Next Step

Stories created 2026-05-17. Run `/story-readiness production/epics/sort-mechanic/story-001-fsm-core-initialization.md` then `/dev-story` to begin implementation. Work through stories in dependency order (001 → 002/003/006 → 004 → 005 → 007).
