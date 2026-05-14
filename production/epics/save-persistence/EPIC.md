# Epic: Save & Persistence

> **Layer**: Foundation
> **GDD**: design/gdd/save-persistence.md
> **Architecture Module**: SaveSystem
> **Status**: Ready
> **Manifest Version**: 2026-05-12
> **Stories**: Not yet created — run `/create-stories save-persistence`

## Overview

Save & Persistence is BoltSort's data layer: it serializes, stores, and retrieves all player-facing state that must survive between sessions. It owns `save.json` on disk (`Application.persistentDataPath/save.json`) and the `PlayerPrefs` audio preference keys. It defines the atomic write primitive (`WriteCompletionAtomic`) that Level Progression requires to advance `current_level_id` and `best_stars[N]` as a single operation, the schema version contract governing all future migrations, and the iOS cold-start file protection retry strategy. The system runs at Script Execution Order −90 and reads synchronously in `Awake` so that `IsReady = true` is set before any lower-SEO system's `Awake` runs. All consumer systems (CoinEconomy at SEO −40, LevelProgression at SEO −30) use the subscribe-then-check pattern to handle the already-fired `OnSaveReady` event.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Singleton Architecture and Boot Sequence | SaveSystem at SEO −90; synchronous read in Awake; subscribe-then-check mandatory for all OnSaveReady consumers | HIGH |
| ADR-0002: Event and Signal Architecture | `event Action OnSaveReady` declared on SaveSystem; subscribe-then-check pattern; DDOL-to-DDOL subscriptions exempt from OnDestroy unsubscribe | LOW |
| ADR-0003: Save System Design | Atomic write-then-swap (FileStream + Flush(flushToDisk:true) + File.Replace); W-1 via Awaitable.BackgroundThreadAsync; W-2 synchronous; iOS catch UnauthorizedAccessException; schema versioning | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-SP-001 | JSON file; atomic write via FileStream + `Flush(flushToDisk:true)` + `File.Replace`/`File.Move` (write-then-swap) | ADR-0003 ✅ |
| TR-SP-002 | Save schema v1: `schema_version`, `current_level_id`, `completion_record[]`, `coin_balance`, `undo_stack[]` | ADR-0003 ✅ |
| TR-SP-003 | `WriteCompletionAtomic(levelId, bestStars, version, newCurrentLevelId)`: capture snapshot on main thread → `Awaitable.BackgroundThreadAsync()` → `_writeLock.WaitAsync()` → file I/O → release | ADR-0003 ✅ |
| TR-SP-004 | `IsReady` bool + `event Action OnSaveReady`; all consumers use subscribe-then-check | ADR-0001, ADR-0003 ✅ |
| TR-SP-005 | PlayerPrefs for audio prefs (`audio.sfx_volume`, `audio.ambient_volume`, `audio.ui_volume`); SaveSystem does not mediate PlayerPrefs writes | ADR-0003 ✅ |
| TR-SP-006 | Integer `schema_version`; sequential migrators (`migrate_v0_to_v1`, etc.); migrators run synchronously on R-2; `completion_version` is write-once (migrators must not set it on empty records) | ADR-0003 ✅ |
| TR-SP-007 | iOS cold-start file protection: catch `UnauthorizedAccessException` (sibling to `IOException` — catch both separately); 250ms retry; 5-second timeout; thread joined before `IsReady = true` | ADR-0003 ✅ |
| TR-SP-008 | W-1 write off main thread via `Awaitable.BackgroundThreadAsync`; W-2 (`OnApplicationPause`) synchronous on main thread; no `async void` on either | ADR-0003 ✅ |

## Key Implementation Notes

- **Never** use `File.Move(source, dest, overwrite: true)` (3-arg overload) — does not exist in .NET Standard 2.1 (Unity 6.3 BCL), compile error. Use `File.Replace(tmp, save, null)` when save exists; `File.Move(tmp, save)` for first write.
- **Never** use `async void Awake()` — Unity does not await Awake; `IsReady = true` must be set before any other system's `Start()` runs.
- **Never** use `async void OnApplicationPause()` — Unity returns control to OS at first `await`; W-2 must be synchronous.
- `catch(IOException)` does NOT catch `UnauthorizedAccessException` — they are sibling .NET types. Always catch both.
- iOS retry thread MUST be joined (`thread.Join()`) before `IsReady = true` — emitting `OnSaveReady` before the retry completes delivers stale defaults.
- `SetCoinBalance(int)` MUST set `_isDirty = true` inside SP — otherwise W-2 skips the write.
- `PlayerPrefs.Save()` called after every `PlayerPrefs.Set*()` — Android OOM kill bypasses `OnApplicationQuit`.
- `save.json` must be excluded from iCloud and Android Auto-Backup (iOS: `NSURLIsExcludedFromBackupKey`; Android: `<cloud-backup-rules>` XML).
- `JsonUtility` serializes null strings as `""` — treat `""` as the absent sentinel for `completion_version`, not `null`.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/save-persistence.md` are verified
- Unit tests pass: write-then-swap produces valid `save.json` with `schema_version: 1`; `WriteCompletionAtomic` is idempotent; R-4 (corrupt JSON) recovers to defaults; R-2 (v0 schema) migrates correctly
- Integration test: `SaveSystem.Awake()` completes before `CoinEconomy.Awake()` — `IsReady == true` when CE subscribes
- Device test (iOS): cold-start after hard reboot + passcode lock starts without crash; `IsReady` set within 5 seconds
- Device test (Android, Galaxy A14): W-1 completes without I/O error; `save.json` valid after write

## Next Step

Run `/create-stories save-persistence` to break this epic into implementable stories.
