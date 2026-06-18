"""
BoltSort Phase-2 rebalance + mechanic introduction (deterministic, self-verifying).

Two workstreams, both proven against the REAL in-game rules (boltsort_levels.Board,
parity-checked vs SortMechanic.cs / Editor LevelSolver.cs):

  PART A — fairness fix for the brittle 1-FULL-BUFFER bands (L51-60, L81-100).
           Adds a 2nd full-depth buffer (temp_slot_count 1->2). Same scramble, more
           maneuvering room. Re-solves each board (0 extra tubes) and recomputes par.

  PART B — early mechanic progression (multicolor, asymmetric, frozen; mystery deferred).
           Converts selected non-outlier levels to schema_version 2 teaching/reinforce/
           combination levels. Every conversion is re-solved with the real rules and only
           committed if solvable with 0 extra tubes and healthy room.

Run:
    python rebalance_phase2.py --part A            # buffer fix only
    python rebalance_phase2.py --part B            # mechanics only
    python rebalance_phase2.py --part AB           # both (writes once)
    python rebalance_phase2.py --part AB --dry-run # report, write nothing
A backup levels.json.bak is written before the first mutation.
"""
from __future__ import annotations
import argparse, json, random, shutil
from boltsort_levels import Board, solve_optimal, canonical_signature

CAT = "../../My project/Assets/Resources/levels.json"
NODE_CAP = 2_000_000
GEN_CAP = 120_000         # fail-fast cap for candidate search (teaching levels must be easy;
                          # a candidate that needs more than this is "too hard" and is skipped)
ROOM_MIN = 10.0            # nodes/opt below this == still brittle; reject

BUFFER_BANDS = list(range(51, 61)) + list(range(81, 101))


def cushion_par(opt: int) -> int:
    return opt + min(5, max(2, round(opt * 0.2) + 1))


def room_of(res) -> float:
    return (res.nodes / res.optimal_moves) if (res.optimal_moves and not res.capped) else 0.0


def solve(rec: dict):
    return solve_optimal(Board.from_record(rec), node_cap=NODE_CAP)


def solveg(rec: dict):
    """Fail-fast solve for candidate search; a capped result is rejected as 'too hard'."""
    return solve_optimal(Board.from_record(rec), node_cap=GEN_CAP)


# ── PART A ────────────────────────────────────────────────────────────────────
def part_a(by_id, report, dry):
    print("\n=== PART A — add 2nd buffer to 1-buffer bands ===")
    print(f"{'ID':>3} {'old_par':>7} {'new_opt':>7} {'new_par':>7} {'room':>7}  shape")
    for lid in BUFFER_BANDS:
        lv = by_id[lid]
        if lv["temp_slot_count"] != 1:
            print(f"{lid:>3}  SKIP (temp_slot_count={lv['temp_slot_count']}, not 1)")
            continue
        trial = dict(lv)
        trial["temp_slot_count"] = 2          # add a 2nd full-depth buffer
        res = solve(trial)
        if res.capped or not res.solvable:
            report.append(("A", lid, "FAIL", "unsolvable/capped after buffer add"))
            print(f"{lid:>3}  FAIL — capped={res.capped} solvable={res.solvable}")
            continue
        opt = res.optimal_moves
        new_par = cushion_par(opt)
        room = room_of(res)
        print(f"{lid:>3} {lv['par_moves']:>7} {opt:>7} {new_par:>7} {room:>7.1f}  "
              f"c{lv['color_count']}d{lv['stack_depth']}t2x{lv['temp_slot_depth']}")
        report.append(("A", lid, "ok", f"opt={opt} par {lv['par_moves']}->{new_par} room={room:.0f}"))
        lv["temp_slot_count"] = 2          # always apply in-memory; disk write gated in main
        lv["par_moves"] = new_par


# ── PART B — mechanic conversions (each verified before commit) ───────────────
def to_v2(lv):
    lv["schema_version"] = 2


def try_multicolor(lv, seed):
    """Replace one ball with the wildcard (0). Strictly increases room => always solvable.
    Search positions deterministically for the one that keeps room healthy and not pre-won."""
    rng = random.Random(seed)
    positions = [(i, j) for i, s in enumerate(lv["color_stacks"]) for j in range(len(s))]
    rng.shuffle(positions)
    base = [list(s) for s in lv["color_stacks"]]
    for (i, j) in positions[:15]:          # bound candidates; each is fail-fast (GEN_CAP)
        if base[i][j] == 0:
            continue
        cand = [list(s) for s in base]
        cand[i][j] = 0
        rec = dict(lv); rec["color_stacks"] = cand
        res = solveg(rec)
        if not res.capped and res.solvable and not Board.from_record(rec).is_won() and room_of(res) >= ROOM_MIN:
            return cand, res
    return None, None


def try_asym_buffer(lv, caps_tail):
    """Asymmetric BUFFER capacities (color tubes stay uniform => bolt invariant untouched).
    caps_tail = list of temp-slot capacities (may exceed stack_depth = 'extra-large tube').
    The tall height lives ONLY in tube_capacities; temp_slot_depth stays <= stack_depth so the
    runtime AssertTempSlotDepthValid guard passes. Bigger buffer = pure room; verify solvable."""
    n, sd = lv["color_count"], lv["stack_depth"]
    tc = len(caps_tail)
    rec = dict(lv)
    rec["temp_slot_count"] = tc
    rec["temp_slot_depth"] = sd                      # <= stack_depth (runtime guard); caps carry true size
    rec["tube_capacities"] = [sd] * n + list(caps_tail)
    res = solveg(rec)
    if not res.capped and res.solvable and room_of(res) >= ROOM_MIN:
        return rec["tube_capacities"], rec["temp_slot_depth"], tc, res
    return None, None, None, None


def gen_asym_tubes(n, sizes, tc, td, seed, room_hi=150.0):
    """Generate a solvable MIXED-COLOR-TUBE-SIZE board: color c gets `sizes[c-1]` bolts,
    each color tube full at its own capacity. tube_capacities = sizes + buffers.
    Bounded fast search — a fixed small shape (c5 d5, two short tubes) solves in <20k nodes,
    so the first roomy hit is found quickly; capped/over-roomy deals are skipped."""
    rng = random.Random(seed)
    caps = list(sizes) + [td] * tc
    for _ in range(1500):
        bag = []
        for c in range(1, n + 1):
            bag += [c] * sizes[c - 1]
        rng.shuffle(bag)
        stacks, k = [], 0
        for c in range(n):
            stacks.append(bag[k:k + sizes[c]]); k += sizes[c]
        rec = {"color_count": n, "stack_depth": max(sizes), "temp_slot_count": tc,
               "temp_slot_depth": td, "color_stacks": [list(s) for s in stacks],
               "tube_capacities": caps}
        b = Board.from_record(rec)
        if b.is_won():
            continue
        res = solveg(rec)
        if not res.capped and res.solvable and ROOM_MIN <= room_of(res) <= room_hi:
            return stacks, caps, res
    return None, None, None


def try_frozen(lv, seed):
    """Add ONE short-freeze tube to an existing (roomy) board; verify still solvable."""
    rng = random.Random(seed)
    total_cols = lv["color_count"] + lv["temp_slot_count"]
    combos = [(idx, t) for idx in range(total_cols) for t in (2, 3, 4)]
    rng.shuffle(combos)
    for idx, turns in combos[:12]:         # bound candidates; each is fail-fast (GEN_CAP)
        rec = dict(lv)
        rec["frozen_tubes"] = [{"tube_index": idx, "turns": turns}]
        res = solveg(rec)
        if not res.capped and res.solvable and room_of(res) >= ROOM_MIN:
            return rec["frozen_tubes"], res
    return None, None


# Curated early progression. (kind, level_id, args). Teaching levels sit in easy bands;
# each mechanic appears solo in an easy board before any harder reuse. Mystery deferred.
MECHANIC_PLAN = [
    # multicolor (relief valve) — earliest, then reinforce, then relief inside hard bands
    ("multicolor", 14, 1401), ("multicolor", 23, 2301), ("multicolor", 37, 3701),
    ("multicolor", 66, 6601), ("multicolor", 93, 9301),
    # asymmetric BUFFERS (gentle: one tall, one short) — teaching + reinforce
    ("asym_buffer", 24, [6, 4]), ("asym_buffer", 38, [7, 4]),
    # asymmetric MIXED COLOR TUBES (the real mixed-capacity, introduced AFTER buffers)
    ("asym_tubes", 114, None), ("asym_tubes", 152, None),
    # frozen tube — teaching + reinforce + 1 combination
    ("frozen", 29, 2901), ("frozen", 46, 4601), ("frozen", 134, 13401),
]


def part_b(by_id, report, dry):
    print("\n=== PART B — early mechanic progression ===")
    sigs = {canonical_signature(lv): lv["level_id"] for lv in by_id.values()}
    for spec in MECHANIC_PLAN:
        kind, lid = spec[0], spec[1]
        lv = by_id[lid]
        changed = None
        if kind == "multicolor":
            cand, res = try_multicolor(lv, spec[2])
            if cand:
                changed = dict(lv, schema_version=2, color_stacks=cand,
                               has_multicolor=True, par_moves=cushion_par(res.optimal_moves))
        elif kind == "asym_buffer":
            caps, td, tc, res = try_asym_buffer(lv, spec[2])
            if caps:
                changed = dict(lv, schema_version=2, tube_capacities=caps,
                               temp_slot_depth=td, temp_slot_count=tc,
                               par_moves=cushion_par(res.optimal_moves))
        elif kind == "asym_tubes":
            # Fixed FAST shape: c5 d5 with two short cap-4 color tubes => the canonical
            # "4 tubes cap5 / 2 tubes cap4" mixed-capacity board, now fair (room>=10) and
            # solver-proven. Lighter than its c6d6 neighbours = a variety breather.
            n, base, tc, td = 6, 5, 2, 5
            sizes = [base, base, base, base, base - 1, base - 1]      # [5,5,5,5,4,4]
            stacks, caps, res = gen_asym_tubes(n, sizes, tc, td, lid * 7 + 3)
            if stacks:
                changed = dict(lv, schema_version=2, color_count=n, stack_depth=base,
                               color_stacks=[list(s) for s in stacks],
                               temp_slot_count=tc, temp_slot_depth=td,
                               tube_capacities=caps, par_moves=cushion_par(res.optimal_moves))
        elif kind == "frozen":
            fz, res = try_frozen(lv, spec[2])
            if fz:
                changed = dict(lv, schema_version=2, frozen_tubes=fz,
                               par_moves=cushion_par(res.optimal_moves))

        if not changed:
            print(f"L{lid:>3} {kind:<11} FAIL — no verified solvable variant")
            report.append(("B", lid, "FAIL", kind))
            continue
        opt = res.optimal_moves
        print(f"L{lid:>3} {kind:<11} ok  opt={opt} par->{changed['par_moves']} room={room_of(res):.0f}")
        report.append(("B", lid, "ok", f"{kind} opt={opt} room={room_of(res):.0f}"))
        by_id[lid].clear(); by_id[lid].update(changed)   # always apply in-memory; write gated in main


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--part", default="AB", choices=["A", "B", "AB"])
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    cat = json.load(open(CAT, encoding="utf-8"))
    by_id = {lv["level_id"]: lv for lv in cat["levels"]}
    report = []

    if not args.dry_run:
        shutil.copyfile(CAT, CAT + ".bak")
        print(f"backup -> {CAT}.bak")

    if "A" in args.part:
        part_a(by_id, report, args.dry_run)
    if "B" in args.part:
        part_b(by_id, report, args.dry_run)

    if not args.dry_run:
        json.dump(cat, open(CAT, "w", encoding="utf-8"), indent=2)
        open(CAT, "a", encoding="utf-8").write("\n")
        print(f"\nwrote {len(cat['levels'])} levels -> {CAT}")
    fails = [r for r in report if r[2] == "FAIL"]
    print(f"\nsummary: {len(report)-len(fails)} ok, {len(fails)} FAIL")
    for r in fails:
        print("  FAIL", r)


if __name__ == "__main__":
    main()
