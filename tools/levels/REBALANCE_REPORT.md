# BoltSort — Rebalance & Progression Report (Phase-2)

_Date: 2026-06-18 · Tool: `rebalance_phase2.py` (deterministic) · Backup: `levels.json.bak`_
_Companion docs: `ROOT_CAUSE_AUDIT.md` (why), `MECHANIC_PROGRESSION.md` (mechanic plan)_

## What changed, in one line

The two **1-full-buffer difficulty walls** (L51–60, L81–100) were given a second buffer to
restore maneuvering room, and **10 levels** were converted to schema_version 2 to introduce
mechanic variety early — all proven solvable with **0 extra tubes**.

---

## Part A — fairness fix (the actual "impossible after 50" cause)

Root cause (see audit): every level was solvable, but the 1-buffer bands had near-zero
**maneuvering room** (`nodes/optimal`), making them single-line tightropes. Fix: `temp_slot_count
1 → 2` (a second full-depth buffer). Same scramble, far more room. Each board was re-solved with
0 extra tubes and `par` recomputed (`opt + cushion`).

| Band | before room | after room | before opt | after opt | Effect |
|------|------------:|-----------:|-----------:|----------:|--------|
| L51–60 (c6 d5) | **1.5 – 5.4** | **54 – 191** | 33–45 | 25–30 | wall removed |
| L81–100 (c7 d5) | **2.9 – 7.7** | **127 – 428** | 45–54 | 32–37 | wall removed |

Representative levels (the worst offenders):

| Level | room before → after | opt before → after | par before → after |
|-------|--------------------:|-------------------:|-------------------:|
| L55 | 1.5 → 170 | 40 → 29 | 45 → 34 |
| L59 | 2.4 → 91 | 45 → 30 | 50 → 35 |
| L82 | 4.9 → 310 | 48 → 32 | 53 → 37 |
| L100 | 2.9 → 234 | 54 → 37 | 59 → 42 |

30/30 levels fixed, 0 failures. Maneuvering room is now in the same healthy regime as the rest of
the catalogue (the L101–200 deep bands run 228–2517; these now sit at 50–430).

### Curve impact
Move-count progression stays monotone overall and now ramps **within** each former-wall band instead
of spiking. L51–60 and L81–100 are slightly easier than their immediate predecessors (a deliberate
"new-page breather" after the page-1/page-2 climbs), then re-ramp. No level got harder.

---

## Part B — early mechanic progression (schema_version 2)

Mechanics were 100% unused before this pass. 10 levels now introduce three mechanics, each taught
on an easy board first (room well above the brittleness threshold). Mystery deferred (audit L-1).

| Level | Mechanic | opt | par | room | Role |
|-------|----------|----:|----:|-----:|------|
| L14 | multicolor wildcard | 19 | 24 | 884 | teaching |
| L23 | multicolor | 24 | 29 | 216 | reinforce |
| L37 | multicolor | 30 | 35 | 561 | reinforce |
| L93 | multicolor | 34 | 39 | 2650 | relief inside hard band |
| L24 | asymmetric — cap-6 tall buffer | 30 | 35 | 848 | teaching (extra-large tube) |
| L38 | asymmetric — cap-7 tall buffer | 38 | 43 | 2984 | reinforce |
| L114 | asymmetric — **4×cap5 / 2×cap4** color tubes | 24 | 29 | 143 | mixed-capacity (the feared layout, now fair) |
| L152 | asymmetric — mixed color tubes | 25 | 30 | 130 | reinforce |
| L29 | frozen tube (3-turn) | 30 | 35 | 105 | teaching |
| L46 | frozen tube | 31 | 36 | 185 | reinforce |

**2 planned slots deferred** (no fair variant within the fail-fast solve budget; both remain their
original v1 levels — nothing broken):
- L66 multicolor (restricted-buffer band too tight to keep room ≥ 10 with a wildcard added)
- L134 frozen (deep c6d6 board became too hard once a tube was frozen)

Each mechanic still ships with a teaching + at least one reinforcement level, so the deferrals do
not break the progression.

### Why the mixed-capacity introduction is now correct
The exact "4 tubes cap 5 / 2 tubes cap 4" layout that felt impossible is now **L114** — but it is
(a) preceded at L24/L38 by friendly *tall-buffer* asymmetric levels so the concept is already
familiar, (b) solver-proven solvable with 0 extra tubes at room 143 (no tightrope), and (c) a c6 d5
board, lighter than its c6 d6 neighbours — a variety breather, not a spike.

---

## Safety / no-regression

- **Solvability:** every changed level individually re-solved (0 extra tubes) before commit; full-
  catalogue re-validation verdict appended to `VALIDATION_REPORT.md`.
- **Runtime guards:** 0 violations — all `temp_slot_depth ≤ stack_depth` (tall-tube height lives in
  `tube_capacities`), all column counts ≤ 18, all `tube_capacities` lengths = total columns.
- **Rendering:** BoardView already renders all three shipped mechanics (frozen snowflake+counter,
  capacity-tiered tube sprites incl. tier-4 for cap ≥ 7, animated multicolor ball).
- **Extra tubes remain optional** — no level requires them; they are still pure accessibility aid.
- **Win detection / special-ball handling** unchanged (no rule edits).
