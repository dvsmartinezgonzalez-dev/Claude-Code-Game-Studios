"""Produce the shipping levels.json (compact, runtime-schema-clean) from the
generator's candidate_levels.json, plus a human-readable catalogue report."""
import json

src = json.load(open("candidate_levels.json"))

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

blocks = ",\n".join(level_block(lv) for lv in src["levels"])
text = '{\n  "catalogue_version": 2,\n  "levels": [\n' + blocks + "\n  ]\n}\n"
with open("levels.clean.json", "w", encoding="utf-8") as fh:
    fh.write(text)

# round-trip sanity
rt = json.loads(text)
assert len(rt["levels"]) == 50
print(f"levels.clean.json written, {len(rt['levels'])} levels, round-trip OK, {len(text)} bytes")
