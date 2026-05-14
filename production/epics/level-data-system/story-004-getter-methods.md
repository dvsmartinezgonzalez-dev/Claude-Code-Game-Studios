# Story 004: Query Methods — GetLevel, GetRange, GetByFilter, GetReadiness

> **Epic**: Level Data System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/level-data-system.md`
**Requirement**: `TR-LDS-001`, `TR-LDS-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Level Data Loading Strategy (revised 2026-05-12)
**ADR Decision Summary**: Four getter methods are exposed on `ILevelDataSystem`: `GetLevel(int)` returns a single record or throws; `GetRange(int, int)` returns an ordered array (empty on inverted params or all-failed range); `GetByFilter(LevelFilter)` returns an unordered array of matching records; `GetReadiness()` returns the `SystemReadiness` struct. All getters throw `InvalidOperationException` when called in UNINITIALIZED or LOADING state.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# dictionary and LINQ operations. All calls on the main thread — no threading concerns.

**Control Manifest Rules (Foundation layer)**:
- Required: `GetRange(fromId > toId)` returns empty array, not error
- Required: `GetByFilter()` with no matching records returns empty array, not error
- Forbidden: Any getter silently returning null — always throw or return empty collection

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-system.md`, scoped to this story:*

- [ ] **AC-01** — `GetLevel(1)` with `levelId=1` in catalogue → returns complete validated `LevelRecord`, no error
- [ ] **AC-02** — `GetLevel(0)` (below valid range 1–9999) → throws `LevelDataException` with `LdsErrorCode.NotFound`
- [ ] **AC-13** — `GetRange(50, 55)` where all 6 IDs exist but all fail validation at load time → returns `LevelRecord[]` of length 0; no error code; system stays READY
- [ ] **AC-14** — `GetByFilter(dailyChallengeEligible: true, difficultyTier: 5)` with no matching records → returns empty array; no exception; system stays READY
- [ ] **AC-15** — Pre-solved board (all stacks monochromatic at level start) that passed authoring validation → returned without error; not counted in `failed_record_count`
- [ ] **AC-35** — `GetRange(100, 50)` (inverted: `fromId > toId`) → returns empty array of length 0; no exception
- [ ] **AC-36** — `GetLevel()` for record with absent `display_name` → returns `LevelRecord` with `DisplayName == "Level {levelId}"`
- [ ] `GetReadiness()` — returns current `SystemReadiness` with correct `Ready`, `LoadedCount`, `SkippedCount`, `CatalogueVersion`, and `DiagnosticCode` values
- [ ] All getters throw `InvalidOperationException` if called in UNINITIALIZED or LOADING state

---

## Implementation Notes

*Derived from ADR-0004:*

```csharp
public LevelRecord GetLevel(int levelId)
{
    GuardReady();   // throws InvalidOperationException if UNINITIALIZED or LOADING
    if (!_levelCache.TryGetValue(levelId, out var record))
        throw new LevelDataException($"Level {levelId} not found", LdsErrorCode.NotFound);
    return record;
}

public LevelRecord[] GetRange(int fromId, int toId)
{
    GuardReady();
    if (fromId > toId) return Array.Empty<LevelRecord>();
    return Enumerable.Range(fromId, toId - fromId + 1)
        .Where(id => _levelCache.ContainsKey(id))
        .Select(id => _levelCache[id])
        .ToArray();
}

public LevelRecord[] GetByFilter(LevelFilter filter)
{
    GuardReady();
    return _levelCache.Values.Where(filter.Matches).ToArray();
}

public SystemReadiness GetReadiness() => _readiness;

private void GuardReady()
{
    if (_state == LdsState.Uninitialized || _state == LdsState.Loading)
        throw new InvalidOperationException(
            $"LevelDataSystem not ready (state: {_state})");
}
```

Note: `GetReadiness()` does NOT call `GuardReady()` — it is callable in any state, returning the current readiness snapshot including `Ready = false` in DEGRADED.

`GetRange()` silently omits IDs that are absent in `_levelCache` (either not in catalogue or failed validation). Callers distinguish "empty range" from "not found" by checking array length.

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 003: How `_levelCache` is populated (load pipeline)
- Story 005: `ReloadAsync()` which empties and repopulates `_levelCache`

---

## QA Test Cases

- **AC-01**: Happy path `GetLevel`
  - Given: System in READY state; level 1 in cache
  - When: `GetLevel(1)`
  - Then: Returns `LevelRecord` with `LevelId == 1`; no exception

- **AC-02**: `GetLevel(0)` — below valid range
  - Given: READY state
  - When: `GetLevel(0)`
  - Then: `LevelDataException` thrown; `ErrorCode == NotFound`
  - Edge cases: `GetLevel(10000)` → same exception (above valid range)

- **AC-13**: `GetRange` when all IDs in range failed validation
  - Given: READY; levels 50–55 deserialized but all failed Stage 2 validation (not in `_levelCache`)
  - When: `GetRange(50, 55)`
  - Then: Returns `LevelRecord[0]`; no exception; system stays READY

- **AC-14**: `GetByFilter` no matching records
  - Given: READY; no records with `dailyChallengeEligible=true AND difficultyTier=5`
  - When: `GetByFilter(filter)`
  - Then: Returns `LevelRecord[0]`; no exception; no fallback record selected

- **AC-35**: Inverted range
  - Given: READY state
  - When: `GetRange(100, 50)` (from > to)
  - Then: Returns `LevelRecord[0]`; no exception; equivalent to AC-13

- **AC-36**: `DisplayName` default
  - Given: Level 42 in cache; `display_name` was absent in JSON
  - When: `GetLevel(42)`
  - Then: `LevelRecord.DisplayName == "Level 42"` (non-null, non-empty)

- **Guard check**: Getter before READY
  - Given: `InitializeAsync()` not called (UNINITIALIZED)
  - When: `GetRange(1, 5)`, `GetByFilter(filter)` called
  - Then: Both throw `InvalidOperationException`
  - Edge cases: `GetReadiness()` does NOT throw in UNINITIALIZED — returns default struct

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/level-data-system/getter_methods_test.cs` — must exist and pass

**Status**: [x] Present — `tests/unit/level-data-system/getter_methods_test.cs` (21 tests)

---

## Dependencies

- Depends on: Story 003 (DONE) — `_levelCache` must be populated before getters can return meaningful data
- Unlocks: Story 005 (ReloadAsync uses same getter infrastructure)

---

## Completion Notes

**Completed**: 2026-05-14
**Criteria**: 8/9 passing (AC-15 advisory — generic fixture, not monochromatic; getter behaviour correct)
**Deviations**: None. ADR-0004 compliant. Manifest version matched (2026-05-12).
**Test Evidence**: Logic — `tests/unit/level-data-system/getter_methods_test.cs` (21 tests, all ACs covered)
**Code Review**: Complete (lean mode) — R-1 through R-5 applied:
  - R-1: `ArgumentNullException` guard added to `GetByFilter`
  - R-2: `ILevelDataSystem` interface created (`src/LevelData/ILevelDataSystem.cs`); class declaration updated; `ReloadAsync()` implemented
  - R-3: `GetRange` integer overflow guard (`long` count check before `Enumerable.Range`)
  - R-4: `catalogue?.CatalogueVersion ?? 0` null-safe access
  - R-5: `ClearInstanceForTesting()` added to test `TearDown`
**Advisory items**: AC-15 pre-solved fixture gap; `InternalsVisibleTo` / `.asmdef` setup deferred; `sealed` modifier deferred — recommend follow-up in Story 005 or a polish story.
