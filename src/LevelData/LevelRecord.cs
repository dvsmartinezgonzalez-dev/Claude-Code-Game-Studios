using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace BoltSort.LevelData
{
    /// <summary>
    /// Immutable data record for a single BoltSort puzzle level.
    /// Deserialized from the "levels" array inside levels.json via Newtonsoft.Json.
    /// All fields use [JsonProperty] to map snake_case JSON to PascalCase C# properties.
    ///
    /// IL2CPP risk: HintOverride (nullable int?) and ColorStacks (jagged int[][]) require
    /// Newtonsoft.Json. Verify round-trip on an IL2CPP iOS build before ship. See ADR-0004.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class LevelRecord
    {
        // IL2CPP AOT: forces the compiler to emit code for int[][] instantiation used by Newtonsoft.Json
#pragma warning disable CS0414
        private static readonly int[][] _aotHint = new int[0][];
#pragma warning restore CS0414

        /// <summary>Unique level identifier. Primary key for LevelDataSystem cache.</summary>
        [JsonProperty("level_id")]
        public int LevelId { get; private set; }

        /// <summary>
        /// Human-readable level name. Defaults to "Level {LevelId}" when absent or null in JSON.
        /// An empty string is NOT defaulted here — that is a Stage 2 validation concern (Story 002).
        /// </summary>
        [JsonProperty("display_name")]
        public string DisplayName { get; private set; }

        /// <summary>Difficulty grouping. 1 = easiest.</summary>
        [JsonProperty("difficulty_tier")]
        public int DifficultyTier { get; private set; }

        /// <summary>Schema revision for forward-compatibility checks.</summary>
        [JsonProperty("schema_version")]
        public int SchemaVersion { get; private set; }

        /// <summary>Number of distinct bolt colors in this level.</summary>
        [JsonProperty("color_count")]
        public int ColorCount { get; private set; }

        /// <summary>Maximum bolts per stack (all color stacks share this depth).</summary>
        [JsonProperty("stack_depth")]
        public int StackDepth { get; private set; }

        /// <summary>
        /// Initial board layout. Outer index = stack index; inner = bolt color IDs bottom-to-top.
        /// bolt_count_invariant: sum(ColorStacks[i].Length) == ColorCount * StackDepth.
        ///
        /// IL2CPP risk: jagged int[][] deserialization with Newtonsoft.Json — verify on IL2CPP
        /// iOS build before ship. See ADR-0004 §Verification Required.
        /// </summary>
        [JsonProperty("color_stacks")]
        public int[][] ColorStacks { get; private set; }

        /// <summary>Number of temporary holding slots available to the player.</summary>
        [JsonProperty("temp_slot_count")]
        public int TempSlotCount { get; private set; }

        /// <summary>Maximum bolts each temporary slot can hold.</summary>
        [JsonProperty("temp_slot_depth")]
        public int TempSlotDepth { get; private set; }

        /// <summary>True if this level is part of the onboarding tutorial sequence.</summary>
        [JsonProperty("is_tutorial")]
        public bool IsTutorial { get; private set; }

        /// <summary>True if this level may appear in the Daily Challenge pool.</summary>
        [JsonProperty("daily_challenge_eligible")]
        public bool DailyChallengeEligible { get; private set; }

        /// <summary>
        /// Optional hint count override. null = use system default. 0 = zero hints (explicitly disabled).
        /// Absent JSON key and JSON null both deserialize as C# null.
        ///
        /// IL2CPP risk: nullable int? deserialization — verify on IL2CPP iOS build. See ADR-0004.
        /// </summary>
        [JsonProperty("hint_override")]
        public int? HintOverride { get; private set; }

        /// <summary>Catalogue version string when this level was added (e.g. "2026.05").</summary>
        [JsonProperty("added_version")]
        public string AddedVersion { get; private set; }

        /// <summary>Target move count for 3-star completion.</summary>
        [JsonProperty("par_moves")]
        public int ParMoves { get; private set; }

        /// <summary>
        /// Post-deserialization callback. Sets DisplayName to "Level {LevelId}" when the
        /// JSON field is absent or null. Empty string is intentionally not defaulted.
        /// </summary>
        [OnDeserialized]
        [UnityEngine.Scripting.Preserve]
        internal void OnDeserialized(StreamingContext context)
        {
            if (DisplayName == null)
                DisplayName = $"Level {LevelId}";
        }
    }
}
