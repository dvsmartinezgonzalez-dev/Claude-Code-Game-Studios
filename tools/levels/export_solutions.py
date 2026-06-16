"""
BoltSort QA solution-trace exporter — DEV/QA ONLY, never shipped to players.

For every authored level (default 1-200) it emits one solvable verdict, the
optimal move count, and ONE concrete optimal solution path as a list of moves.
A move is a (from, to) tube pair using human-readable labels:

    T1..Tn  = color tube 1..n        (flat indices 0..n-1)
    B1..Bm  = buffer / temp slot 1..m (flat indices n..n+m-1)

Output (written next to this script unless --out given):
    solutions.json   machine-readable: per level {solvable, optimal_moves, moves[]}
    solutions.md     human-readable QA sheet for manual step-through

This lets QA load any level and replay a known winning line by hand to verify
the level AND its special mechanics (multicolor / frozen / asymmetric / mystery)
behave as designed.

    python export_solutions.py                 # all 200, default catalogue
    python export_solutions.py --from 51 --to 200
    python export_solutions.py path/to/levels.json --node-cap 8000000
"""

from __future__ import annotations

import argparse
import json
import sys

from boltsort_levels import Board, solve_path, load_catalogue

DEFAULT_PATH = "../../My project/Assets/Resources/levels.json"


def label(idx: int, n_color: int) -> str:
    return f"T{idx + 1}" if idx < n_color else f"B{idx - n_color + 1}"


def mechanics_of(lv: dict) -> list:
    tags = []
    if any(c < 0 for s in lv["color_stacks"] for c in s):
        tags.append("mystery")
    if any(c == 0 for s in lv["color_stacks"] for c in s):
        tags.append("multicolor")
    if lv.get("frozen_tubes"):
        tags.append("frozen")
    if lv.get("tube_capacities"):
        tags.append("asymmetric")
    return tags


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("path", nargs="?", default=DEFAULT_PATH)
    ap.add_argument("--from", dest="lo", type=int, default=None)
    ap.add_argument("--to", dest="hi", type=int, default=None)
    ap.add_argument("--node-cap", dest="node_cap", type=int, default=8_000_000)
    ap.add_argument("--out-json", default="solutions.json")
    ap.add_argument("--out-md", default="solutions.md")
    args = ap.parse_args()

    cat = load_catalogue(args.path)
    levels = [lv for lv in cat.get("levels", [])
              if (args.lo is None or lv["level_id"] >= args.lo)
              and (args.hi is None or lv["level_id"] <= args.hi)]
    levels.sort(key=lambda x: x["level_id"])

    out = []
    md = ["# BoltSort QA Solution Traces",
          "",
          f"Source: `{args.path}` — {len(levels)} levels — DEV/QA ONLY.",
          "",
          "Tube labels: `Tn` = color tube n, `Bm` = buffer/temp slot m. "
          "Each move is `from->to` (move the single top bolt).",
          ""]

    bad = 0
    for lv in levels:
        lid = lv["level_id"]
        n = lv["color_count"]
        board = Board.from_record(lv)
        moves, res = solve_path(board, node_cap=args.node_cap)
        solvable = bool(res.solvable and not res.capped and moves is not None)
        tags = mechanics_of(lv)
        path = [f"{label(s, n)}->{label(d, n)}" for (s, d) in (moves or [])]
        if not solvable:
            bad += 1

        out.append({
            "level_id": lid,
            "solvable": solvable,
            "optimal_moves": res.optimal_moves,
            "par_moves": lv["par_moves"],
            "capped": res.capped,
            "mechanics": tags,
            "moves": path,
        })

        tagstr = f" [{', '.join(tags)}]" if tags else ""
        verdict = "OK" if solvable else ("CAPPED" if res.capped else "UNSOLVABLE")
        md.append(f"## L{lid}  ({verdict}) — optimal {res.optimal_moves}, par {lv['par_moves']}{tagstr}")
        md.append("")
        md.append("`" + "  ".join(path) + "`" if path else "_(already solved / no moves)_")
        md.append("")
        print(f"L{lid:>3} {verdict:>10} opt={res.optimal_moves} moves={len(path)}{tagstr}", flush=True)

    with open(args.out_json, "w", encoding="utf-8") as fh:
        json.dump({"source": args.path, "levels": out}, fh, indent=2)
    with open(args.out_md, "w", encoding="utf-8") as fh:
        fh.write("\n".join(md) + "\n")

    print(f"\nWrote {args.out_json} and {args.out_md} ({len(out)} levels).")
    if bad:
        print(f"WARNING: {bad} level(s) not conclusively solvable.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
