# Story 006: App-Pause Cancellation + SEO Contract

> **Epic**: Sort Mechanic
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 0.5d
> **Manifest Version**: 2026-05-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/sort-mechanic.md`
**Requirement**: `TR-SORT-009`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation (primary); ADR-0007: Input Handling Strategy (secondary — `OnApplicationPause` contract)
**ADR Decision Summary**: Sort Mechanic reads board state synchronously from GSM. App-pause contract: Sort Mechanic's `OnApplicationPause(true)` handler must execute before GSM's. SEO ordering (SortMechanic lower number = higher priority than GSM at −50) enforces this guarantee.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `async void OnApplicationPause()` is forbidden — Unity returns control to OS at first `await`; write never completes under iOS suspension. Pause handler must be synchronous. SEO registration in Project Settings is not automatic — must be added manually before implementation.

**Control Manifest Rules (Foundation + Core layers):**
- Required: `OnApplicationPause(true)` handler synchronous (no `async`, no `await`, no `yield`); SortMechanic SEO must be explicitly registered with a value lower than GSM's −50 (e.g., −45) — enforces `OnApplicationPause` call order; `move_cancelled` emission before GSM's serialize call
- Forbidden: `async void OnApplicationPause()` — Unity returns control to OS at first `await`; `StopAllCoroutines()` inside pause handler

---

## Acceptance Criteria

*From GDD `design/gdd/sort-mechanic.md`, scoped to this story:*

- [ ] **AC-28** *(Integration)* — Player in BOLT_SELECTED holding bolt from stack N; `OnApplicationPause(true)` fires → Sort Mechanic transitions CANCELLATION → IDLE synchronously within the handler, emitting `move_cancelled(source=N, color_id)`. Bolt returned to source before `OnApplicationPause` returns. GSM serializes board with `total_bolts == color_count × stack_depth`. Verification: inject `OnApplicationPause(true)` while in BOLT_SELECTED; assert total bolt count correct; assert Sort Mechanic state is IDLE; confirm via GSM call-order tracking that `move_cancelled` emission precedes GSM's serialize call. A deserialized `total_bolts` of `(color_count × stack_depth) - 1` is a test failure.

---

## Implementation Notes

*Derived from EPIC.md EC-14 and ADR-0007 guidelines:*

- `OnApplicationPause(bool pauseStatus)` override in `SortMechanic.cs`. When `pauseStatus == true` and current state is `BoltSelected`: call `CancelHeldBolt()` synchronously (the same method used by S-03/S-05 cancellation). This restores held bolt to source and emits `move_cancelled` before returning.
- Without this rule: S-01 removes bolt from board state immediately on lift; GSM's non-serialization of held state (it only serializes board arrays, not Sort Mechanic state) would yield a deserialized board with one fewer bolt than required — win condition structurally unreachable, no signal to player.
- **SEO ordering requirement**: Sort Mechanic must have a lower SEO number (higher execution priority) than GSM. ADR-0001 SEO table must be extended: add an explicit row for SortMechanic with a value between −50 (GSM) and 0 (current SortMechanic slot). Suggest −45. This ensures `OnApplicationPause` fires Sort Mechanic before GSM on the same frame.
- The Project Settings > Script Execution Order entry must be committed to version control via `ProjectSettings/ProjectSettings.asset`.
- `OnApplicationPause(false)` (foreground restore): Sort Mechanic is in IDLE with correct board. No restore logic required — held state was cancelled.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: Basic `OnApplicationPause` wiring — this story adds the specific BOLT_SELECTED cancellation behavior and SEO contract
- Story 007: Broader GSM integration tests (board reads, watchdog, error cases)

---

## QA Test Cases

*Test cases not yet defined — run /qa-plan to generate them.*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/gsm-sort-mechanic/sort_mechanic_app_pause_test.cs` — must exist and pass
- Physical Android device verification for `OnApplicationPause` behavior under real OS suspension (advisory)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 must be DONE
- Unlocks: Story 007
