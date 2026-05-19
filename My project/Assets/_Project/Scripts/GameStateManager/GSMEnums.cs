namespace BoltSort.GameStateManager
{
    /// <summary>
    /// Reason codes for <see cref="IGameStateManager.OnSessionLoadFailed"/>.
    /// Maps to the GDD <c>session_load_failed</c> reason strings.
    /// </summary>
    public enum GsmSessionLoadFailReason
    {
        /// <summary>L-03: bolt_count_invariant check failed (total count or per-color distribution).</summary>
        InvariantViolation,

        /// <summary>L-01: Level Data System not ready when load was attempted.</summary>
        LevelDataUnavailable,

        /// <summary>L-02: LDS returned an error for the requested level ID.</summary>
        LevelRecordError,

        /// <summary>L-05: board array allocation or population failed.</summary>
        InstantiationError,

        /// <summary>SER-03: foreground-restore deserialization failed (corrupt or missing save).</summary>
        SaveCorrupt,
    }

    /// <summary>
    /// Lifecycle states for <see cref="GameStateManager"/>.
    /// Implements the Level Lifecycle FSM defined in ADR-0006.
    /// </summary>
    /// <remarks>
    /// Transition table:
    /// <list type="bullet">
    ///   <item>UNLOADED → LOADING  : <c>LoadLevel()</c> called</item>
    ///   <item>LOADING  → ACTIVE   : board arrays allocated and bolt_count_invariant passes</item>
    ///   <item>ACTIVE   → COMPLETE : <c>OnPuzzleSolved</c> received (Story 003)</item>
    ///   <item>COMPLETE → TEARDOWN : next level initiated (Story 005)</item>
    ///   <item>TEARDOWN → UNLOADED : cleanup complete</item>
    /// </list>
    /// </remarks>
    public enum GSMLifecycleState
    {
        /// <summary>Startup state and post-teardown state. No board data exists.</summary>
        Unloaded,

        /// <summary>
        /// <c>LoadLevel()</c> called; board arrays are being allocated and
        /// <c>LDS.GetLevel()</c> is in progress.
        /// </summary>
        Loading,

        /// <summary>
        /// Board data is valid. Undo stack is writable. All board mutation events are active.
        /// </summary>
        Active,

        /// <summary>
        /// Puzzle solved. Undo stack is frozen (no new entries). <c>OnLevelComplete</c> has fired.
        /// </summary>
        Complete,

        /// <summary>Board arrays are being nulled. Watchdog must be cancelled before entering.</summary>
        Teardown,
    }
}
