"""
BoltSort level generator for levels 101-200 (Phase-2 tier 4/5 expansion).

Why this exists (and supersedes the 101-200 portion of generate_levels_51_200.py):
the original 101-200 band table was infeasible for the random-deal + optimal-solver
pipeline. Two independent defects:
  * 101-110 used (7,5,2,4) -> 7+2 = 9 columns, over the 8-column cap (ADR-0013).
  * every depth-6 single-buffer band (e.g. (7,6,1,6), (6,6,1,6)) and 7-color depth-6
    bands (only t1 is legal under the cap) produce random full deals that are almost
    never solvable, so the pool can never fill.

Measured feasible high-difficulty shapes (random-deal solvable yield):
    6c6d t2x5  -> ~100% yield, optimal 46-55, difficulty score ~159-178
    6c6d t2x4  -> ~17%  yield, optimal 52-58, difficulty score ~182-198
Both keep cc+tc = 8 (at the cap) and depth 6 (one deeper than the 1-100 catalogue),
so 101-200 reads as a genuine tier-4/5 step up in buffer restriction.

Curve: 101-150 use 6c6d t2x5, 151-200 use 6c6d t2x4 -> monotonic rise 159 -> 198.
Per-band percentile 0.45 -> 0.85 gives a gentle intra-band rise; L200 is the
capstone (0.95). Each level_id is seeded independently of the chunk it runs in, so
the catalogue can be generated in parallel chunks and is bit-for-bit reproducible.

Run a chunk:  python generate_levels_101_200.py START END OUT.json
Full range:   python generate_levels_101_200.py 101 200 candidate_levels_101_200.json
"""

from __future__ import annotations

import json
import random
import sys
from dataclasses import dataclass
from typing import List, Tuple

from boltsort_levels import (
    Board, LevelShape, solve_optimal, canonical_signature, difficulty_score,
)

ADDED_VERSION = "2026.06"
SCHEMA_VERSION = 1
MASTER_SEED = 20260616
EXISTING_CATALOGUE = "../../My project/Assets/Resources/levels.json"
FIRST_ID = 101
LAST_ID = 200
NODE_CAP = 800_000

# (color_count, stack_depth, temp_slot_count, temp_slot_depth) per 10-level band,
# bands 0..9 covering ids 101..200. Both shapes are measured-feasible and sit at
# the 8-column cap with depth 6.
BANDS: List[Tuple[int, int, int, int]] = [
    (6, 6, 2, 5),   # 101-110  restricted double buffer, depth 6   (score ~159-178)
    (6, 6, 2, 5),   # 111-120
    (6, 6, 2, 5),   # 121-130
    (6, 6, 2, 5),   # 131-140
    (6, 6, 2, 5),   # 141-150
    (6, 6, 2, 4),   # 151-160  heavily restricted double buffer    (score ~182-198)
    (6, 6, 2, 4),   # 161-170
    (6, 6, 2, 4),   # 171-180
    (6, 6, 2, 4),   # 181-190
    (6, 6, 2, 4),   # 191-200  capstone
]
assert len(BANDS) * 10 == LAST_ID - FIRST_ID + 1


def band_for(level_id: int) -> Tuple[int, int, int, int]:
    return BANDS[(level_id - FIRST_ID) // 10]


def percentile_for(level_id: int) -> float:
    if level_id == LAST_ID:
        return 0.95
    return 0.45 + (0.85 - 0.45) * (((level_id - FIRST_ID) % 10) / 9.0)


def level_seed(level_id: int) -> int:
    # independent per-id seed -> chunk-order-invariant, reproducible
    return (MASTER_SEED * 1_000_003 + level_id * 7919) & 0x7FFFFFFF


def tier_for(level_id: int) -> int:
    return 4 if level_id <= 150 else 5


def random_deal(shape: LevelShape, rng: random.Random) -> List[Tuple[int, ...]]:
    bag: List[int] = []
    for c in range(1, shape.color_count + 1):
        bag += [c] * shape.stack_depth
    rng.shuffle(bag)
    d = shape.stack_depth
    return [tuple(bag[i * d:(i + 1) * d]) for i in range(shape.color_count)]


def staircase_signature(shape: LevelShape):
    n, d = shape.color_count, shape.stack_depth
    stacks = [tuple(((i + j) % n) + 1 for j in range(d)) for i in range(n)]
    rec = {"color_count": n, "stack_depth": d,
           "temp_slot_count": shape.temp_slot_count, "temp_slot_depth": shape.temp_slot_depth,
           "color_stacks": stacks}
    return canonical_signature(rec)


def trivial_floor(shape: LevelShape) -> int:
    return max(3, round(shape.total_bolts * 0.45))


@dataclass
class Candidate:
    stacks: List[Tuple[int, ...]]
    optimal: int
    score: float
    sig: tuple


def load_existing_signatures() -> set:
    with open(EXISTING_CATALOGUE, "r", encoding="utf-8") as fh:
        cat = json.load(fh)
    return {canonical_signature(rec) for rec in cat["levels"]}


def make_level(level_id: int, existing_sigs: set) -> dict:
    cc, sd, tc, td = band_for(level_id)
    shape = LevelShape(cc, sd, tc, td)
    rng = random.Random(level_seed(level_id))
    stair_sig = staircase_signature(shape)
    floor = trivial_floor(shape)

    restricted = td < sd
    pool_target = 3
    max_tries = 400 if restricted else 80

    pool: List[Candidate] = []
    seen_local = set()
    tries = 0
    while len(pool) < pool_target and tries < max_tries:
        tries += 1
        stacks = random_deal(shape, rng)
        rec = {"color_count": cc, "stack_depth": sd, "temp_slot_count": tc,
               "temp_slot_depth": td, "color_stacks": stacks}
        sig = canonical_signature(rec)
        if sig == stair_sig or sig in existing_sigs or sig in seen_local:
            continue
        board = Board.from_record(rec)
        if board.is_won():
            continue
        res = solve_optimal(board, node_cap=NODE_CAP)
        if res.capped or not res.solvable or res.optimal_moves is None:
            continue
        if res.optimal_moves < floor:
            continue
        seen_local.add(sig)
        pool.append(Candidate(stacks, res.optimal_moves,
                              difficulty_score(rec, res.optimal_moves), sig))

    if not pool:
        raise RuntimeError(f"L{level_id}: no candidates for {shape} (tries={tries})")

    pool.sort(key=lambda c: c.score)
    pct = percentile_for(level_id)
    chosen = pool[min(len(pool) - 1, max(0, round(pct * (len(pool) - 1))))]

    cushion = min(5, max(2, round(chosen.optimal * 0.2) + 1))
    par = chosen.optimal + cushion

    print(f"L{level_id:>3} c{cc} d{sd} t{tc}x{td}  opt={chosen.optimal:>2} par={par:>2} "
          f"score={chosen.score:>6} pool={len(pool)} tries={tries}", flush=True)

    return {
        "level_id": level_id,
        "display_name": f"Level {level_id}",
        "difficulty_tier": tier_for(level_id),
        "schema_version": SCHEMA_VERSION,
        "color_count": cc,
        "stack_depth": sd,
        "color_stacks": [list(s) for s in chosen.stacks],
        "temp_slot_count": tc,
        "temp_slot_depth": td,
        "is_tutorial": False,
        "daily_challenge_eligible": True,
        "par_moves": par,
        "added_version": ADDED_VERSION,
        "_optimal_moves": chosen.optimal,
        "_difficulty_score": chosen.score,
        "_pool_size": len(pool),
    }


def main() -> None:
    start = int(sys.argv[1]) if len(sys.argv) > 1 else FIRST_ID
    end = int(sys.argv[2]) if len(sys.argv) > 2 else LAST_ID
    out = sys.argv[3] if len(sys.argv) > 3 else "candidate_levels_101_200.json"
    existing_sigs = load_existing_signatures()
    levels = [make_level(lid, existing_sigs) for lid in range(start, end + 1)]
    with open(out, "w", encoding="utf-8") as fh:
        json.dump({"catalogue_version": 2, "levels": levels}, fh, indent=2)
    print(f"\nWrote {len(levels)} levels [{start}-{end}] -> {out}", flush=True)


if __name__ == "__main__":
    main()
