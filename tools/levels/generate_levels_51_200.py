"""
BoltSort level generator for levels 51-200 (Phase-2 expansion).

Same engine/approach as generate_levels.py (random full-packed deals, proven
solvable by the optimal A* solver, curated by difficulty percentile within a
small pool), extended to a new id range. Excludes structural duplicates of the
existing 1-50 catalogue (loaded from levels.json) as well as duplicates within
51-200 itself.

Deterministic: fixed master seed -> identical output every run.

Run:  python generate_levels_51_200.py   # writes candidate_levels_51_200.json
"""

from __future__ import annotations

import json
import random
from dataclasses import dataclass
from typing import List, Tuple

from boltsort_levels import (
    Board, LevelShape, solve_optimal, canonical_signature, difficulty_score,
)

ADDED_VERSION = "2026.06"
SCHEMA_VERSION = 1
MASTER_SEED = 20260615
OUT_PATH = "candidate_levels_51_200.json"
EXISTING_CATALOGUE = "../../My project/Assets/Resources/levels.json"
FIRST_ID = 51
LAST_ID = 200


@dataclass
class Slot:
    cc: int
    sd: int
    tc: int
    td: int
    pct: float


# (color_count, stack_depth, temp_slot_count, temp_slot_depth) per 10-level band.
# Difficulty growth here comes mainly from buffer restriction (single buffer /
# td < sd) and gentle color/depth growth -- reserved for 51+ per the level-design
# brief. Each band's percentile rises 0.45 -> 0.85; the band-boundary reset to
# 0.45 acts as a natural breather. Level 200 is the capstone (pct 0.95).
BANDS: List[Tuple[int, int, int, int]] = [
    (6, 5, 1, 5),   # 51-60   single full buffer
    (6, 5, 2, 4),   # 61-70   restricted double buffer
    (6, 5, 2, 3),   # 71-80   heavily restricted double buffer
    (7, 5, 1, 5),   # 81-90   7th color, single full buffer (7+1=8 cols, ADR-0013 cap)
    (7, 5, 1, 5),   # 91-100  7 colors, single full buffer (tier 3 capstone)
    (7, 5, 2, 4),   # 101-110 7 colors, restricted double (tier 4 begins)
    (6, 6, 2, 6),   # 111-120 depth 6, comfortable double
    (6, 6, 1, 6),   # 121-130 depth 6, single buffer
    (7, 6, 2, 6),   # 131-140 7 colors depth 6, comfortable double
    (7, 6, 2, 5),   # 141-150 7 colors depth 6, restricted (tier 4 capstone)
    (7, 6, 1, 6),   # 151-160 7 colors depth 6, single buffer (tier 5 begins)
    (7, 6, 2, 4),   # 161-170 7 colors depth 6, heavily restricted
    (7, 5, 1, 4),   # 171-180 7 colors, restricted single buffer
    (7, 6, 2, 5),   # 181-190 7 colors depth 6, restricted
    (7, 6, 1, 6),   # 191-200 7 colors depth 6, single buffer (final capstone)
]
assert len(BANDS) == 15
assert len(BANDS) * 10 == LAST_ID - FIRST_ID + 1


def build_slots() -> List[Slot]:
    slots: List[Slot] = []
    for cc, sd, tc, td in BANDS:
        for i in range(10):
            pct = 0.45 + (0.85 - 0.45) * (i / 9.0)
            slots.append(Slot(cc, sd, tc, td, pct))
    slots[-1].pct = 0.95  # level 200 capstone
    return slots


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


def pool_size(slot: Slot) -> int:
    restricted = slot.td < slot.sd
    if slot.sd <= 5:
        return 5 if restricted else 8
    return 4 if restricted else 6


def node_cap(slot: Slot) -> int:
    restricted = slot.td < slot.sd
    if slot.sd <= 5:
        return 300_000 if restricted else 120_000
    return 450_000 if restricted else 600_000


@dataclass
class Candidate:
    stacks: List[Tuple[int, ...]]
    optimal: int
    score: float
    sig: tuple


def load_existing_signatures() -> set:
    with open(EXISTING_CATALOGUE, "r", encoding="utf-8") as fh:
        cat = json.load(fh)
    sigs = set()
    for rec in cat["levels"]:
        sigs.add(canonical_signature(rec))
    return sigs


def tier_for(level_id: int) -> int:
    if level_id <= 100:
        return 3
    if level_id <= 150:
        return 4
    return 5


def build() -> dict:
    master = random.Random(MASTER_SEED)
    accepted_sigs = load_existing_signatures()
    levels = []

    for slot, level_id in zip(build_slots(), range(FIRST_ID, LAST_ID + 1)):
        shape = LevelShape(slot.cc, slot.sd, slot.tc, slot.td)
        rng = random.Random(master.randrange(1 << 30))
        stair_sig = staircase_signature(shape)
        floor = trivial_floor(shape)

        pool: List[Candidate] = []
        seen_local = set()
        target_pool = pool_size(slot)
        max_tries = target_pool * (50 if slot.sd >= 6 else 25)
        tries = 0
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
            raise RuntimeError(f"L{level_id}: no candidates for shape {shape} (tries={tries})")

        pool.sort(key=lambda c: c.score)
        pick_i = min(len(pool) - 1, max(0, round(slot.pct * (len(pool) - 1))))
        chosen = pool[pick_i]
        accepted_sigs.add(chosen.sig)

        cushion = min(5, max(2, round(chosen.optimal * 0.2) + 1))
        par = chosen.optimal + cushion

        levels.append({
            "level_id": level_id,
            "display_name": f"Level {level_id}",
            "difficulty_tier": tier_for(level_id),
            "schema_version": SCHEMA_VERSION,
            "color_count": shape.color_count,
            "stack_depth": shape.stack_depth,
            "color_stacks": [list(s) for s in chosen.stacks],
            "temp_slot_count": shape.temp_slot_count,
            "temp_slot_depth": shape.temp_slot_depth,
            "is_tutorial": False,
            "daily_challenge_eligible": True,
            "par_moves": par,
            "added_version": ADDED_VERSION,
            "_optimal_moves": chosen.optimal,
            "_difficulty_score": chosen.score,
            "_pool_size": len(pool),
        })
        print(f"L{level_id:>3} c{shape.color_count} d{shape.stack_depth} "
              f"t{shape.temp_slot_count}x{shape.temp_slot_depth}  "
              f"opt={chosen.optimal:>2} par={par:>2} score={chosen.score:>6} "
              f"pool={len(pool):>2} tries={tries}", flush=True)

        with open(OUT_PATH, "w", encoding="utf-8") as fh:
            json.dump({"catalogue_version": 2, "levels": levels}, fh, indent=2)

    return {"catalogue_version": 2, "levels": levels}


if __name__ == "__main__":
    cat = build()
    with open(OUT_PATH, "w", encoding="utf-8") as fh:
        json.dump(cat, fh, indent=2)
    print(f"\nWrote {len(cat['levels'])} levels -> {OUT_PATH}")
