# Technical Debt Register

## TD-CI-001 — Unity CI Pipeline Instability (GameCI + License)

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-22 |
| **Severity** | Medium |
| **Area** | Infrastructure / DevOps |
| **Blocking** | No — Sprint progress decoupled per user instruction |
| **Sprint** | Carry into Sprint 3 backlog |

### Description

The GitHub Actions CI pipeline (`.github/workflows/tests.yml`) is unstable due to GameCI
Unity license activation issues. Multiple fix attempts across commits 309f480–c0ea5ca failed
to resolve the license handshake between `game-ci/unity-activate@v2` and the Personal license
secrets (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`).

### Impact

- CI cannot be used as the authoritative green gate for test results.
- Local EditMode test suite (309/309 passing, confirmed 2026-05-21) is the source of truth.
- Stories S2-05 and S2-09 lack physical-device test evidence (input coordinate space and
  app-pause behavior). These are advisory gaps, not blocking failures.

### Resolution Plan

1. Switch from Personal license to Unity Pro/Plus license activation **or** use
   `game-ci/unity-test-runner@v4` with `unityVersion: auto` and a self-hosted runner
   that has an active seat already cached.
2. Alternatively: migrate to a Unity license server approach (floating license).
3. Capture physical-device evidence for S2-05 and S2-09 before Alpha gate.

### Acceptance Criteria (when resolved)

- CI runs `game-ci/unity-test-runner@v4` and reports 309/309 (or current count) green on push to main.
- Pipeline badge shows green on main branch README.
- No manual intervention required to activate license.

---

## TD-SP-001 — SaveSystem: cross-thread `volatile` missing on `_isDirty` and `_lastWriteError`

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-22 |
| **Severity** | Low (fixed before merge) |
| **Area** | Save & Persistence |
| **Blocking** | No — fixed during Story 002 code review |
| **Story** | production/epics/save-persistence/story-002-atomic-write-w1.md |

### Description

`_isDirty` (bool) and `_lastWriteError` (string) were written on the background thread inside `WriteAtomicCore` catch blocks and read on the main thread after `Awaitable.MainThreadAsync()`. Both fields lacked `volatile`, which allows the JIT/CPU to cache values in registers and delay cross-thread visibility. Fixed to `private volatile bool _isDirty` and `private volatile string _lastWriteError` before story close.

### Resolution

Fixed in `My project/Assets/_Project/Scripts/SaveSystem/SaveSystem.cs` during Story 002 code review (2026-05-22). No follow-up required.

---

## TD-SP-002 — SaveSystem: AC-35 `SetCoinBalance` INT_MAX clamp has no unit test

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-22 |
| **Severity** | Low |
| **Area** | Save & Persistence |
| **Blocking** | No |
| **Story** | production/epics/save-persistence/story-002-atomic-write-w1.md |

### Description

`SetCoinBalance(int balance)` now clamps via `Math.Clamp(balance, 0, int.MaxValue)` (added during Story 002 code review). The clamp implementation is correct but AC-35 has no corresponding unit test in `SaveSystem_AtomicWrite_Test.cs`. The `int` parameter type means the upper clamp (`int.MaxValue`) is technically a no-op, but the lower clamp (0) prevents negative values from a buggy caller.

### Resolution Plan

Add a test `Write_SetCoinBalance_NegativeBalance_ClampedToZero` and `Write_SetCoinBalance_MaxIntValue_Stored` in Story 006 (`SetCoinBalance` PlayerPrefs.Save() integration). The test for the negative clamp is the more meaningful assertion.

---

## TD-SP-003 — SaveSystem: PlayMode tests for AC-05, AC-30, AC-43 not yet scaffolded

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-22 |
| **Severity** | Medium |
| **Area** | Save & Persistence — Concurrency |
| **Blocking** | No (EditMode proxies cover the synchronous path) |
| **Story** | production/epics/save-persistence/story-002-atomic-write-w1.md |

### Description

Three ACs require PlayMode testing (Awaitable thread-switching cannot be simulated in EditMode):
- **AC-05**: Concurrent W-1+W-1 — both completion records in `save.json` after overlapping writes
- **AC-30**: `_writeLock` serializes W-1+W-2 concurrent writes — valid JSON after all writes resolve
- **AC-43**: `Thread.IsBackground == true` AND not main thread at FileStream construction after second `BackgroundThreadAsync()`

EditMode proxies verify the synchronous WriteAtomicCore path but cannot prove thread-switching behavior.

### Resolution Plan

Add a `SaveSystem_AtomicWrite_PlayMode_Test.cs` to `My project/Assets/_Project/Tests/unit/save-persistence/` in the integration testing story (S3-08) or as a standalone polish story. These tests require `[UnityTest]` + coroutine harness.

---

## TD-SP-004 — SaveSystem: `PerformColdStartRead` exceeds 40-line method limit

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-22 |
| **Severity** | Low |
| **Area** | Save & Persistence |
| **Blocking** | No |
| **Story** | production/epics/save-persistence/story-002-atomic-write-w1.md |

### Description

`PerformColdStartRead()` (~68 lines, lines 154–222 in SaveSystem.cs) exceeds the project's 40-line method standard. The method handles cold-start dispatch (R-1 through R-5) in a single body. It is readable but would benefit from extracting the JSON parse + case dispatch into a `TryParseSaveFile(string json)` helper.

### Resolution Plan

Refactor during Story 004 (iOS retry) or Story 005 (migration) when additional cold-start logic is added and the method would grow further. Do not refactor in isolation — wait for a story that already touches the method.

---

## TD-SP-005 — SaveSystem: AC-07 `OnApplicationFocus` coverage gap

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-22 |
| **Severity** | Low |
| **Area** | Save & Persistence — W-2 |
| **Blocking** | No |
| **Story** | production/epics/save-persistence/story-003-w2-pause-write.md |

### Description

AC-07 specifies that `OnApplicationFocus(false)` must not trigger a write. The test `Pause_PauseStatusFalse_NoWrite` verifies that `HandleApplicationPause(false, ...)` is a no-op, but does not verify the actual `OnApplicationFocus` callback. If `OnApplicationFocus` were accidentally implemented with write logic in a future story, the existing test would not catch it.

### Resolution Plan

Add a reflection test in the S3-08 integration story (or a targeted cleanup story): `typeof(SaveSystem).GetMethod("OnApplicationFocus", ...)` — assert it either does not exist or has no write path. Alternatively, add a `HandleApplicationFocus` seam and a corresponding test.

---

## TD-SP-006 — SaveSystem: PlayMode test for W-1+W-2 concurrent dirty-flag race not written

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-22 |
| **Severity** | Medium |
| **Area** | Save & Persistence — Concurrency |
| **Blocking** | No (post-lock dirty check is architecturally correct per ADR-0003) |
| **Story** | production/epics/save-persistence/story-003-w2-pause-write.md |

### Description

`Pause_W2AfterW1_DirtyCheckPostLock` — the test that verifies W-2 blocks on `_writeLock.Wait()` while W-1 holds it, then sees `_isDirty=false` after W-1 completes — requires a concurrent W-1 `Awaitable.BackgroundThreadAsync()` call, which is only available in PlayMode. The EditMode proxy (`Pause_PostLockDirtyCheck_SkipsWriteWhenDirtyFalse`) verifies lock-release semantics but not the concurrent race scenario.

### Resolution Plan

Implement as a `[UnityTest]` PlayMode test in the SP↔GSM integration story (S3-08).

---

## TD-SP-007 — SaveSystem: iOS retry timing seam is instance-level — two tests run ~5s each

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-23 |
| **Severity** | Low (CI speed only; tests are correct) |
| **Area** | Save & Persistence — iOS cold-start |
| **Blocking** | No |
| **Story** | production/epics/save-persistence/story-004-ios-retry-corruption-recovery.md |

### Description

`SaveSystem.RetryIntervalMs` and `RetryTimeoutMs` are instance-level `internal int` fields. Tests cannot set them before `AddComponent<SaveSystem>()` fires `Awake()` because Unity calls `Awake()` synchronously inside `AddComponent`. Two tests (`Read_UnauthorizedAccessException_Timeout_FallsBackToDefaults` and `Read_RetryInterval_AtMost20Attempts_ThenDefaults`) therefore run at the production 250ms interval × 20 retries = ~5 seconds each, adding ~10 seconds to the EditMode test suite.

### Resolution Plan

Add a static pre-boot override following the `SetFileSystemForTesting` pattern:

```csharp
internal static void SetRetryParametersForTesting(int intervalMs, int timeoutMs)
{
    s_testRetryIntervalMs = intervalMs;
    s_testRetryTimeoutMs  = timeoutMs;
}
```
Read these in `ReadWithIosRetry` (e.g. `int interval = s_testRetryIntervalMs > 0 ? s_testRetryIntervalMs : RetryIntervalMs;`). Clear in `ClearInstanceForTesting()`. Update the two affected tests to use 10ms/100ms timing. Implement in Story 005 or as a standalone cleanup story before Alpha gate. The test uses `FakeFileSystem.WriteDelay` to hold the W-1 lock, fires `HandleApplicationPause(true, ...)` on the main thread which blocks on `Wait()`, then lets W-1 complete. Assert no redundant file write from W-2. This is a **blocking open item** for S3-08 — not merely advisory.

---

## TD-SP-008 — SaveSystem: PlayerPrefs test TearDown does not clean audio./qts. keys

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-23 |
| **Severity** | Low |
| **Area** | Save & Persistence — Tests |
| **Blocking** | No |
| **Story** | production/epics/save-persistence/story-006-playerprefs-setcoinbalance.md |

### Description

`SaveSystem_PlayerPrefs_Test.TearDown` only deletes `sp.downgrade_notice_shown`. The test `SaveSystem_DoesNotWriteAudioOrQtsPlayerPrefsKeys` deletes `audio.sfx_volume`, `audio.ambient_volume`, `audio.ui_volume`, and `qts.tier` in the test body but not in TearDown. If future tests leave those keys set to non-zero values, this test could produce incorrect assertions (DeleteKey in body guards against prior state but not against state left by this test for subsequent tests). Additionally, `GetInt` is used to check float-declared keys — a rogue `SetFloat` write would not be detected.

### Resolution Plan

1. Move the four `DeleteKey` calls from the test body into `TearDown` alongside `sp.downgrade_notice_shown`.
2. Replace `PlayerPrefs.GetInt("audio.*", 0) == 0` assertions with `!PlayerPrefs.HasKey("audio.*")` to detect float or string writes as well.
Implement in the SP integration test story (S4-03) or as a quick cleanup alongside that story.

---

## TD-SP-009 — SaveSystem: AC-10 PlayerPrefs.Save() not mechanically verifiable in EditMode

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-23 |
| **Severity** | Low |
| **Area** | Save & Persistence — Tests |
| **Blocking** | No (confirmed by code review at call site) |
| **Story** | production/epics/save-persistence/story-006-playerprefs-setcoinbalance.md |

### Description

AC-10 requires `PlayerPrefs.Save()` to be called after every `PlayerPrefs.Set*()`. The Unity `PlayerPrefs` API does not expose a call count or any observable indicating whether `Save()` was flushed to disk. The EditMode test `Downgrade_R5_SetsDowngradeNoticeKeyAsInt` verifies that `SetInt` was called (key = 1) but cannot confirm `Save()` was called. AC-10 compliance is enforced only by code review at the `HandleDowngrade` call site (line 561 of `SaveSystem.cs`).

### Resolution Plan

The gap is platform-architectural — Unity provides no test double for `PlayerPrefs.Save()`. Mitigate by: (a) adding a comment in `HandleDowngrade` calling out AC-10 explicitly (already present); (b) ensuring `/code-review` checklists include AC-10 as a manual verification item for any story touching PlayerPrefs writes. Revisit if a custom `IPlayerPrefs` abstraction is introduced in a future story.

---

## TD-SP-010 — SaveSystem: SetCoinBalance missing GuardIsReady() pre-condition enforcement

| Field | Value |
|-------|-------|
| **Logged** | 2026-05-23 |
| **Severity** | Low |
| **Area** | Save & Persistence |
| **Blocking** | No |
| **Story** | production/epics/save-persistence/story-006-playerprefs-setcoinbalance.md |

### Description

AC-07 states `SetCoinBalance` is "usable only after `IsReady = true`", but the method has no `GuardIsReady()` call, unlike all read methods (`GetCurrentLevelId`, `GetCoinBalance`, `GetCompletionRecord`, `GetUndoStack`). `PushUndoMove` has the same omission. In practice, callers subscribe to `OnSaveReady` and call `SetCoinBalance` only in the handler, so the pre-condition is enforced by contract rather than code. No production bug has been observed.

### Resolution Plan

Add `GuardIsReady()` calls to `SetCoinBalance` and `PushUndoMove` in a cleanup story or during the CoinEconomy integration story (S4-08). Also add a test: create a raw `SaveSystem` without calling `Awake()` (not possible via `AddComponent` — requires reflection), confirm either an exception or a log error is emitted. Low priority while all callers observe subscribe-then-check.
