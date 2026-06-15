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

        // ── Phase-2 mechanics (schema_version 2 only; null/false ⇒ classic level) ──

        /// <summary>
        /// Optional per-column capacity override (flat order: color stacks then temp slots).
        /// When present, overrides <see cref="StackDepth"/>/<see cref="TempSlotDepth"/> per tube
        /// (asymmetric / large-capacity tubes). Null ⇒ uniform depths. See ADR-0014, phase-2 TDD §1.4/§1.6.
        /// </summary>
        [JsonProperty("tube_capacities")]
        public int[] TubeCapacities { get; private set; }

        /// <summary>
        /// Optional frozen-tube descriptors. Each entry locks a tube against deposits for
        /// <c>turns</c> committed moves (removals stay legal). Null/empty ⇒ no frozen tubes.
        /// Authoring rule: max 1 per level. See phase-2 TDD §1.3.
        /// </summary>
        [JsonProperty("frozen_tubes")]
        public FrozenTube[] FrozenTubes { get; private set; }

        /// <summary>
        /// Authoring/tooling flag: true if any ball in <see cref="ColorStacks"/> is a mystery ball
        /// (encoded as a negative color id, hidden color = abs). Derivable; kept for fast UI/dedup checks.
        /// </summary>
        [JsonProperty("mystery_balls")]
        public bool MysteryBalls { get; private set; }

        /// <summary>
        /// Authoring/tooling flag: true if the level contains the single multicolor wildcard ball
        /// (encoded as color id 0). Derivable; kept for fast UI/dedup checks. Max 1 per level.
        /// </summary>
        [JsonProperty("has_multicolor")]
        public bool HasMulticolor { get; private set; }

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

    /// <summary>
    /// One frozen-tube descriptor inside a <see cref="LevelRecord"/> (schema_version 2).
    /// Deposits into <see cref="TubeIndex"/> are rejected until <see cref="Turns"/> committed
    /// moves have elapsed; the counter is visible to the player from move 0. See phase-2 TDD §1.3.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class FrozenTube
    {
        /// <summary>Flat column index (color stacks first, then temp slots).</summary>
        [JsonProperty("tube_index")]
        public int TubeIndex { get; private set; }

        /// <summary>Number of committed moves the tube stays deposit-locked.</summary>
        [JsonProperty("turns")]
        public int Turns { get; private set; }
    }
}
