# Story 006: Watchdog Timer

> **Epic**: Game State Manager
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: Small (2h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/game-state-manager.md`
**Requirement**: `TR-GSM-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Board State Representation
**ADR Decision Summary**: A `WaitForSecondsRealtime` coroutine starts on every `move_committed`. If no `OnMoveExecutingExited` arrives within `watchdog_timeout_ms` (1500ms), GSM increments `current_sequence_id` and emits `board_refresh_forced(sequenceId)`. Board reflects the committed state — no rollback. The timer is cancelled (StopCoroutine) when valid exit signal arrives. Only runs during ACTIVE state.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `WaitForSecondsRealtime` is a stable Unity API. Required over `WaitForSeconds` — must fire even at `Time.timeScale = 0`.

**Control Manifest Rules (Core layer)**:
- Required: `WaitForSecondsRealtime` for watchdog — fires even when `Time.timeScale = 0` (e.g., pause screen). `WaitForSeconds` is forbidden for the watchdog timer — source: ADR-0006
- Required: Cancel watchdog (`StopCoroutine`) on every MOVE_EXECUTING exit path: valid `OnMoveExecutingExited`, WIN (`puzzle_solved()`), TEARDOWN, and `OnDestroy`

---

## Acceptance Criteria

*From GDD `design/gdd/game-state-manager.md`, scoped to this story:*

- [ ] **AC-GSM-18** — `move_committed(sequenceId=N)` fires and no `OnMoveExecutingExited` arrives within `watchdog_timeout_ms`: (1) `current_sequence_id=N+1`; (2) `board_refresh_forced(sequenceId=N+1)` emitted; (3) committed bolt is present in `stack_contents[destination]` — no rollback occurs
- [ ] **AC-GSM-19** — `OnMoveExecutingExited` arrives before watchdog fires: watchdog timer is cancelled; `board_refresh_forced` is NOT emitted; `current_sequence_id` is unchanged
- [ ] **AC-GSM-20** — Stale `animation_complete(seqId=N)` arrives AFTER `board_refresh_forced(seqId=N+1)` has fired: no second `board_refresh_forced` emitted; `_watchdogCoroutine` reference is already null (watchdog already cleared itself)

---

## Implementation Notes

*Derived from ADR-0006 WDG-01–WDG-03:*

```csharp
private Coroutine _watchdogCoroutine;
private const float WatchdogTimeoutSec = 1.5f; // 1500ms — from GDD Tuning Knobs

private void HandleMoveCommitted(int src, int dst, int colorId, long seqId)
{
    // ... BSM-01 mutations (Story 001) ...

    // Start watchdog (cancel any prior — defensive)
    if (_watchdogCoroutine != null) StopCoroutine(_watchdogCoroutine);
    _watchdogCoroutine = StartCoroutine(WatchdogCoroutine());
}

private IEnumerator WatchdogCoroutine()
{
    yield return new WaitForSecondsRealtime(WatchdogTimeoutSec);
    // WDG-01: timer expired — animation completion never arrived
    _currentSequenceId++;
    OnBoardRefreshForced?.Invoke(_currentSequenceId);
    _watchdogCoroutine = null;
}

private void HandleMoveExecutingExited(long seqId)
{
    // WDG-03: cancel timer on valid exit
    if (_watchdogCoroutine != null)
    {
        StopCoroutine(_watchdogCoroutine);
        _watchdogCoroutine = null;
    }
    // Process deferred undo if any (Story 007)
}

private void OnDestroy()
{
    if (_watchdogCoroutine != null) StopCoroutine(_watchdogCoroutine);
}
```

**WDG-02 stale signal**: After `board_refresh_forced` fires (seqId=N+1), any late-arriving `animation_complete(seqId=N)` from the Animation System carries a stale ID. Sort Mechanic discards it via the sequence ID mismatch check — GSM takes no additional action.

**No rollback**: `board_refresh_forced` does not reverse the committed move. Board reflects the committed state. The visual snap (Animation System) updates to match.

**`WaitForSecondsRealtime` is required** — not `WaitForSeconds`. If `Time.timeScale = 0` (pause screen), `WaitForSeconds` never fires. `WaitForSecondsRealtime` fires regardless of time scale. This is a control manifest required pattern.

**Unit testing the watchdog**: Use `UnityTest` with `[UnityTest]` attribute and `yield return new WaitForSecondsRealtime(2f)` to trigger the watchdog asynchronously in Unity Test Framework. Alternatively, inject the timeout as a constructor parameter for a synchronous test seam.

**Performance impact**: Minimal. The watchdog is a single `Coroutine` object alive only during `MOVE_EXECUTING` (≤ 1500ms per move). No allocation on the hot path after the coroutine starts. No CPU cost when idle. `WaitForSecondsRealtime` polling is engine-internal and negligible at 60fps on mobile.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 007: Deferred undo processing on watchdog exit (EC-11)
- Story 001: BSM-01 mutations that happen before the watchdog starts

---

## QA Test Cases

- **AC-GSM-18**: Watchdog fires board_refresh_forced
  - Given: GSM ACTIVE; `move_committed(src=0, dst=1, colorId=3, seqId=N)` processed; watchdog timeout shortened to 50ms for test; event spy
  - When: no `OnMoveExecutingExited` signal arrives within timeout
  - Then: spy contains `board_refresh_forced(seqId=N+1)`; `current_sequence_id=N+1`; `stack_contents[1]` contains `colorId=3` (no rollback)
  - Edge cases: `OnMoveExecutingExited(seqId=N)` arrives before watchdog fires → watchdog timer cancelled; `board_refresh_forced` NOT emitted
  - Edge cases: stale `animation_complete(seqId=N)` arrives AFTER `board_refresh_forced(seqId=N+1)` → no second `board_refresh_forced` emitted (watchdog already cleared)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/game-state-manager/watchdog_timer_test.cs` — must exist and pass (use `[UnityTest]` for async path, or inject timeout seam for synchronous testing)

**Status**: [x] Exists and passes — `tests/unit/game-state-manager/WatchdogTimer_Test.cs` (12 tests covering AC-GSM-18, 19, 20 + 2 edge cases)

---

## Dependencies

- Depends on: Story 001 (DONE) — watchdog starts after BSM-01 mutation
- Unlocks: Story 007 (watchdog exit is one of the deferred-undo processing paths)

---

## Completion Notes
**Completed**: 2026-05-16
**Criteria**: 3/3 passing (AC-GSM-18, AC-GSM-19, AC-GSM-20 — all edge cases covered)
**Deviations**:
- ADVISORY: Test file created as `WatchdogTimer_Test.cs` (PascalCase per project convention); story doc had `watchdog_timer_test.cs` — filename is authoritative.
- ADVISORY: `_watchdogTimeoutSec = 1.5f` is a float literal (injectable via seam, not [SerializeField]). Expose as [SerializeField] if designer-accessible tuning is needed later.
**Test Evidence**: Logic — `tests/unit/game-state-manager/WatchdogTimer_Test.cs` (12 tests)
**Code Review**: Complete (lean mode) — R-1: `HandlePuzzleSolved` missing `CancelWatchdog()` on WIN path found and fixed (ADR-0006 WDG-03 compliance); `docs/architecture/ADR-0006.md` created to resolve story-readiness blocker before implementation
**Open follow-ups**: `HandleMoveExecutingExited(long seqId)` — `seqId` param unused until Story 007 wires deferred undo; consider `_seqId` naming if strict Roslyn analyzer rules are enabled
