using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BoltSort.LevelData;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Integration.LevelData.asmdef
// Covers:    Story 003 ACs — InitializeAsync() load pipeline and state machine

namespace BoltSort.Tests.Integration.LevelData
{
    [TestFixture]
    public class InitAsync_Test
    {
        private GameObject _go;
        private LevelDataSystem _lds;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LDS_Test");
            _lds = _go.AddComponent<LevelDataSystem>(); // Awake fires, sets default loader
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go); // triggers OnDestroy → Instance = null
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string MakeCatalogueJson(int validCount, int invalidCount, int catalogueVersion = 1)
        {
            var sb = new StringBuilder();
            sb.Append($@"{{""catalogue_version"":{catalogueVersion},""levels"":[");
            int id = 1;

            // Valid records: all Stage 2 checks pass
            for (int i = 0; i < validCount; i++, id++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ValidRecord(id));
            }

            // Invalid records: par_moves=0 → fails AC-19
            for (int i = 0; i < invalidCount; i++, id++)
            {
                if (validCount + i > 0) sb.Append(',');
                sb.Append(InvalidRecord(id));
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static string ValidRecord(int id) =>
            $@"{{""level_id"":{id},""display_name"":""Level {id}"",""difficulty_tier"":3,""schema_version"":1,""color_count"":2,""stack_depth"":2,""color_stacks"":[[1,2],[2,1]],""temp_slot_count"":1,""temp_slot_depth"":1,""is_tutorial"":false,""daily_challenge_eligible"":true,""hint_override"":null,""added_version"":""2026.05"",""par_moves"":4}}";

        // Invalid: par_moves = 0 → Stage 2 validation rejects it
        private static string InvalidRecord(int id) =>
            $@"{{""level_id"":{id},""display_name"":""Level {id}"",""difficulty_tier"":3,""schema_version"":1,""color_count"":2,""stack_depth"":2,""color_stacks"":[[1,2],[2,1]],""temp_slot_count"":1,""temp_slot_depth"":1,""is_tutorial"":false,""daily_challenge_eligible"":true,""hint_override"":null,""added_version"":""2026.05"",""par_moves"":0}}";

        private static Func<Task<(bool, string)>> FakeLoader(bool succeeded, string json = null) =>
            () => Task.FromResult((succeeded, json));

        // ── AC-05: failure_ratio exactly 0.20 → READY ─────────────────────────

        [UnityTest]
        public IEnumerator Test_FailureRatioExactly20Percent_EntersReady()
        {
            _lds.LoadCatalogueTextAsync = FakeLoader(true, MakeCatalogueJson(validCount: 80, invalidCount: 20));
            var task = _lds.InitializeAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            var r = _lds.GetReadiness();
            Assert.IsTrue(r.Ready, "failure_ratio = 0.20 exactly must resolve to READY");
            Assert.IsTrue(_lds.IsReady, "IsReady must be true on READY path");
            Assert.AreEqual(80, r.LoadedCount);
            Assert.AreEqual(20, r.SkippedCount);
        }

        // ── AC-06: failure_ratio 0.21 → DEGRADED; valid records still loaded ──

        [UnityTest]
        public IEnumerator Test_FailureRatio21Percent_EntersDegraded_ValidRecordsRetained()
        {
            _lds.LoadCatalogueTextAsync = FakeLoader(true, MakeCatalogueJson(validCount: 79, invalidCount: 21));
            var task = _lds.InitializeAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            var r = _lds.GetReadiness();
            Assert.IsFalse(r.Ready, "failure_ratio = 0.21 must resolve to DEGRADED");
            Assert.IsTrue(_lds.IsReady, "IsReady must be true even on DEGRADED path");
            Assert.AreEqual(79, r.LoadedCount, "Valid records must remain in cache when DEGRADED");
            Assert.AreEqual(21, r.SkippedCount);
        }

        // ── AC-07: empty catalogue → DEGRADED unconditionally ─────────────────

        [UnityTest]
        public IEnumerator Test_EmptyCatalogue_EntersDegraded_ZeroCounts()
        {
            _lds.LoadCatalogueTextAsync = FakeLoader(true, @"{""catalogue_version"":1,""levels"":[]}");
            var task = _lds.InitializeAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            var r = _lds.GetReadiness();
            Assert.IsFalse(r.Ready, "Empty catalogue must unconditionally produce DEGRADED");
            Assert.IsTrue(_lds.IsReady);
            Assert.AreEqual(0, r.LoadedCount);
            Assert.AreEqual(0, r.SkippedCount);
            Assert.IsNotNull(r.DiagnosticCode, "DiagnosticCode must be set when DEGRADED");
        }

        // ── AC-10: getters before InitializeAsync → InvalidOperationException ──

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

        // ── AC-10 (LOADING path): getters throw IOE while InitializeAsync in flight ─

        [UnityTest]
        public IEnumerator Test_Getters_WhileLoading_ThrowsInvalidOperationException()
        {
            var pendingLoad = new TaskCompletionSource<(bool, string)>();
            _lds.LoadCatalogueTextAsync = () => pendingLoad.Task;

            var initTask = _lds.InitializeAsync(); // _state = Loading; async void suspends at await

            // State is Loading — all three getters must throw IOE, not NullRef or silent default
            Assert.Throws<InvalidOperationException>(() => _lds.GetLevel(1),
                "GetLevel must throw IOE while LOADING");
            Assert.Throws<InvalidOperationException>(() => _lds.GetRange(1, 5),
                "GetRange must throw IOE while LOADING");
            Assert.Throws<InvalidOperationException>(() => _lds.GetByFilter(new LevelFilter()),
                "GetByFilter must throw IOE while LOADING");

            // Resolve so TearDown can clean up
            pendingLoad.SetResult((true, MakeCatalogueJson(validCount: 5, invalidCount: 0)));
            yield return new WaitUntil(() => initTask.IsCompleted);
        }

        // ── AC-11: concurrent InitializeAsync → same Task; loader called once ──

        [UnityTest]
        public IEnumerator Test_ConcurrentInitializeAsync_ReturnsSameTask_LoaderCalledOnce()
        {
            int callCount = 0;
            var pendingLoad = new TaskCompletionSource<(bool, string)>();
            _lds.LoadCatalogueTextAsync = () => { callCount++; return pendingLoad.Task; };

            var task1 = _lds.InitializeAsync();
            var task2 = _lds.InitializeAsync(); // second call while still LOADING

            Assert.AreEqual(1, callCount, "Loader must be called exactly once");
            Assert.AreSame(task1, task2, "Second call must return the same in-flight Task");

            // resolve the pending load so the test can tear down cleanly
            pendingLoad.SetResult((true, MakeCatalogueJson(validCount: 5, invalidCount: 0)));
            yield return new WaitUntil(() => task1.IsCompleted);
        }

        // ── AC-12: InitializeAsync while READY → completed Task immediately ───

        [UnityTest]
        public IEnumerator Test_InitializeAsync_WhileReady_ReturnsCompletedTask()
        {
            _lds.LoadCatalogueTextAsync = FakeLoader(true, MakeCatalogueJson(validCount: 5, invalidCount: 0));
            var firstTask = _lds.InitializeAsync();
            yield return new WaitUntil(() => firstTask.IsCompleted);

            var secondTask = _lds.InitializeAsync();
            Assert.IsTrue(secondTask.IsCompleted, "InitializeAsync while READY must return an already-completed Task");
        }

        // ── AC-17: Addressables load failure → DEGRADED; no exception to caller

        [UnityTest]
        public IEnumerator Test_AddressablesFailure_EntersDegraded_NoExceptionPropagated()
        {
            _lds.LoadCatalogueTextAsync = FakeLoader(false);
            Task task = null;

            Assert.DoesNotThrow(() => { task = _lds.InitializeAsync(); },
                "InitializeAsync must not throw when Addressables fails");

            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsNull(task.Exception, "No exception must propagate through the returned Task");

            var r = _lds.GetReadiness();
            Assert.IsFalse(r.Ready);
            Assert.IsTrue(_lds.IsReady);
            Assert.AreEqual("CATALOGUE_LOAD_FAILED", r.DiagnosticCode);
            Assert.AreEqual(0, r.LoadedCount);
        }

        // ── OnLevelDataReady compatibility bridge ──────────────────────────────

        [UnityTest]
        public IEnumerator Test_OnLevelDataReady_FiresOnReadyPath()
        {
            bool fired = false;
            _lds.OnLevelDataReady += () => fired = true;
            _lds.LoadCatalogueTextAsync = FakeLoader(true, MakeCatalogueJson(validCount: 5, invalidCount: 0));

            var task = _lds.InitializeAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsTrue(fired, "OnLevelDataReady must fire on READY path");
        }

        [UnityTest]
        public IEnumerator Test_OnLevelDataReady_FiresOnDegradedPath()
        {
            bool fired = false;
            _lds.OnLevelDataReady += () => fired = true;
            _lds.LoadCatalogueTextAsync = FakeLoader(false);

            var task = _lds.InitializeAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsTrue(fired, "OnLevelDataReady must fire on DEGRADED path");
        }

        // ── Subscribe-then-check pattern (ADR-0001 LevelProgression bridge) ───

        [UnityTest]
        public IEnumerator Test_SubscribeThenCheck_HandlesAlreadyReadyState()
        {
            _lds.LoadCatalogueTextAsync = FakeLoader(true, MakeCatalogueJson(validCount: 5, invalidCount: 0));
            var task = _lds.InitializeAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            // Late subscriber uses subscribe-then-check to avoid missing the event
            bool handled = false;
            _lds.OnLevelDataReady += () => handled = true;
            if (_lds.IsReady) handled = true; // check half — fires because already READY

            Assert.IsTrue(handled, "Subscribe-then-check must handle the case where LDS is already READY");
        }
    }
}
