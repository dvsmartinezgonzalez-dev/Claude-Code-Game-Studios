using System.Collections.Generic;
using Newtonsoft.Json;

namespace BoltSort.LevelData
{
    /// <summary>
    /// Deterministic, seeded procedural generator for levels 201+ (System 1).
    /// <para>
    /// <c>seed = levelId</c> ⇒ the same level is produced on every device/replay. Difficulty is
    /// held FLAT at the level-170–200 envelope (no escalation with level id); only mild random
    /// variation is applied. Boards are produced by reverse-construction (start solved, apply
    /// legal solve-inverse moves) which <b>guarantees solvability</b> without needing the
    /// Editor-only solver, then gated through <see cref="LevelRecordValidator"/>.
    /// </para>
    /// Generated records are never written to levels.json — <see cref="LevelDataSystem"/> caches
    /// them in memory only. See design/gdd/phase2-technical-design.md §4.
    /// </summary>
    public static class ProcLevelGenerator
    {
        /// <summary>First procedurally-generated level id. 1–200 are curated in levels.json.</summary>
        public const int ProcFirstLevel = 201;

        /// <summary>Highest servable procedural id. Bounded by SaveSystem's 1–9999 completion guard.</summary>
        public const int ProcMaxLevel = 9999;

        /// <summary>Catalogue version stamped on generated records (must match YYYY.MM).</summary>
        private const string AddedVersion = "2026.06";

        // ── Difficulty envelope (matches curated levels 170–200; NOT scaled by level id) ──
        private const int SpecialMechanicChancePct = 35; // ≥30% must carry a special mechanic
        private const int TallTubeChancePct        = 10; // ~10% carry one tall asymmetric tube

        // ── Public API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generates a valid, solvable <see cref="LevelRecord"/> for <paramref name="levelId"/>.
        /// Runs <see cref="LevelRecordValidator"/> before returning; on failure increments the seed
        /// offset and retries (max 10 attempts). Returns null only if every attempt fails (defensive —
        /// reverse-construction is valid by construction).
        /// </summary>
        public static LevelRecord Generate(int levelId)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                uint seed = attempt == 0 ? (uint)levelId : (uint)(levelId * 31 + attempt);
                var rng = new Rng(seed);

                LevelRecord record = BuildOne(levelId, ref rng);
                if (record != null && LevelRecordValidator.ValidateRecord(record, out _))
                    return record;
            }
            return null;
        }

        // ── Generation ──────────────────────────────────────────────────────────────────

        private static LevelRecord BuildOne(int levelId, ref Rng rng)
        {
            bool tall = rng.Range(0, 100) < TallTubeChancePct;

            int colorCount, tempSlotCount, tempSlotDepth, stackDepthField;
            int[] caps;        // capacity per color stack (length = colorCount)
            int[] tubeCaps;    // full flat capacities (color stacks + temp slots), null when uniform

            if (tall)
            {
                // 1 tall tube (depth 10 or 12); remaining color tubes shallow (depth 3 or 4).
                colorCount    = rng.Range(4, 6);          // 4–5 colors keeps the total reasonable
                tempSlotCount = rng.Range(2, 4);          // 2–3 temp slots for maneuvering room
                tempSlotDepth = 4;
                int tallIdx   = rng.Range(0, colorCount);
                int tallCap   = rng.Range(0, 2) == 0 ? 10 : 12;

                caps = new int[colorCount];
                for (int i = 0; i < colorCount; i++)
                    caps[i] = (i == tallIdx) ? tallCap : (rng.Range(0, 2) == 0 ? 3 : 4);

                stackDepthField = tallCap;
                tubeCaps = new int[colorCount + tempSlotCount];
                for (int i = 0; i < colorCount; i++) tubeCaps[i] = caps[i];
                for (int i = 0; i < tempSlotCount; i++) tubeCaps[colorCount + i] = tempSlotDepth;
            }
            else
            {
                // Uniform layout in the 170–200 band: 5–7 colors, depth 5–6, 2 temp slots.
                int rc = rng.Range(0, 10);
                colorCount    = rc < 2 ? 5 : (rc < 8 ? 6 : 7);   // weighted toward 6
                int depth     = rng.Range(0, 3) == 0 ? 5 : 6;     // mostly 6, sometimes 5
                tempSlotCount = 2;
                tempSlotDepth = 4;
                stackDepthField = depth;

                caps = new int[colorCount];
                for (int i = 0; i < colorCount; i++) caps[i] = depth;
                tubeCaps = null; // uniform ⇒ validator/GSM use stack_depth
            }

            int[][] colorStacks = ReverseConstruct(colorCount, caps, tempSlotCount, tempSlotDepth, ref rng);
            if (colorStacks == null) return null;

            int totalBalls = 0;
            for (int i = 0; i < colorCount; i++) totalBalls += caps[i];

            // Par tuned to the curated 170–200 envelope (≈2× ball count), not to the scramble length.
            int par = (int)System.Math.Round(totalBalls * (1.8 + 0.4 * rng.Unit()));
            if (par < 1) par = 1;

            bool mysteryFlag = false, multicolorFlag = false;
            FrozenSpec frozen = null;
            ApplyMechanics(levelId, colorCount, tempSlotCount, colorStacks, ref rng,
                           ref mysteryFlag, ref multicolorFlag, ref frozen);

            return Assemble(levelId, colorCount, stackDepthField, colorStacks, tempSlotCount,
                            tempSlotDepth, par, tubeCaps, frozen, mysteryFlag, multicolorFlag);
        }

        /// <summary>
        /// Builds a solved board (each color stack monochrome-full, temps empty) then applies legal
        /// solve-inverse moves. A reverse move pops a same-color-run top (or a singleton) and pushes
        /// it onto any tube with space; pushes into temps are restricted to keep temps monochrome so
        /// they are always drainable. Temps are drained back into the color stacks at the end, so the
        /// result has every color stack full and temps empty — and is guaranteed solvable by replaying
        /// the recorded forward moves in reverse. Returns the color stacks (bottom→top), or null if the
        /// scramble degenerated to the solved board.
        /// </summary>
        private static int[][] ReverseConstruct(int colorCount, int[] caps, int tempCount, int tempDepth, ref Rng rng)
        {
            int n = colorCount + tempCount;
            var tube = new List<int>[n];
            var cap  = new int[n];
            for (int i = 0; i < colorCount; i++)
            {
                cap[i]  = caps[i];
                tube[i] = new List<int>(caps[i]);
                for (int k = 0; k < caps[i]; k++) tube[i].Add(i + 1); // monochrome color (i+1)
            }
            for (int i = 0; i < tempCount; i++) { cap[colorCount + i] = tempDepth; tube[colorCount + i] = new List<int>(tempDepth); }

            int totalBalls = 0;
            for (int i = 0; i < colorCount; i++) totalBalls += caps[i];
            int targetMoves = totalBalls * 3;

            bool IsTemp(int idx) => idx >= colorCount;
            bool Poppable(int idx)
            {
                var t = tube[idx];
                int c = t.Count;
                return c >= 1 && (c == 1 || t[c - 1] == t[c - 2]);
            }

            var dsts = new List<int>(n);
            for (int move = 0; move < targetMoves; move++)
            {
                dsts.Clear();
                for (int d = 0; d < n; d++) if (Poppable(d)) dsts.Add(d);
                if (dsts.Count == 0) break;

                // Pick a pop source, preferring color tubes (peels colors apart) over temps.
                int dst = PickPreferringColor(dsts, colorCount, ref rng);
                int color = tube[dst][tube[dst].Count - 1];

                // Collect legal push targets; prefer mixing into other color tubes.
                int chosen = -1; int colorTargets = 0; int anyTargets = 0; int chosenAny = -1;
                for (int s = 0; s < n; s++)
                {
                    if (s == dst) continue;
                    if (tube[s].Count >= cap[s]) continue;
                    if (IsTemp(s) && tube[s].Count > 0 && tube[s][tube[s].Count - 1] != color) continue; // keep temps monochrome
                    anyTargets++;
                    if (rng.Range(0, anyTargets) == 0) chosenAny = s;
                    if (!IsTemp(s))
                    {
                        colorTargets++;
                        if (rng.Range(0, colorTargets) == 0) chosen = s;
                    }
                }
                int src = chosen >= 0 ? chosen : chosenAny;
                if (src < 0) continue;

                tube[dst].RemoveAt(tube[dst].Count - 1);
                tube[src].Add(color);
            }

            // Drain temps back into color-stack free space (always possible; temps are monochrome).
            for (int i = 0; i < tempCount; i++)
            {
                var t = tube[colorCount + i];
                while (t.Count > 0)
                {
                    int color = t[t.Count - 1];
                    int target = -1;
                    for (int c = 0; c < colorCount; c++)
                        if (tube[c].Count < cap[c]) { target = c; break; }
                    if (target < 0) return null; // unreachable: temp balls == color free space
                    t.RemoveAt(t.Count - 1);
                    tube[target].Add(color);
                }
            }

            // Reject a board that never left the solved arrangement.
            bool solved = true;
            for (int i = 0; i < colorCount && solved; i++)
            {
                var t = tube[i];
                if (t.Count != cap[i]) return null;
                for (int k = 0; k < t.Count; k++) if (t[k] != i + 1) { solved = false; break; }
            }
            if (solved) return null;

            var result = new int[colorCount][];
            for (int i = 0; i < colorCount; i++) result[i] = tube[i].ToArray();
            return result;
        }

        /// <summary>Picks an index from <paramref name="dsts"/>, favouring color tubes over temp slots.</summary>
        private static int PickPreferringColor(List<int> dsts, int colorCount, ref Rng rng)
        {
            int pick = -1, seen = 0;
            for (int i = 0; i < dsts.Count; i++)
                if (dsts[i] < colorCount) { seen++; if (rng.Range(0, seen) == 0) pick = dsts[i]; }
            if (pick >= 0) return pick;
            return dsts[rng.Range(0, dsts.Count)];
        }

        /// <summary>
        /// Injects special mechanics so ≥30% of levels carry at least one. Mechanics never change ball
        /// positions or counts (so solvability/validity are preserved): mystery hides a color as a
        /// negative id, multicolor replaces one ball with the wildcard 0, frozen locks a tube's
        /// deposits for 1–3 turns. Multicolor and frozen are never combined.
        /// </summary>
        private static void ApplyMechanics(int levelId, int colorCount, int tempCount, int[][] stacks,
            ref Rng rng, ref bool mysteryFlag, ref bool multicolorFlag, ref FrozenSpec frozen)
        {
            if (rng.Range(0, 100) >= SpecialMechanicChancePct) return;

            int roll = rng.Range(0, 100);
            bool wantMystery    = roll < 40 || roll >= 90;
            bool wantFrozen     = (roll >= 40 && roll < 70) || roll >= 90;
            bool wantMulticolor = roll >= 70 && roll < 90;

            if (wantMulticolor)
            {
                if (TryPickNormalCell(stacks, ref rng, out int si, out int ci))
                { stacks[si][ci] = 0; multicolorFlag = true; }
            }

            if (wantMystery)
            {
                int count = 1 + (rng.Range(0, 100) < 15 ? 1 : 0); // 1–2 mystery balls
                for (int m = 0; m < count; m++)
                    if (TryPickNormalCell(stacks, ref rng, out int si, out int ci))
                    { stacks[si][ci] = -stacks[si][ci]; mysteryFlag = true; }
            }

            if (wantFrozen && !multicolorFlag)
            {
                int tubeIndex = rng.Range(0, colorCount + tempCount);
                int turns     = rng.Range(1, 4); // 1–3 turns
                frozen = new FrozenSpec { Index = tubeIndex, Turns = turns };
            }
        }

        /// <summary>Picks a random cell currently holding a normal (positive) color id.</summary>
        private static bool TryPickNormalCell(int[][] stacks, ref Rng rng, out int si, out int ci)
        {
            si = ci = -1; int seen = 0;
            for (int s = 0; s < stacks.Length; s++)
                for (int c = 0; c < stacks[s].Length; c++)
                    if (stacks[s][c] > 0)
                    { seen++; if (rng.Range(0, seen) == 0) { si = s; ci = c; } }
            return si >= 0;
        }

        /// <summary>Serializes the fields to JSON and round-trips through Newtonsoft so the produced
        /// record is byte-for-byte schema-identical to one loaded from levels.json.</summary>
        private static LevelRecord Assemble(int levelId, int colorCount, int stackDepth, int[][] colorStacks,
            int tempSlotCount, int tempSlotDepth, int par, int[] tubeCaps, FrozenSpec frozen,
            bool mystery, bool multicolor)
        {
            var dict = new Dictionary<string, object>
            {
                ["level_id"]                 = levelId,
                ["difficulty_tier"]          = 5,
                ["schema_version"]           = 2,
                ["color_count"]              = colorCount,
                ["stack_depth"]              = stackDepth,
                ["color_stacks"]             = colorStacks,
                ["temp_slot_count"]          = tempSlotCount,
                ["temp_slot_depth"]          = tempSlotDepth,
                ["is_tutorial"]              = false,
                ["daily_challenge_eligible"] = false,
                ["par_moves"]                = par,
                ["added_version"]            = AddedVersion,
                ["mystery_balls"]            = mystery,
                ["has_multicolor"]           = multicolor,
            };
            if (tubeCaps != null) dict["tube_capacities"] = tubeCaps;
            if (frozen != null)
                dict["frozen_tubes"] = new[]
                {
                    new Dictionary<string, object> { ["tube_index"] = frozen.Index, ["turns"] = frozen.Turns }
                };

            string json = JsonConvert.SerializeObject(dict);
            return JsonConvert.DeserializeObject<LevelRecord>(json);
        }

        private sealed class FrozenSpec { public int Index; public int Turns; }

        /// <summary>
        /// Small xorshift PRNG. Deterministic and stable across .NET runtimes / IL2CPP, unlike
        /// <c>System.Random</c> — required so a given seed yields the identical level forever.
        /// </summary>
        private struct Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }

            public uint Next()
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return _s;
            }

            /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
            public int Range(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive) return minInclusive;
                return minInclusive + (int)(Next() % (uint)(maxExclusive - minInclusive));
            }

            /// <summary>Uniform double in [0,1).</summary>
            public double Unit() => (Next() & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
