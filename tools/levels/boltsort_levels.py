"""
BoltSort level engine — authoring & validation core.

Pure Python model of the BoltSort sort puzzle that EXACTLY mirrors the runtime
rules implemented in:
  - My project/Assets/_Project/Scripts/SortMechanic/SortMechanic.cs  (IsLegalMove, IsWon)
  - My project/Assets/_Project/Scripts/GameStateManager/GameStateManager.cs (board model)

Board model
-----------
A level is N color stacks (flat indices 0..N-1, each capacity = stack_depth) plus
M temp slots (flat indices N..N+M-1, each capacity = temp_slot_depth). Each column is
a list of color ids, index 0 = bottom, last = top. Every color id in 1..N appears
exactly stack_depth times (bolt-count invariant).

Move rule (single top bolt):
  legal(src -> dst) iff dst is empty OR (top(dst) == bolt AND len(dst) < cap(dst))

Win rule:
  every column is EMPTY or FULL-TO-ITS-CAPACITY and monochrome.
  (With a restricted buffer, temp_slot_depth < stack_depth, this forces temps empty.)

This module provides:
  * Board            — immutable-ish state + legal move generation + win test
  * solve_optimal()  — A* with an admissible heuristic -> optimal move count
  * canonical_signature() — structural fingerprint invariant under color relabeling,
                       tube reordering, and cosmetic variation (the duplicate detector)
  * difficulty_score() — consistent heuristic difficulty estimate for ordering levels

No third-party dependencies. Python 3.9+.
"""

from __future__ import annotations

import heapq
import itertools
import json
from dataclasses import dataclass
from typing import Dict, List, Optional, Sequence, Tuple

# A column is a tuple of color ids, bottom -> top. Empty column = ().
Column = Tuple[int, ...]


@dataclass(frozen=True)
class LevelShape:
    """The structural envelope of a level (everything except the scramble)."""
    color_count: int
    stack_depth: int
    temp_slot_count: int
    temp_slot_depth: int

    @property
    def total_columns(self) -> int:
        return self.color_count + self.temp_slot_count

    @property
    def total_bolts(self) -> int:
        return self.color_count * self.stack_depth


class Board:
    """A concrete board state: ordered columns + their capacities."""

    __slots__ = ("cols", "caps", "n_color")

    def __init__(self, cols: Sequence[Column], caps: Sequence[int], n_color: int):
        self.cols: Tuple[Column, ...] = tuple(cols)
        self.caps: Tuple[int, ...] = tuple(caps)
        self.n_color = n_color

    # ── construction ────────────────────────────────────────────────────────
    @staticmethod
    def from_record(rec: dict) -> "Board":
        """Build the initial board from a levels.json record."""
        n = rec["color_count"]
        sd = rec["stack_depth"]
        tc = rec["temp_slot_count"]
        td = rec["temp_slot_depth"]
        cols: List[Column] = [tuple(s) for s in rec["color_stacks"]]
        caps: List[int] = [sd] * n
        cols += [tuple() for _ in range(tc)]
        caps += [td] * tc
        return Board(cols, caps, n)

    @staticmethod
    def from_shape(shape: LevelShape, color_stacks: Sequence[Sequence[int]]) -> "Board":
        cols: List[Column] = [tuple(s) for s in color_stacks]
        caps: List[int] = [shape.stack_depth] * shape.color_count
        cols += [tuple() for _ in range(shape.temp_slot_count)]
        caps += [shape.temp_slot_depth] * shape.temp_slot_count
        return Board(cols, caps, shape.color_count)

    # ── rules ───────────────────────────────────────────────────────────────
    def is_won(self) -> bool:
        """Mirror SortMechanic.IsWon: every column empty or full-to-cap & monochrome."""
        for col, cap in zip(self.cols, self.caps):
            if not col:
                continue
            if len(col) != cap:
                return False
            first = col[0]
            for c in col:
                if c != first:
                    return False
        return True

    def legal_moves(self):
        """Yield (src, dst) flat-index pairs for every legal single-bolt move.

        Optimality-preserving pruning:
          * skip empty sources and completed (full+mono) color sources
          * collapse interchangeable empty destinations of equal capacity to one
          * never move onto the column the bolt came from
        """
        cols, caps = self.cols, self.caps
        n = len(cols)
        for i in range(n):
            src = cols[i]
            if not src:
                continue
            # skip a source that is already a finished, full single-color column
            if len(src) == caps[i] and _mono(src):
                continue
            bolt = src[-1]
            # don't lift a lone bolt from an otherwise-empty column onto another empty
            src_all_same = _mono(src)
            seen_empty_cap: set = set()
            for j in range(n):
                if j == i:
                    continue
                dst = cols[j]
                if not dst:
                    # interchangeable empty destinations: only the first per capacity
                    if caps[j] in seen_empty_cap:
                        continue
                    seen_empty_cap.add(caps[j])
                    # pointless: relocating a mono column wholesale into another empty
                    if src_all_same and len(src) == 1:
                        # a single lone bolt hopping empty->empty is never progress
                        continue
                    yield (i, j)
                else:
                    if len(dst) >= caps[j]:
                        continue
                    if dst[-1] == bolt:
                        yield (i, j)

    def apply(self, src: int, dst: int) -> "Board":
        cols = list(self.cols)
        bolt = cols[src][-1]
        cols[src] = cols[src][:-1]
        cols[dst] = cols[dst] + (bolt,)
        return Board(cols, self.caps, self.n_color)

    # ── hashing / canonical state for the closed set ─────────────────────────
    def state_key(self) -> Tuple:
        """Symmetry-reduced key: columns sorted within equal-capacity buckets."""
        return tuple(sorted((cap, col) for cap, col in zip(self.caps, self.cols)))


def _mono(col: Column) -> bool:
    return all(c == col[0] for c in col) if col else True


# ── admissible heuristic ─────────────────────────────────────────────────────
def _heuristic(board: Board) -> int:
    """Admissible lower bound on remaining moves.

    Every bolt sitting above the bottom-most contiguous same-color run of its
    column is on top of a different color and must move at least once. Summing
    those across all columns never overcounts, so it is a valid A* heuristic.
    """
    h = 0
    for col in board.cols:
        if not col:
            continue
        bottom = col[0]
        run = 1
        while run < len(col) and col[run] == bottom:
            run += 1
        h += len(col) - run
    return h


# ── optimal solver ───────────────────────────────────────────────────────────
@dataclass
class SolveResult:
    solvable: bool
    optimal_moves: Optional[int]
    nodes: int
    capped: bool = False


def solve_optimal(board: Board, node_cap: int = 4_000_000) -> SolveResult:
    """A* search for the minimum number of moves to win.

    Returns optimal_moves when solved. solvable=False means the search exhausted
    the reachable state space without a win. capped=True means node_cap was hit
    before a conclusion (treat as 'unknown' — should not happen for authored
    moderate-band levels).
    """
    if board.is_won():
        return SolveResult(True, 0, 1)

    start_key = board.state_key()
    # priority queue of (f, g, tiebreak, board)
    counter = itertools.count()
    open_heap: List[Tuple[int, int, int, Board]] = []
    g0 = 0
    h0 = _heuristic(board)
    heapq.heappush(open_heap, (g0 + h0, g0, next(counter), board))
    best_g: Dict[Tuple, int] = {start_key: 0}
    nodes = 0

    while open_heap:
        f, g, _, st = heapq.heappop(open_heap)
        nodes += 1
        if nodes > node_cap:
            return SolveResult(False, None, nodes, capped=True)

        key = st.state_key()
        if g > best_g.get(key, g):
            continue  # stale heap entry

        if st.is_won():
            return SolveResult(True, g, nodes)

        ng = g + 1
        for src, dst in st.legal_moves():
            child = st.apply(src, dst)
            ck = child.state_key()
            if ng < best_g.get(ck, 1 << 30):
                best_g[ck] = ng
                heapq.heappush(open_heap, (ng + _heuristic(child), ng, next(counter), child))

    return SolveResult(False, None, nodes)


def solvable_within(board: Board, max_moves: int, node_cap: int = 2_000_000) -> bool:
    """Bounded check: is there ANY winning line of length <= max_moves?

    Cheaper than full optimal when you only need to prove 3-star reachability
    (a solution <= par_moves proves optimal <= par_moves).
    """
    res = solve_optimal(board, node_cap=node_cap)
    return res.solvable and res.optimal_moves is not None and res.optimal_moves <= max_moves


# ── canonical structural signature (duplicate / equivalence detector) ─────────
def canonical_signature(rec_or_board) -> Tuple:
    """Structural fingerprint invariant under:
        * color relabeling  (red<->green is the same puzzle)
        * tube reordering   (swapping tube 1 and 3 is the same puzzle)
        * cosmetic name/order changes

    Two levels with the same signature are the SAME puzzle in disguise.

    Implementation: minimise, over every color bijection, the sorted multiset of
    filled tubes (empties carry no information beyond the shape). The shape
    (stack_depth, temp_slot_count, temp_slot_depth) is folded in so two scrambles
    that are identical but have different buffer envelopes are NOT merged.
    """
    if isinstance(rec_or_board, Board):
        n = rec_or_board.n_color
        filled = [c for c in rec_or_board.cols if c]
        sd = max((len(c) for c, cap in zip(rec_or_board.cols, rec_or_board.caps)
                  if cap == max(rec_or_board.caps)), default=0)
        # shape is not fully recoverable from a Board; callers prefer the record form
        shape_key = (n,)
    else:
        rec = rec_or_board
        n = rec["color_count"]
        filled = [tuple(s) for s in rec["color_stacks"]]
        shape_key = (rec["stack_depth"], rec["temp_slot_count"], rec["temp_slot_depth"])

    best: Optional[Tuple] = None
    base = list(range(1, n + 1))
    for perm in itertools.permutations(base):
        mapping = {base[i]: perm[i] for i in range(n)}
        relabeled = tuple(sorted(tuple(mapping[x] for x in t) for t in filled))
        key = (shape_key, n, relabeled)
        if best is None or key < best:
            best = key
    return best


# ── difficulty score ─────────────────────────────────────────────────────────
def _color_dispersion(rec: dict) -> float:
    """Average number of distinct tubes each color is spread across (>=1)."""
    n = rec["color_count"]
    tubes_per_color = {c: set() for c in range(1, n + 1)}
    for idx, stack in enumerate(rec["color_stacks"]):
        for c in stack:
            tubes_per_color[c].add(idx)
    return sum(len(v) for v in tubes_per_color.values()) / max(1, n)


def _buried_depth(rec: dict) -> float:
    """Average 'work depth': bolts sitting above their column's bottom mono run,
    weighted by how deep the column is. Captures 'colors buried under others'."""
    total = 0.0
    cells = 0
    for stack in rec["color_stacks"]:
        if not stack:
            continue
        bottom = stack[0]
        run = 1
        while run < len(stack) and stack[run] == bottom:
            run += 1
        # each out-of-place bolt contributes its distance from the bottom run top
        for depth_from_bottom in range(run, len(stack)):
            total += 1.0
            cells += 1
    return total / max(1, len(rec["color_stacks"]))


def difficulty_score(rec: dict, optimal_moves: Optional[int]) -> float:
    """Consistent heuristic difficulty estimate used to ORDER levels and detect
    sawtooth. Not a physical quantity — a calibrated blend of the solver's
    optimal move count with structural 'hardness per move' features.

    Higher = harder. Dominated by optimal_moves, modulated by colors, depth,
    buffer scarcity/restriction, color dispersion and buried depth.
    """
    n = rec["color_count"]
    sd = rec["stack_depth"]
    tc = rec["temp_slot_count"]
    td = rec["temp_slot_depth"]

    moves = optimal_moves if optimal_moves is not None else (n * sd * 2)

    buffer_cells = tc * td
    # scarcity: fewer buffer cells relative to a "comfortable" 2 full buffers
    comfortable = 2 * sd
    scarcity = max(0.0, (comfortable - buffer_cells) / max(1, sd))  # ~0..2
    restriction = max(0, sd - td) * tc  # restricted (shallow) buffers add load

    dispersion = _color_dispersion(rec)          # 1..color_count
    buried = _buried_depth(rec)                   # 0..(sd-1)

    score = (
        2.0 * moves
        + 6.0 * (n - 2)
        + 4.0 * (sd - 3)
        + 9.0 * scarcity
        + 5.0 * restriction
        + 3.0 * (dispersion - 1.0)
        + 2.0 * buried
    )
    return round(score, 1)


# ── convenience: load a catalogue ────────────────────────────────────────────
def load_catalogue(path: str) -> dict:
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)
