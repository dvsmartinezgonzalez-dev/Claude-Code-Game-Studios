"""
BoltSort outlier rebalancer — targeted, deterministic regeneration of the
early/mid difficulty spikes found in the 1-100 audit (see difficulty_audit.md).

It ONLY replaces the scramble of the flagged outlier levels, keeping every
level's shape (color_count / stack_depth / temp_slot_count / temp_slot_depth)
and every other catalogue field intact. For each target it searches random
deals of that exact shape for a solvable, non-duplicate board whose OPTIMAL
move count lands inside a target window chosen to smooth the local curve, then
recomputes par_moves with the catalogue's standard cushion formula.

Deterministic: fixed per-level seeds -> identical output every run.

    python rebalance_outliers.py            # writes the catalogue in place
    python rebalance_outliers.py --dry-run  # report picks, write nothing
"""

from __future__ import annotations

import argparse
import json
import random
from typing import Dict, List, Tuple

from boltsort_levels import (
    Board, LevelShape, solve_optimal, canonical_signature, difficulty_score,
)

CATALOGUE = "../../My project/Assets/Resources/levels.json"

# level_id -> (target_opt_lo, target_opt_hi, seed, why)
# Targets chosen to smooth the local neighbourhood (see difficulty_audit.md).
TARGETS: Dict[int, Tuple[int, int, int, str]] = {
    53: (35, 37, 5301, "early single-buffer band spike (was opt 42 between L51=35/L52=34)"),
    54: (36, 38, 5401, "user-flagged wall: 1-buffer + opt 40 too punishing this early"),
    60: (31, 34, 6001, "breather (id%10) but was HARDEST in band (opt 46) — broke relief contract"),
    70: (30, 33, 7001, "breather but was band-max (opt 47) — broke relief contract"),
    76: (41, 44, 7601, "worst 1-100 outlier: opt 54 vs neighbourhood ~43 (+26%)"),
}

NODE_CAP = 400_000
MAX_TRIES = 60_000


def cushion_par(optimal: int) -> int:
    return optimal + min(5, max(2, round(optimal * 0.2) + 1))


def random_deal(shape: LevelShape, rng: random.Random) -> List[Tuple[int, ...]]:
    bag: List[int] = []
    for c in range(1, shape.color_count + 1):
        bag += [c] * shape.stack_depth
    rng.shuffle(bag)
    d = shape.stack_depth
    return [tuple(bag[i * d:(i + 1) * d]) for i in range(shape.color_count)]


def regen_one(lv: dict, lo: int, hi: int, seed: int, blocked_sigs: set):
    shape = LevelShape(lv["color_count"], lv["stack_depth"],
                       lv["temp_slot_count"], lv["temp_slot_depth"])
    mid = (lo + hi) / 2.0
    rng = random.Random(seed)
    best = None  # (dist_to_mid, optimal, stacks, sig)
    solved = 0
    for _ in range(MAX_TRIES):
        stacks = random_deal(shape, rng)
        rec = {"color_count": shape.color_count, "stack_depth": shape.stack_depth,
               "temp_slot_count": shape.temp_slot_count,
               "temp_slot_depth": shape.temp_slot_depth, "color_stacks": stacks}
        sig = canonical_signature(rec)
        if sig in blocked_sigs:
            continue
        board = Board.from_record(rec)
        if board.is_won():
            continue
        res = solve_optimal(board, node_cap=NODE_CAP)
        if res.capped or not res.solvable or res.optimal_moves is None:
            continue
        opt = res.optimal_moves
        solved += 1
        if lo <= opt <= hi:
            # first in-window hit wins (deterministic given the fixed seed)
            best = (abs(opt - mid), opt, stacks, sig)
            break
        if solved % 2000 == 0:
            print(f"  L{lv['level_id']}: {solved} solved, no hit yet...", flush=True)
    if best is None:
        raise RuntimeError(f"L{lv['level_id']}: no candidate in opt window [{lo},{hi}]")
    return best


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    with open(CATALOGUE, "r", encoding="utf-8") as fh:
        cat = json.load(fh)
    levels = cat["levels"]
    by_id = {lv["level_id"]: lv for lv in levels}

    # signatures of every level we are NOT replacing (collision guard)
    blocked = set()
    for lv in levels:
        if lv["level_id"] not in TARGETS:
            blocked.add(canonical_signature(lv))

    print(f"{'ID':>3} {'old_opt':>7} {'new_opt':>7} {'old_par':>7} {'new_par':>7}  note")
    for lid, (lo, hi, seed, why) in TARGETS.items():
        lv = by_id[lid]
        old_opt = solve_optimal(Board.from_record(lv), node_cap=4_000_000).optimal_moves
        _, opt, stacks, sig = regen_one(lv, lo, hi, seed, blocked)
        blocked.add(sig)  # the new board must not collide with later picks either
        new_par = cushion_par(opt)
        print(f"{lid:>3} {old_opt:>7} {opt:>7} {lv['par_moves']:>7} {new_par:>7}  {why}")
        if not args.dry_run:
            lv["color_stacks"] = [list(s) for s in stacks]
            lv["par_moves"] = new_par

    if args.dry_run:
        print("\n(dry run — nothing written)")
        return 0

    with open(CATALOGUE, "w", encoding="utf-8") as fh:
        json.dump(cat, fh, indent=2)
        fh.write("\n")
    print(f"\nWrote {len(levels)} levels -> {CATALOGUE} ({len(TARGETS)} replaced)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
