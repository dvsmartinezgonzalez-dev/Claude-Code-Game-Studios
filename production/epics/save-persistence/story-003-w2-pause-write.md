# Story 003: W-2 Synchronous Pause Write and Dirty Flag

> **Epic**: Save & Persistence
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 0.5 days
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-22

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirements**: `TR-SP-008` (W-2 path)

| TR-ID | Requirement |
|-------|-------------|
| TR-SP-008 | W-2 (OnApplicationPause) synchronous on main thread; no async void; dirty flag gates the write |

**ADR Governing Implementation (Primary)**: ADR-0003: Save System Design
**ADR Decision Summary**: `OnApplicationPause(true)` performs a synchronous write using `_writeLock.Wait(destroyCancellationToken)` (synchronous overload — not `WaitAsync`). No `await` expression may appear in the `OnApplicationPause` body. Dirty flag is checked **after** acquiring the lock (post-lock, not pre-lock) to prevent a race where W-1 clears the dirty flag between the pre-lock check and lock acquisition.

**ADR Secondary Reference**: ADR-0001: Singleton Architecture and Boot Sequence (app lifecycle contract)

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `async void OnApplicationPause()` is forbidden — Unity returns control to the OS at the first `await`, abandoning the write under iOS suspension. `destroyCancellationToken` is Unity 6.0+ — confirms the MonoBehaviour lifecycle is tied to this token. `OnApplicationFocus(false)` must NOT trigger a write — only `OnApplicationPause(true)` is the designated trigger.

**Control Manifest Rules (Foundation layer)**:
- Required: `_writeLock.Wait(destroyCancellationToken)` (synchronous); dirty check post-lock; `try/finally { _writeLock.Release(); }` wraps W-2 I/O
- Forbidden: `async void OnApplicationPause()`; any `await` inside `OnApplicationPause` body
- Guardrail: W-2 synchronous write must complete in < 4 seconds on target devices (17 KB ≈ 2–8 ms expected)

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-06** — Dirty flag `false`: `OnApplicationPause(true)` fires → no file I/O; `save.json` modification timestamp unchanged
- [ ] **AC-07** — `OnApplicationFocus(false)` fires: no write initiated; dirty state unchanged
- [ ] **AC-08** — Dirty flag `true` + `OnApplicationPause(true)`: `_writeLock.Wait()` acquired synchronously; write-then-swap completes before callback returns
- [ ] **AC-08b** *(Advisory)* — Physical device timing: callback-entry to return < 4 seconds on iPhone SE 2nd gen (A13, iOS 16) and Galaxy A13 (Exynos 850, Android 12). Evidence: `production/qa/evidence/ac-08b-device-timing.md`
- [ ] **AC-09** — W-2 write succeeds → dirty flag `false`; W-2 write throws `IOException` → dirty flag remains `true`
- [ ] **AC-42** — `destroyCancellationToken` cancelled mid-W-2 `_writeLock.Wait()`: `OperationCanceledException` caught silently; dirty flag remains `true`; no write; no exception propagates outside `OnApplicationPause`

---

## Implementation Notes

*Derived from ADR-0003 Implementation Guidelines:*

**W-2 implementation** — synchronous, no `await`:
```csharp
void OnApplicationPause(bool pauseStatus) {
    if (!pauseStatus) return;   // only handle suspend, not resume
    try {
        _writeLock.Wait(destroyCancellationToken);  // synchronous overload
    } catch (OperationCanceledException) {
        // MonoBehaviour destroyed — dirty flag stays true; do not write
        return;
    }
    try {
        if (!_isDirty) return;  // post-lock check (not pre-lock — prevents dirty race with W-1)
        // Perform synchronous write-then-swap — same FileStream+Flush+Replace/Move as W-1
        // but executed synchronously here (no BackgroundThreadAsync)
        PerformSynchronousWrite();
        _isDirty = false;
    } catch (IOException ex) {
        // _isDirty remains true — will retry on next trigger
        LogAnalytics("w2_write_failed", ex);
    } catch (UnauthorizedAccessException ex) {
        LogAnalytics("w2_unauthorized", ex);
    } finally {
        _writeLock.Release();
    }
}
```

**W-2-during-W-1 no-deadlock precondition** (GDD C.1): W-2 blocks the main thread in `_writeLock.Wait()` while W-1 holds the lock on a background thread. This is safe ONLY because W-1's locked section contains NO Unity API calls. Violating this constraint causes permanent deadlock. The compiler cannot enforce this — document it prominently in the W-1 locked section.

**Dirty flag state machine**:
- Set to `true` on any in-memory state mutation (GSM board state, coin balance update, level completion)
- Set to `false` only on successful write-then-swap completion
- Remains `true` on any write failure, allowing retry on next W-1 or W-2 trigger

---

## Out of Scope

- Story 002: W-1 `WriteCompletionAtomic`, background thread path
- Story 006: `SetCoinBalance()` that sets dirty flag `true`

---

## QA Test Cases

*Embedded from `production/qa/qa-plan-sprint3-2026-05-22.md`.*

- **AC-06 / Pause_DirtyFalse_NoFileIO**
  - Given: dirty flag = `false`
  - When: `OnApplicationPause(true)` fires
  - Then: no File.Replace or File.Move called; `save.json` modification timestamp unchanged

- **AC-07 / Pause_OnApplicationFocus_NoWrite**
  - Given: dirty flag = `true`
  - When: `OnApplicationFocus(false)` fires
  - Then: no write initiated; dirty flag remains `true`

- **AC-08 / Pause_DirtyTrue_SynchronousWrite**
  - Given: dirty flag = `true`, `FakeFileSystem.WriteDelay = 50ms`
  - When: `OnApplicationPause(true)` fires
  - Then: `_writeLock.Wait()` acquired synchronously; write completes before callback returns

- **AC-09 success / Pause_WriteSuccess_DirtyFlagCleared**
  - Given: dirty flag = `true`, write succeeds
  - When: `OnApplicationPause(true)` fires
  - Then: dirty flag = `false` after write

- **AC-09 failure / Pause_WriteIOException_DirtyFlagRetained**
  - Given: dirty flag = `true`, `FakeFileSystem` throws `IOException`
  - When: `OnApplicationPause(true)` fires
  - Then: dirty flag remains `true`; exception logged; game remains playable

- **AC-42 / Pause_CancellationToken_OperationCanceledSilently**
  - Given: `destroyCancellationToken` cancelled while W-2 blocked in `_writeLock.Wait()`
  - When: `OperationCanceledException` propagates
  - Then: caught silently; dirty flag remains `true`; no write; no exception outside `OnApplicationPause`; `save.json` unchanged

- **Pause_W2AfterW1_DirtyCheckPostLock**
  - Given: W-1 executing (lock held via `FakeFileSystem.WriteDelay`)
  - When: `OnApplicationPause(true)` fires
  - Then: W-2 blocks on `.Wait()`; W-1 completes + clears dirty; W-2 acquires lock; checks dirty = `false`; performs no I/O (no deadlock)

- **Pause_AsyncVoidForbidden_CompileCheck**
  - When: `OnApplicationPause` method signature inspected via reflection
  - Then: method is `void OnApplicationPause(bool)`, not `async void`

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/save-persistence/SaveSystem_Pause_Test.cs` — must exist and all tests pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (boot, IFileSystem seam), Story 002 (W-1 path, `_writeLock` established)
- Unlocks: Story 008 (SP↔GSM round-trip integration test, which exercises W-2)
