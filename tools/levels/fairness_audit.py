"""
BoltSort fairness + ground-truth solvability audit.

Goes beyond "is it solvable" (the existing validate_levels.py answers that). This
quantifies HUMAN fairness — the thing that makes a technically-solvable level
*feel* impossible — using only the real in-game rules (boltsort_levels.Board,
which is parity-checked against SortMechanic.cs and the Editor LevelSolver.cs).

Per-level instrumentation (Phase-2/Phase-6 of the audit brief):
  * solvable            — full A* reachability with ZERO extra tubes
  * optimal             — minimum move count (None if capped)
  * nodes               — states expanded by the optimal search (maneuvering-room proxy:
                          the difficulty audit established ~60-200 == tight, ~4-6k == roomy)
  * root_branch         — number of legal moves from the start board (immediate choice)
  * free_cells          — total empty capacity at start (cap - height summed)
  * room_per_move       — nodes / optimal (low == brittle / near-optimal-only)
  * tight               — room_per_move < TIGHT_ROOM  → flagged as frustrating
  * frustration         — composite 0..100 (higher == more likely to feel impossible)

Usage:
    python fairness_audit.py                 # audits Resources/levels.json
    python fairness_audit.py file.json 150000  # custom file + node cap
"""
from __future__ import annotations
import json, sys, time
from boltsort_levels import Board, solve_optimal

DEFAULT_PATH = "../../My project/Assets/Resources/levels.json"
TIGHT_ROOM = 8.0          # nodes-per-optimal-move below this == brittle
LOW_BRANCH = 6            # root legal-move count at/below this == cramped start


def free_cells(board: Board) -> int:
    return sum(cap - len(col) for col, cap in zip(board.cols, board.caps))


def frustration_score(opt, nodes, root_branch, fcells, n_color) -> int:
    """Composite human-frustration estimate (0..100). Calibrated, not physical."""
    if not opt:
        return 100
    room = nodes / opt
    s = 0.0
    s += max(0.0, (TIGHT_ROOM - room)) * 6.0          # brittleness dominates
    s += max(0, (LOW_BRANCH - root_branch)) * 5.0     # cramped opening
    s += max(0, (n_color - 4)) * 3.0                  # color load
    s += max(0, (12 - fcells)) * 2.0                  # little empty space
    s += max(0, (opt - 30)) * 0.6                     # sheer length
    return int(min(100, s))


def audit(path: str, node_cap: int) -> int:
    cat = json.load(open(path, encoding="utf-8"))
    levels = cat["levels"]
    out = []
    unsolvable, capped, tight = [], [], []
    t0 = time.time()
    for lv in levels:
        b = Board.from_record(lv)
        root_branch = sum(1 for _ in b.legal_moves())
        fcells = free_cells(b)
        r = solve_optimal(b, node_cap=node_cap)
        opt = r.optimal_moves
        room = (r.nodes / opt) if (opt and not r.capped) else None
        fr = frustration_score(opt if not r.capped else None, r.nodes, root_branch, fcells, lv["color_count"])
        rec = dict(id=lv["level_id"], c=lv["color_count"], d=lv["stack_depth"],
                   tc=lv["temp_slot_count"], td=lv["temp_slot_depth"],
                   opt=opt, nodes=r.nodes, par=lv["par_moves"],
                   root_branch=root_branch, free=fcells,
                   room=round(room, 1) if room else None,
                   frustration=fr, solvable=r.solvable, capped=r.capped)
        out.append(rec)
        if r.capped:
            capped.append(lv["level_id"])
        elif not r.solvable:
            unsolvable.append(lv["level_id"])
        elif room is not None and room < TIGHT_ROOM:
            tight.append(rec)
    dt = time.time() - t0

    json.dump(out, open("fairness_audit.json", "w"), indent=1)
    print(f"Audited {len(levels)} levels in {dt:.1f}s (node_cap={node_cap})\n")
    print(f"UNSOLVABLE with 0 extra tubes : {unsolvable or 'NONE'}")
    print(f"CAPPED (search inconclusive)  : {capped or 'NONE'}")
    print(f"TIGHT (room<{TIGHT_ROOM}, brittle): {len(tight)} levels")
    print("\nMost frustrating (top 25 by score):")
    print(f"{'id':>3} {'shape':>13} {'opt':>4} {'nodes':>6} {'root':>4} {'free':>4} {'room':>6} {'frust':>5}")
    for rec in sorted(out, key=lambda x: -x["frustration"])[:25]:
        shape = f"c{rec['c']}d{rec['d']}t{rec['tc']}x{rec['td']}"
        print(f"{rec['id']:>3} {shape:>13} {str(rec['opt']):>4} {rec['nodes']:>6} "
              f"{rec['root_branch']:>4} {rec['free']:>4} {str(rec['room']):>6} {rec['frustration']:>5}")
    # band-level frustration means (10-level bands)
    print("\nFrustration by 10-level band (mean):")
    for lo in range(1, 201, 10):
        band = [r["frustration"] for r in out if lo <= r["id"] < lo + 10]
        if band:
            bar = "#" * int(sum(band) / len(band) / 3)
            print(f"  L{lo:>3}-{lo+9:<3} {sum(band)/len(band):5.1f} {bar}")
    return 1 if (unsolvable or capped) else 0


if __name__ == "__main__":
    args = [a for a in sys.argv[1:]]
    path = args[0] if args else DEFAULT_PATH
    cap = int(args[1]) if len(args) > 1 else 250000
    sys.exit(audit(path, cap))
