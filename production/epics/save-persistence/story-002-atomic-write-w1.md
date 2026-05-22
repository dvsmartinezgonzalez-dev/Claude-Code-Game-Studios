# Story 002: WriteCompletionAtomic — W-1 Background Write

> **Epic**: Save & Persistence
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 1.5 days
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-22

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirements**: `TR-SP-001`, `TR-SP-003`, `TR-SP-008` (W-1 path)

| TR-ID | Requirement |
|-------|-------------|
| TR-SP-001 | JSON atomic write via FileStream + Flush(flushToDisk:true) + File.Replace (two-arg) / File.Move (first write) |
| TR-SP-003 | WriteCompletionAtomic(levelId, bestStars, version, newCurrentLevelId): snapshot on main thread + background write via Awaitable |
| TR-SP-008 | W-1 write off main thread via Awaitable.BackgroundThreadAsync; no async void |

**ADR Governing Implementation**: ADR-0003: Save System Design
**ADR Decision Summary**: W-1 (level completion write) executes on a background thread via `Awaitable.BackgroundThreadAsync()`. A `SemaphoreSlim(1,1)` named `_writeLock` serializes concurrent writes. The mandatory sequence: (1) capture snapshot on main thread, (2) switch to background thread, (3) acquire lock, (4) re-assert background thread, (5) FileStream + Flush + File.Replace/Move, (6) release lock in `finally`. No Unity API calls inside the locked section.

**Engine**: Unity 6.3 LTS | **Risk**: LOW (System.IO APIs unchanged; Awaitable is post-cutoff)
**Engine Notes**: `Awaitable.BackgroundThreadAsync()` is Unity 6.0+ first-party API — not present in 2022.x. `destroyCancellationToken` passed to `_writeLock.WaitAsync()` prevents leaked operations on MonoBehaviour destruction. `File.Move(source, dest, overwrite: true)` (3-arg) does NOT exist in .NET Standard 2.1 — compile error. Use `File.Replace(tmp, save, null)` when file exists; 2-arg `File.Move(tmp, save)` for first write.

**Control Manifest Rules (Foundation layer)**:
- Required: snapshot captured on main thread before `BackgroundThreadAsync`; `try/finally { _writeLock.Release(); }` wraps all I/O; no Unity API calls inside locked section
- Forbidden: `File.Move(src, dst, overwrite: true)` 3-arg overload; `async void` on any write method
- Guardrail: W-1 must not block main thread; background thread assertion at `FileStream` construction point

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-03** — Single `File.Replace`/`File.Move` per `WriteCompletionAtomic` call; fault-injected failure after `save.tmp` write confirms exactly one rename attempt
- [ ] **AC-04** — `IOException` on `File.Move`/`File.Replace` via `IFileSystem` injection: `save.json` intact, `save.tmp` deleted, dirty flag remains `true`, error logged to analytics
- [ ] **AC-05** — Concurrent W-1 for levels N then N+1 (simulated via `IFileSystem` write delay): both completion records in `save.json`, `current_level_id = N+2`, valid JSON after all writes resolve
- [ ] **AC-11** — No `save.tmp` exists at session end in any write path (success, failure, or crash recovery)
- [ ] **AC-12** — Disk full (`IOException` on `Flush`): non-blocking "Could not save progress — please free up storage space" message shown, error logged, game remains playable
- [ ] **AC-20** — `WriteCompletionAtomic` with `best_stars = 0`: no I/O, no mutation, caller bug warning logged, `save.json` and dirty flag unchanged
- [ ] **AC-21** — `completion_record[N].best_stars = 3` exists: calling `WriteCompletionAtomic(N, 2, ...)` leaves `best_stars = 3` — lower value not written
- [ ] **AC-22** — `completion_record[N].completion_version = "2026.04"` exists: calling `WriteCompletionAtomic(N, 3, "2026.09", ...)` leaves `completion_version = "2026.04"` in both memory and file — write-once contract
- [ ] **AC-23** — `completion_record[]` has one entry for `level_id = 5`: calling `WriteCompletionAtomic(5, 3, ...)` → still exactly one entry for `level_id = 5`
- [ ] **AC-30** — `_writeLock` serializes concurrent writes: `save.json` valid after all writes resolve. *Story 002 scope: verify W-1 + W-1 concurrency (two `WriteCompletionAtomic` calls under `FakeFileSystem.WriteDelay`). Full W-1 + W-2 concurrent test deferred to Story 003 (W-2 `OnApplicationPause` not yet implemented).*
- [ ] **AC-35** — `coin_balance` near `INT_MAX`: add mutation clamps to `INT_MAX`; dirty flag set `true`; clamped value written on next trigger
- [ ] **AC-36** — `WriteCompletionAtomic(0, ...)` or `WriteCompletionAtomic(10000, ...)`: no I/O, no mutation, caller bug warning logged
- [ ] **AC-37** — `IOException` on `File.Replace`/`File.Move`: `save.json` intact, `save.tmp` deleted, error logged, dirty flag `true` for retry
- [ ] **AC-39** — 5 undo entries in GSM snapshot at time of W-1: `save.json` contains `level_progress.undo_stack` with exactly those 5 entries in correct order
- [ ] **AC-41** — `undo_stack[]` has 20 entries: 21st move committed → still 20 entries; entry at index 0 is move #2 (oldest discarded)
- [ ] **AC-43** — After second `Awaitable.BackgroundThreadAsync()`: `Thread.IsBackground == true` AND thread is not Unity main thread at `FileStream` construction
- [ ] **AC-46** — `UnauthorizedAccessException` on `File.Replace`: caught by explicit separate `catch(UnauthorizedAccessException)` block (NOT by `catch(IOException)`); same recovery as AC-37

---

## Implementation Notes

*Derived from ADR-0003 Implementation Guidelines:*

**Mandatory W-1 sequence** (GDD C.1, ADR-0003):

> **Serializer**: use `JsonUtility.ToJson` — NOT `JsonConvert.SerializeObject` (Newtonsoft).
> SaveData classes are `[Serializable]` with public fields designed for `JsonUtility`.
> Newtonsoft is scoped to LevelData (ADR-0004) only.
>
> **Write method**: use `_fileSystem.WriteAndFlush(tmpPath, bytes)` — NOT `_fileSystem.OpenWrite`
> (no such method) and NOT `File.WriteAllBytes` (no fsync guarantee).
> `IFileSystem.WriteAndFlush` wraps `FileStream + Flush(flushToDisk:true)` in production.
>
> **Path fields**: use `_savePath` and `_tmpPath` — already cached in `Awake()` by Story 001.
> Do NOT re-access `Application.persistentDataPath` from the background thread.
>
> **`_writeLock`**: add `private SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);` to `SaveSystem`.

```csharp
#pragma warning disable CS1998 // remove this pragma when async body is filled in Story 002
public async Awaitable WriteCompletionAtomic(int levelId, int bestStars,
    string version, int newCurrentLevelId)
{
    // Early-exit guards (run synchronously on main thread before any await)
    if (bestStars == 0) { Debug.LogWarning("[SaveSystem] WriteCompletionAtomic: best_stars=0 is a no-op."); return; }
    if (levelId < 1 || levelId > 9999) { Debug.LogWarning($"[SaveSystem] WriteCompletionAtomic: invalid levelId={levelId}."); return; }

    // Step 1: capture immutable snapshot on main thread BEFORE background switch.
    // _savePath and _tmpPath are pre-cached; never re-read Application.persistentDataPath here.
    var snapshot = CaptureSnapshot(levelId, bestStars, version, newCurrentLevelId);

    // Step 2: switch to background thread.
    await Awaitable.BackgroundThreadAsync();

    // Step 3: acquire write lock — destroyCancellationToken cancels if MonoBehaviour destroyed mid-wait.
    await _writeLock.WaitAsync(destroyCancellationToken);

    // Step 4: defensive re-assertion — guarantees I/O executes on a thread-pool thread
    // even if WaitAsync scheduled its continuation on the main thread. DO NOT remove.
    await Awaitable.BackgroundThreadAsync();

    // Steps 5–6: file I/O — NO Unity API calls inside this block (Application.*, Debug.*, etc.).
    try
    {
        string json = JsonUtility.ToJson(snapshot);           // JsonUtility, NOT JsonConvert
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        _fileSystem.WriteAndFlush(_tmpPath, bytes);           // FileStream + Flush(flushToDisk:true)
        if (_fileSystem.FileExists(_savePath))
            _fileSystem.Replace(_tmpPath, _savePath, null);  // atomic swap (NOT File.Move 3-arg)
        else
            _fileSystem.Move(_tmpPath, _savePath);            // first-ever write (2-arg overload)
    }
    catch (IOException ex)
    {
        SafeDeleteTmp(tmpExists: _fileSystem.FileExists(_tmpPath));
        Debug.LogError($"[SaveSystem] W-1 IOException: {ex.Message}");  // analytics stub
        _isDirty = true;
    }
    catch (UnauthorizedAccessException ex)                    // sibling of IOException — separate catch required
    {
        SafeDeleteTmp(tmpExists: _fileSystem.FileExists(_tmpPath));
        Debug.LogError($"[SaveSystem] W-1 UnauthorizedAccessException: {ex.Message}");
        _isDirty = true;
    }
    finally
    {
        _writeLock.Release();                                 // always release — even on exception
    }

    // Step 7: clear dirty flag on main thread after successful write.
    await Awaitable.MainThreadAsync();
    _isDirty = false;
}
#pragma warning restore CS1998
```

**`CaptureSnapshot` helper** — call BEFORE any `await` (must run on main thread):
```csharp
private SaveData CaptureSnapshot(int levelId, int bestStars, string version, int newCurrentLevelId)
{
    // Deep-copy current in-memory save state into a new SaveData instance.
    // Any GSM mutations that occur after this line will NOT be reflected in this write.
    // Implement via JsonUtility.FromJson(JsonUtility.ToJson(_saveData)) for a clean deep copy,
    // or construct field-by-field. The snapshot must be a value copy — not a reference.
}
```

**`WriteCompletionAtomic` edge case guards** (execute before entering async sequence):
- `best_stars == 0` → log warning, return (no I/O)
- `levelId < 1 || levelId > 9999` → log warning, return
- Existing `best_stars` higher → update in-memory only if new value is higher
- `completion_version` is write-once — never overwrite if already set

**Undo stack cap** (20 entries max): implemented in the method that updates `_undoStack` in memory. When 21st entry added, remove index 0 before appending.

**Snapshot field**: `completion_version` must not be written if already set in memory for that `level_id`. Check in-memory record before including in snapshot.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: W-2 `OnApplicationPause` synchronous write; dirty-flag gate on W-2
- Story 004: R-4 corruption recovery; iOS retry
- Story 005: Migration logic

---

## QA Test Cases

*Embedded from `production/qa/qa-plan-sprint3-2026-05-22.md`.*

- **AC-03 / Write_W1_SingleFileMovePerCall**
  - Given: `FakeFileSystem` tracking all Move/Replace calls
  - When: `WriteCompletionAtomic(N, 3, "2026.05", N+1)` executes
  - Then: exactly one `File.Replace` or `File.Move` — never zero, never two
  - Edge cases: first write uses `File.Move`; subsequent uses `File.Replace`

- **AC-04 / Write_W1_IOException_SaveJsonIntact_TmpDeleted**
  - Given: `FakeFileSystem.FaultOnNextWrite = IOException` on File.Replace
  - When: `WriteCompletionAtomic` executes
  - Then: `save.json` unchanged, `save.tmp` deleted, dirty flag `true`, error logged

- **AC-05 / Write_W1_ConcurrentSecondW1_BothRecordsPresent**
  - Given: `FakeFileSystem.WriteDelay = 100ms`
  - When: W-1 for level N, then W-1 for N+1 before first completes
  - Then: both records in `save.json`, `current_level_id = N+2`, valid JSON

- **AC-30 / Write_W1_W2_Concurrent_NoDeadlockAndValidJson**
  - Given: `FakeFileSystem.WriteDelay = 50ms`; W-1 and W-2 fire same frame
  - Then: `_writeLock` prevents concurrent rename; `save.json` valid; W-2 dirty-check fires after W-1 releases lock

- **AC-43 / Write_W1_BackgroundThread_AssertedAtFileStream**
  - Given: `FakeFileSystem` captures executing thread at FileStream construction
  - When: W-1 executes (after second `BackgroundThreadAsync`)
  - Then: `Thread.IsBackground == true` AND thread is not Unity main thread

- **AC-39 / Write_W1_UndoStack_ExactSnapshotWritten**
  - Given: GSM has 5 undo entries at snapshot time
  - When: W-1 executes
  - Then: `save.json` contains `undo_stack` with exactly those 5 entries in order

- **AC-41 / Write_UndoStack_Cap20_OldestDiscarded**
  - Given: 21 moves committed
  - When: save write executes
  - Then: `undo_stack[]` has exactly 20 entries; oldest (move #1) discarded

- **AC-46 / Write_W1_UnauthorizedAccessException_CaughtSeparately**
  - Given: `FakeFileSystem` throws `UnauthorizedAccessException` on File.Replace
  - Then: caught by explicit `catch(UnauthorizedAccessException)` — NOT by `catch(IOException)`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/save-persistence/SaveSystem_AtomicWrite_Test.cs` — must exist and all tests pass

**Status**: [x] Created — `My project/Assets/_Project/Tests/unit/save-persistence/SaveSystem_AtomicWrite_Test.cs` (20 test methods)

---

## Dependencies

- Depends on: Story 001 must be DONE (IFileSystem seam, schema classes, and `_cachedPersistentDataPath` must exist)
- Unlocks: Story 003 (W-2 path shares `_writeLock` and IFileSystem); Story 008 (SP↔GSM integration)

---

## Completion Notes
**Completed**: 2026-05-22
**Criteria**: 14/17 passing (3 advisory deferred — see below)
**Deviations**:
- ADVISORY: `_isDirty` and `_lastWriteError` lacked `volatile`; found during code review; fixed before story close. Logged as TD-SP-001.
- ADVISORY: AC-35 (`SetCoinBalance` INT_MAX clamp) was missing from original implementation; `Math.Clamp(balance, 0, int.MaxValue)` added during code review. No unit test written. Logged as TD-SP-002.
- ADVISORY: AC-05 (concurrent W-1+W-1), AC-30 (`_writeLock` W-1+W-2), AC-43 (full background-thread identity) deferred to PlayMode — EditMode proxies written. Logged as TD-SP-003.
- ADVISORY: `PerformColdStartRead` exceeds 40-line coding standard (~68 lines). Logged as TD-SP-004.
**Test Evidence**: Logic — `My project/Assets/_Project/Tests/unit/save-persistence/SaveSystem_AtomicWrite_Test.cs` (20 test methods, all EditMode)
**Code Review**: Complete — CHANGES REQUIRED verdict; 3 issues resolved (`volatile _isDirty`, `volatile _lastWriteError`, AC-35 clamp)
