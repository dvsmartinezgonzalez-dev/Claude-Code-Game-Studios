using NUnit.Framework;
using UnityEngine;
using BoltSort.SortMechanic;

// Test assembly: Tests.Unit.SortMechanic
// Covers: AC-01, AC-02, AC-03, AC-04, AC-11, AC-16, TR-SORT-010 (column cap assertion)
//   from design/gdd/sort-mechanic.md (Story 003 scope)
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/sort-mechanic/story-003-move-validation.md

namespace BoltSort.Tests.Unit.SortMechanic
{
    [TestFixture]
    internal sealed class SortMechanic_Validation_Test
    {
        private GameObject _go;
        private global::BoltSort.SortMechanic.SortMechanic _sm;
        private MockGsm _gsm;
        private EventSpy _spy;

        [SetUp]
        public void SetUp()
        {
            // NOTE: Awake() fires via AddComponent<> — all paths go through InjectTapForTesting / InjectForTesting seams.
            _go = new GameObject("SortMechanic_Validation_Test");
            _sm = _go.AddComponent<global::BoltSort.SortMechanic.SortMechanic>();
            _gsm = new MockGsm();
            _spy = new EventSpy();

            // Standard 2-color, depth-3 board:
            //   stack 0: [1]   (one bolt, color 1)
            //   stack 1: []    (empty)
            //   temp slot 0: [] (empty)
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

        // ── Helper ────────────────────────────────────────────────────────────────

        /// Selects bolt from stack 0 (color 1) → BOLT_SELECTED.
        private void SelectBoltFromStack0() => _sm.InjectTapForTesting(0);

        // ── AC-01: Empty destination accepts any color ─────────────────────────────

        [Test]
        public void BoltSelected_EmptyDestination_EmitsMoveCommitted()
        {
            // Arrange: hold bolt from stack 0 (color 1), destination = stack 1 (empty)
            SelectBoltFromStack0();

            // Act
            _sm.InjectTapForTesting(1); // stack 1 is empty

            // Assert
            Assert.AreEqual(1, _spy.MoveCommittedCount,
                "AC-01: empty destination must accept any color and emit move_committed.");
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting);
        }

        [Test]
        public void BoltSelected_EmptyDestination_CommitPayloadCorrect()
        {
            // Arrange
            SelectBoltFromStack0();

            // Act
            _sm.InjectTapForTesting(1);

            // Assert payload: (src=0, dst=1, color=1, seqId)
            Assert.AreEqual(0, _spy.LastCommit.src);
            Assert.AreEqual(1, _spy.LastCommit.dst);
            Assert.AreEqual(1, _spy.LastCommit.color);
        }

        // ── AC-02: Color match + stack not full → move_committed ──────────────────

        [Test]
        public void BoltSelected_ColorMatchNotFull_EmitsMoveCommitted()
        {
            // Arrange: stack 1 has [1,1] — top color matches held color 1, not full (depth=3)
            _gsm.SetColorStacks(new[] { 1 }, new[] { 1, 1 });
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            SelectBoltFromStack0();

            // Act
            _sm.InjectTapForTesting(1); // stack 1 top=1 == held=1, count=2 < cap=3

            // Assert
            Assert.AreEqual(1, _spy.MoveCommittedCount,
                "AC-02: color match on non-full destination must emit move_committed.");
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting);
        }

        // ── AC-03: Color mismatch → move_rejected(COLOR_MISMATCH) ────────────────

        [Test]
        public void BoltSelected_ColorMismatch_EmitsMoveRejected_ColorMismatch()
        {
            // Arrange: stack 1 has [2] — top color 2 ≠ held color 1
            _gsm.SetColorStacks(new[] { 1 }, new[] { 2 });
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            SelectBoltFromStack0();

            // Act
            _sm.InjectTapForTesting(1);

            // Assert
            Assert.AreEqual(1, _spy.MoveRejectedCount,
                "AC-03: color mismatch must emit move_rejected.");
            Assert.AreEqual(MoveRejectedReason.ColorMismatch, _spy.LastRejectedReason,
                "AC-03: rejection reason must be COLOR_MISMATCH.");
            Assert.AreEqual(0, _spy.MoveCommittedCount,
                "AC-03: color mismatch must not emit move_committed.");
        }

        [Test]
        public void BoltSelected_ColorMismatch_EntersInvalidMove()
        {
            // Arrange
            _gsm.SetColorStacks(new[] { 1 }, new[] { 2 });
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            SelectBoltFromStack0();

            // Act
            _sm.InjectTapForTesting(1);

            // Assert: INVALID_MOVE entered (before rejection animation completes)
            Assert.AreEqual(SortMechState.InvalidMove, _sm.CurrentStateForTesting,
                "AC-03: color mismatch must enter INVALID_MOVE.");
        }

        [Test]
        public void BoltSelected_ColorMismatch_AfterRejectionAnimation_ReturnsToBoltSelected()
        {
            // Arrange
            _gsm.SetColorStacks(new[] { 1 }, new[] { 2 });
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            SelectBoltFromStack0();
            _sm.InjectTapForTesting(1); // → INVALID_MOVE

            // Act: rejection animation completes → BOLT_SELECTED re-entry
            _sm.OnRejectionAnimationComplete();

            // Assert
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting,
                "AC-03: after rejection animation, FSM must return to BOLT_SELECTED (bolt still held).");
            Assert.AreEqual(1, _sm.HeldColorIdForTesting,
                "AC-03: held bolt color must be retained across rejection.");
            Assert.AreEqual(0, _sm.HeldSourceIndexForTesting,
                "AC-03: held source index must be retained across rejection.");
        }

        // ── AC-04: Full destination → move_rejected(DESTINATION_FULL) ─────────────

        [Test]
        public void BoltSelected_FullDestination_EmitsMoveRejected_DestinationFull()
        {
            // Arrange: stack 1 has [2,2,2] — full (count=3 == stackDepth=3)
            _gsm.SetColorStacks(new[] { 1 }, new[] { 2, 2, 2 });
            _gsm.StackDepth = 3;
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            SelectBoltFromStack0();

            // Act
            _sm.InjectTapForTesting(1);

            // Assert
            Assert.AreEqual(1, _spy.MoveRejectedCount,
                "AC-04: full destination must emit move_rejected.");
            Assert.AreEqual(MoveRejectedReason.DestinationFull, _spy.LastRejectedReason,
                "AC-04: rejection reason must be DESTINATION_FULL.");
            Assert.AreEqual(0, _spy.MoveCommittedCount);
            Assert.AreEqual(SortMechState.InvalidMove, _sm.CurrentStateForTesting);
        }

        [Test]
        public void BoltSelected_FullDestination_HeldBoltRetained()
        {
            // Arrange
            _gsm.SetColorStacks(new[] { 1 }, new[] { 2, 2, 2 });
            _gsm.StackDepth = 3;
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            SelectBoltFromStack0();
            _sm.InjectTapForTesting(1); // → INVALID_MOVE

            // Simulate: after rejection animation → BOLT_SELECTED
            _sm.OnRejectionAnimationComplete();

            // Assert: bolt still held
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting,
                "AC-04: bolt must remain held after full-destination rejection.");
            Assert.AreEqual(1, _sm.HeldColorIdForTesting);
        }

        // ── AC-11: Temp slot bolt → BOLT_SELECTED ─────────────────────────────────

        [Test]
        public void TempSlot_SingleBolt_EntersBoltSelected()
        {
            // Arrange: temp slot 0 has one bolt (color 2), depth=1 (full) — AC-11 spec
            // flatIndex for temp slot 0 = colorCount = 2
            _gsm.SetColorStacks(new[] { 1 }, new int[0]); // colorCount = 2
            _gsm.SetTempSlots(new[] { 2 });               // temp slot 0: [2]
            _gsm.TempSlotDepth = 1;                       // AC-11: depth=1 containing one bolt
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            // Act: tap temp slot 0 (flatIndex = 2)
            _sm.InjectTapForTesting(2);

            // Assert
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting,
                "AC-11: temp slot bolt tap must enter BOLT_SELECTED.");
            Assert.AreEqual(2, _sm.HeldColorIdForTesting,
                "AC-11: held color must be read from temp slot top bolt.");
            Assert.AreEqual(2, _sm.HeldSourceIndexForTesting,
                "AC-11: held source index must be the temp slot flat index.");
        }

        [Test]
        public void TempSlot_ValidDestination_EmitsMoveCommitted()
        {
            // Arrange: temp slot 0 has [color 1], depth=1 — consistent with AC-11 spec
            _gsm.SetColorStacks(new[] { 2 }, new int[0]); // colorCount=2; stack 0=[2], stack 1=[]
            _gsm.SetTempSlots(new[] { 1 });               // temp slot 0: [1]
            _gsm.TempSlotDepth = 1;                       // AC-11: depth=1 containing one bolt
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            _sm.InjectTapForTesting(2); // IDLE → BOLT_SELECTED from temp slot (flatIndex=2, color=1)

            // Act: tap stack 1 (empty) — valid destination
            _sm.InjectTapForTesting(1);

            // Assert
            Assert.AreEqual(1, _spy.MoveCommittedCount,
                "AC-11: temp slot bolt can be placed on empty color stack.");
            Assert.AreEqual(2, _spy.LastCommit.src, "Source must be temp slot flat index (2).");
            Assert.AreEqual(1, _spy.LastCommit.dst, "Destination must be stack 1.");
        }

        // ── AC-16: Multi-rejection stability ──────────────────────────────────────

        [Test]
        public void MultiRejection_ThreeRejections_ThenLegalMove_FsmStable()
        {
            // Arrange: board matching AC-16 spec exactly
            //   stack 0: [1]     — source (color 1), stackDepth=3
            //   stack 1: [2,2,2] — full
            //   stack 2: [3,3,3] — full
            //   stack 3: [2]     — mismatch (top=2, held=1)
            //   stack 4: []      — empty (legal destination)
            _gsm.SetColorStacks(
                new[] { 1 },
                new[] { 2, 2, 2 },
                new[] { 3, 3, 3 },
                new[] { 2 },
                new int[0]);
            _gsm.StackDepth = 3;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            // Select bolt from stack 0
            _sm.InjectTapForTesting(0); // → BOLT_SELECTED

            // Rejection 1: dest_A = stack 1 (full)
            _sm.InjectTapForTesting(1);
            Assert.AreEqual(SortMechState.InvalidMove, _sm.CurrentStateForTesting);
            _sm.OnRejectionAnimationComplete(); // → BOLT_SELECTED
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting);
            Assert.AreEqual(1, _sm.HeldColorIdForTesting, "Held color must survive rejection 1.");
            Assert.AreEqual(0, _sm.HeldSourceIndexForTesting, "Source index must survive rejection 1.");

            // Rejection 2: dest_B = stack 2 (full)
            _sm.InjectTapForTesting(2);
            Assert.AreEqual(SortMechState.InvalidMove, _sm.CurrentStateForTesting);
            _sm.OnRejectionAnimationComplete(); // → BOLT_SELECTED
            Assert.AreEqual(1, _sm.HeldColorIdForTesting,   "Held color must survive rejection 2.");
            Assert.AreEqual(0, _sm.HeldSourceIndexForTesting, "Held source must survive rejection 2.");

            // Rejection 3: dest_C = stack 3 (mismatch)
            _sm.InjectTapForTesting(3);
            Assert.AreEqual(SortMechState.InvalidMove, _sm.CurrentStateForTesting);
            _sm.OnRejectionAnimationComplete(); // → BOLT_SELECTED
            Assert.AreEqual(1, _sm.HeldColorIdForTesting,   "Held color must survive rejection 3.");
            Assert.AreEqual(0, _sm.HeldSourceIndexForTesting, "Held source must survive rejection 3.");

            // Legal move: legal_dest = stack 4 (empty)
            _sm.InjectTapForTesting(4);

            // Assert
            Assert.AreEqual(3, _spy.MoveRejectedCount,
                "AC-16: exactly 3 move_rejected events must fire across the three rejections.");
            Assert.AreEqual(1, _spy.MoveCommittedCount,
                "AC-16: exactly 1 move_committed must fire on the legal destination tap.");
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting,
                "AC-16: FSM must be in MOVE_EXECUTING after the legal move commits.");
            Assert.AreEqual(0, _spy.LastCommit.src, "Source must be stack 0.");
            Assert.AreEqual(4, _spy.LastCommit.dst, "Destination must be stack 4.");
            Assert.AreEqual(1, _spy.LastCommit.color, "Committed color must be 1.");
        }

        // ── TR-SORT-010: Column cap assertion ─────────────────────────────────────

        [Test]
        public void ColumnCap_AtMaximum_InitializesSuccessfully()
        {
            // Arrange: 16 color stacks + 2 temp slots = 18 == cap (ADR-0014) — must pass
            _gsm.SetColorStacks(
                new[] { 1 },  new[] { 2 },  new[] { 3 },  new[] { 4 },
                new[] { 5 },  new[] { 6 },  new[] { 7 },  new[] { 8 },
                new[] { 9 },  new[] { 10 }, new[] { 11 }, new[] { 12 },
                new[] { 13 }, new[] { 14 }, new[] { 15 }, new[] { 16 });
            _gsm.StackDepth = 3;
            _gsm.SetTempSlots(new int[0], new int[0]); // 2 temp slots, empty

            _sm.InjectForTesting(_gsm);
            var spy2 = new EventSpy();
            spy2.Subscribe(_sm);
            _sm.InitializeBoard();

            // Assert: column cap assertion ran and passed — both guards confirm
            Assert.IsFalse(_sm.InputBlockedForTesting,
                "TR-SORT-010: color_count(16) + temp_slot_count(2) = 18 must NOT trigger level_load_failed.");
            Assert.AreEqual(0, spy2.LevelLoadFailedCount,
                "TR-SORT-010: no upstream assertion must fail; LevelLoadFailed must be 0.");
        }

        [Test]
        public void ColumnCap_Exceeded_EmitsLevelLoadFailed()
        {
            // Arrange: 17 color stacks + 2 temp slots = 19 > 18 (ADR-0014 cap) — must fail
            _gsm.SetColorStacks(
                new[] { 1 },  new[] { 2 },  new[] { 3 },  new[] { 4 },
                new[] { 5 },  new[] { 6 },  new[] { 7 },  new[] { 8 },
                new[] { 9 },  new[] { 10 }, new[] { 11 }, new[] { 12 },
                new[] { 13 }, new[] { 14 }, new[] { 15 }, new[] { 16 }, new[] { 17 });
            _gsm.StackDepth = 3;
            _gsm.SetTempSlots(new int[0], new int[0]); // 2 temp slots

            _sm.InjectForTesting(_gsm);
            var spy2 = new EventSpy();
            spy2.Subscribe(_sm);
            _sm.InitializeBoard();

            // Assert
            Assert.IsTrue(_sm.InputBlockedForTesting,
                "TR-SORT-010: color_count(17) + temp_slot_count(2) = 19 must trigger level_load_failed.");
            Assert.AreEqual(1, spy2.LevelLoadFailedCount,
                "TR-SORT-010: level_load_failed must be emitted once on column cap violation.");
        }

        [Test]
        public void ColumnCap_BelowMaximum_InitializesSuccessfully()
        {
            // Arrange: 2 color stacks + 1 temp slot = 3 — comfortably within cap
            // This is the default SetUp board — verify it passes
            Assert.IsFalse(_sm.InputBlockedForTesting,
                "TR-SORT-010: default 2-color + 1-temp board (=3) must not trigger level_load_failed.");
        }

        // ── IsLegalMove guarded conditional — boundary values ─────────────────────

        [Test]
        public void BoltSelected_DestinationAtCapMinus1_ColorMatch_EmitsMoveCommitted()
        {
            // Stack with count = cap - 1 (one slot remaining), top color matches held
            // stack 1: [1,1] with stackDepth=3 → count=2, cap=3, one slot open, top=1=held(1)
            _gsm.SetColorStacks(new[] { 1 }, new[] { 1, 1 });
            _gsm.StackDepth = 3;
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            SelectBoltFromStack0();

            _sm.InjectTapForTesting(1);

            Assert.AreEqual(1, _spy.MoveCommittedCount,
                "Boundary: count=cap-1, color match — must be legal (not yet full).");
        }

        [Test]
        public void BoltSelected_DestinationAtCap_EmitsMoveRejected_DestinationFull()
        {
            // Stack with count == cap (full), same color as held — still rejected (full beats match)
            // stack 1: [1,1,1] with stackDepth=3 → count=3 == cap=3, full
            _gsm.SetColorStacks(new[] { 1 }, new[] { 1, 1, 1 });
            _gsm.StackDepth = 3;
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            SelectBoltFromStack0();

            _sm.InjectTapForTesting(1);

            Assert.AreEqual(1, _spy.MoveRejectedCount,
                "Boundary: count=cap, same color — must be illegal (full takes priority over color match).");
            Assert.AreEqual(MoveRejectedReason.DestinationFull, _spy.LastRejectedReason,
                "Boundary: full-destination rejection must carry DESTINATION_FULL reason.");
            Assert.AreEqual(0, _spy.MoveCommittedCount);
        }
    }
}
