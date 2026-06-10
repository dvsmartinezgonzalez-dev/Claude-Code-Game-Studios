using NUnit.Framework;
using UnityEngine;
using BoltSort.Gameplay;

namespace BoltSort.Tests.Unit.GameplayLayout
{
    /// <summary>
    /// Verifies the responsive multi-row board layout: row-count selection, balanced
    /// distribution, round (non-deformed) bolts, and correct use of the play area.
    /// Reference play area mirrors a 9:16 portrait camera (orthoSize 9.6).
    /// </summary>
    public class GameplayBoardLayout_Test
    {
        // 9:16 portrait reference camera, matching BoardView's defaults.
        private const float CamHalfW = 5.4f;     // 9.6 * (9/16)
        private const float BoardTop = 7.68f;    // 9.6 - 10% HUD
        private const float BoardBot = -5.76f;   // -9.6 + 20% buttons
        private const float Eps      = 0.002f;

        // ── Row-count selection ──────────────────────────────────────────────────

        [TestCase(3,  1)]
        [TestCase(6,  1)]
        [TestCase(7,  2)]
        [TestCase(10, 2)]
        [TestCase(11, 3)]
        [TestCase(14, 3)]
        [TestCase(15, 4)]
        [TestCase(24, 4)]
        public void test_rows_for_count_matches_thresholds(int total, int expectedRows)
        {
            Assert.AreEqual(expectedRows, GameplayBoardLayout.RowsForColumnCount(total));
        }

        // ── Distribution balance ─────────────────────────────────────────────────

        [TestCase(12, 3)]
        [TestCase(10, 2)]
        [TestCase(7,  2)]
        [TestCase(13, 4)]
        public void test_distribute_is_balanced_and_front_loaded(int total, int rows)
        {
            int[] counts = GameplayBoardLayout.DistributeColumns(total, rows);

            int sum = 0, min = int.MaxValue, max = int.MinValue;
            foreach (int c in counts) { sum += c; min = Mathf.Min(min, c); max = Mathf.Max(max, c); }

            Assert.AreEqual(total, sum, "all tubes placed");
            Assert.LessOrEqual(max - min, 1, "rows differ by at most one tube");
            for (int r = 1; r < counts.Length; r++)
                Assert.GreaterOrEqual(counts[r - 1], counts[r], "earlier rows are never emptier");
        }

        // ── No deformation: bolts are square (round sprite) ──────────────────────

        [TestCase(4,  0, 4, 0)]   // small  → 1 row
        [TestCase(8,  2, 5, 3)]   // medium → 2 rows
        [TestCase(12, 2, 4, 4)]   // large  → 3 rows
        [TestCase(16, 2, 6, 4)]   // xlarge → 4 rows
        public void test_bolts_never_deform(int colors, int temps, int depth, int tempDepth)
        {
            BoardLayout l = Compute(colors, temps, depth, tempDepth);
            Assert.AreEqual(l.BoltWidth, l.BoltHeight, Eps, "bolt width must equal height");
            Assert.Greater(l.BoltWidth, 0f);
            Assert.AreEqual(colors + temps, l.Columns.Length);
        }

        // ── Horizontal containment within the play area ──────────────────────────

        [TestCase(6,  0, 4, 0)]
        [TestCase(10, 2, 5, 3)]
        [TestCase(14, 2, 5, 4)]
        [TestCase(18, 2, 6, 4)]
        public void test_columns_fit_horizontally(int colors, int temps, int depth, int tempDepth)
        {
            BoardLayout l = Compute(colors, temps, depth, tempDepth);
            float halfTube = l.ColWidth * 0.5f;
            float limit    = CamHalfW - GameplayBoardLayout.SideMargin + Eps;
            foreach (Vector3 c in l.Columns)
                Assert.LessOrEqual(Mathf.Abs(c.x) + halfTube, limit,
                    "tube extends past the side margin");
        }

        // ── Rows fill the play area exactly, never spilling into HUD/buttons ─────

        [TestCase(12, 0, 4, 0)]
        [TestCase(16, 2, 6, 4)]
        public void test_rows_fill_board_band(int colors, int temps, int depth, int tempDepth)
        {
            BoardLayout l = Compute(colors, temps, depth, tempDepth);
            float boardH = BoardTop - BoardBot;

            float blockH = l.RowCount * RowHeight(l.RowCount, boardH)
                         + (l.RowCount - 1) * GameplayBoardLayout.RowGap;
            Assert.AreEqual(boardH, blockH, Eps, "row block must exactly fill the play band");

            // Deepest tube fits inside one row slice (no row overlap).
            int   maxDepth = Mathf.Max(depth, tempDepth);
            float tubeH    = maxDepth * l.BoltStep + GameplayBoardLayout.BorderPad;
            Assert.LessOrEqual(tubeH, RowHeight(l.RowCount, boardH) + Eps, "tube taller than its row");
        }

        private static float RowHeight(int rowCount, float boardH)
            => (boardH - (rowCount - 1) * GameplayBoardLayout.RowGap) / rowCount;

        private static BoardLayout Compute(int colors, int temps, int depth, int tempDepth)
            => GameplayBoardLayout.Compute(colors, temps, depth, tempDepth,
                                           CamHalfW, BoardTop, BoardBot);
    }
}
