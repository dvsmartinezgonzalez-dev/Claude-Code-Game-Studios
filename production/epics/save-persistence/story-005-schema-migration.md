# Story 005: Schema Version Migration Runner

> **Epic**: Save & Persistence
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 1.0 day
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-22

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirements**: `TR-SP-006`

| TR-ID | Requirement |
|-------|-------------|
| TR-SP-006 | Save migration versioning: integer schema_version field; sequential migrators applied on R-2 (version < current); completion_version write-once |

**ADR Governing Implementation**: ADR-0003: Save System Design
**ADR Decision Summary**: Files with `schema_version < MAX_KNOWN_VERSION` (Case R-2) have sequential migrators applied (`migrate_v0_to_v1`, `migrate_v1_to_v2`, etc.). Migration functions are idempotent, cumulative, and append-only. After migration completes, the migrated state is written back to disk synchronously within `Awake()` (no `await`). `completion_version` is write-once at the persistence layer — migrators must never synthesise a value for it.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Migration write-back must be synchronous `void` — no `await` inside `Awake()`. `JsonUtility` serializes null strings as `""` (empty string) — treat `""` as the absent sentinel for `completion_version`, not `null`. Newtonsoft.Json handles `null` correctly; if switching serializers, verify this behaviour.

**Control Manifest Rules (Foundation layer)**:
- Required: migration write-back is synchronous blocking within `Awake()`; `completion_version` never set by migrators on previously-empty records; each migrator is idempotent
- Forbidden: `await` in migration write-back; overwriting `completion_version` if already set
- Guardrail: `MAX_KNOWN_VERSION` constant in code must match GDD Formulas value (AC-31 CI lint assertion)

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-14** — `schema_version = 0` in file: `migrate_v0_to_v1` runs; result has nested `level_progress` + `economy` structure; `schema_version = 1` in written file
- [ ] **AC-18** — `schema_version` key absent in valid JSON: treated as v0; migration runs; NOT treated as R-1
- [ ] **AC-22** *(migration aspect)* — `completion_version` present on v0 completion records is NOT mutated by `migrate_v0_to_v1`; write-once contract respected
- [ ] **AC-29** — Post-migration write-back throws `IOException`: in-memory migrated state retained (not reverted); dirty flag `true`; failure logged with migrated-from schema version; `IsReady = true`; migration re-runs on next cold start
- [ ] **AC-31** — `SaveSystem.MaxKnownVersion` constant in code == 1 (matches GDD); implement as unit assertion: `Assert.AreEqual(1, SaveSystem.MaxKnownVersion)`
- [ ] **AC-34** — Running `migrate_v0_to_v1` twice on the same in-memory state produces identical output (idempotency)

---

## Implementation Notes

*Derived from ADR-0003 Implementation Guidelines:*

**Migration runner** (within `Awake()`, synchronous):
```csharp
private SaveData RunMigrations(SaveData data) {
    // Migrations apply in version order; each is a pure function
    if (data.schemaVersion < 1) {
        data = MigrateV0ToV1(data);
    }
    // Future: if (data.schemaVersion < 2) { data = MigrateV1ToV2(data); }

    // Write-back after all migrations — synchronous (no await)
    try {
        PerformSynchronousWrite(data);  // same write-then-swap as W-2
        _isDirty = false;
    } catch (IOException ex) {
        _isDirty = true;  // retry on next W-1 or W-2
        LogAnalytics("migration_write_failure", data.schemaVersion, ex);
    }
    return data;
}
```

**`migrate_v0_to_v1`** — v0 was a flat schema:
```
// v0: { "current_level_id": int, "completion_record": [...], "coin_balance": int }
// v1: nested under level_progress + economy
```
Migration must:
1. Restructure flat v0 fields into nested `levelProgress` + `economy` objects
2. Initialize `undoStack = []` (empty array — v0 has no undo history)
3. Do NOT set `completionVersion` on any existing record (write-once contract)
4. Set `schemaVersion = 1`
5. Be idempotent: running twice on v1 data returns identical v1 data unchanged

**`completion_version` write-once enforcement**: Before any field assignment in a migrator, check if the target record already has `completionVersion` set (non-null, non-empty). If set, skip. Never synthesise a value for an empty record.

**`MAX_KNOWN_VERSION` constant**: Declare as `public const int MaxKnownVersion = 1;` on `SaveSystem`. Write a unit test asserting `Assert.AreEqual(1, SaveSystem.MaxKnownVersion)` — this acts as a CI lint that forces the test to be updated when a new schema version is introduced.

**Case R-5 check** (defined in Story 001 dispatch but reiterated): if `schema_version > MaxKnownVersion`, do NOT run migration — it is a downgrade scenario (Case R-5), handled in Story 001.

---

## Out of Scope

- Story 001: R-5 downgrade detection (`schema_version > MAX_KNOWN_VERSION`)
- Story 004: R-4 corruption recovery, iOS retry

---

## QA Test Cases

*Embedded from `production/qa/qa-plan-sprint3-2026-05-22.md`.*

- **AC-14 / Migration_V0_RunsMigrateToV1**
  - Given: `save.json` with `schema_version = 0` (flat layout)
  - When: cold start executes
  - Then: `migrate_v0_to_v1` runs; result has nested `level_progress` + `economy`; `schema_version = 1`

- **AC-18 / Migration_AbsentSchemaVersion_TreatedAsV0**
  - Given: valid JSON with NO `schema_version` key
  - When: cold start executes
  - Then: treated as v0; migration runs; NOT R-1 (no migration)

- **Migration_V0_UndoStackInitialized**
  - Given: v0 `save.json` (no `undo_stack` field)
  - When: `migrate_v0_to_v1` runs
  - Then: resulting state has `undoStack = []` (empty, not absent)

- **Migration_V0_CompletionVersionNotSynthesized**
  - Given: v0 completion records with no `completionVersion` field
  - When: `migrate_v0_to_v1` runs
  - Then: migrated records leave `completionVersion` absent — NOT backfilled

- **AC-34 / Migration_Idempotency_RunTwiceProducesSameResult**
  - Given: in-memory v0 state
  - When: `migrate_v0_to_v1` runs once (result A), then again on result A (result B)
  - Then: A == B field-for-field

- **AC-29 / Migration_WriteBackFails_InMemoryStateRetained**
  - Given: post-migration write-back throws `IOException` (FakeFileSystem)
  - When: exception caught
  - Then: in-memory migrated state retained; dirty flag `true`; failure logged with migrated-from version; `IsReady = true`

- **AC-31 / Migration_MaxKnownVersion_MatchesGdd**
  - When: `SaveSystem.MaxKnownVersion` inspected
  - Then: value == 1 (matches GDD Formulas)

- **Migration_WriteBack_SynchronousInAwake**
  - Given: migration runs during `Awake()`
  - When: write-back executes
  - Then: no `await` expression in migration write-back code path (static analysis)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/save-persistence/SaveSystem_Migration_Test.cs` — must exist and all tests pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 must be DONE (cold-start dispatch routing, write-back helper, `IsReady` contract)
- Unlocks: None directly — parallel with Stories 002, 004, 006
