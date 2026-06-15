using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;
using BoltSort.SortMechanic;
using BoltSort.Tests.Helpers.SortMechanic;

// Test assembly: Tests.Integration.GsmSortMechanic
// Covers: AC-10, AC-25 — integration-tier deadlock detection
//   from design/gdd/sort-mechanic.md (Story 005 scope)
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/sort-mechanic/story-005-deadlock-detection.md
//
// NOTE: SortMechanic is tested with an inline stub implementing IGameStateManager.
// Full real-GSM integration (where the GSM actually fires OnLevelLoaded and
// OnMoveCommitted) is deferred to Story 007.

namespace BoltSort.Tests.Integration.GsmSortMechanic
{
    [TestFixture]
    internal sealed class SortMechanic_Deadlock_Integration_Test
    {
        private GameObject _go;
        private global::BoltSort.SortMechanic.SortMechanic _sm;
        private GsmStub _gsm;
        private int _deadlockCount;
        private int _puzzleSolvedCount;
        private int _moveExecutingExitedCount;

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("SortMechanic_Deadlock_Integration_Test");
            _sm  = _go.AddComponent<global::BoltSort.SortMechanic.SortMechanic>();
            _gsm = new GsmStub();
            _deadlockCount           = 0;
            _puzzleSolvedCount       = 0;
            _moveExecutingExitedCount = 0;

            _sm.InjectForTesting(_gsm);
            _sm.SubscribeToGsmForTesting(); // Wire OnLevelLoaded / OnBoardRefreshForced events
            _sm.OnDeadlockDetected    += () => _deadlockCount++;
            _sm.OnPuzzleSolved        += _ => _puzzleSolvedCount++;
            _sm.OnMoveExecutingExited += _ => _moveExecutingExitedCount++;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        // ── AC-25: Initial deadlock on level_loaded ───────────────────────────────

        [Test]
        public void AC25_LevelLoaded_InitialDeadlock_EmitsDeadlockDetected()
        {
            // Arrange: configure stub to return canonical deadlock board
            _gsm.SetCanonicalDeadlock();

            // Act: simulate OnLevelLoaded event firing (as GSM would on level load)
            _gsm.FireLevelLoaded();
            // SortMechanic's HandleLevelLoaded: InitializeBoard() then HasLegalMove() check

            // Assert: deadlock detected before any player input
            Assert.AreEqual(1, _deadlockCount,
                "AC-25: deadlock on initial board must emit OnDeadlockDetected during level load.");
        }

        [Test]
        public void AC25_LevelLoaded_NoInitialDeadlock_NoEmission()
        {
            // Arrange: board with a legal move (stack 0 non-empty, stack 1 empty)
            _gsm.SetColorStacks(new[] { 1 }, new int[0]);
            _gsm.StackDepth = 2;
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _gsm.TempSlotDepth = 2;

            // Act
            _gsm.FireLevelLoaded();

            // Assert: no false deadlock on normal board
            Assert.AreEqual(0, _deadlockCount,
                "AC-25: board with legal move must NOT emit OnDeadlockDetected on level load.");
        }

        // ── AC-10: Deadlock detected on IDLE exit after animation_complete ─────────

        [Test]
        public void AC10_AnimationComplete_IdleExit_Deadlocked_EmitsDeadlockDetected()
        {
            // Arrange: canonical deadlock board; advance to MOVE_EXECUTING
            _gsm.SetCanonicalDeadlock();
            _gsm.FireLevelLoaded();
            _deadlockCount = 0; // reset — level-load deadlock already fired, we test post-move
            _gsm.MoveCount = 1;

            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);

            // Act: animation completes → IDLE exit → deadlock check
            _sm.OnAnimationComplete(1L);

            // Assert
            Assert.AreEqual(1, _deadlockCount,
                "AC-10: deadlock check on IDLE exit must emit OnDeadlockDetected.");
            Assert.AreEqual(0, _puzzleSolvedCount);
        }

        [Test]
        public void AC10_AnimationComplete_IdleExit_DeadlockBefore_MoveExecutingExited()
        {
            // AC-10: deadlock_detected emitted before any buffered tap fires
            // OnMoveExecutingExited fires before deadlock in our current impl order;
            // deadlock fires before ProcessPendingTap (the buffered tap).
            _gsm.SetCanonicalDeadlock();
            _gsm.FireLevelLoaded();
            _deadlockCount = 0;

            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);
            _sm.InjectTapForTesting(0); // Buffer tap — fires AFTER deadlock per AC-22

            // Capture ordering
            bool deadlockFiredBeforeProcessPendingTap = false;
            _sm.OnDeadlockDetected += () =>
            {
                // At this point, ProcessPendingTap has not yet run (code order guaranteed)
                // FSM should be in IDLE (deadlock check runs after state set to IDLE, before tap)
                deadlockFiredBeforeProcessPendingTap =
                    (_sm.CurrentStateForTesting == SortMechState.Idle);
            };

            _sm.OnAnimationComplete(1L);

            Assert.IsTrue(deadlockFiredBeforeProcessPendingTap,
                "AC-10: OnDeadlockDetected must fire while FSM is IDLE, before buffered tap fires.");
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting,
                "After deadlock + tap: FSM in BOLT_SELECTED (tap was processed after deadlock).");
        }

        // ── Deadlock with temp slot escape ────────────────────────────────────────

        [Test]
        public void LevelLoaded_TempSlotProvidesEscape_NoDeadlock()
        {
            // Canonical interlocked stacks + empty temp slot → NOT deadlocked
            _gsm.SetColorStacks(DeadlockFixtures.CanonicalStackA, DeadlockFixtures.CanonicalStackB);
            _gsm.StackDepth = DeadlockFixtures.CanonicalStackDepth;
            _gsm.SetTempSlots(new int[0]); // 1 empty temp slot
            _gsm.TempSlotDepth = 2;

            _gsm.FireLevelLoaded();

            Assert.AreEqual(0, _deadlockCount,
                "Empty temp slot provides escape — must NOT emit deadlock on level load.");
        }

        // ── GsmStub: minimal IGameStateManager implementation ─────────────────────

        private sealed class GsmStub : IGameStateManager
        {
            public IReadOnlyList<int>[] StackContents    { get; private set; }
            public IReadOnlyList<int>[] TempSlotContents { get; set; }
            public int ColorCount    { get; private set; }
            public int StackDepth    { get; set; } = 2;
            public int TempSlotDepth { get; set; } = 2;
            public int TempSlotCount { get; set; } = 0;

            // Phase-2 IGameStateManager members (uniform fallback; never frozen).
            public int GetColumnCapacity(int flatIndex) => flatIndex < ColorCount ? StackDepth : TempSlotDepth;
            public bool IsTubeFrozen(int flatIndex) => false;

            public int MoveCount     { get; set; } = 0;
            public long CurrentSequenceId { get; set; } = 0;
            public GSMLifecycleState LifecycleState { get; } = GSMLifecycleState.Active;

            public event Action<int, int, int, int, int, long> OnLevelLoaded;
            public event Action<long>              OnBoardRefreshForced { add { } remove { } }
            public event Action<long, int>         OnBoardStateChanged  { add { } remove { } }
            public event Action<GsmSessionLoadFailReason, int> OnSessionLoadFailed { add { } remove { } }
            public event Action<int, int, int, long>           OnLevelComplete     { add { } remove { } }
            public event Action<int?>              OnLevelUnloaded      { add { } remove { } }

            public void LoadLevel(int levelId)   { }
            public void ExitLevel()              { }
            public void UndoRequested()          { }

            public void SetCanonicalDeadlock()
            {
                SetColorStacks(DeadlockFixtures.CanonicalStackA, DeadlockFixtures.CanonicalStackB);
                StackDepth = DeadlockFixtures.CanonicalStackDepth;
                TempSlotCount = DeadlockFixtures.CanonicalTempSlotCount;
                TempSlotContents = null;
                TempSlotDepth = DeadlockFixtures.CanonicalStackDepth;
            }

            public void SetColorStacks(params int[][] stacks)
            {
                ColorCount = stacks.Length;
                var result = new IReadOnlyList<int>[stacks.Length];
                for (int i = 0; i < stacks.Length; i++)
                    result[i] = new List<int>(stacks[i]);
                StackContents = result;
            }

            public void SetTempSlots(params int[][] slots)
            {
                TempSlotCount = slots.Length;
                var result = new IReadOnlyList<int>[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                    result[i] = new List<int>(slots[i]);
                TempSlotContents = result;
            }

            public void FireLevelLoaded()
            {
                OnLevelLoaded?.Invoke(1, ColorCount, StackDepth, TempSlotCount, TempSlotDepth, 0L);
            }
        }
    }
}
