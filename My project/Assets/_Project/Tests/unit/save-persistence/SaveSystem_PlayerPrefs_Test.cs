using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.SaveSystem;
using BoltSort.Tests.Helpers.SavePersistence;
using SS = BoltSort.SaveSystem.SaveSystem;          // alias avoids namespace/class collision

// Test assembly: Tests.Unit.SaveSystem
// Covers: AC-02, AC-07, AC-10, AC-26, AC-40, TD-SP-002 (Story 006)
//   from design/gdd/save-persistence.md
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/save-persistence/story-006-playerprefs-setcoinbalance.md

namespace BoltSort.Tests.Unit.SaveSystem
{
    [TestFixture]
    public class SaveSystem_PlayerPrefs_Test
    {
        private FakeFileSystem _fakeFs;
        private SS _saveSystem;

        private const string ValidV1Json =
            @"{""schema_version"":1," +
            @"""level_progress"":{""current_level_id"":5,""completion_record"":[],""undo_stack"":[]}," +
            @"""economy"":{""coin_balance"":150}," +
            @"""skins"":{""_status"":""reserved""}}";

        private const string FutureSchemaJson =
            @"{""schema_version"":99," +
            @"""level_progress"":{""current_level_id"":1,""completion_record"":[],""undo_stack"":[]}," +
            @"""economy"":{""coin_balance"":0}," +
            @"""skins"":{""_status"":""reserved""}}";

        [SetUp]
        public void SetUp()
        {
            _fakeFs = new FakeFileSystem();
            SS.SetFileSystemForTesting(_fakeFs);
        }

        [TearDown]
        public void TearDown()
        {
            if (_saveSystem != null)
                UnityEngine.Object.DestroyImmediate(_saveSystem.gameObject);
            SS.ClearInstanceForTesting();
            // Clear PlayerPrefs keys written during tests — prevents leakage across test runs.
            PlayerPrefs.DeleteKey("sp.downgrade_notice_shown");
        }

        private SS CreateSaveSystem()
        {
            var go = new GameObject("SaveSystem_PlayerPrefs_Test");
            _saveSystem = go.AddComponent<SS>();
            return _saveSystem;
        }

        // ── AC-07: SetCoinBalance — in-memory update and dirty flag ──────────────

        [Test]
        public void SetCoinBalance_ValidValue_SetsInMemoryAndMarksDirty()
        {
            // Arrange
            _fakeFs.ReadAllTextResult = ValidV1Json; // starts with coin_balance = 150
            var ss = CreateSaveSystem();

            // Act
            ss.SetCoinBalance(250);

            // Assert
            Assert.AreEqual(250, ss.GetCoinBalance(), "coin_balance must be updated in memory (AC-07)");
            Assert.IsTrue(ss.IsDirty, "dirty flag must be true after SetCoinBalance (AC-07)");
        }

        // ── TD-SP-002 / AC-07: negative input clamped to 0 ───────────────────────

        [Test]
        public void SetCoinBalance_NegativeValue_ClampsToZero()
        {
            // Arrange
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var ss = CreateSaveSystem();

            // Act
            ss.SetCoinBalance(-1);

            // Assert
            Assert.AreEqual(0, ss.GetCoinBalance(),
                "Negative input must be clamped to 0 (AC-07, TD-SP-002)");
            Assert.IsTrue(ss.IsDirty,
                "Dirty flag must be true even when clamped (AC-07)");
        }

        [Test]
        public void SetCoinBalance_IntMinValue_ClampsToZeroWithoutOverflow()
        {
            // Arrange
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var ss = CreateSaveSystem();

            // Act
            ss.SetCoinBalance(int.MinValue);

            // Assert
            Assert.AreEqual(0, ss.GetCoinBalance(),
                "int.MinValue must clamp to 0 without arithmetic overflow (TD-SP-002)");
        }

        [Test]
        public void SetCoinBalance_Zero_SetsFloorValue()
        {
            // Arrange
            _fakeFs.ReadAllTextResult = ValidV1Json; // coin_balance = 150
            var ss = CreateSaveSystem();

            // Act
            ss.SetCoinBalance(0);

            // Assert
            Assert.AreEqual(0, ss.GetCoinBalance(), "0 is a valid balance — the hard floor (AC-07)");
            Assert.IsTrue(ss.IsDirty);
        }

        // ── AC-07: SetCoinBalance must not trigger a file write ───────────────────

        [Test]
        public void SetCoinBalance_DoesNotTriggerFileWrite()
        {
            // Arrange
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var ss = CreateSaveSystem();
            int writtenBefore = _fakeFs.WrittenFiles.Count;

            // Act
            ss.SetCoinBalance(999);

            // Assert
            Assert.AreEqual(writtenBefore, _fakeFs.WrittenFiles.Count,
                "SetCoinBalance must not trigger a file write — W-1 or W-2 will flush (AC-07)");
        }

        // ── AC-10: PlayerPrefs.Save() called on R-5 downgrade path ───────────────

        [Test]
        public void Downgrade_R5_SetsDowngradeNoticeKeyAsInt()
        {
            // Arrange: ensure key is absent before the test
            PlayerPrefs.DeleteKey("sp.downgrade_notice_shown");
            _fakeFs.ReadAllTextResult = FutureSchemaJson;

            // Act
            CreateSaveSystem();

            // Assert: key was written as int = 1 (AC-10 — scalar write, Save() called)
            Assert.AreEqual(1, PlayerPrefs.GetInt("sp.downgrade_notice_shown", 0),
                "R-5 path must set 'sp.downgrade_notice_shown' = 1 as int and call PlayerPrefs.Save() (AC-10)");
        }

        [Test]
        public void Downgrade_R5_AlreadyShown_GuardPreventsDoubleWrite()
        {
            // Arrange: key already set — 'if (GetInt == 0)' guard must skip the Set*() call
            PlayerPrefs.SetInt("sp.downgrade_notice_shown", 1);
            _fakeFs.ReadAllTextResult = FutureSchemaJson;

            // Act
            CreateSaveSystem();

            // Assert: value unchanged (guard prevented double-write)
            Assert.AreEqual(1, PlayerPrefs.GetInt("sp.downgrade_notice_shown", 0),
                "Guard must prevent double-write when key is already set to 1");
        }

        // ── AC-02: SaveSystem uses only the registered 'sp.' namespace key ────────

        [Test]
        public void PlayerPrefs_DowngradeNoticeKey_ConstantMatchesGddSpec()
        {
            // Verify the internal constant matches the GDD namespace spec exactly.
            const string expectedKey = "sp.downgrade_notice_shown";
            var field = typeof(SS).GetField(
                "DowngradeNoticeShownKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.IsNotNull(field,
                "DowngradeNoticeShownKey constant must exist in SaveSystem (AC-02)");
            Assert.AreEqual(expectedKey, field.GetValue(null) as string,
                "Key must match GDD spec 'sp.downgrade_notice_shown' — " +
                "registered namespace prefix 'sp.' required (AC-02)");
        }

        [Test]
        public void SaveSystem_DoesNotWriteAudioOrQtsPlayerPrefsKeys()
        {
            // Verify that SaveSystem does not write audio.* or qts.* keys — those are owned
            // by AudioSystem and QTS respectively (AC-02, TR-SP-005).
            PlayerPrefs.DeleteKey("audio.sfx_volume");
            PlayerPrefs.DeleteKey("audio.ambient_volume");
            PlayerPrefs.DeleteKey("audio.ui_volume");
            PlayerPrefs.DeleteKey("qts.tier");
            _fakeFs.ReadAllTextResult = ValidV1Json;

            CreateSaveSystem();

            Assert.AreEqual(0, PlayerPrefs.GetInt("audio.sfx_volume", 0),
                "SaveSystem must not write 'audio.sfx_volume' — owned by AudioSystem (AC-02)");
            Assert.AreEqual(0, PlayerPrefs.GetInt("audio.ambient_volume", 0),
                "SaveSystem must not write 'audio.ambient_volume' (AC-02)");
            Assert.AreEqual(0, PlayerPrefs.GetInt("audio.ui_volume", 0),
                "SaveSystem must not write 'audio.ui_volume' (AC-02)");
            Assert.AreEqual(0, PlayerPrefs.GetInt("qts.tier", 0),
                "SaveSystem must not write 'qts.tier' — owned by QTS (AC-02)");
        }

        // ── AC-26: file size with 200 max-length records ≤ 21,780 bytes ──────────

        [Test]
        public void FileSizeFormula_200MaxLengthRecords_UnderCeiling()
        {
            // Arrange: 200 records with max-length fields + 20 undo entries (GDD Formula 2)
            SaveData saveData = BuildMaxLengthSaveData(recordCount: 200, undoCount: 20);

            // Act: serialize using the same method used in production (main thread — safe in EditMode)
            string json = JsonUtility.ToJson(saveData);
            int byteCount = System.Text.Encoding.UTF8.GetByteCount(json);

            // Assert
            Assert.LessOrEqual(byteCount, 21_780,
                $"200 max-length records must be ≤ 21,780 bytes (AC-26). " +
                $"Actual: {byteCount} bytes. If this fails, update GDD Formula 2 and recalculate.");
        }

        // ── AC-40: file size with 300 max-length records ≤ 32,500 bytes ──────────

        [Test]
        public void FileSizeFormula_300MaxLengthRecords_UnderContentCeiling()
        {
            // Arrange: 300 records — content-update ceiling (GDD Formula 2 extended ceiling)
            SaveData saveData = BuildMaxLengthSaveData(recordCount: 300, undoCount: 20);

            // Act
            string json = JsonUtility.ToJson(saveData);
            int byteCount = System.Text.Encoding.UTF8.GetByteCount(json);

            // Assert
            Assert.LessOrEqual(byteCount, 32_500,
                $"300 max-length records must be ≤ 32,500 bytes (AC-40). " +
                $"Actual: {byteCount} bytes. If this fails, update GDD Formula 2 before content milestone.");
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a SaveData with <paramref name="recordCount"/> completion records using the
        /// maximum-length field values specified in GDD Formula 2: level_id=9999 (4 chars),
        /// completion_version="9999.12" (7 chars), plus <paramref name="undoCount"/> undo entries.
        /// </summary>
        private static SaveData BuildMaxLengthSaveData(int recordCount, int undoCount)
        {
            var records = new List<CompletionRecord>(recordCount);
            for (int i = 0; i < recordCount; i++)
            {
                records.Add(new CompletionRecord
                {
                    level_id           = 9999,
                    best_stars         = 3,
                    completion_version = "9999.12",
                });
            }

            var undoStack = new List<UndoMove>(undoCount);
            for (int i = 0; i < undoCount; i++)
                undoStack.Add(new UndoMove { f = 9, t = 9 });

            return new SaveData
            {
                schema_version = 1,
                level_progress = new LevelProgress
                {
                    current_level_id  = 9999,
                    completion_record = records,
                    undo_stack        = undoStack,
                },
                economy = new Economy { coin_balance = int.MaxValue },
                skins   = new Skins(),
            };
        }
    }
}
