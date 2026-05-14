namespace BoltSort.LevelData
{
    /// <summary>
    /// Describes a single validation failure returned by LevelRecordValidator.ValidateRecord().
    /// </summary>
    public readonly struct LdsValidationError
    {
        /// <summary>
        /// Error classification. ValidationFailed for field constraint failures;
        /// VersionMismatch for unknown schema_version values.
        /// </summary>
        public LdsErrorCode Code { get; }

        /// <summary>
        /// JSON field name (snake_case) that caused the failure.
        /// Matches the exact JSON key, e.g. "color_stacks", "par_moves".
        /// </summary>
        public string FailingField { get; }

        /// <summary>
        /// Optional diagnostic detail. For VersionMismatch, contains the unrecognised
        /// schema_version value as a string (e.g. "99"). Null for ValidationFailed.
        /// </summary>
        public string Payload { get; }

        /// <param name="code">ValidationFailed or VersionMismatch.</param>
        /// <param name="failingField">snake_case JSON field name that failed.</param>
        /// <param name="payload">Optional extra diagnostic string.</param>
        public LdsValidationError(LdsErrorCode code, string failingField, string payload = null)
        {
            Code = code;
            FailingField = failingField;
            Payload = payload;
        }
    }
}
