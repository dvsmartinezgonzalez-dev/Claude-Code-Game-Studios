# BoltSort — Post-Rebalance Validation Report

_Date: 2026-06-18 · Tool: `validate_levels.py` (parity-fixed) on `Resources/levels.json` (200 levels)_

## Verdict: ✅ BLOCKING — all checks passed

Authoritative full re-validation after the Part A + Part B changes. The validator runs a complete
A* **reachability** search per level (not a "legal moves remain" heuristic) using the real in-game
rules, with **zero extra tubes**.

| Check | Meaning | Result |
|-------|---------|--------|
| B1 schema | bolt-count / color-domain / column-cap / temp-depth (v1 & v2 aware) | ✅ 200/200 |
| **B2 solvable** | **full reachability search reaches a win — 0 extra tubes** | ✅ **200/200** |
| B3 par achievable | `par_moves ≥ optimal` (3 stars reachable) | ✅ 200/200 |
| B4 par sane | `par_moves ≤ optimal + 10` | ✅ 200/200 |
| B5 no exact dupes | no two levels share an identical board | ✅ |
| B6 no equivalents | no two share a canonical signature (relabel/reorder) | ✅ |

**Every shipped level is provably beatable with 0 extra tubes, 0 mechanics-it-doesn't-declare, and
only legal player moves.** This includes the 10 new schema_version-2 mechanic levels (each also
re-solved independently during authoring) and the 30 buffer-fixed levels.

## Advisory (5, non-blocking — all intentional)

| Advisory | Why it's expected |
|----------|-------------------|
| L113→L114 difficulty drop >30% | L114 is the mixed-capacity **variety breather** (c6 d5, lighter than its c6 d6 neighbours). Drops relax the player — the opposite of the frustration we removed. |
| L151→L152 difficulty drop >30% | Same: L152 mixed-capacity breather. |
| L50 / L80 / L100 breather not below a neighbour | Band-boundary breathers sitting just under the *next* band's opening level (a new, harder shape). Within normal curve noise. |

No advisory indicates a difficulty **spike**; all are drops or boundary effects.

## Parity note (validator now matches runtime)

The validator was brought into parity with the runtime in this pass (audit §C latent bugs):
- `MAX_COLUMNS` 8 → 18 (ADR-0014, matches `SortMechanic.MaxColumnCount`).
- `schema_ok` is now schema-version-aware: v2 uses the capacity-sum invariant + wildcard/mystery
  token domain (mirrors `GameStateManager.RunInvariantChecksV2` / `LevelRecordValidator.V2`),
  instead of the v1-only `cc*sd` + per-color-frequency rule that would have wrongly blocked
  legitimate asymmetric/wildcard levels.

Remaining known gap (latent, not triggered by shipped content): **L-1** — both solvers assume full
information for **mystery** balls. No mystery level ships; do not author one until the solver models
worst-case reveals or mystery placement is constrained. Tracked in `ROOT_CAUSE_AUDIT.md`.

## Reproduce

```
cd tools/levels
python validate_levels.py            # B1-B6, exit 0 == all blocking pass
python fairness_audit.py             # maneuvering-room / frustration instrumentation
python rebalance_phase2.py --part AB --dry-run   # show the exact changes, write nothing
```
Original catalogue preserved at `Resources/levels.json.bak`.
