using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Extra-Tube mechanic (header redesign) — GSM.ApplyExtraTube() runtime helper tubes.
//            Helper tubes are extra temp-slot columns appended to the END of the flat
//            namespace, so existing indices never shift and undo entries stay valid.

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class HelperTube_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_HelperTubeTest");
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
        /// Flat namespace: 0 = color stack 0, 1 = color stack 1, 2 = temp slot 0.
        /// MAX helper capacity therefore equals stackDepth = 4.
        /// </summary>
        private void SeedAndActivate(List<int>[] combined)
        {
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth:       4,
                tempSlotDepth:    1,
                tempSlotCount:    1,
                colorCount:       2);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);
        }

        private static List<int>[] FreshBoard() => new List<int>[]
        {
            new List<int> { 1, 1, 1, 1 }, // color stack 0
            new List<int> { 2, 2, 2, 2 }, // color stack 1
            new List<int>(),              // temp slot 0
        };

        // ── First use creates a capacity-1 helper appended at the end ──────────────

        [Test]
        public void test_gsm_apply_extra_tube_first_use_creates_capacity1_helper()
        {
            // Arrange
            SeedAndActivate(FreshBoard());

            // Act
            int affected = _gsm.ApplyExtraTube();

            // Assert — appended as flat index 3 (colorCount 2 + original temp 1)
            Assert.AreEqual(3, affected);
            Assert.AreEqual(1, _gsm.HelperTubeCount);
            Assert.AreEqual(2, _gsm.TempSlotCount);                 // 1 original + 1 helper
            Assert.AreEqual(1, _gsm.GetColumnCapacity(3));          // helper capacity 1
            Assert.AreEqual(1, _gsm.GetColumnCapacity(2));          // original temp untouched
            Assert.AreEqual(0, _gsm.StackContents[3].Count);       // empty
        }

        // ── Repeated use grows the last helper to MAX, then starts a new one ───────

        [Test]
        public void test_gsm_apply_extra_tube_grows_last_helper_until_max_then_creates_new()
        {
            // Arrange — MAX capacity == stackDepth == 4
            SeedAndActivate(FreshBoard());

            // Act — 4 uses fill one helper to capacity 4
            _gsm.ApplyExtraTube(); // new helper, cap 1 (col 3)
            _gsm.ApplyExtraTube(); // grow -> 2
            _gsm.ApplyExtraTube(); // grow -> 3
            _gsm.ApplyExtraTube(); // grow -> 4 (MAX)

            // Assert — still one helper, now at MAX
            Assert.AreEqual(1, _gsm.HelperTubeCount);
            Assert.AreEqual(4, _gsm.GetColumnCapacity(3));
            Assert.AreEqual(2, _gsm.TempSlotCount);

            // Act — 5th use cannot grow past MAX, so a new helper begins
            int affected = _gsm.ApplyExtraTube();

            // Assert
            Assert.AreEqual(4, affected);                   // new helper at flat index 4
            Assert.AreEqual(2, _gsm.HelperTubeCount);
            Assert.AreEqual(3, _gsm.TempSlotCount);
            Assert.AreEqual(4, _gsm.GetColumnCapacity(3));  // first helper still MAX
            Assert.AreEqual(1, _gsm.GetColumnCapacity(4));  // new helper cap 1
        }

        // ── Not allowed outside ACTIVE ─────────────────────────────────────────────

        [Test]
        public void test_gsm_apply_extra_tube_when_not_active_is_noop()
        {
            // Arrange — seed but force COMPLETE (e.g. level already won)
            SeedAndActivate(FreshBoard());
            _gsm.SetStateForTesting(GSMLifecycleState.Complete);

            // Act
            int affected = _gsm.ApplyExtraTube();

            // Assert — rejected, board shape unchanged
            Assert.AreEqual(-1, affected);
            Assert.AreEqual(0, _gsm.HelperTubeCount);
            Assert.AreEqual(1, _gsm.TempSlotCount);
        }

        // ── Adding a helper preserves existing indices + undo validity ────────────

        [Test]
        public void test_gsm_apply_extra_tube_preserves_existing_indices_and_undo()
        {
            // Arrange — board with a movable top on color stack 0
            var combined = new List<int>[]
            {
                new List<int> { 1, 1, 1, 2 }, // color stack 0 (top = 2)
                new List<int> { 2, 2, 2 },    // color stack 1
                new List<int>(),              // temp slot 0
            };
            SeedAndActivate(combined);

            // Act 1 — add a helper tube (flat index 3)
            int helper = _gsm.ApplyExtraTube();
            Assert.AreEqual(3, helper);

            // Act 2 — move the top of stack 0 into the helper, then undo it
            _gsm.SimulateMoveCommitted(src: 0, dst: 3, colorId: 2, seqId: 1L);
            Assert.AreEqual(1, _gsm.StackContents[3].Count, "ball should land in the helper");
            _gsm.SimulateMoveExecutingExited(1L); // clear in-flight so undo runs synchronously
            _gsm.UndoRequested();

            // Assert — move reverted, helper still present but empty (tube add is not undoable)
            Assert.AreEqual(0, _gsm.StackContents[3].Count, "helper emptied by undo");
            Assert.AreEqual(2, _gsm.StackContents[0][_gsm.StackContents[0].Count - 1],
                            "ball returned to source top");
            Assert.AreEqual(1, _gsm.HelperTubeCount, "undo must not remove the helper tube");
            Assert.AreEqual(2, _gsm.TempSlotCount);
        }
    }
}
