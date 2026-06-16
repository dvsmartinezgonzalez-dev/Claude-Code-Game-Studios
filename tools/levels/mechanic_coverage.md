# BoltSort — Mechanic Coverage Report (Levels 1–200)

_Date: 2026-06-16 · Source: `My project/Assets/Resources/levels.json`_

## Headline finding

**No authored level uses any special mechanic.** All 200 levels are
`schema_version: 1` and rely solely on **shape** (color count, stack depth, and
buffer count/depth) for difficulty.

| Mechanic | Solver support | Runtime support | Levels using it |
|----------|:--------------:|:---------------:|-----------------|
| **Mystery** ball (negative color id) | ✅ | ✅ | **none** |
| **Multicolor / wildcard** ball (color id 0) | ✅ | ✅ (sprite-sheet animated) | **none** |
| **Frozen** tube (`frozen_tubes`) | ✅ | ✅ | **none** |
| **Asymmetric** capacity (`tube_capacities`) | ✅ | ✅ | **none** |

The Phase-2 mechanics are fully implemented in the engine and the Python solver
(`boltsort_levels.Board` understands all four), and the multicolor ball even has
finished art (commit `110fd57`) — but **no level in the shipping catalogue
deploys them.** If you want to manually test multicolor / frozen / asymmetric
behaviour, there is currently **no level to test them on**; authored content
would have to be created first.

## What actually provides progression today

Difficulty escalates purely through shape bands:

| Bands | Lever pulled |
|-------|--------------|
| 1–13 | color count 2→4, depth 3→4 (onboarding) |
| 14–50 | color count → 6, depth → 5, two full buffers |
| 51–60 | **buffer count 2 → 1** (scarcity) |
| 61–80 | **buffer depth 5 → 4 → 3** (restriction) |
| 81–100 | **color count → 7** |
| 101–200 | depth 5 → 6, repeated buffer scarcity/restriction at higher color/depth |

## Are mechanics introduced progressively / too abruptly?

- **Special mechanics:** N/A — none are introduced at all. There is no abrupt
  multicolor/frozen/asymmetric spike because there is no multicolor/frozen/
  asymmetric content.
- **Shape "mechanics":** the one genuinely abrupt transition is the **2→1 buffer
  cliff at L51** (see `difficulty_audit.md`). Buffer-depth restriction (61–80)
  and the 7th color (81+) are introduced one lever at a time, which is fine.

## Recommendations (no changes made — report only, per request)

1. **Decide whether Phase-2 mechanics ship at all in 1–200.** Right now they are
   dead capabilities. Either author levels that introduce them, or document that
   they are reserved for a later content drop.
2. **If they ship:** introduce each mechanic on a *breather/low-complexity* board
   first (a "teaching" level) before combining it with high buffer scarcity, so
   the mechanic itself is learned in isolation — the opposite of the L51 cliff.
3. **QA path is ready now:** `export_solutions.py` already tags every level with
   its mechanics and emits an optimal move list, so the moment any mechanic-using
   level is authored it becomes manually verifiable with zero extra work.
