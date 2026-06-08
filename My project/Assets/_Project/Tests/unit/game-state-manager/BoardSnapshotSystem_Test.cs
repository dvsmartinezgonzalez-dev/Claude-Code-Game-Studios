using NUnit.Framework;
using UnityEngine;
using BoltSort.GameStateManager;

// Framework: Unity Test Framework (NUnit, EditMode)
// Covers:    Story 008 — BoardSnapshotSystem round-trip, single-use consumption, corruption handling
// Design:    design/gdd/game-state-manager.md (SER-01/02/03)

namespace BoltSort.Tests.Unit.GameStateManager
{
    [TestFixture]
    public class BoardSnapshotSystem_Test
    {
        private const string SnapshotKey = "bs.board_snapshot";
        private BoardSnapshotSystem _system;

        [SetUp]
        public void SetUp()
        {
            _system = new BoardSnapshotSystem();
        }

        [TearDown]
        public void TearDown()
        {
            // Prevent leakage across test runs (matches SaveSystem_PlayerPrefs_Test convention).
            PlayerPrefs.DeleteKey(SnapshotKey);
        }

        private static BoardSnapshot MakeSnapshot()
        {
            return new BoardSnapshot
            {
                StackContents     = new[] { new[] { 1, 2 }, new[] { 2, 1 } },
                TempSlotContents  = new int[0][],
                StackDepth        = 2,
                TempSlotDepth     = 1,
                TempSlotCount     = 0,
                ColorCount        = 2,
                MoveCount         = 3,
                SequenceId        = 7L,
                LevelId           = 42,
                GsmState          = "Active",
                WasInBoltSelected = true,
                IsValid           = true,
            };
        }

        [Test]
        public void test_board_snapshot_system_write_then_read_round_trips_all_fields()
        {
            // Arrange
            var written = MakeSnapshot();

            // Act
            _system.WriteBoardSnapshotSync(written);
            var read = _system.ReadBoardSnapshot();

            // Assert
            Assert.IsNotNull(read);
            Assert.IsTrue(read.IsValid);
            Assert.AreEqual(written.LevelId, read.LevelId);
            Assert.AreEqual(written.ColorCount, read.ColorCount);
            Assert.AreEqual(written.StackDepth, read.StackDepth);
            Assert.AreEqual(written.TempSlotCount, read.TempSlotCount);
            Assert.AreEqual(written.TempSlotDepth, read.TempSlotDepth);
            Assert.AreEqual(written.MoveCount, read.MoveCount);
            Assert.AreEqual(written.SequenceId, read.SequenceId);
            Assert.AreEqual(written.GsmState, read.GsmState);
            Assert.AreEqual(written.WasInBoltSelected, read.WasInBoltSelected);
            Assert.AreEqual(written.StackContents[0], read.StackContents[0]);
            Assert.AreEqual(written.StackContents[1], read.StackContents[1]);
        }

        [Test]
        public void test_board_snapshot_system_read_consumes_stored_snapshot()
        {
            // Arrange
            _system.WriteBoardSnapshotSync(MakeSnapshot());

            // Act
            var first  = _system.ReadBoardSnapshot();
            var second = _system.ReadBoardSnapshot();

            // Assert — single-use: the stored snapshot must not resurface on a later read
            Assert.IsNotNull(first);
            Assert.IsNull(second, "ReadBoardSnapshot must consume (delete) the stored snapshot");
        }

        [Test]
        public void test_board_snapshot_system_read_with_no_stored_snapshot_returns_null()
        {
            // Act
            var read = _system.ReadBoardSnapshot();

            // Assert
            Assert.IsNull(read);
        }

        [Test]
        public void test_board_snapshot_system_read_with_corrupt_json_returns_invalid_snapshot()
        {
            // Arrange — write malformed JSON directly, bypassing the system's writer
            PlayerPrefs.SetString(SnapshotKey, "{not valid json");
            PlayerPrefs.Save();

            // Act
            var read = _system.ReadBoardSnapshot();

            // Assert — SER-03: corrupt data yields IsValid = false, never an exception
            Assert.IsNotNull(read);
            Assert.IsFalse(read.IsValid);
        }

        [Test]
        public void test_board_snapshot_system_write_with_null_snapshot_does_not_throw_or_store()
        {
            // Act
            Assert.DoesNotThrow(() => _system.WriteBoardSnapshotSync(null));

            // Assert — nothing stored; subsequent read finds nothing
            Assert.IsNull(_system.ReadBoardSnapshot());
        }
    }
}
