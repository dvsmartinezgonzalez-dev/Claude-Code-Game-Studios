# Story 007: GSM Full Integration — Board Reads, Watchdog, Error Cases

> **Epic**: Sort Mechanic
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 0.5d
> **Manifest Version**: 2026-05-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/sort-mechanic.md`
**Requirement**: `TR-SORT-009`, `TR-SORT-010`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation (primary); ADR-0007: Input Handling Strategy (secondary); ADR-0013: Level Layout Column Cap (secondary)
**ADR Decision Summary**: Sort Mechanic reads board state from GSM via `IReadOnlyList<int>[]` synchronously. Atomic 5-step board mutation in GSM executes synchronously before Animation System receives `board_state_changed`. Watchdog (`board_refresh_forced`) triggers win check before IDLE transition. Column cap `color_count + temp_slot_count ≤ 8` enforced at authoring and at Sort Mechanic initialization.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Integration tests require a real `GameStateManager` instance. Use `IGameStateManager` interface for dependency injection in tests to avoid requiring the full scene hierarchy.

**Control Manifest Rules (Core layer):**
- Required: Sort Mechanic reads only via `IReadOnlyList<int>[]` interface; GSM is sole writer; `CancelWatchdog()` called on every MOVE_EXECUTING exit (IDLE, WIN, TEARDOWN, `OnDestroy`)
- Forbidden: Writing board state from Sort Mechanic; emitting `animation_complete` on watchdog path (handled by AS abort); `WaitForSeconds` for watchdog (use `WaitForSecondsRealtime`)

---

## Acceptance Criteria

*From GDD `design/gdd/sort-mechanic.md`, scoped to this story:*

- [ ] **AC-05b** *(Integration)* — `color_count = 2`, `stack_depth = 3`; 2 prior committed moves in real GSM undo history; stack A = [1,1]; player holds color 1 in BOLT_SELECTED. Player taps stack A; `animation_complete` received → `puzzle_solved(move_count: 3)` emitted — GSM's `GetMoveCount()` returns 3 after this final `move_committed`. Verified with real GSM instance tracking undo history.
- [ ] **AC-13** *(Integration)* — Player taps valid destination in BOLT_SELECTED → Sort Mechanic transitions to MOVE_EXECUTING → `move_committed(source, destination, color_id)` emitted synchronously before bolt movement animation begins. GSM board state updated before first animation frame renders. Verified with real GSM + AnimationSystem harness.
- [ ] **AC-15b** *(Integration)* — `move_committed` emitted for move from stack A to stack B; player issues undo → GSM restores board (bolt returns from B to A). Verified at integration tier with Sort Mechanic + GSM running together.
- [ ] **AC-19** *(Integration)* — Level record where `total_bolts ≠ color_count × stack_depth` (one bolt missing): GSM invariant check fires before board initialization completes; GSM refuses to expose valid board state; Sort Mechanic detects inconsistency via initialization assertions and emits `level_load_failed(reason: CORRUPTED_BOARD_STATE)`. Board never made available for interaction. Verified with synthetic broken level fixture in `tests/integration/gsm-sort-mechanic/`.
- [ ] **AC-23** *(Integration)* — Sort Mechanic in MOVE_EXECUTING; `board_refresh_forced(seqId)` received (animation watchdog triggered). Sort Mechanic reads current board from GSM and evaluates win condition before transitioning. If `is_won = TRUE` → transitions to WIN and emits `puzzle_solved(move_count)`. If `is_won = FALSE` → transitions to IDLE. Sort Mechanic must NEVER transition to IDLE on a won board. Verified by simulating animation crash on final winning move and confirming `puzzle_solved` fires.
- [ ] **AC-26** *(Integration)* — Level record where `total_bolts == color_count × stack_depth` (total invariant holds) but either (a) one color has more than `stack_depth` instances, or (b) a `color_id` outside domain `{1..color_count}` is present. GSM per-color validation catches this and refuses to expose valid board; Sort Mechanic detects inconsistency via assertions and emits `level_load_failed(CORRUPTED_BOARD_STATE)`. Verified with two synthetic fixtures (over-representation + phantom color) in `tests/integration/gsm-sort-mechanic/`.
- [ ] **AC-31** *(Integration)* — Level where all color stacks satisfy `is_won = TRUE` at initial board load (Level Data authoring error). Sort Mechanic processes `level_loaded` → `puzzle_solved(move_count: 0)` emitted before any player input; Sort Mechanic transitions to WIN; no `deadlock_detected()` emitted. Verified with synthetic pre-won level fixture.

---

## Implementation Notes

*Derived from EPIC.md Key Implementation Notes, EC-04, EC-08, and ADR-0006 guidelines:*

- All tests in this story require a real `GameStateManager` instance — mock GSM is insufficient for verifying board mutation ordering, undo history, and `GetMoveCount()` accuracy.
- AC-23 (watchdog win check): on `board_refresh_forced` receipt while in MOVE_EXECUTING, Sort Mechanic must re-read board state from GSM and call `is_won` BEFORE deciding the exit state. Failure to run win check here produces a softlock: player returned to IDLE on a won board with no path to WIN.
- AC-19/AC-26: Sort Mechanic is the EMITTER of `level_load_failed` — it is not a signal GSM sends. GSM's failure to expose a valid board is the trigger; Sort Mechanic detects and emits via its initialization assertions (AC-18b/AC-27 pattern).
- AC-31 (pre-won board): pre-won check runs before deadlock check on `level_loaded` path. If `is_won = TRUE` against initial board: emit `puzzle_solved(move_count: 0)`, transition to WIN, lock all input. `deadlock_detected()` is NOT emitted on this path.
- Test fixtures for AC-19, AC-26, AC-31 are synthetic JSON level records. Store in `tests/integration/gsm-sort-mechanic/fixtures/`.
- `CancelWatchdog()` must be called on every MOVE_EXECUTING exit path (IDLE, WIN, TEARDOWN, `OnDestroy`). Missing any path leaks a DDOL coroutine.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: Win check logic itself (unit-level) — this story verifies it runs correctly with a real GSM
- Story 005: Deadlock detection logic — this story verifies board state correctness; Story 005 owns the deadlock algorithm
- Story 006: App-pause cancellation (separate integration concern)

---

## QA Test Cases

*Test cases not yet defined — run /qa-plan to generate them.*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/gsm-sort-mechanic/sort_mechanic_gsm_integration_test.cs` — must exist and pass
- Synthetic fixtures for AC-19, AC-26, AC-31 in `tests/integration/gsm-sort-mechanic/fixtures/`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 002, Story 003, Story 004, Story 005, Story 006 — all must be DONE
- Unlocks: None (epic complete when this story is Done)
