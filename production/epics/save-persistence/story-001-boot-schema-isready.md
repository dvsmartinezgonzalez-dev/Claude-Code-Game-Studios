# Story 001: SaveSystem Boot, Schema v1, and IsReady Contract

> **Epic**: Save & Persistence
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 1.5 days
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-22

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirements**: `TR-SP-002`, `TR-SP-004`

| TR-ID | Requirement |
|-------|-------------|
| TR-SP-002 | Save schema fields: schema_version, current_level_id, completion_record[], coin_balance, undo_stack[] |
| TR-SP-004 | IsReady bool + OnSaveReady event; all consumers use subscribe-then-check pattern |

**ADR Governing Implementation (Primary)**: ADR-0001: Singleton Architecture and Boot Sequence
**ADR Decision Summary**: SaveSystem is a DontDestroyOnLoad singleton at SEO −90. `IsReady = true` is set synchronously inside `Awake()` before any lower-SEO system's `Start()` runs. All consumers use the subscribe-then-check pattern: subscribe to `OnSaveReady`, then immediately check `IsReady`; if already true, execute the callback directly.

**ADR Secondary Reference**: ADR-0003: Save System Design
ADR-0003 defines the schema v1 JSON structure, the `IFileSystem` injection seam, and cold-start read cases R-1 (valid) and R-3 (fresh install). Cases R-2 (migration), R-4 (corruption), and R-5 (downgrade) are handled in later stories but the dispatch routing established here must accommodate them.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (SEO ordering, DDOL singleton, Awaitable APIs)
**Engine Notes**: `destroyCancellationToken` is a Unity 6.0+ MonoBehaviour property — not present in Unity 2022.x. `Awaitable.BackgroundThreadAsync()` is Unity 6.0+ first-party. Confirm availability in Unity 6.3 editor before use. `Application.persistentDataPath` MUST be cached in `Awake()` — it may not be accessed from background threads.

**Control Manifest Rules (Foundation layer)**:
- Required: DDOL singleton guard as first statement in `Awake()`; `IsReady = true` set synchronously before any `await`; subscribe-then-check mandatory for all `OnSaveReady` consumers; `event Action OnSaveReady` (not UnityEvent, not ScriptableObject channel)
- Forbidden: `async void Awake()` — compile error guard; `[SerializeField]` on properties or event fields; `FindObjectsOfType` without sort mode
- Guardrail: SaveSystem cold-start read < 2ms for < 22 KB save file (monitor in Editor with Profiler)

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-01** — Codebase search for direct `save.json` file reads outside `SaveSystem` returns zero results
- [ ] **AC-13** — `save.json` with `schema_version = 1` and valid JSON: all fields load into memory, `IsReady = true` (Case R-1)
- [ ] **AC-15** — No `save.json` present: `current_level_id = 1`, `completion_record[] = []`, `coin_balance = 0`, `IsReady = true`, no file written (Case R-3)
- [ ] **AC-17** — `schema_version = 99` (> `MAX_KNOWN_VERSION`): file not overwritten, defaults loaded, analytics warning emitted, one-time notice shown (if `sp.downgrade_notice_shown = 0`), `IsReady = true` (Case R-5)
- [ ] **AC-19** — DEBUG build: any read method called before `IsReady` throws `InvalidOperationException`; RELEASE build: stalls with 2-second timeout, returns nullable result — never silent `0` or `false`
- [ ] **AC-27** — `coin_balance = -50` in file: clamped to 0 at load, anomaly logged to analytics, all other fields intact
- [ ] **AC-32** — `completion_record[] = []` (empty array): loads without error, `IsReady = true`
- [ ] **AC-33** — `save.tmp` present when `save.json` is valid: `save.tmp` deleted silently before `IsReady = true`

---

## Implementation Notes

*Derived from ADR-0001 and ADR-0003:*

**Singleton guard — mandatory first statement in `Awake()`:**
```csharp
void Awake() {
    if (instance != null && instance != this) { Destroy(gameObject); return; }
    instance = this;
    DontDestroyOnLoad(gameObject);
    // initialization continues...
}
```

**SEO registration**: SaveSystem must be registered in Project Settings → Script Execution Order at −90 before this story can be verified. Without SEO registration, `Awake()` order is non-deterministic.

**Schema v1 data classes** (C# classes, not structs — IL2CPP requires classes for Newtonsoft.Json deserialization per ADR-0004; apply same reasoning here):
- `SaveData`: root object with `schemaVersion`, `levelProgress`, `economy`, `skins`
- `LevelProgress`: `currentLevelId`, `completionRecord[]`, `undoStack[]`
- `CompletionRecord`: `levelId`, `bestStars`, `completionVersion`
- `Economy`: `coinBalance`

**`IFileSystem` injection seam** — MUST be created in this story as a shared dependency for stories 002–005:
```csharp
public interface IFileSystem {
    bool FileExists(string path);
    string ReadAllText(string path);
    void WriteAllBytes(string path, byte[] data);
    void Replace(string src, string dst, string backup);
    void Move(string src, string dst);
    void Delete(string path);
}
```
Production implementation wraps `System.IO`. Test implementation allows fault injection. Inject via constructor parameter or `[Inject]` pattern — not `FindObjectOfType`.

**Cold-start dispatch order** (GDD C.5):
1. Cache `Application.persistentDataPath` (main thread only — cache, never re-read on background threads)
2. Check `save.tmp` existence (record result for R-4/cleanup)
3. Read and parse `save.json`
4. Dispatch to R-1/R-2/R-3/R-4/R-5 based on file existence + parse success + `schema_version`
5. Handle `save.tmp` cleanup as part of dispatch (not as a separate post-dispatch step)
6. Set `IsReady = true`
7. Fire `OnSaveReady?.Invoke()`

**Case R-5 dispatch** (schema_version > MAX_KNOWN_VERSION): `MAX_KNOWN_VERSION = 1`. If file's schema version exceeds this constant, do not migrate — fall back to defaults, emit analytics warning, show one-time notice via `PlayerPrefs.GetInt("sp.downgrade_notice_shown", 0)` guard.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: `WriteCompletionAtomic()`, W-1 background write, `SemaphoreSlim`, `File.Replace`/`File.Move` I/O
- Story 003: `OnApplicationPause` W-2 write path, dirty flag
- Story 004: R-4 corruption recovery, iOS `UnauthorizedAccessException` retry loop
- Story 005: R-2 migration dispatch, `migrate_v0_to_v1`, migration write-back
- Story 006: `SetCoinBalance()`, PlayerPrefs helpers, backup exclusion attributes

---

## QA Test Cases

*Embedded from `production/qa/qa-plan-sprint3-2026-05-22.md`.*

- **AC-13 / Boot_R1_ValidV1File_LoadsAllFields**
  - Given: `save.json` with `schema_version = 1`, `current_level_id = 5`, `coin_balance = 150`, 2 completion records
  - When: `SaveSystem.Awake()` executes
  - Then: in-memory fields match file values exactly, `IsReady = true`
  - Edge cases: absent optional fields default per C.2; `undo_stack` absent = empty list (not error)

- **AC-15 / Boot_R3_NoFile_DefaultsAndNoWrite**
  - Given: no `save.json` at `persistentDataPath`
  - When: `SaveSystem.Awake()` executes
  - Then: `GetCurrentLevelId() = 1`, `completion_record[] = []`, `GetCoinBalance() = 0`, `IsReady = true`, no file written
  - Edge cases: directory missing does not crash; `persistentDataPath` itself may not exist on first run

- **AC-19 / Boot_IsReady_Debug_ThrowsBeforeReady**
  - Given: DEBUG build, `SaveSystem` not yet initialized
  - When: any read method called before `IsReady = true`
  - Then: `InvalidOperationException` thrown immediately
  - Edge cases: RELEASE build: stalls 2s, returns nullable — no silent `false`/`0` default

- **AC-27 / Boot_NegativeCoinBalance_ClampedToZero**
  - Given: `save.json` with `coin_balance = -50`
  - When: cold start executes
  - Then: in-memory `coin_balance = 0`, anomaly logged, all other fields intact

- **AC-32 / Boot_EmptyCompletionRecord_NotAnError**
  - Given: `save.json` with `schema_version = 1`, `completion_record = []`
  - When: cold start executes
  - Then: `IsReady = true`, no error, `current_level_id` loads correctly

- **AC-33 / Boot_TmpPresent_ValidJson_TmpDeleted**
  - Given: both `save.json` valid and `save.tmp` exist
  - When: cold start executes
  - Then: `save.json` loaded normally, `save.tmp` deleted silently before `IsReady = true`

- **Boot_SingletonGuard_SecondAwakeDestroyseDuplicate**
  - Given: `SaveSystem` already initialized
  - When: second `Awake()` fires on a duplicate GameObject
  - Then: duplicate destroyed, cold-start logic NOT re-run, `IsReady` state unchanged

- **Boot_OnSaveReady_SubscribeAfterReady_StillFires**
  - Given: `SaveSystem.IsReady = true` already
  - When: a system subscribes to `OnSaveReady`
  - Then: subscriber callback fires immediately (subscribe-then-check pattern)

- **Boot_AsyncVoidAwake_Forbidden**
  - When: `SaveSystem.Awake()` method inspected via reflection
  - Then: method is `void Awake()`, not `async void Awake()`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/save-persistence/SaveSystem_Boot_Test.cs` — must exist and all tests pass

**Status**: [x] Created and passing — `My project/Assets/_Project/Tests/unit/save-persistence/SaveSystem_Boot_Test.cs` (18 tests, all pass — confirmed 2026-05-22)
**Completed**: 2026-05-22
**Code Review**: Complete — `/code-review` run this session; 5 required changes applied (GuardIsReady Thread.Sleep removed, async on WriteCompletionAtomic, PlayerPrefs TearDown, singleton assertion, null sub-object test); re-review verdict APPROVED

---

## Dependencies

- Depends on: None — this is the foundation story; implement first
- Unlocks: Story 002, Story 003, Story 004, Story 005, Story 006 (all depend on boot + IFileSystem seam)

---

## Completion Notes

**Completed**: 2026-05-22
**Criteria**: 8/8 passing
**Deviations**:
- ADVISORY: `HandleDowngrade` (R-5) writes `PlayerPrefs.SetInt` without `PlayerPrefs.Save()` — deferred to Story 006 per story scope; Story 006 adds the flush call
- ADVISORY: `JsonUtility.FromJson` null check at line 194 is dead code for corrupt-but-parseable JSON — functionally safe (loads defaults); comment clarified
**Test Evidence**: Logic — `My project/Assets/_Project/Tests/unit/save-persistence/SaveSystem_Boot_Test.cs` (18 tests)
**Code Review**: Complete — APPROVED after 5 required changes applied
