# Epic: Level Data System

> **Layer**: Foundation
> **GDD**: design/gdd/level-data-system.md
> **Architecture Module**: LevelDataSystem
> **Status**: Ready
> **Manifest Version**: 2026-05-12
> **Stories**: 6 stories created (2026-05-12)

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [LevelRecord, LevelCatalogue, SystemReadiness Types](story-001-level-record-types.md) | Logic | Ready | ADR-0004 |
| 002 | [Stage 2 Runtime Validation](story-002-stage2-validation.md) | Logic | Ready | ADR-0004 |
| 003 | [InitializeAsync() — Load Pipeline and State Machine](story-003-init-async-state-machine.md) | Integration | Ready | ADR-0001, ADR-0004 |
| 004 | [Query Methods — GetLevel, GetRange, GetByFilter, GetReadiness](story-004-getter-methods.md) | Logic | Ready | ADR-0004 |
| 005 | [ReloadAsync() — Hot-Swap Catalogue](story-005-reload-async.md) | Logic | Ready | ADR-0004 |
| 006 | [Authoring Pipeline Validator](story-006-authoring-pipeline-validator.md) | Logic | Ready | ADR-0004, ADR-0013 |

## Overview

The Level Data System defines the serializable data format for all BoltSort levels and loads validated level records into memory at app start via Unity Addressables. It owns the `LevelRecord` cache (a `Dictionary<int, LevelRecord>` populated from JSON `TextAsset` files in the `LevelData` Addressables group), exposes synchronous `GetLevel(int levelId)` to the Game State Manager, and signals readiness via `IsReady` + `OnLevelDataReady`. It runs at Script Execution Order −95, starts an async batch load in `Awake`, and enters a DEGRADED state if more than 20% of level records fail to parse or the catalogue is empty. LevelProgression uses a dual-ready guard — waiting for both this system and SaveSystem — before calling `GSM.LoadLevel()`. Players experience the LDS only as instant, seamless level transitions; it is pure infrastructure.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Singleton Architecture and Boot Sequence | LevelDataSystem is a DDOL singleton at SEO −95; subscribe-then-check pattern for `OnLevelDataReady` | HIGH |
| ADR-0004: Level Data Loading Strategy | `Addressables.LoadAssetsAsync<TextAsset>(key, callback, true)` — 3-arg form; `AsyncOperationHandle` stored in field; `LevelRecord`/`ColorStack` as `class` (not struct) for IL2CPP; handles released after parsing | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-LDS-001 | Level record schema: levelId, colorCount, stackDepth, colorStacks[], tempSlotCount, tempSlotDepth, parMoves (camelCase JSON fields matching C# field names) | ADR-0004 ✅ |
| TR-LDS-002 | `bolt_count_invariant` validated at authoring time (pipeline `LevelRecordValidator`) and at runtime `GetLevel()` (throws `LevelDataException` on failure) | ADR-0004 ✅ |
| TR-LDS-003 | System readiness pattern: `IsReady` bool + `OnLevelDataReady` event; callers use subscribe-then-check | ADR-0001, ADR-0004 ✅ |
| TR-LDS-004 | DEGRADED state when `failure_ratio > 0.20` (strict greater-than) or `total_record_count == 0`; `IsReady` still fires (no boot hang); `IsDegrade` and `DegradedErrorCode` exposed | ADR-0004 ✅ |

## Key Implementation Notes

- `LevelRecord` and `ColorStack` must be **class**, not struct — `JsonUtility` does not reliably deserialize nested arrays in structs on IL2CPP builds
- `JsonUtility` requires **camelCase** field names (exact match, no attribute remapping)
- `AsyncOperationHandle<IList<TextAsset>> _loadHandle` must be stored as a field — not stored = GC-eligible mid-load, silent abort
- `Addressables.Release(_loadHandle)` must be called after parsing — only retain typed `LevelRecord` dictionary
- `GetLevel()` throws `InvalidOperationException` if called before `IsReady`; throws `LevelDataException` if level not found or bolt count invariant fails
- LevelProgression dual-ready guard: subscribe to BOTH `OnSaveReady` and `OnLevelDataReady`; call `GSM.LoadLevel()` only when both flags are true AND `IsDegrade == false`
- Column cap (`color_count + temp_slot_count ≤ 8`) is enforced at authoring time by `LevelRecordValidator` editor script — governed by ADR-0013

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/level-data-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/unit/level-data-system/` or `tests/integration/`
- `JsonUtility` round-trip test passes on IL2CPP build (including `colorStacks[i].colors` length verification)
- `GetLevel()` bolt count invariant test passes
- LDS cold-start load time < 500ms on Samsung Galaxy A14 (device test with 50 levels)

## Next Step

Run `/create-stories level-data-system` to break this epic into implementable stories.
