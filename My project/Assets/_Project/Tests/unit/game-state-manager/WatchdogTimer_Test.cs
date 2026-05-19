using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Story 006 ACs — AC-GSM-18, AC-GSM-19, AC-GSM-20
// Design:    design/gdd/game-state-manager.md  (TR-GSM-004, WDG-01/WDG-02/WDG-03)
// ADR:       docs/architecture/ADR-0006.md
//
// NOTE: Coroutine timing is tested via SimulateWatchdogFire() (synchronous seam).
//       This fires the expiry logic directly without real-time delay.
//       The [UnityTest] async path (WaitForSecondsRealtime) is intentionally skipped
//       for EditMode compatibility; the seam tests the same code path the coroutine calls.

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class WatchdogTimer_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_WatchdogTest");
            _gsm = _go.AddComponent<global::BoltSort.GameStateManager.GameStateManager>();

            // Minimal 2-color, 3-deep board — enough for a bolt to be moved
            // Flat namespace: index 0 = color stack 0, index 1 = color stack 1
            var stacks = new List<int>[]
            {
                new List<int> { 1, 2, 3 },  // stack 0 — full; top = 3
                new List<int> { 2, 1 },      // stack 1 — not full; top = 1
            };
            _gsm.SeedBoardForTesting(
                combinedContents: stacks,
                stackDepth: 3,
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
        /// Commits a bolt move from stack 0 to stack 1 and returns the sequence ID
        /// after the BSM-01 increment (N in AC-GSM-18/19/20 notation).
        /// </summary>
        private long CommitMove()
        {
            long seqBefore = _gsm.CurrentSequenceId;
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 3, seqId: seqBefore);
            return _gsm.CurrentSequenceId; // N = seqBefore + 1
        }

        // ── AC-GSM-18: Watchdog fires — seqId incremented, event emitted, no rollback ──

        [Test]
        public void Test_WatchdogFire_IncrementsCurrentSequenceId()
        {
            // Arrange
            long N = CommitMove(); // seqId after BSM-01

            // Act
            _gsm.SimulateWatchdogFire();

            // Assert — WDG-01: seqId incremented once more
            Assert.AreEqual(N + 1, _gsm.CurrentSequenceId,
                "CurrentSequenceId must be N+1 after watchdog fires (WDG-01)");
        }

        [Test]
        public void Test_WatchdogFire_EmitsBoardRefreshForced_WithNewSeqId()
        {
            // Arrange
            long N = CommitMove();
            long receivedSeqId = -1;
            _gsm.OnBoardRefreshForced += seqId => receivedSeqId = seqId;

            // Act
            _gsm.SimulateWatchdogFire();

            // Assert
            Assert.AreEqual(N + 1, receivedSeqId,
                "OnBoardRefreshForced must carry N+1 (the new seqId after watchdog increment)");
        }

        [Test]
        public void Test_WatchdogFire_CommittedBoltPresentInDestination_NoRollback()
        {
            // Arrange — stack 0 top is colorId=3; move commits it to stack 1
            CommitMove();

            // Act
            _gsm.SimulateWatchdogFire();

            // Assert — WDG-01: no rollback; committed bolt is the new top of stack 1
            var dst = _gsm.StackContents[1];
            Assert.AreEqual(3, dst[dst.Count - 1],
                "Committed bolt must remain at destination after watchdog fires (no rollback)");
        }

        [Test]
        public void Test_MoveCommitted_StartsWatchdog()
        {
            // Act
            CommitMove();

            // Assert — watchdog coroutine reference is set (IsWatchdogRunning)
            Assert.IsTrue(_gsm.IsWatchdogRunning,
                "Watchdog must be running immediately after move_committed is processed");
        }

        [Test]
        public void Test_WatchdogFire_ClearsCoroutineReference()
        {
            // Arrange
            CommitMove();
            Assert.IsTrue(_gsm.IsWatchdogRunning); // sanity

            // Act
            _gsm.SimulateWatchdogFire();

            // Assert — AC-GSM-20: _watchdogCoroutine is null after fire
            Assert.IsFalse(_gsm.IsWatchdogRunning,
                "Watchdog coroutine reference must be null after firing (self-clearing)");
        }

        [Test]
        public void Test_WatchdogFire_OnlyOneEvent_NoDoubleEmit()
        {
            // Arrange
            CommitMove();
            int fireCount = 0;
            _gsm.OnBoardRefreshForced += _ => fireCount++;

            // Act — fire once
            _gsm.SimulateWatchdogFire();

            // Assert — exactly one event
            Assert.AreEqual(1, fireCount,
                "OnBoardRefreshForced must fire exactly once per watchdog expiry");
        }

        // ── AC-GSM-19: Valid exit cancels timer, no board_refresh_forced emitted ─

        [Test]
        public void Test_ValidExit_CancelsWatchdog()
        {
            // Arrange
            long N = CommitMove();
            Assert.IsTrue(_gsm.IsWatchdogRunning); // sanity

            // Act — valid exit arrives before timeout
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — WDG-03: coroutine stopped
            Assert.IsFalse(_gsm.IsWatchdogRunning,
                "Watchdog must be cancelled when OnMoveExecutingExited arrives");
        }

        [Test]
        public void Test_ValidExit_NoBoardRefreshForcedEmitted()
        {
            // Arrange
            long N = CommitMove();
            bool boardRefreshFired = false;
            _gsm.OnBoardRefreshForced += _ => boardRefreshFired = true;

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — coroutine is stopped; fire logic never executes
            Assert.IsFalse(boardRefreshFired,
                "OnBoardRefreshForced must NOT fire when valid exit cancels the watchdog");
        }

        [Test]
        public void Test_ValidExit_SeqIdUnchanged()
        {
            // Arrange
            long N = CommitMove();

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — cancellation must not touch CurrentSequenceId
            Assert.AreEqual(N, _gsm.CurrentSequenceId,
                "CurrentSequenceId must remain N after valid exit — only watchdog increments it");
        }

        // ── AC-GSM-20: Stale exit after watchdog fired — no second event ─────────

        [Test]
        public void Test_StaleExit_AfterWatchdogFired_NoBoardRefreshForced()
        {
            // Arrange
            long N = CommitMove();
            int fireCount = 0;
            _gsm.OnBoardRefreshForced += _ => fireCount++;
            _gsm.SimulateWatchdogFire(); // watchdog fires — count=1, coroutine=null
            Assert.AreEqual(1, fireCount); // sanity

            // Act — stale exit arrives after watchdog already fired
            _gsm.SimulateMoveExecutingExited(N); // seqId is now stale (N vs current N+1)

            // Assert — no second event; CancelWatchdog is a no-op when coroutine is null
            Assert.AreEqual(1, fireCount,
                "OnBoardRefreshForced must NOT fire a second time on a stale exit");
        }

        [Test]
        public void Test_StaleExit_AfterWatchdogFired_CoroutineRemainsNull()
        {
            // Arrange
            long N = CommitMove();
            _gsm.SimulateWatchdogFire();
            Assert.IsFalse(_gsm.IsWatchdogRunning); // sanity — watchdog cleared itself

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — _watchdogCoroutine stays null; no new coroutine started by CancelWatchdog
            Assert.IsFalse(_gsm.IsWatchdogRunning,
                "_watchdogCoroutine must remain null after stale exit (CancelWatchdog is no-op when null)");
        }

        [Test]
        public void Test_StaleExit_AfterWatchdogFired_SeqIdPreserved()
        {
            // Arrange
            long N = CommitMove();
            _gsm.SimulateWatchdogFire();
            long seqAfterFire = _gsm.CurrentSequenceId; // N+1

            // Act
            _gsm.SimulateMoveExecutingExited(N);

            // Assert — stale exit must not change seqId
            Assert.AreEqual(seqAfterFire, _gsm.CurrentSequenceId,
                "CurrentSequenceId must not change after a stale exit following watchdog fire");
        }

        // ── Edge cases ────────────────────────────────────────────────────────────

        [Test]
        public void Test_ConsecutiveMoves_SecondMoveRestartsWatchdog()
        {
            // Arrange — first move starts watchdog
            CommitMove();
            Assert.IsTrue(_gsm.IsWatchdogRunning); // sanity

            // Second move: stack 1 now has [2, 1, 3]; move 3 back to stack 0
            // Stack 0 now has [1, 2] after first move removed 3; add 3 back there
            long N2 = _gsm.CurrentSequenceId;
            _gsm.SimulateMoveCommitted(src: 1, dst: 0, colorId: 3, seqId: N2);

            // Assert — watchdog restarted (still running after second move)
            Assert.IsTrue(_gsm.IsWatchdogRunning,
                "Watchdog must still be running after a second move restarts it");
            Assert.AreEqual(N2 + 1, _gsm.CurrentSequenceId,
                "CurrentSequenceId must increment for second move");
        }

        [Test]
        public void Test_ValidExit_WhenNoWatchdogRunning_IsNoOp()
        {
            // Arrange — no move committed; no watchdog running
            Assert.IsFalse(_gsm.IsWatchdogRunning);
            bool boardRefreshFired = false;
            _gsm.OnBoardRefreshForced += _ => boardRefreshFired = true;

            // Act — spurious exit signal (e.g., from a previous level)
            _gsm.SimulateMoveExecutingExited(seqId: 0);

            // Assert — no crash, no event
            Assert.IsFalse(boardRefreshFired,
                "Spurious OnMoveExecutingExited when no watchdog is running must be a no-op");
            Assert.IsFalse(_gsm.IsWatchdogRunning);
        }
    }
}
