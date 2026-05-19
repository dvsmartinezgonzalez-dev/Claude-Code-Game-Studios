# Story 002: Input Handling — Touch, Back Gesture, One-Tap Buffer

> **Epic**: Sort Mechanic
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 1.5d
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-18

## Context

**GDD**: `design/gdd/sort-mechanic.md`
**Requirement**: `TR-SORT-004`, `TR-SORT-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: Input Handling Strategy (primary); ADR-0006: Board State Representation (secondary for buffer discard rules)
**ADR Decision Summary**: All touch via `EnhancedTouch.Touch.activeTouches`; `TouchPhase.Began` fires immediately on first contact. `Keyboard.current?.escapeKey.wasPressedThisFrame` for Android back gesture with mandatory null guard on iOS. One-tap buffer: store `_pendingTap` + `_pendingTapStackIndex`; process on `OnMoveExecutingExited` IDLE path; discard on WIN and watchdog (`OnBoardRefreshForced`).

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `Physics2D.OverlapPoint` + Input System touch coordinate conversion (screen → world via `Camera.ScreenToWorldPoint`) is post-cutoff territory — verify coordinate space on physical device early. `Keyboard.current` is null on iOS (no hardware keyboard device) — the null guard is a required implementation contract, not optional defensive code. Active Input Handling project setting must be "Input System Package (New)" — not "Both".

**Control Manifest Rules (Core layer):**
- Required: `EnhancedTouch.Touch.activeTouches` for touch; `_boltStacksLayerMask` on every `Physics2D.OverlapPoint` call; `if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)` in BOLT_SELECTED; one-tap buffer via `_pendingTap` + `_pendingTapStackIndex`; discard buffer on WIN and `OnBoardRefreshForced`
- Forbidden: legacy `Input` class; `StandaloneInputModule` on EventSystem
- Guardrail: `EventSystem` must use `InputSystemUIInputModule` — verify on every scene

---

## Acceptance Criteria

*From GDD `design/gdd/sort-mechanic.md`, scoped to this story:*

- [ ] **AC-08a** — Sort Mechanic in MOVE_EXECUTING with one tap already buffered: additional taps emit zero signals of any kind. Verified by event-bus spy confirming zero events after first buffered input.
- [ ] **AC-08b** — Sort Mechanic in MOVE_EXECUTING: additional taps after first buffered tap do not change state. Sort Mechanic remains in MOVE_EXECUTING — no drift to IDLE, BOLT_SELECTED, or WIN.
- [ ] **AC-08c** — Sort Mechanic in MOVE_EXECUTING: additional taps after first produce zero GSM board state writes. Verified by mock GSM confirming no mutation methods called after first tap.
- [ ] **AC-12** — Player in BOLT_SELECTED: `Keyboard.current` non-null AND `Keyboard.current.escapeKey.wasPressedThisFrame` true → `move_cancelled` emitted, CANCELLATION entered, bolt returns to source. **Null guard mandatory**: `if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)` — omitting crashes iOS in BOLT_SELECTED. AC is Android-only (iOS cannot trigger; null guard prevents crash).
- [ ] **AC-29b** — Sort Mechanic commits the only bolt from stack A (emptying it); tap targeting stack A was buffered during MOVE_EXECUTING. On IDLE exit, buffered tap fires against now-empty stack A → treated as S-02: Sort Mechanic stays in IDLE, no event emitted, no GSM mutation. Verified with `color_count = 2, stack_depth = 1` board.
- [ ] **AC-30** — Sort Mechanic in INVALID_MOVE (rejection animation active); tap arrives targeting a valid legal destination (empty stack). On rejection animation complete and BOLT_SELECTED re-entry: buffered tap fires as destination evaluation → `move_committed` emitted, Sort Mechanic transitions to MOVE_EXECUTING. Verified with mock animation system withholding `rejection_animation_complete` until explicitly triggered.
- [ ] **AC-30b** — Sort Mechanic in INVALID_MOVE with one tap already buffered: additional taps during same rejection animation window are discarded. Only the first buffered tap fires on BOLT_SELECTED re-entry.
- [ ] **AC-14** *(advisory)* — `temp_slot_count = 0`: tap injected targeting non-existent slot index — if bolt held: Sort Mechanic treats as empty space → CANCELLATION, `move_cancelled`. If no bolt held: no event, no GSM change.

---

## Implementation Notes

*Derived from EPIC.md Key Implementation Notes and ADR-0007 guidelines:*

- MOVE_EXECUTING one-tap buffer: store `_pendingTap` (bool) + `_pendingTapStackIndex` (int) as private fields. On first tap during MOVE_EXECUTING, set `_pendingTap = true` and record stack index. All subsequent taps: check `_pendingTap`; if already set, discard entirely. On IDLE exit: fire buffered tap as new selection (behaves as IDLE tap — S-01 rules). On WIN exit: clear buffer without firing.
- INVALID_MOVE one-tap buffer: separate from MOVE_EXECUTING buffer. Buffers exactly one tap during rejection animation (60–200ms). On BOLT_SELECTED re-entry, fires buffered tap as S-04 destination evaluation: valid → MOVE_EXECUTING; invalid → INVALID_MOVE again; source or empty space → CANCELLATION.
- Android back gesture: check `Keyboard.current.escapeKey.wasPressedThisFrame` inside `Update()`, guarded by `Keyboard.current != null`. Only active in BOLT_SELECTED state → call `CancelHeldBolt()`. No-op in all other states. No separate `BackGestureHandler` MonoBehaviour required.
- Do NOT add `android:enableOnBackInvokedCallback` to AndroidManifest — if added, `escapeKey` back gesture stops working and requires migration to `Application.onBackReceived` per ADR-0007.
- `Physics2D.OverlapPoint` call: `Physics2D.OverlapPoint(worldPos, _boltStacksLayerMask)`. Convert touch position via `_mainCamera.ScreenToWorldPoint(new Vector3(touch.screenPosition.x, touch.screenPosition.y, 0))`. Cache camera in `Awake`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: FSM state enum, initialization assertions, cancellation logic
- Story 003: `is_legal_move` validation formula — this story dispatches taps; Story 003 decides if the move is legal
- Story 006: `OnApplicationPause` cancellation (separate input path from touch/keyboard)

---

## QA Test Cases

*Test cases not yet defined — run /qa-plan to generate them.*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/sort-mechanic/sort_mechanic_input_test.cs` — must exist and pass

**Status**: [x] `tests/unit/sort-mechanic/sort_mechanic_input_test.cs` — 29 tests (AC-08a/b/c, AC-12, AC-29b, AC-30, AC-30b, AC-14)

---

## Dependencies

- Depends on: Story 001 must be DONE
- Unlocks: Story 005, Story 007

---

## Completion Notes
**Completed**: 2026-05-18
**Criteria**: 8/8 passing (AC-14 advisory — covered)
**Deviations**: ADVISORY — `SortMechanic_Fsm_Test.cs` modified out of scope to fix `EventSpy` (add `OnMoveExecutingExited`); WIN path `DiscardPendingTap` wiring deferred to Story 004 (in scope)
**Test Evidence**: Logic — `tests/unit/sort-mechanic/sort_mechanic_input_test.cs` (29 tests)
**Code Review**: Complete — APPROVED after fixes (2026-05-18)
