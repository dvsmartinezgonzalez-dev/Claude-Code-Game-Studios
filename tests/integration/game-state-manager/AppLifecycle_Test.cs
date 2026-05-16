using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Integration.GameStateManager.asmdef
// Covers:    Story 008 ACs — SER-01, SER-02, SER-03, EC-06, L-08, L-08-LOADING
// Design:    design/gdd/game-state-manager.md  (TR-GSM-008, TR-GSM-011, SER rules, EC-06, L-08)
// ADRs:      docs/architecture/ADR-0001.md, ADR-0003.md, ADR-0006.md

namespace BoltSort.Tests.Integration.GameStateManager
{
    [TestFixture]
    public class AppLifecycle_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;
        private BoardSnapshotStub _saveStub;

        // ── Stub ──────────────────────────────────────────────────────────────────

        /// <summary>In-memory IBoardSnapshotSystem stub for integration tests.</summary>
        private class BoardSnapshotStub : IBoardSnapshotSystem
        {
            public BoardSnapshot LastWritten  { get; private set; }
            public BoardSnapshot SnapshotToReturn { get; set; }
            public int WriteCount { get; private set; }
            public int ReadCount  { get; private set; }

            public void WriteBoardSnapshotSync(BoardSnapshot snapshot)
            {
                LastWritten = snapshot;
                WriteCount++;
            }

            public BoardSnapshot ReadBoardSnapshot()
            {
                ReadCount++;
                return SnapshotToReturn;
            }
        }

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_AppLifecycleTest");
            _gsm = _go.AddComponent<global::BoltSort.GameStateManager.GameStateManager>();

            _saveStub = new BoardSnapshotStub();
            _gsm.InjectBoardSnapshotSystemForTesting(_saveStub);

            // Standard board: 2 colors, 2-deep stacks; flat namespace [stack0, stack1]
            // Initial state: [[1,2],[2,1]] — both full
            var stacks = new List<int>[]
            {
                new List<int> { 1, 2 },  // stack 0
                new List<int> { 2, 1 },  // stack 1
            };
            _gsm.SeedBoardForTesting(
                combinedContents: stacks,
                stackDepth:    2,
                tempSlotDepth: 1,
                tempSlotCount: 0,
                colorCount:    2,
                initialSeqId:  5,
                initialMoveCount: 2);
            _gsm.SeedLevelForTesting(levelId: 42);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            global::BoltSort.GameStateManager.GameStateManager.ClearInstanceForTesting();
        }

        // ── SER-01: Board state serialized on background ──────────────────────────

        [Test]
        public void Test_SER01_OnPause_WritesSnapshotToSaveSystem()
        {
            // Act
            _gsm.SimulateApplicationPause(true);

            // Assert
            Assert.AreEqual(1, _saveStub.WriteCount, "WriteBoardSnapshotSync must be called once on pause");
            Assert.IsNotNull(_saveStub.LastWritten,   "Snapshot must not be null");
        }

        [Test]
        public void Test_SER01_OnPause_SnapshotContainsAllRequiredFields()
        {
            // Act
            _gsm.SimulateApplicationPause(true);

            var snap = _saveStub.LastWritten;

            // Assert all 10 required fields (SER-01)
            Assert.AreEqual(42,       snap.LevelId,       "LevelId");
            Assert.AreEqual(2,        snap.ColorCount,     "ColorCount");
            Assert.AreEqual(2,        snap.StackDepth,     "StackDepth");
            Assert.AreEqual(0,        snap.TempSlotCount,  "TempSlotCount");
            Assert.AreEqual(1,        snap.TempSlotDepth,  "TempSlotDepth");
            Assert.AreEqual(2,        snap.MoveCount,      "MoveCount");
            Assert.AreEqual(5L,       snap.SequenceId,     "SequenceId");
            Assert.AreEqual("Active", snap.GsmState,       "GsmState");
            Assert.IsNotNull(snap.StackContents,           "StackContents must be present");
            Assert.IsNotNull(snap.TempSlotContents,        "TempSlotContents must be present");
        }

        [Test]
        public void Test_SER01_OnPause_SnapshotExcludesUndoStack()
        {
            // Arrange — seed a board with undo entries to confirm they aren't serialized
            var undoStack = new List<UndoEntry>
            {
                new UndoEntry { From = 0, To = 1, ColorId = 1, SeqId = 3 },
                new UndoEntry { From = 1, To = 0, ColorId = 2, SeqId = 4 },
            };
            var stacks = new List<int>[]
            {
                new List<int> { 1, 2 },
                new List<int> { 2, 1 },
            };
            _gsm.SeedBoardForTesting(stacks, 2, 1, 0, 2, initialUndoStack: undoStack);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            // Act
            _gsm.SimulateApplicationPause(true);

            // Assert — BoardSnapshot has no undo stack field; WriteCount confirms one call
            Assert.AreEqual(1, _saveStub.WriteCount);
            // Undo stack is excluded by design — BoardSnapshot class has no such property (compile-time guarantee)
        }

        [Test]
        public void Test_SER01_OnPause_WhenUnloaded_DoesNotWrite()
        {
            // Arrange
            _gsm.SetStateForTesting(GSMLifecycleState.Unloaded);

            // Act
            _gsm.SimulateApplicationPause(true);

            // Assert — not in ACTIVE or COMPLETE; no write
            Assert.AreEqual(0, _saveStub.WriteCount, "No write when GSM is in UNLOADED state");
        }

        [Test]
        public void Test_SER01_OnPause_WhenComplete_WritesSnapshot()
        {
            // Arrange
            _gsm.SetStateForTesting(GSMLifecycleState.Complete);

            // Act
            _gsm.SimulateApplicationPause(true);

            // Assert — COMPLETE state also serializes board
            Assert.AreEqual(1, _saveStub.WriteCount, "Snapshot must be written when GSM is in COMPLETE state");
            Assert.AreEqual("Complete", _saveStub.LastWritten.GsmState, "GsmState must reflect Complete");
        }

        // ── SER-02: Foreground restore from valid ACTIVE snapshot ─────────────────

        [Test]
        public void Test_SER02_OnResume_RestoresBoardState()
        {
            // Arrange — build snapshot from current board, then change board so we can detect restore
            _gsm.SimulateApplicationPause(true);
            var savedSnapshot = _saveStub.LastWritten;

            // Corrupt the in-memory board to prove restore works
            _gsm.SeedBoardForTesting(
                new List<int>[] { new List<int>(), new List<int>() },
                stackDepth: 2, tempSlotDepth: 1, tempSlotCount: 0, colorCount: 2);
            _gsm.SetStateForTesting(GSMLifecycleState.Unloaded);

            _saveStub.SnapshotToReturn = savedSnapshot;

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert — board restored to saved state
            Assert.AreEqual(2, _gsm.StackContents[0].Count, "stack 0 must have 2 bolts after restore");
            Assert.AreEqual(2, _gsm.StackContents[1].Count, "stack 1 must have 2 bolts after restore");
            Assert.AreEqual(5L, _gsm.CurrentSequenceId, "SequenceId must match snapshot");
            Assert.AreEqual(2,  _gsm.MoveCount,         "MoveCount must match snapshot");
        }

        [Test]
        public void Test_SER02_OnResume_EmitsBoardStateChanged()
        {
            // Arrange
            _gsm.SimulateApplicationPause(true);
            _saveStub.SnapshotToReturn = _saveStub.LastWritten;
            bool boardStateChangedFired = false;
            _gsm.OnBoardStateChanged += (_, __) => boardStateChangedFired = true;

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert
            Assert.IsTrue(boardStateChangedFired, "OnBoardStateChanged must be emitted on foreground restore");
        }

        [Test]
        public void Test_SER02_OnResume_GsmStaysActive()
        {
            // Arrange
            _gsm.SimulateApplicationPause(true);
            _saveStub.SnapshotToReturn = _saveStub.LastWritten;

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert
            Assert.AreEqual(GSMLifecycleState.Active, _gsm.LifecycleState,
                "GSM must remain in ACTIVE state after successful restore");
        }

        [Test]
        public void Test_SER02_OnResume_UndoStackEmptyAfterRestore()
        {
            // Arrange — seed undo entries, pause, then restore
            var undoStack = new List<UndoEntry>
            {
                new UndoEntry { From = 0, To = 1, ColorId = 1, SeqId = 3 },
            };
            var stacks = new List<int>[] { new List<int> { 1, 2 }, new List<int> { 2, 1 } };
            _gsm.SeedBoardForTesting(stacks, 2, 1, 0, 2, initialUndoStack: undoStack);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            _gsm.SimulateApplicationPause(true);
            _saveStub.SnapshotToReturn = _saveStub.LastWritten;
            _gsm.SimulateApplicationPause(false);

            // Assert — undo stack is session-only; must be empty after restore
            Assert.AreEqual(0, _gsm.UndoStackDepth,
                "Undo stack must be empty after restore — session-only, never serialized");
        }

        // ── SER-03: Deserialization failure ───────────────────────────────────────

        [Test]
        public void Test_SER03_NullSnapshot_EmitsSessionLoadFailed()
        {
            // Arrange
            _saveStub.SnapshotToReturn = null;
            GsmSessionLoadFailReason? receivedReason = null;
            _gsm.OnSessionLoadFailed += (reason, _) => receivedReason = reason;

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert
            Assert.AreEqual(GsmSessionLoadFailReason.SaveCorrupt, receivedReason,
                "OnSessionLoadFailed must fire with SaveCorrupt when snapshot is null");
        }

        [Test]
        public void Test_SER03_InvalidSnapshot_GsmToUnloaded()
        {
            // Arrange
            _saveStub.SnapshotToReturn = new BoardSnapshot { IsValid = false, LevelId = 42 };

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState,
                "GSM must transition to UNLOADED after corrupt restore (SER-03)");
        }

        [Test]
        public void Test_SER03_InvalidSnapshot_BoardStateCleared()
        {
            // Arrange
            _saveStub.SnapshotToReturn = new BoardSnapshot { IsValid = false };

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert — partial state cleared
            Assert.IsNull(_gsm.StackContents,     "StackContents must be null after SER-03 failure");
            Assert.AreEqual(0, _gsm.MoveCount,    "MoveCount must be 0 after SER-03 failure");
        }

        // ── EC-06: Sort Mechanic was in BOLT_SELECTED on background ───────────────

        [Test]
        public void Test_EC06_WasInBoltSelected_IncrementsSeqIdOnRestore()
        {
            // Arrange — pause with BOLT_SELECTED flag set
            _gsm.SetSortMechanicWasInBoltSelectedForTesting(true);
            _gsm.SimulateApplicationPause(true);

            var snap = _saveStub.LastWritten;
            Assert.IsTrue(snap.WasInBoltSelected); // sanity — flag serialized
            _saveStub.SnapshotToReturn = snap;

            long seqInSnapshot = snap.SequenceId; // 5

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert — EC-06: seqId incremented to invalidate held bolt state
            Assert.AreEqual(seqInSnapshot + 1, _gsm.CurrentSequenceId,
                "CurrentSequenceId must be snapshot.SequenceId+1 when WasInBoltSelected=true");
        }

        [Test]
        public void Test_EC06_NotInBoltSelected_SeqIdUnchangedOnRestore()
        {
            // Arrange — normal pause without BOLT_SELECTED
            _gsm.SimulateApplicationPause(true);
            _saveStub.SnapshotToReturn = _saveStub.LastWritten;
            long seqInSnapshot = _saveStub.LastWritten.SequenceId;

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert — seqId must equal snapshot value (no increment)
            Assert.AreEqual(seqInSnapshot, _gsm.CurrentSequenceId,
                "CurrentSequenceId must equal snapshot value when WasInBoltSelected=false");
        }

        // ── L-08: ExitLevel from ACTIVE ───────────────────────────────────────────

        [Test]
        public void Test_L08_ExitLevel_FromActive_ClearsBoardArrays()
        {
            // Act
            _gsm.ExitLevel();

            // Assert — board arrays cleared
            Assert.IsNull(_gsm.StackContents,    "StackContents must be null after ExitLevel");
            Assert.IsNull(_gsm.TempSlotContents, "TempSlotContents must be null after ExitLevel");
        }

        [Test]
        public void Test_L08_ExitLevel_FromActive_ClearsCounters()
        {
            // Act
            _gsm.ExitLevel();

            // Assert
            Assert.AreEqual(0, _gsm.MoveCount,        "MoveCount must be 0 after teardown");
            Assert.AreEqual(0, _gsm.UndoStackDepth,   "Undo stack must be empty after teardown");
        }

        [Test]
        public void Test_L08_ExitLevel_FromActive_EmitsLevelUnloaded_WithLevelId()
        {
            // Arrange
            int? receivedLevelId = -1;
            _gsm.OnLevelUnloaded += id => receivedLevelId = id;

            // Act
            _gsm.ExitLevel();

            // Assert
            Assert.AreEqual(42, receivedLevelId,
                "OnLevelUnloaded must carry the active level ID");
        }

        [Test]
        public void Test_L08_ExitLevel_FromActive_GsmToUnloaded()
        {
            // Act
            _gsm.ExitLevel();

            // Assert
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState,
                "GSM must be in UNLOADED state after ExitLevel");
        }

        [Test]
        public void Test_L08_ExitLevel_FromComplete_EmitsLevelUnloaded_WithLevelId()
        {
            // Arrange
            _gsm.SetStateForTesting(GSMLifecycleState.Complete);
            int? receivedLevelId = -1;
            _gsm.OnLevelUnloaded += id => receivedLevelId = id;

            // Act
            _gsm.ExitLevel();

            // Assert
            Assert.AreEqual(42, receivedLevelId,
                "OnLevelUnloaded must carry level ID when exiting from COMPLETE state");
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState);
        }

        [Test]
        public void Test_L08_ExitLevel_AllowsSubsequentLoadLevel()
        {
            // Arrange — ExitLevel returns GSM to UNLOADED; LoadLevel must be accepted
            _gsm.ExitLevel();
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState);

            // Act — attempt LoadLevel (will fail at L-01 with no LDS, but must not be silently rejected)
            bool levelLoadedFired = false;
            bool sessionLoadFailedFired = false;
            _gsm.OnLevelLoaded        += (_, __, ___, ____, _____, ______) => levelLoadedFired = true;
            _gsm.OnSessionLoadFailed  += (_, __) => sessionLoadFailedFired = true;

            _gsm.LoadLevel(1);

            // Assert — either succeeds (ACTIVE) or fails (UNLOADED + OnSessionLoadFailed) — not silently ignored
            bool wasHandled = levelLoadedFired || sessionLoadFailedFired;
            Assert.IsTrue(wasHandled,
                "LoadLevel must be processed after ExitLevel — GSM is in UNLOADED state");
        }

        // ── L-08-LOADING: ExitLevel during LOADING cancels load ───────────────────

        [Test]
        public void Test_L08Loading_ExitLevel_EmitsLevelUnloadedWithNull()
        {
            // Arrange — inject LOADING state (simulates mid-load cancellation)
            _gsm.SetStateForTesting(GSMLifecycleState.Loading);
            int? receivedLevelId = -99; // sentinel
            _gsm.OnLevelUnloaded += id => receivedLevelId = id;

            // Act
            _gsm.ExitLevel();

            // Assert — null because level was never confirmed during LOADING
            Assert.IsNull(receivedLevelId,
                "OnLevelUnloaded must carry null levelId when cancelling during LOADING");
        }

        [Test]
        public void Test_L08Loading_ExitLevel_GsmToUnloaded_NoOtherEvents()
        {
            // Arrange
            _gsm.SetStateForTesting(GSMLifecycleState.Loading);
            bool levelLoadedFired       = false;
            bool sessionLoadFailedFired = false;
            _gsm.OnLevelLoaded       += (_, __, ___, ____, _____, ______) => levelLoadedFired = true;
            _gsm.OnSessionLoadFailed += (_, __) => sessionLoadFailedFired = true;

            // Act
            _gsm.ExitLevel();

            // Assert
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState);
            Assert.IsFalse(levelLoadedFired,       "OnLevelLoaded must NOT fire on LOADING cancellation");
            Assert.IsFalse(sessionLoadFailedFired, "OnSessionLoadFailed must NOT fire on LOADING cancellation");
        }

        [Test]
        public void Test_L08_ExitLevel_FromUnloaded_IsNoOp()
        {
            // Arrange
            _gsm.SetStateForTesting(GSMLifecycleState.Unloaded);
            bool levelUnloadedFired = false;
            _gsm.OnLevelUnloaded += _ => levelUnloadedFired = true;

            // Act
            _gsm.ExitLevel();

            // Assert — no-op; no event, still UNLOADED
            Assert.IsFalse(levelUnloadedFired, "OnLevelUnloaded must NOT fire when already UNLOADED");
            Assert.AreEqual(GSMLifecycleState.Unloaded, _gsm.LifecycleState);
        }

        // ── S-1: Null save system is a silent no-op on pause ──────────────────────

        [Test]
        public void Test_SER01_NullSaveSystem_InActive_NoThrowNoWrite()
        {
            // Arrange — remove the injected stub so _boardSnapshotSystem is null
            _gsm.InjectBoardSnapshotSystemForTesting(null);

            // Act + Assert — must not throw; null-conditional write swallows it silently
            Assert.DoesNotThrow(() => _gsm.SimulateApplicationPause(true),
                "OnApplicationPause(true) must not throw when _boardSnapshotSystem is null");
            Assert.AreEqual(0, _saveStub.WriteCount,
                "No write must occur when _boardSnapshotSystem is null");
        }

        // ── S-2: Complete-state snapshot restores to Complete ─────────────────────

        [Test]
        public void Test_SER02_OnResume_RestoresCompleteState()
        {
            // Arrange — build a Complete snapshot
            _gsm.SetStateForTesting(GSMLifecycleState.Complete);
            _gsm.SimulateApplicationPause(true);

            var completedSnap = _saveStub.LastWritten;
            Assert.AreEqual("Complete", completedSnap.GsmState, "Snapshot must record Complete state");

            // Wipe board to prove restore works
            _gsm.SeedBoardForTesting(
                new List<int>[] { new List<int>(), new List<int>() },
                stackDepth: 2, tempSlotDepth: 1, tempSlotCount: 0, colorCount: 2);
            _gsm.SetStateForTesting(GSMLifecycleState.Unloaded);
            _saveStub.SnapshotToReturn = completedSnap;

            // Act
            _gsm.SimulateApplicationPause(false);

            // Assert — must restore to Complete, not Active
            Assert.AreEqual(GSMLifecycleState.Complete, _gsm.LifecycleState,
                "GSM must restore to COMPLETE state when snapshot.GsmState is \"Complete\"");
        }
    }
}
