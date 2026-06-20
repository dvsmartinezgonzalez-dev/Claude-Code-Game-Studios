using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Fix 3 — frozen-tube turn counter must NOT be consumable via undo/redo cycling.
//            A frozen tube's remaining turns is a pure function of MoveCount
//            (freezeInit − moveCount), and undo decrements MoveCount, so a committed-then-
//            undone move leaves the counter exactly where it started.

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class FrozenUndo_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        private const int FrozenTube = 0;     // color stack 0 starts frozen for 3 turns
        private const int InitialFreeze = 3;

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_FrozenUndoTest");
            _gsm = _go.AddComponent<global::BoltSort.GameStateManager.GameStateManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            global::BoltSort.GameStateManager.GameStateManager.ClearInstanceForTesting();
        }

        /// <summary>
        /// Seeds a 2-color, depth-4 board with one temp slot and marks color stack 0 frozen
        /// for <see cref="InitialFreeze"/> turns. Flat namespace: 0,1 = color stacks; 2 = temp slot.
        /// Color stack 0's top (color 2) is movable out of the frozen tube.
        /// </summary>
        private void SeedFrozenBoard()
        {
            var combined = new List<int>[]
            {
                new List<int> { 1, 1, 1, 2 }, // color stack 0 (frozen) — top 2 can be lifted out
                new List<int> { 2, 2, 2 },    // color stack 1
                new List<int>(),              // temp slot 0
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth:       4,
                tempSlotDepth:    1,
                tempSlotCount:    1,
                colorCount:       2);
            _gsm.SeedFreezeForTesting(new[] { InitialFreeze, 0, 0 });
            _gsm.SetStateForTesting(GSMLifecycleState.Active);
        }

        /// <summary>Commits "lift color 2 out of frozen tube 0 into the temp slot", clears the
        /// in-flight animation guard, then undoes it — one full do/undo cycle.</summary>
        private void DoThenUndoOnce()
        {
            _gsm.SimulateMoveCommitted(src: FrozenTube, dst: 2, colorId: 2, seqId: 1L);
            _gsm.SimulateMoveExecutingExited(1L); // clear in-flight so the undo runs synchronously
            _gsm.UndoRequested();
        }

        // ── A committed (not undone) move DOES decrement the frozen counter ────────

        [Test]
        public void test_gsm_frozen_counter_decrements_on_committed_move()
        {
            // Arrange
            SeedFrozenBoard();
            Assert.AreEqual(InitialFreeze, _gsm.GetFreezeRemaining(FrozenTube),
                "freeze starts at its initial value before any move");

            // Act — commit one move (do NOT undo)
            _gsm.SimulateMoveCommitted(src: FrozenTube, dst: 2, colorId: 2, seqId: 1L);

            // Assert — a committed move is a turn, so the counter drops by exactly one
            Assert.AreEqual(InitialFreeze - 1, _gsm.GetFreezeRemaining(FrozenTube),
                "a committed move must decrement the frozen counter by one");
        }

        // ── Undo restores the frozen counter to its pre-move value ─────────────────

        [Test]
        public void test_gsm_frozen_counter_restored_after_single_undo()
        {
            // Arrange
            SeedFrozenBoard();

            // Act — one do/undo cycle
            DoThenUndoOnce();

            // Assert — counter back to its starting value; the move was not "spent"
            Assert.AreEqual(InitialFreeze, _gsm.GetFreezeRemaining(FrozenTube),
                "undoing the move must restore the frozen counter to its pre-move value");
            Assert.AreEqual(0, _gsm.MoveCount, "move count returns to zero after a do/undo cycle");
        }

        // ── 100× do/undo cannot consume the frozen counter (the abuse) ─────────────

        [Test]
        public void test_gsm_frozen_counter_unchanged_after_100_do_undo_cycles()
        {
            // Arrange
            SeedFrozenBoard();

            // Act — repeatedly move a ball out of the frozen tube and undo it
            for (int i = 0; i < 100; i++)
                DoThenUndoOnce();

            // Assert — the frozen tube is no closer to thawing than when we started
            Assert.AreEqual(InitialFreeze, _gsm.GetFreezeRemaining(FrozenTube),
                "100 do/undo cycles must leave the frozen counter unchanged (no turn abuse)");
            Assert.IsTrue(_gsm.IsTubeFrozen(FrozenTube),
                "the tube must still be frozen — undo/redo cycling cannot unfreeze it");
        }
    }
}
