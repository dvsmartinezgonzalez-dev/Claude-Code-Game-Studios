using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Result of a board layout pass. Holds the uniform visual sizing and the
    /// per-column local positions, indexed by <b>flat column index</b>
    /// (0..colorCount-1 = color stacks, then temp slots) — the same convention the
    /// rest of the game uses. Purely visual: carries no game state.
    /// </summary>
    public struct BoardLayout
    {
        /// <summary>Number of rows the tubes are spread across (1..4).</summary>
        public int RowCount;
        /// <summary>Tube body width in world units.</summary>
        public float ColWidth;
        /// <summary>Bolt sprite width == height (kept equal so marbles never deform).</summary>
        public float BoltWidth;
        /// <summary>Bolt sprite height == width.</summary>
        public float BoltHeight;
        /// <summary>Vertical distance between stacked bolt slots.</summary>
        public float BoltStep;
        /// <summary>Geometric centre of the play area (used for win FX origin).</summary>
        public float BoardCenterY;
        /// <summary>Local position (x, slot0-Y, 0) per flat column index.</summary>
        public Vector3[] Columns;
        /// <summary>Column count assigned to each row (length == RowCount).</summary>
        public int[] RowCounts;
    }

    /// <summary>
    /// Pure (engine-math only) responsive layout for the bolt board. Decides how
    /// many rows to use, balances tubes across them, and computes a single uniform
    /// scale so bolts stay round regardless of tube count. Owns NO game rules — it
    /// only produces transforms/sizes that <see cref="BoardView"/> applies.
    /// </summary>
    public static class GameplayBoardLayout
    {
        // ── Tuning knobs ──────────────────────────────────────────────────────────
        /// <summary>Horizontal world-unit margin on each side of the play area.</summary>
        public const float SideMargin = 0.20f;
        /// <summary>Vertical world-unit gap between rows.</summary>
        public const float RowGap = 0.18f;
        /// <summary>Tube body width as a fraction of its horizontal cell.</summary>
        public const float ColWidthFactor = 0.82f;
        /// <summary>World units trimmed from the cell width to get the bolt width.</summary>
        public const float BoltInset = 0.12f;
        /// <summary>Bolt diameter as a fraction of the vertical slot step.</summary>
        public const float BoltAspect = 0.78f;
        /// <summary>Fraction of a row's height the tube sprite is allowed to occupy.</summary>
        public const float RowFillFactor = 0.92f;
        /// <summary>Lower clamp so a degenerate level never produces a zero scale.</summary>
        public const float MinBoltDiameter = 0.05f;

        /// <summary>
        /// Rows used for a given tube count:
        /// ≤6 → 1, 7–10 → 2, 11–14 → 3, 15+ → 4.
        /// </summary>
        public static int RowsForColumnCount(int totalCols)
        {
            if (totalCols <= 6)  return 1;
            if (totalCols <= 10) return 2;
            if (totalCols <= 14) return 3;
            return 4;
        }

        /// <summary>
        /// Splits <paramref name="totalCols"/> across <paramref name="rowCount"/> rows
        /// as evenly as possible (sizes differ by at most 1). Earlier rows take any
        /// remainder, so the last row is never the fullest — keeping extra/empty tubes
        /// (highest flat indices) on a clean, equal-or-shorter final row.
        /// </summary>
        public static int[] DistributeColumns(int totalCols, int rowCount)
        {
            rowCount = Mathf.Clamp(rowCount, 1, Mathf.Max(1, totalCols));
            int[] counts = new int[rowCount];
            int baseCount = totalCols / rowCount;
            int remainder = totalCols % rowCount;
            for (int r = 0; r < rowCount; r++)
                counts[r] = baseCount + (r < remainder ? 1 : 0);
            return counts;
        }

        /// <summary>
        /// Sprite height-to-width aspect ratio for a tube of the given capacity.
        /// Mirrors the Aspect values in <c>BoardView._tubeMetricsByTier</c>.
        /// </summary>
        public static float TubeAspectForDepth(int depth) =>
            depth <= 3 ? 2.440f :
            depth == 4 ? 2.844f :
            depth == 5 ? 3.252f :
            depth == 6 ? 3.944f : 5.127f;

        /// <summary>
        /// Computes the full board layout for the given level shape and play area.
        /// The play area is the band between <paramref name="boardTop"/> and
        /// <paramref name="boardBot"/> (already excluding the HUD and bottom bar).
        /// </summary>
        public static BoardLayout Compute(
            int colorCount, int tempSlotCount,
            int stackDepth, int tempSlotDepth,
            float camHalfW, float boardTop, float boardBot)
        {
            int totalCols = Mathf.Max(1, colorCount + tempSlotCount);
            int maxDepth  = Mathf.Max(1, Mathf.Max(stackDepth, tempSlotDepth));

            int   rowCount   = RowsForColumnCount(totalCols);
            int[] rowCounts  = DistributeColumns(totalCols, rowCount);
            rowCount         = rowCounts.Length;

            int maxColsInRow = 1;
            for (int r = 0; r < rowCount; r++)
                if (rowCounts[r] > maxColsInRow) maxColsInRow = rowCounts[r];

            float boardH      = boardTop - boardBot;
            float boardCenter = (boardTop + boardBot) * 0.5f;
            float availW      = camHalfW * 2f - SideMargin * 2f;

            // Width-constrained bolt diameter (widest row sets the horizontal limit).
            float colStepW = availW / maxColsInRow;
            float diaW     = colStepW * ColWidthFactor - BoltInset;

            // Height-constrained bolt diameter: the full tube sprite must fit within
            // RowFillFactor of each row's height. Using the sprite aspect ratio avoids
            // the over-conservative boltStep formula that left large empty bands.
            float rowHeight  = (boardH - (rowCount - 1) * RowGap) / rowCount;
            float tubeAspect = TubeAspectForDepth(maxDepth);
            float diaH       = rowHeight * RowFillFactor / tubeAspect - BoltInset;

            // Uniform diameter → bolts stay round and never deform.
            float dia = Mathf.Max(MinBoltDiameter, Mathf.Min(diaW, diaH));

            float boltStep = dia / BoltAspect;
            float colWidth = dia + BoltInset;
            float colStep  = colWidth / ColWidthFactor;

            // Stack the rows, vertically centred within the play area.
            float stride = rowHeight + RowGap;
            float blockH = rowCount * rowHeight + (rowCount - 1) * RowGap;
            float topRowCenterY = boardCenter + blockH * 0.5f - rowHeight * 0.5f;

            var columns = new Vector3[totalCols];
            int flat = 0;
            for (int r = 0; r < rowCount; r++)
            {
                int   count    = rowCounts[r];
                float rowCtrY  = topRowCenterY - r * stride;
                // Bottom-align tubes to a shared baseline (matches single-row behaviour).
                float slot0Y   = rowCtrY - (maxDepth - 1) * 0.5f * boltStep;
                float startX   = -(count - 1) * 0.5f * colStep;
                for (int k = 0; k < count; k++, flat++)
                    columns[flat] = new Vector3(startX + k * colStep, slot0Y, 0f);
            }

            return new BoardLayout
            {
                RowCount     = rowCount,
                ColWidth     = colWidth,
                BoltWidth    = dia,
                BoltHeight   = dia,
                BoltStep     = boltStep,
                BoardCenterY = boardCenter,
                Columns      = columns,
                RowCounts    = rowCounts,
            };
        }
    }
}
