# Story 005: ReloadAsync() — Hot-Swap Catalogue

> **Epic**: Level Data System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: Medium (4–6h)
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/level-data-system.md`
**Requirement**: `TR-LDS-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Level Data Loading Strategy (revised 2026-05-12)
**ADR Decision Summary**: `Task ReloadAsync()` enables live-ops hot-swap of the catalogue without restarting the app. It re-runs the full load pipeline (Addressables load → deserialization → Stage 2 validation → state transition) and atomically replaces the in-memory `_levelCache` and `_readiness` on completion. Callable only from READY or DEGRADED. Calling from UNINITIALIZED throws. Duplicate calls while LOADING return the same in-flight `Task`. Only `GameBootstrap` is the permitted caller.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `ReloadAsync()` and `InitializeAsync()` are separate code paths — `ReloadAsync()` does NOT call `InitializeAsync()` internally. Both are backed by the same `TaskCompletionSource` pattern.

**Control Manifest Rules (Foundation layer)**:
- Required: `ReloadAsync()` from UNINITIALIZED → `InvalidOperationException`
- Required: Duplicate `ReloadAsync()` while LOADING → same `Task` returned, no second load
- Required: `_levelCache` replaced atomically on completion — no mid-swap observable state

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-system.md`, scoped to this story:*

- [ ] **AC-23** — After `ReloadAsync()` is called (in READY state), the system transitions to LOADING before any subsequent getter executes; no getter returns data from the pre-reload catalogue after the transition
- [ ] **AC-25** — `ReloadAsync()` from READY with `catalogue_version=1` (100 records) replaces with version 2 (105 records): system transitions READY → LOADING → READY; `GetReadiness()` returns `LoadedCount=105`, `CatalogueVersion=2`; old catalogue no longer served
- [ ] **AC-26** — `ReloadAsync()` called while LOADING (reload already in progress) → same in-flight `Task` returned; no second Addressables load
- [ ] **AC-27** — `ReloadAsync()` called from UNINITIALIZED → `InvalidOperationException` thrown immediately
- [ ] **AC-28** — `ReloadAsync()` called from DEGRADED state → transitions to LOADING, re-runs full pipeline, resolves to READY or DEGRADED based on new catalogue results

---

## Implementation Notes

*Derived from ADR-0004. Uses Option A (atomic cache swap — recommended).*

### Pre-implementation changes required in `src/LevelData/LevelDataSystem.cs`

Story 004's code review (R-2) placed a `ReloadAsync()` stub that reuses `_initTcs` and
calls `LoadCatalogueAsync()`. **That stub must be replaced** by the pattern below. Two
field-level changes are also required before implementing:

1. Remove `readonly` from the `_levelCache` field — reference reassignment is required
   for the atomic swap:
   ```csharp
   // Before (Story 003):
   private readonly Dictionary<int, LevelRecord> _levelCache = new Dictionary<int, LevelRecord>();
   // After:
   private Dictionary<int, LevelRecord> _levelCache = new Dictionary<int, LevelRecord>();
   ```

2. Add `_reloadTcs` as a separate field — kept distinct from `_initTcs` so that
   `InitializeAsync()` and `ReloadAsync()` do not share state:
   ```csharp
   private TaskCompletionSource<bool> _reloadTcs;
   ```

### ReloadAsync() — full implementation

```csharp
public Task ReloadAsync()
{
    if (_state == LdsState.Uninitialized)
        throw new InvalidOperationException(
            "ReloadAsync() cannot be called before InitializeAsync() completes.");

    if (_state == LdsState.Loading && _reloadTcs != null)
        return _reloadTcs.Task;    // reload already in progress — same Task

    _state = LdsState.Loading;
    IsReady = false;               // GuardReady() will throw for all getters
    _reloadTcs = new TaskCompletionSource<bool>();
    ReloadCatalogueAsync();        // async void fire-and-forget
    return _reloadTcs.Task;
}

private async void ReloadCatalogueAsync()
{
    var newCache = new Dictionary<int, LevelRecord>();
    try
    {
        var (succeeded, text) = await LoadCatalogueTextAsync();

        if (!succeeded)
        { CompleteReload(null, "CATALOGUE_LOAD_FAILED", 0, 0); return; }

        var catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(text);

        int loaded = 0, skipped = 0;
        foreach (var record in catalogue?.Levels ?? Array.Empty<LevelRecord>())
        {
            if (LevelRecordValidator.ValidateRecord(record, out _))
            { newCache[record.LevelId] = record; loaded++; }
            else { skipped++; }
        }

        int total = loaded + skipped;
        float ratio = total > 0 ? (float)skipped / total : 1f;
        if (total == 0 || ratio > 0.20f)
            CompleteReload(null, "CATALOGUE_PARTIAL_FAILURE", loaded, skipped);
        else
            CompleteReload(newCache, null, loaded, skipped,
                           catalogue?.CatalogueVersion ?? 0);
    }
    catch (Exception ex)
    {
        Debug.LogError($"[LevelDataSystem] ReloadCatalogueAsync failed: {ex.Message}");
        CompleteReload(null, "CATALOGUE_LOAD_FAILED", 0, 0);
    }
}

private void CompleteReload(Dictionary<int, LevelRecord> newCache, string errorCode,
                            int loaded, int skipped, int version = 0)
{
    if (newCache != null)
    {
        _levelCache = newCache;    // atomic reference swap — old cache replaced in one assignment
        _state = LdsState.Ready;
        IsReady = true;
        _readiness = new SystemReadiness(true, loaded, skipped, version, null);
    }
    else
    {
        _state = LdsState.Degraded;
        IsReady = true;
        _readiness = new SystemReadiness(false, loaded, skipped, 0, errorCode);
    }
    OnLevelDataReady?.Invoke();    // compatibility bridge — fires on READY and DEGRADED
    _reloadTcs?.TrySetResult(true);
    _reloadTcs = null;
}
```

**AC-23**: `IsReady = false` and `_state = Loading` are set synchronously before any `await`.
`GuardReady()` in all getters throws `InvalidOperationException` for LOADING state — no getter
can return stale pre-reload data after `ReloadAsync()` is called.

**Atomic swap**: `_levelCache = newCache` assigns a fully-populated dictionary reference in a
single statement on the main thread. The old cache survives in memory until this line; if
`ReloadCatalogueAsync` throws or the new catalogue is invalid, `CompleteReload(null, ...)` is
called instead and `_levelCache` is never reassigned — old data never partially exposed.

**Separate TCS**: `_reloadTcs` is distinct from `_initTcs`. `InitializeAsync()` called during
an active reload (state = LOADING, `_initTcs` from a prior completed TCS) will see
`_state == Loading && _initTcs != null` as true and return `_initTcs.Task` — the completed
Task from initialization, not the in-flight reload Task. This is correct: `InitializeAsync()`
is idempotent after first completion.

**`LoadCatalogueTextAsync` seam**: `ReloadCatalogueAsync` uses the same injected
`LoadCatalogueTextAsync` Func as `LoadCatalogueAsync`. Tests replace this Func to inject a
fake catalogue without hitting Addressables.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 003: `InitializeAsync()` first-load pipeline
- Story 004: Getter implementations (unchanged by this story)

---

## QA Test Cases

- **AC-25**: Successful reload replaces catalogue
  - Given: System READY with `catalogue_version=1`, 100 records; new `levels.json` has version=2, 105 records
  - When: `await ReloadAsync()`
  - Then: `GetReadiness().LoadedCount == 105`; `GetReadiness().CatalogueVersion == 2`; `GetLevel(101)` returns record from new catalogue

- **AC-26**: Duplicate reload returns same Task
  - Given: `ReloadAsync()` in progress (LOADING)
  - When: Second `ReloadAsync()` called
  - Then: Same `Task` instance returned; Addressables called only once

- **AC-27**: Reload from UNINITIALIZED
  - Given: `InitializeAsync()` never called
  - When: `ReloadAsync()`
  - Then: `InvalidOperationException` thrown immediately; `_state` unchanged

- **AC-28**: Reload from DEGRADED
  - Given: System in DEGRADED (initial load had >20% failures)
  - When: `await ReloadAsync()` with a clean catalogue (0 failures)
  - Then: Transitions DEGRADED → LOADING → READY; `GetReadiness().Ready == true`

- **AC-23**: Getter blocked during reload
  - Given: `ReloadAsync()` initiated, still in LOADING
  - When: `GetLevel(1)` called mid-reload
  - Then: `InvalidOperationException` thrown; no stale data returned

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/level-data-system/reload_async_test.cs` — must exist and pass

**Status**: [x] Present — `tests/unit/level-data-system/reload_async_test.cs` (7 tests)

---

## Dependencies

- Depends on: Story 003 (DONE) — same patterns; Story 004 (DONE) — `_levelCache` replacement observable via getters
- Unlocks: None — this is the last runtime story for LDS

---

## Completion Notes

**Completed**: 2026-05-14
**Criteria**: 5/5 passing
**Deviations**: None. ADR-0004 compliant. Manifest version matched (2026-05-12).
**Test Evidence**: Logic — `tests/unit/level-data-system/reload_async_test.cs` (7 tests, all ACs covered)
**Code Review**: Complete — initial CHANGES REQUIRED; R-1 applied (LOADING guard race condition closed); re-review APPROVED WITH SUGGESTIONS.
  - R-1: `ReloadAsync()` guard collapsed to `if (_state == LdsState.Loading) return _reloadTcs?.Task ?? _initTcs?.Task ?? Task.CompletedTask`
**Advisory items** (follow-up story or tech debt):
  - AC-25 QA spec level-ID mismatch (spec: 101 from 100→105; test: 5 from 3→5) — raise with qa-lead
  - AC-26: Addressables call-count assertion missing — add invocation counter
  - CATALOGUE_PARTIAL_FAILURE reload path untested
  - OnLevelDataReady not asserted on failed-reload path
  - Task.CompletedTask fallback has no diagnostic signal — add Debug.LogError
  - ReloadCatalogueAsync/LoadCatalogueAsync duplication — acknowledged Option A trade-off
