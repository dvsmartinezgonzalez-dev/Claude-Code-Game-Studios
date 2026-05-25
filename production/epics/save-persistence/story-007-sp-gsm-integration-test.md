# Story 007: SP ↔ GSM Integration Test — Board Persistence Round-Trip

> **Epic**: Save & Persistence
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 0.5 days
> **Manifest Version**: 2026-05-12
> **Last Updated**: 2026-05-23
> **Sprint Story**: S4-03

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirements**: `TR-SP-008`, `TR-GSM-011`

| TR-ID | Requirement |
|-------|-------------|
| TR-SP-008 | W-1 write off main thread via Awaitable.BackgroundThreadAsync; W-2 (app pause) synchronous on main thread |
| TR-GSM-011 | App background serialization: GSM serializes board state snapshot to SP on OnApplicationPause; deserializes + increments seqId on foreground restore; emits session_load_failed(SAVE_CORRUPT) on deserialization failure |

**ADR Governing Implementation**: ADR-0003: Save System Design
**ADR Decision Summary**: W-2 (`OnApplicationPause`) is a synchronous write-then-swap via `_writeLock.Wait()`. Cold-start read (R-1) reads the same file path on the next boot. Together they form the round-trip contract that GSM relies on for board state persistence across app backgrounding. This story verifies that contract end-to-end using `FakeFileSystem` injection.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `HandleApplicationPause` is synchronous and fully testable in EditMode. `Awaitable.BackgroundThreadAsync` (W-1) is NOT used in this story — the W-1 + W-2 concurrent race (S4-04) requires PlayMode and is out of scope here. All file I/O goes through `FakeFileSystem`.

**Control Manifest Rules (Foundation layer)**:
- Required: atomic write-then-swap (FileStream + Flush + Replace/Move); catch both `IOException` AND `UnauthorizedAccessException`
- Forbidden: real file I/O in tests; `async void OnApplicationPause()`
- Guardrail: cold-start read < 2ms for < 22KB save file (not enforced in FakeFileSystem tests — no real I/O)

---

## Acceptance Criteria

*Integration test verifying the W-2 → cold-start round-trip for all SaveData fields. All tests are EditMode + FakeFileSystem — no real file I/O.*

- [ ] **AC-INT-01** — `completion_record` round-trip: call `ApplyCompletionToMemory(levelId=7, bestStars=3, version="1.0", newCurrentLevelId=8)`, trigger W-2 via `HandleApplicationPause(true, ...)`, re-initialize SaveSystem from the bytes W-2 wrote, verify `GetCompletionRecord(7)` returns a record with `level_id=7`, `best_stars=3`, `completion_version="1.0"`
- [ ] **AC-INT-02** — `undo_stack` round-trip (bolt-count invariant): push 3 moves via `PushUndoMove`, trigger W-2, re-initialize SaveSystem from written bytes, verify `GetUndoStack().Count == 3` and each `(f, t)` entry matches original — entries before pause equal entries after reload
- [ ] **AC-INT-03** — `economy.coin_balance` round-trip: call `SetCoinBalance(500)`, trigger W-2, re-initialize, verify `GetCoinBalance() == 500`
- [ ] **AC-INT-04** — `current_level_id` round-trip: call `ApplyCompletionToMemory` with `newCurrentLevelId=12`, trigger W-2, re-initialize, verify `GetCurrentLevelId() == 12`
- [ ] **AC-INT-05** — Determinism: all phases use `FakeFileSystem` — no calls to real filesystem; `FakeFileSystem.WrittenFiles` captures W-2 output; second `FakeFileSystem` is pre-seeded with those bytes for cold-start read
- [ ] **AC-INT-06** — W-2 no-dirty is a no-op: after W-2 clears `_isDirty`, a second `HandleApplicationPause(true, ...)` call must NOT produce additional write ops (`FakeFileSystem.WrittenFiles.Count` must not increase)

---

## Implementation Notes

*Derived from ADR-0003 and the existing `SaveSystem_Pause_Test.cs` pattern:*

**Round-trip test pattern**:
```csharp
// Phase 1: write via W-2
var writeFs = new FakeFileSystem { ReadAllTextResult = ValidV1Json };
SS.SetFileSystemForTesting(writeFs);
var go1 = new GameObject("SS_Roundtrip_Phase1");
var ss1 = go1.AddComponent<SS>();        // Awake → R-1 read (ValidV1Json)
ss1.SetCoinBalance(500);                 // mutate in-memory
ss1.HandleApplicationPause(true, CancellationToken.None);  // W-2 write

// Phase 2: cold-start read from W-2 output
byte[] writtenBytes = writeFs.WrittenFiles[0].Bytes;
var readFs = new FakeFileSystem();
readFs.ReadAllTextResult = Encoding.UTF8.GetString(writtenBytes);
SS.ClearInstanceForTesting();
SS.SetFileSystemForTesting(readFs);
var go2 = new GameObject("SS_Roundtrip_Phase2");
var ss2 = go2.AddComponent<SS>();        // Awake → R-1 read (writtenBytes decoded)
Assert.AreEqual(500, ss2.GetCoinBalance());
```

**`WrittenFiles[0].Bytes`**: W-2 calls `WriteAndFlush(_tmpPath, bytes)` first. The bytes are the serialized save.json content. `FakeFileSystem` stores these; they are the correct bytes to use for the Phase 2 cold-start seed regardless of whether W-2 used Move or Replace.

**Bolt-count invariant**: `GetUndoStack().Count` before pause must equal `GetUndoStack().Count` after reload. Each undo entry corresponds to one bolt move — this is the integration-level check that GSM's board state serialization round-trips correctly through the SP layer.

**`ApplyCompletionToMemory` is `internal`**: accessible from the test assembly. Use it to set up in-memory state without triggering an async W-1 write — the W-2 path reads from in-memory state via `CaptureSnapshot()`.

**Namespace for new test file**: `BoltSort.Tests.Integration.SaveSystem` (matches the existing `SaveSystem_Pause_Test.cs` in the same directory).

**Assembly**: `Tests.Integration.SaveSystem` (existing `.asmdef` at `My project/Assets/_Project/Tests/integration/save-persistence/Tests.Integration.SaveSystem.asmdef`).

---

## Out of Scope

- W-1 (background `Awaitable`) concurrent race with W-2 — that is S4-04 (TD-SP-006, PlayMode only)
- GSM board state serialization (TR-GSM-011 full implementation) — GSM is not yet implemented; this story tests the SP layer using SP's own internal seams
- Real file I/O — all operations go through `FakeFileSystem`
- PlayMode test infrastructure — EditMode only

---

## QA Test Cases

- **AC-INT-01 / CompletionRecord_RoundTrip_PreservesAllFields**
  - Given: SaveSystem at R-1 with valid V1 JSON; `ApplyCompletionToMemory(7, 3, "1.0", 8)` called
  - When: `HandleApplicationPause(true, ...)` fires; new SaveSystem initialised from written bytes
  - Then: `GetCompletionRecord(7)` → `level_id=7`, `best_stars=3`, `completion_version="1.0"`

- **AC-INT-02 / UndoStack_RoundTrip_PreservesCountAndEntries** (bolt-count invariant)
  - Given: 3 undo moves pushed `(0→1), (1→2), (2→3)`; W-2 fired
  - When: SaveSystem re-initialised from written bytes
  - Then: `GetUndoStack().Count == 3`; entries match originals

- **AC-INT-03 / CoinBalance_RoundTrip_PreservesValue**
  - Given: `SetCoinBalance(500)`; W-2 fired
  - When: SaveSystem re-initialised
  - Then: `GetCoinBalance() == 500`

- **AC-INT-04 / CurrentLevelId_RoundTrip_PreservesValue**
  - Given: `ApplyCompletionToMemory(levelId=5, ..., newCurrentLevelId=12)`; W-2 fired
  - When: SaveSystem re-initialised
  - Then: `GetCurrentLevelId() == 12`

- **AC-INT-05 / Determinism_AllIoThroughFakeFileSystem**
  - When: all phases execute
  - Then: no real filesystem paths accessed; `writeFs.WrittenFiles.Count > 0` after W-2

- **AC-INT-06 / W2_NoDirty_SecondPauseIsNoOp**
  - Given: W-2 fired and `_isDirty` cleared; second pause triggered
  - When: `HandleApplicationPause(true, ...)` called again
  - Then: `writeFs.WrittenFiles.Count` does not increase

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `My project/Assets/_Project/Tests/integration/save-persistence/SaveSystem_BoardPersistence_Integration_Test.cs` — must exist and all tests pass

**Status**: [x] Created — `My project/Assets/_Project/Tests/integration/save-persistence/SaveSystem_BoardPersistence_Integration_Test.cs` (6 tests)

---

## Dependencies

- Depends on: Story 003 (W-2 write path — `HandleApplicationPause` seam) — **Complete** ✓
- Depends on: Story 001 (IsReady contract, cold-start R-1 read) — **Complete** ✓
- Unlocks: S4-04 (TD-SP-006 PlayMode W-1+W-2 race test)
