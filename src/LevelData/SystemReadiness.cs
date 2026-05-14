namespace BoltSort.LevelData
{
    /// <summary>
    /// Immutable snapshot of LevelDataSystem readiness returned by GetReadiness().
    /// Safe to copy and cache by callers.
    /// </summary>
    public readonly struct SystemReadiness
    {
        /// <summary>True when LevelDataSystem is in READY state. False for DEGRADED.</summary>
        public bool Ready { get; }

        /// <summary>Number of LevelRecord entries that passed Stage 2 validation.</summary>
        public int LoadedCount { get; }

        /// <summary>Number of LevelRecord entries that failed validation and were skipped.</summary>
        public int SkippedCount { get; }

        /// <summary>Catalogue version integer read from the JSON root.</summary>
        public int CatalogueVersion { get; }

        /// <summary>
        /// Machine-readable error code string. Null when Ready is true.
        /// Example values: "CATALOGUE_LOAD_FAILED", "CATALOGUE_PARTIAL_FAILURE".
        /// </summary>
        public string DiagnosticCode { get; }

        /// <param name="ready">True = READY state; false = DEGRADED.</param>
        /// <param name="loaded">Records that passed validation.</param>
        /// <param name="skipped">Records that failed validation.</param>
        /// <param name="version">Catalogue version from JSON root.</param>
        /// <param name="code">Diagnostic code; null when ready.</param>
        public SystemReadiness(bool ready, int loaded, int skipped, int version, string code)
        {
            Ready = ready;
            LoadedCount = loaded;
            SkippedCount = skipped;
            CatalogueVersion = version;
            DiagnosticCode = code;
        }
    }
}
