using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;
using BoltSort.LevelData;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.GameStateManager.asmdef
// Covers:    Story 003 ACs — AC-GSM-08
// Design:    design/gdd/game-state-manager.md  (TR-GSM-008, TR-GSM-009, TR-GSM-010)
// ADR:       docs/architecture/adr-0006-board-state-representation.md
//            docs/architecture/adr-0012-hud-and-level-complete-ui-business-logic.md

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class WinDetection_Test
    {
        private GameObject _go;
        private global::BoltSort.GameStateManager.GameStateManager _gsm;

        // ── Minimal LDS stub ─────────────────────────────────────────────────────
        // Only GetLevel() is implemented — all other methods are not required by Story 003.

        private sealed class StubLds : ILevelDataSystem
        {
            private readonly int _parMoves;
            public StubLds(int parMoves) => _parMoves = parMoves;

            public bool IsReady => true;
            public event Action OnLevelDataReady { add {} remove {} }
            public Task InitializeAsync() => Task.CompletedTask;
            public Task ReloadAsync() => Task.CompletedTask;
            public SystemReadiness GetReadiness() => default;

            public LevelRecord GetLevel(int levelId)
            {
                var json = $@"{{
                    ""level_id"": {levelId},
                    ""par_moves"": {_parMoves},
                    ""color_count"": 2,
                    ""stack_depth"": 4,
                    ""color_stacks"": [[1,2],[3,4]],
                    ""temp_slot_count"": 0,
                    ""temp_slot_depth"": 1,
                    ""schema_version"": 1,
                    ""daily_challenge_eligible"": false,
                    ""is_tutorial"": false
                }}";
                return JsonConvert.DeserializeObject<LevelRecord>(json);
            }

            public LevelRecord[] GetRange(int f, int t) => Array.Empty<LevelRecord>();
            public LevelRecord[] GetByFilter(LevelFilter filter) => Array.Empty<LevelRecord>();
        }

        // ── Test Lifecycle ────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("GSM_WinTest");
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
        /// Seeds GSM into ACTIVE with move_count=7, current_sequence_id=7, level_id=42,
        /// and a stub LDS returning par_moves=10 for level 42.
        /// Matches the QA test case precondition exactly.
        /// </summary>
        private void SeedForWinTest()
        {
            var combined = new List<int>[]
            {
                new List<int> { 1, 2 },
                new List<int> { 3, 4 },
                new List<int>(),
            };
            _gsm.SeedBoardForTesting(
                combinedContents:  combined,
                stackDepth:        4,
                tempSlotDepth:     1,
                tempSlotCount:     1,
                colorCount:        2,
                initialSeqId:      7L,
                initialMoveCount:  7);
            _gsm.SeedLevelForTesting(levelId: 42, levelDataSystem: new StubLds(parMoves: 10));
            _gsm.SetStateForTesting(GSMLifecycleState.Active);
        }

        // ── AC-GSM-08: WIN-01 transition, payload, and frozen state ──────────────

        /// <summary>
        /// AC-GSM-08 (1): puzzle_solved() transitions GSM to COMPLETE state.
        /// </summary>
        [Test]
        public void Test_HandlePuzzleSolved_TransitionsToComplete()
        {
            SeedForWinTest();
            _gsm.SimulatePuzzleSolved();
            Assert.AreEqual(GSMLifecycleState.Complete, _gsm.LifecycleState,
                "puzzle_solved() must transition GSM to COMPLETE");
        }

        /// <summary>
        /// AC-GSM-08 (2): level_complete emitted with correct 4-arg payload.
        /// sequenceId = 7 (NOT incremented — WIN-01).
        /// </summary>
        [Test]
        public void Test_HandlePuzzleSolved_EmitsLevelComplete_WithCorrectPayload()
        {
            SeedForWinTest();

            int  emittedLevelId  = -1;
            int  emittedMoveCount = -1;
            int  emittedParMoves  = -1;
            long emittedSeqId    = -1L;

            void LevelCompleteSpy(int lId, int mc, int pm, long s)
            {
                emittedLevelId   = lId;
                emittedMoveCount = mc;
                emittedParMoves  = pm;
                emittedSeqId     = s;
            }
            _gsm.OnLevelComplete += LevelCompleteSpy;

            _gsm.SimulatePuzzleSolved();

            Assert.AreEqual(42,  emittedLevelId,   "level_complete must carry levelId=42");
            Assert.AreEqual(7,   emittedMoveCount,  "level_complete must carry moveCount=7");
            Assert.AreEqual(10,  emittedParMoves,   "level_complete must carry parMoves=10 from LDS stub");
            Assert.AreEqual(7L,  emittedSeqId,      "level_complete must carry sequenceId=7 (NOT incremented — WIN-01)");

            _gsm.OnLevelComplete -= LevelCompleteSpy;
        }

        /// <summary>
        /// AC-GSM-08 (seqId not incremented): CurrentSequenceId stays 7 after puzzle_solved().
        /// WIN-01: the winning move_committed already incremented it in BSM-01.
        /// </summary>
        [Test]
        public void Test_HandlePuzzleSolved_SequenceIdNotIncremented()
        {
            SeedForWinTest();
            _gsm.SimulatePuzzleSolved();
            Assert.AreEqual(7L, _gsm.CurrentSequenceId,
                "CurrentSequenceId must not be incremented by puzzle_solved() (WIN-01)");
        }

        /// <summary>
        /// AC-GSM-08 (3): move_committed received after puzzle_solved() is silently ignored.
        /// Board state unchanged, no events emitted.
        /// </summary>
        [Test]
        public void Test_HandleMoveCommitted_AfterWin_IsIgnored()
        {
            SeedForWinTest();
            _gsm.SimulatePuzzleSolved();

            int  boardCountBefore = _gsm.StackContents[0].Count;
            long seqIdBefore      = _gsm.CurrentSequenceId;
            int  moveCountBefore  = _gsm.MoveCount;

            int eventsAfterWin = 0;
            void PostWinBoardSpy(long s, int m) => eventsAfterWin++;
            _gsm.OnBoardStateChanged += PostWinBoardSpy;

            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 7L);

            Assert.AreEqual(boardCountBefore, _gsm.StackContents[0].Count,
                "Post-win move_committed must not change board state");
            Assert.AreEqual(seqIdBefore, _gsm.CurrentSequenceId,
                "Post-win move_committed must not change CurrentSequenceId");
            Assert.AreEqual(moveCountBefore, _gsm.MoveCount,
                "Post-win move_committed must not change MoveCount");
            Assert.AreEqual(0, eventsAfterWin,
                "Post-win move_committed must not emit OnBoardStateChanged");

            _gsm.OnBoardStateChanged -= PostWinBoardSpy;
        }

        /// <summary>
        /// AC-GSM-08 (4): undo_requested received after puzzle_solved() is silently ignored.
        /// No mutation, no event.
        /// </summary>
        [Test]
        public void Test_HandleUndoRequested_AfterWin_IsIgnored()
        {
            SeedForWinTest();
            // Commit one move so undo stack is non-empty — ensures the guard fires, not empty-stack path
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 7L);
            long seqIdBeforeWin = _gsm.CurrentSequenceId; // 8 after the commit

            _gsm.SimulatePuzzleSolved();

            long seqIdAfterWin = _gsm.CurrentSequenceId;
            int undoDepthAfterWin = _gsm.UndoStackDepth;

            int eventsAfterWin = 0;
            void PostWinUndoSpy(long s, int m) => eventsAfterWin++;
            _gsm.OnBoardStateChanged += PostWinUndoSpy;

            _gsm.UndoRequested();

            Assert.AreEqual(seqIdAfterWin, _gsm.CurrentSequenceId,
                "Post-win undo_requested must not change CurrentSequenceId");
            Assert.AreEqual(undoDepthAfterWin, _gsm.UndoStackDepth,
                "Post-win undo_requested must not pop the undo stack");
            Assert.AreEqual(0, eventsAfterWin,
                "Post-win undo_requested must not emit OnBoardStateChanged");

            _gsm.OnBoardStateChanged -= PostWinUndoSpy;
        }

        /// <summary>
        /// AC-GSM-08 (5): No events emitted after level_complete.
        /// Subscribing a global spy to all GSM events confirms only one level_complete fires.
        /// </summary>
        [Test]
        public void Test_HandlePuzzleSolved_NoEventsEmittedAfterLevelComplete()
        {
            SeedForWinTest();

            var emittedEvents = new List<string>();
            void BoardSpy(long s, int m) => emittedEvents.Add("board_state_changed");
            void WinSpy(int lId, int mc, int pm, long s) => emittedEvents.Add("level_complete");
            _gsm.OnBoardStateChanged += BoardSpy;
            _gsm.OnLevelComplete     += WinSpy;

            // Trigger win
            _gsm.SimulatePuzzleSolved();

            Assert.AreEqual(1, emittedEvents.Count,
                "Exactly one event must fire on puzzle_solved()");
            Assert.AreEqual("level_complete", emittedEvents[0],
                "The one event must be level_complete");

            // Post-win interactions must emit nothing
            _gsm.SimulateMoveCommitted(src: 0, dst: 1, colorId: 2, seqId: 7L);
            _gsm.UndoRequested();
            _gsm.SimulatePuzzleSolved(); // second puzzle_solved — must be no-op

            Assert.AreEqual(1, emittedEvents.Count,
                "No additional events must fire after level_complete (post-win actions are all no-ops)");

            _gsm.OnBoardStateChanged -= BoardSpy;
            _gsm.OnLevelComplete     -= WinSpy;
        }

        /// <summary>
        /// AC-GSM-08 (4, QA spec exact): post-win undo_requested leaves seqId=7 and moveCount=7 frozen.
        /// Matches QA test case precondition exactly (move_count=7, seqId=7 at win time).
        /// </summary>
        [Test]
        public void Test_HandleUndoRequested_AfterWin_WinningValuesAreFrozen()
        {
            SeedForWinTest();                   // move_count=7, seqId=7
            _gsm.SimulatePuzzleSolved();        // seqId stays 7 (WIN-01); state = COMPLETE

            long seqIdAtWin = _gsm.CurrentSequenceId;  // must be 7
            int  movesAtWin = _gsm.MoveCount;           // must be 7

            _gsm.UndoRequested();               // COMPLETE state guard fires — no-op

            Assert.AreEqual(seqIdAtWin, _gsm.CurrentSequenceId,
                "Post-win undo must not change CurrentSequenceId — winning seqId must remain frozen at 7");
            Assert.AreEqual(movesAtWin, _gsm.MoveCount,
                "Post-win undo must not change MoveCount — winning value must remain frozen at 7");
            Assert.AreEqual(7L, _gsm.CurrentSequenceId,
                "seqId must be exactly 7 — confirming WIN-01 (not incremented) and undo guard (not decremented)");
            Assert.AreEqual(7, _gsm.MoveCount,
                "MoveCount must be exactly 7 — confirming undo guard respects COMPLETE state");
        }

        /// <summary>
        /// Edge case: puzzle_solved() received while already in COMPLETE — no second level_complete.
        /// </summary>
        [Test]
        public void Test_HandlePuzzleSolved_WhenAlreadyComplete_NoSecondEvent()
        {
            SeedForWinTest();

            int winEventCount = 0;
            void WinSpy(int lId, int mc, int pm, long s) => winEventCount++;
            _gsm.OnLevelComplete += WinSpy;

            _gsm.SimulatePuzzleSolved(); // first: fires event
            _gsm.SimulatePuzzleSolved(); // second: must be no-op

            Assert.AreEqual(1, winEventCount,
                "level_complete must fire exactly once — second puzzle_solved() must be ignored (EC-15)");

            _gsm.OnLevelComplete -= WinSpy;
        }

        /// <summary>
        /// Edge case: puzzle_solved() with null LDS (Story 005 not yet wired) emits parMoves=0.
        /// Guards against NullReferenceException when LDS is not injected.
        /// </summary>
        [Test]
        public void Test_HandlePuzzleSolved_WithNullLds_EmitsParMovesZero()
        {
            var combined = new List<int>[]
            {
                new List<int> { 1 },
                new List<int>(),
                new List<int>(),
            };
            _gsm.SeedBoardForTesting(
                combinedContents: combined,
                stackDepth: 4, tempSlotDepth: 1, tempSlotCount: 1, colorCount: 2,
                initialSeqId: 0L, initialMoveCount: 1);
            _gsm.SeedLevelForTesting(levelId: 1, levelDataSystem: null);
            _gsm.SetStateForTesting(GSMLifecycleState.Active);

            int emittedParMoves = -1;
            void WinSpy(int lId, int mc, int pm, long s) => emittedParMoves = pm;
            _gsm.OnLevelComplete += WinSpy;

            Assert.DoesNotThrow(() => _gsm.SimulatePuzzleSolved(),
                "puzzle_solved() must not throw when LDS is null");
            Assert.AreEqual(0, emittedParMoves,
                "parMoves must be 0 when LDS is null (graceful fallback)");

            _gsm.OnLevelComplete -= WinSpy;
        }
    }
}
