# Story 003: InitializeAsync() — Load Pipeline and State Machine

> **Epic**: Level Data System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/level-data-system.md`
**Requirement**: `TR-LDS-003`, `TR-LDS-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004 (revised 2026-05-12); ADR-0001 (boot sequence, compatibility bridge)
**ADR Decision Summary**: `Task InitializeAsync()` is the primary initialization contract for `GameBootstrap`. It loads `levels.json` via `Addressables.LoadAssetAsync<TextAsset>`, deserializes with Newtonsoft.Json, runs Stage 2 validation per record, applies the `failure_ratio` threshold, and transitions to READY or DEGRADED. `OnLevelDataReady` fires on completion (both paths) as a subscribe-then-check compatibility bridge for LevelProgression's dual-ready guard from ADR-0001. `IsReady = true` is set on both READY and DEGRADED transitions.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `Addressables.LoadAssetAsync<TextAsset>` is the single-asset form (Addressables 2.x). `await handle.Task` suspends the coroutine until load completes. The `async void LoadCatalogueAsync()` fire-and-forget pattern is intentional here — `InitializeAsync()` returns a `Task` backed by `TaskCompletionSource`, which is resolved when `EnterReady()` or `EnterDegraded()` fires.

**Control Manifest Rules (Foundation layer)**:
- Required: `IsReady = true` set on both READY and DEGRADED transitions — consumers must not hang
- Required: `OnLevelDataReady?.Invoke()` fires on both READY and DEGRADED — subscribe-then-check bridge
- Required: `failure_ratio > 0.20` (strict greater-than) → DEGRADED; exactly `0.20` → READY
- Required: `total_record_count == 0` → DEGRADED unconditionally
- Forbidden: Propagating exceptions to `InitializeAsync()` caller — all failures produce DEGRADED, not exceptions

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-system.md`, scoped to this story:*

- [ ] **AC-05** — `failure_ratio = 0.20` exactly (20/100 fail) → READY; 80 passing records available; `GetReadiness().Ready == true`
- [ ] **AC-06** — `failure_ratio = 0.21` (21/100 fail) → DEGRADED; 79 passing records still served; `GetReadiness().SkippedCount == 21`
- [ ] **AC-07** — Zero records in catalogue → DEGRADED unconditionally; `LoadedCount == 0`, `SkippedCount == 0`; diagnostic flag set
- [ ] **AC-10** — `GetLevel()`, `GetRange()`, `GetByFilter()` called before `InitializeAsync()` or while LOADING → `InvalidOperationException` thrown immediately
- [ ] **AC-11** — `InitializeAsync()` called while LOADING → same in-flight `Task` returned; no second Addressables load issued
- [ ] **AC-12** — `InitializeAsync()` called while READY → completed `Task` returned immediately; no reload
- [ ] **AC-17** — Addressables key `"levels.json"` fails to resolve → DEGRADED; `LoadedCount == 0`; `DiagnosticCode == "CATALOGUE_LOAD_FAILED"`; no exception propagates to caller
- [ ] `OnLevelDataReady` fires after `InitializeAsync()` completes on both READY and DEGRADED paths
- [ ] `IsReady == true` after both READY and DEGRADED transitions
- [ ] `LevelProgression`'s subscribe-then-check (`LDS.OnLevelDataReady += HandleLDSReady; if (LDS.IsReady) HandleLDSReady()`) correctly receives the event when `InitializeAsync()` completes after subscription

---

## Implementation Notes

*Derived from ADR-0004:*

```csharp
private TaskCompletionSource<bool> _initTcs;

public Task InitializeAsync()
{
    if (_state == LdsState.Ready || _state == LdsState.Degraded)
        return Task.CompletedTask;
    if (_state == LdsState.Loading && _initTcs != null)
        return _initTcs.Task;

    _state = LdsState.Loading;
    _initTcs = new TaskCompletionSource<bool>();
    LoadCatalogueAsync();    // async void fire-and-forget; resolves _initTcs on completion
    return _initTcs.Task;
}

private async void LoadCatalogueAsync()
{
    try {
        var handle = Addressables.LoadAssetAsync<TextAsset>("levels.json");
        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        { EnterDegraded("CATALOGUE_LOAD_FAILED", 0, 0); return; }

        var catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(handle.Result.text);
        Addressables.Release(handle);

        int loaded = 0, skipped = 0;
        foreach (var record in catalogue?.Levels ?? Array.Empty<LevelRecord>())
        {
            if (ValidateRecord(record, out _))
            { _levelCache[record.LevelId] = record; loaded++; }
            else { skipped++; }
        }

        int total = loaded + skipped;
        float ratio = total > 0 ? (float)skipped / total : 1f;
        if (total == 0 || ratio > 0.20f)
            EnterDegraded("CATALOGUE_PARTIAL_FAILURE", loaded, skipped);
        else
            EnterReady(loaded, skipped, catalogue?.CatalogueVersion ?? 0);
    }
    catch (Exception ex)
    { Debug.LogError($"[LDS] {ex.Message}"); EnterDegraded("CATALOGUE_LOAD_FAILED", 0, 0); }
}

private void EnterReady(int loaded, int skipped, int version)
{
    _state = LdsState.Ready;
    IsReady = true;
    _readiness = new SystemReadiness(true, loaded, skipped, version, null);
    OnLevelDataReady?.Invoke();     // compatibility bridge
    _initTcs?.SetResult(true);
}

private void EnterDegraded(string code, int loaded, int skipped)
{
    _state = LdsState.Degraded;
    IsReady = true;                 // true even on DEGRADED — callers must not hang
    _readiness = new SystemReadiness(false, loaded, skipped, 0, code);
    OnLevelDataReady?.Invoke();     // compatibility bridge fires even on DEGRADED
    _initTcs?.SetResult(true);
}
```

**Important**: `InitializeAsync()` must be callable from `MonoBehaviour.Start()` (not `Awake()`) — `Start()` is guaranteed to run after all `Awake()` calls and the Unity SynchronizationContext is active, ensuring `await` continuations dispatch to the main thread.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 001: Type definitions (`LdsState`, `LevelRecord`, `LevelCatalogue`)
- Story 002: `ValidateRecord()` implementation
- Story 004: Getter methods (`GetLevel`, `GetRange`, `GetByFilter`, `GetReadiness`)
- Story 005: `ReloadAsync()` implementation

---

## Estimate

Medium — 6–8 hours

---

## QA Test Cases

- **AC-05**: Threshold at 0.20 exactly → READY
  - Given: 100-record catalogue where exactly 20 fail validation
  - When: `await InitializeAsync()`
  - Then: `_state == LdsState.Ready`; `GetReadiness().Ready == true`; `GetReadiness().LoadedCount == 80`

- **AC-06**: Above 0.20 → DEGRADED
  - Given: 100-record catalogue, 21 failures
  - When: `await InitializeAsync()`
  - Then: `_state == LdsState.Degraded`; `GetReadiness().Ready == false`; `GetReadiness().SkippedCount == 21`; valid records still in `_levelCache`

- **AC-07**: Empty catalogue → DEGRADED unconditionally
  - Given: `levels.json` with `{ "catalogue_version": 1, "levels": [] }`
  - When: `await InitializeAsync()`
  - Then: DEGRADED; `LoadedCount == 0`; `SkippedCount == 0`; `DiagnosticCode != null`

- **AC-10**: Pre-init getter throws
  - Given: `InitializeAsync()` never called
  - When: `GetLevel(1)` called
  - Then: `InvalidOperationException` thrown; no null ref, no silent return

- **AC-11**: Concurrent `InitializeAsync()` calls
  - Given: First call initiated, still in LOADING
  - When: Second `InitializeAsync()` call made
  - Then: Same `Task` instance returned; Addressables `LoadAssetAsync` called only once (verify via mock)

- **AC-17**: Addressables failure → DEGRADED, no exception
  - Given: Mock Addressables that returns `AsyncOperationStatus.Failed`
  - When: `await InitializeAsync()`
  - Then: Returns normally (no exception thrown); `GetReadiness().DiagnosticCode == "CATALOGUE_LOAD_FAILED"`; `LoadedCount == 0`

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/level-data-system/init_async_test.cs` — must exist and pass

**Status**: [x] Created and reviewed — 13 tests (13 `[UnityTest]`/`[Test]`), two-pass code review APPROVED 2026-05-13

---

## Dependencies

- Depends on: Story 001 (DONE), Story 002 (DONE) — requires types and `ValidateRecord()`
- Unlocks: Story 004 (getters operate on `_levelCache` populated here)

---

## Completion Notes
**Completed**: 2026-05-13
**Criteria**: 13/13 passing (all covered by automated integration tests)
**Deviations**: `src/AssemblyInfo.cs` added (InternalsVisibleTo for test seam — not listed in story scope but required infrastructure); getter stubs throw `NotImplementedException` after `GuardReady` — intentional partial implementation, Story 004 fills bodies
**Test Evidence**: Integration — `tests/integration/level-data-system/init_async_test.cs` (13 tests)
**Code Review**: APPROVED — two-pass review (2026-05-13); fixes: Addressables handle `try/finally`, `TrySetResult` replacing `SetResult`, LOADING-state getter test added
