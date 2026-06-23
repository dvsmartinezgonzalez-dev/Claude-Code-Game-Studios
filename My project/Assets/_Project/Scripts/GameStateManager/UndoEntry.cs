using System.Collections.Generic;

namespace BoltSort.GameStateManager
{
    /// <summary>
    /// Immutable record of a single committed board move. Pushed onto the undo stack by
    /// <see cref="GameStateManager.HandleMoveCommitted"/> at Step 3 of the atomic 5-step mutation.
    /// Used by <c>ExecuteUndo()</c> (Story 002) to reverse the move.
    /// </summary>
    /// <remarks>
    /// <c>SeqId</c> captures <c>CurrentSequenceId</c> at the moment the move was committed —
    /// i.e. the ID BEFORE the post-commit increment. This matches the seqId value that was
    /// in-flight during the animation that corresponds to this undo entry.
    /// Defined as a struct (value type) to avoid per-entry heap allocation.
    /// Implements the undo data structure from ADR-0006.
    /// </remarks>
    public struct UndoEntry
    {
        /// <summary>Flat-namespace index of the stack or temp slot the bolt moved FROM.</summary>
        public int From;

        /// <summary>Flat-namespace index of the stack or temp slot the bolt moved TO.</summary>
        public int To;

        /// <summary>Color identifier of the bolt that was moved.</summary>
        public int ColorId;

        /// <summary>
        /// Number of same-color bolts moved as one logical action. 1 for a normal single move;
        /// &gt;1 when the Magnet mechanic (L50+) auto-chained additional bolts. Undo reverses
        /// all <c>Count</c> bolts atomically. A value of 0 (legacy/unset) is treated as 1.
        /// </summary>
        public int Count;

        /// <summary>
        /// <c>CurrentSequenceId</c> value at the time the move was committed (before the post-commit
        /// increment). <c>long</c> (int64) per GDD EC-12: int64 minimum for stale-signal safety.
        /// </summary>
        public long SeqId;

        /// <summary>
        /// Phase-2 (schema_version 2): source-column depths (bottom=0) of mystery bolts that this
        /// move revealed (negative→positive flip when exposed at the source top, possibly several
        /// during a Magnet chain). Null when the move revealed nothing — the common case, kept null
        /// to avoid per-move heap allocation. On undo the reverse re-stacks the covering bolts and
        /// these depths are flipped back to their hidden (negative) encoding (UND rule #10). All
        /// reveals in one move occur on the <see cref="From"/> column.
        /// </summary>
        public List<int> RevealedDepths;
    }
}
