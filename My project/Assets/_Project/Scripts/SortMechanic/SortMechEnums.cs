namespace BoltSort.SortMechanic
{
    /// <summary>
    /// FSM states for <see cref="SortMechanic"/>.
    /// Implements the States and Transitions table in <c>design/gdd/sort-mechanic.md</c>.
    /// </summary>
    public enum SortMechState
    {
        /// <summary>
        /// Waiting for player input. No bolt is held.
        /// Entry: level load, move complete, cancellation complete.
        /// </summary>
        Idle,

        /// <summary>
        /// Player has lifted a bolt. Validating destination taps.
        /// Entry: non-empty stack/slot top tapped with nothing held.
        /// </summary>
        BoltSelected,

        /// <summary>
        /// Legal destination tapped; bolt travel animation is in flight.
        /// Sort Mechanic buffers exactly one tap; further taps are discarded.
        /// Entry: valid destination tapped while in BoltSelected.
        /// </summary>
        MoveExecuting,

        /// <summary>
        /// Source or empty space tapped while holding. Cancel animation plays asynchronously.
        /// Sort Mechanic returns to Idle synchronously on <c>move_cancelled</c> emission.
        /// Entry: source tap (S-03) or empty-space tap (S-05) while in BoltSelected.
        /// </summary>
        Cancellation,

        /// <summary>
        /// Invalid destination tapped while holding. Rejection animation plays.
        /// Buffers exactly one tap during the animation window.
        /// Entry: illegal destination tapped while in BoltSelected.
        /// </summary>
        InvalidMove,

        /// <summary>
        /// Win condition satisfied. All input is locked. No further events are emitted.
        /// Entry: win condition evaluates TRUE after animation_complete receipt.
        /// </summary>
        Win,
    }

    /// <summary>
    /// Reason codes for the <c>level_load_failed</c> event emitted by <see cref="SortMechanic"/>
    /// when an initialization assertion fails.
    /// Covers all three initialization assertions from <c>design/gdd/sort-mechanic.md</c>
    /// Win Condition Formula section.
    /// </summary>
    public enum SortMechLoadFailReason
    {
        /// <summary>
        /// <c>len(color_stacks) ≠ color_count</c> (assertion 1),
        /// <c>temp_slot_depth &gt; stack_depth</c> (assertion 2), or
        /// a <c>color_id</c> outside <c>{1..color_count}</c> was found in level data (assertion 3).
        /// </summary>
        CorruptedBoardState,
    }

    /// <summary>
    /// Reason codes carried by the <c>move_rejected</c> event.
    /// Maps to V-03 (ColorMismatch) and V-04 (DestinationFull) in the GDD move validation table.
    /// </summary>
    public enum MoveRejectedReason
    {
        /// <summary>
        /// Default sentinel — not a real rejection reason.
        /// <c>default(MoveRejectedReason)</c> resolves to this value, not to a rejection code.
        /// </summary>
        None = 0,

        /// <summary>
        /// Destination top bolt color does not match the held bolt color (V-03).
        /// Only evaluated when destination is non-empty and not full.
        /// </summary>
        ColorMismatch = 1,

        /// <summary>
        /// Destination bolt count equals or exceeds its capacity (V-04).
        /// Evaluated before color check — full stacks are illegal regardless of color.
        /// </summary>
        DestinationFull = 2,

        /// <summary>
        /// Destination tube is frozen (remaining freeze turns &gt; 0) — deposits are rejected
        /// while frozen, though removals stay legal. Phase-2 mechanic (schema_version 2).
        /// Evaluated before the empty/full/color checks. See phase-2 TDD §1.3.
        /// </summary>
        DestinationFrozen = 3,
    }
}
