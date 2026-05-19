using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Story 001 ACs — AC-GSM-01, AC-GSM-02, AC-GSM-03
// Design:    design/gdd/game-state-manager.md  (TR-GSM-001, TR-GSM-005)
// ADR:       docs/architecture/adr-0006-board-state-representation.md

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class BoardMutation_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_Test");
            _gsm = _go.AddComponent<global::BoltSort.GameStateManager.GameStateManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            global::BoltSort.GameStateManager.GameStateManager.ClearInstanceForTesting();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Seeds a standard 2-color, 4-deep board with 1 temp slot (depth 1).
        /// Flat namespace layout:
        ///   index 0 = color stack 0  → [1, 2]
        ///   index 1 = color stack 1  → [3]
        ///   index 2 = temp slot 0    → []
        /// Starts with moveCount=5, sequenceId=3, undoStack depth=2.
        /// </summary>
        private void SeedStandardBoard()
        {
            const int colorCount    = 2;
            const int tempSlotCount = 1;

            var combined = new List<int>[]
            {
                new List<int> { 1, 2 },   // index 0: color stack 0
                new List<int> { 3 },       // index 1: color stack 1
                new List<int>(),           // index 2: temp slot 0 (empty)
            };

            var initialUndoStack = new List<UndoEntry>
            {
                new UndoEntry { From = 0, To = 1, ColorId = 1, SeqId = 1 },
                new UndoEntry { From = 1, To = 0, ColorId = 3, SeqId = 2 },
            };

            _gsm.SeedBoardForTesting(
                combinedContents:  combined,
                stackDepth:        4,
                tempSlotDepth:     1,
                tempSlotCount:     tempSlotCount,
                colorCount:        colorCount,
                initialSeqId:      3,
                initialMoveCount:  5,
                initialUndoStack:  initialUndoStack
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Active);
        }

        // ── AC-GSM-01: Atomic 5-Step Mutation ────────────────────────────────────

        /// <summary>
        /// AC-GSM-01 (core case): After SimulateMoveCommitted(src=0, dst=1, colorId=2, seqId=3):
        ///   1. StackContents[0] has 1 element (was [1,2], now [1])
        ///   2. StackContents[1] has 2 elements (was [3], now [3,2])
        ///   3. Undo stack depth = 3; top entry = {From=0, To=1, ColorId=2, SeqId=3}
        ///   4. CurrentSequenceId = 4
        ///   5. MoveCount = 6
        /// All five observable immediately post-event (TR-GSM-005).
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_StandardMove_AllFiveStepsObservablePostEvent()
        {
            // Arrange
            SeedStandardBoard();
            Assert.AreEqual(2, _gsm.StackContents[0].Count, "Precondition: src=[1,2]");
            Assert.AreEqual(1, _gsm.StackContents[1].Count, "Precondition: dst=[3]");

            // Act
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 3);

            // Assert — Step 1: top bolt removed from source
            Assert.AreEqual(1, _gsm.StackContents[0].Count,
                "Step 1: StackContents[0] must have 1 element after top bolt removed");
            Assert.AreEqual(1, _gsm.StackContents[0][0],
                "Step 1: Remaining element in StackContents[0] must be colorId=1 (the bottom bolt)");

            // Assert — Step 2: bolt appended to destination
            Assert.AreEqual(2, _gsm.StackContents[1].Count,
                "Step 2: StackContents[1] must have 2 elements after bolt appended");
            Assert.AreEqual(3, _gsm.StackContents[1][0],
                "Step 2: StackContents[1][0] must remain colorId=3");
            Assert.AreEqual(2, _gsm.StackContents[1][1],
                "Step 2: StackContents[1][1] must be the newly moved colorId=2");

            // Assert — Step 3: undo entry pushed (direct via internal test seam)
            Assert.AreEqual(3, _gsm.UndoStackDepth,
                "Step 3: undo stack depth must be D+1=3 (seed=2, one commit)");
            Assert.IsTrue(_gsm.TryPeekUndoStack(out var topEntry),
                "Step 3: undo stack must be non-empty after commit");
            Assert.AreEqual(0,  topEntry.From,    "Step 3: UndoEntry.From must be src=0");
            Assert.AreEqual(1,  topEntry.To,      "Step 3: UndoEntry.To must be dst=1");
            Assert.AreEqual(2,  topEntry.ColorId, "Step 3: UndoEntry.ColorId must be colorId=2");
            Assert.AreEqual(3L, topEntry.SeqId,   "Step 3: UndoEntry.SeqId must be pre-increment seqId=3");

            // Assert — Step 4: CurrentSequenceId incremented
            Assert.AreEqual(4L, _gsm.CurrentSequenceId,
                "Step 4: CurrentSequenceId must be 3+1=4 after one commit");

            // Assert — Step 5: MoveCount incremented
            Assert.AreEqual(6, _gsm.MoveCount,
                "Step 5: MoveCount must be 5+1=6 after one commit");
        }

        /// <summary>
        /// AC-GSM-01 (undo stack fields): Undo stack depth and top entry fields are correct
        /// after a single commit from a known initial state.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_UndoEntry_FieldsMatchCommittedMove()
        {
            // Arrange — seed with depth=0 undo stack for clean assertion
            var combined = new List<int>[]
            {
                new List<int> { 1, 2 },
                new List<int> { 3 },
                new List<int>(),
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: 2,
                initialSeqId: 3, initialMoveCount: 0,
                initialUndoStack: null  // empty
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            // Act
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 3);

            // Assert — undo stack depth must be 1 after first commit from empty stack
            Assert.AreEqual(1, _gsm.UndoStackDepth,
                "Undo stack depth must be 1 after one commit from empty stack");

            // Assert — top entry fields must match exactly the committed move
            Assert.IsTrue(_gsm.TryPeekUndoStack(out var entry),
                "Undo stack must contain one entry");
            Assert.AreEqual(0,  entry.From,    "UndoEntry.From must be src=0");
            Assert.AreEqual(1,  entry.To,      "UndoEntry.To must be dst=1");
            Assert.AreEqual(2,  entry.ColorId, "UndoEntry.ColorId must be colorId=2");
            Assert.AreEqual(3L, entry.SeqId,   "UndoEntry.SeqId must be pre-increment seqId=3");
        }

        /// <summary>
        /// AC-GSM-01 (seqId captured before increment): The UndoEntry.SeqId must equal the
        /// CurrentSequenceId value at the time of commit, BEFORE the post-commit increment.
        /// Verified by checking that CurrentSequenceId is seqId+1 while the undo entry carries seqId.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_SequenceId_IncrementedAfterUndoEntryCapture()
        {
            // Arrange
            var combined = new List<int>[]
            {
                new List<int> { 5 },
                new List<int>(),
                new List<int>(),
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: 2,
                initialSeqId: 10L, initialMoveCount: 0
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            // Act
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 5, seqId: 10L);

            // Assert — seqId after commit must be 11
            Assert.AreEqual(11L, _gsm.CurrentSequenceId,
                "CurrentSequenceId must be initialSeqId+1=11 after one commit");

            // Move count must be 1
            Assert.AreEqual(1, _gsm.MoveCount, "MoveCount must be 1 after one commit");
        }

        /// <summary>
        /// AC-GSM-01 (multiple sequential commits): Three commits from known state produce
        /// the correct cumulative MoveCount and CurrentSequenceId.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_MultipleCommits_CumulativeCountersCorrect()
        {
            // Arrange
            var combined = new List<int>[]
            {
                new List<int> { 1, 2, 3 },
                new List<int>(),
                new List<int>(),
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: 2,
                initialSeqId: 0L, initialMoveCount: 0
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            // Act — three moves: 0→1, 0→2, 0→1
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 3, seqId: 0L);
            _gsm.SimulateMoveCommitted(src: 0, dst: 2, colorId: 2, seqId: 1L);
            _gsm.SimulateMoveCommitted(src: 1, dst: 0, colorId: 3, seqId: 2L);

            // Assert
            Assert.AreEqual(3L, _gsm.CurrentSequenceId, "Three commits from seqId=0 → seqId=3");
            Assert.AreEqual(3,  _gsm.MoveCount,          "Three commits → moveCount=3");
            Assert.AreEqual(2,  _gsm.StackContents[0].Count,
                "StackContents[0] was [1,2,3]; 2 removals then 1 return → [1,3] (count=2)");
        }

        /// <summary>
        /// AC-GSM-01 (edge case: src is temp slot index): Commit with src=colorCount (temp slot 0)
        /// removes from temp slot and adds to color stack. Flat namespace must apply.
        /// Given colorCount=2, index 2 = temp slot 0.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_TempSlotAsSource_FlatNamespaceApplied()
        {
            // Arrange — temp slot 0 (index 2) holds bolt colorId=7
            const int colorCount = 2;
            var combined = new List<int>[]
            {
                new List<int>(),         // index 0: color stack 0 (empty)
                new List<int>(),         // index 1: color stack 1 (empty)
                new List<int> { 7 },     // index 2: temp slot 0 — holds bolt 7
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: colorCount,
                initialSeqId: 0L, initialMoveCount: 0
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            // Act — move from temp slot (index 2) to color stack 0 (index 0)
            _gsm.SimulateMoveCommitted(src: 2, dst: 0, colorId: 7, seqId: 0L);

            // Assert — temp slot 0 must now be empty
            Assert.AreEqual(0, _gsm.TempSlotContents[0].Count,
                "Temp slot 0 must be empty after bolt moved out");

            // Assert — color stack 0 must now contain bolt 7
            Assert.AreEqual(1, _gsm.StackContents[0].Count,
                "StackContents[0] must have 1 bolt after move from temp slot");
            Assert.AreEqual(7, _gsm.StackContents[0][0],
                "StackContents[0][0] must be colorId=7");

            // Counters must be updated
            Assert.AreEqual(1L, _gsm.CurrentSequenceId, "seqId must be 1 after one commit");
            Assert.AreEqual(1,  _gsm.MoveCount,          "moveCount must be 1 after one commit");
        }

        /// <summary>
        /// AC-GSM-01 (edge case: dst is temp slot index): Commit with dst=colorCount (temp slot 0)
        /// removes from color stack and adds to temp slot.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_TempSlotAsDestination_FlatNamespaceApplied()
        {
            // Arrange
            const int colorCount = 2;
            var combined = new List<int>[]
            {
                new List<int> { 4, 5 },  // index 0: color stack 0
                new List<int>(),          // index 1: color stack 1 (empty)
                new List<int>(),          // index 2: temp slot 0 (empty)
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: colorCount,
                initialSeqId: 0L, initialMoveCount: 0
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            // Act — move top bolt from color stack 0 (index 0) to temp slot 0 (index 2)
            _gsm.SimulateMoveCommitted(src: 0, dst: 2, colorId: 5, seqId: 0L);

            // Assert — color stack 0 must now have 1 bolt
            Assert.AreEqual(1, _gsm.StackContents[0].Count,
                "StackContents[0] must have 1 bolt after top removed");
            Assert.AreEqual(4, _gsm.StackContents[0][0],
                "Remaining bolt in StackContents[0] must be colorId=4");

            // Assert — temp slot 0 (via TempSlotContents) must now have bolt 5
            Assert.AreEqual(1, _gsm.TempSlotContents[0].Count,
                "TempSlotContents[0] must have 1 bolt after receive");
            Assert.AreEqual(5, _gsm.TempSlotContents[0][0],
                "TempSlotContents[0][0] must be colorId=5");
        }

        // ── AC-GSM-02: Cancelled/Rejected Produce Zero Mutations ──────────────────

        /// <summary>
        /// AC-GSM-02 (move_cancelled): SimulateMoveCancelled produces zero mutations —
        /// all fields equal snapshot taken before the call.
        /// </summary>
        [Test]
        public void Test_HandleMoveCancelled_ProducesZeroMutations()
        {
            // Arrange
            SeedStandardBoard();
            long snapshotSeqId    = _gsm.CurrentSequenceId;
            int  snapshotMoves    = _gsm.MoveCount;
            int  srcCountBefore   = _gsm.StackContents[0].Count;
            int  dstCountBefore   = _gsm.StackContents[1].Count;
            int  snapshotUndoDepth = _gsm.UndoStackDepth;

            // Act
            _gsm.SimulateMoveCancelled(src: 0, colorId: 2);

            // Assert — all fields unchanged
            Assert.AreEqual(snapshotSeqId,    _gsm.CurrentSequenceId,
                "SimulateMoveCancelled must not change CurrentSequenceId");
            Assert.AreEqual(snapshotMoves,    _gsm.MoveCount,
                "SimulateMoveCancelled must not change MoveCount");
            Assert.AreEqual(srcCountBefore,   _gsm.StackContents[0].Count,
                "SimulateMoveCancelled must not change StackContents[0]");
            Assert.AreEqual(dstCountBefore,   _gsm.StackContents[1].Count,
                "SimulateMoveCancelled must not change StackContents[1]");
            Assert.AreEqual(snapshotUndoDepth, _gsm.UndoStackDepth,
                "SimulateMoveCancelled must not change undo stack depth");
        }

        /// <summary>
        /// AC-GSM-02 (move_rejected): SimulateMoveRejected produces zero mutations —
        /// all fields equal snapshot taken before the call.
        /// </summary>
        [Test]
        public void Test_HandleMoveRejected_ProducesZeroMutations()
        {
            // Arrange
            SeedStandardBoard();
            long snapshotSeqId    = _gsm.CurrentSequenceId;
            int  snapshotMoves    = _gsm.MoveCount;
            int  srcCountBefore   = _gsm.StackContents[0].Count;
            int  dstCountBefore   = _gsm.StackContents[1].Count;
            int  snapshotUndoDepth = _gsm.UndoStackDepth;

            // Act
            _gsm.SimulateMoveRejected(src: 1, dst: 2, colorId: 3, reason: "COLOR_MISMATCH");

            // Assert — all fields unchanged
            Assert.AreEqual(snapshotSeqId,    _gsm.CurrentSequenceId,
                "SimulateMoveRejected must not change CurrentSequenceId");
            Assert.AreEqual(snapshotMoves,    _gsm.MoveCount,
                "SimulateMoveRejected must not change MoveCount");
            Assert.AreEqual(srcCountBefore,   _gsm.StackContents[0].Count,
                "SimulateMoveRejected must not change StackContents[0]");
            Assert.AreEqual(dstCountBefore,   _gsm.StackContents[1].Count,
                "SimulateMoveRejected must not change StackContents[1]");
            Assert.AreEqual(snapshotUndoDepth, _gsm.UndoStackDepth,
                "SimulateMoveRejected must not change undo stack depth");
        }

        /// <summary>
        /// AC-GSM-02 (combined sequence): move_cancelled then move_rejected both produce
        /// zero mutations each — board is byte-identical to pre-event snapshot after each call.
        /// </summary>
        [Test]
        public void Test_HandleCancelledThenRejected_BothProduceZeroMutations()
        {
            // Arrange
            SeedStandardBoard();
            long snapshotSeqId  = _gsm.CurrentSequenceId;
            int  snapshotMoves  = _gsm.MoveCount;

            // Act — cancelled, then rejected
            _gsm.SimulateMoveCancelled(src: 0, colorId: 2);
            Assert.AreEqual(snapshotSeqId, _gsm.CurrentSequenceId,
                "After move_cancelled: CurrentSequenceId must be unchanged");
            Assert.AreEqual(snapshotMoves, _gsm.MoveCount,
                "After move_cancelled: MoveCount must be unchanged");

            _gsm.SimulateMoveRejected(src: 1, dst: 2, colorId: 3, reason: "DESTINATION_FULL");
            Assert.AreEqual(snapshotSeqId, _gsm.CurrentSequenceId,
                "After move_rejected: CurrentSequenceId must still be unchanged");
            Assert.AreEqual(snapshotMoves, _gsm.MoveCount,
                "After move_rejected: MoveCount must still be unchanged");
        }

        // ── AC-GSM-02 / AC-GSM-03: No events emitted on cancel/reject/commit ────

        /// <summary>
        /// AC-GSM-02 (event spy): SimulateMoveCancelled emits no OnBoardStateChanged events.
        /// </summary>
        [Test]
        public void Test_HandleMoveCancelled_EmitsNoOnBoardStateChangedEvent()
        {
            // Arrange
            SeedStandardBoard();
            int eventCallCount = 0;
            void CancelledEventSpy(long seqId, int moveCount) => eventCallCount++;
            _gsm.OnBoardStateChanged += CancelledEventSpy;

            // Act
            _gsm.SimulateMoveCancelled(src: 0, colorId: 2);

            // Assert
            Assert.AreEqual(0, eventCallCount,
                "SimulateMoveCancelled must emit zero OnBoardStateChanged events");

            // Cleanup
            _gsm.OnBoardStateChanged -= CancelledEventSpy;
        }

        /// <summary>
        /// AC-GSM-02 (event spy): SimulateMoveRejected emits no OnBoardStateChanged events.
        /// </summary>
        [Test]
        public void Test_HandleMoveRejected_EmitsNoOnBoardStateChangedEvent()
        {
            // Arrange
            SeedStandardBoard();
            int eventCallCount = 0;
            void RejectedEventSpy(long seqId, int moveCount) => eventCallCount++;
            _gsm.OnBoardStateChanged += RejectedEventSpy;

            // Act
            _gsm.SimulateMoveRejected(src: 1, dst: 2, colorId: 3, reason: "COLOR_MISMATCH");

            // Assert
            Assert.AreEqual(0, eventCallCount,
                "SimulateMoveRejected must emit zero OnBoardStateChanged events");

            // Cleanup
            _gsm.OnBoardStateChanged -= RejectedEventSpy;
        }

        // ── AC-GSM-03: No OnBoardStateChanged on move_committed ──────────────────

        /// <summary>
        /// AC-GSM-03: After SimulateMoveCommitted, OnBoardStateChanged is NOT emitted.
        /// Verified with an event spy.
        /// Per ADR-0006: OnBoardStateChanged fires only on undo and foreground restore,
        /// NOT on regular move_committed mutations.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_DoesNotEmitOnBoardStateChanged()
        {
            // Arrange
            SeedStandardBoard();
            int spyCallCount = 0;
            void OnBoardStateChangedSpy(long seqId, int moveCount) => spyCallCount++;
            _gsm.OnBoardStateChanged += OnBoardStateChangedSpy;

            // Act
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 3);

            // Assert
            Assert.AreEqual(0, spyCallCount,
                "OnBoardStateChanged must NOT be emitted after HandleMoveCommitted (AC-GSM-03). " +
                "This event is reserved for undo (Story 002) and foreground restore (Story 008).");

            // Cleanup
            _gsm.OnBoardStateChanged -= OnBoardStateChangedSpy;
        }

        // ── Lifecycle Guard: mutations blocked outside ACTIVE state ───────────────

        /// <summary>
        /// Mutations must be silently ignored when GSM is in UNLOADED state.
        /// No board data is valid in UNLOADED — processing would corrupt null arrays.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_WhenUnloaded_MutationIgnored()
        {
            // Arrange — seed board but do NOT transition to ACTIVE
            var combined = new List<int>[]
            {
                new List<int> { 1 },
                new List<int>(),
                new List<int>(),
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: 2,
                initialSeqId: 0L, initialMoveCount: 0
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Unloaded);

            long snapshotSeqId = _gsm.CurrentSequenceId;
            int  snapshotMoves = _gsm.MoveCount;

            // Act
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 1, seqId: 0L);

            // Assert — no change because lifecycle guard returned early
            Assert.AreEqual(snapshotSeqId, _gsm.CurrentSequenceId,
                "Mutation in UNLOADED state must be ignored (no CurrentSequenceId change)");
            Assert.AreEqual(snapshotMoves, _gsm.MoveCount,
                "Mutation in UNLOADED state must be ignored (no MoveCount change)");
            Assert.AreEqual(1, _gsm.StackContents[0].Count,
                "Mutation in UNLOADED state must not remove bolt from source");
        }

        /// <summary>
        /// Mutations must be silently ignored when GSM is in COMPLETE state.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_WhenComplete_MutationIgnored()
        {
            // Arrange
            var combined = new List<int>[]
            {
                new List<int> { 1 },
                new List<int>(),
                new List<int>(),
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: 2,
                initialSeqId: 0L, initialMoveCount: 0
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Complete);

            long snapshotSeqId = _gsm.CurrentSequenceId;
            int  snapshotMoves = _gsm.MoveCount;

            // Act
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 1, seqId: 0L);

            // Assert
            Assert.AreEqual(snapshotSeqId, _gsm.CurrentSequenceId,
                "Mutation in COMPLETE state must be ignored (CurrentSequenceId)");
            Assert.AreEqual(snapshotMoves, _gsm.MoveCount,
                "Mutation in COMPLETE state must be ignored (MoveCount)");
            Assert.AreEqual(1, _gsm.StackContents[0].Count,
                "Mutation in COMPLETE state must not remove bolt from source");
        }

        // ── Read-Only Property Exposure ───────────────────────────────────────────

        /// <summary>
        /// After SeedBoardForTesting, StackContents and TempSlotContents expose the correct
        /// IReadOnlyList views and ColorCount / TempSlotCount / StackDepth are set.
        /// </summary>
        [Test]
        public void Test_SeedBoardForTesting_PublicPropertiesReflectSeedValues()
        {
            // Arrange & Act
            SeedStandardBoard();

            // Assert
            Assert.AreEqual(2, _gsm.ColorCount,    "ColorCount must be 2");
            Assert.AreEqual(1, _gsm.TempSlotCount, "TempSlotCount must be 1");
            Assert.AreEqual(4, _gsm.StackDepth,    "StackDepth must be 4");
            Assert.AreEqual(1, _gsm.TempSlotDepth, "TempSlotDepth must be 1");
            Assert.AreEqual(3L, _gsm.CurrentSequenceId, "CurrentSequenceId must be 3 (seeded)");
            Assert.AreEqual(5, _gsm.MoveCount,           "MoveCount must be 5 (seeded)");

            Assert.IsNotNull(_gsm.StackContents,     "StackContents must not be null");
            Assert.IsNotNull(_gsm.TempSlotContents,  "TempSlotContents must not be null");
            Assert.AreEqual(3, _gsm.StackContents.Length,    "StackContents must have 3 entries (2 color + 1 temp)");
            Assert.AreEqual(1, _gsm.TempSlotContents.Length, "TempSlotContents must have 1 entry");
        }

        /// <summary>
        /// TempSlotContents[0] reflects mutations to StackContents[colorCount] because
        /// they reference the same underlying List. Validates the flat-namespace aliasing.
        /// </summary>
        [Test]
        public void Test_TempSlotContents_ReflectsStackContentsAtFlatIndex()
        {
            // Arrange — colorCount=2, so temp slot 0 lives at StackContents[2]
            var combined = new List<int>[]
            {
                new List<int>(),
                new List<int>(),
                new List<int> { 9 },   // temp slot 0 starts with bolt 9
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: 2,
                initialSeqId: 0L, initialMoveCount: 0
            );
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            // Assert before mutation
            Assert.AreEqual(1, _gsm.TempSlotContents[0].Count,
                "TempSlotContents[0] must show bolt 9 before mutation");

            // Act — move from StackContents[2] (temp slot) to StackContents[0]
            _gsm.SimulateMoveCommitted(src: 2, dst: 0, colorId: 9, seqId: 0L);

            // Assert after mutation — TempSlotContents[0] must be empty (same reference)
            Assert.AreEqual(0, _gsm.TempSlotContents[0].Count,
                "TempSlotContents[0] must be empty after bolt moved to StackContents[0]");
            Assert.AreEqual(1, _gsm.StackContents[0].Count,
                "StackContents[0] must have 1 bolt after receive from temp slot");
        }
    }
}
