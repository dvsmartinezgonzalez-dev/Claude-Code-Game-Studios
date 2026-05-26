using System.Collections.Generic;
using System.IO;
using BoltSort.Editor;
using BoltSort.LevelData;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace BoltSort.Tests.EditMode
{
    [TestFixture]
    public class LevelSolvabilityTest
    {
        private LevelCatalogue _catalogue;

        [OneTimeSetUp]
        public void LoadCatalogue()
        {
            // Load directly from disk to avoid needing a running AssetDatabase
            string path = Path.Combine(Application.dataPath, "Resources/levels.json");
            Assert.IsTrue(File.Exists(path), $"levels.json not found at {path}");
            string json = File.ReadAllText(path);
            _catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(json);
            Assert.IsNotNull(_catalogue, "LevelCatalogue failed to deserialize");
            Assert.IsNotNull(_catalogue.Levels, "LevelCatalogue.Levels is null");
            Assert.IsTrue(_catalogue.Levels.Length > 0, "LevelCatalogue.Levels is empty");
        }

        [Test]
        public void AllLevels_AreSolvable()
        {
            // Arrange
            var failures = new List<string>();

            // Act
            foreach (LevelRecord level in _catalogue.Levels)
            {
                LevelSolver.SolverResult result = LevelSolver.Solve(level);
                if (!result.IsSolvable)
                    failures.Add($"Level {level.LevelId} ({level.DisplayName}): {result.FailReason}");
            }

            // Assert
            Assert.IsEmpty(failures,
                $"Unsolvable levels found:\n{string.Join("\n", failures)}");
        }

        [Test]
        public void AllLevels_SolveWithinStateLimit()
        {
            // Arrange — verifies no level exhausts the BFS state budget
            var timeouts = new List<string>();

            // Act
            foreach (LevelRecord level in _catalogue.Levels)
            {
                LevelSolver.SolverResult result = LevelSolver.Solve(level);
                if (result.FailReason == "timeout")
                    timeouts.Add($"Level {level.LevelId} ({level.DisplayName})");
            }

            // Assert
            Assert.IsEmpty(timeouts,
                $"Levels that timed out (exceeded {LevelSolver.DefaultStateLimit:N0} states):\n" +
                string.Join("\n", timeouts));
        }

        [Test]
        public void AllLevels_HavePositiveMinMoves()
        {
            // Arrange — guard against pre-solved or degenerate levels
            var preSolved = new List<string>();

            // Act
            foreach (LevelRecord level in _catalogue.Levels)
            {
                LevelSolver.SolverResult result = LevelSolver.Solve(level);
                if (result.IsSolvable && result.MinMoves == 0)
                    preSolved.Add($"Level {level.LevelId} ({level.DisplayName})");
            }

            // Assert
            Assert.IsEmpty(preSolved,
                $"Levels that start already solved (pre-solved boards):\n" +
                string.Join("\n", preSolved));
        }
    }
}
