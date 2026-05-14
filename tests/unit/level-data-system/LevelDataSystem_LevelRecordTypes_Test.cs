using NUnit.Framework;
using Newtonsoft.Json;
using BoltSort.LevelData;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.LevelData.asmdef (see adjacent .asmdef file)
// Covers:    Story 001 AC-types-1..4, enums, struct, LevelFilter.Matches()

namespace BoltSort.Tests.Unit.LevelData
{
    [TestFixture]
    public class LevelDataSystem_LevelRecordTypes_Test
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static string FullJson(
            int levelId = 1,
            string displayName = "\"Test Level\"",
            string hintOverride = "null",
            string colorStacks = "[[1,2],[2,1]]")
        {
            return $@"{{
                ""level_id"": {levelId},
                ""display_name"": {displayName},
                ""difficulty_tier"": 1,
                ""schema_version"": 1,
                ""color_count"": 2,
                ""stack_depth"": 2,
                ""color_stacks"": {colorStacks},
                ""temp_slot_count"": 1,
                ""temp_slot_depth"": 1,
                ""is_tutorial"": false,
                ""daily_challenge_eligible"": true,
                ""hint_override"": {hintOverride},
                ""added_version"": ""2026.05"",
                ""par_moves"": 4
            }}";
        }

        private static string JsonWithoutKey(string omittedKey)
        {
            // Full record JSON except for the specified key — used for absent-key tests
            return $@"{{
                ""level_id"": 42,
                {(omittedKey == "display_name" ? "" : @"""display_name"": ""Test"",")}
                ""difficulty_tier"": 1,
                ""schema_version"": 1,
                ""color_count"": 2,
                ""stack_depth"": 2,
                ""color_stacks"": [[1,2],[2,1]],
                ""temp_slot_count"": 1,
                ""temp_slot_depth"": 1,
                ""is_tutorial"": false,
                ""daily_challenge_eligible"": true,
                {(omittedKey == "hint_override" ? "" : @"""hint_override"": null,")}
                ""added_version"": ""2026.05"",
                ""par_moves"": 4
            }}";
        }

        private static LevelRecord Deserialize(string json)
            => JsonConvert.DeserializeObject<LevelRecord>(json);

        private static LevelRecord MakeRecord(
            int levelId = 1,
            int difficultyTier = 2,
            bool dailyChallengeEligible = true,
            int colorCount = 3,
            string addedVersion = "2026.05")
        {
            return Deserialize($@"{{
                ""level_id"": {levelId},
                ""display_name"": ""Test"",
                ""difficulty_tier"": {difficultyTier},
                ""schema_version"": 1,
                ""color_count"": {colorCount},
                ""stack_depth"": 4,
                ""color_stacks"": [[1,2,3,4],[2,1,3,4],[3,2,1,4]],
                ""temp_slot_count"": 1,
                ""temp_slot_depth"": 1,
                ""is_tutorial"": false,
                ""daily_challenge_eligible"": {dailyChallengeEligible.ToString().ToLower()},
                ""hint_override"": null,
                ""added_version"": ""{addedVersion}"",
                ""par_moves"": 8
            }}");
        }

        // ── AC-types-1: HintOverride nullable round-trip ───────────────────────

        [Test]
        public void Test_HintOverrideNull_DeserializesAsNull()
        {
            LevelRecord record = Deserialize(FullJson(hintOverride: "null"));
            Assert.IsNull(record.HintOverride, "JSON null must deserialize as C# null");
        }

        [Test]
        public void Test_HintOverrideZero_DeserializesAsZeroNotNull()
        {
            LevelRecord record = Deserialize(FullJson(hintOverride: "0"));
            Assert.IsNotNull(record.HintOverride, "JSON 0 must not deserialize as null");
            Assert.AreEqual(0, record.HintOverride.Value);
        }

        [Test]
        public void Test_HintOverrideAbsent_DeserializesAsNull()
        {
            LevelRecord record = Deserialize(JsonWithoutKey("hint_override"));
            Assert.IsNull(record.HintOverride, "Absent key must deserialize as C# null");
        }

        [Test]
        public void Test_HintOverrideNegativeOne_DeserializesAsNegativeOne()
        {
            LevelRecord record = Deserialize(FullJson(hintOverride: "-1"));
            Assert.IsNotNull(record.HintOverride);
            Assert.AreEqual(-1, record.HintOverride.Value);
        }

        // ── AC-types-2: ColorStacks jagged array ──────────────────────────────

        [Test]
        public void Test_ColorStacks_ThreeInnerArrays_DeserializeCorrectly()
        {
            string json = @"{
                ""level_id"": 1, ""display_name"": ""T"", ""difficulty_tier"": 1,
                ""schema_version"": 1, ""color_count"": 3, ""stack_depth"": 3,
                ""color_stacks"": [[1,2,3],[2,1,3],[3,2,1]],
                ""temp_slot_count"": 1, ""temp_slot_depth"": 1, ""is_tutorial"": false,
                ""daily_challenge_eligible"": true, ""hint_override"": null,
                ""added_version"": ""2026.05"", ""par_moves"": 6 }";
            LevelRecord record = Deserialize(json);

            Assert.AreEqual(3, record.ColorStacks.Length);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, record.ColorStacks[0]);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, record.ColorStacks[2]);
        }

        [Test]
        public void Test_ColorStacks_EmptyInnerArray_DeserializesWithLengthZero()
        {
            string json = @"{
                ""level_id"": 1, ""display_name"": ""T"", ""difficulty_tier"": 1,
                ""schema_version"": 1, ""color_count"": 1, ""stack_depth"": 3,
                ""color_stacks"": [[],[1,2,3]],
                ""temp_slot_count"": 1, ""temp_slot_depth"": 1, ""is_tutorial"": false,
                ""daily_challenge_eligible"": true, ""hint_override"": null,
                ""added_version"": ""2026.05"", ""par_moves"": 3 }";
            LevelRecord record = Deserialize(json);

            Assert.AreEqual(0, record.ColorStacks[0].Length);
        }

        // ── AC-types-3: DisplayName defaulting ────────────────────────────────

        [Test]
        public void Test_DisplayNameAbsent_DefaultsToLevelId()
        {
            LevelRecord record = Deserialize(JsonWithoutKey("display_name"));
            Assert.AreEqual("Level 42", record.DisplayName);
        }

        [Test]
        public void Test_DisplayNameJsonNull_DefaultsToLevelId()
        {
            LevelRecord record = Deserialize(FullJson(levelId: 42, displayName: "null"));
            Assert.AreEqual("Level 42", record.DisplayName);
        }

        [Test]
        public void Test_DisplayNameEmptyString_NotDefaulted()
        {
            // Empty string is a Stage 2 validation concern (Story 002), not defaulted here
            LevelRecord record = Deserialize(FullJson(levelId: 1, displayName: "\"\""));
            Assert.AreEqual(string.Empty, record.DisplayName);
        }

        [Test]
        public void Test_DisplayNameProvided_NotOverwritten()
        {
            LevelRecord record = Deserialize(FullJson(levelId: 7, displayName: "\"Tutorial Start\""));
            Assert.AreEqual("Tutorial Start", record.DisplayName);
        }

        // ── AC-types-4: CatalogueVersion defaults to 0 when absent ────────────

        [Test]
        public void Test_CatalogueVersionAbsent_DefaultsToZero()
        {
            string json = @"{ ""levels"": [] }";
            LevelCatalogue catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(json);
            Assert.AreEqual(0, catalogue.CatalogueVersion);
        }

        [Test]
        public void Test_CatalogueVersionPresent_DeserializesCorrectly()
        {
            string json = @"{ ""catalogue_version"": 7, ""levels"": [] }";
            LevelCatalogue catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(json);
            Assert.AreEqual(7, catalogue.CatalogueVersion);
        }

        // ── LdsState enum values ───────────────────────────────────────────────

        [Test]
        public void Test_LdsState_AllExpectedValuesExist()
        {
            Assert.AreEqual(0, (int)LdsState.Uninitialized);
            Assert.AreEqual(1, (int)LdsState.Loading);
            Assert.AreEqual(2, (int)LdsState.Ready);
            Assert.AreEqual(3, (int)LdsState.Degraded);
        }

        // ── LdsErrorCode enum values ───────────────────────────────────────────

        [Test]
        public void Test_LdsErrorCode_AllExpectedValuesExist()
        {
            Assert.AreEqual(0, (int)LdsErrorCode.NotFound);
            Assert.AreEqual(1, (int)LdsErrorCode.ValidationFailed);
            Assert.AreEqual(2, (int)LdsErrorCode.VersionMismatch);
            Assert.AreEqual(3, (int)LdsErrorCode.SystemNotReady);
        }

        // ── SystemReadiness struct ─────────────────────────────────────────────

        [Test]
        public void Test_SystemReadiness_Constructor_SetsAllFields()
        {
            var r = new SystemReadiness(true, 10, 2, 5, null);
            Assert.IsTrue(r.Ready);
            Assert.AreEqual(10, r.LoadedCount);
            Assert.AreEqual(2, r.SkippedCount);
            Assert.AreEqual(5, r.CatalogueVersion);
            Assert.IsNull(r.DiagnosticCode);
        }

        [Test]
        public void Test_SystemReadiness_Degraded_HasDiagnosticCode()
        {
            var r = new SystemReadiness(false, 0, 5, 0, "CATALOGUE_LOAD_FAILED");
            Assert.IsFalse(r.Ready);
            Assert.AreEqual("CATALOGUE_LOAD_FAILED", r.DiagnosticCode);
        }

        // ── LevelFilter.Matches ────────────────────────────────────────────────

        [Test]
        public void Test_LevelFilter_NoConstraints_MatchesAnyRecord()
        {
            var filter = new LevelFilter();
            Assert.IsTrue(filter.Matches(MakeRecord()));
        }

        [Test]
        public void Test_LevelFilter_DifficultyTierMatch_ReturnsTrue()
        {
            var filter = new LevelFilter { DifficultyTier = 2 };
            Assert.IsTrue(filter.Matches(MakeRecord(difficultyTier: 2)));
        }

        [Test]
        public void Test_LevelFilter_DifficultyTierMismatch_ReturnsFalse()
        {
            var filter = new LevelFilter { DifficultyTier = 3 };
            Assert.IsFalse(filter.Matches(MakeRecord(difficultyTier: 2)));
        }

        [Test]
        public void Test_LevelFilter_DailyChallengeEligible_Match_ReturnsTrue()
        {
            var filter = new LevelFilter { DailyChallengeEligible = true };
            Assert.IsTrue(filter.Matches(MakeRecord(dailyChallengeEligible: true)));
        }

        [Test]
        public void Test_LevelFilter_DailyChallengeEligible_Mismatch_ReturnsFalse()
        {
            var filter = new LevelFilter { DailyChallengeEligible = true };
            Assert.IsFalse(filter.Matches(MakeRecord(dailyChallengeEligible: false)));
        }

        [Test]
        public void Test_LevelFilter_ColorCountMin_FiltersCorrectly()
        {
            var filter = new LevelFilter { ColorCountMin = 4 };
            Assert.IsFalse(filter.Matches(MakeRecord(colorCount: 3)), "below min");
            Assert.IsTrue(filter.Matches(MakeRecord(colorCount: 4)),  "at min");
            Assert.IsTrue(filter.Matches(MakeRecord(colorCount: 5)),  "above min");
        }

        [Test]
        public void Test_LevelFilter_ColorCountMax_FiltersCorrectly()
        {
            var filter = new LevelFilter { ColorCountMax = 4 };
            Assert.IsTrue(filter.Matches(MakeRecord(colorCount: 3)),  "below max");
            Assert.IsTrue(filter.Matches(MakeRecord(colorCount: 4)),  "at max");
            Assert.IsFalse(filter.Matches(MakeRecord(colorCount: 5)), "above max");
        }

        [Test]
        public void Test_LevelFilter_AddedVersionMin_LexicographicComparison()
        {
            var filter = new LevelFilter { AddedVersionMin = "2026.05" };
            Assert.IsFalse(filter.Matches(MakeRecord(addedVersion: "2026.04")), "before");
            Assert.IsTrue(filter.Matches(MakeRecord(addedVersion: "2026.05")),  "exact");
            Assert.IsTrue(filter.Matches(MakeRecord(addedVersion: "2026.06")),  "after");
        }

        [Test]
        public void Test_LevelFilter_NullRecord_ReturnsFalse()
        {
            Assert.IsFalse(new LevelFilter().Matches(null));
        }

        [Test]
        public void Test_LevelFilter_MultipleConstraints_AllMustMatch()
        {
            var filter = new LevelFilter
            {
                DifficultyTier = 2,
                DailyChallengeEligible = true,
                ColorCountMin = 3,
                ColorCountMax = 5
            };
            Assert.IsTrue(filter.Matches(MakeRecord(difficultyTier: 2, dailyChallengeEligible: true, colorCount: 4)));
            Assert.IsFalse(filter.Matches(MakeRecord(difficultyTier: 1, dailyChallengeEligible: true, colorCount: 4)), "tier fails");
        }

        // ── IL2CPP AOT regression guards ──────────────────────────────────────

        [Test]
        public void Test_Aot_NullableInt_DeserializesNull()
        {
            // Guards against IL2CPP stripping Newtonsoft.Json NullableConverter
            var r = JsonConvert.DeserializeObject<LevelRecord>(FullJson(hintOverride: "null"));
            Assert.IsNull(r.HintOverride);
        }

        [Test]
        public void Test_LevelFilter_AddedVersionMin_NullRecordVersion_ExcludesRecord()
        {
            // Policy: a record whose added_version is absent from JSON (null) is treated as
            // "before any versioned minimum" and excluded. See LevelFilter.Matches() null guard.
            var filter = new LevelFilter { AddedVersionMin = "2026.05" };
            string json = @"{
                ""level_id"": 1, ""display_name"": ""T"", ""difficulty_tier"": 1,
                ""schema_version"": 1, ""color_count"": 2, ""stack_depth"": 2,
                ""color_stacks"": [[1,2],[2,1]], ""temp_slot_count"": 1, ""temp_slot_depth"": 1,
                ""is_tutorial"": false, ""daily_challenge_eligible"": true,
                ""hint_override"": null, ""par_moves"": 4 }";
            LevelRecord record = Deserialize(json);
            Assert.IsNull(record.AddedVersion, "Precondition: AddedVersion must be null when absent from JSON");
            Assert.IsFalse(filter.Matches(record), "Null AddedVersion must be excluded by AddedVersionMin filter");
        }

        [Test]
        public void Test_Aot_JaggedIntArray_CanBeInstantiated()
        {
            // NOTE: This test verifies plain C# int[][] allocation, not Newtonsoft.Json
            // deserialization. IL2CPP stripping of JaggedArrayConverter must be verified
            // on-device per ADR-0004 §Verification Required — this stub does not substitute.
            var arr = new int[2][];
            arr[0] = new[] { 0, 1 };
            arr[1] = new[] { 2, 3 };
            Assert.AreEqual(2, arr.Length);
            Assert.AreEqual(3, arr[1][1]);
        }
    }
}
