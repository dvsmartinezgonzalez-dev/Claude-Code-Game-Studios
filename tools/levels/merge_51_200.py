"""Merge generated levels 51-200 into the shipping levels.json, preserving
levels 1-50 byte-for-byte (same clean-schema format as finalize.py)."""
import json

EXISTING  = "../../My project/Assets/Resources/levels.json"
CANDIDATE = "candidate_levels_51_200.json"
OUT       = EXISTING

existing = json.load(open(EXISTING, encoding="utf-8"))
new      = json.load(open(CANDIDATE, encoding="utf-8"))

assert len(existing["levels"]) == 50
assert [lv["level_id"] for lv in existing["levels"]] == list(range(1, 51))

new_levels = new["levels"]
assert len(new_levels) == 150, f"expected 150 new levels, got {len(new_levels)}"
assert [lv["level_id"] for lv in new_levels] == list(range(51, 201))


def level_block(rec):
    cs = "[" + ",".join("[" + ",".join(map(str, s)) + "]" for s in rec["color_stacks"]) + "]"
    f = [
        ("level_id", rec["level_id"]),
        ("display_name", json.dumps(rec["display_name"])),
        ("difficulty_tier", rec["difficulty_tier"]),
        ("schema_version", rec["schema_version"]),
        ("color_count", rec["color_count"]),
        ("stack_depth", rec["stack_depth"]),
        ("color_stacks", cs),
        ("temp_slot_count", rec["temp_slot_count"]),
        ("temp_slot_depth", rec["temp_slot_depth"]),
        ("is_tutorial", "true" if rec["is_tutorial"] else "false"),
        ("daily_challenge_eligible", "true" if rec["daily_challenge_eligible"] else "false"),
        ("par_moves", rec["par_moves"]),
        ("added_version", json.dumps(rec["added_version"])),
    ]
    lines = ["    {"]
    for i, (k, v) in enumerate(f):
        comma = "," if i < len(f) - 1 else ""
        lines.append(f'      "{k}": {v}{comma}')
    lines.append("    }")
    return "\n".join(lines)


all_levels = existing["levels"] + new_levels
blocks = ",\n".join(level_block(lv) for lv in all_levels)
text = '{\n  "catalogue_version": 2,\n  "levels": [\n' + blocks + "\n  ]\n}\n"

rt = json.loads(text)
assert len(rt["levels"]) == 200
assert [lv["level_id"] for lv in rt["levels"]] == list(range(1, 201))
for i in range(50):
    assert rt["levels"][i] == existing["levels"][i], f"level {i+1} changed!"

with open(OUT, "w", encoding="utf-8") as fh:
    fh.write(text)
print(f"levels.json written, {len(rt['levels'])} levels (1-50 unchanged), round-trip OK, {len(text)} bytes")
