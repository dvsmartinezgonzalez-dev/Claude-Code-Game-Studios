using System;
using System.Collections.Generic;

namespace BoltSort.Editor
{
    /// <summary>BFS solvability solver for bolt-sort puzzle levels.</summary>
    public static class LevelSolver
    {
        public const int DefaultStateLimit = 500_000;

        public struct SolverResult
        {
            public bool  IsSolvable;
            public int   MinMoves;
            public bool  TimedOut;
            public int   StatesExplored;
        }

        public struct LevelSpec
        {
            public int     ColorCount;
            public int     StackDepth;
            public int     TempSlotCount;
            public int     TempSlotDepth;
            public int[][] ColorStacks;
        }

        /// <summary>
        /// BFS from initial board state to win. Matches the exact move-legality rules
        /// and win condition defined by SortMechanic.IsLegalMove() and SortMechanic.IsWon().
        ///
        /// Move rules:
        ///   - One bolt moves per step (src top → dst top).
        ///   - Destination is legal if: empty, OR (not full AND top matches held color).
        ///   - Color stacks cap = StackDepth; temp slots cap = TempSlotDepth.
        ///
        /// Win condition (mirrors SortMechanic.IsWon):
        ///   - Every non-empty column must be full (length == capacity) AND mono-color.
        ///   - Empty columns are OK.
        /// </summary>
        public static SolverResult Solve(LevelSpec spec, int stateLimit = DefaultStateLimit)
        {
            int totalCols = spec.ColorCount + spec.TempSlotCount;

            // Build initial state (jagged array: index 0 = bottom, last = top)
            var initial = new int[totalCols][];
            for (int i = 0; i < spec.ColorCount; i++)
                initial[i] = (int[])spec.ColorStacks[i].Clone();
            for (int i = spec.ColorCount; i < totalCols; i++)
                initial[i] = Array.Empty<int>();

            if (IsWon(initial, spec))
                return new SolverResult { IsSolvable = true, MinMoves = 0, StatesExplored = 0 };

            var visited = new HashSet<string>();
            var queue   = new Queue<(int[][] state, int moves)>();

            visited.Add(Encode(initial));
            queue.Enqueue((initial, 0));

            int explored = 0;

            while (queue.Count > 0)
            {
                var (state, moves) = queue.Dequeue();
                explored++;

                if (explored > stateLimit)
                    return new SolverResult { TimedOut = true, MinMoves = -1, StatesExplored = explored };

                for (int src = 0; src < totalCols; src++)
                {
                    if (state[src].Length == 0) continue;
                    int held = state[src][state[src].Length - 1];

                    for (int dst = 0; dst < totalCols; dst++)
                    {
                        if (dst == src) continue;
                        int cap = dst < spec.ColorCount ? spec.StackDepth : spec.TempSlotDepth;

                        if (!IsLegal(held, state[dst], cap)) continue;

                        // Apply move immutably
                        var next = CopyState(state, totalCols);
                        next[src] = Remove(next[src]);
                        next[dst] = Append(next[dst], held);

                        if (IsWon(next, spec))
                            return new SolverResult
                            {
                                IsSolvable     = true,
                                MinMoves       = moves + 1,
                                StatesExplored = explored,
                            };

                        string key = Encode(next);
                        if (visited.Add(key))
                            queue.Enqueue((next, moves + 1));
                    }
                }
            }

            return new SolverResult { IsSolvable = false, MinMoves = -1, StatesExplored = explored };
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static bool IsLegal(int heldColor, int[] dest, int cap)
        {
            if (dest.Length == 0)       return true;
            if (dest.Length >= cap)     return false;
            return dest[dest.Length - 1] == heldColor;
        }

        private static bool IsWon(int[][] state, LevelSpec spec)
        {
            int totalCols = spec.ColorCount + spec.TempSlotCount;
            for (int i = 0; i < totalCols; i++)
            {
                int[] col = state[i];
                if (col.Length == 0) continue;
                int cap = i < spec.ColorCount ? spec.StackDepth : spec.TempSlotDepth;
                if (col.Length != cap) return false;
                int first = col[0];
                for (int j = 1; j < col.Length; j++)
                    if (col[j] != first) return false;
            }
            return true;
        }

        private static int[][] CopyState(int[][] src, int cols)
        {
            var copy = new int[cols][];
            for (int i = 0; i < cols; i++)
                copy[i] = (int[])src[i].Clone();
            return copy;
        }

        private static int[] Remove(int[] stack)
        {
            if (stack.Length == 0) return Array.Empty<int>();
            var r = new int[stack.Length - 1];
            Array.Copy(stack, r, r.Length);
            return r;
        }

        private static int[] Append(int[] stack, int value)
        {
            var r = new int[stack.Length + 1];
            Array.Copy(stack, r, stack.Length);
            r[stack.Length] = value;
            return r;
        }

        private static string Encode(int[][] state)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var col in state)
            {
                sb.Append('[');
                for (int i = 0; i < col.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(col[i]);
                }
                sb.Append(']');
            }
            return sb.ToString();
        }
    }
}
