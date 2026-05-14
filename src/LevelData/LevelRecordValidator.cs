using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BoltSort.LevelData
{
    /// <summary>
    /// Stage 2 runtime validation for LevelRecord entries loaded from levels.json.
    /// Structural schema checks only — does NOT re-run the authoring-time solvability
    /// solver (that is Stage 1, handled offline by Story 006).
    ///
    /// Called by LevelDataSystem.LoadCatalogueAsync() on every deserialized record.
    /// Records that fail are excluded from _levelCache and counted in failed_record_count.
    /// </summary>
    public static class LevelRecordValidator
    {
        // Known schema versions. Unknown value → VERSION_MISMATCH (not VALIDATION_FAILED).
        private static readonly HashSet<int> KnownSchemaVersions = new HashSet<int> { 1 };

        // "YYYY.MM" — 4-digit year, dot, exactly 2-digit zero-padded month.
        private static readonly Regex AddedVersionFormat =
            new Regex(@"^\d{4}\.\d{2}$", RegexOptions.Compiled);

        /// <summary>
        /// Validates a single LevelRecord against all Stage 2 structural constraints.
        /// Stops at the first violation and populates <paramref name="error"/>.
        /// </summary>
        /// <param name="record">The record to validate. Must not be null.</param>
        /// <param name="error">Set to the validation error on failure; default on success.</param>
        /// <returns>True if valid; false if any constraint fails.</returns>
        public static bool ValidateRecord(LevelRecord record, out LdsValidationError error)
        {
            // 1. schema_version — unknown version may mean other fields have different semantics
            if (!KnownSchemaVersions.Contains(record.SchemaVersion))
            {
                error = new LdsValidationError(
                    LdsErrorCode.VersionMismatch,
                    "schema_version",
                    record.SchemaVersion.ToString());
                return false;
            }

            // 2. display_name — empty string is invalid (null already defaulted by [OnDeserialized])
            if (record.DisplayName == string.Empty)
            {
                error = new LdsValidationError(LdsErrorCode.ValidationFailed, "display_name");
                return false;
            }

            // 3. par_moves — must be >= 1; 0 is the C# int default when JSON key is absent
            if (record.ParMoves <= 0)
            {
                error = new LdsValidationError(LdsErrorCode.ValidationFailed, "par_moves");
                return false;
            }

            // 4. added_version — must match "YYYY.MM"
            if (record.AddedVersion == null || !AddedVersionFormat.IsMatch(record.AddedVersion))
            {
                error = new LdsValidationError(LdsErrorCode.ValidationFailed, "added_version");
                return false;
            }

            // 5. hint_override = 0 guards (is_tutorial checked first)
            if (record.HintOverride == 0)
            {
                if (record.IsTutorial)
                {
                    error = new LdsValidationError(LdsErrorCode.ValidationFailed, "hint_override");
                    return false;
                }
                if (record.DifficultyTier <= 2)
                {
                    error = new LdsValidationError(LdsErrorCode.ValidationFailed, "hint_override");
                    return false;
                }
            }

            // 6. Tutorial/daily conflict
            if (record.IsTutorial && record.DailyChallengeEligible)
            {
                error = new LdsValidationError(LdsErrorCode.ValidationFailed, "daily_challenge_eligible");
                return false;
            }

            // 7. Bolt count Check 1: null guard then total bolt count invariant
            if (record.ColorStacks == null)
            {
                error = new LdsValidationError(LdsErrorCode.ValidationFailed, "color_stacks");
                return false;
            }

            int total = 0;
            foreach (var stack in record.ColorStacks)
                total += stack.Length;
            if (total != record.ColorCount * record.StackDepth)
            {
                error = new LdsValidationError(LdsErrorCode.ValidationFailed, "color_stacks");
                return false;
            }

            // 8. Bolt count Check 2: valid color ID range [1, ColorCount] + per-color frequency
            var colorCounts = new int[record.ColorCount + 1]; // index 0 unused
            foreach (var stack in record.ColorStacks)
            {
                foreach (var colorId in stack)
                {
                    if (colorId < 1 || colorId > record.ColorCount)
                    {
                        error = new LdsValidationError(LdsErrorCode.ValidationFailed, "color_stacks");
                        return false;
                    }
                    colorCounts[colorId]++;
                }
            }
            for (int c = 1; c <= record.ColorCount; c++)
            {
                if (colorCounts[c] != record.StackDepth)
                {
                    error = new LdsValidationError(LdsErrorCode.ValidationFailed, "color_stacks");
                    return false;
                }
            }

            error = default;
            return true;
        }
    }
}
