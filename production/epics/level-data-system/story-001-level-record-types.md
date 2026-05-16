# Story 001: LevelRecord, LevelCatalogue, SystemReadiness Types

> **Epic**: Level Data System
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-05-12

## Context

**GDD**: `design/gdd/level-data-system.md`
**Requirement**: `TR-LDS-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Level Data Loading Strategy (revised 2026-05-12)
**ADR Decision Summary**: All level data is deserialized using Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`). `LevelRecord` uses `[JsonObject(MemberSerialization.OptIn)]` with `[JsonProperty("snake_case_name")]` attributes, PascalCase C# properties with private setters, and nullable `int?` for `HintOverride`. `LevelCatalogue` is the root deserialization target containing `catalogue_version` and a `LevelRecord[]` array. `SystemReadiness` is a readonly struct returned by `GetReadiness()`.

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: `[JsonProperty]` attribute mapping and nullable `int?` deserialization must be verified on IL2CPP build (iOS). `[JsonObject(MemberSerialization.OptIn)]` ensures only attributed members are deserialized — unattributed fields return defaults silently.

**Control Manifest Rules (Foundation layer)**:
- Required: Newtonsoft.Json for LevelRecord deserialization; `[JsonObject(MemberSerialization.OptIn)]` + `[JsonProperty("snake_case")]` on all fields
- Required: All serialized types must use `MemberSerialization.OptIn` — never `OptOut`
- Forbidden: `JsonUtility` for LevelRecord — cannot handle nullable `int?`, private setters, or attribute-based name mapping

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-system.md`, scoped to this story:*

- [ ] `LevelRecord` sealed class implements all GDD schema fields with `[JsonProperty("snake_case_name")]` and private setters
- [ ] `HintOverride` is `int?` (nullable) — deserializes JSON `null` or absent key as C# `null`; deserializes `0` as C# `int? = 0` (distinct from null)
- [ ] `ColorStacks` is `int[][]` — nested array deserializes correctly from JSON nested array
- [ ] `DisplayName` returns `"Level {LevelId}"` as a default when the JSON field is absent or `null` (post-deserialization defaulting)
- [ ] `LevelCatalogue` sealed class has `CatalogueVersion` (int, defaults to 0 if absent) and `Levels` (LevelRecord[])
- [ ] `SystemReadiness` readonly struct has fields: `Ready` (bool), `LoadedCount` (int), `SkippedCount` (int), `CatalogueVersion` (int), `DiagnosticCode` (string, null when Ready)
- [ ] `LdsState` enum has values: `Uninitialized`, `Loading`, `Ready`, `Degraded`
- [ ] `LdsErrorCode` enum has values: `NotFound`, `ValidationFailed`, `VersionMismatch`, `SystemNotReady`
- [ ] `LevelFilter` class supports filter criteria matching as documented in ADR-0004 `Matches(LevelRecord)` method

---

## Implementation Notes

*Derived from ADR-0004 Implementation Guidelines:*

```csharp
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
    [JsonProperty("hint_override")]          public int? HintOverride { get; private set; }
    [JsonProperty("added_version")]          public string AddedVersion { get; private set; }
    [JsonProperty("par_moves")]              public int ParMoves { get; private set; }
}
```

`DisplayName` defaulting: after deserialization, if `DisplayName` is null or empty, set it to `$"Level {LevelId}"`. Do this in a `[OnDeserialized]` callback or in the validator before returning records to callers.

All types live in `Assets/Scripts/LevelData/` (or equivalent namespace `BoltSort.LevelData`).

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 002: Stage 2 validation logic using these types
- Story 003: Addressables loading and state machine using these types

---

## Estimate

Small — 3–4 hours

---

## QA Test Cases

- **AC-types-1**: `HintOverride` nullable round-trip
  - Given: JSON string `{ ..., "hint_override": null }` and JSON string `{ ..., "hint_override": 0 }` and JSON string `{ ... }` (key absent)
  - When: `JsonConvert.DeserializeObject<LevelRecord>(json)` called for each
  - Then: null → `HintOverride == null`; `0` → `HintOverride == 0` (not null); absent → `HintOverride == null`
  - Edge cases: `"hint_override": -1` → `HintOverride == -1`

- **AC-types-2**: `ColorStacks` jagged array
  - Given: JSON with `"color_stacks": [[1,2,3],[2,1,3],[3,2,1]]`
  - When: `JsonConvert.DeserializeObject<LevelRecord>(json)`
  - Then: `ColorStacks[0]` = `[1,2,3]`, `ColorStacks[2]` = `[3,2,1]`
  - Edge cases: empty inner array `[[],[1,2,3]]` → `ColorStacks[0].Length == 0`

- **AC-types-3**: `DisplayName` defaulting
  - Given: JSON with `display_name` absent; separate test with `"display_name": null`
  - When: Record returned from system
  - Then: `DisplayName == "Level 42"` (where LevelId = 42)
  - Edge cases: `"display_name": ""` is a validation failure (Story 002) — empty string must NOT be defaulted here

- **AC-types-4**: `CatalogueVersion` defaults to 0 when absent
  - Given: JSON root `{ "levels": [...] }` with no `catalogue_version` key
  - When: `JsonConvert.DeserializeObject<LevelCatalogue>(json)`
  - Then: `CatalogueVersion == 0`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/level-data-system/level_record_types_test.cs` — must exist and pass

**Status**: [x] Exists — `tests/unit/level-data-system/LevelDataSystem_LevelRecordTypes_Test.cs` (29 tests)

---

## Dependencies

- Depends on: None — this is the data type foundation
- Unlocks: Story 002 (validation uses these types), Story 003 (load pipeline uses these types)

---

## Completion Notes
**Completed**: 2026-05-13
**Criteria**: 9/9 passing (all covered by automated tests)
**Deviations**: None
**Test Evidence**: Logic — `tests/unit/level-data-system/LevelDataSystem_LevelRecordTypes_Test.cs` (24 NUnit tests)
**Code Review**: APPROVED — unity-specialist second pass (2026-05-13)
**IL2CPP Mitigations**: `Assets/link.xml` (preserve Newtonsoft.Json + LevelRecord/LevelCatalogue), `[Preserve]` on `[OnDeserialized]`, `_aotHint` static field for `int[][]` AOT. On-device IL2CPP verification required before iOS submission (ADR-0004 §Verification Required).
