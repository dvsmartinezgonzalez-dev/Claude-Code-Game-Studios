# BoltSort — Level Authoring & Validation Tools

Python tooling that keeps the level catalogue (`My project/Assets/Resources/levels.json`)
**varied, solvable, and honestly progressive**. Built after a diagnosis found the
original 30 levels were really only **7 distinct puzzles** (the rest were the same
"staircase / cyclic-shift" template under color relabeling and tube reordering),
with several `par_moves` set *below* the optimal solution (making 3 stars impossible).

No third-party dependencies. Python 3.9+.

## The puzzle model (mirrors the runtime exactly)

A level is `color_count` color stacks (each holds `stack_depth` bolts) plus
`temp_slot_count` temp slots (each holds `temp_slot_depth`). Temp slots **start
empty**, so the starting board is always fully-packed color stacks — this is forced
by the engine (total bolts = `color_count × stack_depth` over exactly `color_count`
stacks), not a bug.

- **Move**: take the top bolt of a column; drop it on an empty column, or on one
  whose top bolt matches and isn't full.
- **Win**: every column is empty, or full-to-capacity and a single color.

## Files

| File | What it does |
|------|--------------|
| `boltsort_levels.py` | Core engine: rules simulator, **optimal A\* solver**, **canonical structural signature** (the duplicate detector), and a **difficulty score**. |
| `generate_levels.py` | Authoring tool. Produces a structurally-varied, solvable, duplicate-free 50-level curve and writes `candidate_levels.json`. Deterministic (fixed seed). |
| `validate_levels.py` | The re-runnable guard. Validates any `levels.json`: solvable, no exact/structural duplicates, `par_moves` achievable & sane, rising curve, no absurd sawtooth. CI-friendly exit codes. |

## Usage

```bash
cd tools/levels

# Validate the shipped catalogue (run this after ANY edit to levels.json)
python validate_levels.py

# Validate a specific file; --strict also fails on advisory (curve) findings
python validate_levels.py "../../My project/Assets/Resources/levels.json" --strict

# Regenerate the candidate catalogue from scratch (deterministic)
python generate_levels.py
```

`validate_levels.py` exits non-zero on any BLOCKING failure, so it can gate CI.

## How equivalence ("the same puzzle in disguise") is detected

`canonical_signature()` minimises, over **every color bijection**, the **sorted
multiset of filled tubes**, with the buffer envelope folded in. Two levels with the
same signature are the same challenge even if colors were swapped or tubes reordered.
This is the exact transform group the original catalogue abused, so the validator —
and the C# EditMode test `CatalogueIntegrityTest` — now reject it.

## Difficulty model

`difficulty_score()` blends the solver's **optimal move count** (dominant) with
structural "hardness per move" features: color count, depth, buffer scarcity /
restriction, color dispersion, and buried depth. It is a *consistent ordering
heuristic*, not a physical unit — used to shape a rising curve and flag sawtooth.

## Difficulty curve (levels 1–50)

The first 50 levels are tuned to sit at roughly **one third** of the eventual
difficulty ceiling of the planned ~150-level game. Variety comes from **scramble
structure** (distribution, buried colors, dispersion, traps) and gentle color/depth
growth — never from a repeated template. Two **full buffers** are the baseline
throughout; **single buffers and aggressively-restricted buffers are reserved for
levels 51+** so there is real headroom left to escalate. Breather levels (every 10th)
dip one notch for pacing.

The C# mirror of these checks lives in:
- `Assets/_Project/Editor/LevelSolver.cs` — optimal/solvable solver (symmetry-reduced)
- `Assets/_Project/Editor/LevelEquivalence.cs` — canonical signature
- `Assets/_Project/Tests/EditMode/LevelSolvabilityTest.cs` — solvability gate
- `Assets/_Project/Tests/EditMode/CatalogueIntegrityTest.cs` — dedup + par-achievable gate
