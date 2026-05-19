# Story 001: SortMechanic FSM Core + Initialization

> **Epic**: Sort Mechanic
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 1.5d
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-17

## Context

**GDD**: `design/gdd/sort-mechanic.md`
**Requirement**: `TR-SORT-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation (primary); ADR-0007: Input Handling Strategy (secondary for EnhancedTouch setup)
**ADR Decision Summary**: GSM is sole owner of board arrays; Sort Mechanic reads via `IReadOnlyList<int>[]` synchronously on demand and emits C# events. All tap input dispatched via `EnhancedTouchSupport` + `Physics2D.OverlapPoint` against the BoltStacks layer.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `EnhancedTouchSupport.Enable()` must be called in `Awake` — not `Start`. `Physics2D.OverlapPoint` requires a cached `LayerMask` (cache in `Awake`). `[SerializeField]` on properties/methods is a compile error in Unity 6.3 — use backing fields only.

**Control Manifest Rules (Feature layer — Core layer also applies):**
- Required: `EnhancedTouchSupport.Enable()` in `Awake`; `Physics2D.OverlapPoint` with `_boltStacksLayerMask`; typed `event Action<T>`; `?.Invoke()` on all event calls; named-method subscribers; `Keyboard.current` null guard
- Forbidden: legacy `Input` class; `FindObjectOfType`; `[SerializeField]` on properties or methods; `UnityEvent`; lambda subscribers
- Guardrail: SortMechanic SEO = 0 (lower priority than GSM at −50); must be registered in Script Execution Order before implementation begins

---

## Acceptance Criteria

*From GDD `design/gdd/sort-mechanic.md`, scoped to this story:*

- [ ] **AC-07** — Cancellation via source tap returns bolt to source, no undo entry. `move_cancelled(source, color_id)` emitted; board state identical to pre-lift; mock GSM spy confirms `RecordUndo()` was never called.
- [ ] **AC-15a** — Legal move emits `move_committed(source, destination, color_id, sequence_id)`; cancellation emits `move_cancelled` and does NOT emit `move_committed`.
- [ ] **AC-17** — Tap not matching any stack/slot index while holding triggers CANCELLATION: `move_cancelled` emitted, Sort Mechanic transitions to IDLE, board reverts to pre-lift state. (S-05 dead-zone spatial constraint is input-layer concern — not unit-tier.)
- [ ] **AC-18b** — Board where `len(color_stacks) ≠ color_count`: `level_load_failed(reason: CORRUPTED_BOARD_STATE)` emitted and `IDiagnosticLogger.Error(category: "SortMechanic", payload: {reason: CORRUPTED_BOARD_STATE})` called. Verified via mock logger — not message text.
- [ ] **AC-18c** — After any `level_load_failed` emission, all subsequent tap events produce zero Sort Mechanic event emissions and zero GSM state mutations.
- [ ] **AC-21** — CANCELLATION exits to IDLE synchronously within same `Update()` call stack as `move_cancelled` emission — no yield, no deferred invoke, no `animation_complete` wait. Assert `_currentState == SortMechState.Idle` on immediately following line.
- [ ] **AC-27** — Board where `temp_slot_depth > stack_depth`: `level_load_failed(reason: CORRUPTED_BOARD_STATE)` emitted and `IDiagnosticLogger.Error` called. Input blocked thereafter (verified by AC-18c pattern).
- [ ] **AC-09** *(advisory)* — Player taps empty color stack (nothing held): Sort Mechanic remains in IDLE, no event emitted, no board state change.

---

## Implementation Notes

*Derived from EPIC.md Key Implementation Notes and ADR-0006/ADR-0007 guidelines:*

- State enum: `SortMechState { Idle, BoltSelected, MoveExecuting, Cancellation, InvalidMove, Win }`. Private field `_currentState`.
- Initialization asserts three conditions before accepting any input:
  1. `colorStacks.Length == colorCount` — mismatch silently excludes stacks from win check
  2. `tempSlotDepth <= stackDepth` — violation allows hoarding more bolts in temp than any stack holds
  3. All `colorId` values in level data belong to domain `{1..colorCount}` — phantom ID makes win structurally unreachable
  If any fails: emit `level_load_failed(CORRUPTED_BOARD_STATE)`, log via `IDiagnosticLogger.Error`, set `_inputBlocked = true`, return. A hard crash is not acceptable on mobile.
- CANCELLATION exits to IDLE synchronously on `move_cancelled` emission. No animation handshake on this path. Cancel animation plays asynchronously — Sort Mechanic accepts new input immediately.
- `sequence_id` must be `long` (C# `int64`), never `int`. `int32` wrapping to negative in unchecked context produces permanent MOVE_EXECUTING softlock. Session-global — never resets within one app session.
- `EnhancedTouchSupport.Enable()` in `Awake`; `EnhancedTouchSupport.Disable()` in `OnDestroy`.
- Cache `Camera.main` in `Awake` — never call per-frame.
- Cache `LayerMask.GetMask("BoltStacks")` in `Awake` as `_boltStacksLayerMask`.
- Subscribe to GSM events in `Awake` using named methods; unsubscribe in `OnDestroy` with null guard.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: One-tap input buffer during MOVE_EXECUTING; INVALID_MOVE buffering; Android back gesture
- Story 003: Move validation formula (`is_legal_move`); column cap assertion
- Story 004: Win condition check; sequence ID stale-signal guard; `OnMoveExecutingExited`
- Story 005: Deadlock detection (`deadlock_detected()`)
- Story 006: `OnApplicationPause` cancellation + SEO ordering contract

---

## QA Test Cases

*Test cases not yet defined — run /qa-plan to generate them.*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/sort-mechanic/sort_mechanic_fsm_test.cs` — must exist and pass

**Status**: [x] Created and passing — `tests/unit/sort-mechanic/SortMechanic_Fsm_Test.cs` (19 tests, all pass — confirmed 2026-05-17)

---

## Dependencies

- Depends on: None
- Unlocks: Story 002, Story 003, Story 006

---

## Completion Notes

**Completed**: 2026-05-17
**Criteria**: 8/8 passing (AC-07, AC-09, AC-15a, AC-17, AC-18b, AC-18c, AC-21, AC-27 — all COVERED)
**Deviations**:
- ADVISORY: `Camera.ScreenToWorldPoint` passes `nearClipPlane` as z; for 2D orthographic `Mathf.Abs(camera.transform.position.z)` is clearer. No runtime impact.
- ADVISORY: `_boltStacksLayerMask` typed as `int` rather than `LayerMask`. Cosmetic only.
**Test Evidence**: Logic — `tests/unit/sort-mechanic/SortMechanic_Fsm_Test.cs` (19 tests, all pass)
**Code Review**: Complete — `/code-review` run this session; 4 required changes applied (AddComponent test pattern, temp-slot phantom color test, `InitializeBoard` extraction, unified dispatch)
**Extra files created**: `SortMechEnums.cs`, `IDiagnosticLogger.cs`, `BoltStackIndex.cs`, `Tests.Unit.SortMechanic.asmdef`, `AssemblyInfo.cs` (InternalsVisibleTo) — all justified, not scope creep
