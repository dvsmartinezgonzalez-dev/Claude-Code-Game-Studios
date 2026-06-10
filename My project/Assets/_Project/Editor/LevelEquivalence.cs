using System;
using System.Collections.Generic;
using System.Text;
using BoltSort.LevelData;

namespace BoltSort.Editor
{
    /// <summary>
    /// Structural-equivalence fingerprinting for bolt-sort levels.
    ///
    /// Two levels are the SAME puzzle in disguise when one can be turned into the
    /// other by (a) renaming colors and/or (b) reordering interchangeable tubes.
    /// The old catalogue shipped 30 levels that collapsed to only 7 distinct
    /// puzzles under exactly these transforms; this class is the guard that stops
    /// that from recurring.
    ///
    /// <para><see cref="CanonicalSignature"/> returns a string that is invariant
    /// under color relabeling and tube reordering. Equal signatures ⇒ equivalent
    /// puzzles. The buffer envelope (stack depth, temp slot count + depth) is folded
    /// into the signature so two identical scrambles with different buffers are NOT
    /// merged.</para>
    /// </summary>
    public static class LevelEquivalence
    {
        /// <summary>
        /// Canonical signature invariant under color relabeling and tube reordering.
        /// Minimises, over every color bijection, the sorted multiset of filled tubes.
        /// Cost is O(colorCount! · tubes) — fine for authoring/test use at colorCount ≤ 8.
        /// </summary>
        public static string CanonicalSignature(LevelRecord level)
        {
            int n = level.ColorCount;
            var filled = new List<int[]>();
            foreach (var stack in level.ColorStacks)
                if (stack != null && stack.Length > 0)
                    filled.Add(stack);

            string shapePrefix =
                $"{level.StackDepth}.{level.TempSlotCount}.{level.TempSlotDepth}.{n}";

            string best = null;
            foreach (var perm in Permutations(n))
            {
                // perm[oldColor-1] = newColor
                var tubeStrings = new string[filled.Count];
                for (int t = 0; t < filled.Count; t++)
                {
                    var col = filled[t];
                    var sb = new StringBuilder(col.Length * 2);
                    for (int j = 0; j < col.Length; j++)
                    {
                        if (j > 0) sb.Append(',');
                        sb.Append(perm[col[j] - 1]);
                    }
                    tubeStrings[t] = sb.ToString();
                }
                Array.Sort(tubeStrings, StringComparer.Ordinal);
                string candidate = shapePrefix + "||" + string.Join("/", tubeStrings);
                if (best == null || string.CompareOrdinal(candidate, best) < 0)
                    best = candidate;
            }
            return best;
        }

        /// <summary>Yields every permutation of {1..n} as an int[] (Heap's algorithm).</summary>
        private static IEnumerable<int[]> Permutations(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i + 1;
            var c = new int[n];
            yield return (int[])a.Clone();
            int idx = 0;
            while (idx < n)
            {
                if (c[idx] < idx)
                {
                    int swap = (idx % 2 == 0) ? 0 : c[idx];
                    (a[swap], a[idx]) = (a[idx], a[swap]);
                    yield return (int[])a.Clone();
                    c[idx]++;
                    idx = 0;
                }
                else
                {
                    c[idx] = 0;
                    idx++;
                }
            }
        }
    }
}
