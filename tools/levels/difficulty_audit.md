# BoltSort — Difficulty Audit (Levels 1–100)

_Date: 2026-06-16 · Source: `My project/Assets/Resources/levels.json` · Solver: `boltsort_levels.solve_optimal` (A*, optimal move count)_

This is a **pacing / QA** audit, not a solvability audit. Every level 1–200 is
already confirmed solvable. The question here is whether the **difficulty curve**
is appropriate for a relaxing game — i.e. does any early/mid level *feel* like a
late-game wall.

## Method

For each level we measured the solver's **optimal move count** and the calibrated
`difficulty_score` (move count modulated by colors, depth, buffer scarcity, color
dispersion and buried depth). Outliers are levels whose optimal move count rises
well above their **shape-band neighbourhood**, plus any breather (id % 10 == 0)
that is not actually a relief.

## Shape progression (the real difficulty engine)

Difficulty is driven entirely by the **shape** of each 10-level band — color
count, stack depth, and especially **buffer scarcity**. No special mechanics are
used anywhere (see `mechanic_coverage.md`).

| Band | Shape | Note |
|------|-------|------|
| 1–13 | c2–4, d3–4, 2 buffers (full) | gentle onboarding |
| 14–50 | c4–6, d5, **2 full buffers** | comfortable mid-game |
| 51–60 | c6, d5, **1 full buffer** | ⚠ buffer drops 2→1 — difficulty cliff |
| 61–70 | c6, d5, 2 buffers depth 4 (restricted) | |
| 71–80 | c6, d5, 2 buffers depth 3 (heavily restricted) | |
| 81–100 | c7, d5, 1 full buffer | 7th color |

**Systemic finding — the 2→1 buffer cliff at L51.** Levels 1–50 always give the
player two buffer tubes. At L51 that drops to a single buffer with 6 colors — the
least forgiving non-mechanic configuration in the catalogue. The solver finds
these boards in very few nodes (≈60–200 vs ≈4–6k for the 2-buffer band), which
means there is almost **no maneuvering room**: a human must play near-optimally
or soft-lock. That is why the 51–60 band *feels* much harder than its move count
alone suggests, and why L54 reads as a wall. This is the root cause behind the
user's report.

> Recommendation (NOT executed — out of scope for "targeted regeneration"):
> consider easing the cliff by giving L51–L55 a slightly deeper single buffer or
> a transitional 2-buffer-depth-4 band before the pure 1-buffer band. Flagged for
> a future pass; the spikes below were fixed in place without changing structure.

## Outliers flagged (levels 1–100)

| Level | Old opt | Neighbourhood | Why it's an outlier |
|-------|--------:|---------------|---------------------|
| **53** | 42 | L51=35, L52=34 | Optimal jumps +7–8 moves on the *third* level after the 1-buffer cliff — too steep, too early. |
| **54** | 40 | L52=34, L55=40 | **User-priority.** 1-buffer + opt 40 + high buried depth = unforgiving wall at a relaxing point. |
| **60** | 46 | L59=45, L61=39 | **Breather** (id % 10) but was the *hardest* level in its band — broke the relief contract. |
| **70** | 47 | L69=45, L71=34 | **Breather** but tied band-max (47) — also broke the relief contract. |
| **76** | 54 | L75=40, L77=44 | **Worst outlier in 1–100**: optimal 54 vs neighbourhood ≈43 (+26%). |

Borderline (left in place, advisory only):
- **L65** opt 47 — mid-band local peak in 61–70 (+12% over band median). Within
  noise for a ramping band; not regenerated.
- **L57 / L59** opt 44–45 — late in the 51–60 ramp; consistent with rising
  difficulty toward the band's breather, so acceptable.

## Action taken (targeted, deterministic regeneration)

Tool: `rebalance_outliers.py` (fixed per-level seeds → reproducible). Only the
flagged levels' **scrambles** were replaced; every shape and all other levels are
untouched. `par_moves` recomputed with the catalogue's standard cushion formula.

| Level | opt: old → new | par: old → new | Effect |
|-------|----------------|----------------|--------|
| 53 | 42 → 37 | 47 → 42 | smooth early-band ramp (35,34,**37**,…) |
| 54 | 40 → 36 | 45 → 41 | clearly eased; no longer a wall |
| 60 | 46 → 33 | 51 → 38 | now a genuine breather (below both neighbours) |
| 70 | 47 → 32 | 52 → 37 | now a genuine breather |
| 76 | 54 → 44 | 59 → 49 | brought in line with its 71–80 band |

All five replacements were re-validated by the full solver (see
`validate_levels.py` output) — solvable, unique, par achievable, no duplicates.
