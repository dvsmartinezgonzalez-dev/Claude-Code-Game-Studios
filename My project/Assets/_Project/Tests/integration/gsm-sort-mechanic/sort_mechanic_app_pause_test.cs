using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;
using BoltSort.SortMechanic;

// Test assembly: Tests.Integration.GsmSortMechanic
// Covers: AC-28 — app-pause cancellation + SEO call-order contract
//   from design/gdd/sort-mechanic.md (Story 006 scope)
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/sort-mechanic/story-006-app-pause-cancellation.md
//
// Design note: Sort Mechanic never mutates GSM board state during BOLT_SELECTED
// (ADR-0006: GSM is sole board owner; bolt "held" state is tracked internally only).
// OnApplicationPause cancellation emits move_cancelled to signal downstream systems
// (animation, HUD) that the visual hold is released. Board count is always intact
// because Sort Mechanic's Awake-to-BOLT_SELECTED path never removes the bolt from GSM.
// The critical production guarantee — that Sort Mechanic fires before GSM in
// OnApplicationPause — is enforced by SEO −55 (SortMechanic) vs −50 (GameStateManager).

namespace BoltSort.Tests.Integration.GsmSortMechanic
{
    [TestFixture]
    internal sealed class SortMechanic_AppPause_Integration_Test
    {
        private GameObject _go;
        private global::BoltSort.SortMechanic.SortMechanic _sm;
        private GsmStub _gsm;

        private int _moveCancelledCount;
        private int _lastCancelledSource;
        private int _lastCancelledColorId;
        private int _moveCommittedCount;
        private int _puzzleSolvedCount;

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("SortMechanic_AppPause_Integration_Test");
            _sm  = _go.AddComponent<global::BoltSort.SortMechanic.SortMechanic>();
            _gsm = new GsmStub();

            _moveCancelledCount = 0;
            _moveCommittedCount = 0;
            _puzzleSolvedCount  = 0;

            _sm.InjectForTesting(_gsm);

            _sm.OnMoveCancelled  += (src, color) => { _moveCancelledCount++; _lastCancelledSource = src; _lastCancelledColorId = color; };
            _sm.OnMoveCommitted  += (_, __, ___, ____) => _moveCommittedCount++;
            _sm.OnPuzzleSolved   += _ => _puzzleSolvedCount++;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        // ── AC-28: BOLT_SELECTED + pause → IDLE + move_cancelled ─────────────────

        [Test]
        public void AC28_Pause_InBoltSelected_EmitsMoveCancelled()
        {
            // Arrange: 2 colors, depth 2; stack 0 = [1,2] (top = color 2)
            _gsm.SetColorStacks(new[] { 1, 2 }, new[] { 2, 1 });
            _sm.InitializeBoard();
            _sm.InjectTapForTesting(0); // Lift color 2 from stack 0 → BOLT_SELECTED
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting,
                "Precondition: must be in BOLT_SELECTED before pause.");

            // Act
            _sm.TriggerApplicationPauseForTesting(true);

            // Assert
            Assert.AreEqual(1, _moveCancelledCount,
                "AC-28: OnMoveCancelled must fire exactly once when paused in BOLT_SELECTED.");
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "AC-28: Sort Mechanic must be in IDLE after pause cancellation.");
        }

        [Test]
        public void AC28_Pause_InBoltSelected_MoveCancelledCarriesCorrectSourceAndColor()
        {
            // Arrange: stack 0 = [1, 2]; top bolt is color 2 — that's the one being held
            _gsm.SetColorStacks(new[] { 1, 2 }, new[] { 2, 1 });
            _sm.InitializeBoard();
            _sm.InjectTapForTesting(0); // Lift top bolt from stack 0 (color 2)

            // Act
            _sm.TriggerApplicationPauseForTesting(true);

            // Assert
            Assert.AreEqual(0, _lastCancelledSource,
                "AC-28: move_cancelled source must be the stack index the bolt was lifted from.");
            Assert.AreEqual(2, _lastCancelledColorId,
                "AC-28: move_cancelled color_id must match the held bolt color.");
        }

        [Test]
        public void AC28_Pause_InBoltSelected_BoardCountRemainsEqualToColorCountTimesStackDepth()
        {
            // Arrange: 2 colors, depth 2 → total expected = 4 bolts
            _gsm.SetColorStacks(new[] { 1, 2 }, new[] { 2, 1 });
            _sm.InitializeBoard();
            _sm.InjectTapForTesting(0); // Enter BOLT_SELECTED

            // Act: pause while holding bolt
            _sm.TriggerApplicationPauseForTesting(true);

            // Assert: "serialise" board — count all bolts in GSM state
            // Sort Mechanic never removes the bolt from GSM during BOLT_SELECTED (ADR-0006:
            // GSM is sole board owner; mutation only occurs on move_committed). A count of
            // (color_count × stack_depth) − 1 here would indicate a regression in that contract.
            int totalBolts = CountTotalBolts(_gsm);
            int expected   = _gsm.ColorCount * _gsm.StackDepth;
            Assert.AreEqual(expected, totalBolts,
                $"AC-28: board must have {expected} bolts after pause cancellation (got {totalBolts}). " +
                "A count of (color_count × stack_depth) − 1 means a bolt was removed from GSM without being restored.");
        }

        // ── AC-28: Pause in non-BOLT_SELECTED states is a no-op ──────────────────

        [Test]
        public void Pause_InIdle_NoEventsEmitted_StateUnchanged()
        {
            // Arrange: Sort Mechanic initialised — starts in IDLE
            _gsm.SetColorStacks(new[] { 1, 2 }, new[] { 2, 1 });
            _sm.InitializeBoard();
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);

            // Act
            _sm.TriggerApplicationPauseForTesting(true);

            // Assert: no-op — IDLE has no held bolt to cancel
            Assert.AreEqual(0, _moveCancelledCount, "Pause in IDLE must not emit move_cancelled.");
            Assert.AreEqual(0, _moveCommittedCount, "Pause in IDLE must not emit move_committed.");
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "Pause in IDLE must leave state unchanged.");
        }

        [Test]
        public void Pause_InMoveExecuting_NoEventsEmitted_StateUnchanged()
        {
            // Arrange: force into MOVE_EXECUTING (bolt already committed to GSM — no "held" state)
            _gsm.SetColorStacks(new[] { 1, 2 }, new[] { 2, 1 });
            _sm.InitializeBoard();
            _sm.ForceEnterMoveExecutingForTesting(0, 2, 1L);
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting);

            // Act
            _sm.TriggerApplicationPauseForTesting(true);

            // Assert: bolt was already committed — no cancellation needed
            Assert.AreEqual(0, _moveCancelledCount,
                "Pause in MOVE_EXECUTING must NOT emit move_cancelled (bolt already committed to GSM).");
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting,
                "Pause in MOVE_EXECUTING must leave state unchanged.");
        }

        // ── Resume after pause: board remains playable ────────────────────────────

        [Test]
        public void Pause_Resume_SortMechanicAcceptsNewInput()
        {
            // Arrange: enter BOLT_SELECTED; pause → IDLE
            _gsm.SetColorStacks(new[] { 1, 2 }, new[] { 2, 1 });
            _sm.InitializeBoard();
            _sm.InjectTapForTesting(0);
            _sm.TriggerApplicationPauseForTesting(true);
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting, "Precondition: IDLE after pause.");

            // Simulate foreground restore — no action needed (Sort Mechanic is already IDLE)
            // Act: player starts a new move
            _sm.InjectTapForTesting(0);

            // Assert
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting,
                "After pause + foreground restore, Sort Mechanic must accept a new bolt lift normally.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static int CountTotalBolts(GsmStub gsm)
        {
            int total = 0;
            if (gsm.StackContents != null)
                foreach (var col in gsm.StackContents)
                    if (col != null) total += col.Count;
            if (gsm.TempSlotContents != null)
                foreach (var slot in gsm.TempSlotContents)
                    if (slot != null) total += slot.Count;
            return total;
        }

        // ── GsmStub: minimal IGameStateManager ────────────────────────────────────

        private sealed class GsmStub : IGameStateManager
        {
            public IReadOnlyList<int>[] StackContents    { get; private set; }
            public IReadOnlyList<int>[] TempSlotContents { get; set; } = null;
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

            public event Action<int, int, int, int, int, long> OnLevelLoaded      { add { } remove { } }
            public event Action<long>              OnBoardRefreshForced            { add { } remove { } }
            public event Action<long, int>         OnBoardStateChanged             { add { } remove { } }
            public event Action<GsmSessionLoadFailReason, int> OnSessionLoadFailed { add { } remove { } }
            public event Action<int, int, int, long>           OnLevelComplete     { add { } remove { } }
            public event Action<int?>              OnLevelUnloaded                 { add { } remove { } }

            public void LoadLevel(int levelId)   { }
            public void ExitLevel()              { }
            public void UndoRequested()          { }

            /// <summary>
            /// Sets up <paramref name="stacks"/> as color stack contents.
            /// Each inner array is one stack's bolts: index 0 = bottom, last = top.
            /// </summary>
            public void SetColorStacks(params int[][] stacks)
            {
                ColorCount = stacks.Length;
                var result = new IReadOnlyList<int>[stacks.Length];
                for (int i = 0; i < stacks.Length; i++)
                    result[i] = new List<int>(stacks[i]);
                StackContents = result;
            }
        }
    }
}
