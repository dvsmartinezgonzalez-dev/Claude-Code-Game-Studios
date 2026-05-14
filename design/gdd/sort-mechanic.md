# Sort Mechanic

> **Status**: Approved (2026-05-10 — pass 8 lean re-validation)
> **Author**: Design session + systems-designer agent
> **Last Updated**: 2026-05-09
> **Implements Pillar**: Flow Over Friction, Every Pixel Earns Its Place

## Overview

The Sort Mechanic is BoltSort's core interactive system — the complete rule set governing how the player moves bolts between stacks and when a puzzle is solved. It defines three things: which moves are legal (a bolt may only be placed on an empty slot or on top of a matching color), how the player initiates a move (tap to lift the topmost bolt from a source stack; tap a destination to drop), and when the puzzle is won (every color stack is full and contains only one color). All board state — bolt positions, selected piece, undo history — lives in the Game State Manager; the Sort Mechanic is the rule engine that validates proposed moves against that state and emits outcomes. For the player, this system is the game: the satisfying click of a bolt dropping into place, the moment of seeing the path forward through a tangled board, and the burst of clarity when the last bolt slots home.

## Player Fantasy

The player is a technician reading a disordered machine. Each column is a stack of mismatched components that wants to be sorted; each legal move is a step toward order. The player scans, selects, explores: a bolt lifted and tested against a destination, accepted with a quiet click or rejected with a brief shake that says *try again*. That binary feedback — immediate, unambiguous — is what makes the board legible. Every response from the machine is information.

Undo is a design instrument, not an admission of failure. A path that dead-ends can be retraced. The machine keeps full history; the technician can reverse any number of steps. This freedom is what makes bold exploration possible — the cost of a wrong path is a few taps back, not a loss screen.

As the board resolves, the scanning gives way to execution. The path forward becomes readable and moves begin arriving faster than deliberation — the player stops evaluating and starts acting. Column after column fills. The final bolt seats with a precise click. The board reads as clean, sorted columns. The machine is in spec.

A column completing is not celebrated — it is registered. The glow travels top to bottom, a precise mechanical tone sounds, and the player's attention is already on the next move. The defining satisfaction beat is not the win screen; it is the quiet *click* of the last bolt and the board reading clean.

This system is the primary expression of **"Flow Over Friction"** — every interaction must serve the player's ability to read and act, never interrupt it — and the baseline expression of **"The Machine Must Sing"** — every bolt placement must feel mechanically exact.

## Detailed Rules

### Core Rules

**Interaction model: single-bolt, two-tap**

The Sort Mechanic operates on a single-bolt, two-tap model. One tap lifts the topmost bolt from a source; a second tap places it at a destination. Multi-bolt lifting is out of scope — each bolt is moved individually.

**Interaction model rationale (tap-only, not drag):** Tap-only is a deliberate departure from the drag-dominant convention in the sort puzzle genre (Ball Sort Puzzle, Water Sort, Screw Sort 3D all use drag). Each tap is a discrete selection — the player indicates a source or destination to the machine, which responds immediately with acceptance or rejection. Tap implies discrete command input to a system; drag implies continuous manipulation of a physical object. The machine-interface aesthetic fits the sci-fi context: the player is instructing a system. S-05 slide-into-stack misfire protection is provided by the 16dp spatial gap between stack tap targets (see Tuning Knobs).

**Design hypothesis — two-tap and execution-mode flow:** The Player Fantasy promises that scanning gives way to execution — that move cadence accelerates toward automaticity in late-board play. Two-tap (two spatially separated target acquisitions per move) places a higher motor cost per move than a single gesture, which creates tension with this promise. This is an **unvalidated design hypothesis** to be evaluated in the first vertical slice playtest. The playtest success condition is: player move-rate (moves/minute) in the final 25% of a level is measurably higher than in the first 25%, indicating flow-state acceleration despite two-tap. If this condition is not met, alternative input optimizations will be designed in a follow-up spec after playtest data is collected.

**Held-bolt visual model (committed spec):** The held bolt renders above its source stack (not following the player's finger). This preserves spatial context during the deliberation phase — the player sees which stack was lifted from while deciding the destination. This is the definitive implementation for the vertical slice and production; it is not conditional on playtest outcomes for the two-tap interaction model. If the two-tap hypothesis is revised post-playtest, the held-at-source rendering is still the correct model for any two-tap variant.

**Tap definition:** A "tap" throughout this GDD means `TouchPhase.Began` (finger-down), per ADR-0007. Input fires immediately on first contact with no drag threshold guard. S-05 slide-into-stack misfire protection (a finger beginning on empty space and sliding onto a column) is provided by the 16dp spatial gap between tap targets (see Tuning Knobs — S-05 dead-zone), not by a movement threshold. All rule references to "tap" in this document assume this ADR-0007-compliant model.

**Selection rules**

| Rule | Condition | Outcome |
|---|---|---|
| S-01 | Tap a non-empty stack or temp slot top (nothing held) | Topmost bolt lifted into held state. Bolt is immediately removed from source in board state — source is one bolt shorter for all validation. |
| S-02 | Tap an empty stack or temp slot (nothing held) | No selection. No feedback. Sort Mechanic remains in IDLE. No bolt is lifted. (Tapping an empty slot with nothing held is board exploration, not an error — zero feedback is correct.) |
| S-03 | Tap the source slot while a bolt is held | Cancellation. Bolt returns to source. Board state reverts. No undo entry created. |
| S-04 | Tap a different destination while a bolt is held | Move validation runs. If legal: placed. If illegal: bolt stays in hand — player may tap another destination. |
| S-05 | Tap empty board space while a bolt is held | Cancellation. Identical to S-03. |

**Held state:** Contains exactly one bolt and the source reference (stack index + original position). Source reference is used for cancellation (S-03) and undo (GSM returns the bolt to its exact source). Only one bolt is held at a time.

**Move validation rules**

A move is legal if both destination capacity and color constraints are satisfied. The complete decision logic:

```
is_legal_move(held_bolt, destination):
    if destination.bolt_count == 0:              → LEGAL   (empty slot — any color accepted)
    if destination.bolt_count >= capacity:        → ILLEGAL (full — V-04)
    if destination.top.color == held_bolt.color: → LEGAL   (color match — V-02)
    → ILLEGAL                                    (color mismatch — V-03)
```

*Capacity*: `stack_depth` for color stacks; `temp_slot_depth` for temp slots.

Tapping the source while holding is cancellation (S-03), not a validation attempt.

**Win condition**

| Rule | Definition |
|---|---|
| W-01 | Win check runs after every legal bolt placement — not every tap. Check runs on animation completion (end of MOVE_EXECUTING, triggered by the Animation System's completion signal), not at animation start. |
| W-02 | Puzzle is won when: (1) every color stack has exactly `stack_depth` bolts, AND (2) every bolt in every color stack has the same `color_id`. |
| W-03 | Temp slots are not evaluated. If all color stacks satisfy W-02, the `bolt_count_invariant` guarantees no bolts remain elsewhere — temp slots are empty by necessity. |
| W-04 | Win is detected once. All input is locked on transition to WIN state. The win animation plays; the Level Complete UI appears. |

**Deadlock**

True deadlock (no legal move exists from the current board position) is possible through legal play. The authoring-time solvability solver guarantees a solution path at level start; it does not prevent the player from abandoning that path.

Three-layer response:

1. **Hint system (primary)**: A hint always leads back toward the solver's solution path. If zero hints remain and a deadlock is imminent, the Hint System must provide one free hint (pity mechanic). This guarantee is a contract the Hint System GDD must satisfy.
2. **Rewarded ad — extra temp slot**: One temporary additional temp slot (depth 1) unlocked via rewarded ad is sufficient to break single-blocker deadlocks — configurations where one intermediate holding position resolves the jam. It may be insufficient for complex deadlocks requiring multiple concurrent overflow positions (e.g., `color_count = 8, temp_slot_count = 0`). The Hint System GDD must quantify the failure configurations.
3. **Soft detection — hint pulse**: The shallow check runs in two situations: (a) after each legal bolt placement, and (b) on `level_loaded` (immediately after the board is instantiated). Both paths run: "does any legal first move exist from this board?" If no legal first move exists, Sort Mechanic emits `deadlock_detected()`. The hint icon pulses visually. No dialog, no forced intervention. The player pulls the escape valve when ready. The `level_loaded` check ensures boards that are deadlocked at the start (which the authoring solver should prevent but does not guarantee at runtime) are detected immediately rather than silently. On the `level_loaded` path, no bolt is held — board state is the full initial configuration and the check evaluates all columns as potential sources. In this context, move validation is applied by treating each column's topmost bolt as the hypothetical `held_color` parameter: for each non-empty source column `i`, `held_color = stack_contents[i].top`, and the formula evaluates all other columns and temp slots as potential destinations. A "legal first move exists" if at least one (source, destination) pair returns LEGAL from `is_legal_move`. This is identical to the BOLT_SELECTED validation path, simulated for every possible first lift.

**Zero-cost escape (unlimited undo):** Unlimited undo is always available as a free path out of any deadlock. A player who backed themselves into a dead end can undo as many moves as needed to return to a solvable state. GSM is responsible for maintaining full undo history for the duration of a level session. This guarantee must be documented in the GSM GDD.

Exhaustive deadlock verification (full BFS/DFS from current state) is owned by the Hint System's solver. Sort Mechanic runs only the shallow first-move check.

**Deadlock signal durability:** `deadlock_detected()` describes the board state at the moment of emission — it is not a persistent condition. If the player subsequently makes a legal move that breaks the deadlock, no counter-signal is emitted. HUD must not latch the hint pulse permanently; the pulse should deactivate after the player's next legal bolt placement (whether or not a new `deadlock_detected()` fires on the resulting board).

---

### States and Transitions

| State | Entry condition | Exit condition | Sort Mechanic behavior |
|---|---|---|---|
| `IDLE` | Level load; move complete; cancellation complete | Player taps non-empty stack/slot top | Waits for input. No bolt held. |
| `BOLT_SELECTED` | Tap non-empty stack/slot top | → MOVE_EXECUTING (valid destination tapped); → INVALID_MOVE (invalid destination tapped); → CANCELLATION (source or empty space tapped) | Reads board state; validates all destination taps. |
| `INVALID_MOVE` | Invalid destination tapped while in BOLT_SELECTED | → BOLT_SELECTED (rejection animation completes — bolt stays in hand) | Emits `move_rejected`. Bolt remains held. Buffers exactly **one** tap during rejection animation (60–200ms); subsequent taps discarded. On exit to BOLT_SELECTED, fires the buffered tap as a destination evaluation (S-04 rules apply: valid destination → MOVE_EXECUTING; invalid destination → INVALID_MOVE again; source or empty space tap → CANCELLATION). If no tap was buffered, BOLT_SELECTED is re-entered normally. |
| `CANCELLATION` | Source or empty space tapped while in BOLT_SELECTED; Android back gesture in BOLT_SELECTED | → IDLE **immediately on emitting `move_cancelled`** (no animation handshake required — cancel animation plays asynchronously) | Emits `move_cancelled` and transitions to IDLE synchronously. Animation System plays the bolt return animation asynchronously; Sort Mechanic accepts new input immediately without waiting for animation completion. No undo entry. |
| `MOVE_EXECUTING` | Valid destination tapped | → WIN (animation complete + win condition passes); → IDLE (animation complete + win condition fails) | Emits `move_committed`. Buffers one tap input; all subsequent taps discarded. On `animation_complete` receipt, exit sequence: (1) evaluate win condition → (2) if no win and shallow check fails, emit `deadlock_detected()` → (3) fire buffered tap if present (discarded if exit is WIN) → transition to WIN or IDLE. |
| `WIN` | Win condition passes after move completion | UI "next level" tap (outside Sort Mechanic domain) | Emits `puzzle_solved`. No further input processed. |
| `LEVEL_FAILED` | *Provisional — reserved for future move-limit or mandatory-deadlock mechanic* | TBD | Shell state. Not active in MVP. |

During `MOVE_EXECUTING`, Sort Mechanic buffers exactly **one** tap input. If a tap arrives during the animation window, it is held and fired against board state immediately on `MOVE_EXECUTING` exit. If the exit state is `WIN`, the buffered tap is discarded. Subsequent taps beyond the first are discarded — no multi-tap queuing. This prevents silent input loss for players in flow state during animation lock.

**Buffered tap identity:** The buffered tap is processed as a **new selection** — it behaves identically to a tap received in IDLE state. The bolt committed in the completed move is seated at its destination and is no longer held. The buffered tap lifts the topmost bolt of the tapped stack (entering BOLT_SELECTED); it is not a continuation of any previously held bolt. If the tapped stack is empty when the buffered tap fires, it is treated as S-02 (empty source tap — no action, no feedback).

**Android back gesture mapping:** Cancellation while in `BOLT_SELECTED`; pause/options while in `IDLE`.

---

### Interactions with Other Systems

**Game State Manager (Approved — interface resolved)**

> GSM GDD is authored and Approved. The interface below matches the GSM GDD exactly. Any deviation between this section and the GSM GDD is a dependency conflict that must be surfaced before implementation begins.

*Sort Mechanic reads from GSM (pull-on-demand, synchronous):*

| Field | Type | Used for |
|---|---|---|
| `stack_contents[index]` | `array<color_id>` | Top bolt color + stack length (for capacity check) |
| `stack_depth` | int (3–8) | Color stack capacity |
| `temp_slot_contents[index]` | `array<color_id>` | Temp slot top bolt color + length |
| `temp_slot_depth` | int (1–stack_depth) | Temp slot capacity |
| `temp_slot_count` | int (0–3) | Valid temp slot indices for destination validation |
| `color_count` | int (2–8) | How many stacks to evaluate for win condition |
| `move_count` | int (≥ 0) | Read synchronously at WIN entry, immediately before emitting `puzzle_solved`. Used as the `move_count` parameter in `puzzle_solved(move_count: int)`. Sort Mechanic calls `GSM.GetMoveCount()` once at this moment — no caching, no polling. |

*Sort Mechanic does NOT read:* undo history (raw entries), score, session metadata, UI state.

*Sort Mechanic emits (events GSM and other systems subscribe to):*

| Event | Parameters | When | GSM obligation |
|---|---|---|---|
| `move_committed(source, destination, color_id, sequence_id: int64)` | stack/slot indices + color + monotonic counter | Immediately on entering MOVE_EXECUTING — before animation plays | Update board state (remove from source, add to destination). Authoritative state-change moment. `sequence_id` increments on each MOVE_EXECUTING entry; animation completion signal must echo it back for staleness check. **`sequence_id` is session-global `int64` and never resets during a single app session** — prevents stale `animation_complete` cross-level matches if a level is reloaded. Typed as `int64` rather than `int32`: an `int32` wrapping to a negative value in C#'s unchecked context would produce a permanent MOVE_EXECUTING softlock (negative IDs never match future positive signals). `int64` (~9.2 × 10¹⁸) eliminates this failure mode permanently. All event signatures and subscriptions must use `int64` for `sequence_id`. |
| `move_executing_exited(sequence_id: int64)` | `sequence_id` matching the move that completed | On exiting MOVE_EXECUTING via `animation_complete` receipt, **IDLE path only** (win condition failed). NOT emitted on WIN path — on WIN, `puzzle_solved` is the terminal signal and GSM clears any deferred undo on `puzzle_solved` receipt (GSM GDD AC-GSM-15). NOT emitted on watchdog exit — GSM uses `board_refresh_forced` for that path. | Process any deferred undo request (GSM GDD UND-03). |
| `move_cancelled(source, color_id)` | source index + color | On entering CANCELLATION | No board state change. |
| `move_rejected(source, destination, color_id, reason)` | indices + color + `DESTINATION_FULL` \| `COLOR_MISMATCH` | On entering INVALID_MOVE | No board state change. |
| `puzzle_solved(move_count: int)` | total committed moves in this session | On entering WIN | Sort Mechanic calls `GSM.GetMoveCount()` once at WIN entry; the returned value is the `move_count` parameter. This parameter is the authoritative value for all downstream systems (analytics, reward flow). GSM must not re-read its own internal state on `puzzle_solved` receipt — the parameter is canonical. |
| `deadlock_detected()` | — | (a) After each legal placement, if shallow check finds no legal first move from current board; (b) On `level_loaded`, if initial board has no legal first move | HUD subscribes to pulse hint button. On the post-placement path: emitted after `move_committed`, during MOVE_EXECUTING exit sequence (after win check fails), before buffered tap is fired. On the `level_loaded` path: emitted after `level_loaded` is received and board state is readable. |
| `level_load_failed(reason: CORRUPTED_BOARD_STATE)` | `reason: enum` | On initialization assertion failure (`len(color_stacks) ≠ color_count`) | HUD or session controller must subscribe and present a recovery path (e.g., return to level select). Sort Mechanic refuses all input after emitting this event. A hard crash is not acceptable on mobile — the player must be able to navigate away. |

**Animation System (Designed — contract resolved)**

Sort Mechanic emits `move_committed` → GSM processes it synchronously (BSM-01) and emits `board_state_changed` → Animation System subscribes to GSM's `board_state_changed` and drives the visual. The Animation System is not a direct subscriber of `move_committed` from Sort Mechanic; the trigger arrives via GSM's `board_state_changed` event. Board state is fully committed before Animation System receives the signal (BSM-06 guarantees this).

**Cancel animation (CANCELLATION path):** When Sort Mechanic emits `move_cancelled`, Animation System subscribes to this event and plays the bolt-return animation asynchronously. Sort Mechanic transitions to IDLE immediately on emitting `move_cancelled` — it does not wait for cancel animation completion. No `animation_complete` signal is expected or required for the cancel path. (See CANCELLATION state and EC for implications.)

**BM-06 completion signal authority:** The cancel-return animation (BM-06) must NOT emit `animation_complete` when it completes. CANCELLATION exits to IDLE synchronously on `move_cancelled` emission — no completion handshake is required or expected on the cancellation path. This Sort Mechanic GDD is authoritative on this contract. BM-06 in the Animation System GDD must be updated to remove any claim that it emits a completion signal on the cancel path; the CANCELLATION → IDLE transition is driven by Sort Mechanic, not the Animation System.

**Animation System interrupt contract:** If a new `board_state_changed` event arrives for the same source column while a cancel-return animation (BM-06) is still in progress on that column, the Animation System must cancel the return animation immediately (bolt snaps to rest position at source) before beginning the new lift animation. Without this rule, a rapid lift-cancel-re-lift on the same column produces two concurrent animations competing for the same visual slot.

When bolt placement animation completes, Animation System emits **`animation_complete(sequence_id: int64)`** (resolved — Animation System GDD confirms this name and signature via BM-04). Sort Mechanic listens for this signal to evaluate win condition and exit `MOVE_EXECUTING`. Sort Mechanic discards `animation_complete` signals whose `sequence_id` does not match the current `sequence_id` issued in `move_committed` — this is the stale-signal guard (see EC-11). Sort Mechanic has no authority over animation — it only fires events and listens for completion on the MOVE_EXECUTING path.

**Level Data System (indirect)**

Sort Mechanic never calls Level Data System directly. It reads `stack_depth`, `temp_slot_depth`, `temp_slot_count`, and `color_count` from GSM's board state, which GSM populated from the level record at load time.

**In-Game HUD (downstream)**

HUD subscribes to `move_committed`, `move_rejected`, `move_cancelled`, and `puzzle_solved` for move counter display, feedback animations, and state-appropriate button visibility (hint button pulse on deadlock detection). HUD has no read access to Sort Mechanic state directly.

## Formulas

### Move Legality Formula

Determines whether placing a held bolt at a destination is a legal move.

```
if destination_bolt_count == 0:                           → LEGAL  (empty; destination_top_color not read)
else if destination_bolt_count >= destination_capacity:   → ILLEGAL (full)
else if destination_top_color == held_color:              → LEGAL  (color match, not full)
else:                                                     → ILLEGAL (color mismatch)
```

> **Implementation note:** The formula must be evaluated as a guarded conditional, not a flat boolean expression. An eager boolean evaluator would read `destination_top_color` before the empty-slot guard, where it is undefined. The pseudocode above is the authoritative form; any symbolic rendering is informational only.

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Bolts currently in destination slot | `destination_bolt_count` | int | 0–max(stack_depth, temp_slot_depth) | Current occupancy of the destination stack or temp slot |
| Color ID of the destination's top bolt | `destination_top_color` | int | 1–8 | Only evaluated when `destination_bolt_count > 0` |
| Color ID of the held bolt | `held_color` | int | 1–8 | Color of the bolt currently lifted |
| Max capacity of the destination slot | `destination_capacity` | int | 3–8 (stack) or 1–8 (temp) | `stack_depth` for color stacks; `temp_slot_depth` for temp slots. For temp slots, `temp_slot_depth` is itself bounded above by `stack_depth` (enforced by initialization assertion 2). |

**Output:** Boolean — LEGAL or ILLEGAL.

**Short-circuit evaluation:** If `destination_bolt_count == 0`, the result is immediately LEGAL — `destination_top_color` is not read (undefined for empty slots). Implementors must not dereference top color on an empty slot.

**Example calculations:**

| Scenario | destination_bolt_count | destination_top_color | held_color | destination_capacity | Result |
|---|---|---|---|---|---|
| Empty stack | 0 | — | any | any | LEGAL |
| Color match, not full | 2 | 3 | 3 | 4 | LEGAL |
| Color mismatch | 2 | 3 | 1 | 4 | ILLEGAL |
| Full stack, color match | 4 | 3 | 3 | 4 | ILLEGAL |
| Temp slot, depth 1, empty | 0 | — | any | 1 | LEGAL |
| Temp slot, depth 1, occupied | 1 | 2 | 2 | 1 | ILLEGAL (full) |

---

### Win Condition Formula

Determines whether the current board state is a solved puzzle.

`is_won = ∀ stack ∈ color_stacks: (len(stack) == stack_depth) AND (all_same_color(stack))`

where `all_same_color(stack)` means all elements in the `stack` array have the same `color_id` value — equivalently, every element equals `stack[0]`. No canonical per-stack color is assigned during play; the win condition requires only internal uniformity within each stack.

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Number of color stacks | `color_count` | int | 2–8 | From GSM board state (sourced from level record) |
| Bolts per color stack | `stack_depth` | int | 3–8 | From GSM board state |
| Contents of stack i | `color_stacks[i]` | array\<color_id\> | length 0–stack_depth | Ordered array for each of the `color_count` stacks |

**Output:** Boolean — WIN or NOT_WIN.

**Evaluation:** Iterate over all `color_count` stacks. For each: check length equals `stack_depth` (fast reject), then check all entries have the same `color_id`. Both conditions must hold for all stacks. Temp slots are excluded from evaluation.

**Connection to bolt_count_invariant:** If `is_won = TRUE`, the invariant (`total_bolts = color_count × stack_depth`) guarantees all bolts are accounted for in color stacks. Temp slots are empty as a logical consequence — no separate temp-slot emptiness check is needed.

**Initialization assertion:** Before any interaction begins, Sort Mechanic must assert three conditions: (1) `len(color_stacks) == color_count` — a mismatch would silently exclude stacks from the win check; (2) `temp_slot_depth ≤ stack_depth` — a violation allows the player to hoard more bolts in temp than any color stack can hold, breaking intended level difficulty without detection; (3) all `color_id` values present in the level data must belong to the domain `{1..color_count}` — an undeclared (phantom) `color_id` passes the bolt-count invariant but makes the win condition structurally unreachable (no stack can fill monochromatically with the correct color). If any assertion fails, Sort Mechanic emits `level_load_failed(reason: CORRUPTED_BOARD_STATE)` (see Events table) and refuses all further input. This is a soft block — the game remains running but all tap events are silently discarded. The HUD or session controller subscribes to `level_load_failed` and must present a recovery path (return to level select). A hard crash is not acceptable on mobile.

**Example:** `color_count = 3`, `stack_depth = 4`

| Stack | Contents | Full? | Monochromatic? |
|---|---|---|---|
| Stack 0 | [1,1,1,1] | ✓ | ✓ |
| Stack 1 | [2,2,2,2] | ✓ | ✓ |
| Stack 2 | [3,3,3,3] | ✓ | ✓ |
| **Result** | | | **WIN** |

| Stack | Contents | Full? | Monochromatic? |
|---|---|---|---|
| Stack 0 | [1,1,1,1] | ✓ | ✓ |
| Stack 1 | [2,2,2,3] | ✓ | ✗ |
| Stack 2 | [3,3,2,2] | ✓ | ✗ |
| **Result** | | | **NOT_WIN** |

---

### Referenced Formula (not re-defined here)

**bolt_count_invariant** — owned by `design/gdd/level-data-system.md`. The Sort Mechanic's win condition correctness depends on this invariant holding at level load. If `total_bolts ≠ color_count × stack_depth`, the win state is structurally unreachable — the puzzle cannot be solved. GSM verifies the invariant at board initialization; Sort Mechanic trusts this check has passed before any interaction begins.

**Assertion 3 ownership:** GSM's L-03 is the primary enforcement point — it runs the per-color domain check before Sort Mechanic sees the board. Sort Mechanic's assertion (3) is a defensive backstop that activates only when the board is loaded outside the normal GSM flow (e.g., in test harnesses injecting board state directly). If GSM's L-03 fires, Sort Mechanic's assertion (3) is never reached. HUD subscribes to Sort Mechanic's `level_load_failed(CORRUPTED_BOARD_STATE)` for recovery UI (not GSM's internal `session_load_failed`).

**Runtime win-check array length guard:** The `len(color_stacks) == color_count` assertion runs at initialization. Implementors must also add `Debug.Assert(stack_contents.Length == color_count)` inside the win check function itself to guard against array length mutation by a defective undo implementation. A silent inclusion or exclusion of stacks at win-check time would produce false positives or negatives that the one-time init assertion cannot catch.

**Invariant trust contract enforcement:** This is a verbal contract that must be backed by a named BLOCKING acceptance criterion in the GSM GDD. A corresponding integration test must exist in `tests/integration/gsm-sort-mechanic/` verifying that a level loaded with `total_bolts ≠ color_count × stack_depth` is caught at GSM initialization and causes Sort Mechanic to emit `level_load_failed`. Without this GSM AC, a malformed level passes initialization silently, creates an unsolvable puzzle, and traps the player: `deadlock_detected()` never fires (legal moves exist; the board simply has no winning state), and the only exit is unlimited undo to a dead end.

## Edge Cases

**EC-01 — Tap on the only empty color stack while holding a bolt of any color:** Legal (V-01 — empty slot accepts any color). The bolt must be placed even if its color "doesn't belong" in that stack by visual position. The Sort Mechanic does not assign a canonical color to a stack — any bolt may occupy any stack at any time. The win condition enforces monochromatic stacks at completion, not during play.

**EC-02 — Temp slot depth = 1 (single-bolt holding area): placing then immediately lifting:** A bolt placed into a depth-1 temp slot may be lifted on the very next tap. This is legal — no lock-in timer or cool-down. The player may use the temp slot as a pure intermediate holding position. No special case needed in Sort Mechanic logic.

**EC-03 — `temp_slot_count = 0`: player attempts to tap a temp slot area:** There are no temp slot objects instantiated — no tap targets exist in the temp slot region. Sort Mechanic receives no tap event from that area. If the UI renders an "empty" temp slot zone for `temp_slot_count = 0`, the tap must be treated as tapping empty board space (→ cancellation if holding, → nothing if not holding).

**EC-04 — Board initially solved (pre-won level):** The Level Data System passes a warning at authoring time but does not block export (Level Data System GDD, EC-09). Pre-won boards are a Level Data authoring error; the Level Data System's export tooling must prevent them. At runtime, Sort Mechanic detects this on `level_loaded`: the pre-won check runs **before** the deadlock check. If `is_won` evaluates TRUE against the initial board state, Sort Mechanic immediately emits `puzzle_solved(move_count: 0)` and transitions to WIN, locking all input. `move_count = 0` is correct — no player moves were made. Level Complete UI appears without player interaction. The deadlock check is skipped entirely on this path (no `deadlock_detected()` emitted). GSM must not auto-win at initialization — Sort Mechanic owns the win detection via the `level_loaded` check.

**EC-05 — Player holds a bolt, app is backgrounded, then foregrounded:** Held state is never persisted across app backgrounding. EC-14 governs this: Sort Mechanic transitions BOLT_SELECTED → CANCELLATION → IDLE synchronously within `OnApplicationPause(true)`, returning the held bolt to source before GSM serializes board state. On foreground restore, Sort Mechanic is in IDLE and GSM has a complete board with `total_bolts = color_count × stack_depth`. No held-bolt restoration path exists or is needed. See EC-14 for implementation requirements and AC-28 for the integration test.

**EC-06 — Win detected while app is backgrounding (race condition):** If `puzzle_solved` is emitted and the app is simultaneously backgrounded, the win state must be written to GSM's persistent state before the process suspends. The Level Complete UI may not appear until foreground is restored — this is acceptable. Sort Mechanic remains in WIN state. No double-win can occur because WIN state discards all input.

**EC-07 — All color stacks and all temp slots are full, player attempts a move:** By the bolt count invariant, this state is only reachable if the puzzle is already solved (all stacks full and monochromatic) — win fires before this is reached in a valid level. If somehow reached through data corruption: all move attempts return ILLEGAL (V-04 — full). Sort Mechanic enters a soft deadlock; the hint pulse triggers immediately.

**EC-08 — `move_committed` emitted; animation system crashes mid-animation:** Board state is already updated (optimistic commit). Sort Mechanic remains in MOVE_EXECUTING indefinitely, waiting for `animation_complete`. GSM implements a watchdog timeout: if no `animation_complete` arrives within `watchdog_timeout_ms` (1500ms — confirmed by GSM GDD Tuning Knobs), GSM emits **`board_refresh_forced(sequence_id: int64)`** (resolved — GSM GDD WDG-01). Sort Mechanic subscribes to this signal and, if in MOVE_EXECUTING on receipt, re-reads board state from GSM and **runs the win condition check before transitioning**. If `is_won` evaluates to TRUE (the animation crash occurred on the final winning move), Sort Mechanic transitions to WIN and emits `puzzle_solved(move_count)` — the puzzle is correctly completed despite the crash. If `is_won` is FALSE, Sort Mechanic transitions to IDLE. Board reflects the committed (correct) state in either case — the move is preserved. `move_executing_exited` is NOT emitted on watchdog exit; GSM uses `board_refresh_forced` to coordinate deferred undos on the watchdog path (see GSM GDD UND-03). Failure to run the win check on this path produces a softlock: the player is returned to IDLE on a won board with full input re-enabled and no path to WIN.

**Frame-gap discard rule:** Sort Mechanic silently discards any `board_refresh_forced` signal received outside MOVE_EXECUTING state. If Sort Mechanic exits MOVE_EXECUTING on a valid `animation_complete` in the same frame that the watchdog timer expires, GSM may emit `board_refresh_forced` before receiving `move_executing_exited`. Sort Mechanic in IDLE must discard this signal without state change or event emission.

**EC-09 — Player taps extremely rapidly (two or more destinations before MOVE_EXECUTING exits):** The first tap during MOVE_EXECUTING is buffered and fired on exit. All subsequent taps during the same window are discarded. If the exit state is WIN, the buffered tap is discarded. This ensures fast players never lose a committed next-move, while bounding queue depth to prevent runaway input replay.

**EC-10 — `color_count = 2`, `stack_depth = 3` (minimum board):** Valid board. Win condition iterates over 2 stacks — trivially fast. No special case. The minimum board must function identically to a maximum board.

**EC-11 — Player uses undo during MOVE_EXECUTING:** Undo is owned by GSM. The HUD undo button must be disabled while Sort Mechanic is in MOVE_EXECUTING. If undo fires despite this, `animation_complete` will arrive carrying the `sequence_id` of the undone move. Sort Mechanic compares the signal's `sequence_id` against the current MOVE_EXECUTING `sequence_id`: if they do not match, the signal is silently discarded. Sort Mechanic does not need GSM to notify it of undos — the sequence_id mismatch is sufficient. If `animation_complete` for the current sequence_id never arrives (because undo cleared the move), EC-08 watchdog recovery handles the timeout.

**EC-12 — Win condition fires on the last bolt of a stack that completes mid-sequence:** If the placed bolt completes the final remaining non-monochromatic stack, the win check runs immediately and finds all stacks complete. Puzzle is won. Correct and desired — earliest possible win detection is the rule (W-01).

**EC-13 — Board state during BOLT_SELECTED reflects bolt as removed from source:** When the player lifts a bolt (S-01), Sort Mechanic removes it from source in board state immediately. During BOLT_SELECTED, the board reflected in valid-destination highlights is the post-lift state: the source stack appears one bolt shorter than at rest. This is intentional — it simplifies validation logic and accurately represents the board state that will exist if the player commits the move or cancels. On near-full boards, this can make the source stack appear to have one additional free slot. This is expected behavior, not a bug. On cancel (S-03/S-05), the bolt returns and board state reverts to its pre-lift state.

**EC-14 — App paused (`OnApplicationPause(true)`) while in BOLT_SELECTED:** If the app is backgrounded during BOLT_SELECTED, Sort Mechanic must transition to CANCELLATION → IDLE synchronously within the `OnApplicationPause(true)` handler, before GSM serializes board state. The held bolt is returned to its source, restoring `total_bolts = color_count × stack_depth`. No held state persists across sessions. Without this rule, S-01's immediate bolt removal from board state combined with GSM's non-serialization of held state (BSM-02) would yield a deserialized board with one fewer bolt than required — making the win condition structurally unreachable with no signal to the player. GSM must not serialize board state until Sort Mechanic has completed its IDLE transition on this path. **SEO ordering requirement:** This guarantee requires that Sort Mechanic's `OnApplicationPause(true)` handler executes before GSM's. ADR-0001's Script Execution Order must be extended with an explicit entry for `OnApplicationPause` ordering: Sort Mechanic must have a lower SEO number (higher execution priority) than GSM. Without this, execution order is platform-dependent and the cancellation-before-serialization guarantee cannot be enforced.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| Game State Manager | Upstream — Sort Mechanic depends on it | Data + event contract. Sort Mechanic reads board state synchronously; emits all move events to GSM. | Reads: `stack_contents`, `stack_depth`, `temp_slot_contents`, `temp_slot_depth`, `temp_slot_count`, `color_count`. Emits: `move_committed(source, destination, color_id, sequence_id: int64)`, `move_cancelled(source, color_id)`, `move_rejected(source, destination, color_id, reason)`, `puzzle_solved(move_count: int)`, `move_executing_exited(sequence_id: int64)`, `deadlock_detected()`. Listens: `board_refresh_forced(sequence_id: int64)` (watchdog signal — GSM GDD WDG-01). |
| Level Data System | Upstream — indirect | Data dependency via GSM. Sort Mechanic never calls Level Data System directly. | No direct interface. Relies on GSM having loaded the level record. |
| Animation System | Downstream — Sort Mechanic is upstream | Event dependency. Animation System subscribes to **GSM's `board_state_changed`** (canonical trigger) to drive bolt move animations. Sort Mechanic is upstream of GSM, which is upstream of Animation System — the path is Sort Mechanic → GSM → Animation System. Animation System also subscribes to `move_cancelled` and `move_rejected` directly from Sort Mechanic for cancel/rejection animations. Animation System emits `animation_complete(sequence_id: int64)` which Sort Mechanic listens to in order to exit MOVE_EXECUTING only (not CANCELLATION). | Emits (consumed by AS directly): `move_cancelled(source, color_id)`, `move_rejected(source, destination, color_id, reason)`. Indirect trigger: `move_committed` → GSM → `board_state_changed` → AS. Listens: `animation_complete(sequence_id: int64)` on MOVE_EXECUTING path only. |
| In-Game HUD | Downstream — Sort Mechanic is upstream | Event dependency. HUD subscribes to Sort Mechanic events for display updates. | Emits (consumed by HUD): `move_committed`, `move_rejected`, `move_cancelled`, `puzzle_solved`. |
| Hint System | Downstream — Sort Mechanic is upstream | Deadlock detection contract. Hint System must guarantee a free hint when deadlock is imminent (pity mechanic). **PROVISIONAL — Hint System has no GDD yet. The pity mechanic guarantee described in this GDD is non-binding in implementation until `hint-system.md` exists and is Approved.** | Sort Mechanic emits `deadlock_detected()` on shallow deadlock detection. Hint System owns exhaustive solve verification. |

**Hard vs. soft dependencies:**
- Game State Manager: **hard** — Sort Mechanic cannot validate or process any input without reading board state from GSM.
- Level Data System: **soft indirect** — depends on GSM having already loaded the level record before interaction begins.
- Animation System: **soft** — Sort Mechanic emits events regardless. If Animation System is absent (e.g., test environment), Sort Mechanic must have a timeout fallback to exit MOVE_EXECUTING (see EC-08).
- In-Game HUD, Hint System: **soft** — Sort Mechanic emits events regardless of whether listeners are subscribed.

**Bidirectional consistency:** Each system listed as downstream above must reference Sort Mechanic in their own Dependencies section. This table is the authoritative record of upstream connections for those GDDs to verify against.

**Dependency note:** The Game State Manager and Animation System GDDs are both authored. The interface contracts in this GDD have been verified against both downstream GDDs. Any future change to GSM or Animation System event signatures must be reconciled here.

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect if Too High | Effect if Too Low |
|---|---|---|---|---|
| Animation duration — bolt move | *Delegated to Animation System GDD* | *See Animation System GDD F-01: `travel_min_ms` (80ms) — `travel_max_ms` (300ms), distance-proportional* | *Owned by Animation System GDD — do not tune here* | *Owned by Animation System GDD — do not tune here* |
| Animation duration — rejection shake | 120ms | 60–200ms | Interrupts flow; feels punishing | Rejection is not perceived; player thinks the tap was ignored |
| Animation duration — stack completion glow | 400ms | 200–600ms | Player attention delayed before next move; anti-flow | Completion beat is not felt; "The Machine Must Sing" fails |
| Tap target radius (bolt/stack) | 44pt (iOS HIG) / 48dp (Android HIG) | 44–72pt iOS / 48–72dp Android | Targets overlap on boards with many stacks; mis-taps increase | Mis-taps on small phone screens; accessibility failure |
| Shallow deadlock check depth | 1 (any legal first move?) | 1–3 moves | Deeper check → CPU cost per move on low-end devices | Depth 1 is sufficient for hint pulse trigger; deeper detection owned by Hint System |
| Input lock during MOVE_EXECUTING | One-tap buffer (no added delay) | One buffered tap | N/A — buffer is bounded to 1 tap | N/A — buffer always enabled |
| S-05 empty-space cancellation dead-zone | 16dp minimum gap between stack tap targets | 8–24dp | Mis-taps on valid stacks increase as dead-zone shrinks | Accidental cancellations increase as dead-zone grows |

**Knob interactions:**
- Animation duration and tap target radius must be calibrated together: fast animations require larger tap targets to compensate for reduced time to predict landing position.
- **Timing invariant:** `rejection_shake_ms < travel_min_ms` must always hold — a rejected move must never feel as heavy as a committed one. Enforce with a boot-time assertion: `Debug.Assert(rejectionShakeMs < travelMinMs, "Rejection shake must be shorter than minimum bolt travel");`
- Shallow deadlock check at depth 1 is O(N(N-1)) — each non-empty source tested against all other columns (self-comparison excluded since tapping source while holding is cancellation, not placement). Maximum = O(110) at N=11 columns (8 color stacks + 3 temp slots). Negligible on any mobile device. Increasing depth introduces exponential branching; exhaustive detection is the Hint System's domain.

**Scope boundary:** `stack_depth`, `color_count`, `temp_slot_count`, and `temp_slot_depth` are level authoring parameters owned by Level Data System and Level Progression GDDs. These knobs define Sort Mechanic interaction feel only.

**Column cap constraint (hard design rule):** `color_count + temp_slot_count` must not exceed **8** in any level. On a 375pt viewport (iPhone SE — the floor of the target demographic), 8 columns = 47pt per column, which satisfies the 44pt tap target minimum. Exceeding 8 columns makes the tap target constraint impossible to satisfy without a layout redesign. Level Progression GDD must enforce this cap.

**Column cap implementation risk:** At the 8-column limit, the tap target margin is exactly 3pt above the iOS HIG minimum — one logical pixel at 3× retina. No UI padding, border, or gap treatment may consume this margin. **On Android, the HIG minimum is 48dp — 8-column layouts at equivalent viewport density fall below this floor.** 8-column levels require explicit UI implementation review and on-device measurement on both platforms before shipping. The UX Designer must verify tap zone compliance on target iOS and Android hardware and provide screenshot evidence in `production/qa/evidence/`. 47pt (iOS) / 48dp (Android) is the hard floor; the effective tap zone must not be reduced by visual chrome.

**High-deadlock configuration note:** `color_count = 8, temp_slot_count = 0` is the highest deadlock-risk configuration — maximum bolt density with zero overflow capacity. The hint pity mechanic and rewarded-ad extra temp slot are the only non-undo escape paths, and the Hint System GDD is not yet authored. Level authors should avoid this combination until the Hint System pity mechanic is implemented.

## Visual/Audio Requirements

*These are requirements Sort Mechanic places on the Animation and Audio systems — not implementation specs. Animation System and Audio System GDDs own the how.*

| Event | Visual requirement | Audio requirement |
|---|---|---|
| Bolt lift (S-01) | Bolt raises above stack; shadow or glow indicates "held" state | Short metallic lift sound — distinct from drop |
| Bolt drop — legal (move_committed) | Bolt travels arc from source to destination; seats with visible micro-settle | Precise mechanical click — "The Machine Must Sing" anchor moment |
| Bolt drop — illegal (move_rejected) | Destination shakes briefly (rejection micro-animation); bolt stays visually held | Short negative tone — softer than the accept click, not harsh |
| Empty source tap (S-02) | No feedback — this is board exploration, not an error | No sound |
| Stack completion | Glow travels top-to-bottom through completed stack | Ascending tone or chime — distinct from individual bolt drop |
| Win (puzzle_solved) | Board-wide completion visual | Win fanfare — distinct from per-stack completion |
| Cancellation (move_cancelled) | Bolt returns to source with abbreviated version of drop animation | Soft declicking sound — bolt re-seating, no negative connotation |

**Constraints for Animation System:**
- Bolt move animation: 80–300ms (see Animation System GDD F-01 — distance-proportional, `travel_min_ms`/`travel_max_ms`). Must complete before input is re-enabled.
- Rejection shake: 60–200ms. Must complete before INVALID_MOVE auto-exits to BOLT_SELECTED.
- Animation System must emit a completion signal when bolt placement animation finishes so Sort Mechanic can exit MOVE_EXECUTING and evaluate the win condition.

## UI Requirements

*The Sort Mechanic does not own any UI directly. These are constraints placed on the In-Game HUD.*

- **Held bolt visual**: HUD must render a clear "held" indicator — the lifted bolt must be visually distinguished from in-stack bolts at all times. The held bolt renders **above its source stack** (not following the player's finger and not in a fixed HUD slot). This preserves spatial context for the two-tap model: the player sees which stack was lifted from while deciding the destination. Failure to render the bolt above the source breaks the mental model for BOLT_SELECTED state.
- **Valid-destination highlighting**: HUD must highlight all legal destination stacks/slots while a bolt is held (BOLT_SELECTED). A legal destination is any stack or slot that would pass move validation for the currently held bolt. Without this signal, players must trial-and-error destinations in violation of "Flow Over Friction." Highlight must activate on bolt lift (BOLT_SELECTED entry) and clear on placement or cancellation. **On rejection (INVALID_MOVE → BOLT_SELECTED): highlight must re-activate immediately on re-entry to BOLT_SELECTED** — the player is still holding the bolt and needs full destination affordance. A player returning from INVALID_MOVE must see the same highlighting they had before tapping the illegal destination. Failing to re-activate on rejection creates a no-affordance state: the player holds a bolt with no visual indication of where to go.
- **Hint button state**: HUD must expose a hint button that Sort Mechanic can pulse when shallow deadlock detection triggers. Hint button must be disabled (non-tappable) during MOVE_EXECUTING.
- **Undo button state**: HUD undo button must be disabled during MOVE_EXECUTING. Undo during animation is an undefined state (see EC-11).
- **Tap target sizes**: All stack and temp slot tap targets must meet minimum 44pt (iOS) / 48dp (Android) touch target size per platform HIG. Tap target sizing is a UI implementation concern but Sort Mechanic's correctness depends on it — mis-taps on undersized targets produce incorrect input events.

## Acceptance Criteria

> **Test type**: Logic (move validation, state machine, win condition). AC-01, AC-02, AC-03, AC-04, AC-05a, AC-06, AC-07, AC-08a, AC-08b, AC-08c, AC-11, AC-12, AC-15a, AC-16, AC-18a, AC-18b, AC-18c, AC-21, AC-22, AC-24, AC-27, AC-29a, AC-29b, AC-30, AC-30b are BLOCKING — automated unit tests required in `tests/unit/sort-mechanic/` before implementation stories can be marked Done. AC-05b, AC-10, AC-13, AC-15b, AC-19, AC-23, AC-25, AC-26, AC-28, AC-31 are integration tests (`tests/integration/`). AC-20 is a UI integration test (Sort Mechanic + HUD). AC-09, AC-14, AC-17 are advisory unit-tier tests. (AC-13 is integration tier — "GSM board state updated before first animation frame" requires a real GSM + Animation System harness to verify.)

**AC-01 — Legal move: empty destination accepts any color** *(BLOCKING)*
**GIVEN** a board with one empty color stack and the player holding a bolt of color 3, **WHEN** the player taps the empty stack, **THEN** `move_committed(source, empty_stack, color_id=3)` is emitted, Sort Mechanic transitions to MOVE_EXECUTING, and the bolt is placed regardless of what color "belongs" in that stack.

**AC-02 — Legal move: color match, stack not full** *(BLOCKING)*
**GIVEN** a stack containing [1,1] with `stack_depth = 4`, and the player holding a bolt of color 1, **WHEN** the player taps that stack, **THEN** `move_committed` is emitted and the bolt is placed. Stack becomes [1,1,1].

**AC-03 — Illegal move: color mismatch — bolt stays in hand** *(BLOCKING)*
**GIVEN** a stack with top bolt of color 2 and the player holding a bolt of color 1, **WHEN** the player taps that stack, **THEN** `move_rejected(source, destination, color_id=1, reason=COLOR_MISMATCH)` is emitted, Sort Mechanic transitions to INVALID_MOVE then back to BOLT_SELECTED, and the player is still holding the bolt.

**AC-04 — Illegal move: full destination — bolt stays in hand** *(BLOCKING)*
**GIVEN** a stack of [3,3,3,3] with `stack_depth = 4` (full) and the player holding a bolt of color 3, **WHEN** the player taps that stack, **THEN** `move_rejected(source, destination, color_id=3, reason=DESTINATION_FULL)` is emitted and the bolt remains held.

**AC-05a — Win condition: puzzle_solved emitted and WIN entered** *(BLOCKING — unit test)*
**GIVEN** a board with `color_count = 2`, `stack_depth = 3`, stack A = [1,1] and stack B = [2,2,2] (B full and monochromatic; A has one slot remaining), with the player in BOLT_SELECTED holding a bolt of color 1, and a mock GSM configured to return `GetMoveCount() = 2`, **WHEN** the player taps stack A (the legal destination) and `animation_complete` is received, **THEN** `puzzle_solved` is emitted (any `move_count` parameter value is accepted at this tier), Sort Mechanic transitions to WIN, and all subsequent tap events produce no signal emission and no board state change.

**AC-05b — Win condition: move_count parameter value** *(Integration test — requires real GSM)*
**GIVEN** a board with `color_count = 2`, `stack_depth = 3`, with 2 prior committed moves recorded in a real GSM's undo history, and stack A = [1,1] with the player holding a bolt of color 1 in BOLT_SELECTED, **WHEN** the player taps stack A and `animation_complete` is received, **THEN** `puzzle_solved(move_count: 3)` is emitted — GSM's `GetMoveCount()` returns 3 after this final `move_committed` is recorded. Verified at integration tier with a real GSM instance tracking undo history.

**AC-06 — Win condition: not triggered with monochromatic but non-full stacks** *(BLOCKING)*
**GIVEN** a board with `color_count = 2`, `stack_depth = 3`, stack A = [1,1] (not full; one slot remaining) and stack B = [2,2,2] (full and monochromatic; B is complete), with the player placing any bolt into stack A, **WHEN** the placement completes and `animation_complete` is received, **THEN** `puzzle_solved()` is NOT emitted and Sort Mechanic transitions to IDLE (not WIN).

**AC-07 — Cancellation: tap source returns bolt, no undo entry** *(BLOCKING)*
**GIVEN** the player has lifted a bolt from stack index 2 (BOLT_SELECTED), and a mock GSM with a spy on its undo-write method (`RecordUndo()` or equivalent), **WHEN** the player taps stack index 2 again (the source), **THEN** `move_cancelled(source=2, color_id)` is emitted, Sort Mechanic transitions to CANCELLATION then IDLE, the board state is identical to its pre-lift state, and the mock GSM spy confirms `RecordUndo()` was not called at any point during or after the cancellation.

**AC-08a — No events emitted during MOVE_EXECUTING (beyond first buffered tap)** *(BLOCKING)*
**GIVEN** Sort Mechanic is in MOVE_EXECUTING state and one tap has already been buffered, **WHEN** additional taps arrive, **THEN** Sort Mechanic emits no signal of any kind. Verified with an event-bus spy confirming zero events after the first buffered input.

**AC-08b — No state transition during MOVE_EXECUTING (beyond first buffered tap)** *(BLOCKING)*
**GIVEN** Sort Mechanic is in MOVE_EXECUTING state, **WHEN** additional taps arrive after the first buffered tap, **THEN** Sort Mechanic remains in MOVE_EXECUTING state. State machine must not drift to IDLE, BOLT_SELECTED, or WIN on discarded input.

**AC-08c — No GSM board state write during MOVE_EXECUTING (beyond first buffered tap)** *(BLOCKING)*
**GIVEN** Sort Mechanic is in MOVE_EXECUTING state, **WHEN** additional taps arrive after the first buffered tap, **THEN** no write to GSM board state occurs. Verified by injecting a mock GSM and confirming no mutation methods are called after the first tap.

**AC-09 — Empty source tap: no selection, no Sort Mechanic events**
**GIVEN** the player has no bolt held and taps an empty color stack, **THEN** Sort Mechanic remains in IDLE, no event of any kind is emitted by Sort Mechanic, and no board state change occurs. (Visual and audio silence is a requirement on the Animation and Audio systems — out of scope for Sort Mechanic unit tests.)

**AC-10 — Deadlock detection: `deadlock_detected()` emitted when no legal first move** *(Integration test — not unit tier)*
**GIVEN** a board state where every stack and every temp slot has a non-empty top bolt and every possible first move fails move validation (use the canonical deadlock fixture in `tests/helpers/sort-mechanic-fixtures`), **WHEN** a bolt placement that creates this state completes and `animation_complete` is received, **THEN** Sort Mechanic emits `deadlock_detected()` before the buffered tap (if any) is fired and before further input is accepted. Verified at integration tier by asserting signal emission order from Sort Mechanic's event bus. **The `tests/helpers/sort-mechanic-fixtures` file defining the canonical deadlock fixture is a named deliverable in the Sort Mechanic implementation story — it must be authored alongside the implementation, not assumed to exist.**

**AC-11 — Temp slot depth = 1: bolt in temp slot can be immediately re-lifted** *(BLOCKING)*
**GIVEN** a temp slot of depth 1 containing one bolt, **WHEN** the player taps it, **THEN** Sort Mechanic enters BOLT_SELECTED with the bolt held — no lock-in, no rejection. The bolt behaves identically to the top bolt of a color stack.

**AC-12 — Android back gesture cancels held bolt** *(BLOCKING — Android only)*
**GIVEN** the player is in BOLT_SELECTED state holding a bolt, **WHEN** `Keyboard.current` is non-null AND `Keyboard.current.escapeKey.wasPressedThisFrame` evaluates true in `SortMechanic.Update()` (per ADR-0007 — covers Android hardware back on all supported versions without requiring `android:enableOnBackInvokedCallback` in AndroidManifest), **THEN** `move_cancelled` is emitted, CANCELLATION state is entered, and the bolt returns to source. **Platform scope and null guard:** This AC is Android-only. On iOS, `Keyboard.current` is null (no hardware keyboard device) and this condition cannot be triggered. The null guard `if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)` is a **required implementation contract**, not optional defensive code — omitting it produces a NullReferenceException on iOS in BOLT_SELECTED state. The null guard must be enforced at code review. No separate `BackGestureHandler` MonoBehaviour required.

**AC-13 — `move_committed` emitted before animation plays** *(Integration test)*
**GIVEN** the player taps a valid destination in BOLT_SELECTED, **WHEN** Sort Mechanic transitions to MOVE_EXECUTING, **THEN** `move_committed(source, destination, color_id)` is emitted synchronously before the bolt movement animation begins. GSM board state is updated before the first animation frame renders.

**AC-14 — Temp slot count = 0: tap targeting non-existent slot index is treated as empty space**
**GIVEN** a level with `temp_slot_count = 0`, **WHEN** a tap event is injected targeting a temp slot index that does not exist in the board state (e.g., slot_index = 0 when `temp_slot_count = 0`), **THEN** if a bolt is currently held: Sort Mechanic treats the tap as empty board space and transitions to CANCELLATION, emitting `move_cancelled`. If no bolt is held: Sort Mechanic emits no signal and GSM state does not change.

**AC-15a — Committed move emits `move_committed`; cancellation does NOT** *(BLOCKING — unit test)*
**GIVEN** the player commits a legal move from stack A to stack B, **THEN** `move_committed(source=A, destination=B, color_id, sequence_id)` is emitted. **AND** if the player cancels a held bolt via S-03 or S-05, `move_cancelled` is emitted and `move_committed` is NOT — verifying Sort Mechanic does not accidentally emit a committal signal on cancellation.

**AC-15b — Undo restores board to pre-move state** *(Integration test — not unit tier)*
**GIVEN** `move_committed` was emitted for a move from stack A to stack B, **WHEN** the player issues an undo command, **THEN** GSM restores the board (bolt returns from B to A). Verified at integration tier with Sort Mechanic + GSM running together.

**AC-16 — Multi-rejection stability: state machine does not drift across sequential illegal taps** *(BLOCKING)*
**GIVEN** the following board layout: source stack = [1] (one bolt, color 1, lifted by player), dest_A = [2,2,2] (full — ILLEGAL via V-04), dest_B = [3,3,3] (full — ILLEGAL via V-04), dest_C = [2] (color mismatch — ILLEGAL via V-03), legal_dest = [] (empty — LEGAL), `stack_depth = 3`, **WHEN** the player sequentially taps dest_A, dest_B, dest_C (receiving three `move_rejected` events), and then taps legal_dest, **THEN** `move_committed(source, legal_dest, color_id=1)` is emitted on the fourth tap, Sort Mechanic transitions to MOVE_EXECUTING, and the bolt (color 1) is placed in legal_dest. Sort Mechanic must remain in BOLT_SELECTED through all three rejections without state drift or held-bolt loss.

**AC-17 — S-05 empty-space cancellation cancels held bolt**
**GIVEN** the player is in BOLT_SELECTED holding a bolt, **WHEN** a tap event is injected that does not correspond to any stack or temp slot index in the board state, **THEN** `move_cancelled` is emitted, Sort Mechanic transitions to CANCELLATION then IDLE, and the bolt returns to its source. Board state is identical to its pre-lift state. (The S-05 dead-zone spatial constraint is a UI/input-layer concern and is not verified at the Sort Mechanic unit tier.)

**AC-18a — Win condition fires correctly when invariant holds** *(BLOCKING)*
**GIVEN** a board where all color stacks satisfy `is_won = TRUE` (each full and monochromatic) and `len(color_stacks) == color_count` (invariant holds — temp slots are empty as guaranteed by `bolt_count_invariant`), **WHEN** the final bolt is placed and `animation_complete` is received, **THEN** `puzzle_solved` is emitted and Sort Mechanic transitions to WIN. No separate temp-slot emptiness check is required.

**AC-18b — Initialization assertion emits level_load_failed with logger call** *(BLOCKING)*
**GIVEN** a board where `len(color_stacks) ≠ color_count` (stacks array does not match declared color count), **WHEN** Sort Mechanic initializes, **THEN** `level_load_failed(reason: CORRUPTED_BOARD_STATE)` is emitted and `IDiagnosticLogger.Error` is called with category `SortMechanic` and structured payload `{reason: CORRUPTED_BOARD_STATE}`. Verified by injecting a mismatched board state and asserting the mock logger received this call (not the message text).

**AC-18c — All tap events are blocked after any initialization assertion failure** *(BLOCKING)*
**GIVEN** `level_load_failed(reason: CORRUPTED_BOARD_STATE)` has been emitted (from any initialization assertion failure — see AC-18b, AC-27, AC-26), **WHEN** any tap event is subsequently injected, **THEN** Sort Mechanic emits no event of any kind (`move_committed`, `move_cancelled`, `move_rejected`, `puzzle_solved` are all silent) and GSM board state does not change. Verified by confirming zero event emissions and zero GSM mutations after the failed initialization.

**AC-19 — GSM bolt-count invariant failure triggers level_load_failed** *(Integration test)*
**GIVEN** a level record where `total_bolts ≠ color_count × stack_depth` (one bolt missing from level data), **WHEN** GSM loads the level and validates the `bolt_count_invariant`, **THEN** GSM's invariant check fires before board initialization completes — GSM refuses to expose a valid board state — and Sort Mechanic, detecting the inconsistency through its own initialization assertions (assertion 1 fails: `len(color_stacks) ≠ color_count`, or board is otherwise malformed), emits `level_load_failed(reason: CORRUPTED_BOARD_STATE)`. The board is never made available for interaction. **Ownership note:** Sort Mechanic is the emitter of `level_load_failed` — it is not a signal GSM sends to Sort Mechanic. GSM's failure to expose a valid board is the trigger; Sort Mechanic detects and emits. Verified at integration tier with a real GSM instance loading a synthetic broken level fixture in `tests/integration/gsm-sort-mechanic/`.

**AC-20 — HUD held-bolt indicator and destination highlight** *(UI integration test — Sort Mechanic + HUD)*
**GIVEN** a board is loaded and the player lifts a bolt (BOLT_SELECTED), **WHEN** Sort Mechanic enters BOLT_SELECTED, **THEN** (a) the HUD renders the held bolt visually above its source stack, (b) all legal destinations are highlighted, and (c) on INVALID_MOVE → BOLT_SELECTED re-entry, highlighting re-activates immediately without requiring a new lift. Verified at UI integration tier with Sort Mechanic + HUD running together.

**AC-21 — CANCELLATION exits to IDLE synchronously without animation handshake** *(BLOCKING)*
**GIVEN** the player is in BOLT_SELECTED holding a bolt, **WHEN** the player taps the source stack (S-03) or a tap is injected with no matching stack/slot index (S-05), **THEN** Sort Mechanic emits `move_cancelled` and is in IDLE state within the same synchronous `Update()` call stack as `move_cancelled` emission — no `yield return`, no `Task.Delay`, no deferred invocation — without waiting for any `animation_complete` signal. Sort Mechanic accepts new tap input immediately in IDLE. Verified by calling the cancellation trigger method and asserting `_currentState == SortMechState.Idle` on the immediately following line without awaiting or yielding.

**AC-22 — `deadlock_detected()` emitted before buffered tap fires** *(BLOCKING)*
**GIVEN** the canonical deadlock fixture from `tests/helpers/sort-mechanic-fixtures` is loaded as board state, AND a tap was buffered during MOVE_EXECUTING for a bolt placement that creates the deadlocked state, **WHEN** `animation_complete` is received and Sort Mechanic exits MOVE_EXECUTING on the IDLE path, **THEN** the event emission order is: (1) win check fails → (2) `deadlock_detected()` emitted → (3) buffered tap fires. `deadlock_detected()` must not be deferred until after the buffered tap.

**AC-23 — Watchdog recovery runs win check before returning to IDLE** *(Integration test)*
**GIVEN** Sort Mechanic is in MOVE_EXECUTING and `board_refresh_forced(sequence_id)` is received (animation watchdog triggered), **WHEN** Sort Mechanic processes `board_refresh_forced`, **THEN** Sort Mechanic reads current board state from GSM and evaluates the win condition before transitioning. If `is_won` is TRUE, Sort Mechanic transitions to WIN and emits `puzzle_solved(move_count)`. If `is_won` is FALSE, Sort Mechanic transitions to IDLE. Sort Mechanic must never transition to IDLE on a won board. Verified at integration tier by simulating an animation crash on the final winning move and confirming `puzzle_solved` fires.

**AC-24 — Buffered tap discarded on WIN exit** *(BLOCKING)*
**GIVEN** Sort Mechanic is in MOVE_EXECUTING with exactly one tap buffered AND the committed move completes the win condition, **WHEN** `animation_complete(sequence_id)` is received and `is_won` evaluates to TRUE, **THEN** `puzzle_solved` is emitted, Sort Mechanic is in WIN state, and the buffered tap is silently discarded — no `move_committed`, `move_cancelled`, `move_rejected`, `move_executing_exited`, or any other Sort Mechanic event is emitted after `puzzle_solved`. Verified with an event-bus spy confirming zero emissions (including `move_executing_exited`) after `puzzle_solved`.

**AC-25 — `deadlock_detected()` emitted on `level_loaded` when initial board has no legal first move** *(Integration test)*
**GIVEN** a level is loaded where no legal first move exists from the initial board configuration (use the canonical deadlock fixture in `tests/helpers/sort-mechanic-fixtures` — same fixture used for AC-10), **WHEN** `level_loaded` fires and GSM board state is readable, **THEN** Sort Mechanic emits `deadlock_detected()` before any player input is accepted. No bolt is held at this point — the check evaluates the full initial board. Verified at integration tier by asserting `deadlock_detected()` fires during Sort Mechanic's initialization phase, before the first player-facing frame.

**AC-26 — Per-color bolt imbalance or undeclared color ID triggers level_load_failed** *(Integration test)*
**GIVEN** a level record where `total_bolts == color_count × stack_depth` (bolt-count invariant holds on total) but either (a) one color has more than `stack_depth` instances (over-representation), or (b) a `color_id` outside the domain `{1..color_count}` is present in the level data (phantom color that passes the total-count check but makes win structurally unreachable), **WHEN** GSM loads the level and validates per-color distribution, **THEN** GSM's per-color validation catches this and refuses to expose a valid board; Sort Mechanic, detecting the inconsistency through its initialization assertions, emits `level_load_failed(reason: CORRUPTED_BOARD_STATE)`. The board is never made available for interaction. GSM must validate: for each `color_id ∈ {1..color_count}`, `count(color_id) == stack_depth`; AND no `color_id` outside this domain is present in the level data. Verified at integration tier with a real GSM instance loading synthetic fixtures covering both failure modes in `tests/integration/gsm-sort-mechanic/`. **Note:** This AC places a combined requirement on the GSM GDD — GSM must validate both over-representation and presence of undeclared color IDs before board initialization completes.

**AC-27 — `temp_slot_depth > stack_depth` initialization assertion triggers level_load_failed** *(BLOCKING)*
**GIVEN** a board where `temp_slot_depth > stack_depth` (e.g., `temp_slot_depth = 5`, `stack_depth = 3`), **WHEN** Sort Mechanic initializes, **THEN** `level_load_failed(reason: CORRUPTED_BOARD_STATE)` is emitted, no tap events produce any Sort Mechanic action, and the error is logged via `IDiagnosticLogger.Error` with category `SortMechanic` and structured payload `{reason: CORRUPTED_BOARD_STATE}`. Verified by constructing a board with `temp_slot_depth > stack_depth` and confirming Sort Mechanic emits `level_load_failed` and refuses all subsequent input. (AC-18c verifies input blocking post-failure.)

**AC-28 — App pause during BOLT_SELECTED cancels held bolt before serialization** *(Integration test)*
**GIVEN** the player is in BOLT_SELECTED state holding a bolt lifted from stack index N, **WHEN** `OnApplicationPause(true)` fires, **THEN** Sort Mechanic transitions to CANCELLATION → IDLE synchronously within the `OnApplicationPause` handler, emitting `move_cancelled(source=N, color_id)`. The bolt is returned to source in board state before `OnApplicationPause` returns, ensuring GSM serializes a complete board with `total_bolts == color_count × stack_depth`. Verified at integration tier: inject `OnApplicationPause(true)` while in BOLT_SELECTED; assert board state has correct total bolt count; assert Sort Mechanic state is IDLE; use GSM call-order tracking to confirm `move_cancelled` emission precedes GSM's serialize call. A deserialized `total_bolts` of `(color_count × stack_depth) - 1` is a test failure.

**AC-29a — `move_executing_exited` NOT emitted on watchdog path** *(BLOCKING — unit test)*
**GIVEN** Sort Mechanic is in MOVE_EXECUTING, **WHEN** `board_refresh_forced(sequence_id)` is received matching the current in-flight sequence, **THEN** Sort Mechanic processes the watchdog exit sequence (runs win check, transitions to WIN or IDLE as appropriate) and `move_executing_exited` is NOT emitted on this path. Verified by event-bus spy confirming `move_executing_exited` emission count is zero after `board_refresh_forced` processing. (`puzzle_solved` on the WIN path, or zero additional Sort Mechanic events on the IDLE path, are the only valid signals.)

**AC-29b — Buffered tap targeting now-empty stack treated as S-02** *(BLOCKING — unit test)*
**GIVEN** Sort Mechanic commits the only bolt from stack A (emptying it — `stack_A.bolt_count` was 1, now 0), AND a tap targeting stack A index was buffered during MOVE_EXECUTING, **WHEN** MOVE_EXECUTING exits on the IDLE path and the buffered tap fires against empty stack A, **THEN** Sort Mechanic treats the tap as S-02 (empty source tap): Sort Mechanic remains in IDLE, no event is emitted (`move_committed`, `move_cancelled`, `move_rejected` are all silent), and GSM board state does not change. Verified with a `color_count = 2, stack_depth = 1` board where committing the only bolt in stack A empties it entirely.

**AC-30 — INVALID_MOVE one-tap buffer: valid destination fires on rejection exit** *(BLOCKING — unit test)*
**GIVEN** Sort Mechanic has entered INVALID_MOVE (player tapped a color-mismatch destination while holding bolt of color 1), AND during the rejection animation a tap arrives targeting a valid legal destination (empty stack), **WHEN** the rejection animation completes and Sort Mechanic exits to BOLT_SELECTED, **THEN** the buffered tap fires immediately as a destination evaluation: `move_committed` is emitted and Sort Mechanic transitions to MOVE_EXECUTING. The player's rapid valid-destination tap is not silently lost. Verified with a mock animation system that withholds `rejection_animation_complete` until explicitly triggered.

**AC-30b — INVALID_MOVE one-tap buffer: subsequent taps beyond first discarded** *(BLOCKING — unit test)*
**GIVEN** Sort Mechanic is in INVALID_MOVE with one tap already buffered, **WHEN** additional taps arrive during the same rejection animation window, **THEN** Sort Mechanic emits no event for the additional taps and they are discarded. Only the first buffered tap is fired on BOLT_SELECTED re-entry.

**AC-31 — Pre-won board auto-wins at level_loaded** *(Integration test)*
**GIVEN** a level where all color stacks satisfy `is_won = TRUE` at initial board load (every stack full and monochromatic — a Level Data authoring error), **WHEN** Sort Mechanic processes `level_loaded`, **THEN** `puzzle_solved(move_count: 0)` is emitted before any player input is accepted, Sort Mechanic transitions to WIN, and no `deadlock_detected()` is emitted. Verified at integration tier with a synthetic pre-won level fixture confirming the emission sequence and absence of the deadlock signal.

## Open Questions

| Question | Owner | Target Resolution | Resolution |
|---|---|---|---|
| What is the animation completion signal name and signature that Animation System emits to Sort Mechanic? (Required to exit MOVE_EXECUTING.) | Animation System GDD | Before Animation System GDD is authored | **Resolved: `animation_complete(sequence_id: int64)` — confirmed in Animation System GDD (SQ-01, AC-BM-02, Consumers table).** |
| What is the watchdog timeout duration (N seconds) for EC-08 — animation system crash recovery? | GSM GDD | Before GSM GDD is authored | **Resolved: 1500ms — confirmed in GSM GDD Tuning Knobs (`watchdog_timeout_ms = 1500ms`).** |
| Does GSM persist held state (BOLT_SELECTED) across app backgrounding, or does it drop to IDLE on foreground restore? | Game State Manager GDD | Before GSM GDD is authored | **Resolved (EC-14, Pass 7):** Held state is never persisted. Sort Mechanic cancels held bolt synchronously in `OnApplicationPause(true)` before GSM serializes. GSM always serializes a complete board. No held-state persistence mechanism is needed or designed. |
| Should `puzzle_solved` carry `move_count` as a parameter, or should move count be emitted in a separate event? Affects analytics and reward flow. | GSM GDD / Analytics System | Before GSM GDD is authored | **Resolved: `puzzle_solved(move_count: int)`. GSM reads from undo history length.** |
| Does multi-bolt lifting become necessary at `stack_depth = 7–8` to prevent tedium? Decision: single-bolt only for MVP. Re-evaluate after playtesting at max stack depth. | Playtesting | After first vertical slice playtest | Open |
| Should the shallow deadlock check (depth 1) be upgraded to depth 2–3 for a more proactive hint pulse? Current: depth 1 triggers on "no legal first move." Deeper check catches near-deadlocks one move earlier. | Lead Programmer + Hint System GDD | Before Hint System GDD is authored | Open |
| Android back gesture: is the target input the hardware back button (Android 9 and earlier virtual button), the predictive back gesture (Android 13+), or both? On Android 10+, swipe-from-edge is OS-intercepted before app receives it — verify which mechanism is reachable and document platform-specific handling. | Platform / Android specialist | Before first Android build | **Resolved (ADR-0007, Accepted):** Use `Keyboard.current.escapeKey.wasPressedThisFrame` in `SortMechanic.Update()`. Covers Android hardware back button on all supported versions. Works on Android 13+ provided the project does NOT opt into `android:enableOnBackInvokedCallback` in AndroidManifest. If that flag is ever added, migrate to `Application.onBackReceived` delegate per ADR-0007. No separate `BackGestureHandler` MonoBehaviour required. |
