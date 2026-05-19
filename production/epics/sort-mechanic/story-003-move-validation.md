# Story 003: Move Validation + Column Cap Assertion

> **Epic**: Sort Mechanic
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 1.0d
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-18

## Context

**GDD**: `design/gdd/sort-mechanic.md`
**Requirement**: `TR-SORT-002`, `TR-SORT-010`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation (primary); ADR-0013: Level Layout Column Cap (secondary)
**ADR Decision Summary**: Sort Mechanic reads board state via `IReadOnlyList<int>[]` synchronously on demand. Move validation uses a guarded conditional — `destination_top_color` must NOT be read before the empty-slot guard (undefined on empty slots). `color_count + temp_slot_count ≤ 8` enforced via initialization assertion.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff risk on this story. Pure logic — no Unity API calls beyond GSM interface reads.

**Control Manifest Rules (Core layer):**
- Required: Guarded conditional for `is_legal_move` (evaluate `destination_bolt_count == 0` FIRST before reading `destination_top_color`); `IReadOnlyList<int>[]` read from GSM; `color_count + temp_slot_count ≤ 8` init assertion emitting `level_load_failed`
- Forbidden: Eager boolean evaluation reading `destination_top_color` before empty-slot guard; writing board state from Sort Mechanic
- Guardrail: Win condition check O(colorCount × stackDepth) ≤ 64 iterations, no allocation

---

## Acceptance Criteria

*From GDD `design/gdd/sort-mechanic.md`, scoped to this story:*

- [ ] **AC-01** — Empty destination accepts any color: board with one empty color stack; player holds bolt of color 3. Tap empty stack → `move_committed(source, empty_stack, color_id=3)` emitted; Sort Mechanic enters MOVE_EXECUTING; bolt placed regardless of which color "belongs" in that stack.
- [ ] **AC-02** — Color match, stack not full: stack containing [1,1] with `stack_depth = 4`; player holds bolt color 1. Tap that stack → `move_committed` emitted; stack becomes [1,1,1].
- [ ] **AC-03** — Color mismatch: stack top = color 2; player holds color 1. Tap → `move_rejected(source, destination, color_id=1, reason=COLOR_MISMATCH)` emitted; Sort Mechanic enters INVALID_MOVE then BOLT_SELECTED; player still holds bolt.
- [ ] **AC-04** — Full destination: stack [3,3,3,3] with `stack_depth = 4`; player holds color 3. Tap → `move_rejected(source, destination, color_id=3, reason=DESTINATION_FULL)` emitted; bolt remains held.
- [ ] **AC-11** — Temp slot depth = 1 containing one bolt: player taps it → Sort Mechanic enters BOLT_SELECTED with bolt held. No lock-in. Behaves identically to top bolt of a color stack.
- [ ] **AC-16** — Multi-rejection stability: source stack = [1] (color 1, lifted); dest_A = [2,2,2] full; dest_B = [3,3,3] full; dest_C = [2] mismatch; legal_dest = [] empty; `stack_depth = 3`. Player sequentially taps dest_A, dest_B, dest_C (three `move_rejected` events), then taps legal_dest → `move_committed(source, legal_dest, color_id=1)` emitted; Sort Mechanic transitions to MOVE_EXECUTING. State machine must not drift or drop held bolt across three rejections.

---

## Implementation Notes

*Derived from EPIC.md Key Implementation Notes and ADR-0006 guidelines:*

- `is_legal_move(held_color, destination)` must be implemented as a guarded conditional, not a flat bool:
  ```csharp
  if (destination.BoltCount == 0)               return Legal;   // empty — any color
  if (destination.BoltCount >= destination.Cap)  return Illegal; // full
  if (destination.TopColor == held_color)        return Legal;   // match, not full
  return Illegal;                                                  // mismatch
  ```
  An eager boolean evaluator would dereference `destination.TopColor` before the empty guard — undefined for empty slots. The pseudocode above is authoritative.
- Capacity: `stackDepth` for color stacks; `tempSlotDepth` for temp slots. `tempSlotDepth` is bounded above by `stackDepth` (init assertion 2 in Story 001 enforces this).
- Column cap assertion (`color_count + temp_slot_count ≤ 8`): run at initialization. Failure → emit `level_load_failed(CORRUPTED_BOARD_STATE)`. This is a layout safety constraint; at the 8-column limit, 375pt viewport (iPhone SE) yields exactly 47pt per column — 3pt above iOS HIG minimum. Any more columns makes tap target compliance impossible without a layout redesign.
- Tapping the source while holding is cancellation (S-03), not a validation attempt — guard against this before calling `is_legal_move`.
- Board state during BOLT_SELECTED reflects bolt as already removed from source (S-01 removes immediately). Source appears one bolt shorter during BOLT_SELECTED. On cancel, bolt is restored.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: FSM transitions driven by the LEGAL/ILLEGAL outcome — this story computes the outcome only
- Story 004: Win condition check that runs AFTER a successful move completes
- Story 007: Integration-level verification that GSM correctly refuses malformed boards before Sort Mechanic sees them (AC-19, AC-26)

---

## QA Test Cases

*Test cases not yet defined — run /qa-plan to generate them.*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/sort-mechanic/sort_mechanic_validation_test.cs` — must exist and pass

**Status**: [x] `tests/unit/sort-mechanic/sort_mechanic_validation_test.cs` — 15 tests (AC-01/02/03/04/11/16, TR-SORT-010, boundary values)

---

## Dependencies

- Depends on: Story 001 must be DONE
- Unlocks: Story 004, Story 005

---

## Completion Notes
**Completed**: 2026-05-18
**Criteria**: 6/6 passing
**Deviations**: ADVISORY — `AssertNoPhantomColorIds` null-slot pass (bounded by LDS validation); `SortMechEnums.cs` modified out of scope to fix `MoveRejectedReason.None = 0` sentinel
**Test Evidence**: Logic — `tests/unit/sort-mechanic/sort_mechanic_validation_test.cs` (15 tests)
**Code Review**: Complete — APPROVED WITH SUGGESTIONS after fixes (2026-05-18)
