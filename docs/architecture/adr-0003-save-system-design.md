# ADR-0003: Save System Design

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Persistence |
| **Knowledge Risk** | LOW — `System.IO.File`, `System.Threading.Thread`, `Application.persistentDataPath`, `JsonUtility`, `SemaphoreSlim`, `PlayerPrefs` are all stable APIs unchanged in Unity 6.x |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md` |
| **Post-Cutoff APIs Used** | `Awaitable.BackgroundThreadAsync()` / `Awaitable.MainThreadAsync()` (Unity 6.0+, first-party thread-switch awaitables); `destroyCancellationToken` (Unity 6.0+ MonoBehaviour property) |
| **Verification Required** | (1) Confirm `File.Replace` atomicity on target Android test device (Galaxy A14, Android 11+) — check for FUSE overlay. (2) Confirm cold-start iOS retry (`UnauthorizedAccessException` path) on physical device after hard reboot with passcode set. (3) Confirm `JsonUtility` round-trips the full C.2 schema without data loss. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (SaveSystem is a DDOL singleton at SEO −90; subscribe-then-check pattern for `OnSaveReady` established there) |
| **Enables** | ADR-0006 (GSM design depends on `SaveSystem.IsReady` being true before GSM.LoadLevel is called) |
| **Blocks** | All implementation sprints — no system may read saved values until this ADR is Accepted |
| **Ordering Note** | ADR-0001 must be Accepted first (SaveSystem's boot slot and `IsReady` contract are defined there). |

## Context

### Problem Statement
BoltSort requires persistent save data (level progress, completion records, coin balance, undo stack) that survives app restarts without data loss. The save must be atomic (no partial-write corruption), thread-safe (write path runs off main thread to avoid frame drops), and handle iOS file protection on cold start (inaccessible file before first device unlock after reboot).

### Constraints
- `File.Move(source, dest, overwrite: true)` (three-argument overload) **does not exist in .NET Standard 2.1** (Unity 6.3's BCL target) — it was added in .NET 5. Using it causes a compile error.
- `File.WriteAllText` does not guarantee `fsync` / `F_FULLFSYNC` before closing the file handle — `save.tmp` may be unflushed on disk before the rename, causing corruption after power loss.
- `async void Awake()` is forbidden — Unity does not await `Awake()`, so any `await` in `Awake()` allows `Start()` on other MonoBehaviours to fire before `IsReady = true`, breaking the initialization contract.
- iOS cold-start retry must catch `UnauthorizedAccessException` (not `IOException`) — the two are sibling .NET types; one `catch(IOException)` does not catch the other.
- No third-party persistence or serialization packages in the allowed-library list.
- All Unity API calls (`Application.*`, `Debug.*`, MonoBehaviour fields) are main-thread-only — the write path's locked section must contain only `System.IO` calls.

### Requirements
- Atomic write: no partial write may be visible between serialization and commit
- Background write for W-1 (level completion): must not stall the main thread
- Synchronous read and synchronous W-2 (app pause write): must complete before `Start()` / OS suspension deadline
- Schema versioning: forward-compatible migrations that run at cold start
- Backup exclusion: `save.json` excluded from iCloud and Android Auto-Backup
- `IsReady` + `OnSaveReady` pattern per ADR-0001 / ADR-0002

## Decision

### Storage Architecture

Split storage model:
- **`Application.persistentDataPath/save.json`**: structured data (level progress, economy, skins). Authoritative for all cross-session progress. Accessed only through the `ISaveSystem` interface.
- **`PlayerPrefs`**: scalar settings only (audio volumes, QTS tier override). Written directly by owning systems; Save & Persistence does not mediate PlayerPrefs writes.
- **`PlayerPrefs.Save()`** must be called explicitly after every `PlayerPrefs.Set*()` call — `OnApplicationQuit` is not guaranteed to fire on Android process kills.

### JSON Schema (`save.json`)

```json
{
  "schema_version": 1,
  "level_progress": {
    "current_level_id": 1,
    "completion_record": [
      { "level_id": 1, "best_stars": 3, "completion_version": "2026.04" }
    ],
    "undo_stack": [
      { "f": 2, "t": 0 }
    ]
  },
  "economy": {
    "coin_balance": 0
  },
  "skins": {
    "_status": "reserved"
  }
}
```

Serialization: **`JsonUtility`** (Unity built-in, zero dependency). All serialized classes must be `[Serializable]`. Note: `JsonUtility` serializes null strings as `""` — absent `completion_version` in migrated records will be `""`, not `null`. Migration and read code must treat `""` as the absent-sentinel, not `null`.

### Cold-Start Read (Synchronous, in Awake())

The entire cold-start read — including migration write-back and corruption recovery — executes **synchronously and blocking within `SaveSystem.Awake()`**. `async void Awake()` is forbidden. `IsReady = true` is set and `OnSaveReady?.Invoke()` fires at the end of `Awake()`, before any other system's `Start()` runs.

> **Architecture doc correction**: `architecture.md` Flow 2 described a background Thread + MainThreadDispatcher for the cold-start read. The GDD (`save-persistence.md`, Approved 2026-04-23) overrides this with a synchronous read. The synchronous approach is correct: the save file is <22KB, reads in <2ms, and a synchronous read is required so that `IsReady = true` is set before any lower-SEO system's `Awake()` runs.

**Why subscribe-then-check is required**: `OnSaveReady` fires synchronously at the end of `SaveSystem.Awake()` [SEO −90]. Systems at lower priority (e.g., `CoinEconomy` at SEO −40) have not yet run their `Awake()` and have not yet subscribed. They will subscribe in their own `Awake()` and must use the subscribe-then-check pattern (ADR-0001) to catch up:

```csharp
// In CoinEconomy.Awake() [SEO -40]
SaveSystem.Instance.OnSaveReady += HandleSaveReady;
if (SaveSystem.Instance.IsReady) HandleSaveReady();  // fires immediately — event already fired
```

**Cold-start cases:**

| Case | Condition | Action |
|------|-----------|--------|
| R-1 | File valid, `schema_version == current` | Load into memory |
| R-2 | File valid, `schema_version < current` | Run sequential migrators; write-back synchronously |
| R-3 | File does not exist (fresh install) | Initialize defaults; defer first write to W-1/W-2 |
| R-4 | File exists, JSON parse fails (corrupted) | Try `save.tmp`; fall back to defaults; write-back synchronously |
| R-5 | `schema_version > MAX_KNOWN_VERSION` (downgrade) | Reject; start from defaults; do NOT overwrite; show one-time notice |

**iOS cold-start file protection**: On first boot after device restart, `Application.persistentDataPath/save.json` is protected by `NSFileProtectionCompleteUntilFirstUserAuthentication`. File access throws `UnauthorizedAccessException` (not `IOException`) until the user unlocks the device for the first time. Retry strategy (background thread spawned from Awake — the blocking sleep stays off the main thread):

> **Thread join requirement**: `Awake()` MUST call `thread.Join()` (or equivalent blocking wait) before setting `IsReady = true`. Emitting `OnSaveReady` before the retry thread completes would deliver stale defaults to all consumers — the race condition the subscribe-then-check pattern is designed to prevent.

```csharp
// Synchronous retry — called from background thread spawned in Awake()
var deadline = DateTime.UtcNow.AddSeconds(5.0);
while (true)
{
    try
    {
        return File.ReadAllText(_savePath);
    }
    catch (UnauthorizedAccessException) when (DateTime.UtcNow < deadline)
    {
        Thread.Sleep(250);  // blocking sleep — on background thread, acceptable
    }
    catch (UnauthorizedAccessException)
    {
        return null;  // timeout — treat as R-3 (fresh install defaults)
    }
    catch (FileNotFoundException)
    {
        return null;  // R-3
    }
    catch (IOException)
    {
        return null;  // R-4 — corrupted or unreadable
    }
}
```

**Singleton guard** (required first statement in Awake()):
```csharp
if (_instance != null && _instance != this) { Destroy(gameObject); return; }
```

### Write Contract

**W-1 — Level completion write** (background, async):

```csharp
// Called from LevelProgression (main thread)
public async Awaitable WriteCompletionAtomic(int levelId, int bestStars,
    string version, int newCurrentLevelId)
{
    // 1. Capture immutable snapshot on main thread BEFORE background switch
    var snapshot = CaptureSnapshot(levelId, bestStars, version, newCurrentLevelId);

    await Awaitable.BackgroundThreadAsync();
    await _writeLock.WaitAsync(destroyCancellationToken);
    await Awaitable.BackgroundThreadAsync();  // defensive re-assertion (see GDD C.1)

    try
    {
        WriteAtomicToFile(snapshot);
    }
    finally
    {
        _writeLock.Release();
    }

    await Awaitable.MainThreadAsync();
    _isDirty = false;
}
```

**W-2 — App pause write** (synchronous, main thread):

No `await` in `OnApplicationPause`. Uses synchronous semaphore wait. Must complete before iOS 5-second suspension deadline:

```csharp
private void OnApplicationPause(bool paused)
{
    if (!paused || !_isDirty) return;
    try
    {
        _writeLock.Wait(destroyCancellationToken);
    }
    catch (OperationCanceledException)
    {
        // DDOL MonoBehaviour destroyed mid-pause (e.g., unit test teardown).
        // Preserve _isDirty so the next session can detect unsaved state.
        return;
    }
    try
    {
        if (_isDirty)  // re-check after lock (W-1 may have cleared it)
            WriteAtomicToFile(CaptureSnapshot());
    }
    finally
    {
        _writeLock.Release();
    }
}
```

**W-3 — Settings change**: handled directly by owning systems via `PlayerPrefs.Set*()` + `PlayerPrefs.Save()`. Save & Persistence does not mediate.

### Write-Then-Swap Procedure

For both W-1 and W-2:

```csharp
private void WriteAtomicToFile(SaveSnapshot snapshot)
{
    var json = JsonUtility.ToJson(snapshot.saveData);
    var tmpPath = _savePath + ".tmp";

    // Step 1: Write to .tmp with fsync
    using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write))
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        fs.Write(bytes, 0, bytes.Length);
        fs.Flush(flushToDisk: true);  // required — File.WriteAllText does NOT flush to storage
    }

    // Step 2: Atomic rename
    if (File.Exists(_savePath))
        File.Replace(tmpPath, _savePath, destinationBackupFileName: null);
    else
        File.Move(tmpPath, _savePath);  // first-ever write — no existing file to replace
    // NOTE: File.Move(source, dest, overwrite:true) does NOT exist in .NET Standard 2.1
    //       Three-argument overload was added in .NET 5 — compile error in Unity 6.3
}
```

**Write failure handling**: All catch blocks must catch BOTH `IOException` AND `UnauthorizedAccessException` (they are sibling .NET types — `catch(IOException)` does not catch `UnauthorizedAccessException`). On failure: leave `save.json` intact, delete `save.tmp`, log to analytics, retry on next W-1/W-2 trigger.

### Custom MainThreadDispatcher

A custom `MainThreadDispatcher` DDOL MonoBehaviour (ConcurrentQueue<Action> + Update()) is included in the project for any future raw-Thread code paths that need to dispatch callbacks to the main thread. For the SaveSystem's W-1 path specifically, `Awaitable.MainThreadAsync()` provides built-in thread-switching and the custom dispatcher is not required. The dispatcher is available for other systems.

### Save Migration

Schema version is stored as an integer `schema_version` field. On R-2 (version < current): apply sequential migrators (`migrate_v0_to_v1`, `migrate_v1_to_v2`, etc.) until current version. Migrators must be idempotent. `completion_version` is write-once and must never be modified by a migrator.

**v0 migration**: Pre-versioning files (`schema_version` key absent) have flat structure: `{"current_level_id": int, "completion_record": [...], "coin_balance": int}`. `migrate_v0_to_v1` restructures into nested v1 schema, initializes `undo_stack: []`, leaves absent `completion_version` fields empty (not backfilled).

### Backup Exclusion

- **iOS**: Apply `NSURLIsExcludedFromBackupKey = true` to `save.json` immediately after first file creation (requires native plugin call — Unity Managed code cannot set this directly). This prevents iCloud backup of save data.
- **Android**: `<cloud-backup-rules>` XML in the Android manifest excludes `save.json` and `save.tmp`.

### Architecture Diagram

```
SaveSystem.Awake() [SEO -90]
    │
    ├── Pre-cache Application.persistentDataPath
    ├── Check save.tmp existence
    ├── Read save.json (synchronous)
    │   └── iOS retry: UnauthorizedAccessException → Thread.Sleep(250) → retry (max 5s)
    ├── Dispatch to R-1/R-2/R-3/R-4/R-5
    │   ├── R-2: migrate synchronously → write-back (synchronous W-2-style)
    │   └── R-4: recover → write-back (synchronous W-2-style)
    ├── IsReady = true
    └── OnSaveReady?.Invoke()  ← fires here; no subscribers yet (lower-SEO systems subscribe later)

CoinEconomy.Awake() [SEO -40]
    ├── Subscribe OnSaveReady
    └── IsReady == true → HandleSaveReady() immediately (subscribe-then-check)

LevelProgression.Awake() [SEO -30]
    ├── Subscribe OnSaveReady, GSM.OnLevelComplete
    └── IsReady == true → HandleSaveReady() → GSM.LoadLevel(current_level_id)

W-1 Write (async, off main thread):
    Level Completion
        → CaptureSnapshot() [main thread]
        → Awaitable.BackgroundThreadAsync()
        → _writeLock.WaitAsync()
        → Awaitable.BackgroundThreadAsync() [defensive]
        → FileStream + Flush(flushToDisk:true) + File.Replace
        → _writeLock.Release()
        → Awaitable.MainThreadAsync()
        → _isDirty = false

W-2 Write (synchronous, main thread):
    OnApplicationPause(true)
        → _writeLock.Wait()
        → if (_isDirty): FileStream + Flush + File.Replace
        → _writeLock.Release()
```

### Key Interfaces

```csharp
public interface ISaveSystem
{
    bool IsReady { get; }
    event Action OnSaveReady;

    int GetCurrentLevelId();
    CompletionRecord? GetCompletionRecord(int levelId);
    void WriteCompletionAtomic(int levelId, int bestStars, string version, int newCurrentLevelId);

    int GetCoinBalance();
    void SetCoinBalance(int balance);  // sets _isDirty = true; W-2 or next W-1 will persist it
}

[Serializable]
public struct CompletionRecord
{
    public int level_id;
    public int best_stars;
    public string completion_version;  // "" = absent (JsonUtility null → ""); treat "" as not-yet-written
}
```

## Alternatives Considered

### Alternative A: PlayerPrefs Only (no JSON file)
- **Description**: Store all save data in PlayerPrefs key-value pairs. No file I/O.
- **Pros**: Simplest implementation; no threading concerns; atomic per-key writes
- **Cons**: No atomic multi-field write (advancing `current_level_id` + `best_stars` + `completion_version` as one operation is impossible). No schema versioning. Storage limit ~1MB on iOS. Not suitable for arrays. Binary data not supported.
- **Rejection Reason**: Cannot satisfy TR-SP-003 (WriteCompletionAtomic) — multi-field atomicity is architecturally required.

### Alternative B: BinaryFormatter + file
- **Description**: Serialize a C# object graph to a binary file using `BinaryFormatter`.
- **Pros**: Compact; handles complex object graphs
- **Cons**: `BinaryFormatter` is deprecated and disabled by default in .NET 6+ (Microsoft security advisory); not human-readable (hard to debug); migration across schema versions is complex; generates GC during serialization.
- **Rejection Reason**: Deprecated pattern; not human-readable; migration tooling harder.

### Alternative C: Unity Cloud Save (Unity Gaming Services)
- **Description**: Save data stored in Unity's cloud; loaded on app start from network.
- **Pros**: Cross-device progression; no local file management
- **Cons**: Requires network on first launch (blocks boot if offline); adds UGS SDK dependency; inappropriate for MVP (adds complexity without player-facing benefit for a single-device casual game); cost scales with MAU.
- **Rejection Reason**: Out of scope for MVP; adds network dependency to a local game.

## Consequences

### Positive
- Atomic write prevents partial-write corruption — `save.json` is always either the old complete state or the new complete state
- Synchronous cold-start read ensures `IsReady = true` before any other system's `Awake()` completes — eliminates the save-read race condition for all lower-SEO systems
- `Awaitable.BackgroundThreadAsync()` for W-1 keeps level-completion write off the main thread — no frame drop on completion
- Synchronous W-2 satisfies iOS 5-second suspension deadline without `async void` risks

### Negative
- `FileStream.Flush(flushToDisk:true)` adds 2–8ms latency to W-1 on the background thread (acceptable — not on main thread)
- iOS cold-start retry: in the rare post-reboot-before-unlock scenario, `Awake()` may block the main thread for up to 5 seconds. This is acceptable; the alternative (async retry) violates the "no async Awake()" constraint.
- Android 11+ FUSE paths: `Flush(flushToDisk:true)` and `File.Replace` atomicity are best-effort — full fsync guarantee requires a native plugin. Accepted risk for MVP.
- Backup exclusion on iOS requires a native plugin call for `NSURLIsExcludedFromBackupKey` — pure managed C# cannot set this attribute. The native call must be added before any public release.

### Risks
- **Risk**: Developer writes `File.Move(tmp, save, overwrite: true)` (three-arg overload) → compile error in Unity 6.3 (.NET Standard 2.1). **Mitigation**: This ADR documents the correct two-path logic (`File.Replace` / `File.Move`); control manifest will list it as a REQUIRED rule; code review enforces it.
- **Risk**: Catch block only catches `IOException`, missing `UnauthorizedAccessException` → iOS cold-start retry silently fails; app starts with default data on post-reboot launch. **Mitigation**: This ADR mandates sibling-exception catches throughout; control manifest enforces it.
- **Risk**: `OnApplicationPause` uses `await` (`async void OnApplicationPause`) → Unity returns control to OS at first `await`; write never completes under iOS suspension. **Mitigation**: W-2 is synchronous; `async void` is banned for `OnApplicationPause` in the control manifest.
- **Risk**: `completion_version` migration backfill — a migrator accidentally sets `completion_version` on a pre-existing record → violates write-once contract; player's completion date becomes wrong. **Mitigation**: Control manifest FORBIDDEN rule: migrators must not set `completion_version` on any record where it was previously empty.
- **Risk**: iOS retry background thread not joined before `IsReady = true` → `OnSaveReady` fires before retry completes → consumers receive stale defaults (fresh-install state instead of real progress) on post-reboot cold start. **Mitigation**: Thread join (or completion signal) is mandatory before any `IsReady = true` assignment — documented in the cold-start section; control manifest enforces it.
- **Risk**: `SetCoinBalance()` called by `CoinEconomy` does not mark `_isDirty = true` → W-2 on app pause skips the write → coin balance change (e.g., hint purchase) lost on force-quit. **Mitigation**: `SetCoinBalance` implementation MUST set `_isDirty = true`; documented in ISaveSystem interface comment and control manifest.
- **Risk**: `save.tmp` left on disk after a crash → next cold-start R-4 picks up partial data. **Mitigation**: Cold-start step 2 checks `save.tmp` existence; if `save.json` is valid, delete `save.tmp` silently.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| save-persistence.md | TR-SP-001: JSON file; atomic via `File.Move` | Documents `FileStream + Flush(flushToDisk:true) + File.Replace/Move` write-then-swap procedure |
| save-persistence.md | TR-SP-002: Fields `current_level_id`, `completion_record[]`, `coin_balance` | Defines the `schema_version` 1 JSON schema (C.2) with all required fields |
| save-persistence.md | TR-SP-003: `WriteCompletionAtomic(...)` | Documents W-1 write path: snapshot on main thread + Awaitable background write |
| save-persistence.md | TR-SP-004: `IsReady` + `OnSaveReady`; subscribe-then-check | Establishes synchronous read in Awake(); documents why subscribe-then-check is mandatory |
| save-persistence.md | TR-SP-005: `PlayerPrefs` for audio prefs | Documents split storage model; confirms audio keys owned by AudioSystem |
| save-persistence.md | TR-SP-006: Save migration versioning | Defines integer `schema_version` field and sequential migrator pattern |
| save-persistence.md | TR-SP-007: iOS file protection; cold-start retry | Mandates `UnauthorizedAccessException` catch; 250ms retry; 5-second timeout |
| save-persistence.md | TR-SP-008: Background Thread for file I/O | W-1 uses `Awaitable.BackgroundThreadAsync()`; W-2 is synchronous (by necessity) |

## Performance Implications
- **CPU**: W-1 background write: ~2–8ms (dominated by `Flush(flushToDisk:true)`). W-2 synchronous write: same, on main thread — acceptable at screen lock (not during gameplay). Cold-start read: <2ms for <22KB save file.
- **Memory**: SaveSystem holds full in-memory save state: ~2–5KB. Snapshot copy per write: ~2–5KB peak (released after write). `_writeLock` (SemaphoreSlim): negligible.
- **Load Time**: Synchronous cold-start read: <2ms (typical), up to 5 seconds (iOS post-reboot cold start — exceptional).
- **Network**: N/A — all I/O is local file system.

## Migration Plan
No existing code to migrate — this ADR is written before implementation begins.

## Validation Criteria
1. Unit test: write-then-swap produces a valid `save.json` with `schema_version: 1` and all fields from snapshot
2. Unit test: `WriteCompletionAtomic` with matching level already persisted → idempotent (no duplicate record)
3. Unit test: R-4 (corrupt `save.json`) → starts from defaults, valid `save.json` written, no crash
4. Unit test: R-2 (v0 schema) → migrated to v1 correctly; `completion_version` absent fields remain empty
5. Integration test: `SaveSystem.Awake()` completes before `CoinEconomy.Awake()` (SEO ordering) — `IsReady == true` when CE subscribes
6. Device test: iOS cold-start after hard reboot + passcode lock → app starts without crash (retry path exercised); `IsReady` set within 5 seconds
7. Device test: Android (Galaxy A14, Android 11) — W-1 completes without I/O error; `save.json` valid after write

## Related Decisions
- ADR-0001: Singleton Architecture and Boot Sequence — SaveSystem at SEO −90; `IsReady`/`OnSaveReady` pattern defined there
- ADR-0002: Event and Signal Architecture — `OnSaveReady` is `event Action`; subscribe-then-check applies
- `design/gdd/save-persistence.md` — source of truth for schema, edge cases, and migration rules (Approved)
