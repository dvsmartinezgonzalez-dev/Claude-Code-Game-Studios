using System.Collections.Generic;
using System.IO;
using BoltSort.Editor;
using BoltSort.LevelData;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace BoltSort.Tests.EditMode
{
    /// <summary>
    /// Catalogue-integrity gate. Complements <see cref="LevelSolvabilityTest"/>
    /// (which proves every level is solvable) by guarding the two failures that let
    /// the original catalogue ship 30 levels that were really only 7 puzzles with
    /// broken star targets:
    ///   * structural duplication — the same puzzle under color relabel / tube reorder
    ///   * impossible 3-star targets — par_moves below the optimal solution length
    /// These run on the shipped Resources/levels.json so a regression fails CI.
    /// </summary>
    [TestFixture]
    public class CatalogueIntegrityTest
    {
        private const int ParSlackMax = 10; // authoring rule: par ∈ [optimal, optimal+10]

        private LevelCatalogue _catalogue;

        [OneTimeSetUp]
        public void LoadCatalogue()
        {
            string path = Path.Combine(Application.dataPath, "Resources/levels.json");
            Assert.IsTrue(File.Exists(path), $"levels.json not found at {path}");
            string json = File.ReadAllText(path);
            _catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(json);
            Assert.IsNotNull(_catalogue, "LevelCatalogue failed to deserialize");
            Assert.IsNotNull(_catalogue.Levels, "LevelCatalogue.Levels is null");
            Assert.IsTrue(_catalogue.Levels.Length > 0, "LevelCatalogue.Levels is empty");
        }

        [Test]
        public void AllLevels_HaveUniqueIds()
        {
            // Arrange
            var seen = new HashSet<int>();
            var dupes = new List<int>();

            // Act
            foreach (LevelRecord level in _catalogue.Levels)
                if (!seen.Add(level.LevelId))
                    dupes.Add(level.LevelId);

            // Assert
            Assert.IsEmpty(dupes, $"Duplicate level_id(s): {string.Join(", ", dupes)}");
        }

        [Test]
        public void AllLevels_AreNotExactDuplicates()
        {
            // Arrange
            var byBoard = new Dictionary<string, int>();
            var failures = new List<string>();

            // Act
            foreach (LevelRecord level in _catalogue.Levels)
            {
                string key = ExactBoardKey(level);
                if (byBoard.TryGetValue(key, out int firstId))
                    failures.Add($"Level {level.LevelId} is an EXACT duplicate of Level {firstId}");
                else
                    byBoard[key] = level.LevelId;
            }

            // Assert
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void AllLevels_AreNotStructurallyEquivalent()
        {
            // Arrange — equal canonical signature ⇒ same puzzle under color relabel / tube reorder
            var bySignature = new Dictionary<string, int>();
            var failures = new List<string>();

            // Act
            foreach (LevelRecord level in _catalogue.Levels)
            {
                string sig = LevelEquivalence.CanonicalSignature(level);
                if (bySignature.TryGetValue(sig, out int firstId))
                    failures.Add(
                        $"Level {level.LevelId} is structurally EQUIVALENT to Level {firstId} " +
                        "(same puzzle under color relabel / tube reorder)");
                else
                    bySignature[sig] = level.LevelId;
            }

            // Assert
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void AllLevels_ParMoves_Are3StarAchievable()
        {
            // Arrange — 3 stars requires move_count <= par_moves, so par must be >= optimal
            var failures = new List<string>();

            // Act
            foreach (LevelRecord level in _catalogue.Levels)
            {
                LevelSolver.SolverResult res = LevelSolver.Solve(level);
                if (!res.IsSolvable) continue; // solvability is covered by LevelSolvabilityTest
                if (level.ParMoves < res.MinMoves)
                    failures.Add(
                        $"Level {level.LevelId}: par_moves ({level.ParMoves}) < optimal " +
                        $"({res.MinMoves}) — 3 stars is impossible");
            }

            // Assert
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void AllLevels_ParMoves_WithinAuthoringSlack()
        {
            // Arrange — par must not be wildly generous either (ADR authoring rule)
            var failures = new List<string>();

            // Act
            foreach (LevelRecord level in _catalogue.Levels)
            {
                LevelSolver.SolverResult res = LevelSolver.Solve(level);
                if (!res.IsSolvable) continue;
                if (level.ParMoves > res.MinMoves + ParSlackMax)
                    failures.Add(
                        $"Level {level.LevelId}: par_moves ({level.ParMoves}) > optimal+{ParSlackMax} " +
                        $"({res.MinMoves + ParSlackMax})");
            }

            // Assert
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private static string ExactBoardKey(LevelRecord level)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(level.ColorCount).Append('|')
              .Append(level.StackDepth).Append('|')
              .Append(level.TempSlotCount).Append('|')
              .Append(level.TempSlotDepth).Append("||");
            foreach (var stack in level.ColorStacks)
            {
                sb.Append('[');
                if (stack != null)
                    for (int j = 0; j < stack.Length; j++)
                    {
                        if (j > 0) sb.Append(',');
                        sb.Append(stack[j]);
                    }
                sb.Append(']');
            }
            return sb.ToString();
        }
    }
}
