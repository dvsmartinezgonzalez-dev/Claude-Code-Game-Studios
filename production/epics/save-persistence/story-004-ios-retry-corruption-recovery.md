# Story 004: Cold-Start Read Cases R-4 and iOS Protection Retry

> **Epic**: Save & Persistence
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 1.0 day
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-22

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirements**: `TR-SP-007`

| TR-ID | Requirement |
|-------|-------------|
| TR-SP-007 | iOS file protection cold-start: catch UnauthorizedAccessException separately; 250ms retry loop; 5-second timeout; thread joined before IsReady = true |

**ADR Governing Implementation**: ADR-0003: Save System Design
**ADR Decision Summary**: iOS cold-start after reboot (before first unlock) raises `UnauthorizedAccessException` — not `IOException` — because they are sibling .NET types under `SystemException`. The system must retry at 250ms intervals up to 5 seconds. During the retry window, no default file is written. If the timeout elapses, fall back to defaults and emit `first_unlock_read_failure` to analytics. R-4 (JSON corruption) attempts `save.tmp` recovery before falling back to defaults.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: iOS default data protection class in Unity 6.3 is `NSFileProtectionCompleteUntilFirstUserAuthentication` — files are inaccessible only between reboot and first unlock (not on every screen lock). `catch(IOException)` will NOT catch `UnauthorizedAccessException` — they are siblings, not parent/child. Retry uses a **background thread** spawned from `Awake()` with `Thread.Sleep(250)` off the main thread. `Awake()` calls `thread.Join()` before setting `IsReady = true`. `Awaitable.WaitForSecondsAsync` is NOT usable here — it requires `async void Awake()` which is FORBIDDEN (ADR-0003).

**Control Manifest Rules (Foundation layer)**:
- Required: separate `catch(IOException)` and `catch(UnauthorizedAccessException)` blocks; retry loop uses background thread with `Thread.Sleep(250)` (NOT on main thread); `Awake()` calls `thread.Join()` before `IsReady = true`
- Forbidden: sharing `catch(IOException)` with `UnauthorizedAccessException`; `Thread.Sleep` on the main thread; `async void Awake()`; writing a default file during the iOS retry window
- Guardrail: at most `floor(5000 / 250) = 20` retry attempts; background thread joined before `IsReady = true` (ADR-0003 thread-join requirement)

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-11** — No `save.tmp` exists at session end in any path (success, failure, crash recovery)
- [ ] **AC-16** — `save.json` fails JSON parse (Case R-4): error logged (file size, first 256 bytes, exception); `save.tmp` tried if present; recovered or default state written via write-then-swap; no error UI; `IsReady = true`
- [ ] **AC-28** *(Simulated)* — `UnauthorizedAccessException` on read: retry at 250ms intervals; accessible within 5s → file loads; timeout → defaults + `first_unlock_read_failure` analytics + no file written during window
- [ ] **AC-44** — Both `save.json` and `save.tmp` fail JSON parsing: both errors logged, defaults loaded, default state written to `save.json`, `IsReady = true`

---

## Implementation Notes

*Derived from ADR-0003 Implementation Guidelines:*

**R-4 corruption recovery sequence** (GDD C.5):
1. `save.json` exists but `JsonConvert.DeserializeObject` throws or returns null
2. Log to analytics: file size, first 256 bytes (as hex or base64), exception message
3. Check if `save.tmp` exists AND parses successfully → use as recovered state
4. If `save.tmp` also fails or absent → fall back to all defaults (same as R-3)
5. Write recovered or default state to `save.json` via synchronous write-then-swap (blocking, within `Awake()`)
6. Set `IsReady = true`

**iOS retry sequence** (GDD Edge Cases — iOS cold-start):
```csharp
private SaveData ReadWithIosRetry(string savePath) {
    const int retryIntervalMs = 250;
    const int timeoutMs = 5000;
    int elapsed = 0;

    while (elapsed < timeoutMs) {
        try {
            string json = _fileSystem.ReadAllText(savePath);
            return JsonUtility.FromJson<SaveData>(json);  // JsonUtility, NOT JsonConvert (ADR-0003)
        } catch (UnauthorizedAccessException) {
            // iOS pre-unlock — retry on background thread (do NOT catch IOException here)
            Thread.Sleep(retryIntervalMs);   // OK — this method runs on background thread (not main thread)
            elapsed += retryIntervalMs;
        } catch (IOException ex) {
            // Separate handler — not the iOS retry case
            LogAnalytics("cold_start_io_error", ex);
            return null;  // triggers R-4 path
        }
    }

    LogAnalytics("first_unlock_read_failure");
    return null;  // triggers R-3 (defaults) path; no file written during retry window
}
```

**`save.tmp` cleanup rule** (GDD C.5): If `save.json` is valid AND `save.tmp` exists → delete `save.tmp` silently. This is handled in Story 001's boot dispatch. This story handles the case where `save.json` is absent or corrupt AND `save.tmp` exists as the recovery source.

**`IOException` vs `UnauthorizedAccessException` hierarchy note**: Both inherit from `SystemException`. `catch(IOException ex)` does NOT catch `UnauthorizedAccessException`. Always write two separate catch blocks. Order matters: more-specific exceptions first if needed, but for these siblings, order is stylistic.

---

## Out of Scope

- Story 001: R-1 (valid file), R-3 (fresh install), R-5 (downgrade), `IsReady` contract
- Story 005: R-2 (migration), `migrate_v0_to_v1`, migration write-back

---

## QA Test Cases

*Embedded from `production/qa/qa-plan-sprint3-2026-05-22.md`.*

- **AC-28 pass / Read_UnauthorizedAccessException_RetriesUntilAccessible**
  - Given: `FakeFileSystem` throws `UnauthorizedAccessException` on first 2 reads, succeeds on 3rd
  - When: cold start executes
  - Then: file loads successfully on 3rd attempt; all fields correct

- **AC-28 fail / Read_UnauthorizedAccessException_Timeout_FallsBackToDefaults**
  - Given: `FakeFileSystem` throws `UnauthorizedAccessException` on every read for > 5 seconds
  - When: cold start executes
  - Then: falls back to defaults; `first_unlock_read_failure` analytics emitted; no file written during retry window

- **Read_IOException_CaughtSeparatelyFromUnauthorized**
  - Given: `FakeFileSystem` throws `IOException` on first read
  - When: cold start executes
  - Then: caught by `catch(IOException)` block (NOT retry loop); separate handler from `UnauthorizedAccessException`

- **AC-16 / Read_R4_JsonParseFailure_TmpRecovery**
  - Given: `save.json` with corrupted JSON; `save.tmp` with valid JSON
  - When: cold start executes
  - Then: `save.tmp` state used, written back to `save.json`, `IsReady = true`, no error UI

- **AC-44 / Read_R4_BothFilesCorrupt_DefaultsLoaded**
  - Given: both `save.json` and `save.tmp` fail JSON parsing
  - When: cold start executes
  - Then: both errors logged; defaults loaded; default state written to `save.json`; `IsReady = true`

- **Read_RetryInterval_AtMost20Attempts**
  - Given: `FakeFileSystem` throws `UnauthorizedAccessException` indefinitely
  - When: retry loop runs for 5000ms timeout
  - Then: at most 20 retry attempts made (floor(5000/250))

- **Read_RetryLoop_DoesNotBlockMainThread** *(advisory — verify by profiler)*
  - Given: retry loop active
  - When: retry wait executes
  - Then: no `Thread.Sleep` in build output (static analysis); yields between retries

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/save-persistence/SaveSystem_ReadCases_Test.cs` — must exist and all tests pass

**Advisory evidence** (physical iOS device post-reboot, carry to Alpha gate):
`production/qa/evidence/ac-28-ios-cold-start.md`

**Status**: [x] Created — `My project/Assets/_Project/Tests/unit/save-persistence/SaveSystem_ReadCases_Test.cs` (6 test methods)

---

## Dependencies

- Depends on: Story 001 must be DONE (boot dispatch routing and IFileSystem seam must exist)
- Unlocks: None directly — parallel with Stories 002, 005, 006

---

## Completion Notes
**Completed**: 2026-05-23
**Criteria**: 4/4 passing
**Deviations**:
- ADVISORY: `RetryIntervalMs`/`RetryTimeoutMs` are instance fields — cannot be set before `AddComponent` fires `Awake`. Two tests run ~5 s each (20 × 250ms). Static pre-boot override needed for CI speed. Logged as TD-SP-007.
- ADVISORY: `Read_UnauthorizedAccessException_RetriesUntilAccessible` test creates/destroys an intermediate SaveSystem instance — functional but messy. Cleanup candidate.
- ADVISORY: `EmitAnalyticsEvent` thread-safety contract undocumented on the field itself.
- Code review fixes: `catch(Exception)` split into `catch(IOException)` + `catch(UnauthorizedAccessException)` in `WriteSaveJsonSync` and `AttemptTmpRecovery`; `AttemptTmpRecovery` doc comment corrected.
**Test Evidence**: Logic — `My project/Assets/_Project/Tests/unit/save-persistence/SaveSystem_ReadCases_Test.cs` (6 test methods, EditMode)
**Code Review**: Complete — CHANGES REQUIRED; 2 issues resolved before story close
