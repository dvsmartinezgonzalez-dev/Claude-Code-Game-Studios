namespace BoltSort.GameStateManager
{
    /// <summary>
    /// Board state snapshot serialized to Save &amp; Persistence on app background (SER-01).
    /// Deserialized on foreground restore (SER-02/SER-03).
    /// The undo stack is intentionally excluded — it is session-only (GDD SER-01).
    /// </summary>
    public class BoardSnapshot
    {
        /// <summary>Color stack contents — indices 0..ColorCount-1. Inner array: bottom=0, top=last.</summary>
        public int[][] StackContents    { get; set; }

        /// <summary>Temp slot contents — indices 0..TempSlotCount-1.</summary>
        public int[][] TempSlotContents { get; set; }

        /// <summary>Maximum bolts per color stack column.</summary>
        public int StackDepth           { get; set; }

        /// <summary>Maximum bolts per temp slot.</summary>
        public int TempSlotDepth        { get; set; }

        /// <summary>Number of temp slot columns (0 if none).</summary>
        public int TempSlotCount        { get; set; }

        /// <summary>Number of distinct bolt colors.</summary>
        public int ColorCount           { get; set; }

        /// <summary>
        /// Phase-2: per-column capacity (flat order). Null ⇒ uniform <see cref="StackDepth"/>/<see cref="TempSlotDepth"/>.
        /// </summary>
        public int[] TubeCapacities     { get; set; }

        /// <summary>
        /// Phase-2: initial freeze turns per flat column (0 = none). Null ⇒ no frozen tubes.
        /// Remaining freeze is derived from this and <see cref="MoveCount"/> on restore.
        /// </summary>
        public int[] FreezeInit         { get; set; }

        /// <summary>Total committed moves since level load.</summary>
        public int MoveCount            { get; set; }

        /// <summary>GSM sequence ID at the time of serialization.</summary>
        public long SequenceId          { get; set; }

        /// <summary>Level ID of the active level.</summary>
        public int LevelId              { get; set; }

        /// <summary>"Active" or "Complete" — serialized lifecycle state.</summary>
        public string GsmState          { get; set; }

        /// <summary>EC-06: true if Sort Mechanic was in BOLT_SELECTED when app backgrounded.</summary>
        public bool WasInBoltSelected   { get; set; }

        /// <summary>
        /// False when deserialization failed (corrupt data, schema mismatch).
        /// GSM checks this before restoring — a false value triggers SER-03.
        /// </summary>
        public bool IsValid             { get; set; } = true;
    }
}
