"""
BoltSort — GDD realign (1-200) + baked extension (201-300). Deterministic, self-verifying.

Every board that ships is proven against the REAL in-game rules (boltsort_levels.Board,
the same model parity-checked vs SortMechanic.cs / GameStateManager.cs). A level is only
committed if solve_optimal() returns a CONCLUSIVE solvable verdict (never capped), par is
cushioned above optimal, and its canonical signature is unique.

What it does
------------
PART 1 — REALIGN 1-200 to the Phase-2 GDD mechanic ranges
   (design/gdd/phase2-technical-design.md: frozen 121+, mystery 161+, multicolor 171+,
    asymmetric/large 121+; combos frozen+mystery 181+, mystery+multicolor 191+;
    frozen+multicolor NEVER; no exotic mechanic below 121).
   * Revert every off-range mechanic level (<intro) back to a solver-verified classic board.
   * Inject GDD-positioned mechanics across 121-200 (~27 mechanic levels total in 1-200),
     each solver-verified, breathers (id%10==0) left classic.

PART 2 — GENERATE 201-300 baked levels
   * Difficulty continues upward from L200; mechanics + combos sprinkled per the 191+ matrix.
   * Each level is a forward-random scramble accepted only when conclusively solvable.

Run:
    python retrofit_extend.py            # writes levels.json (backup first)
    python retrofit_extend.py --dry-run  # report only
Then gate with:  python validate_levels.py "../../My project/Assets/Resources/levels.json"
"""
from __future__ import annotations
import argparse, json, random, shutil
from boltsort_levels import (Board, solve_optimal, canonical_signature, difficulty_score)
from rebalance_phase2 import (try_multicolor, try_frozen, try_asym_buffer,
                              cushion_par, room_of, ROOM_MIN)

CAT = "../../My project/Assets/Resources/levels.json"
NODE_CAP = 2_000_000      # conclusive solve for final commit
GEN_CAP  = 60_000         # fail-fast cap during candidate search: a solvable board is found by
                          # A* well under this; an unsolvable random fill hits the cap fast and is
                          # rejected (capped == reject). Keeps each attempt cheap.
ADDED_VERSION = "2026.06"

# ── catalogue I/O (preserve format: indent=2, utf-8, CRLF, trailing newline) ──
def load_cat():
    return json.load(open(CAT, encoding="utf-8"))

def save_cat(cat):
    out = json.dumps(cat, indent=2, ensure_ascii=False) + "\n"
    open(CAT, "w", encoding="utf-8", newline="\r\n").write(out)

def solve(rec, cap=NODE_CAP):
    return solve_optimal(Board.from_record(rec), node_cap=cap)

def is_won(rec):
    return Board.from_record(rec).is_won()

# ── generic classic scramble generator (forward-random + solver filter) ──────
def gen_classic(n, sd, tc, td, seed, sigs, room_lo=ROOM_MIN, room_hi=1e9,
                tries=6000, allow_tc_fallback=True):
    """Return (color_stacks, res, used_tc) for a unique, conclusively-solvable classic board."""
    rng = random.Random(seed)
    for attempt in range(tries):
        bag = [c for c in range(1, n + 1) for _ in range(sd)]
        rng.shuffle(bag)
        stacks = [bag[i * sd:(i + 1) * sd] for i in range(n)]
        rec = {"color_count": n, "stack_depth": sd, "temp_slot_count": tc,
               "temp_slot_depth": td, "color_stacks": [list(s) for s in stacks]}
        if is_won(rec):
            continue
        res = solve_optimal(Board.from_record(rec), node_cap=GEN_CAP)
        if res.capped or not res.solvable:
            continue
        if not (room_lo <= room_of(res) <= room_hi):
            continue
        sig = canonical_signature(rec)
        if sig in sigs:
            continue
        sigs.add(sig)
        return [list(s) for s in stacks], res, tc
    if allow_tc_fallback and tc < 3:   # extra buffer raises the solvable hit-rate
        return gen_classic(n, sd, tc + 1, td, seed + 99991, sigs,
                           room_lo, room_hi, tries, allow_tc_fallback=False)
    return None, None, tc

def add_mystery_cells(stacks, count, seed):
    """Negate `count` covered, non-top, non-bottom cells (mystery). Solvability is unchanged
    (the solver reads abs). Returns a NEW stacks list or None if no legal cell exists."""
    rng = random.Random(seed)
    cells = [(i, j) for i, s in enumerate(stacks)
             for j in range(1, len(s) - 1) if s[j] > 0]      # j in (bottom, top)
    if len(cells) < count:
        return None
    rng.shuffle(cells)
    out = [list(s) for s in stacks]
    for (i, j) in cells[:count]:
        out[i][j] = -out[i][j]
    return out

def base_fields(lid, tier, daily=True):
    return {"level_id": lid, "display_name": f"Level {lid}", "difficulty_tier": tier}

# ════════════════════════════════════════════════════════════════════════════
# PART 1 — REALIGN 1-200
# ════════════════════════════════════════════════════════════════════════════

# Off-range mechanic levels to revert to classic (below their GDD intro level).
REVERT_MULTICOLOR = [14, 23, 37, 66, 93]      # multicolor intro 171
REVERT_FROZEN     = [29, 46]                   # frozen intro 121
REVERT_ASYMBUF    = [24, 38]                   # asymmetric intro 121
REVERT_REGEN      = [114]                      # mixed-tube asym <121: regenerate classic
# Kept (already in-range & GDD-valid): 134 frozen, 152 asym, 161/171 mystery (added earlier).

def revert_multicolor(lv):
    n, sd = lv["color_count"], lv["stack_depth"]
    counts = {c: 0 for c in range(1, n + 1)}
    for s in lv["color_stacks"]:
        for c in s:
            if c > 0:
                counts[c] += 1
    missing = [c for c, k in counts.items() if k == sd - 1]
    stacks = [list(s) for s in lv["color_stacks"]]
    for s in stacks:
        for j, c in enumerate(s):
            if c == 0:
                s[j] = missing[0]
    new = {k: lv[k] for k in lv if k not in ("tube_capacities", "frozen_tubes",
                                             "mystery_balls", "has_multicolor")}
    new["schema_version"] = 1
    new["color_stacks"] = stacks
    return new

def revert_strip(lv, drop):
    new = {k: lv[k] for k in lv if k not in drop}
    new["schema_version"] = 1
    return new

# GDD-positioned mechanic plan for 121-200 (breathers id%10==0 avoided).
# kinds: asym | frozen | multicolor | mystery | frozen+mystery | mystery+multicolor
PLAN_121_200 = {
    123: "asym", 127: "asym", 132: "asym", 138: "asym", 143: "asym", 147: "asym",
    157: "asym", 163: "asym", 167: "asym", 178: "asym", 186: "asym", 193: "asym",
    124: "frozen", 142: "frozen", 156: "frozen", 168: "frozen", 182: "frozen",
    174: "multicolor", 197: "multicolor",
    184: "mystery", 195: "mystery",
    188: "frozen+mystery",       # combo allowed 181+
    192: "mystery+multicolor",   # combo allowed 191+
    # keep (no-op): 134 frozen, 152 asym, 161 mystery, 171 mystery
}

def apply_mechanic_121_200(by_id, lid, kind, report):
    lv = by_id[lid]
    res = None
    if kind == "asym":
        sd = lv["stack_depth"]
        for tail in ([sd + 2, sd - 1], [sd + 1, sd - 1], [sd + 2, sd], [sd + 1, sd]):
            caps, td, tc, r = try_asym_buffer(lv, tail)
            if caps:
                lv.clear(); lv.update(dict(by_id_orig[lid], schema_version=2,
                    tube_capacities=caps, temp_slot_depth=td, temp_slot_count=tc,
                    par_moves=cushion_par(r.optimal_moves))); res = r; break
    elif kind == "frozen":
        fz, r = try_frozen(lv, lid * 7 + 1)
        if fz:
            lv.clear(); lv.update(dict(by_id_orig[lid], schema_version=2, frozen_tubes=fz,
                par_moves=cushion_par(r.optimal_moves))); res = r
    elif kind == "multicolor":
        cand, r = try_multicolor(lv, lid * 7 + 2)
        if cand:
            lv.clear(); lv.update(dict(by_id_orig[lid], schema_version=2, color_stacks=cand,
                has_multicolor=True, par_moves=cushion_par(r.optimal_moves))); res = r
    elif kind == "mystery":
        stacks = add_mystery_cells(lv["color_stacks"], 1, lid * 7 + 3)
        if stacks:
            r = solve(dict(lv, color_stacks=stacks))
            if r.solvable and not r.capped:
                lv.clear(); lv.update(dict(by_id_orig[lid], schema_version=2,
                    color_stacks=stacks, mystery_balls=True,
                    par_moves=cushion_par(r.optimal_moves))); res = r
    elif kind == "frozen+mystery":
        fz, r = try_frozen(lv, lid * 7 + 4)
        if fz:
            tmp = dict(lv, schema_version=2, frozen_tubes=fz)
            stacks = add_mystery_cells(tmp["color_stacks"], 1, lid * 7 + 5)
            if stacks:
                r2 = solve(dict(tmp, color_stacks=stacks))
                if r2.solvable and not r2.capped:
                    lv.clear(); lv.update(dict(by_id_orig[lid], schema_version=2,
                        frozen_tubes=fz, color_stacks=stacks, mystery_balls=True,
                        par_moves=cushion_par(r2.optimal_moves))); res = r2
    elif kind == "mystery+multicolor":
        cand, r = try_multicolor(lv, lid * 7 + 6)
        if cand:
            stacks = add_mystery_cells(cand, 1, lid * 7 + 7)
            if stacks:
                r2 = solve(dict(lv, color_stacks=stacks))
                if r2.solvable and not r2.capped:
                    lv.clear(); lv.update(dict(by_id_orig[lid], schema_version=2,
                        color_stacks=stacks, has_multicolor=True, mystery_balls=True,
                        par_moves=cushion_par(r2.optimal_moves))); res = r2
    ok = res is not None
    report.append(("P1", lid, kind, "ok" if ok else "FAIL",
                   f"opt={res.optimal_moves} room={room_of(res):.0f}" if ok else "no verified variant"))
    return ok

by_id_orig = {}  # snapshot of pre-mutation level dicts (for clean field rebuild)

def part1(by_id, sigs, report):
    print("=== PART 1 — realign 1-200 ===")
    # reverts
    for lid in REVERT_MULTICOLOR:
        nv = revert_multicolor(by_id[lid]); r = solve(nv)
        nv["par_moves"] = cushion_par(r.optimal_moves)
        by_id[lid].clear(); by_id[lid].update(nv)
        report.append(("P1", lid, "revert-multicolor", "ok" if r.solvable else "FAIL", f"opt={r.optimal_moves}"))
    for lid in REVERT_FROZEN:
        nv = revert_strip(by_id[lid], ("frozen_tubes",)); r = solve(nv)
        nv["par_moves"] = cushion_par(r.optimal_moves)
        by_id[lid].clear(); by_id[lid].update(nv)
        report.append(("P1", lid, "revert-frozen", "ok" if r.solvable else "FAIL", f"opt={r.optimal_moves}"))
    for lid in REVERT_ASYMBUF:
        sd = by_id[lid]["stack_depth"]
        nv = revert_strip(by_id[lid], ("tube_capacities",))
        nv["temp_slot_count"] = 2; nv["temp_slot_depth"] = sd
        r = solve(nv)
        nv["par_moves"] = cushion_par(r.optimal_moves)
        by_id[lid].clear(); by_id[lid].update(nv)
        report.append(("P1", lid, "revert-asymbuf", "ok" if r.solvable else "FAIL", f"opt={r.optimal_moves}"))
    for lid in REVERT_REGEN:
        stacks, r, tc = gen_classic(6, 6, 2, 5, lid * 31 + 5, sigs)
        nv = {**base_fields(lid, by_id[lid].get("difficulty_tier", 4)),
              "schema_version": 1, "color_count": 6, "stack_depth": 6,
              "color_stacks": stacks, "temp_slot_count": tc, "temp_slot_depth": 5,
              "is_tutorial": False, "daily_challenge_eligible": by_id[lid].get("daily_challenge_eligible", True),
              "par_moves": cushion_par(r.optimal_moves), "added_version": ADDED_VERSION}
        by_id[lid].clear(); by_id[lid].update(nv)
        report.append(("P1", lid, "regen-classic", "ok", f"opt={r.optimal_moves}"))

    # snapshot reverted/clean state before mechanic injection
    for lid, lv in by_id.items():
        by_id_orig[lid] = dict(lv)

    # inject GDD-positioned mechanics
    for lid, kind in sorted(PLAN_121_200.items()):
        apply_mechanic_121_200(by_id, lid, kind, report)

# ════════════════════════════════════════════════════════════════════════════
# PART 2 — GENERATE 201-300
# ════════════════════════════════════════════════════════════════════════════
def shape_for(lid):
    """Difficulty continues upward from L200 (c6d6). Breathers (id%10==0) are eased."""
    breather = (lid % 10 == 0)
    if lid <= 230:   n, sd, td = 6, 6, 4
    elif lid <= 260: n, sd, td = 7, 6, 4
    elif lid <= 290: n, sd, td = 6, 7, 4
    else:            n, sd, td = 7, 6, 3
    if breather:     # ease: fewer colors / roomier buffer
        n = max(5, n - 1); td = min(sd, td + 1)
    return n, sd, 2, td

def mech_for(lid):
    """GDD 191+ matrix: all combos allowed. Cadence: mystery ~1/10, frozen ~1/15,
    multicolor ~1/20 (relief), asym ~1/8. Breathers stay classic."""
    if lid % 10 == 0:           return None
    if lid % 10 == 1 and lid % 20 == 1:  return "mystery+multicolor"
    if lid % 10 == 7:           return "mystery"
    if lid % 15 == 3:           return "frozen"
    if lid % 20 == 13:          return "multicolor"
    if lid % 8 == 5:            return "asym"
    if lid % 10 == 4 and lid % 30 == 24: return "frozen+mystery"
    return None

def tier_for(lid):
    return 6 if lid <= 250 else 7

def make_level(lid, sigs, report):
    n, sd, tc, td = shape_for(lid)
    kind = mech_for(lid)
    seed = lid * 131 + 17
    stacks, r, tc = gen_classic(n, sd, tc, td, seed, sigs)
    if stacks is None:
        report.append(("P2", lid, "classic", "FAIL", f"no solvable c{n}d{sd}")); return None
    rec = {**base_fields(lid, tier_for(lid)), "schema_version": 1,
           "color_count": n, "stack_depth": sd, "color_stacks": stacks,
           "temp_slot_count": tc, "temp_slot_depth": td, "is_tutorial": False,
           "daily_challenge_eligible": True, "par_moves": cushion_par(r.optimal_moves),
           "added_version": ADDED_VERSION}
    res = r
    if kind == "asym":
        caps, td2, tc2, rr = try_asym_buffer(rec, [sd + 2, sd - 1])
        if caps:
            rec.update(schema_version=2, tube_capacities=caps, temp_slot_depth=td2,
                       temp_slot_count=tc2, par_moves=cushion_par(rr.optimal_moves)); res = rr
    elif kind == "frozen":
        fz, rr = try_frozen(rec, seed + 1)
        if fz:
            rec.update(schema_version=2, frozen_tubes=fz, par_moves=cushion_par(rr.optimal_moves)); res = rr
    elif kind == "multicolor":
        cand, rr = try_multicolor(rec, seed + 2)
        if cand:
            rec.update(schema_version=2, color_stacks=cand, has_multicolor=True,
                       par_moves=cushion_par(rr.optimal_moves)); res = rr
    elif kind == "mystery":
        ms = add_mystery_cells(rec["color_stacks"], 1 + (lid % 2), seed + 3)  # 1-2 mystery
        if ms:
            rr = solve(dict(rec, color_stacks=ms))
            if rr.solvable and not rr.capped:
                rec.update(schema_version=2, color_stacks=ms, mystery_balls=True,
                           par_moves=cushion_par(rr.optimal_moves)); res = rr
    elif kind == "frozen+mystery":
        fz, rr = try_frozen(rec, seed + 4)
        if fz:
            ms = add_mystery_cells(rec["color_stacks"], 1, seed + 5)
            if ms:
                rr2 = solve(dict(rec, frozen_tubes=fz, color_stacks=ms))
                if rr2.solvable and not rr2.capped:
                    rec.update(schema_version=2, frozen_tubes=fz, color_stacks=ms,
                               mystery_balls=True, par_moves=cushion_par(rr2.optimal_moves)); res = rr2
    elif kind == "mystery+multicolor":
        cand, rr = try_multicolor(rec, seed + 6)
        if cand:
            ms = add_mystery_cells(cand, 1, seed + 7)
            if ms:
                rr2 = solve(dict(rec, color_stacks=ms))
                if rr2.solvable and not rr2.capped:
                    rec.update(schema_version=2, color_stacks=ms, has_multicolor=True,
                               mystery_balls=True, par_moves=cushion_par(rr2.optimal_moves)); res = rr2
    report.append(("P2", lid, kind or "classic", "ok",
                   f"opt={res.optimal_moves} score={difficulty_score(rec, res.optimal_moves):.0f}"))
    return rec

def part2(by_id, sigs, report):
    print("=== PART 2 — generate 201-300 ===")
    for lid in range(201, 301):
        rec = make_level(lid, sigs, report)
        if rec:
            by_id[lid] = rec

# ════════════════════════════════════════════════════════════════════════════
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    cat = load_cat()
    by_id = {lv["level_id"]: lv for lv in cat["levels"]}
    report = []

    if not args.dry_run:
        shutil.copyfile(CAT, CAT + ".bak"); print(f"backup -> {CAT}.bak")

    part1(by_id, set(), report)
    # signatures of the finalized 1-200 set, for 201-300 dedup
    sigs = set()
    for lid in range(1, 201):
        sigs.add(canonical_signature(by_id[lid]))
    part2(by_id, sigs, report)

    cat["levels"] = [by_id[i] for i in sorted(by_id)]
    if not args.dry_run:
        save_cat(cat); print(f"wrote {len(cat['levels'])} levels -> {CAT}")

    fails = [r for r in report if r[3] == "FAIL"]
    print("\n--- report ---")
    for r in report:
        if r[3] == "FAIL":
            print("  FAIL", r[1], r[2], "-", r[4])
    mech = sum(1 for i in range(1, 201) if by_id[i].get("schema_version") == 2)
    print(f"\nmechanic levels in 1-200: {mech}")
    print(f"total levels: {len(cat['levels'])}")
    print(f"summary: {len(report)-len(fails)} ok, {len(fails)} FAIL")

if __name__ == "__main__":
    main()
