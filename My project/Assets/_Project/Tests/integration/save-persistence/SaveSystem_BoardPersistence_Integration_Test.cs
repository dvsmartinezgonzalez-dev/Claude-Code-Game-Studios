using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using BoltSort.SaveSystem;
using BoltSort.Tests.Helpers.SavePersistence;
using SS = BoltSort.SaveSystem.SaveSystem;

// Test assembly: Tests.Integration.SaveSystem
// Covers: AC-INT-01 through AC-INT-06 — W-2 → cold-start round-trip contract
//   TR-SP-008: W-2 (app pause) synchronous on main thread
//   TR-GSM-011: App background serialization board state snapshot round-trip
// Framework: Unity Test Framework (NUnit, EditMode — no real file I/O)
// ADR: ADR-0003 (Save System Design — atomic write-then-swap, W-2 synchronous path)
// Story: production/epics/save-persistence/story-007-sp-gsm-integration-test.md

namespace BoltSort.Tests.Integration.SaveSystem
{
    [TestFixture]
    public class SaveSystem_BoardPersistence_Integration_Test
    {
        // ValidV1Json is a clean baseline save used as Phase 1 cold-start seed.
        // current_level_id=1, empty records, coin_balance=0.
        private const string ValidV1Json =
            @"{""schema_version"":1," +
            @"""level_progress"":{""current_level_id"":1,""completion_record"":[],""undo_stack"":[]}," +
            @"""economy"":{""coin_balance"":0}," +
            @"""skins"":{""_status"":""reserved""}}";

        // Phase 1 objects — the write-side of each round-trip test.
        private FakeFileSystem _writeFs;
        private SS _ss1;
        private GameObject _go1;

        // Phase 2 objects — the cold-start read-side. Null until explicitly constructed per test.
        private FakeFileSystem _readFs;
        private SS _ss2;
        private GameObject _go2;

        [SetUp]
        public void SetUp()
        {
            _writeFs = new FakeFileSystem { ReadAllTextResult = ValidV1Json };
            SS.SetFileSystemForTesting(_writeFs);
            _go1 = new GameObject("SS_BoardPersistence_Phase1");
            _ss1 = _go1.AddComponent<SS>(); // Awake → R-1 read (ValidV1Json)
        }

        [TearDown]
        public void TearDown()
        {
            if (_go2 != null)
                Object.DestroyImmediate(_go2);
            if (_go1 != null)
                Object.DestroyImmediate(_go1);
            SS.ClearInstanceForTesting();
        }

        // ── Round-trip helper ─────────────────────────────────────────────────────

        /// <summary>
        /// Triggers W-2, then boots a fresh SaveSystem from the bytes W-2 wrote.
        /// Returns the Phase 2 SaveSystem ready for assertions.
        /// </summary>
        private SS Phase2FromW2Write()
        {
            _ss1.HandleApplicationPause(true, CancellationToken.None); // W-2 write

            byte[] writtenBytes = _writeFs.WrittenFiles[0].Bytes;
            _readFs = new FakeFileSystem
            {
                ReadAllTextResult = Encoding.UTF8.GetString(writtenBytes)
            };
            SS.ClearInstanceForTesting();
            SS.SetFileSystemForTesting(_readFs);
            _go2 = new GameObject("SS_BoardPersistence_Phase2");
            _ss2 = _go2.AddComponent<SS>(); // Awake → R-1 cold-start read from W-2 bytes
            return _ss2;
        }

        // ── AC-INT-01: completion_record round-trip ───────────────────────────────

        [Test]
        public void CompletionRecord_RoundTrip_PreservesAllFields()
        {
            // Arrange
            _ss1.ApplyCompletionToMemory(7, 3, "1.0", 8);

            // Act
            SS ss2 = Phase2FromW2Write();

            // Assert
            CompletionRecord? record = ss2.GetCompletionRecord(7);
            Assert.IsTrue(record.HasValue, "AC-INT-01: completion record for level 7 must exist after round-trip");
            Assert.AreEqual(7,     record.Value.level_id,           "AC-INT-01: level_id must round-trip");
            Assert.AreEqual(3,     record.Value.best_stars,         "AC-INT-01: best_stars must round-trip");
            Assert.AreEqual("1.0", record.Value.completion_version, "AC-INT-01: completion_version must round-trip");
        }

        // ── AC-INT-02: undo_stack round-trip (bolt-count invariant) ───────────────

        [Test]
        public void UndoStack_RoundTrip_PreservesCountAndEntries()
        {
            // Arrange — push 3 moves
            _ss1.PushUndoMove(0, 1);
            _ss1.PushUndoMove(1, 2);
            _ss1.PushUndoMove(2, 3);

            int stackCountBeforePause = _ss1.GetUndoStack().Count;

            // Act
            SS ss2 = Phase2FromW2Write();

            // Assert — bolt-count invariant: count before pause equals count after reload
            Assert.AreEqual(3, stackCountBeforePause,
                "Setup: 3 moves must be present before W-2");
            Assert.AreEqual(stackCountBeforePause, ss2.GetUndoStack().Count,
                "AC-INT-02: undo stack count must survive round-trip (bolt-count invariant)");

            var stack = ss2.GetUndoStack();
            Assert.AreEqual(0, stack[0].f, "AC-INT-02: entry 0 from-index must match");
            Assert.AreEqual(1, stack[0].t, "AC-INT-02: entry 0 to-index must match");
            Assert.AreEqual(1, stack[1].f, "AC-INT-02: entry 1 from-index must match");
            Assert.AreEqual(2, stack[1].t, "AC-INT-02: entry 1 to-index must match");
            Assert.AreEqual(2, stack[2].f, "AC-INT-02: entry 2 from-index must match");
            Assert.AreEqual(3, stack[2].t, "AC-INT-02: entry 2 to-index must match");
        }

        // ── AC-INT-03: economy.coin_balance round-trip ────────────────────────────

        [Test]
        public void CoinBalance_RoundTrip_PreservesValue()
        {
            // Arrange
            _ss1.SetCoinBalance(500);

            // Act
            SS ss2 = Phase2FromW2Write();

            // Assert
            Assert.AreEqual(500, ss2.GetCoinBalance(),
                "AC-INT-03: coin_balance=500 must survive W-2 → cold-start round-trip");
        }

        // ── AC-INT-04: current_level_id round-trip ────────────────────────────────

        [Test]
        public void CurrentLevelId_RoundTrip_PreservesValue()
        {
            // Arrange
            _ss1.ApplyCompletionToMemory(5, 1, "1.0", 12);

            // Act
            SS ss2 = Phase2FromW2Write();

            // Assert
            Assert.AreEqual(12, ss2.GetCurrentLevelId(),
                "AC-INT-04: current_level_id=12 must survive W-2 → cold-start round-trip");
        }

        // ── AC-INT-05: determinism — all I/O through FakeFileSystem ───────────────

        [Test]
        public void Determinism_AllIoThroughFakeFileSystem()
        {
            // Arrange
            _ss1.SetCoinBalance(250);

            // Act
            _ss1.HandleApplicationPause(true, CancellationToken.None); // W-2

            // Assert Phase 1 — W-2 wrote via FakeFileSystem
            Assert.Greater(_writeFs.WrittenFiles.Count, 0,
                "AC-INT-05: FakeFileSystem must capture W-2 output (WrittenFiles must be non-empty)");

            // Assert Phase 2 — cold-start read via a second FakeFileSystem (no real I/O)
            byte[] writtenBytes = _writeFs.WrittenFiles[0].Bytes;
            Assert.Greater(writtenBytes.Length, 0,
                "AC-INT-05: written bytes must be non-empty");

            _readFs = new FakeFileSystem
            {
                ReadAllTextResult = Encoding.UTF8.GetString(writtenBytes)
            };
            SS.ClearInstanceForTesting();
            SS.SetFileSystemForTesting(_readFs);
            _go2 = new GameObject("SS_BoardPersistence_Determinism_Phase2");
            _ss2 = _go2.AddComponent<SS>();

            // The Phase 2 FakeFileSystem must have been read exactly once (R-1 cold-start)
            Assert.AreEqual(1, _readFs.ReadCallCount,
                "AC-INT-05: Phase 2 cold-start must call ReadAllText exactly once");

            // No writes during cold-start read
            Assert.AreEqual(0, _readFs.WrittenFiles.Count,
                "AC-INT-05: no write ops may occur during cold-start read");
        }

        // ── AC-INT-06: W-2 no-dirty is a no-op ───────────────────────────────────

        [Test]
        public void W2_NoDirty_SecondPauseIsNoOp()
        {
            // Arrange — make dirty and fire W-2 to clear the dirty flag
            _ss1.PushUndoMove(0, 1);
            _ss1.HandleApplicationPause(true, CancellationToken.None); // W-2 → clears _isDirty

            int writeCountAfterFirst = _writeFs.WrittenFiles.Count
                + _writeFs.MoveCalls.Count
                + _writeFs.ReplaceCalls.Count;
            Assert.Greater(writeCountAfterFirst, 0, "Setup: first pause must produce at least one write op");

            // Act — second pause with _isDirty = false
            _ss1.HandleApplicationPause(true, CancellationToken.None);

            int writeCountAfterSecond = _writeFs.WrittenFiles.Count
                + _writeFs.MoveCalls.Count
                + _writeFs.ReplaceCalls.Count;

            // Assert
            Assert.AreEqual(writeCountAfterFirst, writeCountAfterSecond,
                "AC-INT-06: second W-2 with _isDirty=false must not produce additional write ops");
        }
    }
}
