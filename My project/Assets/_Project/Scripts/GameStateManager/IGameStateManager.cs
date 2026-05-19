using System;
using System.Collections.Generic;

namespace BoltSort.GameStateManager
{
    /// <summary>
    /// Public contract for the Game State Manager. All external systems must depend on this
    /// interface — never on the concrete <see cref="GameStateManager"/> class.
    /// Implements the Key Interfaces section of ADR-0006.
    /// </summary>
    /// <remarks>
    /// Story 001 subset: board state read properties, <c>OnBoardStateChanged</c> event declaration,
    /// and command stubs for <c>LoadLevel</c> / <c>UndoRequested</c>.
    /// Story 005: <c>OnLevelLoaded</c> event added; <c>LoadLevel</c> fully implemented.
    /// Full interface is expanded as stories are implemented.
    /// </remarks>
    public interface IGameStateManager
    {
        // ── Board State (read-only, synchronous pull) ────────────────────────────

        /// <summary>
        /// Read-only view of every stack column in flat-namespace order:
        /// indices 0..<c>ColorCount-1</c> are color stacks;
        /// indices <c>ColorCount</c>..<c>ColorCount+TempSlotCount-1</c> are temp slots.
        /// Inner list index 0 = bottom bolt; last index = top bolt. Empty stack = empty list.
        /// </summary>
        IReadOnlyList<int>[] StackContents { get; }

        /// <summary>Maximum bolts allowed in a single color stack column.</summary>
        int StackDepth { get; }

        /// <summary>
        /// Read-only view of every temp slot in slot-index order (index 0 = first temp slot).
        /// Inner list index 0 = bottom bolt; last index = top bolt. Empty slot = empty list.
        /// </summary>
        IReadOnlyList<int>[] TempSlotContents { get; }

        /// <summary>Maximum bolts allowed in a single temp slot.</summary>
        int TempSlotDepth { get; }

        /// <summary>Number of temp slot columns in the current level.</summary>
        int TempSlotCount { get; }

        /// <summary>Number of distinct bolt colors in the current level.</summary>
        int ColorCount { get; }

        /// <summary>Total number of committed moves since <c>LoadLevel</c> was called.</summary>
        int MoveCount { get; }

        /// <summary>
        /// Monotonically increasing sequence identifier. Incremented at Step 4 of every
        /// atomic board mutation. <c>long</c> (int64) per GDD EC-12 stale-signal safety requirement.
        /// </summary>
        long CurrentSequenceId { get; }

        /// <summary>Current position in the level lifecycle FSM.</summary>
        GSMLifecycleState LifecycleState { get; }

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired after an undo operation or foreground restore — NOT after a regular
        /// <c>move_committed</c> mutation (AC-GSM-03). Carries the new sequence ID and move count.
        /// Parameters: <c>(long sequenceId, int moveCount)</c>.
        /// </summary>
        event Action<long, int> OnBoardStateChanged;

        /// <summary>
        /// Fired when a level load fails at any of steps L-01 through L-05, or on foreground-restore
        /// deserialization failure (SER-03). Carries the reason code and the level ID (0 if unavailable).
        /// After firing, GSM transitions to UNLOADED. Level Progression subscribes for error recovery.
        /// Parameters: <c>(GsmSessionLoadFailReason reason, int levelId)</c>.
        /// </summary>
        event Action<GsmSessionLoadFailReason, int> OnSessionLoadFailed;

        /// <summary>
        /// Fired once when the puzzle is solved and GSM transitions to COMPLETE.
        /// Canonical 4-arg payload per ADR-0012: <c>(int levelId, int moveCount, int parMoves, long sequenceId)</c>.
        /// <c>sequenceId</c> is the value at WIN transition — NOT incremented on win (WIN-01).
        /// <c>parMoves</c> is read from LDS by GSM so downstream systems never re-query LDS.
        /// </summary>
        event Action<int, int, int, long> OnLevelComplete;

        /// <summary>
        /// Fired once when a level load completes at step L-06, immediately after board arrays
        /// are allocated and before any gameplay input is accepted.
        /// Carries all board dimensions so subscribers never need to query LDS independently.
        /// ADR-0001 subscribe-then-check: callers must check <c>LifecycleState == Active</c>
        /// after subscribing in case the load completed before subscription.
        /// Parameters: <c>(int levelId, int colorCount, int stackDepth, int tempSlotCount, int tempSlotDepth, long sequenceId)</c>.
        /// <c>sequenceId</c> is always 0 at load time (L-05 resets to 0).
        /// </summary>
        event Action<int, int, int, int, int, long> OnLevelLoaded;

        /// <summary>
        /// Fired when the watchdog timer (1500ms) expires after <c>move_committed</c> without
        /// a corresponding <c>OnMoveExecutingExited</c> arriving (WDG-01).
        /// GSM increments <c>CurrentSequenceId</c> before firing — subscribers receive the new value.
        /// Board reflects the committed move — no rollback occurs.
        /// Sort Mechanic and Animation System exit MOVE_EXECUTING on receipt.
        /// Parameters: <c>(long sequenceId)</c> — the new sequence ID after the watchdog increment.
        /// </summary>
        event Action<long> OnBoardRefreshForced;

        /// <summary>
        /// Fired when TEARDOWN completes (L-08) or a LOADING cancellation completes (L-08-LOADING).
        /// Signals that <c>LoadLevel</c> may be called again.
        /// Parameters: <c>(int? levelId)</c> — the level that was torn down; <c>null</c> if
        /// <c>ExitLevel</c> was called during LOADING before the level ID was confirmed.
        /// </summary>
        event Action<int?> OnLevelUnloaded;

        // ── Commands ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads the level with the given ID, transitioning GSM from UNLOADED → LOADING → ACTIVE.
        /// Executes the L-01 through L-07 pipeline synchronously on the main thread.
        /// <list type="bullet">
        ///   <item>L-01: Boot guard — queries LDS readiness (never cached).</item>
        ///   <item>L-02: Fetches <c>LevelRecord</c> from LDS.</item>
        ///   <item>L-03: Runs bolt-count invariant checks (Story 004).</item>
        ///   <item>L-04: Pre-won board detection — logs warning only; does NOT auto-win.</item>
        ///   <item>L-05: Instantiates board arrays; resets sequence ID, move count, undo stack.</item>
        ///   <item>L-07: Transitions to ACTIVE (must precede L-06 so subscribers see correct state).</item>
        ///   <item>L-06: Fires <c>OnLevelLoaded</c>.</item>
        /// </list>
        /// No-op (silent reject) when GSM is in any non-UNLOADED state (EC-09).
        /// Fires <c>OnSessionLoadFailed</c> and transitions to UNLOADED on any step failure.
        /// </summary>
        void LoadLevel(int levelId);

        /// <summary>
        /// Requests an undo of the last committed move.
        /// If GSM is in MOVE_EXECUTING state, the undo is deferred until <c>OnMoveExecutingExited</c>.
        /// </summary>
        void UndoRequested();

        /// <summary>
        /// Initiates TEARDOWN: cancels the watchdog, clears all board state, transitions to UNLOADED,
        /// and fires <c>OnLevelUnloaded</c>. Called exclusively by Level Progression.
        /// <list type="bullet">
        ///   <item>ACTIVE / COMPLETE: full L-08 teardown — board cleared, <c>OnLevelUnloaded(levelId)</c> fired.</item>
        ///   <item>LOADING: cancellation — partial state discarded, <c>OnLevelUnloaded(null)</c> fired.</item>
        ///   <item>UNLOADED: no-op.</item>
        /// </list>
        /// </summary>
        void ExitLevel();
    }
}
