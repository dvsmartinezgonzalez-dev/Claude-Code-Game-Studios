using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using BoltSort.Tests.Helpers.SavePersistence;
using BoltSort.SaveSystem;
using SS = BoltSort.SaveSystem.SaveSystem;

// Test assembly: Tests.Unit.SaveSystem
// Covers: AC-14, AC-18, AC-22, AC-29, AC-31, AC-34 (Story 005)
//   from design/gdd/save-persistence.md
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/save-persistence/story-005-schema-migration.md

namespace BoltSort.Tests.Unit.SaveSystem
{
    [TestFixture]
    public class SaveSystem_Migration_Test
    {
        private FakeFileSystem _fakeFs;
        private SS _saveSystem;

        // v0 flat-schema JSON: current_level_id, completion_record, coin_balance at root.
        // No nested level_progress or economy. schema_version = 0 (or absent).
        private const string V0JsonWithVersion =
            @"{""schema_version"":0," +
            @"""current_level_id"":7," +
            @"""coin_balance"":42," +
            @"""completion_record"":[" +
            @"{""level_id"":1,""best_stars"":3,""completion_version"":""2026.04""}," +
            @"{""level_id"":2,""best_stars"":1,""completion_version"":""""}" +
            @"]}";

        // AC-18: valid JSON with NO schema_version key — JsonUtility deserializes missing int as 0.
        private const string V0JsonWithoutVersion =
            @"{""current_level_id"":5," +
            @"""coin_balance"":20," +
            @"""completion_record"":[" +
            @"{""level_id"":3,""best_stars"":2,""completion_version"":""""}" +
            @"]}";

        // Simple v0 JSON with no completion records.
        private const string V0JsonNoCompletions =
            @"{""schema_version"":0," +
            @"""current_level_id"":3," +
            @"""coin_balance"":10," +
            @"""completion_record"":[]}";

        // Paths must match what SaveSystem computes in Awake().
        private string SavePath => Application.persistentDataPath + "/save.json";
        private string TmpPath  => Application.persistentDataPath + "/save.tmp";

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
            PlayerPrefs.DeleteKey("sp.downgrade_notice_shown");
        }

        /// <summary>Creates and returns the SaveSystem MonoBehaviour (triggers Awake immediately).</summary>
        private SS CreateSaveSystem()
        {
            var go = new GameObject("SaveSystem_Migration_Test");
            _saveSystem = go.AddComponent<SS>();
            return _saveSystem;
        }

        // ── AC-31: MaxKnownVersion constant matches GDD ───────────────────────────

        /// <summary>
        /// AC-31: SaveSystem.MaxKnownVersion must equal 1. Acts as a CI lint — when a new
        /// schema version is introduced, this test forces both the constant and the GDD to
        /// be updated together.
        /// </summary>
        [Test]
        public void Migration_MaxKnownVersion_MatchesGdd()
        {
            Assert.AreEqual(1, SS.MaxKnownVersion,
                "MaxKnownVersion must equal the value declared in GDD Formulas (currently 1). " +
                "Update both the GDD and the constant when adding a new schema version.");
        }

        // ── AC-14: v0 file → migrate_v0_to_v1 runs ───────────────────────────────

        /// <summary>
        /// AC-14: A file with schema_version = 0 triggers MigrateV0ToV1.
        /// Result must have nested level_progress + economy structure, and schema_version = 1
        /// in the written file. In-memory values must match the v0 source data.
        /// </summary>
        [Test]
        public void Migration_V0_RunsMigrateToV1_NestedStructureAndVersion1Written()
        {
            // Arrange: v0 flat schema.
            _fakeFs.SetReadResultByPath(SavePath, V0JsonWithVersion);

            // Act
            var ss = CreateSaveSystem();

            // Assert: IsReady and in-memory values.
            Assert.IsTrue(ss.IsReady, "IsReady must be true after v0 migration (AC-14)");
            Assert.AreEqual(7, ss.GetCurrentLevelId(),
                "current_level_id must be read from v0 flat field and nested into level_progress");
            Assert.AreEqual(42, ss.GetCoinBalance(),
                "coin_balance must be read from v0 flat field and nested into economy");

            // Assert: write-back occurred (migrated file written to disk).
            Assert.IsTrue(_fakeFs.WrittenFiles.Count > 0,
                "Migrated state must be written back to save.json synchronously within Awake (AC-14)");

            // Assert: written content contains schema_version = 1.
            var (_, writtenBytes) = _fakeFs.WrittenFiles[0];
            string writtenJson = System.Text.Encoding.UTF8.GetString(writtenBytes);
            Assert.IsTrue(writtenJson.Contains("\"schema_version\":1"),
                "Written file must contain schema_version = 1 after migration (AC-14)");

            // Assert: written content contains nested level_progress.
            Assert.IsTrue(writtenJson.Contains("\"level_progress\""),
                "Written file must contain nested level_progress structure (AC-14)");

            // Assert: written content contains nested economy.
            Assert.IsTrue(writtenJson.Contains("\"economy\""),
                "Written file must contain nested economy structure (AC-14)");
        }

        // ── AC-18: absent schema_version key treated as v0 ───────────────────────

        /// <summary>
        /// AC-18: Valid JSON with no schema_version key must be treated as v0 (not R-1 skip).
        /// JsonUtility deserializes a missing int field as 0, so schema_version = 0 triggers
        /// migration just as explicitly-set v0 does.
        /// </summary>
        [Test]
        public void Migration_AbsentSchemaVersion_TreatedAsV0_MigrationRuns()
        {
            // Arrange: JSON with no schema_version key.
            _fakeFs.SetReadResultByPath(SavePath, V0JsonWithoutVersion);

            // Act
            var ss = CreateSaveSystem();

            // Assert: migration ran — values from the flat v0 JSON are present.
            Assert.IsTrue(ss.IsReady, "IsReady must be true when absent schema_version treated as v0 (AC-18)");
            Assert.AreEqual(5, ss.GetCurrentLevelId(),
                "current_level_id from flat v0 JSON (absent schema_version treated as 0)");
            Assert.AreEqual(20, ss.GetCoinBalance(),
                "coin_balance from flat v0 JSON");

            // Assert: write-back occurred — migration ran (NOT the R-1 no-migration path).
            Assert.IsTrue(_fakeFs.WrittenFiles.Count > 0,
                "Migration write-back must have occurred — absent schema_version is v0, not v1 (AC-18)");
        }

        // ── undo_stack initialised to empty on v0 migration ──────────────────────

        /// <summary>
        /// v0 had no undo_stack field. After migration, the in-memory state must have an
        /// empty undo stack — not null or absent (which would NullReferenceException on access).
        /// </summary>
        [Test]
        public void Migration_V0_UndoStackInitializedEmpty()
        {
            // Arrange: v0 JSON with no undo_stack field.
            _fakeFs.SetReadResultByPath(SavePath, V0JsonNoCompletions);

            // Act
            var ss = CreateSaveSystem();

            // Assert: undo stack is present and empty (not null, not missing).
            var stack = ss.GetUndoStack();
            Assert.IsNotNull(stack, "undo_stack must be non-null after v0 migration");
            Assert.AreEqual(0, stack.Count,
                "undo_stack must be empty after v0 migration — v0 had no undo history");
        }

        // ── AC-22 (migration aspect): completion_version not synthesised ──────────

        /// <summary>
        /// AC-22 (migration aspect): completion_version on v0 completion records must NOT be
        /// synthesised or mutated by MigrateV0ToV1. Write-once contract requires migrators to
        /// preserve the existing value (or leave the field empty if it was absent in v0).
        /// </summary>
        [Test]
        public void Migration_V0_CompletionVersionNotSynthesized()
        {
            // Arrange: v0 JSON with two records:
            //   - level 1: completion_version = "2026.04" (already set — must be preserved)
            //   - level 2: completion_version = "" (absent sentinel — must remain empty, not synthesised)
            _fakeFs.SetReadResultByPath(SavePath, V0JsonWithVersion);

            // Act
            var ss = CreateSaveSystem();

            // Assert: level 1 record has completion_version preserved.
            var rec1 = ss.GetCompletionRecord(1);
            Assert.IsNotNull(rec1, "Completion record for level 1 must exist after migration");
            Assert.AreEqual("2026.04", rec1.Value.completion_version,
                "completion_version '2026.04' must be preserved through v0→v1 migration (AC-22 write-once)");

            // Assert: level 2 record has completion_version left as empty (not synthesised).
            var rec2 = ss.GetCompletionRecord(2);
            Assert.IsNotNull(rec2, "Completion record for level 2 must exist after migration");
            Assert.IsTrue(string.IsNullOrEmpty(rec2.Value.completion_version),
                "completion_version must NOT be synthesised for records where it was absent in v0 (AC-22 write-once)");
        }

        // ── AC-34: MigrateV0ToV1 is idempotent ───────────────────────────────────

        /// <summary>
        /// AC-34: Running MigrateV0ToV1 twice on the same in-memory state must produce
        /// identical output field-for-field. The method is internal so this test calls it
        /// directly on a fully booted SaveSystem instance.
        /// </summary>
        [Test]
        public void Migration_Idempotency_RunTwiceProducesSameResult()
        {
            // Arrange: boot with a v0 file so _fakeFs and instance are initialised.
            // We then call MigrateV0ToV1 directly (internal visibility) to test idempotency
            // without going through the full PerformColdStartRead path a second time.
            _fakeFs.SetReadResultByPath(SavePath, V0JsonWithVersion);
            var ss = CreateSaveSystem();

            // First run: migrate v0 JSON → result A.
            SaveData resultA = ss.MigrateV0ToV1(V0JsonWithVersion);

            // Serialize result A back to JSON, then re-migrate (simulates running twice).
            string jsonA = JsonUtility.ToJson(resultA);
            SaveData resultB = ss.MigrateV0ToV1(jsonA);

            // Assert field-for-field equality (AC-34).
            Assert.AreEqual(resultA.schema_version, resultB.schema_version,
                "Idempotency (AC-34): schema_version must be identical on second migration run");
            Assert.AreEqual(resultA.level_progress.current_level_id,
                            resultB.level_progress.current_level_id,
                "Idempotency (AC-34): current_level_id must be identical");
            Assert.AreEqual(resultA.economy.coin_balance,
                            resultB.economy.coin_balance,
                "Idempotency (AC-34): coin_balance must be identical");
            Assert.AreEqual(resultA.level_progress.completion_record.Count,
                            resultB.level_progress.completion_record.Count,
                "Idempotency (AC-34): completion_record count must be identical");

            for (int i = 0; i < resultA.level_progress.completion_record.Count; i++)
            {
                var recA = resultA.level_progress.completion_record[i];
                var recB = resultB.level_progress.completion_record[i];
                Assert.AreEqual(recA.level_id, recB.level_id,
                    $"Idempotency (AC-34): completion_record[{i}].level_id must match");
                Assert.AreEqual(recA.best_stars, recB.best_stars,
                    $"Idempotency (AC-34): completion_record[{i}].best_stars must match");
                Assert.AreEqual(recA.completion_version, recB.completion_version,
                    $"Idempotency (AC-34): completion_record[{i}].completion_version must match");
            }

            Assert.IsNotNull(resultB.level_progress.undo_stack,
                "Idempotency (AC-34): undo_stack must be non-null on second migration run");
            Assert.AreEqual(resultA.level_progress.undo_stack.Count,
                            resultB.level_progress.undo_stack.Count,
                "Idempotency (AC-34): undo_stack count must be identical");
        }

        // ── AC-29: write-back IOException → in-memory state retained, dirty=true ──

        /// <summary>
        /// AC-29: When the migration write-back throws IOException, the in-memory migrated
        /// state must be retained (not reverted to v0 or defaults). IsReady must be true.
        /// The dirty flag is set so W-1/W-2 will retry on the next opportunity.
        /// We verify retention by asserting IsReady = true and that in-memory values match
        /// the migrated v0 source data (not defaults), which proves RunMigrations returned
        /// the migrated SaveData regardless of write-back outcome.
        /// </summary>
        [Test]
        public void Migration_WriteBackFails_InMemoryStateRetained_IsReadyTrue()
        {
            // Arrange: v0 file with known values; write will throw IOException.
            _fakeFs.SetReadResultByPath(SavePath, V0JsonWithVersion);
            _fakeFs.WriteAndFlushException = new IOException("disk full — AC-29 test");

            // Act
            var ss = CreateSaveSystem();

            // Assert: IsReady is true despite write-back failure (AC-29).
            Assert.IsTrue(ss.IsReady,
                "IsReady must be true even when migration write-back throws IOException (AC-29)");

            // Assert: in-memory migrated state is retained (not reverted to defaults).
            Assert.AreEqual(7, ss.GetCurrentLevelId(),
                "In-memory current_level_id must match migrated v0 value (not defaults=1) after write-back failure (AC-29)");
            Assert.AreEqual(42, ss.GetCoinBalance(),
                "In-memory coin_balance must match migrated v0 value (not defaults=0) after write-back failure (AC-29)");

            // Assert: completion records are retained.
            var rec1 = ss.GetCompletionRecord(1);
            Assert.IsNotNull(rec1,
                "Completion record for level 1 must be retained in memory after write-back failure (AC-29)");

            // Assert: dirty flag is true — W-1/W-2 must retry the write on next opportunity (AC-29).
            Assert.IsTrue(ss.IsDirty,
                "_isDirty must be true after migration write-back IOException so W-1/W-2 retries (AC-29)");
        }

        // ── Migration write-back is synchronous (no await) ────────────────────────

        /// <summary>
        /// Verifies that the RunMigrations write-back does not use async/await.
        /// Uses reflection to check that neither RunMigrations nor WriteSaveJsonSyncCore
        /// is an async state machine method — async void is forbidden in Awake context.
        /// </summary>
        [Test]
        public void Migration_WriteBack_SynchronousInAwake_NoAsyncStateMachine()
        {
            var bindingFlags = System.Reflection.BindingFlags.Instance |
                               System.Reflection.BindingFlags.NonPublic |
                               System.Reflection.BindingFlags.Public;

            // RunMigrations must be a plain void method.
            var runMigrations = typeof(SS).GetMethod("RunMigrations", bindingFlags);
            Assert.IsNotNull(runMigrations, "RunMigrations method must exist");
            var runMigrationsAttrs = runMigrations.GetCustomAttributes(
                typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false);
            Assert.AreEqual(0, runMigrationsAttrs.Length,
                "RunMigrations must NOT be async — migration write-back must be synchronous within Awake()");

            // WriteSaveJsonSyncCore must be a plain void method.
            var writeSyncCore = typeof(SS).GetMethod("WriteSaveJsonSyncCore", bindingFlags);
            Assert.IsNotNull(writeSyncCore, "WriteSaveJsonSyncCore method must exist");
            var writeSyncCoreAttrs = writeSyncCore.GetCustomAttributes(
                typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false);
            Assert.AreEqual(0, writeSyncCoreAttrs.Length,
                "WriteSaveJsonSyncCore must NOT be async");
        }
    }
}
