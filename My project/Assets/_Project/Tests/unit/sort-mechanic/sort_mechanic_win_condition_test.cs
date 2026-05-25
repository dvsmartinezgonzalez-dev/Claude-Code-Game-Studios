using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.SortMechanic;

// Test assembly: Tests.Unit.SortMechanic
// Covers: AC-05a, AC-06, AC-18a, AC-24, AC-29a; TR-SORT-003, TR-SORT-006, TR-SORT-007
//   from design/gdd/sort-mechanic.md (Story 004 scope)
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/sort-mechanic/story-004-win-condition-seqid.md

namespace BoltSort.Tests.Unit.SortMechanic
{
    [TestFixture]
    internal sealed class SortMechanic_WinCondition_Test
    {
        private GameObject _go;
        private global::BoltSort.SortMechanic.SortMechanic _sm;
        private MockGsm _gsm;
        private EventSpy _spy;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SortMechanic_WinCondition_Test");
            _sm = _go.AddComponent<global::BoltSort.SortMechanic.SortMechanic>();
            _gsm = new MockGsm();
            _spy = new EventSpy();

            // Default board — 2 stacks, depth 3, mid-game (not won).
            _gsm.SetColorStacks(new[] { 1 }, new int[0]);
            _gsm.StackDepth = 3;
            _gsm.TempSlotCount = 1;
            _gsm.TempSlotDepth = 2;
            _gsm.SetTempSlots(new int[0]);

            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy.Subscribe(_sm);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// Configures a won board (all stacks full and monochromatic) and forces MOVE_EXECUTING.
        private long SetupWonBoardInMoveExecuting(long seqId = 1L)
        {
            _gsm.SetColorStacks(new[] { 1, 1, 1 }, new[] { 2, 2, 2 });
            _gsm.StackDepth = 3;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(sourceIndex: 0, heldColorId: 1, seqId: seqId);
            return seqId;
        }

        /// Configures a non-won board (stack A not full) and forces MOVE_EXECUTING.
        private long SetupNotWonBoardInMoveExecuting(long seqId = 1L)
        {
            _gsm.SetColorStacks(new[] { 1, 1 }, new[] { 2, 2, 2 });
            _gsm.StackDepth = 3;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(sourceIndex: 0, heldColorId: 1, seqId: seqId);
            return seqId;
        }

        // ── AC-05a: Won board → puzzle_solved emitted, state = WIN ────────────────

        [Test]
        public void AnimationComplete_WonBoard_EmitsPuzzleSolved()
        {
            // Arrange: post-move board = [1,1,1] + [2,2,2] — all full and monochromatic
            long seqId = SetupWonBoardInMoveExecuting();

            // Act
            _sm.OnAnimationComplete(seqId);

            // Assert
            Assert.AreEqual(1, _spy.PuzzleSolvedCount,
                "AC-05a: won board on animation_complete must emit puzzle_solved.");
        }

        [Test]
        public void AnimationComplete_WonBoard_TransitionsToWin()
        {
            long seqId = SetupWonBoardInMoveExecuting();

            _sm.OnAnimationComplete(seqId);

            Assert.AreEqual(SortMechState.Win, _sm.CurrentStateForTesting,
                "AC-05a: Sort Mechanic must be in WIN state after puzzle_solved.");
        }

        [Test]
        public void AnimationComplete_WonBoard_NoMoveExecutingExited()
        {
            // TR-SORT-007: OnMoveExecutingExited must NOT fire on WIN path
            long seqId = SetupWonBoardInMoveExecuting();

            _sm.OnAnimationComplete(seqId);

            Assert.AreEqual(0, _spy.MoveExecutingExitedCount,
                "TR-SORT-007: OnMoveExecutingExited must NOT be emitted on WIN path.");
        }

        [Test]
        public void Win_SubsequentTaps_EmitZeroEvents()
        {
            // AC-05a: all subsequent taps after WIN produce zero emissions
            long seqId = SetupWonBoardInMoveExecuting();
            _sm.OnAnimationComplete(seqId);
            Assert.AreEqual(SortMechState.Win, _sm.CurrentStateForTesting);
            int eventsBefore = _spy.TotalEvents;

            // Act: inject multiple taps in WIN state
            _sm.InjectTapForTesting(0);
            _sm.InjectTapForTesting(1);

            // Assert
            Assert.AreEqual(eventsBefore, _spy.TotalEvents,
                "AC-05a: taps in WIN state must produce zero events.");
        }

        // ── AC-06: Non-full board → no puzzle_solved, transitions to IDLE ─────────

        [Test]
        public void AnimationComplete_NotWon_NotFull_NoPuzzleSolved()
        {
            // Stack A=[1,1] (depth=3, not full); stack B=[2,2,2] (full)
            long seqId = SetupNotWonBoardInMoveExecuting();

            _sm.OnAnimationComplete(seqId);

            Assert.AreEqual(0, _spy.PuzzleSolvedCount,
                "AC-06: non-full board must NOT emit puzzle_solved.");
        }

        [Test]
        public void AnimationComplete_NotWon_NotFull_TransitionsToIdle()
        {
            long seqId = SetupNotWonBoardInMoveExecuting();

            _sm.OnAnimationComplete(seqId);

            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "AC-06: non-full board must transition to IDLE, not WIN.");
        }

        [Test]
        public void AnimationComplete_NotWon_EmitsMoveExecutingExited()
        {
            // TR-SORT-007: OnMoveExecutingExited emitted on IDLE path
            long seqId = SetupNotWonBoardInMoveExecuting();

            _sm.OnAnimationComplete(seqId);

            Assert.AreEqual(1, _spy.MoveExecutingExitedCount,
                "TR-SORT-007: OnMoveExecutingExited must be emitted on IDLE path.");
        }

        // ── AC-18a: Win invariant — all stacks full and monochromatic ─────────────

        [Test]
        public void AnimationComplete_ThreeStacksAllWon_EmitsPuzzleSolved()
        {
            // Arrange: 3-color, depth-2 board — all full and monochromatic
            _gsm.SetColorStacks(new[] { 1, 1 }, new[] { 2, 2 }, new[] { 3, 3 });
            _gsm.StackDepth = 2;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(0, 1, 1L);

            // Act
            _sm.OnAnimationComplete(1L);

            // Assert
            Assert.AreEqual(1, _spy.PuzzleSolvedCount,
                "AC-18a: all-won board must emit puzzle_solved regardless of stack count.");
            Assert.AreEqual(SortMechState.Win, _sm.CurrentStateForTesting);
        }

        [Test]
        public void AnimationComplete_FullButNonMonochromatic_NotWon()
        {
            // Stack A=[1,2,1] — full but mixed colors; stack B=[2,2,2] — full monochromatic
            _gsm.SetColorStacks(new[] { 1, 2, 1 }, new[] { 2, 2, 2 });
            _gsm.StackDepth = 3;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(0, 1, 1L);

            _sm.OnAnimationComplete(1L);

            Assert.AreEqual(0, _spy.PuzzleSolvedCount,
                "AC-18a: full but non-monochromatic stack must NOT win.");
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
        }

        // ── AC-24: Buffered tap discarded on WIN ───────────────────────────────────

        [Test]
        public void AnimationComplete_WonWithPendingTap_DiscardsPendingTap()
        {
            long seqId = SetupWonBoardInMoveExecuting();
            _sm.InjectTapForTesting(0); // Buffer a tap during MOVE_EXECUTING
            Assert.IsTrue(_sm.PendingTapForTesting, "Pre-condition: pending tap set.");

            // Act
            _sm.OnAnimationComplete(seqId);

            // Assert: tap discarded silently
            Assert.IsFalse(_sm.PendingTapForTesting,
                "AC-24: buffered tap must be discarded when WIN is entered.");
        }

        [Test]
        public void AnimationComplete_WonWithPendingTap_NoMoveExecutingExitedOrAdditionalEvents()
        {
            long seqId = SetupWonBoardInMoveExecuting();
            _sm.InjectTapForTesting(0); // Buffer a tap

            // Act
            _sm.OnAnimationComplete(seqId);

            // Assert: only puzzle_solved emitted; no move_executing_exited
            Assert.AreEqual(0, _spy.MoveExecutingExitedCount,
                "AC-24: OnMoveExecutingExited must NOT fire on WIN path.");
            Assert.AreEqual(1, _spy.PuzzleSolvedCount,
                "AC-24: puzzle_solved must fire exactly once.");
            Assert.AreEqual(SortMechState.Win, _sm.CurrentStateForTesting);
        }

        [Test]
        public void Win_AfterBufferedTapDiscarded_TapsProduceNoEvents()
        {
            // Verifies buffered tap did not fire as a subsequent action after WIN entry
            long seqId = SetupWonBoardInMoveExecuting();
            _sm.InjectTapForTesting(0); // Buffer tap targeting non-empty stack 0
            _sm.OnAnimationComplete(seqId);
            Assert.AreEqual(SortMechState.Win, _sm.CurrentStateForTesting);
            int eventsBefore = _spy.TotalEvents;

            // Act: inject a tap directly into WIN state
            _sm.InjectTapForTesting(0);

            // Assert: zero events — WIN is terminal
            Assert.AreEqual(eventsBefore, _spy.TotalEvents,
                "AC-24: after WIN entry (with discarded buffer), additional taps emit no events.");
        }

        // ── AC-29a: Watchdog — OnMoveExecutingExited NOT emitted ─────────────────

        [Test]
        public void Watchdog_NotWon_NoMoveExecutingExited()
        {
            // Arrange: non-won board in MOVE_EXECUTING
            long seqId = SetupNotWonBoardInMoveExecuting();

            // Act: watchdog fires (e.g., animation_complete never arrived)
            _sm.TriggerBoardRefreshForcedForTesting(seqId);

            // Assert: transitions to IDLE but OnMoveExecutingExited NOT emitted
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
            Assert.AreEqual(0, _spy.MoveExecutingExitedCount,
                "AC-29a: OnMoveExecutingExited must NOT be emitted on watchdog path.");
            Assert.AreEqual(0, _spy.PuzzleSolvedCount);
        }

        [Test]
        public void Watchdog_WonBoard_EmitsPuzzleSolved_NoMoveExecutingExited()
        {
            // AC-29a: watchdog runs win check; WIN path also skips OnMoveExecutingExited
            long seqId = SetupWonBoardInMoveExecuting();

            _sm.TriggerBoardRefreshForcedForTesting(seqId);

            Assert.AreEqual(SortMechState.Win, _sm.CurrentStateForTesting,
                "AC-29a: watchdog on won board must transition to WIN.");
            Assert.AreEqual(1, _spy.PuzzleSolvedCount,
                "AC-29a: watchdog on won board must emit puzzle_solved.");
            Assert.AreEqual(0, _spy.MoveExecutingExitedCount,
                "AC-29a: OnMoveExecutingExited must NOT be emitted on watchdog WIN path.");
        }

        [Test]
        public void Watchdog_WonWithPendingTap_DiscardsPendingTap()
        {
            long seqId = SetupWonBoardInMoveExecuting();
            _sm.InjectTapForTesting(0); // Buffer a tap

            _sm.TriggerBoardRefreshForcedForTesting(seqId);

            Assert.IsFalse(_sm.PendingTapForTesting,
                "AC-24/AC-29a: watchdog WIN path must discard pending tap.");
        }

        // ── SeqId guard — stale animation_complete ────────────────────────────────

        [Test]
        public void AnimationComplete_StaleSeqId_WonBoard_IsNoOp()
        {
            // Even on a won board, stale seqId must be discarded (TR-SORT-006)
            long seqId = SetupWonBoardInMoveExecuting(seqId: 5L);

            _sm.OnAnimationComplete(seqId + 1); // Wrong seqId

            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting,
                "TR-SORT-006: stale seqId must not trigger win check or state change.");
            Assert.AreEqual(0, _spy.PuzzleSolvedCount);
            Assert.AreEqual(0, _spy.MoveExecutingExitedCount);
        }

        // ── Win check: temp slots excluded ────────────────────────────────────────

        // ── AC-18a: Debug.Assert defensive fallback — mutated array length ────────

        [Test]
        public void IsWon_StackContentsLengthMismatchColorCount_DefensiveFallbackPreventsWin()
        {
            // Arrange: initialize with valid board, then mutate StackContents to wrong length
            // (simulates a defective undo corrupting the array outside GSM's control)
            _gsm.SetColorStacks(new[] { 1, 1, 1 }, new[] { 2, 2, 2 });
            _gsm.StackDepth = 3;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard(); // stacks.Length(2) == colorCount(2) — passes assertion 1
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            // Corrupt the array between init and win check: length 3 != colorCount 2
            _gsm.StackContents = new IReadOnlyList<int>[]
            {
                new List<int> { 1, 1, 1 },
                new List<int> { 2, 2, 2 },
                new List<int> { 3, 3, 3 }, // extra stack — causes mismatch
            };

            _sm.ForceEnterMoveExecutingForTesting(0, 1, 1L);

            // Act: Debug.Assert fires (logged in Unity, does not throw); defensive fallback returns false
            _sm.OnAnimationComplete(1L);

            // Assert: defensive fallback prevents false win; FSM takes IDLE path
            Assert.AreEqual(0, _spy.PuzzleSolvedCount,
                "AC-18a guard: StackContents.Length != ColorCount defensive fallback must prevent puzzle_solved.");
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "AC-18a guard: defensive fallback must transition to IDLE, not WIN.");
        }

        // ── Single-bolt win (stackDepth = 1 boundary) ─────────────────────────────

        [Test]
        public void AnimationComplete_StackDepthOne_SingleBoltPerStack_Won()
        {
            // stackDepth=1: each stack has exactly one bolt — trivially monochromatic
            // AllSameColor loop body never executes for a 1-element list (i starts at 1, count=1)
            _gsm.SetColorStacks(new[] { 1 }, new[] { 2 });
            _gsm.StackDepth = 1;
            _gsm.TempSlotDepth = 1; // TempSlotDepth must be ≤ StackDepth
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(0, 1, 1L);

            _sm.OnAnimationComplete(1L);

            Assert.AreEqual(1, _spy.PuzzleSolvedCount,
                "Single-bolt win (stackDepth=1): one full monochromatic bolt per stack must trigger win.");
            Assert.AreEqual(SortMechState.Win, _sm.CurrentStateForTesting);
        }

        // ── Empty color stack at win-check time → not won ─────────────────────────

        [Test]
        public void AnimationComplete_EmptyColorStack_NotWon()
        {
            // Stack A completely empty (count=0 != stackDepth=3); exercises the count guard
            // that fires before AllSameColor is called — prevents stack[0] on empty list
            _gsm.SetColorStacks(new int[0], new[] { 2, 2, 2 });
            _gsm.StackDepth = 3;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(0, 1, 1L);

            _sm.OnAnimationComplete(1L);

            Assert.AreEqual(0, _spy.PuzzleSolvedCount,
                "Empty color stack must not satisfy win condition (count=0 != stackDepth).");
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
        }

        // ── AC-18a: temp slots with bolts — still wins (excluded from check) ────────

        [Test]
        public void AnimationComplete_WonColorStacksWithTempBolts_StillWins()
        {
            // Win condition evaluates color stacks only; temp slots are irrelevant
            _gsm.SetColorStacks(new[] { 1, 1, 1 }, new[] { 2, 2, 2 });
            _gsm.StackDepth = 3;
            _gsm.SetTempSlots(new[] { 1 }); // Temp slot has a bolt — excluded from win check
            _gsm.TempSlotDepth = 2;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(0, 1, 1L);

            _sm.OnAnimationComplete(1L);

            Assert.AreEqual(1, _spy.PuzzleSolvedCount,
                "TR-SORT-003: temp slots must be excluded from win check; won color stacks must still trigger win.");
            Assert.AreEqual(SortMechState.Win, _sm.CurrentStateForTesting);
        }
    }
}
