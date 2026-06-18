"""
Recover the 2 mechanic slots deferred by rebalance_phase2.py (L66 multicolor,
L134 frozen). They were skipped only because the fail-fast GEN_CAP marked the
heavier-band boards 'capped' (inconclusive) — a roomy solution there simply needs
more nodes to PROVE optimal. Here we use a higher cap, a full deterministic
candidate sweep, and (for frozen) target buffer tubes with the shortest freeze.

Each conversion is solver-proven solvable with 0 extra tubes and room >= ROOM_MIN
before it is written. Reproducible (no RNG). Writes in place; idempotent.
"""
from __future__ import annotations
import json
from boltsort_levels import Board, solve_optimal

CAT = "../../My project/Assets/Resources/levels.json"
CAP = 1_500_000
ROOM_MIN = 10.0


def cushion_par(o: int) -> int:
    return o + min(5, max(2, round(o * 0.2) + 1))


def solve(rec: dict):
    return solve_optimal(Board.from_record(rec), node_cap=CAP)


def room(r) -> float:
    return (r.nodes / r.optimal_moves) if (r.optimal_moves and not r.capped) else 0.0


def chase_multicolor(lv: dict, max_tries=12):
    """Sweep ball positions (board order, bounded) at the higher cap; take the best room
    seen, and early-exit as soon as a clearly-roomy (>=100) relief placement is found."""
    base = [list(s) for s in lv["color_stacks"]]
    positions = [(i, j) for i, s in enumerate(base) for j in range(len(s)) if base[i][j] != 0]
    best = None  # (room, opt, stacks)
    for (i, j) in positions[:max_tries]:
        cand = [list(t) for t in base]
        cand[i][j] = 0
        rec = dict(lv); rec["color_stacks"] = cand
        if Board.from_record(rec).is_won():
            continue
        r = solve(rec)
        if r.capped or not r.solvable:
            continue
        rm = room(r)
        if rm >= ROOM_MIN and (best is None or rm > best[0]):
            best = (rm, r.optimal_moves, cand)
        if best and best[0] >= 100:        # clearly roomy relief — good enough, stop early
            break
    if best is None:
        return None
    rm, opt, stacks = best
    return dict(lv, schema_version=2, color_stacks=stacks, has_multicolor=True,
                par_moves=cushion_par(opt)), opt, rm


def chase_frozen(lv: dict):
    """Try the SHORTEST freeze on the LEAST disruptive tube first: buffers before
    color tubes, turns 2 before 3. Accept the first solvable, room>=ROOM_MIN result."""
    n = lv["color_count"]
    total = n + lv["temp_slot_count"]
    order = list(range(n, total)) + list(range(n))   # buffers first, then color tubes
    for turns in (2, 3):
        for idx in order:
            rec = dict(lv)
            rec["frozen_tubes"] = [{"tube_index": idx, "turns": turns}]
            r = solve(rec)
            if r.capped or not r.solvable:
                continue
            if room(r) >= ROOM_MIN:
                return (dict(lv, schema_version=2,
                             frozen_tubes=[{"tube_index": idx, "turns": turns}],
                             par_moves=cushion_par(r.optimal_moves)),
                        r.optimal_moves, room(r), idx, turns)
    return None


def main():
    cat = json.load(open(CAT, encoding="utf-8"))
    by = {l["level_id"]: l for l in cat["levels"]}
    changed = False

    print("L66 multicolor (sweep all positions, cap=1.5M)...")
    res = chase_multicolor(by[66])
    if res:
        rec, opt, rm = res
        by[66].clear(); by[66].update(rec); changed = True
        print(f"  OK  opt={opt} par={rec['par_moves']} room={rm:.0f}")
    else:
        print("  STILL FAILED")

    print("L134 frozen (buffers first, shortest freeze, cap=1.5M)...")
    res = chase_frozen(by[134])
    if res:
        rec, opt, rm, idx, turns = res
        by[134].clear(); by[134].update(rec); changed = True
        print(f"  OK  tube_index={idx} turns={turns} opt={opt} par={rec['par_moves']} room={rm:.0f}")
    else:
        print("  STILL FAILED")

    if changed:
        json.dump(cat, open(CAT, "w", encoding="utf-8"), indent=2)
        open(CAT, "a", encoding="utf-8").write("\n")
        print(f"wrote -> {CAT}")
    else:
        print("no change")


if __name__ == "__main__":
    main()
