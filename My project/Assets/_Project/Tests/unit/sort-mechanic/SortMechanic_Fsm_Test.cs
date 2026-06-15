using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;
using BoltSort.SortMechanic;

// Test assembly: Tests.Unit.SortMechanic
// Covers: AC-07, AC-09, AC-15a, AC-17, AC-18b, AC-18c, AC-21, AC-27
//   from design/gdd/sort-mechanic.md (Story 001 scope)
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/sort-mechanic/story-001-fsm-core-initialization.md

namespace BoltSort.Tests.Unit.SortMechanic
{
    // ── Shared fixtures ───────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal mock GSM. All properties return configurable values.
    /// Board state is static — no mutation tracking needed at Story 001 tier.
    /// </summary>
    internal sealed class MockGsm : IGameStateManager
    {
        // Board dimensions
        public int ColorCount    { get; set; } = 2;
        public int StackDepth    { get; set; } = 3;
        public int TempSlotCount { get; set; } = 1;
        public int TempSlotDepth { get; set; } = 2;

        // Phase-2 IGameStateManager members (uniform fallback; never frozen).
        public int GetColumnCapacity(int flatIndex) => flatIndex < ColorCount ? StackDepth : TempSlotDepth;
        public bool IsTubeFrozen(int flatIndex) => false;

        public int MoveCount     { get; set; } = 0;
        public long CurrentSequenceId { get; set; } = 0;
        public GSMLifecycleState LifecycleState { get; set; } = GSMLifecycleState.Active;

        /// <summary>
        /// StackContents array. Indices 0..(ColorCount-1) are color stacks.
        /// Set via <see cref="SetColorStacks"/>.
        /// </summary>
        public IReadOnlyList<int>[] StackContents  { get; set; }
        public IReadOnlyList<int>[] TempSlotContents { get; set; }

        // Spy: tracks whether RecordUndo was called (AC-07).
        public int RecordUndoCallCount { get; private set; }

        // Events (unused in Story 001 unit tests — wired to suppress compiler warnings).
        public event Action<long, int>         OnBoardStateChanged  { add { } remove { } }
        public event Action<GsmSessionLoadFailReason, int> OnSessionLoadFailed { add { } remove { } }
        public event Action<int, int, int, long>           OnLevelComplete     { add { } remove { } }
        public event Action<int, int, int, int, int, long> OnLevelLoaded       { add { } remove { } }
        public event Action<long>              OnBoardRefreshForced { add { } remove { } }
        public event Action<int?>              OnLevelUnloaded      { add { } remove { } }

        public void LoadLevel(int levelId)   { }
        public void ExitLevel()              { }

        public void UndoRequested()
        {
            RecordUndoCallCount++;
        }

        // ── Convenience builders ──────────────────────────────────────────────────

        /// <summary>
        /// Configures a valid two-stack board with the given bolt contents.
        /// Call before <see cref="BoltSort.SortMechanic.SortMechanic.InitializeBoard"/>.
        /// </summary>
        public void SetColorStacks(params int[][] stacks)
        {
            ColorCount = stacks.Length;
            var result = new IReadOnlyList<int>[stacks.Length];
            for (int i = 0; i < stacks.Length; i++)
                result[i] = new List<int>(stacks[i]);
            StackContents = result;
        }

        /// <summary>Sets TempSlotContents to a single slot with the given bolt IDs.</summary>
        public void SetTempSlots(params int[][] slots)
        {
            TempSlotCount = slots.Length;
            var result = new IReadOnlyList<int>[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                result[i] = new List<int>(slots[i]);
            TempSlotContents = result;
        }
    }

    /// <summary>
    /// Mock IDiagnosticLogger — captures Error and Warning calls for assertion.
    /// </summary>
    internal sealed class MockLogger : IDiagnosticLogger
    {
        public int ErrorCallCount   { get; private set; }
        public int WarningCallCount { get; private set; }
        public string LastErrorCategory { get; private set; }
        public object LastErrorPayload  { get; private set; }

        public void Error(string category, string message, object payload = null)
        {
            ErrorCallCount++;
            LastErrorCategory = category;
            LastErrorPayload  = payload;
        }

        public void Warning(string category, string message, object payload = null)
        {
            WarningCallCount++;
        }
    }

    /// <summary>
    /// Captures all events emitted by SortMechanic for assertion.
    /// </summary>
    internal sealed class EventSpy
    {
        public int MoveCommittedCount       { get; private set; }
        public int MoveCancelledCount       { get; private set; }
        public int MoveRejectedCount        { get; private set; }
        public int PuzzleSolvedCount        { get; private set; }
        public int LevelLoadFailedCount     { get; private set; }
        public int DeadlockDetectedCount    { get; private set; }
        public int MoveExecutingExitedCount { get; private set; }
        public MoveRejectedReason LastRejectedReason      { get; private set; }
        public int LastPuzzleSolvedMoveCount               { get; private set; }

        // Last-emitted values
        public (int src, int dst, int color, long seq) LastCommit;
        public (int src, int color)                    LastCancel;
        public SortMechLoadFailReason                  LastFailReason;

        public void Subscribe(global::BoltSort.SortMechanic.SortMechanic sm)
        {
            sm.OnMoveCommitted    += (src, dst, color, seq) =>
            {
                MoveCommittedCount++;
                LastCommit = (src, dst, color, seq);
            };
            sm.OnMoveCancelled    += (src, color) =>
            {
                MoveCancelledCount++;
                LastCancel = (src, color);
            };
            sm.OnMoveRejected     += (src, dst, color, reason) =>
            {
                MoveRejectedCount++;
                LastRejectedReason = reason;
            };
            sm.OnPuzzleSolved     += moveCount =>
            {
                PuzzleSolvedCount++;
                LastPuzzleSolvedMoveCount = moveCount;
            };
            sm.OnLevelLoadFailed  += reason =>
            {
                LevelLoadFailedCount++;
                LastFailReason = reason;
            };
            sm.OnDeadlockDetected    += () => DeadlockDetectedCount++;
            sm.OnMoveExecutingExited += _ => MoveExecutingExitedCount++;
        }

        public int TotalEvents =>
            MoveCommittedCount + MoveCancelledCount + MoveRejectedCount +
            PuzzleSolvedCount  + LevelLoadFailedCount + DeadlockDetectedCount +
            MoveExecutingExitedCount;
    }

    // ── Test Fixture ──────────────────────────────────────────────────────────────

    [TestFixture]
    public class SortMechanic_Fsm_Test
    {
        private global::BoltSort.SortMechanic.SortMechanic _sm;
        private MockGsm    _gsm;
        private MockLogger _logger;
        private EventSpy   _spy;

        // Tracks all GameObjects created during a test for teardown cleanup.
        private readonly List<GameObject> _toDestroy = new List<GameObject>();

        /// <summary>
        /// Creates a fresh <see cref="SortMechanic"/> via AddComponent (correct Unity pattern).
        /// The backing GameObject is tracked and destroyed in <see cref="TearDown"/>.
        /// Awake fires but is benign: _gsmComponent is null, so SubscribeToGsm() is skipped;
        /// EnhancedTouchSupport.Enable() is safe in EditMode; Camera.main is null but never
        /// called in test paths (tests use InjectTapForTesting, not Physics2D.OverlapPoint).
        /// </summary>
        private global::BoltSort.SortMechanic.SortMechanic CreateFresh()
        {
            var go = new GameObject("SM_Test");
            _toDestroy.Add(go);
            return go.AddComponent<global::BoltSort.SortMechanic.SortMechanic>();
        }

        /// <summary>
        /// Builds a SortMechanic with a two-stack board (colorCount=2, stackDepth=3)
        /// and one temp slot (tempSlotDepth=2). Assertions pass; FSM starts in Idle.
        /// Stack 0 = [1, 1] (two color-1 bolts), Stack 1 = [2, 2] (two color-2 bolts).
        /// Temp slot 0 = [] (empty).
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _gsm    = new MockGsm();
            _logger = new MockLogger();
            _spy    = new EventSpy();

            _gsm.ColorCount    = 2;
            _gsm.StackDepth    = 3;
            _gsm.TempSlotCount = 1;
            _gsm.TempSlotDepth = 2;
            _gsm.SetColorStacks(new[] { 1, 1 }, new[] { 2, 2 });
            _gsm.SetTempSlots(Array.Empty<int>());

            _sm = CreateFresh();
            _sm.InjectForTesting(_gsm, _logger);
            _spy.Subscribe(_sm);
            _sm.InitializeBoard();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _toDestroy)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _toDestroy.Clear();
        }

        // ── Initialization assertion tests ────────────────────────────────────────

        /// <summary>
        /// AC-18b: StackContents.Length ≠ ColorCount → level_load_failed + IDiagnosticLogger.Error.
        /// Option B for AC-15a: assertion path verified by structured logger call, not message text.
        /// </summary>
        [Test]
        public void Test_Init_StackLengthMismatch_EmitsLevelLoadFailed()
        {
            // Arrange: 2 stacks but ColorCount overridden to 3 → length mismatch
            _gsm.SetColorStacks(new[] { 1, 1 }, new[] { 2 }); // sets ColorCount=2, stacks.Length=2
            _gsm.ColorCount = 3; // override: ColorCount=3 but stacks.Length=2 → assertion fails
            var sm = CreateFresh();
            sm.InjectForTesting(_gsm, _logger);
            var spy = new EventSpy();
            spy.Subscribe(sm);

            // Act
            sm.InitializeBoard();

            // Assert
            Assert.AreEqual(1, spy.LevelLoadFailedCount, "level_load_failed must be emitted once");
            Assert.AreEqual(SortMechLoadFailReason.CorruptedBoardState, spy.LastFailReason);
            Assert.AreEqual(1, _logger.ErrorCallCount, "IDiagnosticLogger.Error must be called once");
            Assert.AreEqual("SortMechanic", _logger.LastErrorCategory);
        }

        /// <summary>
        /// AC-18b (logger payload): Error payload must carry the reason field.
        /// Asserts on the call structure — not on the text of the message.
        /// </summary>
        [Test]
        public void Test_Init_StackLengthMismatch_LoggerPayloadCarriesReason()
        {
            // Arrange: 2 stacks but ColorCount overridden to 3 → mismatch → logger payload
            _gsm.SetColorStacks(new[] { 1 }, new[] { 2 }); // sets ColorCount=2
            _gsm.ColorCount = 3;                             // override: ColorCount=3, stacks.Length=2
            var sm = CreateFresh();
            sm.InjectForTesting(_gsm, _logger);
            sm.InitializeBoard();

            // Assert — payload must be non-null and expose a reason field.
            Assert.IsNotNull(_logger.LastErrorPayload, "Logger payload must not be null");
            // Payload is an anonymous type; verify via ToString or reflection that it contains
            // "CorruptedBoardState". This is the minimum verifiable contract without
            // coupling the test to the anonymous-type structure.
            string payloadStr = _logger.LastErrorPayload.ToString();
            Assert.IsTrue(payloadStr.Contains("CorruptedBoardState"),
                "Payload must reference CorruptedBoardState reason");
        }

        /// <summary>
        /// AC-18c: After level_load_failed, all tap events produce zero emissions.
        /// </summary>
        [Test]
        public void Test_Init_StackLengthMismatch_BlocksAllInput()
        {
            // Arrange — mismatched board: 2 stacks but ColorCount=3
            _gsm.SetColorStacks(new[] { 1 }, new[] { 2 }); // sets ColorCount=2
            _gsm.ColorCount = 3;                             // override: mismatch
            var sm = CreateFresh();
            sm.InjectForTesting(_gsm, _logger);
            var spy = new EventSpy();
            spy.Subscribe(sm);
            sm.InitializeBoard();
            int emissionsAfterInit = spy.TotalEvents;

            // Act — inject taps; expect zero additional emissions
            sm.InjectTapForTesting(0);
            sm.InjectTapForTesting(1);
            sm.InjectTapForTesting(-1);

            // Assert
            Assert.IsTrue(sm.InputBlockedForTesting, "_inputBlocked must be true");
            Assert.AreEqual(emissionsAfterInit, spy.TotalEvents, "Zero events after init failure");
            Assert.AreEqual(0, _gsm.RecordUndoCallCount, "Zero GSM mutations");
        }

        /// <summary>
        /// AC-27: temp_slot_depth > stack_depth → level_load_failed + logger call.
        /// </summary>
        [Test]
        public void Test_Init_TempSlotDepthExceedsStackDepth_EmitsLevelLoadFailed()
        {
            // Arrange
            _gsm.StackDepth    = 3;
            _gsm.TempSlotDepth = 5; // violation: 5 > 3
            var sm = CreateFresh();
            sm.InjectForTesting(_gsm, _logger);
            var spy = new EventSpy();
            spy.Subscribe(sm);

            // Act
            sm.InitializeBoard();

            // Assert
            Assert.AreEqual(1, spy.LevelLoadFailedCount);
            Assert.AreEqual(SortMechLoadFailReason.CorruptedBoardState, spy.LastFailReason);
            Assert.AreEqual(1, _logger.ErrorCallCount);
            Assert.AreEqual("SortMechanic", _logger.LastErrorCategory);
        }

        /// <summary>
        /// AC-27 + AC-18c: Input blocked after temp_slot_depth assertion failure.
        /// </summary>
        [Test]
        public void Test_Init_TempSlotDepthExceedsStackDepth_BlocksAllInput()
        {
            // Arrange
            _gsm.StackDepth    = 3;
            _gsm.TempSlotDepth = 5;
            var sm = CreateFresh();
            sm.InjectForTesting(_gsm, _logger);
            var spy = new EventSpy();
            spy.Subscribe(sm);
            sm.InitializeBoard();
            int emissionsAfterInit = spy.TotalEvents;

            // Act
            sm.InjectTapForTesting(0);
            sm.InjectTapForTesting(1);

            // Assert
            Assert.IsTrue(sm.InputBlockedForTesting);
            Assert.AreEqual(emissionsAfterInit, spy.TotalEvents);
        }

        /// <summary>
        /// Assertion 3: color_id outside {1..colorCount} → level_load_failed.
        /// </summary>
        [Test]
        public void Test_Init_PhantomColorId_EmitsLevelLoadFailed()
        {
            // Arrange: colorCount=2 but stack contains color_id=3 (out of domain)
            _gsm.ColorCount = 2;
            _gsm.SetColorStacks(new[] { 1, 3 }, new[] { 2 }); // 3 is a phantom color
            var sm = CreateFresh();
            sm.InjectForTesting(_gsm, _logger);
            var spy = new EventSpy();
            spy.Subscribe(sm);

            // Act
            sm.InitializeBoard();

            // Assert
            Assert.AreEqual(1, spy.LevelLoadFailedCount);
            Assert.AreEqual(SortMechLoadFailReason.CorruptedBoardState, spy.LastFailReason);
            Assert.AreEqual(1, _logger.ErrorCallCount);
        }

        /// <summary>
        /// Assertion 3 (temp-slot branch): color_id outside {1..colorCount} in a temp slot
        /// → level_load_failed. Covers the second branch of AssertNoPhantomColorIds().
        /// </summary>
        [Test]
        public void Test_Init_PhantomColorId_InTempSlot_EmitsLevelLoadFailed()
        {
            // Arrange: colorCount=2; color stacks are valid; temp slot contains color_id=5
            _gsm.ColorCount = 2;
            _gsm.StackDepth    = 3;
            _gsm.TempSlotDepth = 2;
            _gsm.SetColorStacks(new[] { 1, 1 }, new[] { 2, 2 });
            _gsm.SetTempSlots(new[] { 5 }); // 5 is a phantom color outside {1..2}
            var sm = CreateFresh();
            sm.InjectForTesting(_gsm, _logger);
            var spy = new EventSpy();
            spy.Subscribe(sm);

            // Act
            sm.InitializeBoard();

            // Assert
            Assert.AreEqual(1, spy.LevelLoadFailedCount,
                "level_load_failed must fire for phantom color in temp slot");
            Assert.AreEqual(SortMechLoadFailReason.CorruptedBoardState, spy.LastFailReason);
            Assert.AreEqual(1, _logger.ErrorCallCount,
                "IDiagnosticLogger.Error must be called with category SortMechanic");
            Assert.IsTrue(sm.InputBlockedForTesting,
                "Input must be blocked after temp-slot phantom color failure");
        }

        /// <summary>
        /// Valid board must pass all assertions and leave FSM in Idle.
        /// </summary>
        [Test]
        public void Test_Init_ValidBoard_FsmInIdleNoEvents()
        {
            // Already initialized in SetUp with a valid board.
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
            Assert.IsFalse(_sm.InputBlockedForTesting);
            Assert.AreEqual(0, _spy.LevelLoadFailedCount);
            Assert.AreEqual(0, _logger.ErrorCallCount);
        }

        // ── S-02: Empty source tap ────────────────────────────────────────────────

        /// <summary>
        /// AC-09: Tapping an empty stack while Idle → no event, no state change.
        /// </summary>
        [Test]
        public void Test_Idle_TapEmptyStack_NoEventNoStateChange()
        {
            // Arrange: add an empty third stack (color 1 stacks full, stack 2 = empty)
            _gsm.SetColorStacks(new[] { 1, 1 }, Array.Empty<int>());
            _sm.InjectForTesting(_gsm, _logger);
            _sm.InitializeBoard();
            int eventsBefore = _spy.TotalEvents;

            // Act: tap stack index 1 (empty)
            _sm.InjectTapForTesting(1);

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
            Assert.AreEqual(eventsBefore, _spy.TotalEvents, "Zero events on empty-stack tap");
        }

        // ── S-01: Successful bolt lift ────────────────────────────────────────────

        /// <summary>
        /// S-01: Tapping a non-empty stack → BOLT_SELECTED with correct held bolt data.
        /// </summary>
        [Test]
        public void Test_Idle_TapNonEmptyStack_EntersBoltSelected()
        {
            // Act: tap stack 0 (top bolt = color 1)
            _sm.InjectTapForTesting(0);

            // Assert
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting);
            Assert.AreEqual(1, _sm.HeldColorIdForTesting,     "Held color must be 1 (top of stack 0)");
            Assert.AreEqual(0, _sm.HeldSourceIndexForTesting, "Source index must be 0");
        }

        // ── S-03: Source-tap cancellation (AC-07) ─────────────────────────────────

        /// <summary>
        /// AC-07: Tapping the source stack while in BOLT_SELECTED → move_cancelled,
        /// FSM in Idle, no RecordUndo call.
        /// </summary>
        [Test]
        public void Test_BoltSelected_TapSource_CancelsAndEmitsMoveCancelled()
        {
            // Arrange: lift bolt from stack 0
            _sm.InjectTapForTesting(0);
            Assert.AreEqual(SortMechState.BoltSelected, _sm.CurrentStateForTesting);

            // Act: tap source again (S-03)
            _sm.InjectTapForTesting(0);

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting, "Must be Idle after cancellation");
            Assert.AreEqual(1, _spy.MoveCancelledCount, "move_cancelled must be emitted once");
            Assert.AreEqual(0, _spy.MoveCommittedCount, "move_committed must NOT be emitted");
            Assert.AreEqual(0, _gsm.RecordUndoCallCount, "RecordUndo must not be called");
        }

        /// <summary>
        /// AC-07: move_cancelled carries correct source index and color_id.
        /// </summary>
        [Test]
        public void Test_BoltSelected_TapSource_MoveCancelledParametersCorrect()
        {
            // Arrange
            _sm.InjectTapForTesting(0); // lift color 1 from stack 0

            // Act
            _sm.InjectTapForTesting(0);

            // Assert
            Assert.AreEqual(0, _spy.LastCancel.src,   "Cancelled source index must be 0");
            Assert.AreEqual(1, _spy.LastCancel.color, "Cancelled color must be 1");
        }

        // ── S-05: Empty-space cancellation (AC-17) ────────────────────────────────

        /// <summary>
        /// AC-17: Injecting flatStackIndex = -1 while in BOLT_SELECTED → move_cancelled,
        /// FSM in Idle, board reverts.
        /// </summary>
        [Test]
        public void Test_BoltSelected_TapEmptySpace_CancelsAndEmitsMoveCancelled()
        {
            // Arrange: lift bolt from stack 0
            _sm.InjectTapForTesting(0);

            // Act: tap empty board space (index = -1)
            _sm.InjectTapForTesting(-1);

            // Assert
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);
            Assert.AreEqual(1, _spy.MoveCancelledCount);
            Assert.AreEqual(0, _spy.MoveCommittedCount);
        }

        // ── AC-21: Synchronous CANCELLATION → IDLE ────────────────────────────────

        /// <summary>
        /// AC-21: After cancellation trigger is injected, FSM is already in Idle
        /// on the immediately following assertion — no yield, no deferred invoke.
        /// </summary>
        [Test]
        public void Test_Cancellation_ExitsToIdle_Synchronously()
        {
            // Arrange
            _sm.InjectTapForTesting(0); // enter BoltSelected

            // Act: source tap — cancellation
            _sm.InjectTapForTesting(0);

            // Assert — synchronous check, no awaiting required
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting,
                "FSM must be in Idle synchronously after move_cancelled emission");
        }

        // ── AC-15a: move_committed vs move_cancelled split ────────────────────────

        /// <summary>
        /// AC-15a (Option B): Legal move emits move_committed; cancellation does NOT.
        /// Verifies the positive committed path (legal destination tap).
        /// </summary>
        [Test]
        public void Test_BoltSelected_LegalDestination_EmitsMoveCommitted()
        {
            // Arrange: board with empty stack 1 so any color is a legal destination
            _gsm.SetColorStacks(new[] { 1, 1 }, Array.Empty<int>());
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm, _logger);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            _sm.InjectTapForTesting(0); // lift color 1 from stack 0
            _sm.InjectTapForTesting(1); // stack 1 is empty → legal → MoveCommitted

            // Assert
            Assert.AreEqual(1, _spy.MoveCommittedCount, "move_committed must be emitted");
            Assert.AreEqual(0, _spy.MoveCancelledCount, "move_cancelled must NOT be emitted");
            Assert.AreEqual(0, _spy.LastCommit.src, "Source must be stack 0");
            Assert.AreEqual(1, _spy.LastCommit.dst, "Destination must be stack 1");
            Assert.AreEqual(1, _spy.LastCommit.color, "Color must be 1");
        }

        /// <summary>
        /// AC-15a: Cancellation emits move_cancelled and does NOT emit move_committed.
        /// </summary>
        [Test]
        public void Test_BoltSelected_Cancellation_EmitsMoveCancelledNotMoveCommitted()
        {
            // Arrange: lift bolt from stack 0
            _sm.InjectTapForTesting(0);

            // Act: empty-space cancel
            _sm.InjectTapForTesting(-1);

            // Assert
            Assert.AreEqual(1, _spy.MoveCancelledCount,  "move_cancelled must be emitted exactly once");
            Assert.AreEqual(0, _spy.MoveCommittedCount,  "move_committed must NOT be emitted on cancellation");
        }

        /// <summary>
        /// AC-15a: sequence_id in move_committed is positive and increments across moves.
        /// </summary>
        [Test]
        public void Test_MoveCommitted_SequenceId_IsPositiveAndIncrementing()
        {
            // Arrange: board with empty stack 1 for legal move
            _gsm.SetColorStacks(new[] { 1, 1 }, Array.Empty<int>());
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm, _logger);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            // Act: commit two moves
            _sm.InjectTapForTesting(0); // lift from 0
            _sm.InjectTapForTesting(1); // stack 1 empty → legal → seq = 1; now in MoveExecuting
            long firstSeq = _spy.LastCommit.seq;

            // Exit MoveExecuting, then commit a second move.
            // MockGsm is static — stack 0 still shows [1,1] and stack 1 still shows empty,
            // so the same valid move (0→1) can be committed again.
            _sm.OnAnimationComplete(firstSeq);

            _sm.InjectTapForTesting(0); // lift color 1 from stack 0 (static board unchanged)
            _sm.InjectTapForTesting(1); // stack 1 still empty in MockGsm → legal → seq = 2
            long secondSeq = _spy.LastCommit.seq;

            // Assert
            Assert.Greater(firstSeq, 0L, "sequence_id must be positive");
            Assert.Greater(secondSeq, firstSeq, "sequence_id must increment between moves");
        }

        // ── MoveExecuting: tap discard ────────────────────────────────────────────

        /// <summary>
        /// AC-08a / AC-08b: Taps in MOVE_EXECUTING produce zero events and no state drift.
        /// Story 001 scope: first buffered tap is also discarded (buffer is Story 002).
        /// </summary>
        [Test]
        public void Test_MoveExecuting_AdditionalTaps_Discarded()
        {
            // Arrange: board with empty stack 1 so move 0→1 is legal
            _gsm.SetColorStacks(new[] { 1, 1 }, Array.Empty<int>());
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm, _logger);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            // Enter MoveExecuting
            _sm.InjectTapForTesting(0); // BoltSelected
            _sm.InjectTapForTesting(1); // stack 1 empty → legal → MoveExecuting
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting);
            int emissionsAtEntry = _spy.TotalEvents;

            // Act: inject more taps
            _sm.InjectTapForTesting(0);
            _sm.InjectTapForTesting(1);
            _sm.InjectTapForTesting(-1);

            // Assert
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting,
                "State must not drift from MoveExecuting");
            Assert.AreEqual(emissionsAtEntry, _spy.TotalEvents,
                "Zero additional events during MoveExecuting");
        }

        // ── Full round-trip: lift → cancel → lift → commit ────────────────────────

        /// <summary>
        /// Round-trip: cancel one bolt, then lift and commit another.
        /// Verifies the FSM resets cleanly between interactions.
        /// </summary>
        [Test]
        public void Test_RoundTrip_CancelThenCommit_StateClean()
        {
            // Arrange: board with empty stack 1 so move 0→1 is legal
            _gsm.SetColorStacks(new[] { 1, 1 }, Array.Empty<int>());
            _gsm.TempSlotCount = 0;
            _gsm.TempSlotContents = null;
            _sm.InjectForTesting(_gsm, _logger);
            _sm.InitializeBoard();
            _spy = new EventSpy();
            _spy.Subscribe(_sm);

            // First interaction: lift stack 0, cancel
            _sm.InjectTapForTesting(0);
            _sm.InjectTapForTesting(0); // cancel
            Assert.AreEqual(SortMechState.Idle, _sm.CurrentStateForTesting);

            // Second interaction: lift stack 0 again, commit to stack 1 (empty → legal)
            _sm.InjectTapForTesting(0);
            _sm.InjectTapForTesting(1);

            // Assert
            Assert.AreEqual(SortMechState.MoveExecuting, _sm.CurrentStateForTesting);
            Assert.AreEqual(1, _spy.MoveCancelledCount, "Exactly one cancellation");
            Assert.AreEqual(1, _spy.MoveCommittedCount, "Exactly one committed move");
        }
    }
}
