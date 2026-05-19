using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using BoltSort.LevelData;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.LevelData.asmdef
// Covers:    Story 005 ACs — ReloadAsync

namespace BoltSort.Tests.Unit.LevelData
{
    [TestFixture]
    public class ReloadAsync_Test
    {
        private GameObject _go;
        private LevelDataSystem _lds;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LDS_ReloadTest");
            _lds = _go.AddComponent<LevelDataSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
            LevelDataSystem.ClearInstanceForTesting();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string MakeCatalogueJson(int version, params int[] levelIds)
        {
            var records = string.Join(",", Enumerable.Select(levelIds, id => $@"{{
                ""level_id"": {id}, ""display_name"": null, ""difficulty_tier"": 3,
                ""schema_version"": 1, ""color_count"": 2, ""stack_depth"": 2,
                ""color_stacks"": [[1,2],[2,1]], ""temp_slot_count"": 1, ""temp_slot_depth"": 1,
                ""is_tutorial"": false, ""daily_challenge_eligible"": true,
                ""hint_override"": null, ""added_version"": ""2026.05"", ""par_moves"": 4
            }}"));
            return $@"{{""catalogue_version"": {version}, ""levels"": [{records}]}}";
        }

        private void SeedReady(int catalogueVersion, params int[] levelIds)
        {
            _lds.LoadCatalogueTextAsync = () => Task.FromResult((true, MakeCatalogueJson(catalogueVersion, levelIds)));
            _lds.InitializeAsync();   // completes synchronously — seam is already-complete Task
        }

        private void SeedDegraded()
        {
            _lds.LoadCatalogueTextAsync = () => Task.FromResult((true, @"{""catalogue_version"":0,""levels"":[]}"));
            _lds.InitializeAsync();
        }

        // ── AC-27: ReloadAsync from UNINITIALIZED throws InvalidOperationException ──

        [Test]
        public void Test_ReloadAsync_FromUninitialized_ThrowsInvalidOperationException()
        {
            // Arrange: system stays UNINITIALIZED — no SeedReady / SeedDegraded call

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _lds.ReloadAsync());
        }

        // ── AC-23: Getters throw while LOADING during a reload ────────────────

        [Test]
        public void Test_ReloadAsync_FromReady_GetterThrowsDuringReload()
        {
            // Arrange
            SeedReady(1, 1, 2, 3);
            var pauseTcs = new TaskCompletionSource<(bool, string)>();
            _lds.LoadCatalogueTextAsync = () => pauseTcs.Task;

            // Act: system enters LOADING; ReloadCatalogueAsync pauses at await
            _lds.ReloadAsync();

            // Assert: getters must throw while LOADING
            Assert.Throws<InvalidOperationException>(() => _lds.GetLevel(1));

            // Cleanup: let ReloadCatalogueAsync reach its completion path
            pauseTcs.SetResult((false, null));
        }

        // ── AC-25: ReloadAsync atomically replaces the full catalogue ─────────

        [Test]
        public void Test_ReloadAsync_ReplacesFullCatalogue()
        {
            // Arrange: version 1 with levels 1, 2, 3
            SeedReady(1, 1, 2, 3);

            // Act: reload with version 2 containing levels 1–5
            _lds.LoadCatalogueTextAsync = () => Task.FromResult((true, MakeCatalogueJson(2, 1, 2, 3, 4, 5)));
            _lds.ReloadAsync();

            // Assert: readiness reflects the new catalogue
            var r = _lds.GetReadiness();
            Assert.AreEqual(2, r.CatalogueVersion, "CatalogueVersion must update to 2 after reload");
            Assert.AreEqual(5, r.LoadedCount, "LoadedCount must equal the new catalogue size");
            Assert.IsTrue(r.Ready, "System must be READY after a successful reload");

            // Assert: new level accessible
            var level5 = _lds.GetLevel(5);
            Assert.AreEqual(5, level5.LevelId, "Level 5 must be accessible after reload");

            // Assert: level not present in new catalogue raises LevelDataException
            Assert.Throws<LevelDataException>(() => _lds.GetLevel(6),
                "Level 6 is absent from the new catalogue — must throw LevelDataException");
        }

        // ── AC-26: Duplicate ReloadAsync calls while LOADING return same Task ─

        [Test]
        public void Test_ReloadAsync_DuplicateCallWhileLoading_ReturnsSameTask()
        {
            // Arrange
            SeedReady(1, 1);
            var pauseTcs = new TaskCompletionSource<(bool, string)>();
            _lds.LoadCatalogueTextAsync = () => pauseTcs.Task;

            // Act
            var t1 = _lds.ReloadAsync();
            var t2 = _lds.ReloadAsync();

            // Assert: idempotent — same Task object returned
            Assert.AreSame(t1, t2, "A second ReloadAsync call while LOADING must return the same in-flight Task");

            // Cleanup
            pauseTcs.SetResult((false, null));
        }

        // ── AC-28: ReloadAsync from DEGRADED transitions to READY ─────────────

        [Test]
        public void Test_ReloadAsync_FromDegraded_TransitionsToReady()
        {
            // Arrange
            SeedDegraded();
            Assert.IsFalse(_lds.GetReadiness().Ready, "Precondition: system must be in DEGRADED state");

            // Act: reload with a valid catalogue
            _lds.LoadCatalogueTextAsync = () => Task.FromResult((true, MakeCatalogueJson(1, 10, 20, 30)));
            _lds.ReloadAsync();

            // Assert
            var r = _lds.GetReadiness();
            Assert.IsTrue(r.Ready, "System must be READY after a successful reload from DEGRADED");
            Assert.AreEqual(3, r.LoadedCount, "LoadedCount must equal the new catalogue size");
            var level10 = _lds.GetLevel(10);
            Assert.AreEqual(10, level10.LevelId, "Level 10 must be accessible after reload");
        }

        // ── AC-28 complement: load failure during reload → DEGRADED ──────────

        [Test]
        public void Test_ReloadAsync_LoadFails_TransitionsToDegraded()
        {
            // Arrange
            SeedReady(1, 1, 2, 3);

            // Act: seam returns load failure
            _lds.LoadCatalogueTextAsync = () => Task.FromResult((false, (string)null));
            _lds.ReloadAsync();

            // Assert
            var r = _lds.GetReadiness();
            Assert.IsFalse(r.Ready, "System must be DEGRADED when the reload load step fails");
            Assert.AreEqual("CATALOGUE_LOAD_FAILED", r.DiagnosticCode,
                "DiagnosticCode must be CATALOGUE_LOAD_FAILED on load failure");
        }

        // ── OnLevelDataReady fires exactly once on successful reload ──────────

        [Test]
        public void Test_ReloadAsync_FiresOnLevelDataReady()
        {
            // Arrange
            SeedReady(1, 1);
            int fireCount = 0;
            _lds.OnLevelDataReady += () => fireCount++;

            // Act
            _lds.LoadCatalogueTextAsync = () => Task.FromResult((true, MakeCatalogueJson(2, 1, 2)));
            _lds.ReloadAsync();

            // Assert
            Assert.AreEqual(1, fireCount,
                "OnLevelDataReady must fire exactly once when ReloadAsync completes successfully");
        }
    }
}
