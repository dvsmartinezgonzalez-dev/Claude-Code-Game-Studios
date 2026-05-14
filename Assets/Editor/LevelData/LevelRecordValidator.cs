using BoltSort.LevelData;

namespace BoltSort.Editor.LevelData
{
    /// <summary>
    /// Stage 1 authoring-time validator for LevelRecord entries.
    /// Enforces the column cap (ADR-0013) and par_moves range against the solvability
    /// solver output. Called from the level export step before JSON is committed to the
    /// Addressables group. Editor-only assembly — NOT shipped in player builds.
    /// </summary>
    public static class LevelRecordValidator
    {
        /// <summary>
        /// Validates column cap and par_moves constraints for a single record.
        /// Column cap is evaluated first; a column cap failure short-circuits the par_moves check.
        /// </summary>
        /// <param name="record">The record to validate.</param>
        /// <param name="solverMinMoves">Minimum move count from the authoring-time solvability solver.</param>
        /// <returns>A <see cref="ValidationResult"/> indicating pass or fail with an error message.</returns>
        public static ValidationResult Validate(LevelRecord record, int solverMinMoves)
        {
            // Column cap (ADR-0013): color_count + temp_slot_count <= 8
            int columns = record.ColorCount + record.TempSlotCount;
            if (columns > 8)
                return ValidationResult.Fail(
                    $"Column cap exceeded: {record.ColorCount} color + {record.TempSlotCount} temp " +
                    $"= {columns} > 8. Max allowed: 8 (ADR-0013).");

            if (record.ParMoves < solverMinMoves)
                return ValidationResult.Fail(
                    $"par_moves ({record.ParMoves}) is below solver_min_moves ({solverMinMoves}). " +
                    $"par_moves must be >= {solverMinMoves}.");

            int parMovesMax = solverMinMoves + 10;
            if (record.ParMoves > parMovesMax)
                return ValidationResult.Fail(
                    $"par_moves ({record.ParMoves}) exceeds solver_min_moves + 10 ({parMovesMax}). " +
                    $"par_moves must be <= {parMovesMax}.");

            return ValidationResult.Pass();
        }
    }

    /// <summary>Immutable result of a Stage 1 authoring validation check.</summary>
    public readonly struct ValidationResult
    {
        /// <summary>True if all checks passed.</summary>
        public bool IsValid { get; }

        /// <summary>Human-readable failure description; null on success.</summary>
        public string Error { get; }

        private ValidationResult(bool valid, string error) { IsValid = valid; Error = error; }

        /// <returns>A passing result with <see cref="Error"/> == null.</returns>
        public static ValidationResult Pass() => new ValidationResult(true, null);

        /// <returns>A failing result with the supplied error message.</returns>
        public static ValidationResult Fail(string error) => new ValidationResult(false, error);
    }
}
