# Story 006: PlayerPrefs Namespace, SetCoinBalance, and Backup Exclusion

> **Epic**: Save & Persistence
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 0.5 days
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-23

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirements**: `TR-SP-005`

| TR-ID | Requirement |
|-------|-------------|
| TR-SP-005 | PlayerPrefs stores audio preferences; SaveSystem does not mediate PlayerPrefs writes; owning systems write directly |

**ADR Governing Implementation**: ADR-0003: Save System Design
**ADR Decision Summary**: PlayerPrefs is reserved for scalar settings only (`audio.*`, `qts.*`, `sp.*` namespaces). SaveSystem owns the namespace declaration and the `sp.*` internal keys. It does NOT mediate `audio.*` or `qts.*` writes — AudioSystem and QTS write those directly. `PlayerPrefs.Save()` must be called explicitly after every `PlayerPrefs.Set*()` call; `OnApplicationQuit` is not guaranteed on Android OOM kill.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `NSURLIsExcludedFromBackupKey` must be set on the `save.json` file path immediately after first file creation (iOS only). Android backup exclusion is handled via `<cloud-backup-rules>` XML in the manifest — authored in a build configuration step, not in runtime code. Both are required before Beta build.

**Control Manifest Rules (Foundation layer)**:
- Required: `PlayerPrefs.Save()` called immediately after every `PlayerPrefs.Set*()`; no JSON strings or arrays in PlayerPrefs values; keys use registered namespace prefix
- Forbidden: storing structured data (JSON strings, arrays) in PlayerPrefs; writing `audio.*` or `qts.*` keys from SaveSystem
- Guardrail: any new system adding PlayerPrefs keys must register a unique namespace prefix in GDD C.3 before implementation sprint

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-02** — All PlayerPrefs writes are scalar (float, int, or string); no key stores JSON content, arrays, or compound values
- [ ] **AC-10** — `PlayerPrefs.Save()` called in the same method as every `PlayerPrefs.Set*()` call, before returning
- [ ] **AC-26** *(Advisory)* — Serialized `save.json` with 200 max-length records ≤ 21,780 bytes; if exceeded, update Formula 2 in GDD and re-calculate ceiling. Evidence: measure in unit test
- [ ] **AC-38** *(Advisory)* — `NSURLIsExcludedFromBackupKey = true` applied to `save.json` path on iOS after first file creation. Evidence: `production/qa/evidence/ac-38-ios-backup-exclusion.md` (physical iOS device)
- [ ] **AC-40** *(Advisory)* — Serialized `save.json` with 300 max-length records ≤ 32,500 bytes (content-update ceiling). Evidence: measure in unit test
- [ ] **AC-07** *(BLOCKING)* — `SetCoinBalance(int value)` sets in-memory `economy.coin_balance = value`, clamps to `max(0, value)` (negative inputs become 0), marks dirty flag `true`, does NOT trigger a write directly; usable only after `IsReady = true`

---

## Implementation Notes

*Derived from ADR-0003 and GDD C.3:*

**PlayerPrefs namespace contract** (GDD C.3 — all keys SaveSystem writes or declares):

| Key | Type | Default | Written by |
|-----|------|---------|------------|
| `sp.downgrade_notice_shown` | int | 0 | SaveSystem (R-5 path) |
| `audio.sfx_volume` | float | 1.0 | AudioSystem (declared here, not written here) |
| `audio.ambient_volume` | float | 1.0 | AudioSystem |
| `audio.ui_volume` | float | 1.0 | AudioSystem |
| `qts.tier` | int | -1 | QTS (declared here, not written here) |

**SaveSystem writes exactly one PlayerPrefs key**: `sp.downgrade_notice_shown` (int, 0 or 1). All audio and QTS keys are declared by SaveSystem in the namespace table but written by their owning systems.

**`SetCoinBalance(int value)` interface method**:
```csharp
public void SetCoinBalance(int value) {
    // Called by CoinEconomy — not by Level Progression
    _saveData.Economy.CoinBalance = value;
    _isDirty = true;
    // No write triggered here — W-1 or W-2 will flush
}
```

**Performance**: `SetCoinBalance()` is a single field assignment + flag set — completes in < 0.01ms, no frame budget impact. PlayerPrefs reads in `Awake()` (SEO −90) are OS-cached; no save-file I/O. Cold-start read budget (< 2ms per ADR-0003) is unchanged by this story.

**Coin balance overflow guard** (AC-35 — implemented in Story 002 at mutation site): clamp to `int.MaxValue` before setting; dirty flag set `true`.

**`save.json` file size formula** (GDD Formula 2):
- `base_bytes ≈ 180`; `bytes_per_record ≈ 105` (unminified, max-length fields); `max_levels = 200`; `undo_stack_max_bytes = 600`
- Upper bound: `180 + (200 × 105) + 600 = 21,780 bytes ≈ 22 KB`
- Content ceiling: 300 levels → `180 + (300 × 105) + 600 = 32,280 bytes` — verify AC-40 before content milestone

**iOS backup exclusion** (OQ-04 — required before Beta, not blocking Alpha):
- After first successful `File.Move(tmp, save)` (first write), call the iOS native API to set `NSURLIsExcludedFromBackupKey = true` on the `save.json` path
- In Unity, this is done via `UnityEngine.iOS.Device` or a native plugin call
- Android: `<cloud-backup-rules>` XML excludes `save.json` and `save.tmp` — configure in `Assets/Plugins/Android/` before Beta

---

## Out of Scope

- Story 002: `WriteCompletionAtomic`, W-1 write path
- Story 001: `sp.downgrade_notice_shown` write (Story 001 implements R-5 path, which is the only place this key is written)

---

## QA Test Cases

*Embedded from `production/qa/qa-plan-sprint3-2026-05-22.md`.*

- **AC-02 / PlayerPrefs_AllWritesAreScalar**
  - When: all `PlayerPrefs.Set*()` calls in `SaveSystem.cs` enumerated (static analysis)
  - Then: every call is `SetFloat`, `SetInt`, or `SetString`; no JSON content in string values

- **AC-10 / PlayerPrefs_SaveCalledAfterEveryWrite**
  - When: each `PlayerPrefs.Set*()` call site inspected
  - Then: `PlayerPrefs.Save()` called in the same method before returning

- **PlayerPrefs_SpKey_DowngradeNoticeMatchSpec**
  - When: `sp.downgrade_notice_shown` key written
  - Then: key string matches exactly; written as `int` (0 or 1)

- **PlayerPrefs_SaveSystem_DoesNotMediateAudioOrQtsWrites**
  - When: `SaveSystem.cs` searched for `PlayerPrefs.Set*` with `audio.*` or `qts.*` keys
  - Then: zero results — those writes belong to AudioSystem and QTS respectively

- **PlayerPrefs_SetCoinBalance_SetsInMemoryAndMarksDirty**
  - Given: `SaveSystem.IsReady = true`
  - When: `SaveSystem.SetCoinBalance(250)` called
  - Then: in-memory `economy.coin_balance = 250`; dirty flag = `true`; no write triggered directly

- **TD-SP-002 / PlayerPrefs_SetCoinBalance_NegativeValue_ClampsToZero**
  - Given: `SaveSystem.IsReady = true`
  - When: `SaveSystem.SetCoinBalance(-1)` called
  - Then: in-memory `economy.coin_balance = 0` (not −1); dirty flag = `true`
  - Note: resolves TD-SP-002 (logged 2026-05-22); clamp must execute at the `SetCoinBalance` mutation site

- **AC-26 / FileSizeFormula_200Records_UnderCeiling** *(Advisory)*
  - Given: 200 completion records with max-length fields (`level_id=9999`, `completion_version="9999.12"`), 20 undo entries
  - When: serialized to unminified UTF-8 JSON
  - Then: file size ≤ 21,780 bytes; if exceeded, update GDD Formula 2

- **AC-40 / FileSizeFormula_300Records_UnderContentCeiling** *(Advisory)*
  - Given: 300 completion records with max-length fields, 20 undo entries
  - When: serialized
  - Then: file size ≤ 32,500 bytes

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/save-persistence/SaveSystem_PlayerPrefs_Test.cs` — must exist and all tests pass

**Advisory evidence** (physical iOS device, carry to Beta gate):
`production/qa/evidence/ac-38-ios-backup-exclusion.md`

**Status**: [x] Created — `My project/Assets/_Project/Tests/unit/save-persistence/SaveSystem_PlayerPrefs_Test.cs` (12 tests, all passing)

---

## Dependencies

- Depends on: Story 001 must be DONE (`IsReady` contract must exist before `SetCoinBalance` is callable)
- Unlocks: CoinEconomy epic (CE reads `OnSaveReady` and calls `SetCoinBalance` — cannot be implemented until this story is done)

---

## Completion Notes

**Completed**: 2026-05-23
**Criteria**: 5/6 passing (AC-38 deferred — physical iOS device required, carry to Beta gate)
**Deviations**:
- ADVISORY: `SetCoinBalance` has no `GuardIsReady()` — consistent with `PushUndoMove`; enforced by subscribe-then-check contract, not exception
- ADVISORY: AC-10 (`PlayerPrefs.Save()` called) not mechanically verifiable in Unity EditMode — confirmed by code reading and code review
- ADVISORY: `Math.Clamp(balance, 0, int.MaxValue)` upper bound is redundant; equivalent to `Math.Max(0, balance)`. No runtime risk.
- ADVISORY: TearDown does not clean `audio.*`/`qts.*` PlayerPrefs keys from test body — logged as TD-SP-008
**Test Evidence**: `My project/Assets/_Project/Tests/unit/save-persistence/SaveSystem_PlayerPrefs_Test.cs` (12 tests; AC-02, AC-07, AC-10, AC-26, AC-40, TD-SP-002)
**Code Review**: Complete — APPROVED WITH SUGGESTIONS (2026-05-23)
