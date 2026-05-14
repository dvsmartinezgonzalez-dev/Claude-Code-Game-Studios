using System;

namespace BoltSort.LevelData
{
    /// <summary>
    /// Mutable filter criteria for LevelDataSystem.GetByFilter().
    /// Null properties are treated as "no constraint" — they match all records.
    /// All set properties must match for Matches() to return true.
    /// </summary>
    public sealed class LevelFilter
    {
        /// <summary>If set, only records with this exact DifficultyTier match.</summary>
        public int? DifficultyTier { get; set; }

        /// <summary>If set, only records matching this DailyChallengeEligible value match.</summary>
        public bool? DailyChallengeEligible { get; set; }

        /// <summary>If set, only records with ColorCount >= this value match.</summary>
        public int? ColorCountMin { get; set; }

        /// <summary>If set, only records with ColorCount <= this value match.</summary>
        public int? ColorCountMax { get; set; }

        /// <summary>
        /// If set, only records whose AddedVersion is lexicographically >= this value match.
        /// Uses StringComparison.Ordinal. Format convention: "YYYY.MM" (zero-padded).
        /// </summary>
        public string AddedVersionMin { get; set; }

        /// <summary>
        /// Returns true if the given LevelRecord satisfies all non-null filter criteria.
        /// A null record argument returns false.
        /// </summary>
        public bool Matches(LevelRecord r)
        {
            if (r == null) return false;
            if (DifficultyTier.HasValue && r.DifficultyTier != DifficultyTier.Value) return false;
            if (DailyChallengeEligible.HasValue && r.DailyChallengeEligible != DailyChallengeEligible.Value) return false;
            if (ColorCountMin.HasValue && r.ColorCount < ColorCountMin.Value) return false;
            if (ColorCountMax.HasValue && r.ColorCount > ColorCountMax.Value) return false;
            // A null AddedVersion (field absent from JSON) is treated as "before any versioned minimum" — exclude.
            if (AddedVersionMin != null && (r.AddedVersion == null ||
                string.Compare(r.AddedVersion, AddedVersionMin, StringComparison.Ordinal) < 0)) return false;
            return true;
        }
    }
}
