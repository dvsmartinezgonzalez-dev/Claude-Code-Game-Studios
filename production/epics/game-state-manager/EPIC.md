# Epic: Game State Manager

> **Layer**: Core
> **GDD**: design/gdd/game-state-manager.md
> **Architecture Module**: GameStateManager
> **Status**: Ready
> **Manifest Version**: 2026-05-12
> **Stories**: 8 stories created (2026-05-14)

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [Board State Mutation](story-001-board-state-mutation.md) | Logic | Ready | ADR-0006 |
| 002 | [Undo System and Move Count Formula](story-002-undo-system.md) | Logic | Ready | ADR-0006 |
| 003 | [Win Detection and COMPLETE State](story-003-win-detection.md) | Logic | Ready | ADR-0006, ADR-0012, ADR-0002 |
| 004 | [Bolt Count Invariant Checks](story-004-invariant-checks.md) | Logic | Ready | ADR-0006 |
| 005 | [Level Load Pipeline](story-005-level-load-pipeline.md) | Logic | Ready | ADR-0001, ADR-0006 |
| 006 | [Watchdog Timer](story-006-watchdog-timer.md) | Logic | Ready | ADR-0006 |
| 007 | [Deferred Undo and MOVE_EXECUTING Exit Ordering](story-007-deferred-undo.md) | Logic | Ready | ADR-0006 |
| 008 | [App Lifecycle and Board Serialization](story-008-app-lifecycle.md) | Integration | Ready | ADR-0001, ADR-0006 |

## Overview

The Game State Manager is BoltSort's authoritative board state layer — the single source of truth for the current puzzle at all times. It owns four concerns: the live board state (`stack_contents[]` and `temp_slot_contents[]`), the move history stack that powers unlimited undo, the session lifecycle FSM (UNLOADED → LOADING → ACTIVE → COMPLETE → TEARDOWN), and board state persistence across app backgrounding. All mutations flow exclusively through Sort Mechanic events (`move_committed`, `puzzle_solved`); no system writes board state directly. GSM processes `move_committed` synchronously in a single frame — remove source, append destination, push undo entry, increment `current_sequence_id`, increment `move_count` — before any animation plays. A 1500ms `WaitForSecondsRealtime` watchdog guards against animation crashes by emitting `board_refresh_forced` if no animation completion arrives in time. GSM has no visual or audio output; its quality is measured entirely by its consistency — every system that reads from it must see the same board at all times.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Singleton Architecture and Boot Sequence | GameStateManager is a DDOL singleton at SEO −50; subscribe-then-check pattern for `OnLevelLoaded`; `load_level` only after LDS and SaveSystem are ready | HIGH (SerializeField restrictions) |
| ADR-0002: Event and Signal Architecture | Typed C# events (`Action<T>`) on MonoBehaviour; named-method subscribers in Awake; `?.Invoke()` only; no lambda subscribers | LOW |
| ADR-0006: Board State Representation | GSM sole owner of `stack_contents[]`; atomic 5-step mutation on `move_committed`; deferred undo queue capacity = 1; `WaitForSecondsRealtime` for watchdog; `IReadOnlyList<int>[]` for read-only board exposure | LOW |
| ADR-0012: HUD and Level Complete UI Business Logic | `level_complete` carries 4-arg payload `(levelId, moveCount, parMoves, sequenceId)`; GSM reads `parMoves` from LDS at WIN-01 transition; downstream systems must not query LDS independently for `parMoves` | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-GSM-001 | GameStateManager is the sole owner and writer of board state arrays; no external system may write board state | ADR-0006 ✅ |
| TR-GSM-002 | CurrentSequenceId is monotonically increasing; it never decrements | ADR-0006 ✅ |
| TR-GSM-003 | Unlimited undo stack during ACTIVE state; frozen (no new entries) on COMPLETE | ADR-0006 ✅ |
| TR-GSM-004 | Watchdog timer 1500ms: fires OnBoardRefreshForced if OnMoveExecutingExited does not arrive | ADR-0006 ✅ |
| TR-GSM-005 | Atomic board mutation: all 5 steps execute synchronously on main thread in one frame | ADR-0006 ✅ |
| TR-GSM-006 | Deferred undo during MOVE_EXECUTING: UndoRequested sets _pendingUndo; executed on OnMoveExecutingExited (IDLE path only) | ADR-0006 ✅ |
| TR-GSM-007 | bolt_count_invariant check at level load: sum(colorStacks[i].length) == colorCount × stackDepth | ADR-0006 ✅ |
| TR-GSM-008 | Level lifecycle FSM: UNLOADED → LOADING → ACTIVE → COMPLETE → TEARDOWN → UNLOADED | ADR-0001, ADR-0006 ✅ |
| TR-GSM-009 | GSM emits typed C# event Action\<T\> delegates for all inter-system communication | ADR-0002 ✅ |
| TR-GSM-010 | OnLevelComplete canonical payload: (levelId, moveCount, parMoves, sequenceId) — 4-arg signature; GSM reads parMoves from LDS before emitting | ADR-0012, ADR-0006, ADR-0002 ✅ |

## Key Implementation Notes

- **Atomic 5-step mutation contract** (BSM-01): remove source → append destination → push undo entry → `current_sequence_id++` → `move_count++`. All five steps execute synchronously in one frame. No `await`, no yield between steps.
- **Watchdog**: use `WaitForSecondsRealtime` (not `WaitForSeconds`) so the timer fires even when `Time.timeScale = 0` (pause screen). Control manifest required pattern.
- **Deferred undo queue capacity = 1** (EC-17): if a second `undo_requested` arrives while one is already deferred, the second is silently dropped. HUD must disable the undo button during MOVE_EXECUTING.
- **Deferred undo fires BEFORE win evaluation** (EC-10): when MOVE_EXECUTING exits, the deferred undo fires first — reverting the committed move — then the win condition is evaluated against the reverted board. This makes it impossible to accidentally win while the player had tapped undo.
- **`puzzle_solved()` discards deferred undo** (WIN-01 / EC-05): if `puzzle_solved()` arrives before MOVE_EXECUTING exits, the deferred undo is cleared and never executed. COMPLETE state does not process undo.
- **`load_level` rejected outside UNLOADED** (EC-09): GSM emits no `level_loaded` in any non-UNLOADED state. Level Progression must wait for `level_unloaded` before issuing the next `load_level`.
- **Board state NOT available during LOADING**: all read fields return NOT_READY until L-07 completes. Sort Mechanic must not read board state before receiving `level_loaded`.
- **Undo stack is session-only**: serialized on `OnApplicationPause`; undo stack is NOT persisted. On foreground restore, undo history is gone.
- **`board_state_changed` is NOT emitted after `move_committed`** (AC-GSM-03): only emitted after undo and on foreground restore. Animation System triggers from `OnBoardStateChanged` which fires on undo/restore; bolt animation triggers from `move_committed` (committed to GSM before animation plays).
- **`IGameStateManager` interface**: coding standards require dependency injection. No dedicated ADR exists yet — stories should expose the interface and flag for `/architecture-decision` before final code review.
- **`current_sequence_id` type**: GDD EC-12 says int32 minimum; ADR-0006 and Sort Mechanic GDD mandate `int64` for stale-signal safety. Use `long` in C#.
- **L-03 two-pass invariant check**: Check 1 (total bolt count = `colorCount × stackDepth`) AND Check 2 (each `color_id` appears exactly `stackDepth` times) — both must run independently.
- **L-04 pre-won board**: GSM logs a warning but does NOT auto-win. Sort Mechanic owns win detection; it runs on `level_loaded` (not GSM's responsibility).

## Events Reference

| Event | Parameters | Subscribers |
|-------|------------|-------------|
| `OnLevelLoaded` | levelId, colorCount, stackDepth, tempSlotCount, tempSlotDepth, sequenceId | Sort Mechanic, Animation System, HUD, Level Progression, Tutorial System |
| `OnLevelComplete` | levelId, moveCount, parMoves, sequenceId | HUD, Level Progression, Analytics System |
| `OnLevelUnloaded` | levelId (nullable) | Level Progression, Animation System |
| `OnSessionLoadFailed` | reason, errorCode, levelId | Level Progression, HUD |
| `OnBoardStateChanged` | sequenceId, moveCount | HUD, Animation System |
| `OnBoardRefreshForced` | sequenceId | Sort Mechanic, Animation System |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All 21 BLOCKING acceptance criteria (AC-GSM-01 through AC-GSM-21) have passing unit tests in `tests/unit/game-state-manager/`
- Integration tests covering Sort Mechanic × GSM interaction exist in `tests/integration/gsm-sort-mechanic/`
- `IGameStateManager` interface is defined and `GameStateManager` implements it
- `WaitForSecondsRealtime` watchdog verified correct (fires at `timeScale = 0`)
- Board state persistence (SER-01/SER-02/SER-03) integration tested with Save & Persistence stub

## Dependencies

| System | Layer | Status | Notes |
|--------|-------|--------|-------|
| Level Data System | Foundation | Complete | Hard dependency — GSM cannot load any level without LDS ready |
| Save & Persistence | Foundation | No epic started | Soft dependency — GSM serializes board state on app background. Stories touching SER-01/SER-02/SER-03 depend on SaveSystem implementation. Can be stubbed for unit tests. |

## Open Questions (from GDD)

These are design decisions deferred to implementation — none block this epic from being created:

- `IGameStateManager` interface: required by coding standards; no ADR yet. Run `/architecture-decision game-state-manager-interface` before implementation stories are reviewed.
- Undo history persistence across app kills: currently session-only (not persisted). Revisit if playtesting shows player frustration with lost undo history on app kill.
- `level_complete` move_count definition: currently net moves (committed minus undone). If analytics needs gross committed moves, a `total_committed_count` field may be needed — design decision for Analytics GDD.
- Level Progression retry backoff on `LEVEL_DATA_UNAVAILABLE`: owned by Level Progression GDD.

## Next Step

Run `/create-stories game-state-manager` to break this epic into implementable stories.
