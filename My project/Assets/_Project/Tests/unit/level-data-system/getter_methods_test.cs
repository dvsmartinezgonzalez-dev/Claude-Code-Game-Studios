using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using BoltSort.LevelData;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.LevelData.asmdef
// Covers:    Story 004 ACs — GetLevel, GetRange, GetByFilter, GetReadiness

namespace BoltSort.Tests.Unit.LevelData
{
    [TestFixture]
    public class GetterMethods_Test
    {
        private GameObject _go;
        private LevelDataSystem _lds;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LDS_Test");
            _lds = _go.AddComponent<LevelDataSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
            LevelDataSystem.ClearInstanceForTesting();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static LevelRecord MakeRecord(int id, int difficultyTier = 3, bool daily = true,
            bool isTutorial = false, string displayName = null, int? hintOverride = null)
        {
            // Deserialise via Newtonsoft to exercise [OnDeserialized] display-name defaulting
            var json = $@"{{
                ""level_id"": {id},
                ""display_name"": {(displayName != null ? $"\"{displayName}\"" : "null")},
                ""difficulty_tier"": {difficultyTier},
                ""schema_version"": 1,
                ""color_count"": 2,
                ""stack_depth"": 2,
                ""color_stacks"": [[1,2],[2,1]],
                ""temp_slot_count"": 1,
                ""temp_slot_depth"": 1,
                ""is_tutorial"": {isTutorial.ToString().ToLower()},
                ""daily_challenge_eligible"": {daily.ToString().ToLower()},
                ""hint_override"": {(hintOverride.HasValue ? hintOverride.Value.ToString() : "null")},
                ""added_version"": ""2026.05"",
                ""par_moves"": 4
            }}";
            return JsonConvert.DeserializeObject<LevelRecord>(json);
        }

        private void SeedReady(params LevelRecord[] records)
        {
            var readiness = new SystemReadiness(true, records.Length, 0, 1, null);
            _lds.SeedCacheForTesting(records, readiness);
        }

        // ── AC-01: GetLevel happy path ─────────────────────────────────────────

        [Test]
        public void Test_GetLevel_ExistingId_ReturnsRecord()
        {
            SeedReady(MakeRecord(1));
            var record = _lds.GetLevel(1);
            Assert.AreEqual(1, record.LevelId);
        }

        // ── AC-02: GetLevel missing ID → LevelDataException(NotFound) ─────────

        [Test]
        public void Test_GetLevel_BelowValidRange_ThrowsLevelDataException_NotFound()
        {
            SeedReady(MakeRecord(1));
            var ex = Assert.Throws<LevelDataException>(() => _lds.GetLevel(0));
            Assert.AreEqual(LdsErrorCode.NotFound, ex.ErrorCode,
                "GetLevel(0) must throw LevelDataException with NotFound error code");
        }

        [Test]
        public void Test_GetLevel_AboveValidRange_ThrowsLevelDataException_NotFound()
        {
            SeedReady(MakeRecord(1));
            var ex = Assert.Throws<LevelDataException>(() => _lds.GetLevel(10000));
            Assert.AreEqual(LdsErrorCode.NotFound, ex.ErrorCode,
                "GetLevel(10000) must throw LevelDataException with NotFound error code");
        }

        // ── AC-13: GetRange when all IDs in range failed validation (not in cache) ─

        [Test]
        public void Test_GetRange_IdsNotInCache_ReturnsEmptyArray()
        {
            SeedReady(MakeRecord(1)); // levels 50–55 absent — failed validation at load time
            var result = _lds.GetRange(50, 55);
            Assert.AreEqual(0, result.Length,
                "GetRange where all IDs failed validation must return empty array, no exception");
        }

        // ── AC-14: GetByFilter with no matching records → empty array ─────────

        [Test]
        public void Test_GetByFilter_NoMatchingRecords_ReturnsEmptyArray()
        {
            SeedReady(MakeRecord(1, difficultyTier: 3, daily: true));
            var filter = new LevelFilter { DifficultyTier = 5, DailyChallengeEligible = true };
            var result = _lds.GetByFilter(filter);
            Assert.AreEqual(0, result.Length,
                "GetByFilter with no matches must return empty array, not throw");
        }

        // ── AC-15: Validated record returned without error ────────────────────

        [Test]
        public void Test_GetLevel_ValidatedRecord_ReturnedWithoutError()
        {
            SeedReady(MakeRecord(10));
            var record = _lds.GetLevel(10);
            Assert.IsNotNull(record);
            Assert.AreEqual(10, record.LevelId);
        }

        // ── AC-35: GetRange(fromId > toId) → empty array ─────────────────────

        [Test]
        public void Test_GetRange_InvertedParams_ReturnsEmptyArray()
        {
            SeedReady(MakeRecord(1), MakeRecord(50), MakeRecord(100));
            var result = _lds.GetRange(100, 50);
            Assert.AreEqual(0, result.Length,
                "GetRange(100, 50) — fromId > toId — must return empty array, no exception");
        }

        // ── AC-36: GetLevel for absent display_name → DisplayName defaults ────

        [Test]
        public void Test_GetLevel_AbsentDisplayName_DefaultsToLevelPrefix()
        {
            SeedReady(MakeRecord(42, displayName: null));
            var record = _lds.GetLevel(42);
            Assert.AreEqual("Level 42", record.DisplayName,
                "Absent display_name must default to 'Level {levelId}'");
        }

        // ── GetReadiness: callable in any state, returns current snapshot ─────

        [Test]
        public void Test_GetReadiness_InReadyState_ReturnsCorrectSnapshot()
        {
            SeedReady(MakeRecord(1), MakeRecord(2));
            var r = _lds.GetReadiness();
            Assert.IsTrue(r.Ready);
            Assert.AreEqual(2, r.LoadedCount);
            Assert.AreEqual(0, r.SkippedCount);
            Assert.IsNull(r.DiagnosticCode);
        }

        [Test]
        public void Test_GetReadiness_BeforeInit_DoesNotThrow()
        {
            // GetReadiness must NOT call GuardReady — it is callable in any state
            Assert.DoesNotThrow(() => { var _ = _lds.GetReadiness(); },
                "GetReadiness must not throw in UNINITIALIZED state");
        }

        // ── Guard checks: all three getters throw IOE before initialization ───

        [Test]
        public void Test_GetLevel_BeforeInit_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _lds.GetLevel(1));
        }

        [Test]
        public void Test_GetRange_BeforeInit_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _lds.GetRange(1, 5));
        }

        [Test]
        public void Test_GetByFilter_BeforeInit_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _lds.GetByFilter(new LevelFilter()));
        }

        // ── GetRange: partial hits silently omit missing IDs ─────────────────

        [Test]
        public void Test_GetRange_PartialHits_ReturnsOnlyCachedIds()
        {
            SeedReady(MakeRecord(1), MakeRecord(3)); // level 2 absent
            var result = _lds.GetRange(1, 3);
            Assert.AreEqual(2, result.Length, "GetRange must silently omit IDs not in cache");
            Assert.AreEqual(1, result[0].LevelId);
            Assert.AreEqual(3, result[1].LevelId);
        }

        // ── GetRange: equal fromId and toId ──────────────────────────────────

        [Test]
        public void Test_GetRange_SingleId_ExistingId_ReturnsSingleRecord()
        {
            SeedReady(MakeRecord(5));
            var result = _lds.GetRange(5, 5);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(5, result[0].LevelId);
        }

        [Test]
        public void Test_GetRange_SingleId_MissingId_ReturnsEmpty()
        {
            SeedReady(MakeRecord(1));
            var result = _lds.GetRange(5, 5);
            Assert.AreEqual(0, result.Length);
        }

        // ── S-1: GetByFilter(null) → ArgumentNullException ───────────────────

        [Test]
        public void Test_GetByFilter_NullFilter_ThrowsArgumentNullException()
        {
            SeedReady(MakeRecord(1));
            Assert.Throws<ArgumentNullException>(() => _lds.GetByFilter(null));
        }

        // ── S-2: GetByFilter AddedVersionMin excludes/includes by version ────

        [Test]
        public void Test_GetByFilter_AddedVersionMin_ExcludesRecordsBelowThreshold()
        {
            SeedReady(MakeRecord(1), MakeRecord(2), MakeRecord(3));
            var filter = new LevelFilter { AddedVersionMin = "2026.06" };
            var result = _lds.GetByFilter(filter);
            Assert.AreEqual(0, result.Length,
                "GetByFilter with AddedVersionMin newer than all records must return empty array");
        }

        [Test]
        public void Test_GetByFilter_AddedVersionMin_IncludesRecordsAtOrAboveThreshold()
        {
            SeedReady(MakeRecord(1), MakeRecord(2));
            var filter = new LevelFilter { AddedVersionMin = "2026.05" };
            var result = _lds.GetByFilter(filter);
            Assert.AreEqual(2, result.Length,
                "GetByFilter with AddedVersionMin equal to record version must include those records");
        }

        // ── GetByFilter: multiple matching records returned ───────────────────

        [Test]
        public void Test_GetByFilter_MultipleMatches_ReturnsAll()
        {
            SeedReady(
                MakeRecord(1, difficultyTier: 3, daily: true),
                MakeRecord(2, difficultyTier: 5, daily: true),
                MakeRecord(3, difficultyTier: 3, daily: false)
            );
            var result = _lds.GetByFilter(new LevelFilter { DailyChallengeEligible = true });
            Assert.AreEqual(2, result.Length,
                "GetByFilter must return all records that match the filter");
        }
    }
}
