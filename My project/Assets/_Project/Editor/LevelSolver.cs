using System;
using System.Collections.Generic;
using System.Text;
using BoltSort.LevelData;

namespace BoltSort.Editor
{
    /// <summary>
    /// BFS solvability solver for bolt-sort puzzle levels.
    /// Move rules mirror SortMechanic.IsLegalMove() exactly:
    ///   - One bolt per move (source top → destination).
    ///   - Destination legal if: empty, OR (not full AND top matches held color).
    ///   - Color stack cap = StackDepth; temp slot cap = TempSlotDepth.
    /// Win condition mirrors SortMechanic.IsWon():
    ///   - Every non-empty column must be exactly full AND mono-color.
    ///   - Empty columns pass.
    /// </summary>
    public static class LevelSolver
    {
        public const int DefaultStateLimit = 500_000;

        public sealed class SolverResult
        {
            public bool   IsSolvable  { get; internal set; }
            public int    MinMoves    { get; internal set; }
            public string FailReason  { get; internal set; }
        }

        /// <summary>Runs BFS on the given level. Returns immediately on first win found.</summary>
        public static SolverResult Solve(LevelRecord level, int stateLimit = DefaultStateLimit)
        {
            int colorCount  = level.ColorCount;
            int stackDepth  = level.StackDepth;
            int tempCount   = level.TempSlotCount;
            int tempDepth   = level.TempSlotDepth;
            int totalCols   = colorCount + tempCount;

            // Initial state — index 0 = bottom, last = top (matches GSM convention)
            var initial = new int[totalCols][];
            for (int i = 0; i < colorCount; i++)
                initial[i] = (int[])level.ColorStacks[i].Clone();
            for (int i = colorCount; i < totalCols; i++)
                initial[i] = Array.Empty<int>();

            if (IsWon(initial, colorCount, stackDepth, tempDepth, totalCols))
                return new SolverResult { IsSolvable = true, MinMoves = 0 };

            var visited = new HashSet<string>();
            var queue   = new Queue<(int[][] state, int moves)>();
            string startKey = Encode(initial, totalCols);
            visited.Add(startKey);
            queue.Enqueue((initial, 0));

            int explored = 0;
            while (queue.Count > 0)
            {
                var (state, moves) = queue.Dequeue();
                explored++;
                if (explored > stateLimit)
                    return new SolverResult { IsSolvable = false, MinMoves = -1, FailReason = "timeout" };

                for (int src = 0; src < totalCols; src++)
                {
                    if (state[src].Length == 0) continue;
                    int held = state[src][state[src].Length - 1];

                    for (int dst = 0; dst < totalCols; dst++)
                    {
                        if (dst == src) continue;
                        int cap = dst < colorCount ? stackDepth : tempDepth;
                        if (!IsLegal(held, state[dst], cap)) continue;

                        var next = CopyApply(state, totalCols, src, dst, held);

                        if (IsWon(next, colorCount, stackDepth, tempDepth, totalCols))
                            return new SolverResult { IsSolvable = true, MinMoves = moves + 1 };

                        string key = Encode(next, totalCols);
                        if (visited.Add(key))
                            queue.Enqueue((next, moves + 1));
                    }
                }
            }

            return new SolverResult { IsSolvable = false, MinMoves = -1, FailReason = "unsolvable" };
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static bool IsLegal(int heldColor, int[] dest, int cap)
        {
            if (dest.Length == 0)           return true;
            if (dest.Length >= cap)         return false;
            return dest[dest.Length - 1] == heldColor;
        }

        private static bool IsWon(int[][] state, int colorCount,
                                   int stackDepth, int tempDepth, int totalCols)
        {
            for (int i = 0; i < totalCols; i++)
            {
                int[] col = state[i];
                if (col.Length == 0) continue;
                int cap = i < colorCount ? stackDepth : tempDepth;
                if (col.Length != cap) return false;
                int first = col[0];
                for (int j = 1; j < col.Length; j++)
                    if (col[j] != first) return false;
            }
            return true;
        }

        private static int[][] CopyApply(int[][] state, int totalCols, int src, int dst, int held)
        {
            var next = new int[totalCols][];
            for (int i = 0; i < totalCols; i++)
                next[i] = (int[])state[i].Clone();

            // Remove top from src
            next[src] = new int[state[src].Length - 1];
            Array.Copy(state[src], next[src], next[src].Length);

            // Append to dst
            next[dst] = new int[state[dst].Length + 1];
            Array.Copy(state[dst], next[dst], state[dst].Length);
            next[dst][state[dst].Length] = held;

            return next;
        }

        private static string Encode(int[][] state, int cols)
        {
            var sb = new StringBuilder(cols * 8);
            for (int i = 0; i < cols; i++)
            {
                sb.Append('[');
                for (int j = 0; j < state[i].Length; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(state[i][j]);
                }
                sb.Append(']');
            }
            return sb.ToString();
        }
    }
}
