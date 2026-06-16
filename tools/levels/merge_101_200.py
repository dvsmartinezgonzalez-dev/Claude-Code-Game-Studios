"""Splice generated levels 101-200 chunks into the runtime catalogue.

Keeps levels 1-100 byte-identical, strips internal debug fields (_optimal_moves,
_difficulty_score, _pool_size) from the new records, appends 101-200 in order,
and writes back to levels.json. Run after the chunk generation completes.
"""

from __future__ import annotations

import glob
import json

CATALOGUE = "../../My project/Assets/Resources/levels.json"
DEBUG_KEYS = ("_optimal_moves", "_difficulty_score", "_pool_size")


def main() -> None:
    with open(CATALOGUE, "r", encoding="utf-8") as fh:
        cat = json.load(fh)
    base = [lv for lv in cat["levels"] if lv["level_id"] <= 100]

    new: dict = {}
    for path in glob.glob("chunk_*.json"):
        with open(path, "r", encoding="utf-8") as fh:
            for lv in json.load(fh)["levels"]:
                for k in DEBUG_KEYS:
                    lv.pop(k, None)
                new[lv["level_id"]] = lv

    missing = [i for i in range(101, 201) if i not in new]
    if missing:
        raise SystemExit(f"missing generated levels: {missing}")

    cat["levels"] = base + [new[i] for i in range(101, 201)]
    with open(CATALOGUE, "w", encoding="utf-8") as fh:
        json.dump(cat, fh, indent=2)
    print(f"merged: {len(base)} base + {len(new)} new = {len(cat['levels'])} levels")


if __name__ == "__main__":
    main()
