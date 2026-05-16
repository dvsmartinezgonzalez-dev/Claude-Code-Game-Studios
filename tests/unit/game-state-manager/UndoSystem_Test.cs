using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Story 002 ACs — AC-GSM-04, AC-GSM-05, AC-GSM-06, AC-GSM-07, AC-GSM-19
// Design:    design/gdd/game-state-manager.md  (TR-GSM-002, TR-GSM-003, TR-GSM-005)
// ADR:       docs/architecture/adr-0006-board-state-representation.md

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class UndoSystem_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_UndoTest");
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
        /// Seeds a 2-color, 4-deep board with 1 temp slot (depth 1) and activates it.
        /// Flat namespace: index 0 = color stack 0, index 1 = color stack 1, index 2 = temp slot 0.
        /// </summary>
        private void SeedAndActivate(List<int>[] combinedContents,
                                     long initialSeqId       = 0,
                                     int  initialMoveCount   = 0,
                                     List<UndoEntry> initialUndoStack = null)
        {
            _gsm.SeedBoardForTesting(
                combinedContents:  combinedContents,
                stackDepth:        4,
                tempSlotDepth:     1,
                tempSlotCount:     1,
                colorCount:        2,
                initialSeqId:      initialSeqId,
                initialMoveCount:  initialMoveCount,
                initialUndoStack:  initialUndoStack);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);
        }

        // ── AC-GSM-04: Undo reverts board state and increments seqId ─────────────

        /// <summary>
        /// AC-GSM-04 (core case): After two move_committed events, UndoRequested reverts the
        /// last move — removes the moved bolt from its destination and returns it to source.
        /// seqId is incremented, move_count decremented, board_state_changed emitted with
        /// the new seqId and move_count.
        /// </summary>
        [Test]
        public void Test_UndoRequested_AfterTwoCommits_RevertsLastMove()
        {
            // Arrange — build board via SimulateMoveCommitted to get a clean undo stack
            var combined = new List<int>[]
            {
                new List<int> { 1, 2 },  // index 0: color stack 0
                new List<int> { 3 },      // index 1: color stack 1
                new List<int>(),          // index 2: temp slot 0
            };
            SeedAndActivate(combined, initialSeqId: 0, initialMoveCount: 0);

            // Move A: src=0, dst=1, colorId=2 (top of stack0) → stack0=[1], stack1=[3,2]
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 0L);
            // Move B: src=1, dst=2, colorId=2 (top of stack1) → stack1=[3], stack2=[2]
            _gsm.SimulateMoveCommitted(src: 1, dst: 2, colorId: 2, seqId: 1L);

            Assert.AreEqual(2L, _gsm.CurrentSequenceId, "Precondition: seqId=2 after two commits");
            Assert.AreEqual(2,  _gsm.MoveCount,          "Precondition: moveCount=2 after two commits");

            // Capture board_state_changed payload
            long emittedSeqId   = -1;
            int  emittedMoveCount = -1;
            void BoardStateChangedSpy(long s, int m) { emittedSeqId = s; emittedMoveCount = m; }
            _gsm.OnBoardStateChanged += BoardStateChangedSpy;

            // Act
            _gsm.UndoRequested();

            // Assert — move B reverted: colorId=2 removed from stack2, returned to stack1
            Assert.AreEqual(0, _gsm.StackContents[2].Count,
                "stack2 must be empty after undoing move B");
            Assert.AreEqual(2, _gsm.StackContents[1].Count,
                "stack1 must have 2 bolts after undoing move B");
            Assert.AreEqual(3, _gsm.StackContents[1][0],
                "stack1[0] must remain colorId=3");
            Assert.AreEqual(2, _gsm.StackContents[1][1],
                "stack1[1] must be colorId=2 (returned by undo)");

            // Assert — seqId incremented (not decremented — UND-06)
            Assert.AreEqual(3L, _gsm.CurrentSequenceId,
                "CurrentSequenceId must be 3 (incremented from 2 by undo)");

            // Assert — move_count decremented
            Assert.AreEqual(1, _gsm.MoveCount,
                "MoveCount must be 1 after undoing one of two moves");

            // Assert — undo stack depth decremented
            Assert.AreEqual(1, _gsm.UndoStackDepth,
                "Undo stack must have 1 entry left (move A not yet undone)");

            // Assert — board_state_changed emitted with correct payload
            Assert.AreEqual(3L, emittedSeqId,
                "board_state_changed must carry new seqId=3");
            Assert.AreEqual(1, emittedMoveCount,
                "board_state_changed must carry new moveCount=1");

            // Cleanup
            _gsm.OnBoardStateChanged -= BoardStateChangedSpy;
        }

        /// <summary>
        /// AC-GSM-04 (edge: single-entry stack): Undo with exactly one entry leaves the
        /// stack empty (depth=0) after the pop.
        /// </summary>
        [Test]
        public void Test_UndoRequested_SingleEntryStack_StackEmptyAfterUndo()
        {
            // Arrange — one committed move
            var combined = new List<int>[]
            {
                new List<int> { 5 },  // stack0 has one bolt
                new List<int>(),      // stack1 empty
                new List<int>(),      // temp slot empty
            };
            SeedAndActivate(combined, initialSeqId: 0, initialMoveCount: 0);
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 5, seqId: 0L);

            Assert.AreEqual(1, _gsm.UndoStackDepth, "Precondition: one entry in undo stack");

            // Act
            _gsm.UndoRequested();

            // Assert — bolt returned to stack0, stack1 empty
            Assert.AreEqual(1, _gsm.StackContents[0].Count,
                "stack0 must have 1 bolt after undo reverts the only move");
            Assert.AreEqual(5, _gsm.StackContents[0][0],
                "stack0[0] must be colorId=5 (returned by undo)");
            Assert.AreEqual(0, _gsm.StackContents[1].Count,
                "stack1 must be empty after undo");

            // Assert — undo stack is now empty
            Assert.AreEqual(0, _gsm.UndoStackDepth,
                "Undo stack must be empty after popping the only entry");

            // Assert — counters updated
            Assert.AreEqual(2L, _gsm.CurrentSequenceId, "seqId must be 2 (0→1 on commit, 1→2 on undo)");
            Assert.AreEqual(0,  _gsm.MoveCount,          "MoveCount must be 0 after undoing the only move");
        }

        /// <summary>
        /// AC-GSM-04 (edge: temp slot as destination): Undo reverts a move where the
        /// destination was a temp slot (flat namespace index = colorCount).
        /// </summary>
        [Test]
        public void Test_UndoRequested_TempSlotDestination_RevertsCorrectly()
        {
            // Arrange — move bolt from stack0 to temp slot (index 2)
            var combined = new List<int>[]
            {
                new List<int> { 7 },  // stack0: colorId=7 on top
                new List<int>(),      // stack1: empty
                new List<int>(),      // temp slot 0: empty
            };
            SeedAndActivate(combined);
            _gsm.SimulateMoveCommitted(src: 0, dst: 2, colorId: 7, seqId: 0L);

            // Verify setup: bolt is in temp slot
            Assert.AreEqual(1, _gsm.TempSlotContents[0].Count, "Precondition: temp slot has bolt 7");
            Assert.AreEqual(0, _gsm.StackContents[0].Count,     "Precondition: stack0 is empty");

            // Act
            _gsm.UndoRequested();

            // Assert — bolt returned to stack0, temp slot empty
            Assert.AreEqual(0, _gsm.TempSlotContents[0].Count,
                "Temp slot must be empty after undo");
            Assert.AreEqual(1, _gsm.StackContents[0].Count,
                "stack0 must have 1 bolt after undo reverts temp-slot move");
            Assert.AreEqual(7, _gsm.StackContents[0][0],
                "stack0[0] must be colorId=7");
        }

        // ── AC-GSM-05: Empty stack is a strict no-op ──────────────────────────────

        /// <summary>
        /// AC-GSM-05: UndoRequested on an empty undo stack produces zero mutations
        /// and emits no events.
        /// </summary>
        [Test]
        public void Test_UndoRequested_EmptyStack_NoMutationNoEvent()
        {
            // Arrange
            var combined = new List<int>[]
            {
                new List<int> { 1, 2 },
                new List<int>(),
                new List<int>(),
            };
            SeedAndActivate(combined, initialSeqId: 3, initialMoveCount: 2);

            long snapshotSeqId    = _gsm.CurrentSequenceId;
            int  snapshotMoves    = _gsm.MoveCount;
            int  snapshotUndoDepth = _gsm.UndoStackDepth;
            int  stack0CountBefore = _gsm.StackContents[0].Count;

            int eventCount = 0;
            void NoopSpy(long s, int m) => eventCount++;
            _gsm.OnBoardStateChanged += NoopSpy;

            // Act
            _gsm.UndoRequested();

            // Assert — no mutation
            Assert.AreEqual(snapshotSeqId,    _gsm.CurrentSequenceId,
                "Empty-stack undo must not change CurrentSequenceId");
            Assert.AreEqual(snapshotMoves,    _gsm.MoveCount,
                "Empty-stack undo must not change MoveCount");
            Assert.AreEqual(snapshotUndoDepth, _gsm.UndoStackDepth,
                "Empty-stack undo must not change undo stack depth");
            Assert.AreEqual(stack0CountBefore, _gsm.StackContents[0].Count,
                "Empty-stack undo must not change board state");
            Assert.AreEqual(0, eventCount,
                "Empty-stack undo must emit no OnBoardStateChanged events");

            // Cleanup
            _gsm.OnBoardStateChanged -= NoopSpy;
        }

        // ── AC-GSM-06: COMPLETE state rejects undo ────────────────────────────────

        /// <summary>
        /// AC-GSM-06: UndoRequested in COMPLETE state does nothing — no board mutation,
        /// no event, undo stack unchanged.
        /// </summary>
        [Test]
        public void Test_UndoRequested_CompleteState_NoMutationNoEvent()
        {
            // Arrange — seed with 3 entries in undo stack, set state to COMPLETE
            var combined = new List<int>[]
            {
                new List<int> { 1 },
                new List<int> { 2 },
                new List<int>(),
            };
            var undoStack = new List<UndoEntry>
            {
                new UndoEntry { From = 0, To = 1, ColorId = 1, SeqId = 0 },
                new UndoEntry { From = 1, To = 0, ColorId = 2, SeqId = 1 },
                new UndoEntry { From = 0, To = 1, ColorId = 1, SeqId = 2 },
            };
            _gsm.SeedBoardForTesting(
                combinedContents:  combined,
                stackDepth:        4,
                tempSlotDepth:     1,
                tempSlotCount:     1,
                colorCount:        2,
                initialSeqId:      3L,
                initialMoveCount:  3,
                initialUndoStack:  undoStack);
            _gsm.SetStateForTesting(GSMLifecycleState.Complete);

            long snapshotSeqId    = _gsm.CurrentSequenceId;
            int  snapshotMoves    = _gsm.MoveCount;
            int  snapshotUndoDepth = _gsm.UndoStackDepth;

            int eventCount = 0;
            void CompleteSpy(long s, int m) => eventCount++;
            _gsm.OnBoardStateChanged += CompleteSpy;

            // Act
            _gsm.UndoRequested();

            // Assert — no mutation, no event, undo stack unchanged
            Assert.AreEqual(snapshotSeqId,    _gsm.CurrentSequenceId,
                "COMPLETE: undo must not change CurrentSequenceId");
            Assert.AreEqual(snapshotMoves,    _gsm.MoveCount,
                "COMPLETE: undo must not change MoveCount");
            Assert.AreEqual(snapshotUndoDepth, _gsm.UndoStackDepth,
                "COMPLETE: undo must not pop the undo stack");
            Assert.AreEqual(0, eventCount,
                "COMPLETE: undo must emit no events");

            // Cleanup
            _gsm.OnBoardStateChanged -= CompleteSpy;
        }

        // ── AC-GSM-07: Strict monotonicity over mixed sequence ────────────────────

        /// <summary>
        /// AC-GSM-07: After 5 move_committed events and 3 undo_requested events,
        /// current_sequence_id=8 and every observed value after each mutation is
        /// strictly greater than the preceding value.
        /// </summary>
        [Test]
        public void Test_CurrentSequenceId_MonotonicallyIncreasing_OverMixedSequence()
        {
            // Arrange — large enough board for 5 commits (5 bolts in stack0)
            var combined = new List<int>[]
            {
                new List<int> { 1, 2, 3, 4, 5 },  // stack0: 5 bolts
                new List<int>(),                   // stack1: empty
                new List<int>(),                   // temp slot: empty
            };
            SeedAndActivate(combined, initialSeqId: 0, initialMoveCount: 0);

            var observed = new List<long>();

            void SeqSpy(long s, int m) => observed.Add(s);
            _gsm.OnBoardStateChanged += SeqSpy;

            // Sequence: commit, commit, commit, undo, commit, undo, commit, undo
            // (5 commits total, 3 undos — any valid interleaving)

            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 5, seqId: 0L);
            observed.Add(_gsm.CurrentSequenceId);  // 1

            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 4, seqId: 1L);
            observed.Add(_gsm.CurrentSequenceId);  // 2

            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 3, seqId: 2L);
            observed.Add(_gsm.CurrentSequenceId);  // 3

            _gsm.UndoRequested(); // spy fires → observed adds 4

            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 4L);
            observed.Add(_gsm.CurrentSequenceId);  // 5

            _gsm.UndoRequested(); // spy fires → observed adds 6

            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 1, seqId: 6L);
            observed.Add(_gsm.CurrentSequenceId);  // 7

            _gsm.UndoRequested(); // spy fires → observed adds 8

            // Assert — final seqId
            Assert.AreEqual(8L, _gsm.CurrentSequenceId,
                "5 commits + 3 undos from seqId=0 must result in seqId=8");

            // Assert — all 8 observed values are strictly increasing
            Assert.AreEqual(8, observed.Count, "Expected 8 observations (one per mutation)");
            for (int i = 1; i < observed.Count; i++)
            {
                Assert.Greater(observed[i], observed[i - 1],
                    $"observed[{i}]={observed[i]} must be > observed[{i-1}]={observed[i-1]}");
            }

            // Cleanup
            _gsm.OnBoardStateChanged -= SeqSpy;
        }

        // ── AC-GSM-19: Move count formula across interleaved sequence ─────────────

        /// <summary>
        /// AC-GSM-19: move_count follows the formula (+1 on commit, -1 on undo with non-empty stack,
        /// 0 on cancel/reject) across an interleaved sequence.
        /// Given: move_committed → move_committed → move_cancelled → move_rejected →
        ///        undo_requested → move_committed
        /// Final move_count = 2; intermediate values: 1, 2, 2, 2, 1, 2.
        /// </summary>
        [Test]
        public void Test_MoveCount_Formula_InterleavedCancelRejectUndoSequence()
        {
            // Arrange — 3 bolts needed (2 commits + 1 potential undo + 1 final commit)
            var combined = new List<int>[]
            {
                new List<int> { 1, 2, 3 },  // stack0: 3 bolts
                new List<int>(),              // stack1: empty
                new List<int>(),              // temp slot: empty
            };
            SeedAndActivate(combined, initialSeqId: 0, initialMoveCount: 0);

            var moveCountLog = new List<int>();

            // 1. move_committed: moveCount = 0+1 = 1
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 3, seqId: 0L);
            moveCountLog.Add(_gsm.MoveCount);  // expected: 1

            // 2. move_committed: moveCount = 1+1 = 2
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 1L);
            moveCountLog.Add(_gsm.MoveCount);  // expected: 2

            // 3. move_cancelled: moveCount unchanged = 2
            _gsm.SimulateMoveCancelled(src: 1, colorId: 2);
            moveCountLog.Add(_gsm.MoveCount);  // expected: 2

            // 4. move_rejected: moveCount unchanged = 2
            _gsm.SimulateMoveRejected(src: 0, dst: 1, colorId: 1, reason: "COLOR_MISMATCH");
            moveCountLog.Add(_gsm.MoveCount);  // expected: 2

            // 5. undo_requested (stack non-empty): moveCount = 2-1 = 1
            _gsm.UndoRequested();
            moveCountLog.Add(_gsm.MoveCount);  // expected: 1

            // 6. move_committed: moveCount = 1+1 = 2
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 5L);
            moveCountLog.Add(_gsm.MoveCount);  // expected: 2

            // Assert — final value
            Assert.AreEqual(2, _gsm.MoveCount,
                "Final MoveCount must be 2 after the interleaved sequence");

            // Assert — intermediate values (GDD formula verification)
            var expected = new[] { 1, 2, 2, 2, 1, 2 };
            Assert.AreEqual(expected.Length, moveCountLog.Count,
                "Expected 6 intermediate observations");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], moveCountLog[i],
                    $"MoveCount at step {i+1} must be {expected[i]}, got {moveCountLog[i]}");
            }
        }

        // ── Structural: board_state_changed NOT emitted on empty-stack undo ───────

        /// <summary>
        /// Structural verification: board_state_changed fires only when a mutation actually
        /// occurs. An undo with an empty stack must not fire the event.
        /// Complements AC-GSM-05.
        /// </summary>
        [Test]
        public void Test_UndoRequested_EmptyStack_OnBoardStateChangedNotFired()
        {
            // Arrange
            var combined = new List<int>[]
            {
                new List<int> { 1 },
                new List<int>(),
                new List<int>(),
            };
            SeedAndActivate(combined);
            // Do NOT commit any moves — undo stack stays empty

            int fireCount = 0;
            void EmptyStackSpy(long s, int m) => fireCount++;
            _gsm.OnBoardStateChanged += EmptyStackSpy;

            // Act
            _gsm.UndoRequested();

            // Assert
            Assert.AreEqual(0, fireCount,
                "OnBoardStateChanged must not fire when undo stack is empty");

            // Cleanup
            _gsm.OnBoardStateChanged -= EmptyStackSpy;
        }
    }
}
