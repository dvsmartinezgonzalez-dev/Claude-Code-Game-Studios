"""
Feature-5 (stack-pull) + Fix-4 (primary-only win) offline validation.

Re-runs an optimal search over the shipped catalogue under three rule sets and
reports any level that would be BROKEN by the proposed mechanics.

IMPORTANT — why this is a SELF-CONTAINED solver and not boltsort_levels.solve_*:
  The shipped solver's pruning ("interchangeable empty destinations") and its
  symmetry-reduced state key treat EVERY tube as interchangeable, because the
  shipped win rule (every tube empty-or-full+mono) does not care which tube a
  color ends in. Fix-4 changes that: a color must finish in a PRIMARY (color-
  stack) tube; parking it in an auxiliary tube is no longer a win. So primary
  tubes and auxiliary tubes are NOT interchangeable, and both the move
  generator and the closed-set key must keep the two groups distinct. Reusing
  the shipped solver gives false "unsolvable" verdicts (it collapses a primary
  and an auxiliary empty into one state). This module keeps the groups separate.

Rule sets:
  baseline  : shipped rules (single-bolt, any-tube win) — boltsort_levels.solve_optimal
  fix4      : single-bolt moves, win = ONLY color stacks full+mono (temps ignored)
  stackpull : Fix-4 win + STACK-PULL (a top bolt drags the same-color run directly
              below it; multicolor wildcard always moves alone; the whole group must
              fit the destination or the move is rejected)

A level is FLAGGED when, relative to baseline, it becomes UNSOLVABLE, the search
caps out (unverifiable), fix4 optimal exceeds the shipped par (3-star impossible),
or stack-pull optimal collapses to a trivially short / zero count.

Run:  python validate_stackpull.py
"""
from __future__ import annotations

import heapq
import itertools
import json
from typing import Dict, List, Optional, Tuple

from boltsort_levels import Board, WILDCARD, _match, _complete_colors, solve_optimal

PATH = "../../My project/Assets/Resources/levels.json"
NODE_CAP = 8_000_000


# ── win condition (Fix 4) ─────────────────────────────────────────────────────
def won_fix4(b: Board) -> bool:
    """Complete ONLY when every PRIMARY (color-stack) tube is full-to-cap and a single
    color. Auxiliary tubes (temp slots + runtime extra tubes) are ignored."""
    for i in range(b.n_color):
        if len(b.cols[i]) != b.caps[i] or not _complete_colors(b.cols[i]):
            return False
    return True


# ── group-aware state key ─────────────────────────────────────────────────────
def key_fix4(b: Board) -> Tuple:
    """Symmetry-reduced key that KEEPS primaries and auxiliaries in separate buckets,
    so a color sitting in a color stack is a different state from the same color in a
    temp slot. Within each group, equal-(cap,freeze) tubes stay interchangeable."""
    n = b.n_color
    prim = tuple(sorted((b.caps[i], b.frozen[i], b.cols[i]) for i in range(n)))
    aux  = tuple(sorted((b.caps[i], b.frozen[i], b.cols[i]) for i in range(n, len(b.cols))))
    return (prim, aux)


def _tick(b: Board) -> Tuple[int, ...]:
    return tuple(f - 1 if f > 0 else 0 for f in b.frozen)


# ── single-bolt move model (sound for the Fix-4 objective) ────────────────────
def moves_single(b: Board):
    cols, caps, frozen, n = b.cols, b.caps, b.frozen, len(b.cols)
    ncol = b.n_color
    for i in range(n):
        src = cols[i]
        if not src:
            continue
        # A finished PRIMARY tube is never a source. A full+mono AUXILIARY tube is NOT
        # finished under Fix-4 (its color still has to reach a color stack) — keep it.
        if i < ncol and len(src) == caps[i] and _complete_colors(src):
            continue
        bolt = src[-1]
        seen_empty = set()
        for j in range(n):
            if j == i or frozen[j] > 0:
                continue
            dst = cols[j]
            if not dst:
                bucket = (caps[j], j < ncol, frozen[j])   # primaries/aux not interchangeable
                if bucket in seen_empty:
                    continue
                seen_empty.add(bucket)
                yield (i, j, 1)
            elif len(dst) < caps[j] and _match(dst[-1], bolt):
                yield (i, j, 1)


# ── stack-pull move model ─────────────────────────────────────────────────────
def top_run(col: Tuple[int, ...]) -> int:
    """How many bolts move together from the top. Multicolor wildcard (0) moves alone;
    otherwise the maximal run of the SAME exact color below the top is pulled with it."""
    top = col[-1]
    if top == WILDCARD:
        return 1
    k = 1
    while k < len(col) and col[-1 - k] == top:
        k += 1
    return k


def moves_stackpull(b: Board):
    cols, caps, frozen, n = b.cols, b.caps, b.frozen, len(b.cols)
    ncol = b.n_color
    for i in range(n):
        src = cols[i]
        if not src:
            continue
        if i < ncol and len(src) == caps[i] and _complete_colors(src):
            continue
        k = top_run(src)
        bolt = src[-1]
        seen_empty = set()
        for j in range(n):
            if j == i or frozen[j] > 0:
                continue
            dst = cols[j]
            if not dst:
                if k > caps[j]:          # whole group must fit, else REJECTED
                    continue
                bucket = (caps[j], j < ncol, frozen[j])
                if bucket in seen_empty:
                    continue
                seen_empty.add(bucket)
                yield (i, j, k)
            else:
                if len(dst) + k > caps[j]:   # whole group must fit, else REJECTED
                    continue
                if _match(dst[-1], bolt):
                    yield (i, j, k)


def apply_move(b: Board, src: int, dst: int, k: int) -> Board:
    cols = list(b.cols)
    moving = cols[src][-k:]
    cols[src] = cols[src][:-k]
    cols[dst] = cols[dst] + moving
    return Board(cols, b.caps, b.n_color, _tick(b))


# ── admissible heuristics ─────────────────────────────────────────────────────
def h_single(b: Board) -> int:
    """Each bolt above its column's bottom mono run must move at least once."""
    h = 0
    for col in b.cols:
        if not col:
            continue
        bottom = col[0]
        r = 1
        while r < len(col) and (col[r] == bottom or col[r] == WILDCARD or bottom == WILDCARD):
            if bottom == WILDCARD and col[r] != WILDCARD:
                bottom = col[r]
            r += 1
        h += len(col) - r
    return h


def h_stackpull(b: Board) -> int:
    """Each maximal mono segment above a column's bottom run needs >=1 move, and one
    stack-pull move relocates exactly one top segment -> a valid lower bound."""
    h = 0
    for col in b.cols:
        if not col:
            continue
        bottom = col[0]
        r = 1
        while r < len(col) and (col[r] == bottom or col[r] == WILDCARD or bottom == WILDCARD):
            if bottom == WILDCARD and col[r] != WILDCARD:
                bottom = col[r]
            r += 1
        seg, prev = 0, None
        for c in col[r:]:
            if c != prev:
                seg += 1
                prev = c
        h += seg
    return h


# ── A* over a pluggable move/heuristic/win model ──────────────────────────────
def astar(b: Board, gen, heur, won, node_cap: int = NODE_CAP, limit: Optional[int] = None):
    """A* over a pluggable move/heuristic/win model. Returns (solvable, optimal_moves, capped).

    When ``limit`` is set, any branch whose admissible f = g + h exceeds ``limit`` is
    pruned: the result then means "is there a win in <= limit moves?" — much cheaper than
    a full optimal proof and all we need to test 3-star (par) reachability."""
    if won(b):
        return True, 0, False
    counter = itertools.count()
    open_heap: List[Tuple[int, int, int, Board]] = [(heur(b), 0, next(counter), b)]
    best_g: Dict[Tuple, int] = {key_fix4(b): 0}
    nodes = 0
    while open_heap:
        f, g, _, st = heapq.heappop(open_heap)
        if limit is not None and f > limit:
            return False, None, False   # everything left in the frontier costs > limit
        nodes += 1
        if nodes > node_cap:
            return False, None, True
        kk = key_fix4(st)
        if g > best_g.get(kk, g):
            continue
        if won(st):
            return True, g, False
        ng = g + 1
        for src, dst, k in gen(st):
            child = apply_move(st, src, dst, k)
            f_child = ng + heur(child)
            if limit is not None and f_child > limit:
                continue
            ck = key_fix4(child)
            if ng < best_g.get(ck, 1 << 30):
                best_g[ck] = ng
                heapq.heappush(open_heap, (f_child, ng, next(counter), child))
    return False, None, False


def main():
    import sys, time
    cat = json.load(open(PATH, encoding="utf-8"))
    levels = cat["levels"]

    fix4_over_par: List[Tuple[int, int]] = []   # (lid, par) where no win exists within par
    pull_unsolvable: List[int] = []
    pull_capped: List[int] = []
    rows = []                                    # (lid, par, base_opt, fix4_within_par, pull_opt)
    t0 = time.time()

    for lv in levels:
        lid = lv["level_id"]
        par = lv["par_moves"]
        b = Board.from_record(lv)

        base = solve_optimal(b)                                          # shipped rules (reference)
        # Fix-4 only differs from the shipped win when a FULL color can be parked in a temp
        # slot — i.e. temp_slot_depth == stack_depth. With a restricted buffer
        # (temp_slot_depth < stack_depth) no color can finish in an auxiliary tube, so the
        # Fix-4 optimum equals the shipped optimum (<= par by authoring) — skip the search.
        parking_possible = lv["temp_slot_depth"] >= lv["stack_depth"]
        f4_capped = False
        if parking_possible:
            # Bounded: does ANY Fix-4 win exist within par moves? Cap nodes so a hard
            # "no" doesn't run forever — a capped search that found no <=par win after
            # FIX4_CAP nodes is strong (not exhaustive) evidence the level exceeds par.
            f4_ok, _, f4_capped = astar(b, moves_single, h_single, won_fix4,
                                        limit=par, node_cap=400_000)
        else:
            f4_ok = True  # restricted buffer: Fix-4 optimum == shipped optimum <= par
        sp_solv, sp_opt, sp_cap = astar(b, moves_stackpull, h_stackpull, won_fix4,
                                        node_cap=2_500_000)

        if not f4_ok:
            fix4_over_par.append((lid, par, "capped" if f4_capped else "proven"))
        if sp_cap:
            pull_capped.append(lid)
        elif not sp_solv:
            pull_unsolvable.append(lid)

        rows.append((lid, par, base.optimal_moves, f4_ok, sp_opt))
        f4tag = "Y" if f4_ok else ("N?" if f4_capped else "N")
        print(f"L{lid:>3} par={par:>3} base={str(base.optimal_moves):>3} "
              f"fix4<=par={f4tag:>2} pull={str(sp_opt):>3} "
              f"[{time.time()-t0:5.0f}s]", file=sys.stderr)

    # ── Feature-5 difficulty-collapse stats (pull optimal vs authored par) ──
    pull_ratios = [so / par for (lid, par, bo, fo, so) in rows if so]
    pull_below_par   = sum(1 for (lid, par, bo, fo, so) in rows if so is not None and so < par)
    pull_half_par    = sum(1 for (lid, par, bo, fo, so) in rows if so is not None and so <= par / 2)
    avg_ratio = sum(pull_ratios) / len(pull_ratios) if pull_ratios else 0

    print(f"{'ID':>3} {'par':>4} {'base':>5} {'f4<=par':>8} {'pull':>5}")
    for lid, par, bo, fo, so in rows:
        print(f"{lid:>3} {par:>4} {str(bo):>5} {('Y' if fo else 'N'):>8} {str(so):>5}")

    print("\n" + "=" * 64)
    print("FIX-4  (primary-only win) vs shipped par_moves:")
    proven = [x for x in fix4_over_par if x[2] == "proven"]
    capped = [x for x in fix4_over_par if x[2] == "capped"]
    print(f"  levels where NO win exists within par (3-star impossible): {len(fix4_over_par)}/200 "
          f"({len(proven)} proven, {len(capped)} capped/strong-evidence)")
    if fix4_over_par:
        print("   ", ", ".join(f"L{lid}" for lid, par, k in fix4_over_par))

    print("\nFEATURE-5  (stack-pull) reachability + difficulty:")
    print(f"  unsolvable: {len(pull_unsolvable)}   capped/unverified: {len(pull_capped)}")
    if pull_unsolvable:
        print("    unsolvable:", pull_unsolvable)
    if pull_capped:
        print("    capped:", pull_capped)
    print(f"  levels whose stack-pull optimal is BELOW par: {pull_below_par}/200")
    print(f"  levels whose stack-pull optimal is <= par/2 : {pull_half_par}/200")
    print(f"  mean(pull_optimal / par) = {avg_ratio:.2f}  (1.0 = matches par; lower = easier than authored)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
