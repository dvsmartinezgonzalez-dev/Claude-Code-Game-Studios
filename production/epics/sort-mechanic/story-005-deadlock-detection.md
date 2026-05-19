# Story 005: Deadlock Detection

> **Epic**: Sort Mechanic
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 1.0d
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-19

## Context

**GDD**: `design/gdd/sort-mechanic.md`
**Requirement**: `TR-SORT-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation
**ADR Decision Summary**: Sort Mechanic runs a shallow depth-1 deadlock check on every MOVE_EXECUTING exit (IDLE path only). If no legal first move exists from current board, emit `OnDeadlockDetected`. Same check runs on `level_loaded`. Exhaustive verification is the Hint System's domain.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff risk. Pure logic — reads GSM board state via `IReadOnlyList<int>[]`.

**Control Manifest Rules (Core layer):**
- Required: Deadlock check O(N²) where N ≤ 11 stacks (~120 comparisons); triggered on every `OnMoveExecutingExited` (IDLE path); `?.Invoke()` on `OnDeadlockDetected` emission
- Guardrail: Deadlock check is O(N(N-1)) at depth 1 — max ~110 iterations at N=11 (8 color + 3 temp); negligible on mobile; do not increase depth without explicit approval

---

## Acceptance Criteria

*From GDD `design/gdd/sort-mechanic.md`, scoped to this story:*

- [ ] **AC-22** *(BLOCKING — unit test)* — Canonical deadlock fixture loaded as board state AND a tap was buffered during MOVE_EXECUTING for the bolt placement creating the deadlocked state. On `animation_complete` received and IDLE exit: event order is (1) win check fails → (2) `deadlock_detected()` emitted → (3) buffered tap fires. `deadlock_detected()` must NOT be deferred until after the buffered tap. Verified by event-bus spy confirming emission order. **Canonical fixture (`tests/helpers/sort-mechanic-fixtures`) is a named deliverable of this story — author it before writing the AC-22 test.**
- [ ] **AC-10** *(Integration)* — Board state where every possible first move fails validation (canonical deadlock fixture): bolt placement completing this state → `animation_complete` received → `deadlock_detected()` emitted before any buffered tap fires and before further input accepted. Verified at integration tier by asserting signal emission order from Sort Mechanic's event bus.
- [ ] **AC-25** *(Integration)* — Level loaded where no legal first move exists from initial board configuration (canonical deadlock fixture). `level_loaded` fires → Sort Mechanic emits `deadlock_detected()` before any player input accepted. No bolt is held — check evaluates full initial board. Verified at integration tier asserting emission during Sort Mechanic initialization phase, before first player-facing frame.

---

## Implementation Notes

*Derived from EPIC.md Key Implementation Notes and ADR-0006 guidelines:*

- Shallow depth-1 check: for each non-empty source column `i`, `held_color = topBolt(stackContents[i])`. For each other column/temp slot `j` (j ≠ i), evaluate `is_legal_move(held_color, j)`. If ANY pair (i, j) returns LEGAL, the board is not deadlocked — return immediately. A "legal first move exists" requires at least one legal (source, destination) pair.
- Algorithm is O(N(N-1)) comparisons where N = `color_count + temp_slot_count`. Maximum N = 11 (8 color + 3 temp) → ~110 comparisons. Negligible on any mobile device.
- `deadlock_detected()` is emitted AFTER win check fails and BEFORE buffered tap fires. This ordering is enforced by the MOVE_EXECUTING exit sequence defined in Story 004.
- `level_loaded` path: Sort Mechanic subscribes to GSM's `OnLevelLoaded` event. On receipt, after board state is readable (init assertions passed), run deadlock check before accepting player input.
- `deadlock_detected()` describes board state at moment of emission — not a persistent condition. HUD must NOT latch hint pulse permanently; deactivate after player's next legal placement.
- Frame-gap discard rule (watchdog path): `board_refresh_forced` received outside MOVE_EXECUTING → silently discard, no state change, no `deadlock_detected()` emission.
- **`tests/helpers/sort-mechanic-fixtures`** must be authored as part of this story. The fixture must represent a board where all possible first moves fail `is_legal_move`. Used by AC-10, AC-22, AC-25. Other stories and future tests reference this fixture by path.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: Win check that runs before `deadlock_detected()` in exit sequence; MOVE_EXECUTING exit ordering setup
- Story 007: Exhaustive deadlock verification — owned by Hint System (not yet authored); Sort Mechanic runs depth-1 only

---

## QA Test Cases

*Test cases not yet defined — run /qa-plan to generate them.*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/gsm-sort-mechanic/sort_mechanic_deadlock_test.cs` — must exist and pass
- `tests/helpers/sort-mechanic-fixtures/` canonical deadlock fixture — must exist (named deliverable)
- AC-22 blocking unit test may live in `tests/unit/sort-mechanic/` since it can run with mock board state

**Status**: [x] All evidence confirmed — 2026-05-19

---

## Completion Notes
**Completed**: 2026-05-19
**Criteria**: 3/3 passing (AC-22, AC-10, AC-25)
**Deviations**: None blocking. Advisory: INVALID_MOVE exit no-deadlock behavior untested; watchdog integration path comment absent; corrupt-board-on-level-loaded suppression untested; ADR-0006 has typo `MoveRejectReason` → `MoveRejectedReason`.
**Test Evidence**: Unit: `tests/unit/sort-mechanic/sort_mechanic_deadlock_test.cs` (8 tests); Integration: `tests/integration/gsm-sort-mechanic/sort_mechanic_deadlock_test.cs` (5 tests); Fixture: `tests/helpers/sort-mechanic-fixtures/DeadlockFixtures.cs`
**Code Review**: Complete — APPROVED WITH SUGGESTIONS (lean mode, run this session)

---

## Dependencies

- Depends on: Story 001 must be DONE; Story 002 must be DONE; Story 003 must be DONE; Story 004 must be DONE
- Unlocks: Story 007
