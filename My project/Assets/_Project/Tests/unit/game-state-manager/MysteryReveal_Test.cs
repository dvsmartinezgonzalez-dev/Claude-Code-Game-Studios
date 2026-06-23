using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Phase-2 Mystery Ball — reveal-on-expose, Magnet-after-reveal (rule #6),
//            and undo re-hide (rule #10). Mystery bolts are encoded as a NEGATIVE color id
//            (hidden color = abs); reveal flips the stored value positive.
// Design:    design/gdd/phase2-technical-design.md ; design/gdd/game-state-manager.md
// ADR:       docs/architecture/adr-0006-board-state-representation.md

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class MysteryReveal_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_MysteryTest");
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
        /// Seeds a 2-color, 4-deep board with 1 temp slot (depth 4) and activates it. Flat
        /// namespace: 0 = color stack 0, 1 = color stack 1, 2 = temp slot 0. When
        /// <paramref name="levelId"/> ≥ 50 the Magnet mechanic is active for the move.
        /// </summary>
        private void SeedAndActivate(List<int>[] combined, int levelId = 1)
        {
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth:       4,
                tempSlotDepth:    4,
                tempSlotCount:    1,
                colorCount:       2);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);
            _gsm.SeedLevelForTesting(levelId);
        }

        private static int[] Arr(IReadOnlyList<int> col)
        {
            var a = new int[col.Count];
            for (int i = 0; i < col.Count; i++) a[i] = col[i];
            return a;
        }

        // ── Rule #3/#4: reveal when the covering bolt leaves (no Magnet) ─────────────

        /// <summary>
        /// A mystery bolt (encoded -1, hidden color 1) sits under a normal color-2 bolt. Moving the
        /// covering bolt away exposes the mystery top, which is revealed by flipping the stored
        /// value to its positive color and firing OnMysteryRevealed with the real color.
        /// </summary>
        [Test]
        public void Test_MysteryReveal_CoveringBoltMoved_RevealsRealColor()
        {
            // Arrange — stack0 = [mystery(1), 2]; temp empty. Magnet off (level 1).
            var combined = new List<int>[]
            {
                new List<int> { -1, 2 }, // index 0: mystery hidden color 1 at bottom, color 2 on top
                new List<int> { 1 },     // index 1: color stack 1
                new List<int>(),         // index 2: temp slot 0
            };
            SeedAndActivate(combined, levelId: 1);

            int revealedFlat = -99, revealedColor = -99, revealCount = 0;
            _gsm.OnMysteryRevealed += (flat, color) => { revealedFlat = flat; revealedColor = color; revealCount++; };

            // Act — move the covering color-2 bolt from stack0 to temp slot 0
            _gsm.SimulateMoveCommitted(src: 0, dst: 2, colorId: 2, seqId: 0L);

            // Assert — mystery top is now the positive real color (1), event fired once with (0,1)
            Assert.AreEqual(new[] { 1 }, Arr(_gsm.StackContents[0]),
                "stack0 top must flip from mystery -1 to revealed +1");
            Assert.AreEqual(1, revealCount,    "OnMysteryRevealed must fire exactly once");
            Assert.AreEqual(0, revealedFlat,   "reveal must report the source column index");
            Assert.AreEqual(1, revealedColor,  "reveal must report the hidden color (abs)");
        }

        // ── Rule #10: undo re-hides a revealed mystery ──────────────────────────────

        /// <summary>
        /// Undoing the move that revealed a mystery returns the bolt to its hidden (negative) state.
        /// The reverse re-stacks the covering bolt, so the bolt is hidden again AND covered.
        /// </summary>
        [Test]
        public void Test_MysteryReveal_Undo_RestoresHiddenState()
        {
            // Arrange — reveal first (as in the test above)
            var combined = new List<int>[]
            {
                new List<int> { -1, 2 },
                new List<int> { 1 },
                new List<int>(),
            };
            SeedAndActivate(combined, levelId: 1);
            _gsm.SimulateMoveCommitted(src: 0, dst: 2, colorId: 2, seqId: 0L);
            _gsm.SimulateMoveExecutingExited(0L); // clear _isAnimationInFlight so UndoRequested executes
            Assert.AreEqual(new[] { 1 }, Arr(_gsm.StackContents[0]), "Precondition: mystery revealed (+1)");

            // Act
            _gsm.UndoRequested();

            // Assert — bolt is mystery again (-1) and re-covered by the returned color-2 bolt
            Assert.AreEqual(new[] { -1, 2 }, Arr(_gsm.StackContents[0]),
                "undo must re-hide the mystery (-1) and restore the covering bolt");
            Assert.AreEqual(0, _gsm.StackContents[2].Count, "temp slot must be empty again after undo");
        }

        // ── Rule #6: a revealed mystery joins the Magnet chain when its color matches ─

        /// <summary>
        /// With Magnet active (L50+), pulling consecutive same-color bolts exposes a mystery whose
        /// hidden color equals the chain color — it reveals and joins the same chain (one logical
        /// move). Undo reverses the whole chain and re-hides the mystery.
        /// </summary>
        [Test]
        public void Test_MysteryReveal_MagnetChain_RevealedMatchJoinsChain_AndUndoRehides()
        {
            // Arrange — stack0 = [mystery(2), 2, 2]; temp0 = [2] (deposit target). Magnet on (L50).
            var combined = new List<int>[]
            {
                new List<int> { -2, 2, 2 }, // mystery hidden color 2 under two color-2 bolts
                new List<int> { 1, 1, 1, 1 }, // unrelated full color-1 stack (keeps board un-won)
                new List<int> { 2 },          // temp slot 0 — chain destination
            };
            SeedAndActivate(combined, levelId: 50);

            int revealCount = 0, lastRevealColor = -99;
            _gsm.OnMysteryRevealed += (flat, color) => { revealCount++; lastRevealColor = color; };

            // Act — tap the color-2 top of stack0 onto temp0; Magnet pulls the chain
            _gsm.SimulateMoveCommitted(src: 0, dst: 2, colorId: 2, seqId: 0L);

            // Assert — all three color-2 bolts (incl. the revealed mystery) moved as ONE chain
            Assert.AreEqual(3, _gsm.LastMoveBallCount,
                "Magnet must chain 3 bolts: 2 normal + 1 revealed mystery (rule #6)");
            Assert.AreEqual(0, _gsm.StackContents[0].Count, "source stack must be emptied by the chain");
            Assert.AreEqual(new[] { 2, 2, 2, 2 }, Arr(_gsm.StackContents[2]),
                "temp slot must hold all four color-2 bolts after the chain");
            Assert.AreEqual(1, revealCount,     "the single buried mystery must reveal exactly once");
            Assert.AreEqual(2, lastRevealColor, "revealed color must be the hidden color 2");

            // Act — undo the whole chain
            _gsm.SimulateMoveExecutingExited(0L);
            _gsm.UndoRequested();

            // Assert — board restored exactly, mystery hidden (-2) again at the bottom
            Assert.AreEqual(new[] { -2, 2, 2 }, Arr(_gsm.StackContents[0]),
                "undo must restore the source chain and re-hide the mystery (-2)");
            Assert.AreEqual(new[] { 2 }, Arr(_gsm.StackContents[2]), "temp slot restored to its single bolt");
        }

        // ── Rule #6 (negative): a revealed mystery whose color differs stops the chain ─

        /// <summary>
        /// With Magnet active, a mystery exposed mid-chain whose hidden color does NOT match the
        /// chain color is revealed but NOT pulled — the chain stops at it. Undo still re-hides it.
        /// </summary>
        [Test]
        public void Test_MysteryReveal_MagnetChain_RevealedMismatchStopsChain()
        {
            // Arrange — stack0 = [mystery(1), 2, 2]; temp0 = [2]. Magnet on (L50).
            var combined = new List<int>[]
            {
                new List<int> { -1, 2, 2 }, // mystery hidden color 1 (≠ chain color 2)
                new List<int> { 1, 1, 1, 1 },
                new List<int> { 2 },
            };
            SeedAndActivate(combined, levelId: 50);

            // Act
            _gsm.SimulateMoveCommitted(src: 0, dst: 2, colorId: 2, seqId: 0L);

            // Assert — only the two color-2 bolts chained; mystery revealed (+1) but left in place
            Assert.AreEqual(2, _gsm.LastMoveBallCount, "chain must stop at the non-matching revealed mystery");
            Assert.AreEqual(new[] { 1 }, Arr(_gsm.StackContents[0]),
                "revealed mystery (+1) must remain at the source top, not joined to the chain");
            Assert.AreEqual(new[] { 2, 2, 2 }, Arr(_gsm.StackContents[2]),
                "temp slot must hold the original bolt plus the two chained color-2 bolts");

            // Act — undo
            _gsm.SimulateMoveExecutingExited(0L);
            _gsm.UndoRequested();

            // Assert — restored and re-hidden
            Assert.AreEqual(new[] { -1, 2, 2 }, Arr(_gsm.StackContents[0]),
                "undo must restore the chain and re-hide the mystery (-1)");
            Assert.AreEqual(new[] { 2 }, Arr(_gsm.StackContents[2]), "temp slot restored to its single bolt");
        }
    }
}
