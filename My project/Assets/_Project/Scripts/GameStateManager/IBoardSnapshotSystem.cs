namespace BoltSort.GameStateManager
{
    /// <summary>
    /// Minimal Save &amp; Persistence interface for GSM board state serialization.
    /// Created by Story 008 for injection into GSM; full implementation is in the
    /// Save &amp; Persistence epic (<c>SaveSystem.cs</c>).
    /// Follows ADR-0003 W-2 write pattern — all calls must be synchronous (no await).
    /// </summary>
    public interface IBoardSnapshotSystem
    {
        /// <summary>
        /// Writes the board snapshot synchronously (ADR-0003 W-2 path).
        /// Called from <c>OnApplicationPause(true)</c> — must complete before iOS 5-second suspension.
        /// Never use <c>async</c> in the pause handler.
        /// </summary>
        void WriteBoardSnapshotSync(BoardSnapshot snapshot);

        /// <summary>
        /// Returns the stored board snapshot, or <c>null</c> if none exists.
        /// Returns a snapshot with <c>IsValid = false</c> if deserialization fails (corrupt data).
        /// Called from <c>OnApplicationPause(false)</c> (foreground restore).
        /// </summary>
        BoardSnapshot ReadBoardSnapshot();
    }
}
