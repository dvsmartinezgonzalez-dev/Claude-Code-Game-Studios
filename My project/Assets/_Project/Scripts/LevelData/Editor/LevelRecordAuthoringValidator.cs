using BoltSort.LevelData;

namespace BoltSort.Editor.LevelData
{
    /// <summary>
    /// Authoring-pipeline validator for <see cref="LevelRecord"/>.
    /// Runs checks that are only meaningful at design/import time:
    /// column-cap compliance (ADR-0013) and par_moves plausibility against
    /// a solver-provided minimum move count.
    /// Distinct from the runtime <see cref="BoltSort.LevelData.LevelRecordValidator"/>
    /// which validates structural integrity at LDS load time.
    /// </summary>
    public static class LevelRecordValidator
    {
        private const int MaxColumnCount = 8; // ADR-0013

        /// <summary>
        /// Validates a <see cref="LevelRecord"/> against authoring constraints.
        /// Column cap is evaluated before par_moves so the first reported error
        /// is always the most fundamental layout violation.
        /// </summary>
        /// <param name="record">The record to validate.</param>
        /// <param name="solverMinMoves">
        /// Minimum move count produced by the level solver for this layout.
        /// par_moves must fall in [solverMinMoves, solverMinMoves + 10].
        /// </param>
        public static ValidationResult Validate(LevelRecord record, int solverMinMoves)
        {
            int totalColumns = record.ColorCount + record.TempSlotCount;
            if (totalColumns > MaxColumnCount)
                return new ValidationResult(false,
                    $"Column cap exceeded: {totalColumns} columns (max {MaxColumnCount}, ADR-0013).");

            if (record.ParMoves < solverMinMoves)
                return new ValidationResult(false,
                    $"par_moves ({record.ParMoves}) is below solver_min_moves ({solverMinMoves}).");

            if (record.ParMoves > solverMinMoves + 10)
                return new ValidationResult(false,
                    $"par_moves ({record.ParMoves}) exceeds solver_min_moves + 10 ({solverMinMoves + 10}).");

            return new ValidationResult(true, null);
        }
    }

    /// <summary>Result of an authoring-pipeline validation check.</summary>
    public sealed class ValidationResult
    {
        /// <summary>True when all authoring checks pass.</summary>
        public bool IsValid { get; }

        /// <summary>Human-readable failure reason, or null when <see cref="IsValid"/> is true.</summary>
        public string Error { get; }

        internal ValidationResult(bool isValid, string error)
        {
            IsValid = isValid;
            Error   = error;
        }
    }
}
