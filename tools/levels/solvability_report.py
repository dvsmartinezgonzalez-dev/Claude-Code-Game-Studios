"""
BoltSort solvability report — a quick developer dump for the authored catalogue.

    python solvability_report.py                      # shipped catalogue
    python solvability_report.py path/to/levels.json  # a specific file
    python solvability_report.py --from 51 --to 100   # only a range
    python solvability_report.py --node-cap 20000000  # raise the offline budget

For every level it prints: level_id, solvable yes/no, optimal/found moves,
duplicate yes/no (canonical signature), capped yes/no. Exit code is non-zero if
ANY level is unsolvable, capped, or a duplicate — so it doubles as a CI guard.

WHY THIS EXISTS / WHAT IT IS NOT
--------------------------------
Solvability here means a WINNING STATE IS REACHABLE (full A* search), never
"the board still has a legal move". A reversible loop (move a bolt 3->4 then
4->3 forever) keeps legal moves available while the level is impossible; such a
softlock must report solvable=NO. A capped result is 'unknown', NOT a pass:
re-run with a higher --node-cap until the verdict is conclusive before shipping.
"""

from __future__ import annotations

import argparse
import sys

from boltsort_levels import Board, solve_optimal, canonical_signature, load_catalogue

DEFAULT_PATH = "../../My project/Assets/Resources/levels.json"


def main() -> int:
    ap = argparse.ArgumentParser(description="Per-level solvability/duplicate report.")
    ap.add_argument("path", nargs="?", default=DEFAULT_PATH)
    ap.add_argument("--from", dest="lo", type=int, default=None)
    ap.add_argument("--to", dest="hi", type=int, default=None)
    ap.add_argument("--node-cap", dest="node_cap", type=int, default=8_000_000,
                    help="A* node budget; raise for an offline conclusive verdict.")
    args = ap.parse_args()

    cat = load_catalogue(args.path)
    levels = cat.get("levels", [])
    if args.lo is not None:
        levels = [lv for lv in levels if lv["level_id"] >= args.lo]
    if args.hi is not None:
        levels = [lv for lv in levels if lv["level_id"] <= args.hi]

    print(f"Solvability report - {len(levels)} levels from {args.path} "
          f"(node_cap={args.node_cap:,})\n")
    print(f"{'ID':>4}  {'solvable':>8}  {'moves':>5}  {'par':>4}  {'dup':>4}  {'capped':>6}")

    sigs: dict = {}
    bad = 0
    for lv in sorted(levels, key=lambda x: x["level_id"]):
        lid = lv["level_id"]
        board = Board.from_record(lv)
        res = solve_optimal(board, node_cap=args.node_cap)

        sig = canonical_signature(lv)
        dup_of = sigs.get(sig)
        if dup_of is None:
            sigs[sig] = lid
        is_dup = dup_of is not None

        solvable = "YES" if (res.solvable and not res.capped) else "NO"
        moves = res.optimal_moves if res.optimal_moves is not None else "-"
        dup = f"=L{dup_of}" if is_dup else "no"
        capped = "YES" if res.capped else "no"

        if res.capped or not res.solvable or is_dup:
            bad += 1
        print(f"{lid:>4}  {solvable:>8}  {str(moves):>5}  {lv['par_moves']:>4}  "
              f"{dup:>4}  {capped:>6}")

    print("\n" + "=" * 48)
    if bad:
        print(f"FAIL: {bad} level(s) unsolvable, capped, or duplicate.")
        return 1
    print("PASS: every level is solvable, conclusive, and unique.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
