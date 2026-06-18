# BoltSort — Mechanic Progression Plan (Phase-2 rollout)

_Date: 2026-06-18 · Governs the schema_version 2 content introduced by `rebalance_phase2.py`_

## Problem this fixes

Before this pass, **all 200 levels were `schema_version 1` and used zero mechanics** — the only
difficulty lever past L50 was *removing buffers*, which is the least fun lever and produced the
"impossible after 50" walls. The four Phase-2 mechanics were fully built (engine + both solvers +
BoardView rendering) but deployed nowhere.

This plan introduces variety **early and gently**, each mechanic taught in isolation on an easy
board before any harder reuse — the opposite of the old buffer cliff.

## Design principles

1. **Teach in isolation first.** Every mechanic's first appearance is a low-complexity board
   (high maneuvering room) so the *idea* is learned without difficulty noise.
2. **One new concept at a time.** Never stack two brand-new mechanics in the same level.
3. **Relief before restriction.** The multicolor wildcard (which *lowers* difficulty) comes first;
   restrictive mechanics (frozen, short tubes) come later.
4. **Gentle ramp for mixed capacity.** Asymmetric capacity is introduced as a *tall* (extra-large)
   buffer — pure relief/novelty — well before any *short* color-tube board, so "mixed capacity"
   first reads as helpful, not punishing.
5. **Every mechanic level is solver-proven** solvable with **0 extra tubes** and room ≥ 10
   (no tightropes). Par = `optimal + cushion` (same formula as the rest of the catalogue).

## Rollout bands

| Mechanic | First appearance (teaching) | Reinforcement | Combination / reuse | Notes |
|----------|----------------------------|---------------|---------------------|-------|
| **Multicolor wildcard** (id 0) | **L14** (c4 d5, easy) | L23, L37 | L66, L93 (relief inside hard bands) | Relief valve; earliest because it makes boards *easier*. Max 1 per level. |
| **Asymmetric — tall buffer** (extra-large tube) | **L24** (one cap-6 + one cap-4 buffer) | L38 (cap-7 buffer) | folded into later asym levels | Mixed capacity introduced as *help/novelty* first. Height lives in `tube_capacities`; `temp_slot_depth ≤ stack_depth` so the runtime guard passes. |
| **Asymmetric — short color tubes** (true mixed capacity) | **L114** (two cap-(d−1) color tubes) | L152 | future content | The "4×cap5 / 2×cap4" style — introduced only AFTER tall-buffer familiarity, and only at room ≥ 10. |
| **Frozen tube** (snowflake + counter) | **L29** (short 2–4 turn freeze, roomy board) | L46 | L134 | Restrictive; taught late-easy, short freeze only. Freeze is a pure fn of move count → undo-safe. |
| **Mystery ball** (hidden color) | **DEFERRED** | — | — | Blocked on fairness gap **L-1** (both solvers assume full information the player lacks). See audit §C. Do NOT ship until the solver models worst-case reveals or mystery placement is constrained so reveal order can't trap the player. |

## Why this order is better than today

- Players meet a fresh idea by **L14** instead of *never*.
- The first three novelties (wildcard, tall buffer, then short-tube) are spaced ~10 levels apart and
  each lands on an easy board, so the learning curve is flat per-mechanic.
- The old "difficulty = fewer buffers" crutch is replaced (the 1-buffer bands are gone, see
  ROOT_CAUSE_AUDIT.md Part A) by **variety-driven** interest.
- Mixed capacity — the exact thing that felt brutal — is now first seen as a *bigger* tube (a gift),
  defusing the negative association before the genuine short-tube challenge appears at L114.

## Verification — as-shipped result (2026-06-18)

**All 12 planned conversions landed** (all solver-proven, 0 extra tubes, room ≥ 10). See
`REBALANCE_REPORT.md` Part B for the per-level opt/par/room table. Summary:

- multicolor: **L14** (teach), L23, L37, L66, L93 ✅
- asymmetric tall buffer: **L24** (teach, cap-6), L38 (cap-7) ✅
- asymmetric mixed color tubes: **L114** (4×cap5/2×cap4), L152 ✅
- frozen: **L29** (teach), L46, L134 ✅
- mystery: not authored (blocked on L-1).

L66 and L134 were initially skipped by `rebalance_phase2.py`'s fail-fast search (their heavier
bands needed > 120k nodes just to *prove* a roomy solution optimal) and were then recovered by
`chase_deferred.py` at a 1.5M-node cap — L66 multicolor room 14218, L134 frozen (buffer tube,
2-turn freeze) room 2977. Any target that cannot be made solvable-with-good-room with 0 extra tubes
is dropped rather than shipped — correctness over coverage.
