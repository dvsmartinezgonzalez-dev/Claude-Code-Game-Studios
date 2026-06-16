"""
BoltSort catalogue validator — the re-runnable guard against the problems that
made the old catalogue feel repetitive and broken.

Run it any time you add or edit levels:

    python validate_levels.py                       # validates the shipped catalogue
    python validate_levels.py path/to/levels.json   # validates a specific file

Exit code 0 = all BLOCKING checks pass. Non-zero = at least one BLOCKING failure
(suitable for CI). Advisory findings (curve smoothness) are printed but do not
fail the run unless --strict is passed.

BLOCKING checks (a failure here ships a broken or fake-variety catalogue):
  B1  schema integrity  — bolt-count invariant, color range, column cap (<=8),
                          temp_slot_depth <= stack_depth  (mirrors runtime guards)
  B2  solvable          — every level has an optimal solution. This is a FULL
                          reachability search (boltsort_levels.solve_optimal),
                          NOT a "legal moves remain" check: a level can have
                          legal moves forever (e.g. a reversible 3<->4 bolt
                          loop) and still be unwinnable. Only a reachable win
                          state passes; capped == NOT proven == BLOCKING fail.
  B3  par achievable    — par_moves >= optimal (3 stars is reachable)
  B4  par sane          — par_moves <= optimal + 10 (authoring rule, ADR)
  B5  no exact dupes    — no two levels share an identical board
  B6  no equivalents    — no two levels share a canonical structural signature
                          (same puzzle under color relabel / tube reorder)

ADVISORY checks (curve quality):
  A1  rising trend      — difficulty score trends upward across the catalogue
  A2  no absurd sawtooth— consecutive non-breather levels don't crash in difficulty
  A3  breather sanity   — breather levels (id %10==0) are not harder than neighbours
"""

from __future__ import annotations

import sys
from typing import List

from boltsort_levels import (
    Board, solve_optimal, canonical_signature, difficulty_score, load_catalogue,
)

DEFAULT_PATH = "../../My project/Assets/Resources/levels.json"
MAX_COLUMNS = 8          # ADR-0013
PAR_SLACK_MAX = 10       # authoring rule: par in [optimal, optimal+10]
SAWTOOTH_DROP_FRAC = 0.30  # advisory: >30% score drop between adjacent non-breathers


class Report:
    def __init__(self):
        self.blocking: List[str] = []
        self.advisory: List[str] = []

    def block(self, msg: str):
        self.blocking.append(msg)

    def advise(self, msg: str):
        self.advisory.append(msg)


def schema_ok(lv: dict, rep: Report) -> bool:
    lid = lv.get("level_id", "?")
    cc, sd = lv["color_count"], lv["stack_depth"]
    tc, td = lv["temp_slot_count"], lv["temp_slot_depth"]
    stacks = lv["color_stacks"]
    ok = True

    if cc + tc > MAX_COLUMNS:
        rep.block(f"L{lid}: column cap exceeded ({cc}+{tc} > {MAX_COLUMNS}).")
        ok = False
    if td > sd:
        rep.block(f"L{lid}: temp_slot_depth ({td}) > stack_depth ({sd}).")
        ok = False
    if len(stacks) != cc:
        rep.block(f"L{lid}: color_stacks has {len(stacks)} stacks, expected color_count={cc}.")
        ok = False
    total = sum(len(s) for s in stacks)
    if total != cc * sd:
        rep.block(f"L{lid}: bolt-count invariant broken (sum={total}, expected {cc*sd}).")
        ok = False
    freq = {}
    for s in stacks:
        for c in s:
            if c < 1 or c > cc:
                rep.block(f"L{lid}: phantom color id {c} (domain 1..{cc}).")
                ok = False
            freq[c] = freq.get(c, 0) + 1
    for c in range(1, cc + 1):
        if freq.get(c, 0) != sd:
            rep.block(f"L{lid}: color {c} appears {freq.get(c,0)}x, expected {sd}.")
            ok = False
    if lv["par_moves"] < 1:
        rep.block(f"L{lid}: par_moves must be >= 1.")
        ok = False
    return ok


def validate(path: str, strict: bool = False) -> int:
    cat = load_catalogue(path)
    levels = cat.get("levels", [])
    rep = Report()

    print(f"Validating {len(levels)} levels from {path}\n")
    print(f"{'ID':>3} {'shape':>12} {'opt':>4} {'par':>4} {'score':>7}  notes")

    sigs = {}        # canonical signature -> first level id
    exact = {}       # exact board key -> first level id
    ids = set()
    scores = {}      # level_id -> score (for curve checks)
    optimal = {}     # level_id -> optimal

    for lv in levels:
        lid = lv["level_id"]
        if lid in ids:
            rep.block(f"L{lid}: duplicate level_id.")
        ids.add(lid)

        if not schema_ok(lv, rep):
            print(f"{lid:>3} {'SCHEMA-FAIL':>12}")
            continue

        board = Board.from_record(lv)
        res = solve_optimal(board)
        opt = res.optimal_moves

        notes = []
        if res.capped:
            rep.block(f"L{lid}: solver hit node cap (could not verify solvability).")
            notes.append("CAPPED")
        elif not res.solvable:
            rep.block(f"L{lid}: UNSOLVABLE.")
            notes.append("UNSOLVABLE")
        else:
            optimal[lid] = opt
            if lv["par_moves"] < opt:
                rep.block(f"L{lid}: par_moves ({lv['par_moves']}) < optimal ({opt}) "
                          f"-> 3 stars is impossible.")
                notes.append("PAR<OPT")
            if lv["par_moves"] > opt + PAR_SLACK_MAX:
                rep.block(f"L{lid}: par_moves ({lv['par_moves']}) > optimal+{PAR_SLACK_MAX} "
                          f"({opt + PAR_SLACK_MAX}).")
                notes.append("PAR-LOOSE")

        sig = canonical_signature(lv)
        if sig in sigs:
            rep.block(f"L{lid}: structurally EQUIVALENT to L{sigs[sig]} "
                      f"(same puzzle under color relabel / tube reorder).")
            notes.append(f"~=L{sigs[sig]}")
        else:
            sigs[sig] = lid

        ekey = (lv["color_count"], lv["stack_depth"], lv["temp_slot_count"],
                lv["temp_slot_depth"], tuple(tuple(s) for s in lv["color_stacks"]))
        if ekey in exact:
            rep.block(f"L{lid}: EXACT duplicate of L{exact[ekey]}.")
            notes.append(f"==L{exact[ekey]}")
        else:
            exact[ekey] = lid

        score = difficulty_score(lv, opt)
        scores[lid] = score
        shape = f"c{lv['color_count']}d{lv['stack_depth']}t{lv['temp_slot_count']}x{lv['temp_slot_depth']}"
        print(f"{lid:>3} {shape:>12} {str(opt):>4} {lv['par_moves']:>4} {score:>7}  "
              f"{' '.join(notes)}")

    # ── curve checks (advisory) ──
    ordered = sorted(scores)
    for a, b in zip(ordered, ordered[1:]):
        if b % 10 == 0 or a % 10 == 0:
            continue  # breather positions exempt
        if scores[a] > 0 and (scores[a] - scores[b]) / scores[a] > SAWTOOTH_DROP_FRAC:
            rep.advise(f"L{a}->L{b}: difficulty drops {scores[a]}->{scores[b]} "
                       f"(>{int(SAWTOOTH_DROP_FRAC*100)}%) — possible sawtooth.")
    if len(ordered) >= 10:
        first = sum(scores[i] for i in ordered[:5]) / 5
        last = sum(scores[i] for i in ordered[-5:]) / 5
        if last <= first:
            rep.advise(f"Overall difficulty does not rise (first5 avg {first:.0f} "
                       f">= last5 avg {last:.0f}).")
    for lid in ordered:
        if lid % 10 == 0:
            neigh = [scores[x] for x in (lid - 1, lid + 1) if x in scores]
            if neigh and scores[lid] > min(neigh):
                rep.advise(f"L{lid} (breather) score {scores[lid]} is not below a neighbour "
                           f"{neigh}.")

    # ── summary ──
    print("\n" + "=" * 60)
    if rep.blocking:
        print(f"BLOCKING FAILURES ({len(rep.blocking)}):")
        for m in rep.blocking:
            print(f"  [FAIL] {m}")
    else:
        print("BLOCKING: all checks passed [OK]")
    if rep.advisory:
        print(f"\nADVISORY ({len(rep.advisory)}):")
        for m in rep.advisory:
            print(f"  [warn] {m}")
    else:
        print("ADVISORY: clean [OK]")

    if rep.blocking:
        return 1
    if strict and rep.advisory:
        return 2
    return 0


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    strict = "--strict" in sys.argv
    path = args[0] if args else DEFAULT_PATH
    sys.exit(validate(path, strict=strict))
