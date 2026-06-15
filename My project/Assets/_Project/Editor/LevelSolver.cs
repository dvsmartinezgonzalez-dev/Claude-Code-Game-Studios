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

        /// <summary>Reserved color id for the multicolor wildcard ball (matches any color).</summary>
        public const int Wildcard = 0;

        /// <summary>Runs BFS on the given level. Returns immediately on first win found.</summary>
        /// <remarks>
        /// Phase-2 mechanic support (schema_version 2), mirroring tools/levels/boltsort_levels.py:
        ///   * MYSTERY ball — negative color id, normalised to abs() (solver has full info).
        ///   * MULTICOLOR ball — color id 0 (<see cref="Wildcard"/>), matches any color.
        ///   * FROZEN tube — per-column freeze counter; deposits banned while >0; ticks each move;
        ///     folded into the canonical key so it never wrongly merges states.
        ///   * ASYMMETRIC / large capacity — per-column caps from <c>tube_capacities</c>.
        /// </remarks>
        public static SolverResult Solve(LevelRecord level, int stateLimit = DefaultStateLimit)
        {
            int colorCount  = level.ColorCount;
            int stackDepth  = level.StackDepth;
            int tempCount   = level.TempSlotCount;
            int tempDepth   = level.TempSlotDepth;
            int totalCols   = colorCount + tempCount;

            // Per-column capacities: explicit tube_capacities override, else uniform depths.
            var caps = new int[totalCols];
            if (level.TubeCapacities != null && level.TubeCapacities.Length == totalCols)
            {
                Array.Copy(level.TubeCapacities, caps, totalCols);
            }
            else
            {
                for (int i = 0; i < totalCols; i++)
                    caps[i] = i < colorCount ? stackDepth : tempDepth;
            }

            // Initial freeze counters per column (0 = not frozen).
            var frozen0 = new int[totalCols];
            if (level.FrozenTubes != null)
                foreach (var ft in level.FrozenTubes)
                    if (ft != null && ft.TubeIndex >= 0 && ft.TubeIndex < totalCols)
                        frozen0[ft.TubeIndex] = ft.Turns;

            // Initial state — index 0 = bottom, last = top (matches GSM convention).
            // Mystery (negative) normalised to abs(); wildcard 0 kept verbatim.
            var initial = new int[totalCols][];
            for (int i = 0; i < colorCount; i++)
            {
                var src = level.ColorStacks[i];
                var col = new int[src.Length];
                for (int j = 0; j < src.Length; j++)
                    col[j] = src[j] == Wildcard ? Wildcard : Math.Abs(src[j]);
                initial[i] = col;
            }
            for (int i = colorCount; i < totalCols; i++)
                initial[i] = Array.Empty<int>();

            if (IsWon(initial, caps, totalCols))
                return new SolverResult { IsSolvable = true, MinMoves = 0 };

            var visited = new HashSet<string>();
            var queue   = new Queue<(int[][] state, int[] frozen, int moves)>();
            visited.Add(Canonical(initial, caps, frozen0, totalCols));
            queue.Enqueue((initial, frozen0, 0));

            int explored = 0;
            while (queue.Count > 0)
            {
                var (state, frozen, moves) = queue.Dequeue();
                explored++;
                if (explored > stateLimit)
                    return new SolverResult { IsSolvable = false, MinMoves = -1, FailReason = "timeout" };

                for (int src = 0; src < totalCols; src++)
                {
                    int srcLen = state[src].Length;
                    if (srcLen == 0) continue;
                    // Prune: never disturb a finished color tube (full + single color incl. wildcard).
                    if (src < colorCount && srcLen == caps[src] && IsCompleteColumn(state[src]))
                        continue;

                    int held = state[src][srcLen - 1];
                    var usedEmptyCaps = new HashSet<int>(); // collapse interchangeable empties per capacity

                    for (int dst = 0; dst < totalCols; dst++)
                    {
                        if (dst == src) continue;
                        if (frozen[dst] > 0) continue;       // frozen tube: deposits banned
                        int cap = caps[dst];
                        if (state[dst].Length == 0)
                        {
                            // interchangeable empty destinations of equal capacity → one candidate
                            if (!usedEmptyCaps.Add(cap)) continue;
                        }
                        else if (state[dst].Length >= cap ||
                                 !Match(state[dst][state[dst].Length - 1], held))
                        {
                            continue;
                        }

                        var next = CopyApply(state, totalCols, src, dst, held);
                        var nextFrozen = TickFreeze(frozen, totalCols);

                        if (IsWon(next, caps, totalCols))
                            return new SolverResult { IsSolvable = true, MinMoves = moves + 1 };

                        string key = Canonical(next, caps, nextFrozen, totalCols);
                        if (visited.Add(key))
                            queue.Enqueue((next, nextFrozen, moves + 1));
                    }
                }
            }

            return new SolverResult { IsSolvable = false, MinMoves = -1, FailReason = "unsolvable" };
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>Placement match: either ball is a wildcard, or colors are equal.</summary>
        private static bool Match(int top, int held)
            => top == Wildcard || held == Wildcard || top == held;

        /// <summary>True if every non-wildcard ball shares one color (wildcards are neutral).</summary>
        private static bool IsCompleteColumn(int[] col)
        {
            int baseColor = Wildcard;
            for (int j = 0; j < col.Length; j++)
            {
                if (col[j] == Wildcard) continue;
                if (baseColor == Wildcard) baseColor = col[j];
                else if (col[j] != baseColor) return false;
            }
            return true;
        }

        private static bool IsWon(int[][] state, int[] caps, int totalCols)
        {
            for (int i = 0; i < totalCols; i++)
            {
                int[] col = state[i];
                if (col.Length == 0) continue;
                if (col.Length != caps[i]) return false;
                if (!IsCompleteColumn(col)) return false;
            }
            return true;
        }

        private static int[] TickFreeze(int[] frozen, int totalCols)
        {
            var next = new int[totalCols];
            for (int i = 0; i < totalCols; i++)
                next[i] = frozen[i] > 0 ? frozen[i] - 1 : 0;
            return next;
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
        /// Symmetry-reduced key: each column is rendered as "cap.frozen:contents", and the
        /// per-column strings are sorted so interchangeable tubes (equal capacity AND equal
        /// remaining freeze) produce an identical key regardless of their slot index. Once all
        /// tubes thaw the freeze component is uniformly 0 and the key collapses to the v1 form.
        /// </summary>
        private static string Canonical(int[][] state, int[] caps, int[] frozen, int totalCols)
        {
            var parts = new string[totalCols];
            for (int i = 0; i < totalCols; i++)
            {
                var sb = new StringBuilder(8);
                sb.Append(caps[i]).Append('.').Append(frozen[i]).Append(':');
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
