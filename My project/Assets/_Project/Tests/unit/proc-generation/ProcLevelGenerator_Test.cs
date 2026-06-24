using System.Text;
using NUnit.Framework;
using BoltSort.LevelData;

namespace BoltSort.Tests.Unit.ProcGeneration
{
    /// <summary>
    /// Unit tests for <see cref="ProcLevelGenerator"/> (System 1): structural validity, determinism,
    /// flat difficulty envelope, and the ~30% mechanic / ~10% tall-tube variety targets.
    /// </summary>
    public class ProcLevelGenerator_Test
    {
        private static bool HasMechanic(LevelRecord r) =>
            r.MysteryBalls || r.HasMulticolor || (r.FrozenTubes != null && r.FrozenTubes.Length > 0);

        private static bool HasTallTube(LevelRecord r)
        {
            foreach (var stack in r.ColorStacks) if (stack.Length >= 10) return true;
            return false;
        }

        private static string Flatten(LevelRecord r)
        {
            var sb = new StringBuilder();
            sb.Append(r.ColorCount).Append('|').Append(r.StackDepth).Append('|').Append(r.ParMoves).Append('|');
            foreach (var stack in r.ColorStacks) { foreach (var t in stack) sb.Append(t).Append(','); sb.Append(';'); }
            sb.Append(r.MysteryBalls).Append(r.HasMulticolor);
            return sb.ToString();
        }

        // ── AC: level 201+ generates and passes the validator ────────────────────────
        [Test]
        public void test_generate_valid_records_pass_validator()
        {
            foreach (int id in new[] { 201, 250, 333, 777, 1500, 5000, 9999 })
            {
                var rec = ProcLevelGenerator.Generate(id);
                Assert.IsNotNull(rec, $"Generate({id}) returned null");
                Assert.AreEqual(id, rec.LevelId);
                Assert.IsTrue(LevelRecordValidator.ValidateRecord(rec, out var err),
                    $"Generate({id}) failed validation: {err.FailingField}");
                Assert.GreaterOrEqual(rec.ParMoves, 1);
            }
        }

        // ── AC: same level id ⇒ identical level (determinism) ────────────────────────
        [Test]
        public void test_generate_is_deterministic_per_levelid()
        {
            foreach (int id in new[] { 201, 412, 2024, 9001 })
            {
                var a = ProcLevelGenerator.Generate(id);
                var b = ProcLevelGenerator.Generate(id);
                Assert.AreEqual(Flatten(a), Flatten(b), $"Generate({id}) is not deterministic");
            }
        }

        // ── AC: difficulty stays in the curated 170–200 envelope (no escalation) ─────
        [Test]
        public void test_difficulty_stays_within_envelope()
        {
            for (int id = 201; id <= 1000; id++)
            {
                var r = ProcLevelGenerator.Generate(id);
                Assert.That(r.ColorCount, Is.InRange(4, 7), $"color_count out of band at {id}");
                Assert.That(r.TempSlotCount, Is.InRange(1, 3), $"temp_slot_count out of band at {id}");
                // Each color stack starts full; total balls equals the color-stack capacity sum.
                Assert.AreEqual(r.ColorCount, r.ColorStacks.Length, $"stack count mismatch at {id}");
            }
        }

        // ── AC: special mechanics appear in ≥30% of procedural levels ────────────────
        [Test]
        public void test_special_mechanics_appear_in_at_least_30_percent()
        {
            int n = 0, withMechanic = 0;
            for (int id = 201; id <= 1000; id++, n++)
                if (HasMechanic(ProcLevelGenerator.Generate(id))) withMechanic++;
            float ratio = withMechanic / (float)n;
            Assert.GreaterOrEqual(ratio, 0.30f, $"Only {ratio:P1} of levels had a special mechanic");
        }

        // ── AC: tall tube appears in ~10% of procedural levels ───────────────────────
        [Test]
        public void test_tall_tube_appears_near_10_percent()
        {
            int n = 0, tall = 0;
            for (int id = 201; id <= 1000; id++, n++)
                if (HasTallTube(ProcLevelGenerator.Generate(id))) tall++;
            float ratio = tall / (float)n;
            Assert.That(ratio, Is.InRange(0.04f, 0.18f), $"Tall-tube ratio was {ratio:P1}, expected ~10%");
        }

        // ── AC: the tall tube is well-formed (one deep tube, the rest shallow) ───────
        [Test]
        public void test_tall_tube_layout_is_valid()
        {
            bool found = false;
            for (int id = 201; id <= 1000 && !found; id++)
            {
                var r = ProcLevelGenerator.Generate(id);
                if (!HasTallTube(r)) continue;
                found = true;

                int tallCount = 0;
                foreach (var stack in r.ColorStacks)
                {
                    if (stack.Length >= 10) { tallCount++; Assert.IsTrue(stack.Length == 10 || stack.Length == 12, "tall tube must be depth 10 or 12"); }
                    else Assert.That(stack.Length, Is.InRange(3, 4), "non-tall tubes must be shallow (3–4)");
                }
                Assert.AreEqual(1, tallCount, "a tall layout must have exactly one tall tube");
                Assert.IsTrue(LevelRecordValidator.ValidateRecord(r, out _), "tall layout must validate (solvable shape)");
            }
            Assert.IsTrue(found, "no tall-tube level found in 201..1000");
        }
    }
}
