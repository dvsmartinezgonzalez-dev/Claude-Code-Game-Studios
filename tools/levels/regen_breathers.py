"""Regenerate the breather levels (id%10==0, ids 110..190) as local difficulty
valleys so the advisory curve check (A3) passes. Each is re-picked as the EASIEST
candidate of a larger pool (pct=0), keeping it below both neighbours. L200 is left
as the capstone. Dedup is enforced against all other levels.
"""

from __future__ import annotations

import json

from boltsort_levels import canonical_signature
from generate_levels_101_200 import make_level

CATALOGUE = "../../My project/Assets/Resources/levels.json"
DEBUG_KEYS = ("_optimal_moves", "_difficulty_score", "_pool_size")
BREATHERS = [110, 120, 130, 140, 150, 160, 170, 180, 190]


def main() -> None:
    with open(CATALOGUE, "r", encoding="utf-8") as fh:
        cat = json.load(fh)
    by_id = {lv["level_id"]: lv for lv in cat["levels"]}

    # signatures of everything we are NOT regenerating
    keep_sigs = {canonical_signature(lv) for lid, lv in by_id.items() if lid not in BREATHERS}

    for lid in BREATHERS:
        lv = make_level(lid, set(keep_sigs), pct_override=0.0, pool_override=14)
        keep_sigs.add(canonical_signature(lv))
        for k in DEBUG_KEYS:
            lv.pop(k, None)
        by_id[lid] = lv

    cat["levels"] = [by_id[i] for i in sorted(by_id)]
    with open(CATALOGUE, "w", encoding="utf-8") as fh:
        json.dump(cat, fh, indent=2)
    print(f"regenerated {len(BREATHERS)} breather levels")


if __name__ == "__main__":
    main()
