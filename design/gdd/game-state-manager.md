# Game State Manager

> **Status**: In Design
> **Author**: Design session + systems-designer agent
> **Last Updated**: 2026-04-17
> **Implements Pillar**: Flow Over Friction, Respect the Session, The Machine Must Sing

## Overview

The Game State Manager is BoltSort's authoritative board state layer — the single source of truth for the current puzzle at all times. It owns four concerns: the live board state (the ordered contents of every color stack and temp slot), the move history stack that powers undo, the win detection trigger (evaluating the win condition after each `move_committed` and transitioning to a complete state when it passes), and the session lifecycle (loading a level record from the Level Data System, verifying the bolt count invariant, instantiating board state, and tearing down cleanly on level exit). All systems that need to know what is on the board — Sort Mechanic, Animation System, HUD, Level Progression — read from the Game State Manager. No system writes board state directly; all mutations flow through the events that Sort Mechanic emits (`move_committed`, `move_cancelled`, `move_rejected`, `puzzle_solved`), which GSM processes synchronously before any animation plays. The Game State Manager has no visual or audio output of its own — it is infrastructure. Its quality is measured by its consistency: every system that reads from it must see the same board, and the board must always reflect the last committed move, nothing more and nothing less.

## Player Fantasy

The board you see is the board that exists. When you slide a bolt, it goes where you placed it. When you tap undo, the world reverses to exactly the state you remember — not approximately, not mostly, but exactly. There is no lag between intention and confirmation, no flicker of doubt about whether the move registered, no quiet drift between what the screen shows and what the puzzle believes. The machine is honest with you, and that honesty is what lets your hands relax into the work. And the session itself is one continuous gesture: a puzzle ends, the next materializes, and your place is never lost.

This system has no player fantasy of its own. Its success is measured by its invisibility — when the Game State Manager is correct, players feel only the puzzle. When it is wrong, every other system in the game breaks with it.

## Detailed Rules

### Core Rules

**Group A — Board State Mutation**

The GSM is the sole owner of all board state mutation. No system writes to `stack_contents` or `temp_slot_contents` directly. All mutations are triggered exclusively by events from the Sort Mechanic.

**Index convention (used everywhere):** Color stacks occupy indices 0 through `color_count - 1`. Temp slots occupy indices `color_count` through `color_count + temp_slot_count - 1`. This flat namespace is consistent across undo entries, the Sort Mechanic read interface, and all events.

| Rule | Condition | Outcome |
|---|---|---|
| BSM-01 | `move_committed(source, destination, color_id, sequence_id)` received | Remove top element from source array. Append `color_id` to destination array. Push undo entry (BSM-05). Increment `current_sequence_id`. Increment `move_count`. All steps execute synchronously in this order before any callback returns. |
| BSM-02 | `move_cancelled(source, color_id)` received | No mutation. The bolt was never removed from GSM state — held state is Sort Mechanic-local. |
| BSM-03 | `move_rejected(source, destination, color_id, reason)` received | No mutation. |
| BSM-04 | `puzzle_solved()` received | Transition to COMPLETE state (see WIN-01). |
| BSM-05 | Undo entry pushed | Entry: `{ source_index, destination_index, color_id, sequence_id }`. Written on every BSM-01. Never written for cancelled or rejected moves. |
| BSM-06 | Board state is updated before animation | Board is mutated synchronously on `move_committed`. No "pre-commit" state is observable by any downstream system after the event fires. |

**Note on held bolt state:** The held bolt (one `color_id` and its source reference) is owned entirely by the Sort Mechanic's `BOLT_SELECTED` state. GSM does not model held state at any time.

---

**Group B — Undo**

| Rule | Condition | Outcome |
|---|---|---|
| UND-01 | Undo requested while undo stack is non-empty | Pop the top entry. Remove top element from the destination array. Append `color_id` to the source array. Increment `current_sequence_id`. Decrement `move_count`. Emit `board_state_changed(sequence_id, move_count)`. |
| UND-02 | Undo requested while undo stack is empty | No mutation, no event. HUD is responsible for disabling the undo button in this state. |
| UND-03 | Undo requested while Sort Mechanic is in MOVE_EXECUTING | HUD must disable the undo button during MOVE_EXECUTING. If undo fires anyway (input race): defer the request — store exactly one pending undo. Process it immediately on receipt of `move_executing_exited(sequence_id)` from Sort Mechanic (normal animation path) or on GSM's own `board_refresh_forced` emit (watchdog path). Do not silently drop it. |
| UND-04 | Undo depth | Unlimited. Every `move_committed` entry is stored. Memory ceiling is a Tuning Knob. |
| UND-05 | Undo after COMPLETE state is entered | Not accepted. The undo stack is frozen the moment `puzzle_solved()` is received. |
| UND-06 | Sequence ID on undo | Every undo that mutates board state increments `current_sequence_id`. Any animation completion signal in flight from the undone move carries a stale `sequence_id`. Sort Mechanic discards completion signals whose ID does not match its tracked live ID. No separate event is needed — stale ID is the signal. |

---

**Group C — Win Detection**

Win condition evaluation is **owned entirely by the Sort Mechanic** (rules W-01 through W-04 of `design/gdd/sort-mechanic.md`). GSM does not independently run a win check. GSM receives the outcome.

| Rule | Condition | Outcome |
|---|---|---|
| WIN-01 | `puzzle_solved()` received from Sort Mechanic | Transition to COMPLETE state. Read `par_moves` from Level Data System (O(1) dict lookup via `level_id`). Emit `level_complete(level_id, move_count, par_moves, sequence_id)`. `current_sequence_id` is NOT incremented — the `sequence_id` in the payload equals the current value at transition time. Freeze undo stack. Stop accepting `move_committed` events. *(Payload updated by ADR-0012: `par_moves` added so InGameHUD and LevelCompleteUI do not query LDS independently.)* |
| WIN-02 | `puzzle_solved()` received while undo is deferred (UND-03) | Deferred undo is discarded. COMPLETE state does not process undo. |

---

**Group D — Session Lifecycle**

The lifecycle follows a strict linear sequence. No step may be skipped or reordered.

| Step | Action | Failure behavior |
|---|---|---|
| L-01 | **Boot guard**: Call System Readiness Query on Level Data System. | If `ready = false`: retry after fixed backoff. Emit `session_load_failed(LEVEL_DATA_UNAVAILABLE)`. |
| L-02 | **Fetch record**: Call Get Level by ID with target `level_id`. | If error (NOT_FOUND / VALIDATION_FAILED / SYSTEM_NOT_READY): emit `session_load_failed(LEVEL_RECORD_ERROR, error_code)`. Do not proceed. |
| L-03 | **Verify invariant**: Run both bolt count invariant checks against `color_stacks`, `color_count`, `stack_depth`. Check 1: total bolt count equals `color_count × stack_depth`. Check 2: each `color_id` appears exactly `stack_depth` times. | If either check fails: emit `session_load_failed(INVARIANT_VIOLATION, level_id)`. Log which check failed. |
| L-04 | **Detect pre-won board**: Run win condition check against initial `color_stacks` (W-02 definition from Sort Mechanic GDD). | If board is already won: log warning "pre-won board: level_id={X}". Do NOT auto-win. Continue to L-05. |
| L-05 | **Instantiate board state**: Copy `color_stacks` into `stack_contents[]`. Initialize `temp_slot_contents[]` as `temp_slot_count` empty arrays of capacity `temp_slot_depth`. If `temp_slot_count = 0`: allocate zero temp slot arrays — no default slots. Set `stack_depth`, `temp_slot_depth`, `temp_slot_count`, `color_count` from record. Initialize `current_sequence_id = 0`, undo stack empty, `move_count = 0`. | If allocation fails: emit `session_load_failed(INSTANTIATION_ERROR)`. |
| L-06 | **Emit level loaded**: Emit `level_loaded(level_id, color_count, stack_depth, temp_slot_count, temp_slot_depth, sequence_id=0)`. | — |
| L-07 | **Transition to ACTIVE**: Enter ACTIVE state. Sort Mechanic and other systems may now read board state and process input. | — |
| L-08 | **Teardown on level exit**: Clear `stack_contents`, `temp_slot_contents`, undo stack, `move_count`. Emit `level_unloaded(level_id)`. Transition to UNLOADED. | If teardown triggered during LOADING: cancel load, discard partial state, emit `level_unloaded(null)`, go to UNLOADED directly. |

**Watchdog rules:**

| Rule | Condition | Outcome |
|---|---|---|
| WDG-01 | `move_committed` fires and no animation completion signal arrives within `watchdog_timeout_ms` | Increment `current_sequence_id`. Emit `board_refresh_forced(sequence_id)`. Sort Mechanic and Animation System receive this signal and exit MOVE_EXECUTING → IDLE. Board reflects the committed state — no rollback. |
| WDG-02 | Animation completion arrives after `board_refresh_forced` was emitted | Signal carries a stale `sequence_id`. Sort Mechanic discards it. No double-transition. |
| WDG-03 | Watchdog timer lifecycle | Timer starts on `move_committed`. Cancelled (StopCoroutine) when a valid animation completion signal arrives. Not running in IDLE, BOLT_SELECTED, CANCELLATION, INVALID_MOVE, or WIN states. |

---

**Group E — Serialization / App Lifecycle**

| Rule | Condition | Outcome |
|---|---|---|
| SER-01 | App backgrounded while GSM is in ACTIVE or COMPLETE state | Serialize board state to Save & Persistence. Fields serialized: `stack_contents[]`, `temp_slot_contents[]`, `stack_depth`, `temp_slot_depth`, `temp_slot_count`, `color_count`, `move_count`, `current_sequence_id`, `level_id`, `gsm_state`. Undo stack is NOT serialized — it is session-only. |
| SER-02 | Foreground restore; saved state exists and is valid | Deserialize. If `gsm_state = ACTIVE`: restore board state; increment `current_sequence_id` if Sort Mechanic was in BOLT_SELECTED at background time (per EC-06); emit `board_state_changed(sequence_id, move_count)`. If `gsm_state = COMPLETE`: restore board state as frozen; re-enter COMPLETE; do NOT re-emit `level_complete` — Level Progression received it before backgrounding and owns the resume flow. |
| SER-03 | Foreground restore; saved state is missing or deserialization fails (corrupt data, schema mismatch) | Emit `session_load_failed(SAVE_CORRUPT, level_id)` using the `level_id` from partial deserialization if available, otherwise null. Clear all state. Transition to UNLOADED. Level Progression owns recovery (retry fresh load, skip level, or show error UI). |

---

### States and Transitions

```
UNLOADED → LOADING → ACTIVE → COMPLETE → TEARDOWN → UNLOADED
               ↓
         LOAD_FAILED → (auto) → UNLOADED
```

| State | Entry | Exit | GSM behavior |
|---|---|---|---|
| `UNLOADED` | App launch; TEARDOWN complete | → LOADING on `load_level(level_id)` | No board state. All read fields uninitialized. Sort Mechanic must not be active. |
| `LOADING` | `load_level(level_id)` received | → ACTIVE (L-01–L-07 succeed); → LOAD_FAILED (any step fails); → UNLOADED (cancelled via `exit_level`) | Runs L-01–L-07 in sequence. All read requests return NOT_READY. |
| `ACTIVE` | L-07 emits `level_loaded` | → COMPLETE (`puzzle_solved()` received); → TEARDOWN (`exit_level` received) | Processes all Sort Mechanic events. Updates board state. Manages undo stack. Maintains watchdog timer. |
| `COMPLETE` | `puzzle_solved()` received | → TEARDOWN (`exit_level` received from Level Progression) | Board state readable but frozen. Undo stack frozen. `move_count` is final and queryable. Ignores all `move_committed` and `puzzle_solved()` events. |
| `LOAD_FAILED` | Any failure in L-01–L-05 | → UNLOADED (auto, immediate) | Emits `session_load_failed`. Clears partial state. Transient — not an observable steady state. |
| `TEARDOWN` | `exit_level` received in ACTIVE or COMPLETE | → UNLOADED (L-08 complete) | Executes L-08. Clears all state. Emits `level_unloaded`. Non-interruptible once begun. |

**Full transition table:**

| From | Trigger | To | GSM action |
|---|---|---|---|
| UNLOADED | `load_level(level_id)` | LOADING | Begin L-01 |
| LOADING | L-01–L-07 succeed | ACTIVE | Emit `level_loaded` |
| LOADING | Any step L-01–L-05 fails | LOAD_FAILED → UNLOADED | Emit `session_load_failed` |
| LOADING | `exit_level` (cancellation) | TEARDOWN → UNLOADED | Discard partial state; emit `level_unloaded(null)` |
| ACTIVE | `puzzle_solved()` | COMPLETE | Emit `level_complete` |
| ACTIVE | `exit_level` (abandon) | TEARDOWN | Execute L-08 |
| ACTIVE | App backgrounded | ACTIVE (preserved) | Serialize board state (SER-01). Increment `current_sequence_id` if Sort Mechanic was in BOLT_SELECTED. Emit `board_state_changed` on foreground restore (SER-02). If deserialization fails, execute SER-03. |
| COMPLETE | App backgrounded | COMPLETE (preserved) | Serialize board state (SER-01). On foreground restore, remain in COMPLETE — do NOT re-emit `level_complete`. If deserialization fails, execute SER-03. |
| COMPLETE | `exit_level` (progression) | TEARDOWN | Execute L-08 |
| TEARDOWN | L-08 complete | UNLOADED | — |

**App backgrounding — held bolt rule:** On foreground restore, GSM increments `current_sequence_id` if Sort Mechanic was in `BOLT_SELECTED` at background time. Sort Mechanic drops to IDLE because the live sequence ID it held no longer matches. The held bolt is implicitly cancelled. Board state is consistent — the bolt was never committed, so board reflects the pre-lift state.

---

### Interactions with Other Systems

**Level Data System (upstream)**

| Call | When | Parameters | Expected response |
|---|---|---|---|
| System Readiness Query | L-01 | none | `{ ready, loaded_count, skipped_count, catalogue_version }` |
| Get Level by ID | L-02 | `level_id: int` | Full Level Record or error `{ error_code, level_id, diagnostic }` |

Fields read: `color_stacks`, `stack_depth`, `temp_slot_count`, `temp_slot_depth`, `color_count`, `level_id`. Fields NOT read: `display_name`, `difficulty_tier`, `is_tutorial`, `daily_challenge_eligible`, `hint_override`, `added_version`.

Any non-success response causes GSM to emit `session_load_failed` — GSM does not implement its own level fallback.

---

**Sort Mechanic (bidirectional)**

*Sort Mechanic reads from GSM (synchronous pull, ACTIVE state only):*

| Field | Type | Range | Description |
|---|---|---|---|
| `stack_contents[index]` | `array<color_id>` | length 0–`stack_depth` | Ordered contents of color stack. Top = last element. |
| `stack_depth` | int | 3–8 | Uniform capacity of all color stacks. |
| `temp_slot_contents[index]` | `array<color_id>` | length 0–`temp_slot_depth` | Ordered contents of temp slot (offset by `color_count` in flat namespace). |
| `temp_slot_depth` | int | 1–`stack_depth` | Uniform capacity of all temp slots. |
| `temp_slot_count` | int | 0–3 | Number of valid temp slot indices. If 0: no arrays exist. |
| `color_count` | int | 2–8 | Number of color stacks; valid index range [0, color_count). |

Reading these fields outside of ACTIVE state is a contract violation.

*Sort Mechanic emits (GSM subscribes):*

| Event | Parameters | GSM action |
|---|---|---|
| `move_committed(source, destination, color_id, sequence_id)` | Flat indices; `sequence_id` matches `current_sequence_id` at emit time | Execute BSM-01. Reset watchdog. |
| `move_cancelled(source, color_id)` | — | No mutation (BSM-02). |
| `move_rejected(source, destination, color_id, reason)` | — | No mutation (BSM-03). |
| `puzzle_solved()` | — | Execute WIN-01. |
| `move_executing_exited(sequence_id)` | `sequence_id` matches the committed move's sequence | Flush deferred undo queue if non-empty: execute UND-01 and emit `board_state_changed`. No-op if deferred queue is empty. Only emitted on MOVE_EXECUTING → IDLE (non-win path). |

> **Sort Mechanic GDD update required:** The Sort Mechanic GDD must be updated to document `move_executing_exited(sequence_id)` emission after MOVE_EXECUTING transitions to IDLE without a win. This is a new event contract item added in this revision.

*GSM emits (Sort Mechanic subscribes):*

| Event | Parameters | Sort Mechanic action |
|---|---|---|
| `board_refresh_forced(sequence_id)` | Updated `current_sequence_id` | Exit MOVE_EXECUTING → IDLE. |
| `level_loaded(...)` | All board parameters | Reset to IDLE; re-read all board fields. |

**Sequence ID contract:** `current_sequence_id` is a monotonically increasing **int64** starting at 0 at level load. It increments on every `move_committed` processed, every undo, and every watchdog refresh — never decrements. The "current live sequence ID" that Sort Mechanic holds to validate animation completion signals lives in Sort Mechanic (it owns MOVE_EXECUTING). GSM generates IDs and embeds them in events; Sort Mechanic validates completion signals against them.

---

**Animation System (downstream)**

*GSM emits (Animation System subscribes):*

| Event | Animation System use |
|---|---|
| `level_loaded(...)` | Instantiate visual stack/slot layout from board parameters. |
| `board_state_changed(sequence_id, move_count)` | Snap bolt visuals to match current `stack_contents`. State jump — no travel animation. |
| `board_refresh_forced(sequence_id)` | Abandon in-progress animation. Snap visuals to current board state. |
| `level_unloaded(level_id)` | Destroy all bolt and stack visual nodes. |

The Animation System emits `animation_complete(sequence_id: int64)` to Sort Mechanic (and HUD) on bolt settle completion. Sort Mechanic exits MOVE_EXECUTING on receipt; stale IDs are discarded via sequence_id mismatch check.

---

**In-Game HUD (downstream)**

*HUD reads from GSM:* `move_count` (move counter display), undo stack depth/count only (undo button enabled/disabled state). HUD does not read `stack_contents` or `temp_slot_contents`.

*GSM emits (HUD subscribes):*

| Event | HUD action |
|---|---|
| `level_loaded(...)` | Reset move counter. Set undo button to disabled (stack always empty at load). |
| `board_state_changed(sequence_id, move_count)` | Update move counter. Update undo button state. |
| `level_complete(level_id, move_count, sequence_id)` | Show Level Complete UI. Display final `move_count`. Disable undo button. |
| `session_load_failed(reason, error_code, level_id)` | Show error state UI. |

*HUD emits (GSM subscribes):* `undo_requested` → process per UND-01–UND-05. If deferred (UND-03): queue until Sort Mechanic exits MOVE_EXECUTING.

---

**Level Progression (lifecycle orchestrator)**

*Level Progression emits (GSM subscribes):*

| Event | GSM action |
|---|---|
| `load_level(level_id)` | If UNLOADED: begin L-01. If not UNLOADED: reject — caller must not issue `load_level` while a level is active. |
| `exit_level` | If ACTIVE or COMPLETE: begin TEARDOWN. If LOADING: cancel load → UNLOADED. |

*GSM emits (Level Progression subscribes):*

| Event | Parameters | Level Progression use |
|---|---|---|
| `level_loaded(level_id, color_count, stack_depth, temp_slot_count, temp_slot_depth, sequence_id)` | All board config | Confirms load succeeded. |
| `level_complete(level_id, move_count, sequence_id)` | `level_id`, `move_count`, `sequence_id` | Triggers reward flow, records completion, queues next level. |
| `level_unloaded(level_id)` | `level_id` (nullable) | Confirms teardown. May now issue next `load_level`. |
| `session_load_failed(reason, error_code, level_id)` | reason, error_code, `level_id` | Owns fallback (retry, skip, error UI). |

---

**GSM-Emitted Events — Complete Reference**

| Event | Parameters | Subscribers | When emitted |
|---|---|---|---|
| `level_loaded` | `level_id`, `color_count`, `stack_depth`, `temp_slot_count`, `temp_slot_depth`, `sequence_id` | Sort Mechanic, Animation System, HUD, Level Progression, Tutorial System | L-06 |
| `level_complete` | `level_id`, `move_count`, `sequence_id` | HUD, Level Progression, Analytics System | On WIN-01 |
| `level_unloaded` | `level_id` (nullable) | Level Progression, Animation System | L-08; or cancelled LOADING |
| `session_load_failed` | `reason`, `error_code`, `level_id` | Level Progression, HUD | Any failure in L-01–L-05; deserialization failure on foreground restore (SER-03). Valid `reason` codes: `LEVEL_DATA_UNAVAILABLE`, `LEVEL_RECORD_ERROR`, `INVARIANT_VIOLATION`, `INSTANTIATION_ERROR`, `SAVE_CORRUPT`. |
| `board_state_changed` | `sequence_id`, `move_count` | HUD, Animation System | After any undo; foreground restore |
| `board_refresh_forced` | `sequence_id` | Sort Mechanic, Animation System | Watchdog fires (WDG-01) |

## Formulas

### Move Count Formula

The move count formula is defined as:

`move_count' = clamp(move_count + delta_move, 0, MAX_INT)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Current move counter | `move_count` | int | 0–MAX_INT | Counter value before this event. Always non-negative. |
| Mutation delta | `delta_move` | int | {−1, 0, +1} | +1 on `move_committed`. −1 on undo (UND-01, stack non-empty). 0 on all other events (`move_cancelled`, `move_rejected`, watchdog fire, foreground restore). |
| Updated move counter | `move_count'` | int | 0–MAX_INT | Counter value after this event. The output. |

**Output range:** Non-negative integer. Bounded below at 0 by the undo gate (undo is only processed when the stack is non-empty, so `move_count` can never go below 0). Unbounded above in practice; no level will produce a `move_count` near MAX_INT under normal play.

**Freeze condition:** `move_count` becomes read-only when GSM enters COMPLETE state (WIN-01). It is the final value emitted in `level_complete(level_id, move_count, sequence_id)` and consumed by Level Progression for star rating calculation. Coin Economy receives `move_count` data from Level Progression (not as a direct GSM subscriber — see Open Questions).

**Example:**

| Event | `delta_move` | `move_count'` |
|---|---|---|
| Level load | — | 0 (initial) |
| `move_committed` | +1 | 1 |
| `move_committed` | +1 | 2 |
| Undo (UND-01) | −1 | 1 |
| `move_rejected` | 0 | 1 |
| `puzzle_solved()` → freeze | — | 1 (final, emitted in `level_complete`) |

---

### Contract Invariant: Sequence ID Monotonicity

`current_sequence_id` is not a formula — it is an increment-only counter. Its monotonicity property is load-bearing for the stale animation completion discard mechanism and must be stated precisely:

> `current_sequence_id` is strictly monotonically increasing within a session. It initializes to 0 at level load (L-05). It increments by 1 on every board state mutation: `move_committed` (BSM-01), undo (UND-01), and watchdog fire (WDG-01). It also increments on app foreground restore if Sort Mechanic was in BOLT_SELECTED. It never decrements. It never resets within a session. No upper bound is enforced.

Any system that receives `current_sequence_id` in an event can rely on this monotonicity to detect stale signals: if the ID it holds is less than the current `current_sequence_id`, the signal is stale and must be discarded.

---

### Referenced Formula: Bolt Count Invariant

The bolt count invariant is defined and owned by the Level Data System GDD (`design/gdd/level-data-system.md`). GSM applies both checks (Check 1: total bolt count; Check 2: per-color count distribution) during lifecycle step L-03. The formula, variable definitions, output range, and examples are not reproduced here — see the registry entry `bolt_count_invariant` in `design/registry/entities.yaml` and the Level Data System GDD Formulas section.

## Edge Cases

**EC-01 — Pre-won board at initialization:** If the initial `color_stacks` from the level record already satisfy the win condition (all stacks monochromatic and full at level start), GSM logs a warning ("pre-won board: level_id={X}") and continues to L-05 normally. GSM does NOT auto-win. Win detection is owned by Sort Mechanic and runs only on `move_committed` animation completion — no move has been committed at initialization, so win cannot fire. This is a designer authoring error caught by the Level Data System at authoring time (EC-09 of Level Data System GDD); at runtime it is a valid (if trivial) board state.

**EC-02 — `exit_level` received during LOADING:** If `exit_level` arrives while GSM is executing L-01 through L-07, GSM cancels the load, discards any partial board state, emits `level_unloaded(null)`, and transitions directly to UNLOADED. LOAD_FAILED is not emitted — this is a requested cancellation, not a failure. Level Progression may now issue the next `load_level`.

**EC-03 — Undo requested during MOVE_EXECUTING:** The Sort Mechanic's MOVE_EXECUTING state is a window during which the HUD undo button must be disabled. If an undo request arrives despite this (input race): GSM defers it — stores exactly one pending undo request. The deferred undo fires after MOVE_EXECUTING exits (see EC-10 for ordering). If a second undo tap arrives while a request is already deferred, it is silently dropped (queue capacity = 1, per EC-17).

**EC-04 — Undo requested while undo stack is empty:** No mutation occurs. No event is emitted. The HUD is responsible for disabling the undo button when the stack is empty — arriving here despite that is not an error, just a no-op.

**EC-05 — `puzzle_solved()` received while an undo is in the deferred queue:** GSM discards the deferred undo. WIN-01 fires — GSM transitions to COMPLETE. The undo stack is frozen. The player cannot undo from COMPLETE state. The deferred undo is not processed even when MOVE_EXECUTING exits, because GSM is no longer in ACTIVE state.

**EC-06 — App backgrounded while Sort Mechanic is in BOLT_SELECTED:** On foreground restore, GSM increments `current_sequence_id` to invalidate any signals associated with the pre-background held state. Sort Mechanic drops to IDLE because the live sequence ID it held no longer matches. The held bolt is implicitly cancelled — it was never committed to GSM state (the bolt exists only in Sort Mechanic's BOLT_SELECTED memory, not in `stack_contents`), so board state is consistent with the pre-lift position.

**EC-07 — Watchdog fires (animation completion signal never arrives):** GSM increments `current_sequence_id` and emits `board_refresh_forced(sequence_id)`. Sort Mechanic exits MOVE_EXECUTING → IDLE. Animation System snaps visuals to match current board state. Board reflects the committed move — no rollback. If a deferred undo was in the queue (EC-03 scenario), it fires after the watchdog-induced MOVE_EXECUTING exit (see EC-11).

**EC-08 — `temp_slot_count = 0` at level initialization:** GSM allocates zero temp slot arrays. `temp_slot_contents` is an empty collection. Any read of `temp_slot_contents[index]` by Sort Mechanic when `temp_slot_count = 0` is a contract violation — Sort Mechanic must validate `temp_slot_count > 0` before indexing. The Animation System must render no temp slot nodes. The HUD must not display any temp slot targets.

**EC-09 — `load_level` received while GSM is in TEARDOWN:** Level Progression may issue `load_level` immediately on receiving `level_complete`, before GSM has finished teardown. GSM rejects `load_level` in any state other than UNLOADED. Level Progression must wait for `level_unloaded` before issuing the next `load_level`. The Level Progression GDD must document this obligation explicitly. If the violation occurs despite this contract, GSM emits no `level_loaded` — Level Progression receives no confirmation and must implement a guard against this race.

**EC-10 — Undo of the final winning move (deferred queue ordering):** If a player taps undo during MOVE_EXECUTING while Sort Mechanic is animating the last move needed to win the puzzle, the undo is deferred per EC-03. When MOVE_EXECUTING exits (animation completion signal arrives), the deferred undo fires first — before Sort Mechanic evaluates the win condition. The board is reverted to the pre-final-move state. Sort Mechanic then evaluates win condition against the reverted board, which does not satisfy it. Sort Mechanic transitions to IDLE, not WIN. `puzzle_solved()` is never emitted. The player can continue playing. This is the intentional behavior — unlimited undo includes the winning move.

**EC-11 — Watchdog fires with a deferred undo in the queue:** If a deferred undo (EC-03) is pending when the watchdog fires (EC-07), the deferred undo is processed after the watchdog-induced MOVE_EXECUTING exit, consistent with EC-10's ordering rule (deferred undo fires before any win evaluation). The board reflects the committed state (watchdog never rolls back) — the undo then reverses that committed move. This is valid. UND-03's "do not silently drop" guarantee applies to watchdog-induced exits, not only to normal animation completion exits.

**EC-12 — `sequence_id` integer type:** `current_sequence_id` must be typed as at least int32 (signed, 32-bit). Overflow at 2,147,483,647 would cause stale signals to be misidentified as live. In practice, `current_sequence_id` resets to 0 at each level load (L-05), making overflow unreachable in any realistic session. The danger is defensive: an implementor typing this as int16 would overflow at 32,767 mutations — reachable in a long undo-heavy session. Specify int32 minimum; int64 is also acceptable.

**EC-13 — `exit_level` received in COMPLETE before Level Progression has consumed `level_complete`:** If teardown begins before Level Progression processes the `level_complete` event, GSM clears `move_count` during L-08. Level Progression must read `move_count` from the `level_complete` event payload — not from a subsequent GSM state query. Reading GSM state after `level_unloaded` is emitted returns cleared (invalid) values. The Level Progression GDD must document that all values needed from a completed level are read from event payloads, not from GSM state.

**EC-14 — Retry `load_level` after `session_load_failed`:** If GSM emits `session_load_failed` (e.g., `LEVEL_DATA_UNAVAILABLE`) and Level Progression retries with the same `level_id`, GSM must re-run L-01 (System Readiness Query) fresh on each load attempt. The `ready = false` result from the prior attempt must not be cached. Level Progression must implement a retry backoff — spamming `load_level` before the Level Data System is ready will generate repeated `session_load_failed` events and achieve nothing.

**EC-15 — `move_committed` received while GSM is in COMPLETE:** GSM ignores it. No board state change, no event. This case is provably unreachable in the current design: Sort Mechanic enters WIN state before emitting `puzzle_solved()`, and WIN state discards all input (W-04) — no `move_committed` can be emitted after Sort Mechanic is in WIN. If the Animation System ever introduces batched event delivery that could reorder events, this assumption must be re-examined. The Animation System GDD must guarantee FIFO delivery for the `move_committed` → animation completion → `puzzle_solved()` chain within a single move.

**EC-16 — App backgrounded during LOADING (async fetch in flight):** If the app is suspended mid-L-02 (Level Data System fetch), GSM must restart the load sequence from L-01 on foreground restore — not continue from the stale mid-load position. The Level Data System's readiness state may have changed during suspension. Re-running L-01 is safe and conservative; continuing from a stale async result is not.

**EC-17 — Second undo request arrives during MOVE_EXECUTING while a deferred undo is already queued:** The second request is silently dropped. The deferred queue capacity is 1. The player must tap undo again after the animation ends to trigger a second undo. This is acceptable: the HUD undo button appears disabled during MOVE_EXECUTING; the silently dropped request corresponds to a tap on a visually-disabled button, which is a non-event from the player's perspective.

**EC-18 — Foreground restore in COMPLETE state:** If the app is backgrounded and restored while GSM is in COMPLETE state (after `puzzle_solved()` but before Level Progression has issued `exit_level`), GSM remains in COMPLETE on restore (SER-02). Board state is readable but frozen. `level_complete` is NOT re-emitted — Level Progression received it before backgrounding and is responsible for resuming the reward flow. If deserialization fails from COMPLETE state (see EC-19): GSM transitions to UNLOADED; Level Progression must handle recovery from a complete-but-unrestored session.

**EC-19 — Deserialization failure on foreground restore:** If the serialized state from Save & Persistence is corrupt, missing, or does not match the current schema version, GSM executes SER-03: clears all partial state, emits `session_load_failed(SAVE_CORRUPT, level_id)` (using the `level_id` from partial deserialization if recoverable, otherwise null), and transitions to UNLOADED. Level Progression must listen for this event and implement a recovery path (offer to restart the level from scratch, skip to the next, or show an error). The `SAVE_CORRUPT` reason code is distinct from `LEVEL_DATA_UNAVAILABLE`, `LEVEL_RECORD_ERROR`, `INVARIANT_VIOLATION`, and `INSTANTIATION_ERROR` — it indicates a persistence layer failure, not a level data failure.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| Level Data System | Upstream — GSM depends on it | Data dependency. GSM requests the level record at load time. Read-only. | Calls: System Readiness Query (L-01), Get Level by ID (L-02). Reads: `color_stacks`, `stack_depth`, `temp_slot_count`, `temp_slot_depth`, `color_count`, `level_id`. |
| Sort Mechanic | Bidirectional — GSM is upstream for data; Sort Mechanic is the sole source of board mutations | Read + event. Sort Mechanic reads board state synchronously; emits all mutation events to GSM. | Exposes: `stack_contents`, `stack_depth`, `temp_slot_contents`, `temp_slot_depth`, `temp_slot_count`, `color_count`. Subscribes to: `move_committed`, `move_cancelled`, `move_rejected`, `puzzle_solved`. Emits to Sort Mechanic: `level_loaded`, `board_refresh_forced`. |
| Animation System | Downstream — depends on GSM | Event subscription. Animation System subscribes to GSM events and reads board state to drive visuals. | Emits to Animation System: `level_loaded`, `board_state_changed`, `board_refresh_forced`, `level_unloaded`. Animation System emits `animation_complete(sequence_id: int64)` — received by Sort Mechanic and HUD. |
| In-Game HUD | Downstream — depends on GSM | Read + event. HUD reads `move_count` and undo stack depth; subscribes to GSM events for display updates; emits `undo_requested`. | Exposes: `move_count`, undo stack depth (count only). Subscribes to HUD: `undo_requested`. Emits to HUD: `level_loaded`, `board_state_changed`, `level_complete`, `session_load_failed`. |
| Level Progression | Lifecycle orchestrator — sends load/exit commands | Command + event. Level Progression controls GSM lifecycle; GSM responds with lifecycle events. | Subscribes to Level Progression: `load_level(level_id)`, `exit_level`. Emits to Level Progression: `level_loaded`, `level_complete`, `level_unloaded`, `session_load_failed`. |
| Hint System | Downstream — depends on GSM | Read dependency. Hint System reads current board state to compute the optimal next move. No mutation interface — Hint System is read-only. | Exposes: `stack_contents`, `stack_depth`, `temp_slot_contents`, `temp_slot_depth`, `temp_slot_count`, `color_count` (same read interface as Sort Mechanic). |
| Tutorial System | Downstream — depends on GSM | Event subscription. Tutorial System subscribes to `level_loaded` to activate gesture overlays for tutorial levels. | Emits to Tutorial System: `level_loaded`. |
| Save & Persistence | Downstream — GSM provides data to persist | Data push. GSM serializes board state and `move_count` to Save & Persistence on app background events (SER-01). GSM reads serialized state on foreground restore (SER-02). Undo history is session-only — not persisted across app kills. Deserialization failure handled by SER-03 / EC-19. | Pushes: board state snapshot (`stack_contents[]`, `temp_slot_contents[]`, `stack_depth`, `temp_slot_depth`, `temp_slot_count`, `color_count`, `move_count`, `current_sequence_id`, `level_id`, `gsm_state`). Does NOT push: undo stack (session-only). |
| Analytics System | Downstream — depends on GSM | Event subscription. Analytics System subscribes to `level_complete` to record completion telemetry. No mutation interface — Analytics System is read-only. | Emits to Analytics System: `level_complete`. |

**Hard vs. soft dependencies:**
- Level Data System: **hard** — GSM cannot load any level without a ready Level Data System.
- Sort Mechanic: **hard** — all board mutations originate from Sort Mechanic events; without Sort Mechanic, GSM ACTIVE state is inert.
- Animation System: **soft** — GSM emits events regardless. If Animation System is absent (test environment), the watchdog timeout handles MOVE_EXECUTING exit (EC-07).
- In-Game HUD: **soft** — GSM emits events regardless of whether HUD is subscribed.
- Level Progression: **hard** — GSM cannot transition from UNLOADED to LOADING without a `load_level` command from Level Progression (or equivalent orchestrator).
- Hint System, Tutorial System: **soft** — read-only consumers; GSM emits events regardless.
- Save & Persistence: **soft** — GSM functions without persistence (session data only). Required for cross-session state preservation.
- Analytics System: **soft** — GSM emits events regardless of whether Analytics System is subscribed.

**Bidirectional consistency:** Each downstream system listed above must reference Game State Manager in their own Dependencies section when their GDD is authored.

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect if Too High | Effect if Too Low |
|---|---|---|---|---|
| `watchdog_timeout_ms` | 1500ms | 500ms–3000ms | Long timeout → Sort Mechanic stays stuck in MOVE_EXECUTING longer on animation crash; player perceives the game as frozen | Short timeout → fires before legitimate long animations complete; forces board snap on normal moves |
| Undo history memory ceiling | No cap (unlimited) | 30–∞ entries | Not applicable — higher is more permissive | If a cap is introduced below 30: players at max board size (8 colors × 8 depth = 64 bolts) may not be able to undo to level start; cap is invisible but breaks the "unlimited undo" promise |
| L-01 readiness retry backoff | Not specified (deferred to implementation) | 250ms–2000ms | Long backoff → Level Data System delays visible to players on cold launch | Short backoff → CPU churn if Level Data System is slow to initialize on low-end devices |
| `request_queue_limit` (inherited from Level Data System) | 8 (defined in Level Data System GDD) | 4–16 | Not GSM's knob to tune — defined by Level Data System | See Level Data System Tuning Knobs |

**Knob interactions:**
- `watchdog_timeout_ms` must be set above the Animation System's maximum bolt movement animation duration (defined as 300ms in the Sort Mechanic Tuning Knobs). The safe minimum for `watchdog_timeout_ms` is `animation_duration_max + margin` — at least 500ms. Setting `watchdog_timeout_ms` at or below the animation duration budget causes legitimate animations to trigger false watchdog fires.
- The undo history memory ceiling and session length are related: unlimited undo in a very long session (player undoes 200+ moves) consumes proportionally more memory. On low-end Android devices with aggressive memory management, a very large undo stack may be killed by the OS mid-session. Evaluate during performance profiling; a soft cap with a warning log is the mitigation if this becomes a concern.

**Scope boundary:** `color_count`, `stack_depth`, `temp_slot_count`, and `temp_slot_depth` are level authoring parameters owned by the Level Data System and Level Progression GDDs. These knobs define GSM's behavior constraints only.

## Visual/Audio Requirements

Not applicable for direct output. The Game State Manager is a pure data and event layer — it produces no visual or audio output of its own. All visual consequences of GSM events are implemented by the Animation System and In-Game HUD. All audio consequences are implemented by the Audio System. Any visual or audio requirements that depend on GSM behavior should be documented in those systems' GDDs, referencing the GSM events they subscribe to.

## UI Requirements

The Game State Manager has no player-facing UI. It exposes two values used by the In-Game HUD:
- `move_count` — integer; read by HUD on `board_state_changed` and `level_loaded` events to update the move counter display
- Undo stack depth (count only) — read by HUD on `board_state_changed` and `level_loaded` events to set the undo button enabled/disabled state

The HUD owns all display decisions for these values. GSM makes no requirements on how they are formatted or presented. The HUD's constraint on GSM is that these fields must be readable synchronously in ACTIVE state.

## Acceptance Criteria

> **Test type**: Logic (state machine, board mutation, formula correctness). AC-GSM-01 through AC-GSM-21 are all BLOCKING — automated unit tests required in `tests/unit/game-state-manager/` before implementation stories can be marked Done. Use a stub Level Data System for all lifecycle tests. Advisory tests (watchdog timer cancellation, visual freezes, HUD button states, app lifecycle) belong in integration test plans and manual playtest documentation.

**AC-GSM-01 — BSM-01: move_committed triggers all five mutations synchronously** *(BLOCKING)*
**GIVEN** a board in ACTIVE state with `move_count = M`, `current_sequence_id = N`, undo stack depth = D, **WHEN** `move_committed(source=0, destination=1, color_id=3, sequence_id=N)` is received, **THEN**: (1) top element of `stack_contents[0]` is removed, (2) `color_id=3` is appended to `stack_contents[1]`, (3) undo stack depth = D+1 with top entry `{source_index=0, destination_index=1, color_id=3, sequence_id=N}` (pre-increment value), (4) `current_sequence_id = N+1`, (5) `move_count = M+1` — all observable in this exact post-event state.

**AC-GSM-02 — BSM-02/03: move_cancelled and move_rejected produce zero mutations** *(BLOCKING)*
**GIVEN** a board in ACTIVE state with a captured reference snapshot of all fields, **WHEN** `move_cancelled(source=0, color_id=3)` is received, and then `move_rejected(source=0, destination=1, color_id=3, reason=COLOR_MISMATCH)` is received, **THEN** `stack_contents`, `temp_slot_contents`, `current_sequence_id`, `move_count`, and undo stack are byte-identical to the reference snapshot after each event individually. No GSM events are emitted in response to either.

**AC-GSM-03 — board_state_changed is NOT emitted after move_committed** *(BLOCKING)*
**GIVEN** a board in ACTIVE state with a subscribed observer on all GSM events, **WHEN** `move_committed(source, destination, color_id, sequence_id)` is received and board state is updated, **THEN** no `board_state_changed` event is emitted.

**AC-GSM-04 — UND-01: undo fully reverts board state** *(BLOCKING)*
**GIVEN** a board in ACTIVE state that has received two `move_committed` events (move A then move B), `move_count = 2`, `current_sequence_id = 2`, **WHEN** `undo_requested` is processed, **THEN**: (1) undo entry for move B is popped, (2) `color_id` from move B is removed from `destination_B` and appended to `source_B`, (3) `current_sequence_id = 3` (incremented, not decremented), (4) `move_count = 1`, (5) `board_state_changed(sequence_id=3, move_count=1)` is emitted.

**AC-GSM-05 — UND-02: undo on empty stack is a strict no-op** *(BLOCKING)*
**GIVEN** a board in ACTIVE state with an empty undo stack, **WHEN** `undo_requested` is processed, **THEN** `stack_contents`, `temp_slot_contents`, `current_sequence_id`, and `move_count` are all unchanged, and no GSM event of any kind is emitted.

**AC-GSM-06 — UND-05: undo is frozen in COMPLETE state** *(BLOCKING)*
**GIVEN** a board in COMPLETE state with a non-empty undo stack, **WHEN** `undo_requested` is processed, **THEN** no board mutation occurs, no GSM event is emitted, and `current_sequence_id` and `move_count` remain unchanged.

**AC-GSM-07 — UND-06: sequence_id is strictly monotonically increasing** *(BLOCKING)*
**GIVEN** a board in ACTIVE state starting at `current_sequence_id = 0`, **WHEN** five `move_committed` events and three `undo_requested` events are processed in any interleaving, **THEN** `current_sequence_id = 8`, and each value observed after each mutation is strictly greater than the preceding observed value.

**AC-GSM-08 — WIN-01: puzzle_solved triggers COMPLETE with correct payload and frozen input** *(BLOCKING)*
**GIVEN** a board in ACTIVE state with `move_count = 7`, `current_sequence_id = 7`, **WHEN** `puzzle_solved()` is received, **THEN**: (1) GSM transitions to COMPLETE, (2) `level_complete(level_id, move_count=7, sequence_id=7)` is emitted (sequence_id is the current value at transition — WIN-01 does not increment it), (3) a subsequent `move_committed` is silently ignored and board state does not change, (4) a subsequent `undo_requested` is silently ignored, (5) no further GSM events are emitted.

**AC-GSM-09 — L-03: bolt count check 1 (total count) blocks load** *(BLOCKING)*
**GIVEN** a level record with `color_count = 3`, `stack_depth = 4`, but `color_stacks` containing only 11 total bolts (not 12), **WHEN** L-03 executes, **THEN** `session_load_failed(INVARIANT_VIOLATION, level_id)` is emitted, GSM transitions to UNLOADED, and no `level_loaded` event is emitted.

**AC-GSM-10 — L-03: bolt count check 2 (per-color) blocks load independently of check 1** *(BLOCKING)*
**GIVEN** a level record where total bolt count satisfies check 1 (12 total) but one `color_id` appears 5 times and another appears 3 times, **WHEN** L-03 executes, **THEN** `session_load_failed(INVARIANT_VIOLATION, level_id)` is emitted and GSM transitions to UNLOADED. This test must pass independently — both checks must be active even when check 1 passes.

**AC-GSM-11 — L-04: pre-won board does not auto-win** *(BLOCKING)*
**GIVEN** a level record whose `color_stacks` already satisfy the win condition (all stacks full and monochromatic) and that passes L-03, **WHEN** the level loads through L-05 and L-06, **THEN** GSM transitions to ACTIVE, `level_loaded` is emitted, no `level_complete` is emitted, and GSM state is ACTIVE (not COMPLETE).

**AC-GSM-12 — L-05: board initializes to exact spec values** *(BLOCKING)*
**GIVEN** a successfully loaded level with `color_count = 3`, `stack_depth = 4`, `temp_slot_count = 2`, `temp_slot_depth = 1`, **WHEN** L-05 completes, **THEN** `current_sequence_id = 0`, `move_count = 0`, undo stack depth = 0, `stack_contents` matches the level record's `color_stacks` exactly, `temp_slot_contents` is exactly 2 empty arrays of capacity 1, and `level_loaded` carries `sequence_id = 0`. All six assertions must hold independently.

**AC-GSM-13 — EC-09: load_level rejected in non-UNLOADED states** *(BLOCKING)*
**GIVEN** GSM is in each of the following states independently — LOADING, ACTIVE, COMPLETE — **WHEN** `load_level(level_id)` is received, **THEN** no `level_loaded` event is emitted and GSM remains in its current state. Test each state as a separate sub-case; all three must pass.

**AC-GSM-14 — EC-10: deferred undo fires before win evaluation when MOVE_EXECUTING exits** *(BLOCKING)*
**GIVEN** a board where exactly one legal move remains to satisfy the win condition, `move_committed` is in progress (MOVE_EXECUTING), and `undo_requested` arrives during this window (deferred), **WHEN** the animation completion signal arrives (MOVE_EXECUTING exits), **THEN**: (1) the deferred undo fires first — board reverts to the pre-final-move state, (2) win evaluation runs against the reverted board, (3) `puzzle_solved()` is NOT emitted, (4) GSM remains in ACTIVE state, (5) `board_state_changed` is emitted from the undo.

**AC-GSM-15 — EC-05: deferred undo is discarded when puzzle_solved() arrives** *(BLOCKING)*
**GIVEN** `move_committed` is in progress (MOVE_EXECUTING) and `undo_requested` is deferred, **WHEN** `puzzle_solved()` is received before MOVE_EXECUTING exits, **THEN**: (1) the deferred undo request is cleared and never executed, (2) GSM transitions to COMPLETE, (3) `level_complete` is emitted, (4) when MOVE_EXECUTING subsequently exits, no undo fires and board state does not change.

**AC-GSM-16 — EC-11: watchdog-induced MOVE_EXECUTING exit processes deferred undo** *(BLOCKING)*
**GIVEN** `move_committed` fired (MOVE_EXECUTING active) and `undo_requested` is deferred, **WHEN** the watchdog timer expires (no animation completion signal), **THEN**: (1) `current_sequence_id` is incremented, (2) `board_refresh_forced(new_sequence_id)` is emitted, (3) the deferred undo is subsequently processed — board state reverts the committed move, `move_count` decrements, `board_state_changed` is emitted.

**AC-GSM-17 — EC-14: readiness query is fresh per load attempt, not cached** *(BLOCKING)*
**GIVEN** a Level Data System stub that returns `ready = false` on the first call and `ready = true` on the second, **WHEN** `load_level` is called, fails with `LEVEL_DATA_UNAVAILABLE`, GSM returns to UNLOADED, and `load_level` is called a second time, **THEN** the stub's System Readiness Query is called a second time (not returning cached `false`), and the second load proceeds to `level_loaded`. Verify the stub was called exactly twice.

**AC-GSM-18 — WDG-01: watchdog emits board_refresh_forced with incremented sequence_id** *(BLOCKING)*
**GIVEN** `move_committed(sequence_id=N)` fires and no animation completion signal arrives within `watchdog_timeout_ms`, **THEN** `current_sequence_id = N+1` and `board_refresh_forced(sequence_id=N+1)` is emitted. The committed bolt is present in `stack_contents[destination]` — no rollback.

**AC-GSM-19 — move_count formula correctness across interleaved sequence** *(BLOCKING)*
**GIVEN** a board starting at `move_count = 0`, **WHEN** the sequence fires: `move_committed`, `move_committed`, `move_cancelled`, `move_rejected`, `undo_requested` (stack non-empty), `move_committed`, **THEN** `move_count = 2` at the end. Verify intermediate values after each event: 1, 2, 2, 2, 1, 2.

**AC-GSM-20 — level_loaded carries all required parameters** *(BLOCKING)*
**GIVEN** a level record loaded through L-01–L-05 using a stub Level Data System, **WHEN** L-06 fires, **THEN** `level_loaded` carries the correct `level_id`, `color_count`, `stack_depth`, `temp_slot_count`, `temp_slot_depth`, and `sequence_id = 0`. All six fields must match the level record.

**AC-GSM-21 — EC-17: deferred undo queue capacity is exactly 1** *(BLOCKING)*
**GIVEN** MOVE_EXECUTING is active and `undo_requested` is already deferred (queue depth = 1), **WHEN** a second `undo_requested` arrives, **THEN** the queue length remains 1 (second request dropped), and after MOVE_EXECUTING exits exactly one undo fires — not two. Board state reverts exactly one move.

## Open Questions

| Question | Owner | Target Resolution | Resolution |
|---|---|---|---|
| What is the exact `watchdog_timeout_ms` value? Must exceed the Animation System's maximum animation duration (300ms per Sort Mechanic Tuning Knobs) plus a margin. Current default: 1500ms. | Animation System GDD | Before Animation System GDD is authored | **Resolved: 1500ms — confirmed in Animation System GDD and GSM Tuning Knobs.** |
| What is the animation completion signal's name and signature? (Required by Sort Mechanic and GSM watchdog logic.) | Animation System GDD | Before Animation System GDD is authored | **Resolved: `animation_complete(sequence_id: int64)` — confirmed in Animation System GDD (BM-04, AC-BM-02). int64 required; int32 wrap causes permanent MOVE_EXECUTING softlock.** |
| Should the watchdog timer use `WaitForSeconds` (game time) or `WaitForSecondsRealtime` (wall clock)? If the game can be paused while MOVE_EXECUTING is active, `WaitForSeconds` never fires at time scale = 0 — the watchdog would silently fail. | Technical Director / Game Designer | Before Animation System GDD is authored | Open |
| Should undo history be persisted across app kills (not just backgrounding)? Currently: no — undo stack is session-only and cleared on app kill. A player revived mid-puzzle loses all undo history. | Game Designer | Before Save & Persistence GDD is authored | Open |
| Should `level_complete` carry `move_count` as net moves (committed minus undone, current definition) or as total committed moves regardless of undo? If Coin Economy needs total taps for its star rating formula, a separate `total_committed_count` field may be needed. | Coin Economy GDD / Game Designer | Before Coin Economy GDD is authored | Open |
| Service architecture ADR: GSM must be accessed through an `IGameStateManager` interface to satisfy the coding standard's dependency injection requirement. No ADR exists for this decision yet. | Technical Director | Before implementation sprint begins | Open — flag for `/architecture-decision` |
| Level Progression retry backoff: what is the retry interval when `session_load_failed(LEVEL_DATA_UNAVAILABLE)` is received? Level Progression must implement backoff; GSM re-queries fresh on each attempt. | Level Progression GDD | Before Level Progression GDD is authored | Open |
