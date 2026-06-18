# BoltSort — Root-Cause Audit: Solvability & Mechanic Progression

_Date: 2026-06-18 · Branch: feat/phase2-levels-mechanics_
_Sources audited: SortMechanic.cs, GameStateManager.cs, LevelRecord.cs, LevelRecordValidator.cs,
LevelSolver.cs (Editor), boltsort_levels.py, validate_levels.py, Resources/levels.json (200 levels)_

> Scope: this is the Phase-1 deliverable. It establishes ground truth BEFORE any data change.
> The headline is that the reported premise is partly inaccurate, and the real defects are
> different from (and more fixable than) what was assumed.

---

## TL;DR — what is actually true

1. **There is NO validator-vs-runtime solvability mismatch for the shipped catalogue.**
   Three independent rule engines — runtime `SortMechanic.cs`, Python `boltsort_levels.py`,
   and Editor `LevelSolver.cs` — implement the **same** move/win/capacity/frozen/wildcard/mystery
   model. For the shipped levels (all `schema_version: 1`, classic sort) the Python A* full-
   reachability search is an exact oracle. Its pruning can only ever cause **false negatives**
   (miss a solution), never **false positives** (invent an illegal win). So a "solvable" verdict
   on a v1 level is trustworthy.

2. **No shipped level uses any special mechanic.** All 200 levels are `schema_version: 1`:
   0 asymmetric (`tube_capacities`), 0 frozen, 0 mystery, 0 multicolor. The four Phase-2
   mechanics are fully implemented in engine + both solvers but **deployed by zero levels.**

3. **"Mixed tube capacities at ~L82" does not exist in the data.** L82 is `c7 d5`, one depth-5
   buffer, 8 columns all depth 5 (`tube_capacities: null`). The "4 tubes cap 5 / 2 tubes cap 4"
   perception is the **restricted-buffer bands (L61–80)**, where buffer tubes are visibly
   shorter (depth 3–4) than the color tubes (depth 5). That is a *shape* lever, not the
   asymmetric-capacity *mechanic*.

4. **The real defect is FAIRNESS, not solvability.** Difficulty past L50 is driven almost
   entirely by **buffer starvation** (2 full buffers → 1 full buffer at L51, then buffer depth
   5→4→3 across L61–80, then a 7th color at L81). These configurations are solvable but have
   near-zero maneuvering room — a normal player soft-locks unless they play near-optimally.
   That is what "feels impossible."

5. **The mechanic-progression complaint is correct, but stronger than stated:** mechanics
   aren't "introduced too late" — they're **never introduced at all.** The only difficulty lever
   past L50 is taking buffers away, which is the least fun and most frustrating lever available.

---

## A. Runtime rules (ground truth) — extracted

| Rule | Runtime source | Behaviour |
|------|----------------|-----------|
| Move unit | `SortMechanic.DispatchIdleIndexedTap` | single **top** bolt only |
| Legal move | `SortMechanic.IsLegalMove` | dst empty → ok; `dst.Count >= cap` → full; else `ColorsMatch(top,held)` |
| Capacity | `GSM.GetColumnCapacity(i)` | per-tube `_columnCapacities[i]` if present, else `stackDepth` (color) / `tempSlotDepth` (temp) |
| Win | `SortMechanic.IsWon` | every column empty **or** `count==capacity` **and** mono (`AllSameColor`) |
| Frozen | `GSM.GetFreezeRemaining` | `max(0, freezeInit[i] - moveCount)`; deposits banned while >0; **restored by undo** (pure fn of moveCount) |
| Wildcard (0) | `ColorsMatch` / `AllSameColor` | matches any color, both placement and win |
| Mystery (<0) | `GSM.RevealMysteryIfExposed` | stored negative; flips to `abs` and fires reveal when it becomes a top; compared by `abs` |
| Extra tube | `GSM.ApplyExtraTube` | appends/grows a helper temp column at END of flat namespace; reset on reload; **optional** |

## B. Python validator model — extracted

`boltsort_levels.Board` mirrors every rule above: single-bolt `legal_moves`, per-tube `caps`
from `tube_capacities`, `is_won` = empty-or-full-mono, `frozen` decremented per `apply`,
`_match`/`_complete_colors` wildcard, mystery normalised to `abs` at load. `solve_optimal` is a
full A* over the de-duplicated reachable state space — **reachability, not "legal moves remain."**

## C. Parity comparison — mismatch report

| Aspect | Runtime | Python | Editor C# | Verdict |
|--------|---------|--------|-----------|---------|
| Move unit / legality | single bolt | single bolt | single bolt | **MATCH** |
| Per-tube capacity | ✅ | ✅ | ✅ | **MATCH** |
| Win condition | empty/full-mono | empty/full-mono | empty/full-mono | **MATCH** |
| Frozen decrement | per move, undo-restored | per `apply` | per move | **MATCH** (semantically) |
| Wildcard | ✅ | ✅ | ✅ | **MATCH** |
| Mystery (solver) | hidden until reveal | **full info (abs at load)** | **full info** | **DIVERGENCE — latent** |
| Column cap | 18 (ADR-0014) | **8 (`MAX_COLUMNS`, stale ADR-0013)** | n/a | **VALIDATOR BUG — latent** |
| v2 bolt-count invariant | capacity-sum aware | **`schema_ok` assumes `cc*sd` + freq==sd** | n/a | **VALIDATOR BUG — latent** |
| Data source | Addressable `levels.json` → Resources/levels.json | same file | LevelRecord | **MATCH (single source)** |

### Latent issues (NOT affecting the current v1 catalogue, but they WILL the moment Phase-2 ships)

- **L-1 — Mystery full-information false positive.** Both solvers assume perfect knowledge of
  hidden colors. A mystery level can be "solvable" to the solver yet impossible/luck-only for a
  player who cannot see them. **Must be addressed before any mystery level ships** (e.g. restrict
  mystery so reveal order can't create an information trap, or model worst-case reveals).
- **L-2 — `validate_levels.py` `MAX_COLUMNS = 8`.** Runtime allows 18. The validator would
  wrongly BLOCK legitimate ≤18-column levels. Stale.
- **L-3 — `validate_levels.py` `schema_ok` is v1-only.** It hard-checks `sum==cc*sd` and per-color
  `freq==sd`. The runtime's `RunInvariantChecksV2` uses the capacity-sum invariant and does NOT
  enforce per-color frequency. Authoring a v2 asymmetric/wildcard level → validator BLOCKS a level
  the runtime accepts. Directly blocks the "introduce mechanics" goal.

These three are the only genuine validator/runtime divergences found, and all are **latent**
(triggered by future v2 content), not the cause of today's "impossible" feeling.

---

## D. Root cause of "solvable but impossible-feeling"

Not unsolvability. **Maneuvering-room collapse via buffer starvation.**

### Empirical ground-truth run (independent re-solve, all 200 levels)

Full A* reachability, `node_cap=400000`, 552s, ZERO extra tubes:

- **UNSOLVABLE: NONE. CAPPED: NONE.** Every shipped level is genuinely beatable with 0 extra tubes.
- The decisive signal is **maneuvering room = nodes_explored / optimal_moves** (the difficulty
  audit established ~60–200 nodes == tight, thousands == roomy). Low room = the search space is
  almost a single forced line = a human soft-locks unless they play near-optimally.

| Band | Shape | room (nodes/opt) | Verdict |
|------|-------|------------------|---------|
| L1–50 | c2→6, d3→5, **2 full buffers** | 3 → 205 (rises, roomy by L30) | healthy ramp |
| **L51–60** | c6 d5, **1 full buffer** | **1.5 – 5.4** | ⛔ **brittle wall** (e.g. L55: opt 40, nodes **59**) |
| L61–80 | c6 d5, 2 restricted buffers (d4→d3) | 100 – 849 | roomy — restriction is *less* tight than 1 buffer |
| **L81–100** | c7 d5, **1 full buffer** | **2.9 – 7.7** | ⛔ **brittle wall** (e.g. L100: opt 54, nodes **154**) |
| L101–200 | c6 d6, 2 buffers (d5→d4) | 228 – 2517 | roomy — "advanced" but fair |

**31 levels flagged brittle (room < 8); the meaningful clusters are the two 1-FULL-BUFFER bands
L51–60 and L81–100 (~29 levels).** This is the exact span the report calls "impossible after 50"
and "the wall around 82." Note L82 itself: opt 48, nodes **234**, room 4.9 — solvable, but a near-
single-line tightrope, which is why it *feels* impossible even though it is not.

**The single structural culprit: the 1-full-buffer configuration.** It is the least-forgiving
non-mechanic layout in the game. Restricted buffers (2× shallow) and deep boards (d6, 2 buffers)
are far roomier. Difficulty-by-move-count is fine (4→23→32→54→68 is a reasonable curve); difficulty-
by-*room* is what's broken, and only in the 1-buffer bands.

### Fix direction (targeted, low-risk — detail/approval in the rebalance plan)

1. **Eliminate the 1-buffer bands.** Give L51–60 and L81–100 a **second buffer** (or a deeper
   single buffer) so room returns to the 2-buffer regime. This smooths the curve without touching
   move-count progression and without rewriting scrambles.
2. **Validator parity fixes** (latent L-2/L-3) so future v2 mechanic levels validate against the
   real runtime rules — applied in this pass.
3. **Mechanic progression** is a separate, additive workstream (mechanics are 100% unused today);
   it requires resolving the mystery full-info fairness gap (L-1) first.

> Conclusion: there is **no fake "all solvable"** here — they provably are. The defect the player
> feels is **fairness/room**, isolated to two 1-buffer bands, plus a total absence of mechanic
> variety. Both are fixable without changing core rules.
