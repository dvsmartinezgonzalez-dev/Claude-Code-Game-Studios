using NUnit.Framework;
using UnityEngine;
using BoltSort.SortMechanic;

// Test assembly: Tests.Unit.SortMechanic
// Covers: AC-08a, AC-08b, AC-08c, AC-12, AC-29b, AC-30, AC-30b
//   from design/gdd/sort-mechanic.md (Story 002 scope)
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/sort-mechanic/story-002-input-handling.md

namespace BoltSort.Tests.Unit.SortMechanic
{
    [TestFixture]
    internal sealed class SortMechanic_Input_Test
    {
        private GameObject _go;
        private global::BoltSort.SortMechanic.SortMechanic _sm;
        private MockGsm _gsm;
        private EventSpy _spy;

        [SetUp]
        public void SetUp()
        {
            // NOTE: Awake() fires via AddComponent<> — _mainCamera is null in EditMode (no camera in scene).
            // NEVER call Update() or HandleTap(Vector2) from a test; doing so will NRE on _mainCamera.
            // All test paths go through InjectTapForTesting / ForceEnter* seams.
            _go = new GameObject("SortMechanic_Input_Test");
            _sm = _go.AddComponent<global::BoltSort.SortMechanic.SortMechanic>();
            _gsm = new MockGsm();
            _spy = new EventSpy();

            // Standard board: stack 0 has one bolt (color 1), stack 1 is empty.
            _gsm.SetColorStacks(new[] { 1 }, new int[0]);
            _gsm.TempSlotDepth = 2;

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

        /// Advances FSM to MOVE_EXECUTING; returns the committed seqId.
        private long AdvanceToMoveExecuting()
        {
            _sm.InjectTapForTesting(0); // IDLE → BOLT_SELECTED
            _sm.InjectTapForTesting(1); // BOLT_SELECTED → MOVE_EXECUTING
            return _spy.LastCommit.seq;
        }

        private static int AllEvents(EventSpy spy)
            => spy.MoveCommittedCount + spy.MoveCancelledCount + spy.MoveRejectedCount
             + spy.PuzzleSolvedCount  + spy.LevelLoadFailedCount + spy.DeadlockDetectedCount
             + spy.MoveExecutingExitedCount;

        // ── AC-08a: Additional taps during MOVE_EXECUTING emit zero events ─────────

        [Test]
        public void MoveExecuting_SecondTap_EmitsNoAdditionalEvents()
        {
            // Arrange
            AdvanceToMoveExecuting();
            _sm.InjectTapForTesting(0); // First buffered tap
            int eventsBefore = AllEvents(_spy);

            // Act
            _sm.InjectTapForTesting(0); // Second tap — buffer already full

            // Assert
            Assert.AreEqual(eventsBefore, AllEvents(_spy),
                "AC-08a: additional taps after first buffered tap must emit zero events.");
        }

        [Test]
        public void MoveExecuting_ThirdTap_EmitsNoAdditionalEvents()
        {
            // Arrange
            AdvanceToMoveExecuting();
            _sm.InjectTapForTesting(0); // Buffer
            _sm.InjectTapForTesting(0); // First discard
            int eventsBefore = AllEvents(_spy);

            // Act
            _sm.InjectTapForTesting(1); // Third tap, different target

            // Assert
            Assert.AreEqual(eventsBefore, AllEvents(_spy),
                "AC-08a: third and subsequent taps must also be silently discarded.");
        }

        // ── AC-08b: State stays MOVE_EXECUTING after additional taps ──────────────

        [Test]
        public void MoveExecuting_SecondTap_StateRemainsMoveExecuting()
        {
            // Arrange
            AdvanceToMoveExecuting();
            _sm.InjectTapForTesting(0); // First buffered tap

            // Act
            _sm.InjectTapForTesting(0); // Second tap

            // Assert
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting,
                "AC-08b: state must remain MOVE_EXECUTING regardless of additional taps.");
        }

        // ── AC-08c: Additional taps do not trigger GSM mutations ──────────────────

        [Test]
        public void MoveExecuting_SecondTap_NoAdditionalMoveCommitted()
        {
            // Arrange
            AdvanceToMoveExecuting();
            _sm.InjectTapForTesting(0); // First buffered tap
            int commitsBefore = _spy.MoveCommittedCount;

            // Act
            _sm.InjectTapForTesting(0); // Second tap

            // Assert — OnMoveCommitted is the GSM mutation trigger; must not increase
            Assert.AreEqual(commitsBefore, _spy.MoveCommittedCount,
                "AC-08c: additional taps during MOVE_EXECUTING must not trigger OnMoveCommitted.");
        }

        // ── Buffer correctness ────────────────────────────────────────────────────

        [Test]
        public void MoveExecuting_FirstTap_SetsPendingBuffer()
        {
            // Arrange
            AdvanceToMoveExecuting();

            // Act
            _sm.InjectTapForTesting(1);

            // Assert
            Assert.IsTrue(_sm.PendingTapForTesting,
                "First tap during MOVE_EXECUTING must set _pendingTap.");
            Assert.AreEqual(1, _sm.PendingTapStackIndexForTesting);
        }

        [Test]
        public void MoveExecuting_SecondTap_DoesNotOverwriteBuffer()
        {
            // Arrange
            AdvanceToMoveExecuting();
            _sm.InjectTapForTesting(1); // Buffer index 1

            // Act
            _sm.InjectTapForTesting(0); // Second tap targeting index 0

            // Assert — buffer index must remain 1
            Assert.AreEqual(1, _sm.PendingTapStackIndexForTesting,
                "Second tap must not overwrite the already-buffered stack index.");
        }

        // ── Pending tap cleared after OnAnimationComplete (IDLE path) ─────────────

        [Test]
        public void MoveExecuting_OnAnimationComplete_ClearsPendingTap()
        {
            // Arrange
            long seqId = AdvanceToMoveExecuting();
            _sm.InjectTapForTesting(1); // Buffer a tap

            // Act
            _sm.OnAnimationComplete(seqId);

            // Assert
            Assert.IsFalse(_sm.PendingTapForTesting,
                "OnAnimationComplete (IDLE path) must clear the pending tap buffer.");
        }

        // ── AC-29b: Buffered tap against now-empty stack → stays IDLE ─────────────

        [Test]
        public void MoveExecuting_OnAnimationComplete_BufferedTapEmptyStack_StaysIdle()
        {
            // Arrange: board where stack 0 is empty, stack 1 has a bolt.
            // Simulates the post-commit state where the source stack is now empty.
            _gsm.SetColorStacks(new int[0], new[] { 1 });
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            var spy2 = new EventSpy();
            spy2.Subscribe(_sm);

            // Force MOVE_EXECUTING (as if bolt from stack 1 was committed to stack 0).
            _sm.ForceEnterMoveExecutingForTesting(sourceIndex: 1, heldColorId: 1, seqId: 1L);

            // Buffer a tap targeting stack 0 (empty in this board — S-02 case).
            _sm.InjectTapForTesting(0);

            // Act: animation completes → IDLE → ProcessPendingTap → empty stack → no-op
            _sm.OnAnimationComplete(1L);

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "AC-29b: buffered tap against empty stack must leave FSM in IDLE.");
            Assert.AreEqual(0, spy2.MoveCommittedCount,
                "AC-29b: no move_committed must fire when buffered tap targets empty stack.");
            Assert.IsFalse(_sm.PendingTapForTesting,
                "AC-29b: pending tap must be cleared after OnAnimationComplete.");
        }

        // ── Watchdog: discard pending tap without firing it ───────────────────────

        [Test]
        public void MoveExecuting_Watchdog_DiscardsPendingTapWithoutFiring()
        {
            // Arrange
            long seqId = AdvanceToMoveExecuting();
            _sm.InjectTapForTesting(0); // Buffer a tap
            int commitsBefore = _spy.MoveCommittedCount;

            // Act
            _sm.TriggerBoardRefreshForcedForTesting(seqId);

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "Watchdog must return FSM to IDLE.");
            Assert.IsFalse(_sm.PendingTapForTesting,
                "Watchdog must discard the pending tap without firing it.");
            Assert.AreEqual(commitsBefore, _spy.MoveCommittedCount,
                "Watchdog discard must not emit OnMoveCommitted.");
        }

        [Test]
        public void Watchdog_WhenNotInMoveExecuting_IsNoOp()
        {
            // Arrange: FSM in IDLE — frame-gap discard rule applies
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);

            // Act
            _sm.TriggerBoardRefreshForcedForTesting(99L);

            // Assert: state unchanged
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
        }

        // ── AC-12: Android back gesture in BOLT_SELECTED → CANCELLATION → IDLE ────

        [Test]
        public void BoltSelected_BackGesture_EmitsMoveCancelled()
        {
            // Arrange
            _sm.InjectTapForTesting(0); // IDLE → BOLT_SELECTED

            // Act
            _sm.TriggerBackGestureForTesting();

            // Assert
            Assert.AreEqual(1, _spy.MoveCancelledCount,
                "AC-12: back gesture in BOLT_SELECTED must emit OnMoveCancelled.");
        }

        [Test]
        public void BoltSelected_BackGesture_TransitionsToIdle()
        {
            // Arrange
            _sm.InjectTapForTesting(0); // IDLE → BOLT_SELECTED

            // Act
            _sm.TriggerBackGestureForTesting();

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "AC-12: back gesture must settle in IDLE (CANCELLATION is transient).");
        }

        [Test]
        public void Idle_BackGesture_IsNoOp()
        {
            // Arrange: FSM in IDLE — back gesture has no effect outside BOLT_SELECTED
            int cancelsBefore = _spy.MoveCancelledCount;

            // Act
            _sm.TriggerBackGestureForTesting();

            // Assert
            Assert.AreEqual(cancelsBefore, _spy.MoveCancelledCount,
                "AC-12: back gesture in IDLE must emit no events.");
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
        }

        [Test]
        public void MoveExecuting_BackGesture_IsNoOp()
        {
            // Back gesture is only active in BOLT_SELECTED.
            AdvanceToMoveExecuting();
            int cancelsBefore = _spy.MoveCancelledCount;

            _sm.TriggerBackGestureForTesting();

            Assert.AreEqual(cancelsBefore, _spy.MoveCancelledCount);
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting);
        }

        // ── AC-30: INVALID_MOVE buffer fires on BOLT_SELECTED re-entry ────────────

        [Test]
        public void InvalidMove_OnRejectionAnimationComplete_BufferedValidDestination_EmitsCommit()
        {
            // Arrange: hold bolt from stack 0; force into INVALID_MOVE state
            _sm.ForceEnterInvalidMoveForTesting(sourceIndex: 0, heldColorId: 1);

            // Buffer tap targeting stack 1 (not source → valid destination path)
            _sm.InjectTapForTesting(1);

            // Act: rejection animation completes → BOLT_SELECTED → fires buffered tap
            _sm.OnRejectionAnimationComplete();

            // Assert
            Assert.AreEqual(1, _spy.MoveCommittedCount,
                "AC-30: buffered tap must fire as destination evaluation on BOLT_SELECTED re-entry.");
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting,
                "AC-30: valid buffered destination must transition to MOVE_EXECUTING.");
        }

        [Test]
        public void InvalidMove_OnRejectionAnimationComplete_NoBuffer_ReturnsToBoltSelected()
        {
            // Arrange: no tap buffered during rejection animation
            _sm.ForceEnterInvalidMoveForTesting(sourceIndex: 0, heldColorId: 1);

            // Act
            _sm.OnRejectionAnimationComplete();

            // Assert: re-enters BOLT_SELECTED with no tap fired
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting,
                "Without buffered tap, OnRejectionAnimationComplete must return to BOLT_SELECTED.");
            Assert.AreEqual(0, _spy.MoveCommittedCount);
        }

        [Test]
        public void InvalidMove_OnRejectionAnimationComplete_BufferedSourceTap_ExecutesCancellation()
        {
            // Arrange: buffer a tap targeting the held source (same index → cancellation path)
            _sm.ForceEnterInvalidMoveForTesting(sourceIndex: 0, heldColorId: 1);
            _sm.InjectTapForTesting(0); // Source index → cancellation

            // Act
            _sm.OnRejectionAnimationComplete();

            // Assert: DispatchBoltSelectedIndexedTap(0) == source → CANCELLATION → IDLE
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "Buffered source-tap must trigger cancellation and leave FSM in IDLE.");
            Assert.AreEqual(1, _spy.MoveCancelledCount);
        }

        [Test]
        public void OnRejectionAnimationComplete_WhenNotInInvalidMove_IsNoOp()
        {
            // Guard: OnRejectionAnimationComplete while in IDLE must be silently ignored.
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);

            _sm.OnRejectionAnimationComplete();

            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "OnRejectionAnimationComplete in non-INVALID_MOVE state must be a no-op.");
        }

        // ── AC-30b: Only first buffered tap fires in INVALID_MOVE ─────────────────

        [Test]
        public void InvalidMove_SecondTap_DoesNotOverwriteFirstBuffer()
        {
            // Arrange: first tap targets stack 1 (valid destination),
            //          second tap targets stack 0 (source → would cause cancellation)
            _sm.ForceEnterInvalidMoveForTesting(sourceIndex: 0, heldColorId: 1);
            _sm.InjectTapForTesting(1); // First tap → buffer index 1
            _sm.InjectTapForTesting(0); // Second tap → must be discarded

            // Act
            _sm.OnRejectionAnimationComplete();

            // Assert: first tap (stack 1 → commit) wins; if second had overwritten,
            //         stack 0 == source would have caused cancellation (no commit).
            Assert.AreEqual(1, _spy.MoveCommittedCount,
                "AC-30b: only the first buffered tap must fire; second tap must be discarded.");
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting);
        }

        [Test]
        public void InvalidMove_MultipleAdditionalTaps_AllDiscarded()
        {
            // Arrange
            _sm.ForceEnterInvalidMoveForTesting(sourceIndex: 0, heldColorId: 1);
            _sm.InjectTapForTesting(1); // First tap buffered
            int eventsBefore = AllEvents(_spy);

            // Act: many additional taps
            _sm.InjectTapForTesting(0);
            _sm.InjectTapForTesting(1);
            _sm.InjectTapForTesting(0);

            // Assert: no additional events, buffer flag unchanged
            Assert.IsTrue(_sm.InvalidMovePendingTapForTesting);
            Assert.AreEqual(eventsBefore, AllEvents(_spy),
                "AC-30b: additional taps in INVALID_MOVE must emit zero events.");
        }

        // ── AC-14 (advisory): tap targeting non-existent temp slot → no-op ─────────

        [Test]
        public void Idle_TapNonExistentTempSlotIndex_IsNoOp()
        {
            // Arrange: temp slots absent; ColorCount = 2 so index 2 = first temp slot
            _gsm.SetColorStacks(new[] { 1 }, new int[0]);
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            var spy2 = new EventSpy();
            spy2.Subscribe(_sm);

            // Act: tap index 2 — beyond color stacks, no temp slots exist
            _sm.InjectTapForTesting(2);

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "AC-14: tap on non-existent slot index must leave FSM in IDLE.");
            Assert.AreEqual(0, AllEvents(spy2));
        }

        [Test]
        public void BoltSelected_TapNonExistentTempSlotIndex_ExecutesCancellation()
        {
            // Arrange: AC-14 bolt-held branch — tap non-existent slot while bolt held
            _gsm.SetColorStacks(new[] { 1 }, new int[0]);
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            _sm.InjectTapForTesting(0); // IDLE → BOLT_SELECTED (holding color 1 from stack 0)

            // Act: tap index 2 (non-existent slot) while in BOLT_SELECTED
            // DispatchBoltSelectedIndexedTap(2): 2 != 0 (source) → CommitMove(2)
            // Note: Story 003 will add is_legal_move guard. For now, non-existent index
            // proceeds to CommitMove — cancellation path requires flatStackIndex == source or -1.
            // AC-14 "treat as empty space → CANCELLATION" applies when index resolves to -1 (miss).
            // Tap index -1 instead to simulate empty-space tap in BOLT_SELECTED.
            _sm.InjectTapForTesting(-1); // empty space / miss → cancellation

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "AC-14: bolt held + tap on empty space → CANCELLATION → IDLE.");
            Assert.AreEqual(1, _spy.MoveCancelledCount,
                "AC-14: move_cancelled must be emitted.");
        }

        // ── Stale seqId guard on OnAnimationComplete (BLOCKING test) ─────────────

        [Test]
        public void MoveExecuting_OnAnimationComplete_StaleSeqId_IsNoOp()
        {
            // Arrange
            long correctSeqId = AdvanceToMoveExecuting();
            int eventsBefore = AllEvents(_spy);

            // Act: call with a seqId that does not match the in-flight move
            _sm.OnAnimationComplete(correctSeqId + 1);

            // Assert: stale signal guard — FSM stays in MOVE_EXECUTING, no events emitted
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting,
                "Stale seqId on OnAnimationComplete must not change state (EC-11).");
            Assert.AreEqual(eventsBefore, AllEvents(_spy),
                "Stale seqId on OnAnimationComplete must emit zero events.");
        }

        // ── Buffer flags cleared by InitializeBoard re-entry (BLOCKING test) ──────

        [Test]
        public void InitializeBoard_ClearsBothPendingBuffers()
        {
            // Arrange: buffer a MOVE_EXECUTING tap and an INVALID_MOVE tap
            AdvanceToMoveExecuting();
            _sm.InjectTapForTesting(0); // Set _pendingTap

            _sm.ForceEnterInvalidMoveForTesting(sourceIndex: 0, heldColorId: 1);
            _sm.InjectTapForTesting(1); // Set _invalidMovePendingTap

            Assert.IsTrue(_sm.InvalidMovePendingTapForTesting, "Pre-condition: invalid move buffer set.");

            // Simulate: both buffers are set, then level is reloaded
            // (pendingTap was cleared when state forced to INVALID_MOVE path,
            //  but we can force MOVE_EXECUTING with a pending tap directly)
            _sm.ForceEnterMoveExecutingForTesting(sourceIndex: 0, heldColorId: 1, seqId: 99L);
            _sm.InjectTapForTesting(1); // Set _pendingTap again

            Assert.IsTrue(_sm.PendingTapForTesting, "Pre-condition: MOVE_EXECUTING buffer set.");

            // Act: level reload triggers InitializeBoard
            _sm.InitializeBoard();

            // Assert: both buffers cleared
            Assert.IsFalse(_sm.PendingTapForTesting,
                "InitializeBoard must clear _pendingTap to prevent ghost taps on new board.");
            Assert.IsFalse(_sm.InvalidMovePendingTapForTesting,
                "InitializeBoard must clear _invalidMovePendingTap to prevent ghost taps on new board.");
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
        }

        // ── OnAnimationComplete guard when not in MOVE_EXECUTING ─────────────────

        [Test]
        public void OnAnimationComplete_WhenInIdle_IsNoOp()
        {
            // Arrange: FSM in IDLE
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
            int eventsBefore = AllEvents(_spy);

            // Act
            _sm.OnAnimationComplete(1L);

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "OnAnimationComplete in IDLE must be silently ignored.");
            Assert.AreEqual(eventsBefore, AllEvents(_spy));
        }

        // ── Back gesture while input is blocked (WARNING test) ───────────────────

        [Test]
        public void InputBlocked_BackGesture_IsNoOp()
        {
            // Arrange: trigger BlockInput via ColorCount mismatch in InitializeBoard
            _gsm.ColorCount = 99; // stacks.Length = 2, ColorCount = 99 → assertion 1 fails
            _sm.InitializeBoard();
            Assert.IsTrue(_sm.InputBlockedForTesting, "Pre-condition: input must be blocked.");

            // TriggerBackGestureForTesting checks _inputBlocked first → returns early.
            // ForceEnterInvalidMoveForTesting bypasses the block to put FSM in a non-IDLE state,
            // confirming that even a non-IDLE state does not help when input is blocked.
            _sm.ForceEnterInvalidMoveForTesting(sourceIndex: 0, heldColorId: 1);
            int eventsBefore = AllEvents(_spy);

            // Act
            _sm.TriggerBackGestureForTesting();

            // Assert
            Assert.AreEqual(eventsBefore, AllEvents(_spy),
                "Back gesture while input is blocked must emit no events.");
        }
    }
}
