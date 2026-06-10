"""
BoltSort level generator / curator.

Produces a structurally-varied, solvable, duplicate-free 50-level catalogue that
follows an intentional, low-sawtooth difficulty curve.

Design philosophy (per the level-design brief):
  * Variety comes from the SCRAMBLE STRUCTURE (distribution, buried colors,
    dispersion, traps) and gentle color/depth growth — NOT from a repeated
    template or from merely adding a tube / a bolt / swapping colors.
  * Every starting board is fully-packed color stacks with EMPTY temp slots
    (forced by the engine: total bolts = color_count*stack_depth over exactly
    color_count stacks). So generation = random full-packed deals, each PROVEN
    solvable by the optimal A* solver, then curated.
  * 2 full buffers are the baseline for the whole 1-50 range. Single-buffer and
    aggressively-restricted buffers are deliberately RESERVED for levels 51-150,
    so level 50 sits at ~1/3 of the eventual difficulty ceiling.

How a slot is filled:
  1. Sample many random full-packed deals for the slot's shape (seeded RNG).
  2. Keep those that are solvable, non-trivial (optimal >= floor), not the
     cyclic 'staircase' template, and canonically unique vs already-accepted.
  3. Pick the candidate whose difficulty score sits at the slot's target
     percentile of the kept pool -> a controlled rising curve with breather dips.
  4. par_moves = optimal + small cushion (so 3 stars rewards near-optimal play
     and is always ACHIEVABLE: par >= optimal).

Deterministic: fixed master seed -> identical catalogue every run.

Run:  python generate_levels.py            # writes candidate_levels.json
"""

from __future__ import annotations

import json
import random
from dataclasses import dataclass
from typing import List, Optional, Tuple

from boltsort_levels import (
    Board, LevelShape, solve_optimal, canonical_signature, difficulty_score,
)

ADDED_VERSION = "2026.06"
SCHEMA_VERSION = 1
MASTER_SEED = 20260610
OUT_PATH = "candidate_levels.json"


# ── difficulty curve: (color_count, stack_depth, temp_count, temp_depth, percentile) ──
# percentile = where in the slot's candidate score distribution to pick (rising
# within each band; breathers @10/20/30/40 drop low). L50 is a high capstone.
@dataclass
class Slot:
    cc: int
    sd: int
    tc: int
    td: int
    pct: float
    note: str = ""


# Baseline = 2 full buffers across ALL of 1-50. Restricted (td<sd) and single
# buffers are reserved for 51+, so difficulty here is driven by scramble structure
# and gentle color/depth growth, then plateaus at the moderate c6d5 ceiling.
SLOTS: List[Slot] = [
    # ── Band A: Onboarding 1-10 (breather @10) ──
    Slot(2, 3, 2, 3, 0.30, "intro"),
    Slot(2, 3, 2, 3, 0.55, "intro"),
    Slot(2, 4, 2, 4, 0.55, "intro+depth"),
    Slot(3, 3, 2, 3, 0.45, "first 3rd color"),
    Slot(3, 4, 2, 4, 0.55, ""),
    Slot(3, 4, 2, 4, 0.65, ""),
    Slot(3, 4, 2, 4, 0.75, ""),
    Slot(4, 4, 2, 4, 0.55, "first 4th color"),
    Slot(4, 4, 2, 4, 0.70, ""),
    Slot(3, 4, 2, 4, 0.20, "BREATHER"),
    # ── Band B: Easy combinations 11-20 (breather @20) ──
    Slot(4, 4, 2, 4, 0.50, ""),
    Slot(4, 4, 2, 4, 0.65, ""),
    Slot(4, 4, 2, 4, 0.78, ""),
    Slot(4, 5, 2, 5, 0.55, "depth 5"),
    Slot(4, 5, 2, 5, 0.72, ""),
    Slot(5, 4, 2, 4, 0.55, "first 5th color"),
    Slot(5, 4, 2, 4, 0.72, ""),
    Slot(5, 5, 2, 5, 0.60, ""),
    Slot(5, 5, 2, 5, 0.78, ""),
    Slot(4, 5, 2, 5, 0.20, "BREATHER"),
    # ── Band C: Medium-early 21-30 (breather @30) ──
    Slot(5, 4, 2, 4, 0.60, ""),
    Slot(5, 5, 2, 5, 0.62, ""),
    Slot(5, 5, 2, 5, 0.74, ""),
    Slot(5, 5, 2, 5, 0.82, ""),
    Slot(5, 5, 2, 5, 0.70, ""),
    Slot(6, 4, 2, 4, 0.58, "first 6th color"),
    Slot(6, 4, 2, 4, 0.74, ""),
    Slot(5, 5, 2, 5, 0.80, ""),
    Slot(6, 5, 2, 5, 0.66, ""),
    Slot(5, 5, 2, 5, 0.22, "BREATHER"),
    # ── Band C2/D: Medium-solid 31-40 (breather @40) ──
    Slot(6, 5, 2, 5, 0.62, ""),
    Slot(6, 5, 2, 5, 0.72, ""),
    Slot(5, 5, 2, 5, 0.84, ""),
    Slot(6, 5, 2, 5, 0.78, ""),
    Slot(6, 5, 2, 5, 0.84, ""),
    Slot(6, 5, 2, 5, 0.80, ""),
    Slot(6, 5, 2, 5, 0.82, ""),
    Slot(6, 5, 2, 5, 0.86, ""),
    Slot(6, 5, 2, 5, 0.88, ""),
    Slot(5, 5, 2, 5, 0.25, "BREATHER"),
    # ── Band D: Hardest of first third 41-50 (capstone @50) ──
    Slot(6, 5, 2, 5, 0.80, ""),
    Slot(6, 5, 2, 5, 0.86, ""),
    Slot(6, 5, 2, 5, 0.84, ""),
    Slot(6, 5, 2, 5, 0.88, ""),
    Slot(6, 5, 2, 5, 0.90, ""),
    Slot(6, 5, 2, 5, 0.86, ""),
    Slot(6, 5, 2, 5, 0.91, ""),
    Slot(6, 5, 2, 5, 0.90, ""),
    Slot(6, 5, 2, 5, 0.92, ""),
    Slot(6, 5, 2, 5, 0.95, "CAPSTONE"),
]
assert len(SLOTS) == 50


def random_deal(shape: LevelShape, rng: random.Random) -> List[Tuple[int, ...]]:
    bag: List[int] = []
    for c in range(1, shape.color_count + 1):
        bag += [c] * shape.stack_depth
    rng.shuffle(bag)
    d = shape.stack_depth
    return [tuple(bag[i * d:(i + 1) * d]) for i in range(shape.color_count)]


def staircase_signature(shape: LevelShape):
    """Canonical signature of the cyclic 'staircase' template for this shape
    (the lazy pattern the old catalogue used) so we can explicitly reject it."""
    n, d = shape.color_count, shape.stack_depth
    stacks = [tuple(((i + j) % n) + 1 for j in range(d)) for i in range(n)]
    rec = {"color_count": n, "stack_depth": d,
           "temp_slot_count": shape.temp_slot_count, "temp_slot_depth": shape.temp_slot_depth,
           "color_stacks": stacks}
    return canonical_signature(rec)


def trivial_floor(shape: LevelShape) -> int:
    """Minimum optimal move count for a deal to count as a 'real' puzzle.
    Low enough to admit a broad spread (the percentile picker handles ordering),
    high enough to exclude near-solved trivial deals."""
    return max(3, round(shape.total_bolts * 0.45))


@dataclass
class Candidate:
    stacks: List[Tuple[int, ...]]
    optimal: int
    score: float
    sig: tuple


def pool_size(slot: Slot) -> int:
    # restricted buffers are slow to solve optimally -> smaller pool
    return 30 if slot.td < slot.sd else 70


def node_cap(slot: Slot) -> int:
    # cap also acts as a 'too hard for this third' filter: reject capped deals
    return 700_000 if slot.td < slot.sd else 300_000


def build() -> dict:
    master = random.Random(MASTER_SEED)
    accepted_sigs = set()
    levels = []

    for idx, slot in enumerate(SLOTS):
        level_id = idx + 1
        shape = LevelShape(slot.cc, slot.sd, slot.tc, slot.td)
        rng = random.Random(master.randrange(1 << 30))
        stair_sig = staircase_signature(shape)
        floor = trivial_floor(shape)

        pool: List[Candidate] = []
        seen_local = set()
        tries = 0
        max_tries = pool_size(slot) * 40
        target_pool = pool_size(slot)
        while len(pool) < target_pool and tries < max_tries:
            tries += 1
            stacks = random_deal(shape, rng)
            rec = {"color_count": shape.color_count, "stack_depth": shape.stack_depth,
                   "temp_slot_count": shape.temp_slot_count,
                   "temp_slot_depth": shape.temp_slot_depth, "color_stacks": stacks}
            sig = canonical_signature(rec)
            if sig == stair_sig:
                continue
            if sig in accepted_sigs or sig in seen_local:
                continue
            board = Board.from_record(rec)
            if board.is_won():
                continue
            res = solve_optimal(board, node_cap=node_cap(slot))
            if res.capped or not res.solvable or res.optimal_moves is None:
                continue
            if res.optimal_moves < floor:
                continue
            seen_local.add(sig)
            pool.append(Candidate(stacks, res.optimal_moves,
                                  difficulty_score(rec, res.optimal_moves), sig))

        if not pool:
            raise RuntimeError(f"L{level_id}: no candidates for shape {shape} "
                               f"(tries={tries}). Loosen floor or shape.")

        pool.sort(key=lambda c: c.score)
        pick_i = min(len(pool) - 1, max(0, round(slot.pct * (len(pool) - 1))))
        chosen = pool[pick_i]
        accepted_sigs.add(chosen.sig)

        cushion = min(5, max(2, round(chosen.optimal * 0.2) + 1))
        par = chosen.optimal + cushion

        is_tutorial = level_id <= 2
        levels.append({
            "level_id": level_id,
            "display_name": f"Tutorial {level_id}" if is_tutorial else f"Level {level_id}",
            "difficulty_tier": tier_for(level_id),
            "schema_version": SCHEMA_VERSION,
            "color_count": shape.color_count,
            "stack_depth": shape.stack_depth,
            "color_stacks": [list(s) for s in chosen.stacks],
            "temp_slot_count": shape.temp_slot_count,
            "temp_slot_depth": shape.temp_slot_depth,
            "is_tutorial": is_tutorial,
            "daily_challenge_eligible": not is_tutorial,
            "par_moves": par,
            "added_version": ADDED_VERSION,
            # authoring metadata (ignored by runtime schema; useful for review/CI)
            "_optimal_moves": chosen.optimal,
            "_difficulty_score": chosen.score,
            "_pool_size": len(pool),
            "_note": slot.note,
        })
        print(f"L{level_id:>2} c{shape.color_count} d{shape.stack_depth} "
              f"t{shape.temp_slot_count}x{shape.temp_slot_depth}  "
              f"opt={chosen.optimal:>2} par={par:>2} score={chosen.score:>6} "
              f"pool={len(pool):>3} tries={tries} {slot.note}", flush=True)

    return {"catalogue_version": 2, "levels": levels}


def tier_for(level_id: int) -> int:
    # GLOBAL difficulty tiers per design/gdd/level-progression.md:
    # Tier 1 = Intro (levels 1-10), Tier 2 = Easy (levels 11-50).
    # Higher tiers (3-5) belong to levels 51+ and are not used in this first third.
    return 1 if level_id <= 10 else 2


if __name__ == "__main__":
    cat = build()
    with open(OUT_PATH, "w", encoding="utf-8") as fh:
        json.dump(cat, fh, indent=2)
    print(f"\nWrote {len(cat['levels'])} levels -> {OUT_PATH}")
