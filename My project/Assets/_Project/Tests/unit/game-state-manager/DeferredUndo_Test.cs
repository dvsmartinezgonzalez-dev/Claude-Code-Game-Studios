using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Story 007 ACs — AC-GSM-14, AC-GSM-15, AC-GSM-16, AC-GSM-21
// Design:    design/gdd/game-state-manager.md  (TR-GSM-006, UND-03, EC-05, EC-10, EC-11, EC-17)
// ADR:       docs/architecture/ADR-0006.md

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class DeferredUndo_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_DeferredUndoTest");
            _gsm = _go.AddComponent<global::BoltSort.GameStateManager.GameStateManager>();

            // Standard board: color_count=2, stack_depth=2
            // Flat namespace: index 0 = color stack 0, index 1 = color stack 1
            // stacks=[[1,2],[2]] — placing bolt 2 (top of stack 0) onto stack 1
            // produces [[1],[2,2]] — a won board (stack 1 is full and monochromatic)
            var stacks = new List<int>[]
            {
                new List<int> { 1, 2 },  // stack 0 — top = 2
                new List<int> { 2 },      // stack 1 — not full
            };
            _gsm.SeedBoardForTesting(
                combinedContents: stacks,
                stackDepth: 2,
                tempSlotDepth: 1,
                tempSlotCount: 0,
                colorCount: 2);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            global::BoltSort.GameStateManager.GameStateManager.ClearInstanceForTesting();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Commits the standard move (bolt 2 from stack 0 → stack 1).
        /// After this: stacks=[[1],[2,2]], _isAnimationInFlight=true, seqId=N.
        /// Returns N (the post-BSM-01 seqId).
        /// </summary>
        private long CommitStandardMove()
        {
            long seqBefore = _gsm.CurrentSequenceId;
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: seqBefore);
            return _gsm.CurrentSequenceId; // N = seqBefore + 1
        }

        // ── AC-GSM-14: Deferred undo fires before win evaluation ─────────────────
        // Board [[1,2],[2]] → commit → [[1],[2,2]] (win-state board) → undo_requested →
        // exit → revert to [[1,2],[2]]; GSM stays ACTIVE; level_complete not emitted.

        [Test]
        public void Test_DeferredUndo_OnMoveExecutingExited_BoardReverts()
        {
            // Arrange
            long N = CommitStandardMove(); // board = [[1],[2,2]]
            _gsm.UndoRequested();          // deferred
            Assert.IsTrue(_gsm.IsPendingUndo); // sanity

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — board reverted to [[1,2],[2]]
            Assert.AreEqual(2, _gsm.StackContents[0].Count, "stack 0 must have 2 bolts after undo");
            Assert.AreEqual(2, _gsm.StackContents[0][1],    "stack 0 top must be bolt 2 after undo");
            Assert.AreEqual(1, _gsm.StackContents[1].Count, "stack 1 must have 1 bolt after undo");
        }

        [Test]
        public void Test_DeferredUndo_OnMoveExecutingExited_BoardStateChangedEmitted()
        {
            // Arrange
            long N = CommitStandardMove();
            _gsm.UndoRequested();
            bool boardStateChangedFired = false;
            _gsm.OnBoardStateChanged += (_, __) => boardStateChangedFired = true;

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert
            Assert.IsTrue(boardStateChangedFired,
                "OnBoardStateChanged must be emitted when deferred undo executes");
        }

        [Test]
        public void Test_DeferredUndo_OnMoveExecutingExited_LevelCompleteNotEmitted()
        {
            // Arrange — board becomes [[1],[2,2]] after commit (win state if evaluated)
            long N = CommitStandardMove();
            _gsm.UndoRequested();
            bool levelCompleteFired = false;
            _gsm.OnLevelComplete += (_, __, ___, ____) => levelCompleteFired = true;

            // Act — undo fires first, reverting to non-win board
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — win was never evaluated on the committed board; GSM stays ACTIVE
            Assert.IsFalse(levelCompleteFired,
                "OnLevelComplete must NOT fire — undo reverted the board before win evaluation");
            Assert.AreEqual(GSMLifecycleState.Active, _gsm.LifecycleState,
                "GSM must remain ACTIVE after deferred undo reverts the winning move");
        }

        [Test]
        public void Test_DeferredUndo_OnMoveExecutingExited_PendingUndoCleared()
        {
            // Arrange
            long N = CommitStandardMove();
            _gsm.UndoRequested();
            Assert.IsTrue(_gsm.IsPendingUndo);

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert
            Assert.IsFalse(_gsm.IsPendingUndo,
                "_pendingUndo must be false after flush — no second undo on next exit");
        }

        [Test]
        public void Test_NoPendingUndo_OnMoveExecutingExited_NoBoardStateChanged()
        {
            // Arrange — commit but no undo requested
            long N = CommitStandardMove();
            bool boardStateChangedFired = false;
            _gsm.OnBoardStateChanged += (_, __) => boardStateChangedFired = true;

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — FlushDeferredUndo is a no-op when _pendingUndo is false
            Assert.IsFalse(boardStateChangedFired,
                "OnBoardStateChanged must NOT fire when no undo was requested");
        }

        // ── AC-GSM-15: puzzle_solved discards deferred undo ──────────────────────

        [Test]
        public void Test_PuzzleSolved_ClearsDeferredUndo()
        {
            // Arrange
            CommitStandardMove();
            _gsm.UndoRequested();
            Assert.IsTrue(_gsm.IsPendingUndo); // sanity

            // Act
            _gsm.SimulatePuzzleSolved();

            // Assert — EC-05: pending undo discarded on COMPLETE
            Assert.IsFalse(_gsm.IsPendingUndo,
                "_pendingUndo must be false after puzzle_solved (EC-05)");
        }

        [Test]
        public void Test_PuzzleSolved_GsmTransitionsToComplete()
        {
            // Arrange
            CommitStandardMove();
            _gsm.UndoRequested();

            // Act
            _gsm.SimulatePuzzleSolved();

            // Assert
            Assert.AreEqual(GSMLifecycleState.Complete, _gsm.LifecycleState,
                "GSM must be in COMPLETE state after puzzle_solved");
        }

        [Test]
        public void Test_PuzzleSolved_LaterExit_UndoNotProcessed()
        {
            // Arrange
            long N = CommitStandardMove(); // board = [[1],[2,2]]
            _gsm.UndoRequested();          // deferred
            _gsm.SimulatePuzzleSolved();   // clears _pendingUndo, GSM → COMPLETE

            int boardStateChangedCount = 0;
            _gsm.OnBoardStateChanged += (_, __) => boardStateChangedCount++;

            // Act — stale exit arrives after puzzle_solved (GSM is now COMPLETE)
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — FlushDeferredUndo is no-op (_pendingUndo was cleared by puzzle_solved)
            Assert.AreEqual(0, boardStateChangedCount,
                "No undo must fire after a stale exit when puzzle_solved already cleared _pendingUndo");
            // Board remains [[1],[2,2]] — the committed (winning) state
            Assert.AreEqual(2, _gsm.StackContents[1].Count,
                "Board must reflect committed move — no undo was applied");
        }

        // ── AC-GSM-16: Watchdog exit processes deferred undo ─────────────────────

        [Test]
        public void Test_WatchdogFire_WithDeferredUndo_BoardRefreshForcedEmitted()
        {
            // Arrange
            long N = CommitStandardMove();
            _gsm.UndoRequested();
            long receivedRefreshSeqId = -1;
            _gsm.OnBoardRefreshForced += seqId => receivedRefreshSeqId = seqId;

            // Act
            _gsm.SimulateWatchdogFire();

            // Assert — (1) seqId incremented, (2) board_refresh_forced emitted
            Assert.AreEqual(N + 1, receivedRefreshSeqId,
                "board_refresh_forced must carry N+1 after watchdog fires");
        }

        [Test]
        public void Test_WatchdogFire_WithDeferredUndo_BoardReverts()
        {
            // Arrange
            long N = CommitStandardMove(); // board = [[1],[2,2]]
            _gsm.UndoRequested();

            // Act
            _gsm.SimulateWatchdogFire(); // seqId→N+1, board_refresh_forced, then flush deferred undo

            // Assert — (3) board reverted by deferred undo
            Assert.AreEqual(2, _gsm.StackContents[0].Count,
                "stack 0 must have 2 bolts after watchdog-triggered deferred undo");
            Assert.AreEqual(1, _gsm.StackContents[1].Count,
                "stack 1 must have 1 bolt after watchdog-triggered deferred undo");
        }

        [Test]
        public void Test_WatchdogFire_WithDeferredUndo_MoveCountDecrements()
        {
            // Arrange
            long N = CommitStandardMove(); // moveCount = 1
            _gsm.UndoRequested();

            // Act
            _gsm.SimulateWatchdogFire();

            // Assert — undo decrements move_count
            Assert.AreEqual(0, _gsm.MoveCount,
                "MoveCount must decrement after watchdog-triggered deferred undo");
        }

        [Test]
        public void Test_WatchdogFire_WithDeferredUndo_BoardStateChangedEmittedAfterRefreshForced()
        {
            // Arrange — track event order
            long N = CommitStandardMove();
            _gsm.UndoRequested();
            var eventOrder = new List<string>();
            _gsm.OnBoardRefreshForced += _ => eventOrder.Add("refresh");
            _gsm.OnBoardStateChanged  += (_, __) => eventOrder.Add("stateChanged");

            // Act
            _gsm.SimulateWatchdogFire();

            // Assert — board_refresh_forced fires BEFORE board_state_changed (EC-11 ordering)
            Assert.AreEqual(2, eventOrder.Count, "Both events must fire");
            Assert.AreEqual("refresh",      eventOrder[0], "board_refresh_forced must come first");
            Assert.AreEqual("stateChanged", eventOrder[1], "board_state_changed (from undo) must come second");
        }

        [Test]
        public void Test_WatchdogFire_NoDeferredUndo_NoBoardStateChanged()
        {
            // Arrange — commit but no undo requested
            long N = CommitStandardMove();
            bool boardStateChangedFired = false;
            _gsm.OnBoardStateChanged += (_, __) => boardStateChangedFired = true;

            // Act
            _gsm.SimulateWatchdogFire();

            // Assert — FlushDeferredUndo is no-op; board_refresh_forced fired but no undo
            Assert.IsFalse(boardStateChangedFired,
                "OnBoardStateChanged must NOT fire when no undo was requested before watchdog");
        }

        // ── AC-GSM-21: Queue capacity = 1; second request silently dropped ────────

        [Test]
        public void Test_SecondUndoRequest_WhileDeferred_PendingUndoRemainsTrue()
        {
            // Arrange
            CommitStandardMove();
            _gsm.UndoRequested(); // first — deferred
            Assert.IsTrue(_gsm.IsPendingUndo);

            // Act — second request while animation still in flight
            _gsm.UndoRequested(); // EC-17: silently dropped

            // Assert — still pending (not doubled or cleared)
            Assert.IsTrue(_gsm.IsPendingUndo,
                "_pendingUndo must remain true after second request (EC-17)");
        }

        [Test]
        public void Test_SecondUndoRequest_OnlyOneUndoFires_BoardRevertsExactlyOneMove()
        {
            // Arrange — seed deeper board so two undos would produce different board state
            // stacks=[[1,2],[2,1]], stack_depth=2 — both stacks full
            Object.DestroyImmediate(_go);
            global::BoltSort.GameStateManager.GameStateManager.ClearInstanceForTesting();
            _go  = new GameObject("GSM_DeferredUndoTest_Deep");
            _gsm = _go.AddComponent<global::BoltSort.GameStateManager.GameStateManager>();
            var deepStacks = new List<int>[]
            {
                new List<int> { 1, 2 },  // stack 0
                new List<int> { 2 },      // stack 1 — has room for one more
            };
            _gsm.SeedBoardForTesting(deepStacks, stackDepth: 2, tempSlotDepth: 1, tempSlotCount: 0, colorCount: 2);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            long N = CommitStandardMove(); // stacks=[[1],[2,2]], moveCount=1
            int undoCount = 0;
            _gsm.OnBoardStateChanged += (_, __) => undoCount++;

            _gsm.UndoRequested(); // deferred
            _gsm.UndoRequested(); // silently dropped (EC-17)

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — exactly one undo fired
            Assert.AreEqual(1, undoCount,
                "Exactly one undo must fire — second request was silently dropped (EC-17)");
            Assert.AreEqual(0, _gsm.MoveCount,
                "MoveCount must be 0 — only one move was undone");
        }

        // ── _isAnimationInFlight flag behaviour ───────────────────────────────────

        [Test]
        public void Test_MoveCommitted_SetsAnimationInFlight()
        {
            // Act
            CommitStandardMove();

            // Assert
            Assert.IsTrue(_gsm.IsAnimationInFlight,
                "_isAnimationInFlight must be true immediately after move_committed");
        }

        [Test]
        public void Test_MoveExecutingExited_ClearsAnimationInFlight()
        {
            // Arrange
            long N = CommitStandardMove();

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert
            Assert.IsFalse(_gsm.IsAnimationInFlight,
                "_isAnimationInFlight must be false after MOVE_EXECUTING exits");
        }

        [Test]
        public void Test_UndoRequested_WhenNotInFlight_ExecutesSynchronously()
        {
            // Arrange — no animation in flight; board=[[1,2],[2]]
            Assert.IsFalse(_gsm.IsAnimationInFlight);
            bool boardStateChangedFired = false;
            _gsm.OnBoardStateChanged += (_, __) => boardStateChangedFired = true;

            // First commit a move so there's something to undo
            long N = CommitStandardMove(); // starts watchdog, in-flight=true
            _gsm.SimulateMoveExecutingExited(N); // clears in-flight
            Assert.IsFalse(_gsm.IsAnimationInFlight);
            boardStateChangedFired = false; // reset after exit (FlushDeferredUndo fires with no pending undo)

            // Act — undo with no animation in flight → synchronous path
            _gsm.UndoRequested();

            // Assert — synchronous undo fired immediately; no deferred state
            Assert.IsTrue(boardStateChangedFired,
                "UndoRequested must execute synchronously when no animation is in flight");
            Assert.IsFalse(_gsm.IsPendingUndo,
                "_pendingUndo must remain false on the synchronous undo path");
        }
    }
}
