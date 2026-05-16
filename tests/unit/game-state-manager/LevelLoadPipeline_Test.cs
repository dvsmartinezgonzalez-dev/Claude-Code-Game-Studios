using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;
using BoltSort.LevelData;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Story 005 ACs — AC-GSM-11, AC-GSM-12, AC-GSM-13, AC-GSM-17, AC-GSM-20
// Design:    design/gdd/game-state-manager.md  (TR-GSM-008)
// ADR:       docs/architecture/adr-0006-board-state-representation.md (L-01 through L-07)
//            docs/architecture/adr-0001-singleton-boot.md (subscribe-then-check)

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class LevelLoadPipeline_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_LoadTest");
            _gsm = _go.AddComponent<global::BoltSort.GameStateManager.GameStateManager>();
            // GSM starts in UNLOADED — no extra state seeding required for load tests
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            global::BoltSort.GameStateManager.GameStateManager.ClearInstanceForTesting();
        }

        // ── Stub LDS ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Minimal stub implementation of ILevelDataSystem for synchronous unit tests.
        /// Configurable readiness sequence and level record; call counter is accessible.
        /// </summary>
        private sealed class StubLds : ILevelDataSystem
        {
            private readonly Queue<bool> _readinessQueue;
            private readonly LevelRecord _record;
            private readonly bool _throwOnGetLevel;

            public int GetReadinessCallCount { get; private set; }

            /// <param name="readinessSequence">
            /// Ordered sequence of Ready values returned per GetReadiness() call.
            /// Once exhausted, the last value is repeated.
            /// </param>
            /// <param name="record">Record returned by GetLevel(). Null causes GetLevel to throw.</param>
            /// <param name="throwOnGetLevel">When true, GetLevel throws LevelDataException.</param>
            public StubLds(IEnumerable<bool> readinessSequence, LevelRecord record,
                           bool throwOnGetLevel = false)
            {
                _readinessQueue  = new Queue<bool>(readinessSequence);
                _record          = record;
                _throwOnGetLevel = throwOnGetLevel;
            }

            public bool IsReady => true;
            public event Action OnLevelDataReady { add { } remove { } }
            public Task InitializeAsync() => Task.CompletedTask;
            public Task ReloadAsync()     => Task.CompletedTask;

            public SystemReadiness GetReadiness()
            {
                GetReadinessCallCount++;
                bool ready = _readinessQueue.Count > 0
                    ? _readinessQueue.Dequeue()
                    : true; // default: ready after queue exhausted
                return new SystemReadiness(ready, loaded: 1, skipped: 0, version: 1, code: null);
            }

            public LevelRecord GetLevel(int levelId)
            {
                if (_throwOnGetLevel)
                    throw new LevelDataException($"Stub: level {levelId} not found.");
                return _record;
            }

            public LevelRecord[] GetRange(int fromId, int toId) => Array.Empty<LevelRecord>();
            public LevelRecord[] GetByFilter(LevelFilter filter) => Array.Empty<LevelRecord>();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Deserializes a LevelRecord from a JSON string using Newtonsoft.Json,
        /// matching the production deserialization path.
        /// </summary>
        private static LevelRecord MakeRecord(int levelId, int colorCount, int stackDepth,
                                              int tempSlotCount, int tempSlotDepth,
                                              string colorStacksJson,
                                              int parMoves = 5)
        {
            var json = $@"{{
                ""level_id"": {levelId},
                ""color_count"": {colorCount},
                ""stack_depth"": {stackDepth},
                ""color_stacks"": {colorStacksJson},
                ""temp_slot_count"": {tempSlotCount},
                ""temp_slot_depth"": {tempSlotDepth},
                ""schema_version"": 1,
                ""daily_challenge_eligible"": false,
                ""is_tutorial"": false,
                ""par_moves"": {parMoves}
            }}";
            return JsonConvert.DeserializeObject<LevelRecord>(json);
        }

        /// <summary>
        /// Subscribes an event spy to OnLevelLoaded and returns capture variables.
        /// </summary>
        private (Action cleanup,
                 Func<bool>  firedOnce,
                 Func<int>   getLevelId,
                 Func<int>   getColorCount,
                 Func<int>   getStackDepth,
                 Func<int>   getTempSlotCount,
                 Func<int>   getTempSlotDepth,
                 Func<long>  getSequenceId)
            AttachLevelLoadedSpy()
        {
            bool fired = false;
            int  captLevelId      = -1;
            int  captColorCount   = -1;
            int  captStackDepth   = -1;
            int  captTempSlotCount = -1;
            int  captTempSlotDepth = -1;
            long captSequenceId   = -1L;

            void Spy(int lid, int cc, int sd, int tsc, int tsd, long seq)
            {
                fired            = true;
                captLevelId      = lid;
                captColorCount   = cc;
                captStackDepth   = sd;
                captTempSlotCount = tsc;
                captTempSlotDepth = tsd;
                captSequenceId   = seq;
            }

            _gsm.OnLevelLoaded += Spy;
            return (() => _gsm.OnLevelLoaded -= Spy,
                    () => fired,
                    () => captLevelId,
                    () => captColorCount,
                    () => captStackDepth,
                    () => captTempSlotCount,
                    () => captTempSlotDepth,
                    () => captSequenceId);
        }

        /// <summary>Subscribes a spy to OnSessionLoadFailed and returns capture variables.</summary>
        private (Action cleanup,
                 Func<GsmSessionLoadFailReason?> getReason,
                 Func<int> getFailLevelId)
            AttachFailSpy()
        {
            GsmSessionLoadFailReason? captReason = null;
            int captLevelId = -1;
            void Spy(GsmSessionLoadFailReason r, int id) { captReason = r; captLevelId = id; }
            _gsm.OnSessionLoadFailed += Spy;
            return (() => _gsm.OnSessionLoadFailed -= Spy,
                    () => captReason,
                    () => captLevelId);
        }

        /// <summary>Subscribes a spy to OnLevelComplete and returns whether it fired.</summary>
        private (Action cleanup, Func<bool> firedOnce) AttachLevelCompleteSpy()
        {
            bool fired = false;
            void Spy(int a, int b, int c, long d) { fired = true; }
            _gsm.OnLevelComplete += Spy;
            return (() => _gsm.OnLevelComplete -= Spy, () => fired);
        }

        // ── AC-GSM-11: Pre-won board does not auto-win ────────────────────────────

        /// <summary>
        /// AC-GSM-11 (core): level_loaded is emitted and GSM is ACTIVE; level_complete is NOT emitted.
        /// Board: color_count=2, stack_depth=3, all stacks monochromatic and full ([[1,1,1],[2,2,2]]).
        /// Pre-won detection must log a warning but must not transition GSM to COMPLETE.
        /// </summary>
        [Test]
        public void Test_LoadLevel_PreWonBoard_EmitsLevelLoaded_NoLevelComplete_StateIsActive()
        {
            // Arrange
            var record = MakeRecord(levelId: 10, colorCount: 2, stackDepth: 3,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1,1],[2,2,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupLoaded, firedLoaded, _, _, _, _, _, _) = AttachLevelLoadedSpy();
            var (cleanupComplete, firedComplete)                = AttachLevelCompleteSpy();

            // Act
            _gsm.LoadLevel(10);

            // Assert — level_loaded fired
            Assert.IsTrue(firedLoaded(), "OnLevelLoaded must fire for a pre-won board");

            // Assert — level_complete NOT fired (AC-GSM-11: no auto-win)
            Assert.IsFalse(firedComplete(), "OnLevelComplete must NOT fire on LoadLevel for a pre-won board");

            // Assert — GSM state is ACTIVE, not COMPLETE
            Assert.AreEqual(GSMLifecycleState.Active, _gsm.LifecycleState,
                "GSM must be ACTIVE after loading a pre-won board (win is SortMechanic's responsibility)");

            cleanupLoaded();
            cleanupComplete();
        }

        // ── AC-GSM-12: Board initializes to exact spec ────────────────────────────

        /// <summary>
        /// AC-GSM-12 (core): after LoadLevel completes, board state matches the level record exactly.
        /// Checks: current_sequence_id=0, move_count=0, undo stack depth=0,
        /// stack_contents matches color_stacks, temp_slot_contents = 2 empty lists.
        /// </summary>
        [Test]
        public void Test_LoadLevel_BoardInitializesToExactSpec()
        {
            // Arrange
            var record = MakeRecord(levelId: 1, colorCount: 3, stackDepth: 4,
                                    tempSlotCount: 2, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,2,3,4],[2,1,4,3],[3,4,1,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            // Act
            _gsm.LoadLevel(1);

            // Assert — counters reset to zero
            Assert.AreEqual(0L, _gsm.CurrentSequenceId, "current_sequence_id must be 0 after load");
            Assert.AreEqual(0,  _gsm.MoveCount,         "move_count must be 0 after load");
            Assert.AreEqual(0,  _gsm.UndoStackDepth,    "undo stack must be empty after load");

            // Assert — color stack contents match record exactly (index 0 = bottom bolt)
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, new List<int>(_gsm.StackContents[0]),
                "stack_contents[0] must match color_stacks[0]");
            Assert.AreEqual(new[] { 2, 1, 4, 3 }, new List<int>(_gsm.StackContents[1]),
                "stack_contents[1] must match color_stacks[1]");
            Assert.AreEqual(new[] { 3, 4, 1, 2 }, new List<int>(_gsm.StackContents[2]),
                "stack_contents[2] must match color_stacks[2]");

            // Assert — temp slots are empty lists (not null)
            Assert.IsNotNull(_gsm.TempSlotContents,         "TempSlotContents must not be null");
            Assert.AreEqual(2, _gsm.TempSlotContents.Length, "TempSlotContents must have 2 slots");
            Assert.AreEqual(0, _gsm.TempSlotContents[0].Count, "temp slot 0 must be empty");
            Assert.AreEqual(0, _gsm.TempSlotContents[1].Count, "temp slot 1 must be empty");
        }

        /// <summary>
        /// AC-GSM-12 (edge: temp_slot_count=0): no temp slots — TempSlotContents is an empty
        /// collection with length 0 (no arrays allocated).
        /// </summary>
        [Test]
        public void Test_LoadLevel_ZeroTempSlots_TempSlotContentsIsEmpty()
        {
            // Arrange
            var record = MakeRecord(levelId: 2, colorCount: 2, stackDepth: 3,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1,1],[2,2,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            // Act
            _gsm.LoadLevel(2);

            // Assert
            Assert.IsNotNull(_gsm.TempSlotContents, "TempSlotContents must not be null even with 0 slots");
            Assert.AreEqual(0, _gsm.TempSlotContents.Length,
                "TempSlotContents must have length 0 when temp_slot_count=0");
        }

        // ── L-03 invariant failure through LoadLevel pipeline ────────────────────

        /// <summary>
        /// L-03 (pipeline integration): LoadLevel with a record that fails bolt-count
        /// invariant check emits OnSessionLoadFailed(InvariantViolation); GSM → UNLOADED;
        /// OnLevelLoaded NOT emitted. Verifies the L-03 branch inside LoadLevel is wired.
        /// </summary>
        [Test]
        public void Test_LoadLevel_InvariantFailure_EmitsSessionLoadFailed_NoLevelLoaded()
        {
            // Arrange — 11 bolts total for colorCount=3, stackDepth=4 (expected 12 — check 1 fails)
            var record = MakeRecord(levelId: 50, colorCount: 3, stackDepth: 4,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1,1,1],[2,2,2,2],[3,3,3]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupFail,   getReason, getFailLevelId)         = AttachFailSpy();
            var (cleanupLoaded, firedLoaded, _, _, _, _, _, _)     = AttachLevelLoadedSpy();

            // Act
            _gsm.LoadLevel(50);

            // Assert — invariant failure emits OnSessionLoadFailed
            Assert.AreEqual(GsmSessionLoadFailReason.InvariantViolation, getReason(),
                "L-03 failure must emit InvariantViolation");
            Assert.AreEqual(50, getFailLevelId(),
                "OnSessionLoadFailed must carry the requested level ID");

            // Assert — GSM → UNLOADED
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState,
                "GSM must be UNLOADED after L-03 invariant failure");

            // Assert — OnLevelLoaded NOT emitted
            Assert.IsFalse(firedLoaded(),
                "OnLevelLoaded must NOT fire when L-03 invariant check fails");

            cleanupFail();
            cleanupLoaded();
        }

        // ── AC-GSM-13: load_level rejected in non-UNLOADED states ─────────────────

        /// <summary>
        /// AC-GSM-13 (sub-case: LOADING): LoadLevel is a no-op when GSM is in LOADING state.
        /// No level_loaded emitted; state remains LOADING.
        /// </summary>
        [Test]
        public void Test_LoadLevel_RejectedWhileLoading_NoEventEmitted_StateUnchanged()
        {
            // Arrange — put GSM into LOADING manually (simulates mid-load scenario)
            _gsm.SetStateForTesting(GSMLifecycleState.Loading);

            var record = MakeRecord(levelId: 3, colorCount: 2, stackDepth: 2,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1],[2,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupLoaded, firedLoaded, _, _, _, _, _, _) = AttachLevelLoadedSpy();

            // Act
            _gsm.LoadLevel(3);

            // Assert
            Assert.IsFalse(firedLoaded(), "OnLevelLoaded must NOT fire when GSM is in LOADING");
            Assert.AreEqual(GSMLifecycleState.Loading, _gsm.LifecycleState,
                "GSM must remain in LOADING when LoadLevel is called in LOADING state");

            cleanupLoaded();
        }

        /// <summary>
        /// AC-GSM-13 (sub-case: ACTIVE): LoadLevel is a no-op when GSM is in ACTIVE state.
        /// No level_loaded emitted; state remains ACTIVE.
        /// </summary>
        [Test]
        public void Test_LoadLevel_RejectedWhileActive_NoEventEmitted_StateUnchanged()
        {
            // Arrange — put GSM into ACTIVE (as if a level is already running)
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            var record = MakeRecord(levelId: 4, colorCount: 2, stackDepth: 2,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1],[2,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupLoaded, firedLoaded, _, _, _, _, _, _) = AttachLevelLoadedSpy();

            // Act
            _gsm.LoadLevel(4);

            // Assert
            Assert.IsFalse(firedLoaded(), "OnLevelLoaded must NOT fire when GSM is in ACTIVE");
            Assert.AreEqual(GSMLifecycleState.Active, _gsm.LifecycleState,
                "GSM must remain in ACTIVE when LoadLevel is called in ACTIVE state");

            cleanupLoaded();
        }

        /// <summary>
        /// AC-GSM-13 (sub-case: COMPLETE): LoadLevel is a no-op when GSM is in COMPLETE state.
        /// No level_loaded emitted; state remains COMPLETE.
        /// </summary>
        [Test]
        public void Test_LoadLevel_RejectedWhileComplete_NoEventEmitted_StateUnchanged()
        {
            // Arrange — put GSM into COMPLETE (puzzle solved)
            _gsm.SetStateForTesting(GSMLifecycleState.Complete);

            var record = MakeRecord(levelId: 5, colorCount: 2, stackDepth: 2,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1],[2,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupLoaded, firedLoaded, _, _, _, _, _, _) = AttachLevelLoadedSpy();

            // Act
            _gsm.LoadLevel(5);

            // Assert
            Assert.IsFalse(firedLoaded(), "OnLevelLoaded must NOT fire when GSM is in COMPLETE");
            Assert.AreEqual(GSMLifecycleState.Complete, _gsm.LifecycleState,
                "GSM must remain in COMPLETE when LoadLevel is called in COMPLETE state");

            cleanupLoaded();
        }

        // ── AC-GSM-17: Readiness query is fresh per LoadLevel call ────────────────

        /// <summary>
        /// AC-GSM-17 (core): first LoadLevel returns not-ready → session_load_failed + UNLOADED.
        /// Second LoadLevel with ready=true succeeds to level_loaded. Stub call counter = 2.
        /// </summary>
        [Test]
        public void Test_LoadLevel_ReadinessQueryFreshPerAttempt_SecondAttemptSucceeds()
        {
            // Arrange — first call returns Ready=false, second returns Ready=true
            var record = MakeRecord(levelId: 6, colorCount: 2, stackDepth: 2,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1],[2,2]]");
            var lds = new StubLds(new[] { false, true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupFail, getReason, _)                    = AttachFailSpy();
            var (cleanupLoaded, firedLoaded, _, _, _, _, _, _) = AttachLevelLoadedSpy();

            // Act — first attempt
            _gsm.LoadLevel(6);

            // Assert — first attempt fails, GSM → UNLOADED
            Assert.AreEqual(GsmSessionLoadFailReason.LevelDataUnavailable, getReason(),
                "First LoadLevel with Ready=false must emit LevelDataUnavailable");
            Assert.AreEqual(6, getFailLevelId(),
                "OnSessionLoadFailed must carry the requested level ID");
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState,
                "GSM must return to UNLOADED after L-01 failure");
            Assert.IsFalse(firedLoaded(), "OnLevelLoaded must NOT fire on first (failing) attempt");

            // Act — second attempt (GSM is back in UNLOADED)
            _gsm.LoadLevel(6);

            // Assert — second attempt succeeds
            Assert.IsTrue(firedLoaded(), "OnLevelLoaded must fire on second attempt (Ready=true)");
            Assert.AreEqual(GSMLifecycleState.Active, _gsm.LifecycleState,
                "GSM must be ACTIVE after second (successful) LoadLevel");

            // Assert — stub query called exactly twice (not cached)
            Assert.AreEqual(2, lds.GetReadinessCallCount,
                "GetReadiness must be called once per LoadLevel attempt (not cached)");

            cleanupFail();
            cleanupLoaded();
        }

        /// <summary>
        /// AC-GSM-17 (edge: L-02 failure): LDS is ready but GetLevel throws LevelDataException.
        /// GSM → UNLOADED; session_load_failed(LevelRecordError) emitted; no level_loaded.
        /// </summary>
        [Test]
        public void Test_LoadLevel_LevelRecordNotFound_EmitsLevelRecordError_StateIsUnloaded()
        {
            // Arrange — LDS ready but GetLevel throws
            var lds = new StubLds(new[] { true }, record: null, throwOnGetLevel: true);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupFail, getReason, getFailLevelId)       = AttachFailSpy();
            var (cleanupLoaded, firedLoaded, _, _, _, _, _, _) = AttachLevelLoadedSpy();

            // Act
            _gsm.LoadLevel(99);

            // Assert
            Assert.AreEqual(GsmSessionLoadFailReason.LevelRecordError, getReason(),
                "GetLevel exception must emit LevelRecordError");
            Assert.AreEqual(99, getFailLevelId(), "Fail event must carry the requested level ID");
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState,
                "GSM must be UNLOADED after L-02 failure");
            Assert.IsFalse(firedLoaded(), "OnLevelLoaded must NOT fire after L-02 failure");

            cleanupFail();
            cleanupLoaded();
        }

        // ── AC-GSM-20: level_loaded all 6 parameters ──────────────────────────────

        /// <summary>
        /// AC-GSM-20 (core): OnLevelLoaded carries all 6 required parameters with values
        /// matching the level record. Each field is asserted independently.
        /// </summary>
        [Test]
        public void Test_LoadLevel_OnLevelLoaded_AllSixParameters_MatchRecordValues()
        {
            // Arrange — known values for all 5 board parameters
            var record = MakeRecord(levelId: 42, colorCount: 4, stackDepth: 5,
                                    tempSlotCount: 3, tempSlotDepth: 2,
                                    colorStacksJson: "[[1,1,1,1,1],[2,2,2,2,2],[3,3,3,3,3],[4,4,4,4,4]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupLoaded,
                 firedLoaded,
                 getLevelId,
                 getColorCount,
                 getStackDepth,
                 getTempSlotCount,
                 getTempSlotDepth,
                 getSequenceId) = AttachLevelLoadedSpy();

            // Act
            _gsm.LoadLevel(42);

            // Assert — event fired
            Assert.IsTrue(firedLoaded(), "OnLevelLoaded must fire on successful load");

            // Assert — each parameter independently (AC-GSM-20)
            Assert.AreEqual(42, getLevelId(),       "OnLevelLoaded param 0: levelId must match");
            Assert.AreEqual(4,  getColorCount(),    "OnLevelLoaded param 1: colorCount must match");
            Assert.AreEqual(5,  getStackDepth(),    "OnLevelLoaded param 2: stackDepth must match");
            Assert.AreEqual(3,  getTempSlotCount(), "OnLevelLoaded param 3: tempSlotCount must match");
            Assert.AreEqual(2,  getTempSlotDepth(), "OnLevelLoaded param 4: tempSlotDepth must match");
            Assert.AreEqual(0L, getSequenceId(),    "OnLevelLoaded param 5: sequenceId must be 0");

            cleanupLoaded();
        }

        /// <summary>
        /// AC-GSM-20 (ordering guard): when OnLevelLoaded fires, GSM is already in ACTIVE state.
        /// Validates the L-07-before-L-06 fix: subscribers observing OnLevelLoaded must see Active.
        /// </summary>
        [Test]
        public void Test_LoadLevel_OnLevelLoaded_SubscriberSeesActiveState()
        {
            // Arrange
            var record = MakeRecord(levelId: 7, colorCount: 2, stackDepth: 2,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1],[2,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            GSMLifecycleState? stateAtEvent = null;
            void Spy(int a, int b, int c, int d, int e, long f)
                => stateAtEvent = _gsm.LifecycleState;
            _gsm.OnLevelLoaded += Spy;

            // Act
            _gsm.LoadLevel(7);

            // Assert — L-07 (transition to ACTIVE) must precede L-06 (emit OnLevelLoaded)
            Assert.IsNotNull(stateAtEvent, "OnLevelLoaded spy must have been called");
            Assert.AreEqual(GSMLifecycleState.Active, stateAtEvent,
                "LifecycleState must be Active when OnLevelLoaded fires (L-07 before L-06)");

            _gsm.OnLevelLoaded -= Spy;
        }

        /// <summary>
        /// AC-GSM-20 (board dimensions on interface): GSM public properties match record after load.
        /// Validates ColorCount, StackDepth, TempSlotCount, TempSlotDepth, CurrentSequenceId.
        /// </summary>
        [Test]
        public void Test_LoadLevel_PublicProperties_MatchRecordValues()
        {
            // Arrange
            var record = MakeRecord(levelId: 8, colorCount: 3, stackDepth: 4,
                                    tempSlotCount: 2, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,2,3,4],[2,1,4,3],[3,4,1,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            // Act
            _gsm.LoadLevel(8);

            // Assert — interface-exposed board dimensions
            Assert.AreEqual(3,  _gsm.ColorCount,        "ColorCount must match record");
            Assert.AreEqual(4,  _gsm.StackDepth,        "StackDepth must match record");
            Assert.AreEqual(2,  _gsm.TempSlotCount,     "TempSlotCount must match record");
            Assert.AreEqual(1,  _gsm.TempSlotDepth,     "TempSlotDepth must match record");
            Assert.AreEqual(0L, _gsm.CurrentSequenceId, "CurrentSequenceId must be 0 after load");
            Assert.AreEqual(0,  _gsm.MoveCount,         "MoveCount must be 0 after load");
        }

        /// <summary>
        /// AC-GSM-12 (level_loaded event payload matches AC-GSM-20 exact level):
        /// Canonical test case from QA spec: color_count=3, stack_depth=4, temp_slot_count=2,
        /// temp_slot_depth=1. Event carries (levelId, 3, 4, 2, 1, 0).
        /// </summary>
        [Test]
        public void Test_LoadLevel_LevelLoadedPayload_MatchesQaSpec()
        {
            // Arrange — exact values from AC-GSM-12 QA spec
            var record = MakeRecord(levelId: 100, colorCount: 3, stackDepth: 4,
                                    tempSlotCount: 2, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,2,3,4],[2,1,4,3],[3,4,1,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupLoaded,
                 firedLoaded,
                 getLevelId,
                 getColorCount,
                 getStackDepth,
                 getTempSlotCount,
                 getTempSlotDepth,
                 getSequenceId) = AttachLevelLoadedSpy();

            // Act
            _gsm.LoadLevel(100);

            // Assert — QA spec: event carries (100, 3, 4, 2, 1, 0)
            Assert.IsTrue(firedLoaded(), "OnLevelLoaded must fire");
            Assert.AreEqual(100, getLevelId(),       "levelId must be 100");
            Assert.AreEqual(3,   getColorCount(),    "colorCount must be 3");
            Assert.AreEqual(4,   getStackDepth(),    "stackDepth must be 4");
            Assert.AreEqual(2,   getTempSlotCount(), "tempSlotCount must be 2");
            Assert.AreEqual(1,   getTempSlotDepth(), "tempSlotDepth must be 1");
            Assert.AreEqual(0L,  getSequenceId(),    "sequenceId must be 0");

            cleanupLoaded();
        }

        /// <summary>
        /// AC-GSM-13 (no session_load_failed on EC-09 rejection): when LoadLevel is rejected
        /// in a non-UNLOADED state, neither OnSessionLoadFailed nor OnLevelLoaded fires.
        /// </summary>
        [Test]
        public void Test_LoadLevel_RejectedInNonUnloaded_NoEventsEmitted()
        {
            // Arrange — put GSM in ACTIVE
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            var record = MakeRecord(levelId: 9, colorCount: 2, stackDepth: 2,
                                    tempSlotCount: 0, tempSlotDepth: 1,
                                    colorStacksJson: "[[1,1],[2,2]]");
            var lds = new StubLds(new[] { true }, record);
            _gsm.InjectLevelDataSystemForTesting(lds);

            var (cleanupFail,   getReason, _)                  = AttachFailSpy();
            var (cleanupLoaded, firedLoaded, _, _, _, _, _, _) = AttachLevelLoadedSpy();

            // Act
            _gsm.LoadLevel(9);

            // Assert — neither event fired
            Assert.IsNull(getReason(),  "OnSessionLoadFailed must NOT fire on EC-09 silent reject");
            Assert.IsFalse(firedLoaded(), "OnLevelLoaded must NOT fire on EC-09 silent reject");

            cleanupFail();
            cleanupLoaded();
        }
    }
}
