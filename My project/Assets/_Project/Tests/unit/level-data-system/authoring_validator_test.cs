using NUnit.Framework;
using Newtonsoft.Json;
using BoltSort.LevelData;
using EditorValidator = BoltSort.Editor.LevelData.LevelRecordValidator;

// Framework: Unity Test Framework (NUnit, EditMode)
// Assembly:  Tests.Unit.LevelData.asmdef
// Covers:    Story 006 ACs — Authoring Pipeline Validator
//
// AC-6 (assembly exclusion): structural property enforced by Unity's Editor/ folder
// convention — not assertable at runtime; verify by inspecting player build output.

namespace BoltSort.Tests.Unit.LevelData
{
    [TestFixture]
    public class AuthoringValidator_Test
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        // Constructs a LevelRecord via JSON deserialization with the fields under test.
        // color_stacks is intentionally minimal — Stage 1 does not check bolt-count invariants.
        private static LevelRecord MakeRecord(int colorCount, int tempSlotCount, int parMoves)
        {
            var json = $@"{{
                ""level_id"": 1, ""display_name"": ""Test"", ""difficulty_tier"": 3,
                ""schema_version"": 1, ""color_count"": {colorCount}, ""stack_depth"": 2,
                ""color_stacks"": [[1,2],[2,1]], ""temp_slot_count"": {tempSlotCount},
                ""temp_slot_depth"": 1, ""is_tutorial"": false,
                ""daily_challenge_eligible"": true, ""hint_override"": null,
                ""added_version"": ""2026.05"", ""par_moves"": {parMoves}
            }}";
            return JsonConvert.DeserializeObject<LevelRecord>(json);
        }

        // ── AC-32: par_moves below solver_min_moves → validation fails ─────────

        [Test]
        public void Test_Validate_ParMovesBelowSolverMin_ReturnsFailWithParMovesMessage()
        {
            // Arrange: par_moves=6 < solverMinMoves=8
            var record = MakeRecord(colorCount: 2, tempSlotCount: 1, parMoves: 6);

            var result = EditorValidator.Validate(record, solverMinMoves: 8);

            Assert.IsFalse(result.IsValid, "par_moves below solver minimum must fail");
            StringAssert.Contains("par_moves", result.Error);
            StringAssert.Contains("solver_min_moves", result.Error);
        }

        // ── AC-33: par_moves above solver_min_moves + 10 → validation fails ───

        [Test]
        public void Test_Validate_ParMovesAboveSolverMax_ReturnsFailWithExceedsMessage()
        {
            // Arrange: par_moves=19 > solverMinMoves(8)+10=18
            var record = MakeRecord(colorCount: 2, tempSlotCount: 1, parMoves: 19);

            var result = EditorValidator.Validate(record, solverMinMoves: 8);

            Assert.IsFalse(result.IsValid, "par_moves above solver max must fail");
            StringAssert.Contains("par_moves", result.Error);
            StringAssert.Contains("solver_min_moves + 10", result.Error);
        }

        // ── AC-34: par_moves within valid range → passes ──────────────────────

        [Test]
        public void Test_Validate_ParMovesInRange_ReturnsPass()
        {
            // Arrange: par_moves=12, valid range [8, 18]
            var record = MakeRecord(colorCount: 2, tempSlotCount: 1, parMoves: 12);

            var result = EditorValidator.Validate(record, solverMinMoves: 8);

            Assert.IsTrue(result.IsValid, "par_moves within range must pass");
            Assert.IsNull(result.Error);
        }

        // ── AC-34 boundary: lower bound is inclusive ───────────────────────────

        [Test]
        public void Test_Validate_ParMovesEqualsMin_ReturnsPass()
        {
            // Arrange: par_moves=8 == solverMinMoves=8
            var record = MakeRecord(colorCount: 2, tempSlotCount: 1, parMoves: 8);

            var result = EditorValidator.Validate(record, solverMinMoves: 8);

            Assert.IsTrue(result.IsValid, "par_moves == solver_min_moves (lower bound inclusive) must pass");
        }

        // ── AC-34 boundary: upper bound is inclusive ───────────────────────────

        [Test]
        public void Test_Validate_ParMovesEqualsMax_ReturnsPass()
        {
            // Arrange: par_moves=18 == solverMinMoves(8)+10=18
            var record = MakeRecord(colorCount: 2, tempSlotCount: 1, parMoves: 18);

            var result = EditorValidator.Validate(record, solverMinMoves: 8);

            Assert.IsTrue(result.IsValid, "par_moves == solver_min_moves+10 (upper bound inclusive) must pass");
        }

        // ── Column cap fail: color_count=6, temp_slot_count=3 (9 columns > 8) ─

        [Test]
        public void Test_Validate_ColumnCapExceeded_ReturnsFailWithColumnCapMessage()
        {
            // Arrange: 6 color + 3 temp = 9 columns
            var record = MakeRecord(colorCount: 6, tempSlotCount: 3, parMoves: 12);

            var result = EditorValidator.Validate(record, solverMinMoves: 8);

            Assert.IsFalse(result.IsValid, "9 columns must fail the column cap check");
            StringAssert.Contains("Column cap exceeded", result.Error);
        }

        // ── Column cap pass: color_count=6, temp_slot_count=2 (8 columns == cap) ─

        [Test]
        public void Test_Validate_ColumnCapAtMaximum_PassesCapCheck()
        {
            // Arrange: 6 color + 2 temp = 8 columns (exactly at cap)
            var record = MakeRecord(colorCount: 6, tempSlotCount: 2, parMoves: 12);

            var result = EditorValidator.Validate(record, solverMinMoves: 8);

            Assert.IsTrue(result.IsValid, "Exactly 8 columns must pass the column cap check");
        }

        // ── Column cap evaluated before par_moves (ordering guarantee) ─────────

        [Test]
        public void Test_Validate_ColumnCapFailPrecludesParMovesCheck()
        {
            // Arrange: 9 columns AND par_moves below minimum — column cap error takes precedence
            var record = MakeRecord(colorCount: 6, tempSlotCount: 3, parMoves: 6);

            var result = EditorValidator.Validate(record, solverMinMoves: 8);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("Column cap exceeded", result.Error,
                "Column cap error must be reported when both constraints fail");
            StringAssert.DoesNotContain("par_moves", result.Error,
                "par_moves must not appear when column cap is evaluated first");
        }
    }
}
