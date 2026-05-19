using NUnit.Framework;
using BoltSort.SortMechanic;
using BoltSort.Tests.Helpers.SortMechanic;

// Test assembly: Tests.Unit.SortMechanic
// Covers: AC-22 (BLOCKING unit test) + supporting deadlock detection cases
//   from design/gdd/sort-mechanic.md (Story 005 scope)
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/sort-mechanic/story-005-deadlock-detection.md

namespace BoltSort.Tests.Unit.SortMechanic
{
    [TestFixture]
    internal sealed class SortMechanic_Deadlock_Test
    {
        private global::BoltSort.SortMechanic.SortMechanic _sm;
        private MockGsm _gsm;
        private EventSpy _spy;

        [SetUp]
        public void SetUp()
        {
            _sm  = new global::BoltSort.SortMechanic.SortMechanic();
            _gsm = new MockGsm();
            _spy = new EventSpy();

            // Default: canonical deadlock board (used by most tests).
            ApplyCanonicalDeadlock(_gsm);
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy.Subscribe(_sm);
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        private static void ApplyCanonicalDeadlock(MockGsm gsm)
        {
            gsm.SetColorStacks(DeadlockFixtures.CanonicalStackA, DeadlockFixtures.CanonicalStackB);
            gsm.StackDepth     = DeadlockFixtures.CanonicalStackDepth;
            gsm.TempSlotCount  = DeadlockFixtures.CanonicalTempSlotCount;
            gsm.TempSlotContents = null;
            gsm.TempSlotDepth  = DeadlockFixtures.CanonicalStackDepth; // TempSlotDepth ≤ StackDepth
        }

        // ── Deadlock detection basic correctness ──────────────────────────────────

        [Test]
        public void AnimationComplete_Idle_DeadlockedBoard_EmitsDeadlockDetected()
        {
            // Arrange: canonical deadlock board, MOVE_EXECUTING
            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);
            // Stack A=[1,2] full, stack B=[2,1] full — no legal move

            // Act
            _sm.OnAnimationComplete(1L);

            // Assert
            Assert.AreEqual(1, _spy.DeadlockDetectedCount,
                "Deadlocked board on IDLE exit must emit OnDeadlockDetected.");
        }

        [Test]
        public void AnimationComplete_Idle_LegalMoveExists_NoDeadlockDetected()
        {
            // Arrange: board with one empty stack — legal move exists
            _gsm.SetColorStacks(new[] { 1 }, new int[0]); // stack 0=[1], stack 1=[]
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
            Assert.AreEqual(0, _spy.DeadlockDetectedCount,
                "Board with legal move must NOT emit OnDeadlockDetected.");
        }

        // ── AC-22 (BLOCKING): deadlock_detected emitted BEFORE buffered tap fires ──

        [Test]
        public void AC22_AnimationComplete_DeadlockThenBufferedTap_CorrectOrder()
        {
            // Arrange: canonical deadlock board + buffered tap
            // Stack A=[1,2] full, stack B=[2,1] full (deadlocked)
            // Buffer tap targeting stack A (non-empty) — will fire after deadlock signal
            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);
            _sm.InjectTapForTesting(0); // Buffer tap: stack 0 (non-empty, will enter BOLT_SELECTED)
            Assert.IsTrue(_sm.PendingTapForTesting, "Pre-condition: pending tap set.");

            // Act: animation complete → IDLE → deadlock check → buffered tap
            _sm.OnAnimationComplete(1L);

            // Assert (1): deadlock was emitted
            Assert.AreEqual(1, _spy.DeadlockDetectedCount,
                "AC-22: OnDeadlockDetected must be emitted.");

            // Assert (2): buffered tap was processed (FSM entered BOLT_SELECTED after deadlock)
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting,
                "AC-22: buffered tap must fire after deadlock signal; FSM in BOLT_SELECTED.");

            // Assert (3): buffered tap consumed (not still pending)
            Assert.IsFalse(_sm.PendingTapForTesting,
                "AC-22: pending tap must be cleared after processing.");
        }

        [Test]
        public void AC22_DeadlockBeforeBufferedTap_VerifiedByStateSequence()
        {
            // Verify ordering: if deadlock had fired AFTER buffered tap,
            // the state sequence would be inverted. Since tap enters BOLT_SELECTED
            // and deadlock fires in IDLE, the only valid sequence is deadlock-then-tap.
            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);
            _sm.InjectTapForTesting(0); // Buffer tap on stack 0 (will select held bolt from A)

            string firstEvent = null;
            _sm.OnDeadlockDetected += () =>
            {
                // At this point, tap has NOT yet fired (ProcessPendingTap comes after)
                // Verify FSM is in IDLE (deadlock fires while in IDLE, before tap re-enters)
                if (firstEvent == null)
                    firstEvent = _sm.CurrentStateForTesting == SortMechState.Idle
                        ? "deadlock-in-idle"
                        : "deadlock-not-in-idle";
            };

            _sm.OnAnimationComplete(1L);

            Assert.AreEqual("deadlock-in-idle", firstEvent,
                "AC-22: deadlock must fire while FSM is in IDLE, before buffered tap transitions to BOLT_SELECTED.");
        }

        // ── WIN path: deadlock NOT checked ────────────────────────────────────────

        [Test]
        public void AnimationComplete_WinPath_NoDeadlockDetected()
        {
            // Won board: all stacks full and monochromatic — no deadlock check on WIN path
            _gsm.SetColorStacks(new[] { 1, 1 }, new[] { 2, 2 });
            _gsm.StackDepth = 2;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(0, 1, 1L);

            _sm.OnAnimationComplete(1L);

            Assert.AreEqual(0, _spy.DeadlockDetectedCount,
                "WIN path must NOT emit OnDeadlockDetected.");
            Assert.AreEqual(1, _spy.PuzzleSolvedCount);
        }

        // ── Watchdog path: no deadlock emitted (watchdog is non-canonical exit) ───

        [Test]
        public void Watchdog_DeadlockedBoard_NoDeadlockEmitted()
        {
            // Watchdog path does NOT run deadlock check — it's an error recovery path
            // (full board refresh, not normal IDLE exit)
            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);

            _sm.TriggerBoardRefreshForcedForTesting(1L);

            Assert.AreEqual(0, _spy.DeadlockDetectedCount,
                "Watchdog path must NOT emit OnDeadlockDetected.");
        }

        // ── Full temp slot: no escape, board is deadlocked ───────────────────────

        [Test]
        public void AnimationComplete_Idle_AllTempSlotsFull_InterlockStacks_EmitsDeadlockDetected()
        {
            // Arrange: canonical interlocked stacks + 1 temp slot at capacity (TempSlotDepth=1, slot=[1])
            // Top of A=2, dest=B full → DestinationFull; dest=tempSlot=[1] full → DestinationFull
            // Top of B=1, dest=A full → DestinationFull; dest=tempSlot=[1] full → DestinationFull
            // → HasLegalMove() returns false → deadlock
            _gsm.SetColorStacks(DeadlockFixtures.CanonicalStackA, DeadlockFixtures.CanonicalStackB);
            _gsm.StackDepth = DeadlockFixtures.CanonicalStackDepth;
            _gsm.SetTempSlots(new[] { 1 }); // 1 temp slot, depth=1, full with color 1
            _gsm.TempSlotDepth = 1;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);

            // Act
            _sm.OnAnimationComplete(1L);

            // Assert
            Assert.AreEqual(1, _spy.DeadlockDetectedCount,
                "All temp slots full + interlocked stacks: no escape → must emit OnDeadlockDetected.");
        }

        // ── Near-deadlock: temp slot provides escape ──────────────────────────────

        [Test]
        public void AnimationComplete_NearDeadlock_TempSlotProvidesEscape_NoDeadlock()
        {
            // Board: 2 full interlocked stacks + 1 empty temp slot
            // Stack A=[1,2] full, stack B=[2,1] full, temp slot 0=[] empty
            // IsLegalMove(2, [], tempSlotDepth=2): empty → Legal → HasLegalMove=true
            _gsm.SetColorStacks(DeadlockFixtures.CanonicalStackA, DeadlockFixtures.CanonicalStackB);
            _gsm.StackDepth = 2;
            _gsm.SetTempSlots(new int[0]); // 1 empty temp slot
            _gsm.TempSlotDepth = 2;
            _sm.InjectForTesting(_gsm);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);
            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);

            _sm.OnAnimationComplete(1L);

            Assert.AreEqual(0, _spy.DeadlockDetectedCount,
                "Empty temp slot provides escape — board is NOT deadlocked.");
        }
    }
}
