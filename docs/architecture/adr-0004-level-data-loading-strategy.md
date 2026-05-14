# ADR-0004: Level Data Loading Strategy

## Status
Accepted

## Date
2026-05-02

## Revision
2026-05-12 — GDD evolved post-ADR to require Newtonsoft.Json (nullable types, private setters, attribute-based JSON mapping), single catalogue file (atomic reload, `catalogue_version` tracking), and `Task`-based async architecture. Original design (JsonUtility, multiple TextAssets, subscribe-then-check) is preserved in Alternatives Considered as Alternative D.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Asset Loading (Unity Addressables 2.x), JSON deserialization (Newtonsoft.Json) |
| **Knowledge Risk** | MEDIUM — Addressables 2.x API surface changed from 1.x post-LLM-cutoff; Newtonsoft.Json Unity package API is stable |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | `Addressables.LoadAssetAsync<TextAsset>(key)` — single-asset form (Addressables 2.x); `Addressables.Release(handle)` for handle release; `JsonConvert.DeserializeObject<LevelCatalogue>(json)` (Newtonsoft.Json) |
| **Verification Required** | (1) Confirm Newtonsoft.Json `[JsonProperty]` attributes + private setters round-trip correctly on IL2CPP iOS build (nullable `int?`, jagged `int[][]`, PascalCase C# ↔ snake_case JSON). (2) Confirm `LoadAssetAsync<TextAsset>` resolves the `"levels.json"` Addressables key on both platforms. (3) Confirm handle release frees `TextAsset` memory after deserialization (check Profiler). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (LDS is a DDOL singleton at SEO −95; `OnLevelDataReady` compatibility bridge maintained for LevelProgression dual-ready guard) |
| **Enables** | ADR-0006 (GSM depends on `LevelDataSystem.GetLevel(int)` being available at LoadLevel time) |
| **Blocks** | Level Progression implementation sprint — LevelProgression cannot call `GSM.LoadLevel()` until this ADR is Accepted |
| **Ordering Note** | ADR-0001 must be Accepted before this ADR can be implemented. |

## Context

### Problem Statement
BoltSort has potentially 200+ puzzle levels that must be accessible with near-zero load latency during gameplay. The level loading decision affects startup time, memory usage, and the boot coordination between LevelDataSystem and the systems that depend on it (LevelProgression, GameStateManager).

### Constraints
- `JsonUtility` is insufficient for the GDD's `LevelRecord` design: it cannot deserialize nullable `int?` (`hint_override`), cannot map JSON snake_case names to C# PascalCase properties (`[JsonProperty]` attributes), and has known issues with jagged arrays (`int[][]`) inside class hierarchies on IL2CPP builds
- `Newtonsoft.Json` (`com.unity.nuget.newtonsoft-json`) is required — it handles all three constraints and is a Unity-supported package
- A single `levels.json` catalogue file (vs. multiple per-level TextAssets) enables atomic catalogue reload (`ReloadAsync()`), `catalogue_version` tracking for change detection, and simpler Addressables group management
- Callers must not be able to invoke getters before initialization completes — `InvalidOperationException` is the correct guard
- LevelProgression's ADR-0001 dual-ready guard (`bool IsReady` + `event Action OnLevelDataReady`) must be maintained for backward compatibility — both the Task-based API and the subscribe-then-check interface are exposed
- `Task<T>` (standard .NET, .NET Standard 2.1) is used for `InitializeAsync()` / `ReloadAsync()` — UniTask (third-party) is not in the project's allowed-library list
- 512 MB memory ceiling on mid-range Android

### Requirements
- Level data available synchronously via `GetLevel(int levelId)` once in READY state
- `IsReady` + `OnLevelDataReady` compatibility bridge (subscribe-then-check — from ADR-0001)
- `Task InitializeAsync()` as the primary initialization contract for `GameBootstrap`
- `Task ReloadAsync()` for live-ops catalogue hot-swap
- Explicit state machine: UNINITIALIZED → LOADING → READY / DEGRADED
- `GetReadiness()`, `GetLevel(int)`, `GetRange(int, int)`, `GetByFilter(LevelFilter)` query API
- LevelProgression dual-ready guard: wait for BOTH `SaveSystem.IsReady` AND `LDS.IsReady` before calling `GSM.LoadLevel()`
- Addressables handles released after deserialization — only typed `LevelRecord` objects retained
- Duplicate-instance guard in Awake (per ADR-0001 singleton rules)

## Decision

### Serialization Library: Newtonsoft.Json

`JsonConvert.DeserializeObject<LevelCatalogue>(json)` (Newtonsoft.Json, Unity package `com.unity.nuget.newtonsoft-json`). `[JsonProperty("snake_case_name")]` attributes on all `LevelRecord` and `LevelCatalogue` properties allow JSON snake_case names to map to C# PascalCase properties. Private setters are honoured by Newtonsoft.Json. Nullable `int?` (`HintOverride`) deserializes correctly from JSON `null` or absent field. Jagged `int[][]` (`ColorStacks`) deserializes correctly from nested JSON arrays.

All serialized types must use `[JsonObject(MemberSerialization.OptIn)]` — only `[JsonProperty]`-attributed members are deserialized.

### File Structure: Single `levels.json` Catalogue

A single `levels.json` TextAsset is stored in the `LevelCatalogue-Local` Addressables group and loaded via `Addressables.LoadAssetAsync<TextAsset>("levels.json")`. The JSON root is a `LevelCatalogue` object containing `catalogue_version` and the `levels` array.

```json
{
  "catalogue_version": 1,
  "levels": [
    { "level_id": 1, "color_count": 3, "stack_depth": 4, ... },
    { "level_id": 2, ... }
  ]
}
```

`catalogue_version` is an integer that increments monotonically with each catalogue publication. It is sourced from the JSON root — not computed from record contents. Callers can use `GetReadiness().CatalogueVersion` to detect catalogue changes across reloads.

**Two-tier Addressables group:**
| Group | Label | Purpose | Build |
|-------|-------|---------|-------|
| `LevelCatalogue-Local` | `"levels.json"` | Default bundled catalogue | Local, bundled with app |
| `LevelCatalogue-Remote` | `"levels.json"` | Optional LiveOps override | Remote, checked post-READY |

First-launch guarantee: game must reach READY state with no internet connection using only the local catalogue.

### State Machine

```
UNINITIALIZED → LOADING → READY ──┐
                     │              │ ReloadAsync()
                     ↓              ↓
                  DEGRADED ──────► LOADING
```

| State | Entry | Exit | Getter Behavior |
|-------|-------|------|-----------------|
| UNINITIALIZED | App launch | `InitializeAsync()` called | All getters throw `InvalidOperationException` |
| LOADING | `InitializeAsync()` or `ReloadAsync()` called | READY or DEGRADED | All getters throw `InvalidOperationException` |
| READY | Load completes, `failure_ratio ≤ 0.20` | `ReloadAsync()` called | All getters safe to call |
| DEGRADED | Load completes, `failure_ratio > 0.20` or Addressables failure | `ReloadAsync()` called | Getters callable — return errors per normal contract |

`failure_ratio = failed_record_count / total_record_count`. Exactly `0.20` resolves to READY (strict greater-than). Empty catalogue (`total_record_count == 0`) → DEGRADED unconditionally.

### Task-Based Async API

```csharp
// GameBootstrap.Start():
await LevelDataSystem.Instance.InitializeAsync();
// System is now READY or DEGRADED — no subscriber setup required
```

`InitializeAsync()` is idempotent — calling while READY returns a completed Task immediately. Calling while LOADING returns the same in-flight Task (no second load issued). Only `GameBootstrap` calls `InitializeAsync()`; all other systems use getters only.

`ReloadAsync()` is callable only from READY or DEGRADED — throws `InvalidOperationException` from UNINITIALIZED. Calling while LOADING (reload already in progress) returns the same in-flight Task. Only `GameBootstrap` calls `ReloadAsync()`.

### Subscribe-Then-Check Compatibility Bridge

`bool IsReady` and `event Action OnLevelDataReady` are maintained for compatibility with ADR-0001's LevelProgression dual-ready guard:

```csharp
// LevelProgression.Awake() [SEO -30] — unchanged from ADR-0001
LevelDataSystem.Instance.OnLevelDataReady += HandleLDSReady;
if (LevelDataSystem.Instance.IsReady) HandleLDSReady();
```

`OnLevelDataReady` fires synchronously when `InitializeAsync()` completes — both `GameBootstrap`'s await and LevelProgression's subscribe-then-check observe the same event. The dual-ready guard in LevelProgression remains unchanged.

### Loading Flow

```csharp
public class LevelDataSystem : MonoBehaviour
{
    public static LevelDataSystem Instance { get; private set; }
    public bool IsReady { get; private set; }
    public event Action OnLevelDataReady;

    private LdsState _state = LdsState.Uninitialized;
    private readonly Dictionary<int, LevelRecord> _levelCache = new();
    private TaskCompletionSource<bool> _initTcs;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public Task InitializeAsync()
    {
        if (_state == LdsState.Ready || _state == LdsState.Degraded)
            return Task.CompletedTask;                    // READY — no-op
        if (_state == LdsState.Loading && _initTcs != null)
            return _initTcs.Task;                         // LOADING — same in-flight Task

        _state = LdsState.Loading;
        _initTcs = new TaskCompletionSource<bool>();
        LoadCatalogueAsync();                             // fire-and-forget coroutine
        return _initTcs.Task;
    }

    private async void LoadCatalogueAsync()
    {
        try
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>("levels.json");
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                EnterDegraded("CATALOGUE_LOAD_FAILED", 0, 0);
                return;
            }

            var catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(handle.Result.text);
            Addressables.Release(handle);

            int loaded = 0, skipped = 0;
            foreach (var record in catalogue?.Levels ?? Array.Empty<LevelRecord>())
            {
                if (ValidateRecord(record, out _))
                { _levelCache[record.LevelId] = record; loaded++; }
                else
                { skipped++; }
            }

            int total = loaded + skipped;
            float failureRatio = total > 0 ? (float)skipped / total : 1f;
            if (total == 0 || failureRatio > 0.20f)
                EnterDegraded("CATALOGUE_PARTIAL_FAILURE", loaded, skipped);
            else
                EnterReady(loaded, skipped, catalogue?.CatalogueVersion ?? 0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LevelDataSystem] LoadCatalogueAsync failed: {ex.Message}");
            EnterDegraded("CATALOGUE_LOAD_FAILED", 0, 0);
        }
    }

    private void EnterReady(int loaded, int skipped, int version)
    {
        _state = LdsState.Ready;
        IsReady = true;
        _readiness = new SystemReadiness(true, loaded, skipped, version, null);
        OnLevelDataReady?.Invoke();          // compatibility bridge fires here
        _initTcs?.SetResult(true);
    }

    private void EnterDegraded(string code, int loaded, int skipped)
    {
        _state = LdsState.Degraded;
        IsReady = true;                      // IsReady = true so callers don't hang
        _readiness = new SystemReadiness(false, loaded, skipped, 0, code);
        OnLevelDataReady?.Invoke();          // compatibility bridge fires even on DEGRADED
        _initTcs?.SetResult(true);
    }
}
```

### Validation and Getters

```csharp
public LevelRecord GetLevel(int levelId)
{
    if (_state == LdsState.Uninitialized || _state == LdsState.Loading)
        throw new InvalidOperationException("LevelDataSystem not ready");
    if (!_levelCache.TryGetValue(levelId, out var record))
        throw new LevelDataException($"Level {levelId} not found", LdsErrorCode.NotFound);
    return record;
}

public LevelRecord[] GetRange(int fromId, int toId)
{
    if (_state == LdsState.Uninitialized || _state == LdsState.Loading)
        throw new InvalidOperationException("LevelDataSystem not ready");
    if (fromId > toId) return Array.Empty<LevelRecord>();
    return Enumerable.Range(fromId, toId - fromId + 1)
        .Where(id => _levelCache.ContainsKey(id))
        .Select(id => _levelCache[id])
        .ToArray();
}

public LevelRecord[] GetByFilter(LevelFilter filter)
{
    if (_state == LdsState.Uninitialized || _state == LdsState.Loading)
        throw new InvalidOperationException("LevelDataSystem not ready");
    return _levelCache.Values.Where(filter.Matches).ToArray();
}

public SystemReadiness GetReadiness() => _readiness;
```

### Architecture Diagram

```
Frame 0 (Awake):
  LevelDataSystem.Awake() [SEO -95]
      └── Set Instance, DontDestroyOnLoad — no load yet

GameBootstrap.Start() [SEO 0, Start]:
  await LevelDataSystem.Instance.InitializeAsync()
      └── _state = LOADING → LoadCatalogueAsync() (fire-and-forget)
               ├── LoadAssetAsync<TextAsset>("levels.json") [Addressables, async]
               ├── JsonConvert.DeserializeObject<LevelCatalogue>(text) [main thread]
               ├── Addressables.Release(handle)
               ├── Stage 2 validation per record → _levelCache
               └── failure_ratio check → EnterReady() or EnterDegraded()
                    ├── OnLevelDataReady?.Invoke() [compatibility bridge]
                    └── _initTcs.SetResult(true) [Task completes]

LevelProgression.Awake() [SEO -30] — still uses subscribe-then-check:
  LDS.OnLevelDataReady += HandleLDSReady;
  if (LDS.IsReady) HandleLDSReady();  // IsReady = false until GameBootstrap awaits
  [subscribe-then-check: will fire when InitializeAsync completes and OnLevelDataReady fires]

GameBootstrap after await completes:
  → GameBootstrap.OnInitialized() — triggers GSM.LoadLevel(current_level_id) if both
    SaveSystem.IsReady AND LDS.IsReady are true
```

### Key Interfaces

```csharp
public interface ILevelDataSystem
{
    bool IsReady { get; }
    event Action OnLevelDataReady;      // compatibility bridge — fires on READY and DEGRADED

    Task InitializeAsync();
    Task ReloadAsync();
    SystemReadiness GetReadiness();
    LevelRecord GetLevel(int levelId);               // throws if not READY or DEGRADED
    LevelRecord[] GetRange(int fromId, int toId);    // returns empty array if inverted
    LevelRecord[] GetByFilter(LevelFilter filter);
}

public enum LdsState { Uninitialized, Loading, Ready, Degraded }
public enum LdsErrorCode { NotFound, ValidationFailed, VersionMismatch, SystemNotReady }

[JsonObject(MemberSerialization.OptIn)]
public sealed class LevelCatalogue
{
    [JsonProperty("catalogue_version")] public int CatalogueVersion { get; private set; }
    [JsonProperty("levels")]            public LevelRecord[] Levels { get; private set; }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class LevelRecord
{
    [JsonProperty("level_id")]               public int LevelId { get; private set; }
    [JsonProperty("display_name")]           public string DisplayName { get; private set; }
    [JsonProperty("difficulty_tier")]        public int DifficultyTier { get; private set; }
    [JsonProperty("schema_version")]         public int SchemaVersion { get; private set; }
    [JsonProperty("color_count")]            public int ColorCount { get; private set; }
    [JsonProperty("stack_depth")]            public int StackDepth { get; private set; }
    [JsonProperty("color_stacks")]           public int[][] ColorStacks { get; private set; }
    [JsonProperty("temp_slot_count")]        public int TempSlotCount { get; private set; }
    [JsonProperty("temp_slot_depth")]        public int TempSlotDepth { get; private set; }
    [JsonProperty("is_tutorial")]            public bool IsTutorial { get; private set; }
    [JsonProperty("daily_challenge_eligible")] public bool DailyChallengeEligible { get; private set; }
    [JsonProperty("hint_override")]          public int? HintOverride { get; private set; }  // null = system default; 0 = zero hints
    [JsonProperty("added_version")]          public string AddedVersion { get; private set; }
    [JsonProperty("par_moves")]              public int ParMoves { get; private set; }
}

public readonly struct SystemReadiness
{
    public bool   Ready            { get; }
    public int    LoadedCount      { get; }
    public int    SkippedCount     { get; }
    public int    CatalogueVersion { get; }
    public string DiagnosticCode   { get; }

    public SystemReadiness(bool ready, int loaded, int skipped, int version, string code)
    { Ready = ready; LoadedCount = loaded; SkippedCount = skipped;
      CatalogueVersion = version; DiagnosticCode = code; }
}

public sealed class LevelFilter
{
    public int? DifficultyTier { get; set; }
    public bool? DailyChallengeEligible { get; set; }
    public int? ColorCountMin { get; set; }
    public int? ColorCountMax { get; set; }
    public string AddedVersionMin { get; set; }     // lexicographic comparison valid (YYYY.MM zero-padded)

    public bool Matches(LevelRecord r)
    {
        if (DifficultyTier.HasValue && r.DifficultyTier != DifficultyTier.Value) return false;
        if (DailyChallengeEligible.HasValue && r.DailyChallengeEligible != DailyChallengeEligible.Value) return false;
        if (ColorCountMin.HasValue && r.ColorCount < ColorCountMin.Value) return false;
        if (ColorCountMax.HasValue && r.ColorCount > ColorCountMax.Value) return false;
        if (AddedVersionMin != null && string.Compare(r.AddedVersion, AddedVersionMin, StringComparison.Ordinal) < 0) return false;
        return true;
    }
}
```

## Alternatives Considered

### Alternative A: `Resources.Load<TextAsset>` (Synchronous)
- **Rejection Reason**: No memory management; no future remote delivery path. Addressables is in the allowed-library list for this purpose.

### Alternative B: Lazy Load Per-Level (on `GetLevel()` call)
- **Rejection Reason**: Breaks synchronous `GetLevel()` contract required by GSM.

### Alternative C: UniTask instead of `Task`
- **Description**: Use UniTask (`com.cysharp.unitask`) for `InitializeAsync()` and `ReloadAsync()` return type.
- **Pros**: More memory-efficient than `Task` on hot paths; Unity-native integration; ValueTask semantics
- **Cons**: Third-party package not in `technical-preferences.md` allowed-library list; adds dependency; project already uses `Awaitable` (ADR-0003) — mixing patterns
- **Rejection Reason**: Not in allowed-library list. Standard `Task<T>` (.NET Standard 2.1 built-in) suffices for two low-frequency async operations. The GDD Open Question explicitly listed this as a decision to make before implementation — resolved here as `Task`.

### Alternative D: Original Design (2026-05-02) — Multiple TextAssets, JsonUtility, Subscribe-Then-Check
- **Description**: Batch-load multiple `TextAsset` files via `LoadAssetsAsync<TextAsset>`, deserialize with `JsonUtility.FromJson<LevelRecord>()`, expose `bool IsReady` + `event Action OnLevelDataReady` only (no `InitializeAsync()`).
- **Rejection Reason**: `JsonUtility` insufficient for GDD's `LevelRecord` design: cannot handle nullable `int?` (`HintOverride`), cannot map snake_case JSON to PascalCase C# properties, exhibits IL2CPP issues with jagged arrays inside class hierarchies. Single-file catalogue is required for `ReloadAsync()` atomicity and `catalogue_version` tracking. Task-based API is cleaner for GameBootstrap sequencing than relying on event timing at SEO 0.

## Consequences

### Positive
- Newtonsoft.Json correctly handles all field types in `LevelRecord` (nullable int, private setters, attribute mapping, jagged arrays)
- Single-file catalogue enables atomic `ReloadAsync()` and `catalogue_version` tracking for LiveOps
- `Task`-based `InitializeAsync()` gives `GameBootstrap` clean async sequencing
- Subscribe-then-check bridge (`IsReady` + `OnLevelDataReady`) keeps LevelProgression's dual-ready guard from ADR-0001 unchanged
- `GetRange()` and `GetByFilter()` enable Level Progression prefetch and Daily Challenge pool selection without re-querying Addressables

### Negative
- Newtonsoft.Json adds a package dependency (~500KB) and must be added to `technical-preferences.md` Allowed Libraries
- Single `levels.json` file means ALL level data is loaded at once — cannot lazy-load individual levels. For 200+ levels this adds ~1MB initial load vs. streaming only the current level's data.
- `task.GetAwaiter().GetResult()` blocking pattern is forbidden — callers must use `await` properly

### Risks
- **Risk**: `[JsonProperty]` absent from a `LevelRecord` field → field silently deserializes as default value. **Mitigation**: `[JsonObject(MemberSerialization.OptIn)]` ensures only attributed members are read — unattributed fields silently return default, caught by Stage 2 validation.
- **Risk**: `InitializeAsync()` called from `MonoBehaviour.Awake()` before main thread's SynchronizationContext is set → `await` continuation may run on thread pool. **Mitigation**: `GameBootstrap.Start()` is the correct call site — `Start()` is guaranteed to run on main thread after all `Awake()` calls, and Unity's SynchronizationContext is active.
- **Risk**: Large catalogue file (200+ levels × ~0.5KB = ~100KB) causes a single-frame hitch during deserialization. **Mitigation**: At ~100KB, `JsonConvert.DeserializeObject` completes in <5ms on target hardware. If catalogue grows beyond 500 levels, consider moving deserialization to a background thread.
- **Risk**: Newtonsoft.Json not stripped by IL2CPP → build size increase. **Mitigation**: Unity's managed code stripping should handle unused serializer paths; test build size vs. baseline.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| level-data-system.md | TR-LDS-001: Level record schema (camelCase/snake_case JSON, LevelRecord type) | Defines `LevelRecord` with `[JsonProperty]` attributes; Newtonsoft.Json handles snake_case JSON ↔ PascalCase C# mapping |
| level-data-system.md | TR-LDS-002: `bolt_count_invariant` at authoring time and runtime `GetLevel()` | Stage 2 validation in `ValidateRecord()` covers bolt count invariant and all field constraints |
| level-data-system.md | TR-LDS-003: System readiness pattern; `InitializeAsync()`; subscribe-then-check bridge | `Task InitializeAsync()` for GameBootstrap; `bool IsReady + event Action OnLevelDataReady` for LevelProgression compatibility |
| level-data-system.md | TR-LDS-004: DEGRADED state when `failure_ratio > 0.20` or catalogue empty | `EnterDegraded()` covers both cases; `OnLevelDataReady` fires even on DEGRADED so no caller hangs |

## Performance Implications
- **CPU**: `JsonConvert.DeserializeObject<LevelCatalogue>()`: ~1–5ms for 100–200 levels × 0.5KB JSON. Stage 2 validation per record: ~0.05ms each. Total: <10ms for 200 levels.
- **Memory**: `levels.json` TextAsset: ~100KB held during deserialization, released after. `_levelCache` dictionary: ~200 records × ~0.5KB ≈ ~100KB persistent.
- **Load Time**: Time from `InitializeAsync()` call to `READY`: Addressables bundle read + JSON parse. Target: <300ms on Galaxy A14 for 200 levels.

## Validation Criteria
1. Unit test: `JsonConvert.DeserializeObject<LevelRecord>` round-trip with nullable `HintOverride = null` and `HintOverride = 0` — verify `int?` correctly distinguished
2. Unit test: `LevelRecord` with `int[][] ColorStacks` — verify nested array deserialized correctly on IL2CPP build
3. Unit test: `InitializeAsync()` idempotency — call twice while LOADING; verify same `Task` returned
4. Unit test: `InitializeAsync()` in READY — verify returns completed Task immediately, no reload issued
5. Unit test: `failure_ratio = 0.20` exactly → READY state (strict greater-than threshold)
6. Unit test: `GetRange(100, 50)` inverted parameters → empty array, no exception
7. Integration test: `GameBootstrap` awaits `InitializeAsync()`; after await, `GetLevel(1)` returns valid record
8. Integration test: `LevelProgression` subscribe-then-check — `OnLevelDataReady` fires after `InitializeAsync()` completes on GameBootstrap; LevelProgression's dual-ready guard triggers correctly

## Related Decisions
- ADR-0001: Singleton Architecture — LDS at SEO −95; subscribe-then-check bridge kept for LevelProgression
- ADR-0006: Board State Representation — `GSM.LoadLevel()` calls `GetLevel()` synchronously
- ADR-0013: Level Layout Column Cap — `LevelRecordValidator` authoring pipeline enforces `color_count + temp_slot_count ≤ 8`
- `design/gdd/level-data-system.md` — authoritative source for schema fields, validation rules, and state machine (Approved)
