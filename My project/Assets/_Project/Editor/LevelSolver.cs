using System;
using System.Collections.Generic;
using System.Text;
using BoltSort.LevelData;

namespace BoltSort.Editor
{
    /// <summary>
    /// BFS solvability + optimal-move solver for bolt-sort puzzle levels.
    /// Move rules mirror SortMechanic.IsLegalMove() exactly:
    ///   - One bolt per move (source top → destination).
    ///   - Destination legal if: empty, OR (not full AND top matches held color).
    ///   - Color stack cap = StackDepth; temp slot cap = TempSlotDepth.
    /// Win condition mirrors SortMechanic.IsWon():
    ///   - Every non-empty column must be exactly full AND mono-color.
    ///   - Empty columns pass.
    ///
    /// <para>The visited set is keyed on a SYMMETRY-REDUCED canonical encoding:
    /// columns are sorted within equal-capacity buckets before hashing. Two board
    /// states that differ only by which interchangeable tube holds which contents
    /// collapse to one key. This is correctness-preserving (BFS still discovers the
    /// minimum move count) and cuts the explored state count by orders of magnitude,
    /// which is what lets the solver scale to 6-color / depth-5 boards within budget.</para>
    ///
    /// <para>Move generation applies two optimality-preserving prunes: a finished
    /// color tube (full + single color) is never used as a source, and interchangeable
    /// empty destinations of equal capacity collapse to a single candidate.</para>
    /// </summary>
    public static class LevelSolver
    {
        public const int DefaultStateLimit = 1_500_000;

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

            // Per-column capacities (color stacks first, then temp slots).
            var caps = new int[totalCols];
            for (int i = 0; i < totalCols; i++)
                caps[i] = i < colorCount ? stackDepth : tempDepth;

            // Initial state — index 0 = bottom, last = top (matches GSM convention).
            var initial = new int[totalCols][];
            for (int i = 0; i < colorCount; i++)
                initial[i] = (int[])level.ColorStacks[i].Clone();
            for (int i = colorCount; i < totalCols; i++)
                initial[i] = Array.Empty<int>();

            if (IsWon(initial, caps, totalCols))
                return new SolverResult { IsSolvable = true, MinMoves = 0 };

            var visited = new HashSet<string>();
            var queue   = new Queue<(int[][] state, int moves)>();
            visited.Add(Canonical(initial, caps, totalCols));
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
                    int srcLen = state[src].Length;
                    if (srcLen == 0) continue;
                    // Prune: never disturb a finished color tube (full + single color).
                    if (src < colorCount && srcLen == caps[src] && IsMono(state[src]))
                        continue;

                    int held = state[src][srcLen - 1];
                    var usedEmptyCaps = new HashSet<int>(); // collapse interchangeable empties per capacity

                    for (int dst = 0; dst < totalCols; dst++)
                    {
                        if (dst == src) continue;
                        int cap = caps[dst];
                        if (state[dst].Length == 0)
                        {
                            // interchangeable empty destinations of equal capacity → one candidate
                            if (!usedEmptyCaps.Add(cap)) continue;
                        }
                        else if (state[dst].Length >= cap || state[dst][state[dst].Length - 1] != held)
                        {
                            continue;
                        }

                        var next = CopyApply(state, totalCols, src, dst, held);

                        if (IsWon(next, caps, totalCols))
                            return new SolverResult { IsSolvable = true, MinMoves = moves + 1 };

                        string key = Canonical(next, caps, totalCols);
                        if (visited.Add(key))
                            queue.Enqueue((next, moves + 1));
                    }
                }
            }

            return new SolverResult { IsSolvable = false, MinMoves = -1, FailReason = "unsolvable" };
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static bool IsMono(int[] col)
        {
            for (int j = 1; j < col.Length; j++)
                if (col[j] != col[0]) return false;
            return true;
        }

        private static bool IsWon(int[][] state, int[] caps, int totalCols)
        {
            for (int i = 0; i < totalCols; i++)
            {
                int[] col = state[i];
                if (col.Length == 0) continue;
                if (col.Length != caps[i]) return false;
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
                next[i] = state[i]; // shallow share; only src/dst are replaced below

            next[src] = new int[state[src].Length - 1];
            Array.Copy(state[src], next[src], next[src].Length);

            next[dst] = new int[state[dst].Length + 1];
            Array.Copy(state[dst], next[dst], state[dst].Length);
            next[dst][state[dst].Length] = held;

            return next;
        }

        /// <summary>
        /// Symmetry-reduced key: each column is rendered as "cap:contents", and the
        /// per-column strings are sorted so interchangeable tubes (equal capacity)
        /// produce an identical key regardless of their slot index.
        /// </summary>
        private static string Canonical(int[][] state, int[] caps, int totalCols)
        {
            var parts = new string[totalCols];
            for (int i = 0; i < totalCols; i++)
            {
                var sb = new StringBuilder(8);
                sb.Append(caps[i]).Append(':');
                for (int j = 0; j < state[i].Length; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(state[i][j]);
                }
                parts[i] = sb.ToString();
            }
            Array.Sort(parts, StringComparer.Ordinal);
            return string.Join("|", parts);
        }
    }
}
