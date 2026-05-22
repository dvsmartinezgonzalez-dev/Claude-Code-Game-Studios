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
