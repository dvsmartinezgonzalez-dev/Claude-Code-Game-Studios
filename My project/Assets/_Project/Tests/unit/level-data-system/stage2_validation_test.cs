using NUnit.Framework;
using Newtonsoft.Json;
using BoltSort.LevelData;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.LevelData.asmdef
// Covers:    Story 002 ACs — Stage 2 runtime validation (ValidateRecord)

namespace BoltSort.Tests.Unit.LevelData
{
    [TestFixture]
    public class Stage2Validation_Test
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        // Default produces a valid record that passes all Stage 2 checks.
        // difficulty_tier=3 so hint_override=0 is allowed per AC-31.
        private static LevelRecord MakeRecord(
            int schemaVersion = 1,
            string displayName = "\"Test\"",
            int difficultyTier = 3,
            int colorCount = 2,
            int stackDepth = 2,
            string colorStacks = "[[1,2],[2,1]]",
            bool isTutorial = false,
            bool dailyChallengeEligible = true,
            string hintOverride = "null",
            string addedVersion = "\"2026.05\"",
            int parMoves = 4)
        {
            string json = $@"{{
                ""level_id"": 1,
                ""display_name"": {displayName},
                ""difficulty_tier"": {difficultyTier},
                ""schema_version"": {schemaVersion},
                ""color_count"": {colorCount},
                ""stack_depth"": {stackDepth},
                ""color_stacks"": {colorStacks},
                ""temp_slot_count"": 1,
                ""temp_slot_depth"": 1,
                ""is_tutorial"": {isTutorial.ToString().ToLower()},
                ""daily_challenge_eligible"": {dailyChallengeEligible.ToString().ToLower()},
                ""hint_override"": {hintOverride},
                ""added_version"": {addedVersion},
                ""par_moves"": {parMoves}
            }}";
            return JsonConvert.DeserializeObject<LevelRecord>(json);
        }

        private static LevelRecord RecordWithoutParMoves()
        {
            // par_moves key absent → C# int default = 0
            string json = @"{
                ""level_id"": 1, ""display_name"": ""Test"", ""difficulty_tier"": 3,
                ""schema_version"": 1, ""color_count"": 2, ""stack_depth"": 2,
                ""color_stacks"": [[1,2],[2,1]], ""temp_slot_count"": 1, ""temp_slot_depth"": 1,
                ""is_tutorial"": false, ""daily_challenge_eligible"": true,
                ""hint_override"": null, ""added_version"": ""2026.05"" }";
            return JsonConvert.DeserializeObject<LevelRecord>(json);
        }

        // ── Smoke ──────────────────────────────────────────────────────────────

        [Test]
        public void Test_ValidRecord_ReturnsTrue()
        {
            Assert.IsTrue(LevelRecordValidator.ValidateRecord(MakeRecord(), out _));
        }

        // ── ColorStacks null guard ─────────────────────────────────────────────

        [Test]
        public void Test_ColorStacksNull_ReturnsFalse_ColorStacks()
        {
            // color_stacks key absent from JSON → ColorStacks == null after deserialization
            string json = @"{
                ""level_id"": 1, ""display_name"": ""Test"", ""difficulty_tier"": 3,
                ""schema_version"": 1, ""color_count"": 2, ""stack_depth"": 2,
                ""temp_slot_count"": 1, ""temp_slot_depth"": 1, ""is_tutorial"": false,
                ""daily_challenge_eligible"": true, ""hint_override"": null,
                ""added_version"": ""2026.05"", ""par_moves"": 4 }";
            var record = JsonConvert.DeserializeObject<LevelRecord>(json);

            Assert.IsNull(record.ColorStacks, "Precondition: ColorStacks must be null when key is absent");
            bool result = LevelRecordValidator.ValidateRecord(record, out var error);
            Assert.IsFalse(result);
            Assert.AreEqual(LdsErrorCode.ValidationFailed, error.Code);
            Assert.AreEqual("color_stacks", error.FailingField);
        }

        // ── AC-03: Bolt count wrong total ──────────────────────────────────────

        [Test]
        public void Test_BoltCountWrongTotal_ReturnsFalse_ColorStacks()
        {
            // 4×4 board expects 16 bolts; last stack has 3 → total 15
            var record = MakeRecord(colorCount: 4, stackDepth: 4,
                colorStacks: "[[1,2,3,4],[2,1,3,4],[3,2,1,4],[4,3,2]]");

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual(LdsErrorCode.ValidationFailed, error.Code);
            Assert.AreEqual("color_stacks", error.FailingField);
        }

        // ── AC-04: Bolt count correct total, wrong per-color ───────────────────

        [Test]
        public void Test_BoltCountWrongPerColor_ReturnsFalse_ColorStacks()
        {
            // 4×4 board, 16 bolts, but color 1 appears 5× instead of 4×
            var record = MakeRecord(colorCount: 4, stackDepth: 4,
                colorStacks: "[[1,1,1,1],[1,2,3,4],[2,3,4,2],[3,4,3,4]]");

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual(LdsErrorCode.ValidationFailed, error.Code);
            Assert.AreEqual("color_stacks", error.FailingField);
        }

        // ── AC-08: hint_override=0 at difficulty_tier=3 passes ─────────────────

        [Test]
        public void Test_HintOverrideZero_DifficultyTier3_ReturnsTrue()
        {
            var record = MakeRecord(difficultyTier: 3, hintOverride: "0");
            Assert.IsTrue(LevelRecordValidator.ValidateRecord(record, out _));
        }

        // ── AC-09: is_tutorial + daily_challenge_eligible conflict ─────────────

        [Test]
        public void Test_TutorialAndDailyEligible_ReturnsFalse_DailyChallenge()
        {
            var record = MakeRecord(isTutorial: true, dailyChallengeEligible: true);

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual(LdsErrorCode.ValidationFailed, error.Code);
            Assert.AreEqual("daily_challenge_eligible", error.FailingField);
        }

        // ── AC-16: non-contiguous color ID ─────────────────────────────────────

        [Test]
        public void Test_ColorIdOutOfRange_ReturnsFalse_ColorStacks()
        {
            // color_count=3, stack_depth=2; bolt color 4 exceeds color_count
            var record = MakeRecord(colorCount: 3, stackDepth: 2,
                colorStacks: "[[1,2],[2,1],[3,4]]");

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual(LdsErrorCode.ValidationFailed, error.Code);
            Assert.AreEqual("color_stacks", error.FailingField);
        }

        // ── AC-18: par_moves absent from JSON ──────────────────────────────────

        [Test]
        public void Test_ParMovesAbsent_ReturnsFalse_ParMoves()
        {
            var record = RecordWithoutParMoves();

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual(LdsErrorCode.ValidationFailed, error.Code);
            Assert.AreEqual("par_moves", error.FailingField);
        }

        // ── AC-19: par_moves = 0 ───────────────────────────────────────────────

        [Test]
        public void Test_ParMovesZero_ReturnsFalse_ParMoves()
        {
            var record = MakeRecord(parMoves: 0);

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual("par_moves", error.FailingField);
        }

        [Test]
        public void Test_ParMovesOne_ReturnsTrue()
        {
            Assert.IsTrue(LevelRecordValidator.ValidateRecord(MakeRecord(parMoves: 1), out _));
        }

        // ── AC-20: added_version format ────────────────────────────────────────

        [Test]
        public void Test_AddedVersionNonZeroPadded_ReturnsFalse_AddedVersion()
        {
            var record = MakeRecord(addedVersion: "\"2026.1\"");

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual("added_version", error.FailingField);
        }

        [Test]
        public void Test_AddedVersionWrongFormat_ReturnsFalse_AddedVersion()
        {
            Assert.IsFalse(LevelRecordValidator.ValidateRecord(MakeRecord(addedVersion: "\"v1.0\""), out _));
        }

        [Test]
        public void Test_AddedVersionValidZeroPadded_ReturnsTrue()
        {
            Assert.IsTrue(LevelRecordValidator.ValidateRecord(MakeRecord(addedVersion: "\"2026.01\""), out _));
        }

        [Test]
        public void Test_AddedVersionTwoDigitMonth_ReturnsTrue()
        {
            Assert.IsTrue(LevelRecordValidator.ValidateRecord(MakeRecord(addedVersion: "\"2026.12\""), out _));
        }

        // ── AC-21: display_name empty string ───────────────────────────────────

        [Test]
        public void Test_DisplayNameEmpty_ReturnsFalse_DisplayName()
        {
            var record = MakeRecord(displayName: "\"\"");

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual(LdsErrorCode.ValidationFailed, error.Code);
            Assert.AreEqual("display_name", error.FailingField);
        }

        // ── AC-22: hint_override null passes ───────────────────────────────────

        [Test]
        public void Test_HintOverrideNull_ReturnsTrue_NotCoercedToZero()
        {
            var record = MakeRecord(hintOverride: "null");
            Assert.IsNull(record.HintOverride, "Precondition: HintOverride must remain null");
            Assert.IsTrue(LevelRecordValidator.ValidateRecord(record, out _));
        }

        // ── AC-24: unknown schema_version → VersionMismatch ────────────────────

        [Test]
        public void Test_UnknownSchemaVersion_ReturnsVersionMismatch()
        {
            var record = MakeRecord(schemaVersion: 99);

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual(LdsErrorCode.VersionMismatch, error.Code);
            Assert.AreEqual("schema_version", error.FailingField);
            Assert.AreEqual("99", error.Payload);
        }

        // ── AC-29: is_tutorial=true + hint_override=0 ──────────────────────────

        [Test]
        public void Test_TutorialWithHintOverrideZero_ReturnsFalse_HintOverride()
        {
            var record = MakeRecord(isTutorial: true, dailyChallengeEligible: false, hintOverride: "0");

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual("hint_override", error.FailingField);
        }

        // ── AC-30: difficulty_tier<=2 + hint_override=0 ────────────────────────

        [Test]
        public void Test_LowDifficultyWithHintOverrideZero_ReturnsFalse_HintOverride()
        {
            var record = MakeRecord(difficultyTier: 2, hintOverride: "0");

            bool result = LevelRecordValidator.ValidateRecord(record, out var error);

            Assert.IsFalse(result);
            Assert.AreEqual("hint_override", error.FailingField);
        }

        // ── AC-31: difficulty_tier=3 + hint_override=0 passes ──────────────────

        [Test]
        public void Test_DifficultyTier3WithHintOverrideZero_ReturnsTrue()
        {
            Assert.IsTrue(LevelRecordValidator.ValidateRecord(
                MakeRecord(difficultyTier: 3, hintOverride: "0"), out _));
        }
    }
}
