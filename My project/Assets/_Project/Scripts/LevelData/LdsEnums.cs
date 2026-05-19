namespace BoltSort.LevelData
{
    /// <summary>
    /// Runtime state machine states for LevelDataSystem.
    /// Transitions: Uninitialized → Loading → Ready | Degraded.
    /// Degraded may transition back to Loading via ReloadAsync().
    /// </summary>
    public enum LdsState
    {
        /// <summary>System has not started initialization. All getters throw InvalidOperationException.</summary>
        Uninitialized,

        /// <summary>InitializeAsync or ReloadAsync is in progress. All getters throw InvalidOperationException.</summary>
        Loading,

        /// <summary>Catalogue loaded; failure_ratio ≤ 0.20. All getters safe.</summary>
        Ready,

        /// <summary>Load completed but failure_ratio > 0.20, or Addressables failed. Getters callable.</summary>
        Degraded
    }

    /// <summary>
    /// Structured error codes for LevelDataException and diagnostic reporting.
    /// </summary>
    public enum LdsErrorCode
    {
        /// <summary>Requested level ID does not exist in the loaded catalogue.</summary>
        NotFound,

        /// <summary>A LevelRecord failed one or more Stage 2 field constraint checks.</summary>
        ValidationFailed,

        /// <summary>Catalogue schema_version does not match the expected version.</summary>
        VersionMismatch,

        /// <summary>Caller invoked a getter while system is Uninitialized or Loading.</summary>
        SystemNotReady
    }
}
