# Level Progression

> **Status**: Designed
> **Author**: Design session + agents
> **Last Updated**: 2026-04-21
> **Implements Pillar**: Flow Over Friction, Respect the Session

## Overview

Level Progression is BoltSort's sequence controller and session orchestrator. It owns four responsibilities: issuing `load_level` and `exit_level` commands to the Game State Manager (lifecycle), recording star ratings and coin amounts per level (completion ledger), advancing the player's position through the numbered sequence (unlock flow), and routing post-level navigation events to their correct destinations (`next_level_requested`, `retry_requested`, `menu_requested`). For the player, this system is what makes the machine feel continuous: each puzzle solved flows directly into the next, progress is never lost, and returning to the game always means returning exactly where you left off. Its quality is measured by its invisibility — when Level Progression is correct, the player never thinks about it; they just play.

## Player Fantasy

Each level you complete doesn't end — it *advances*. The bolts settle, the column locks, and before you've fully registered the satisfaction, the next puzzle is already loading, already yours. You never have to ask permission to continue. Progress in BoltSort feels less like climbing a ladder and more like being carried gently along a conveyor — every level a small, clean resolution, every unlock a soft forward hum. The number going up is not a score. It is the sound of the machine running smoothly, with you at the controls.

And when you return — to the train, to the lunch break, to before sleep — the machine has kept your place. Level 47 opens with the same quiet confidence it had when you closed the app. The assembly line doesn't forget. That continuity is not a feature; it is the promise the game makes to you, and keeps.

*Primary pillars: Flow Over Friction, Respect the Session, The Machine Must Sing*
*MDA target: Sensation (1), Submission/Flow (2)*

## Detailed Design

### Core Rules

**LP Responsibilities**

Level Progression owns four concerns:
1. **Session lifecycle** — issues `load_level(level_id)` and `exit_level` to the Game State Manager; waits for confirmation before advancing
2. **Completion ledger** — records `best_stars` and `completion_version` per level when `level_complete` fires
3. **Unlock flow** — advances `current_level_id` only after verifying the previous level is completed, the next level exists, and GSM is UNLOADED
4. **Navigation routing** — handles `next_level_requested`, `retry_requested`, `menu_requested`, and app relaunch; routes each to the correct outcome

**Coin Delegation to Coin Economy** *(resolves OQ-01 — Coin Economy GDD now authored)*

LP does not hold a `coin_balance` field. Coin Economy is the sole owner of `economy.coin_balance`. LP acts as a thin router:
- On `coin_reward_granted(amount)` from Level Complete UI (EC-05 state guard passes): LP calls `CE.AddCoins(amount, level_id, EarnSource.Base)`. No LP-owned mutation.
- On `coin_bonus_granted(amount)` from Level Complete UI (EC-06 state guard passes): LP calls `CE.AddCoins(amount, level_id, EarnSource.AdBonus)`. No LP-owned mutation.
- `LP.GetCoinBalance(): int` delegates to `CE.GetCoinBalance()`. LP holds no independent balance state.

LP must pass `level_id` (from the active `LevelCompleted` event context) and the correct `EarnSource` on every `CE.AddCoins` delegation. LP emits `LevelCompleted(stars: int, level_id: int, move_count: int, par_moves: int)` to Coin Economy as the event/callback boundary; CE subscribes for analytics. No circular dependency.

**Completion Record Schema**

One record per level:

| Field | Type | Notes |
|---|---|---|
| `level_id` | int | Foreign key to Level Data System record |
| `best_stars` | int | 0 = never completed; 1–3 = best outcome across all plays |
| `completion_version` | string | Game version at first completion ("YYYY.MM"). Null until first completion. Written only once — never overwritten. |

**Star update rule:** `best_stars = max(current_best_stars, stars_earned)` on every completion. Progress never decreases. A player who earned 3★ cannot lose it by replaying with fewer moves.

**Completion Record Write Guard**

- Write only when LP is in `COMPLETION_FLOW` state
- Write only if `level_id` in the `level_complete` payload matches `current_level_id`
- Duplicate `level_complete` events with the same `level_id` are no-ops
- `completion_version` written only on first completion (when `best_stars` was previously 0)

**Prefetch**

On receiving `level_complete`, LP immediately calls `GetRange(current_level_id + 1, current_level_id + 3)` on the Level Data System (non-blocking background call). This hides load latency behind the Level Complete UI's ~2,000ms celebration. An empty or partial array response (end of catalogue) is handled normally — not an error.

**Breather Level Flag**

`is_breather = (level_id % 10 == 0)`. Computed by LP; used for UI labels and analytics. LP does not enforce any difficulty constraint on breather levels — that is the level designer's authoring responsibility. The authoring pipeline should emit a warning (not a blocking error) if a level at a breather position has `difficulty_tier` higher than one tier below its neighbors.

**Unlock Validation**

Before issuing `load_level(N+1)`, LP validates three conditions in order:
1. `completion_record[N].best_stars >= 1` — Level N was completed
2. `GetLevel(N+1)` returns success — Level N+1 exists in the catalogue
3. `level_unloaded` received from GSM — GSM is UNLOADED

If Condition 2 fails: LP emits `next_level_unavailable`. No navigation occurs.

**Session Load Failed Rules**

| Error | LP Action |
|---|---|
| `LEVEL_DATA_UNAVAILABLE` | Wait `load_failed_retry_delay_ms`. Re-issue `load_level`. Retry up to `load_failed_max_retries` times. On exhaustion: show error UI with manual retry button. |
| `LEVEL_RECORD_ERROR` (NOT_FOUND / VALIDATION_FAILED) | If `current_level_id > 1`: fall back to `current_level_id - 1` (silent). If `current_level_id == 1`: show error UI. |
| `INVARIANT_VIOLATION` | Same as LEVEL_RECORD_ERROR. Additionally emit an analytics event with the corrupt `level_id`. |
| `INSTANTIATION_ERROR` | Wait `instantiation_retry_delay_ms`. Retry once. On second failure: show error UI. |

**App Relaunch / Session Resume**

1. Read `current_level_id` from Save & Persistence on cold start
2. Call `GetLevel(current_level_id)` — existence check
3. If found: issue `load_level(current_level_id)`. Enter `LEVEL_LOADING`
4. If NOT_FOUND: attempt `GetLevel(highest_completed_level + 1)`. If also NOT_FOUND: load Level 1
5. GSM is always UNLOADED on cold start — no `level_unloaded` wait required
6. No coins are awarded; Level Complete UI is not shown; `LevelCompleted` is not emitted

---

**Difficulty Progression Schedule**

Owned by this GDD. Valid parameter envelopes per tier are defined in the Level Data System GDD; this table assigns which levels use which tier.

| Tier | Name | Level Range | color_count | stack_depth | temp_slot_count | Notes |
|---|---|---|---|---|---|---|
| 1 | Intro | 1–10 | 2–3 | 3–4 | 2–3 | Levels 1–5: 2 colors, depth 3, 3 temp slots (fully scaffolded). Levels 6–10: 3 colors, depth 4, 2 temp slots. |
| 2 | Easy | 11–50 | 3–4 | 4–5 | 2 | Wide on-ramp. Color count climbs from 3 to 4 across this range. Temp slots stay at 2 — comfort zone. |
| 3 | Medium | 51–110 | 4–5 | 5–6 | 1–2 | Temp slots begin dropping. Oscillates 1–2, trending toward 1 in the upper range. |
| 4 | Hard | 111–160 | 5–7 | 6–7 | 1 | Single temp slot only. Major step up — 7 colors possible. |
| 5 | Expert | 161–200 | 7–8 | 7–8 | 0–1 | Peak complexity. 0-temp-slot levels permitted only at `difficulty_tier ≥ 3` per LDS hint_override rules. |

**Breather levels** appear at every 10th position (levels 10, 20, 30 ... 200). Each is authored one full tier below its surrounding levels:

| Breather range | Drop to tier |
|---|---|
| Levels 10, 20, 30, 40 | Tier 1 (Intro) |
| Levels 50, 60, 70, 80, 90 | Tier 2 (Easy) |
| Levels 100–140 | Tier 3 (Medium) |
| Levels 150–200 | Tier 3 or 4 (context-dependent) |

---

### States and Transitions

```
IDLE → LEVEL_LOADING → LEVEL_ACTIVE → COMPLETION_FLOW → LOAD_PENDING → IDLE
                ↓                                               ↓
         IDLE (load fail exhausted)                    IDLE (menu routing)
```

| State | Entry | Exit | LP behavior |
|---|---|---|---|
| `IDLE` | App launch; `level_unloaded` + menu routing; load fail recovery | → `LEVEL_LOADING` when `load_level` issued | No level active. Ready for next load command. |
| `LEVEL_LOADING` | `load_level(level_id)` issued to GSM | → `LEVEL_ACTIVE` on `level_loaded`; → `IDLE` on `session_load_failed` (exhausted) | Waiting for GSM load confirmation. Navigation events received here are queued and processed after `level_loaded`. |
| `LEVEL_ACTIVE` | `level_loaded` received | → `COMPLETION_FLOW` on `level_complete` | Level is running. LP listens passively. |
| `COMPLETION_FLOW` | `level_complete(level_id, move_count)` received | → `LOAD_PENDING` on any navigation event | Writes completion record (with guard). Applies coins. Fires `LevelCompleted(stars)`. Fires prefetch. Waits for navigation input. |
| `LOAD_PENDING` | `exit_level` issued to GSM | → `LEVEL_LOADING` on `level_unloaded` + advance; → `IDLE` on `level_unloaded` + menu routing | Waiting for GSM teardown. Does not issue next `load_level` until `level_unloaded` is received. |

**Navigation routing summary:**

| Event | LP action |
|---|---|
| `next_level_requested` (N+1 exists) | `exit_level` → LOAD_PENDING → on `level_unloaded`: advance `current_level_id`, issue `load_level(N+1)` |
| `next_level_requested` (N+1 NOT_FOUND) | Emit `next_level_unavailable`. Stay in COMPLETION_FLOW. No `exit_level` issued. |
| `retry_requested` | `exit_level` → LOAD_PENDING → on `level_unloaded`: issue `load_level(current_level_id)` (same level; no ledger write; no coin grant) |
| `menu_requested` | `exit_level` → LOAD_PENDING → on `level_unloaded`: route to main menu → IDLE |
| App relaunch | Read `current_level_id` from Save & Persistence → issue `load_level` → LEVEL_LOADING |

---

### Interactions with Other Systems

| System | Direction | LP action |
|---|---|---|
| Level Data System | Upstream read | `GetLevel(level_id)` at unlock validation; `GetRange(N+1, N+3)` for prefetch; `GetByFilter` for future Daily Challenge eligibility checks |
| Game State Manager | Bidirectional | Issues `load_level(level_id)`, `exit_level`. Subscribes to `level_loaded`, `level_complete`, `level_unloaded`, `session_load_failed` |
| Level Complete UI | Downstream event receiver | Receives `coin_reward_granted(amount)`, `coin_bonus_granted(amount)`, `next_level_requested`, `retry_requested`, `menu_requested`. Must be subscribed before Level Complete UI's `OnEnable` fires. |
| Save & Persistence | Bidirectional | Reads `current_level_id` and `completion_record[]` on cold start after `SaveSystem.IsReady` (subscribe to `OnSaveReady`, then check synchronously). Writes via `WriteCompletionAtomic(level_id, best_stars, completion_version, new_current_level_id)` on level completion (EC-16). |
| Coin Economy | Downstream event receiver | LP emits `LevelCompleted(stars: int)`. Coin Economy subscribes for economy-side effects. Event/callback boundary — no circular dependency. |
| Main Menu UI | Downstream data source | Provides `current_level_id` and aggregate stats on request. |
| Level Select UI | Downstream data source | Provides `completion_record[level_id].best_stars` and lock state (`level_id > current_level_id`) per level. |

## Formulas

### Formula 1: Breather Level Flag

The `is_breather` formula is defined as:

`is_breather = (level_id % 10 == 0)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Level identifier | `level_id` | int | 1–9999 | The canonical level number from the Level Data System |
| Breather flag | `is_breather` | bool | true / false | True if this level is a designated breather position in the sequence |

**Output Range:** Boolean. True for levels 10, 20, 30 ... 200 (and any future levels at multiples of 10).

**Uses of this flag:** (a) Passed to Level Select UI for a visual label on the level tile. (b) Included in the `LevelCompleted` analytics event payload.

**Edge cases:**
- **Level 10 (Tier 1 boundary):** `is_breather = true` and level 10 is already Tier 1 (Intro). No tier drop needed — it stays at its own tier. Authoring pipeline warning is not triggered.
- **Level 200 (last level):** `is_breather = true`. LP emits `next_level_unavailable` after completion; the flag fires correctly regardless.
- **Levels beyond 200:** Formula is valid for any `level_id` in [1, 9999]. Future level packs inherit the same rule.
- **Level 0:** Outside the valid LDS range (minimum is 1). Not applicable.

---

### Formula 2: Lock State

The `is_locked` formula is defined as:

`is_locked(level_id) = (level_id > current_level_id)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Query level | `level_id` | int | 1–9999 | The level whose lock state is being queried |
| Current level pointer | `current_level_id` | int | 1–9999 | The highest level the player is authorized to load. Advances to N+1 after completing level N. |
| Lock result | `is_locked` | bool | true / false | True = level is inaccessible; False = level can be loaded or has been completed |

**Output Range:** Boolean.

**Definition of `current_level_id`:** The current or next-to-play level — the highest level the player is authorized to load. After completing level N, `current_level_id` advances to N+1. A player who has completed levels 1–29 has `current_level_id = 30`.

**Example:** `current_level_id = 30` → levels 31+ are locked; levels 1–30 are accessible.

**Consumers:** Level Select UI (lock icon display), Main Menu UI (progress display). LP exposes this via an `IsLocked(level_id): bool` read method.

---

### Formula 3: Star Update Rule

The `best_stars` update formula is defined as:

`best_stars' = max(current_best_stars, stars_earned)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Prior best star count | `current_best_stars` | int | 0–3 | Stored value before this update. 0 = never completed (sentinel, not an earned value). |
| Stars earned this play | `stars_earned` | int | 1–3 | Star rating from this session's Level Complete UI formula. Always ≥ 1 when a level is completed. |
| Updated best | `best_stars'` | int | 0–3 | Value written back to the completion record. |

**Output Range:** [0, 3]. 0 means "no completion on record." Stars earned are always in [1, 3] — 0 is never returned by the star rating formula.

**Invariant:** Progress never decreases. A player who earned 3★ cannot lose it by replaying with a worse result.

**Example:**

| Play | `stars_earned` | `current_best_stars` | `best_stars'` |
|---|---|---|---|
| First completion | 2 | 0 | 2 |
| Replay (worse) | 1 | 2 | 2 (no change) |
| Replay (better) | 3 | 2 | 3 |

---

### LevelCompleted Event Signature

LP emits `LevelCompleted` to Coin Economy. Canonical signature:

`LevelCompleted(stars: int, level_id: int, move_count: int, par_moves: int)`

| Parameter | Source | Description |
|---|---|---|
| `stars` | Derived from Level Complete UI formula | 1–3 star rating |
| `level_id` | `current_level_id` at completion | Enables per-tier economy effects in Coin Economy |
| `move_count` | From `level_complete` GSM payload | Raw performance — preserves design space for efficiency bonuses |
| `par_moves` | From Level Data System `GetLevel(level_id).par_moves` | Raw baseline — enables Coin Economy to recalculate or verify |

All four values are available to LP at completion time with no additional computation. Coin Economy subscribes and does not return a response.

---

### Non-Formula Rules

**Coin routing (delegated to CE):** On each `coin_reward_granted` or `coin_bonus_granted` received in `COMPLETION_FLOW` state, LP calls `CE.AddCoins(amount, level_id, earn_source)` (see Coin Delegation section above). LP holds no `coin_balance` state. `LP.GetCoinBalance(): int` delegates to `CE.GetCoinBalance()` — this passthrough is the read interface for HUD, Hint System, and Shop UI.

**Prefetch window:** LP calls `GetRange(current_level_id + 1, current_level_id + 3)` on `level_complete`. An empty or partial result (end of catalogue) is handled normally — not an error per LDS EC-08.

**Difficulty tier assignment:** A lookup table in Section C. LP does not compute `difficulty_tier` — it is an authored field per level in the Level Data System.

## Edge Cases

**EC-01 — `next_level_requested` when N+1 doesn't exist (including Level 200):**
LP already knows the result from the prefetch `GetLevel(N+1)` check at COMPLETION_FLOW entry. Emit `next_level_unavailable` immediately. Stay in COMPLETION_FLOW. No `exit_level` issued. Player must use Retry or Menu. Level 200 is the designed terminal state; `next_level_unavailable` here is the intended "you've beaten the game" signal for UI.

**EC-02 — Duplicate `level_complete` with same `level_id` (timing race):**
LP is still in COMPLETION_FLOW when the second event fires. Write guard (state + level_id match) discards it. No-op. No double coin grant. No re-write of completion record.

**EC-03 — `level_complete` payload `level_id` ≠ `current_level_id`:**
GSM contract violation. Log a warning identifying both IDs (received vs. expected). Write guard: discard. No record write, no coin grant, no advancement. Do not transition state.

**EC-04 — App relaunch: save references a `level_id` not in catalogue:**
1. If `highest_completed_level` is known: attempt `GetLevel(highest_completed_level + 1)`. If NOT_FOUND: load Level 1.
2. If completion record is also empty (new install, no prior completion data): fall through directly to Level 1 without attempting a `highest_completed_level` lookup on an empty record set.

**EC-05 — `coin_reward_granted` received outside COMPLETION_FLOW or for previously-completed levels:**

*In-session state guard:* If `coin_reward_granted` is received outside COMPLETION_FLOW state, discard. No `coin_balance` mutation. Level Complete UI fires this exactly once at `OnEnable` before state can advance — this guard is defensive completeness for race conditions or mis-sequenced boot.

*Cross-session guard (LP-03):* Before forwarding `coin_reward_granted` to CE via `CE.AddCoins(amount, level_id, EarnSource.Base)`, LP must check `SP.has_completion_record(level_id)`. If the completion record already exists for this level (the player is replaying a previously completed level across a session boundary), LP must NOT forward `coin_reward_granted` to CE. This prevents cross-session duplicate coin credits when `last_credited_level_id` in CE has reset to -1 on cold relaunch. *[Cross-GDD LP-03 — Resolved 2026-05-08. Required by Coin Economy GDD CE-12. LP GDD satisfies CE's hard implementation gate.]*

**EC-06 — `coin_bonus_granted` arrives after player has navigated (LP in LOAD_PENDING):**
LP's state guard: LOAD_PENDING is not COMPLETION_FLOW. Discard. Base coins were already applied in COMPLETION_FLOW. Do not re-enter COMPLETION_FLOW to apply the bonus. Level Complete UI GDD handles the UI-side race (AC-28); LP handles it at the state layer.

**EC-07 — Retry: three invariants that must all hold:**
`retry_requested` reloads `current_level_id` without (a) writing to the completion record, (b) mutating `coin_balance`, or (c) emitting `LevelCompleted`. Coins were already granted before the player could tap Retry. Retry resets board state in GSM, not economy or ledger state in LP.

**EC-08 — `session_load_failed(LEVEL_RECORD_ERROR)` at `current_level_id = 1`:**
Step-back is impossible. Show error UI. Log the `error_code` — NOT_FOUND (missing catalogue) and VALIDATION_FAILED (malformed record) require different team responses but produce the same player-facing outcome.

**EC-09 — Prefetch `GetRange` returns empty array (end of catalogue):**
Normal condition per LDS EC-08. Partial or empty arrays near levels 198–200 are handled without error. LP already knows the terminal state via the `next_level_unavailable` path.

**EC-10 — `level_complete` received while LP is in LEVEL_LOADING:**
Sequence violation. Discard (do not queue). Log a warning. LP queues navigation events in LEVEL_LOADING but never `level_complete` events.

**EC-11 — `level_unloaded` received while LP is in IDLE (unexpected GSM teardown):**
LP did not issue `exit_level`. Log a warning. Emit an analytics event: `unexpected_level_unloaded(lp_state: IDLE)`. No state transition. Stay in IDLE. Surfaces in QA rather than being silently swallowed.

**EC-12 — `is_locked(current_level_id)` queried for the player's own current level:**
`is_locked(current_level_id) = (current_level_id > current_level_id) = false`. The current level is always accessible. Level Select UI must not render a lock icon on this tile.

**EC-13 — `best_stars` queried for a level with no completion record:**
Returns 0 (sentinel for "never completed"). Consistent with `is_locked` — a level with no record was never completed, so nothing beyond it should be unlocked.

**EC-14 — `level_unloaded` received while LP is in LEVEL_ACTIVE (unexpected mid-session GSM teardown):**
Session interrupted (crash recovery, OS kill). Transition to IDLE. Do not write a completion record. Do not advance `current_level_id`. Emit: `session_interrupted(level_id, source: "unexpected_unload")`. On next relaunch, LP reloads `current_level_id` — player resumes the same level.

**EC-15 — Save write failure after completion record update:**
Retain the updated in-memory values for the current session. Retry the write per Save & Persistence GDD write-retry policy (dirty flag gating W-2). If retry also fails: surface a non-blocking persistent warning ("Progress may not be saved"). Never block gameplay on a save failure.

**EC-16 — Atomicity: star record and `current_level_id` advance must be written together:**
If one write succeeds and the other fails, the player may be locked out of their current level on next relaunch. LP requires Save & Persistence to support atomic write of both fields. This is a cross-system requirement for the Save & Persistence GDD — flag it explicitly when authored.

**EC-17 — Multiple navigation events queued in LEVEL_LOADING:**
Queue depth limit: 1. If two or more navigation events arrive before `level_loaded` (e.g., rapid "retry" then "menu" taps): only the first queued event is processed on `level_loaded`. Subsequent queued events are discarded.

**EC-18 — `level_complete` payload contains `stars_earned = 0`:**
Contract violation from Level Complete UI. Write guard: discard. Log a contract violation warning with the received payload. Do not write the completion record, advance `current_level_id`, or grant coins.

**EC-19 — `coin_balance` integer overflow:**
`coin_balance: int` (int64 in C#/.NET). Maximum realistic coins: 40/level × 200 levels = 8,000. No overflow risk at current scale. Formal cap (`coin_balance_max`) delegated to Coin Economy GDD. No action needed now.

**EC-20 — App relaunch: `current_level_id = N` but `best_stars[N-1] = 0` (broken completion chain):**
Possible from partial save write. LP trusts `current_level_id` as the single source of truth for progress — no retroactive chain re-validation on relaunch. `best_stars` records are supplementary display/economy data; they do not gate the current level. Accepted inconsistency.

**EC-21 — `GetLevel(N+1)` succeeds at prefetch time but fails at actual load (TOCTOU):**
The level may be removed between the prefetch existence check and the actual `load_level` — e.g., a live catalogue update. The existing `session_load_failed(LEVEL_RECORD_ERROR)` path handles this correctly via the step-back rule. No special case needed.

**EC-22 — Stale prefetch `GetRange` response arrives after LP has already advanced:**
The prefetch is non-blocking. If LP is already in LEVEL_LOADING when the result arrives, it is silently consumed by the Level Data System's internal cache. LP takes no action. Prefetch has no state side-effects on LP.

**EC-23 — `stars_earned = 3` on a breather level:**
`is_breather` is a presentation flag only — it does not affect record-keeping or economy. `best_stars = max(current, 3) = 3` is applied normally.

## Dependencies

**Systems this GDD depends on (upstream):**

| System | Direction | Nature | Hard/Soft | Interface |
|---|---|---|---|---|
| Level Data System | Upstream | Data dependency — queries level existence, difficulty metadata, par_moves | Hard | `GetLevel(level_id)`, `GetRange(from, to)`, `GetByFilter(filter)` |
| Game State Manager | Bidirectional — LP is the lifecycle orchestrator | Command + event — LP issues load/exit; GSM responds with lifecycle events | Hard | Emits: `load_level(level_id)`, `exit_level`. Subscribes: `level_loaded`, `level_complete`, `level_unloaded`, `session_load_failed` |
| Save & Persistence | Bidirectional | Read on cold start; write on completion and progress advance | Hard (cross-session); Soft (in-session — LP holds state in memory) | Reads: `current_level_id`, `completion_record[]` after `SaveSystem.IsReady`. Writes: `WriteCompletionAtomic(level_id, best_stars, completion_version, new_current_level_id)` (EC-16). |
| Coin Economy | Bidirectional | LP delegates `AddCoins` and `GetCoinBalance` to CE (CE-02). CE subscribes to LP's `LevelCompleted` for analytics | Hard (CE GDD now authored) | LP calls: `CE.AddCoins(amount, level_id, earn_source)`, `CE.GetCoinBalance(): int`. LP emits: `LevelCompleted(stars, level_id, move_count, par_moves)`. |

**Systems that depend on this GDD (downstream):**

| System | Direction | Nature | Contract |
|---|---|---|---|
| Level Complete UI | Downstream event receiver + navigation emitter | Event receiver + navigation emitter | LP must be subscribed before Level Complete UI's `OnEnable` fires. LP receives: `coin_reward_granted(amount)`, `coin_bonus_granted(amount)`, `next_level_requested`, `retry_requested`, `menu_requested`. |
| Main Menu UI | Downstream read-only | Queries aggregate progress stats | Queries `current_level_id` and total completion counts. |
| Level Select UI | Downstream read-only | Queries per-level state | Queries `best_stars(level_id)`, `IsLocked(level_id)`, `IsBreather(level_id)`, `GetCoinBalance()`. |
| Daily Challenge System | Downstream read-only | Eligibility check | Queries `IsCompleted(level_id)` to verify a level can appear in the daily pool. Does not replicate LP's ledger. |
| Tutorial System | Downstream event subscriber | Activation trigger | Subscribes to `level_loaded` (forwarded from GSM) to activate gesture overlays on `is_tutorial = true` levels. |

**Hard vs. soft:**
- Level Data System: **hard** — LP cannot sequence any level without it
- Game State Manager: **hard** — LP has no other path to load or exit levels
- Save & Persistence: **hard** for cross-session state; soft for in-session play
- Coin Economy: **hard** — CE GDD authored; LP delegates all coin state to CE (OQ-01 resolved)
- Level Complete UI: **soft** — LP emits events regardless of subscriber presence (test environments)

**Bidirectional consistency:** GSM GDD, Level Complete UI GDD, and Level Data System GDD already reference Level Progression in their Dependencies sections. Save & Persistence GDD (not yet authored) must document the atomic write requirement from EC-16. Coin Economy GDD (not yet authored) must document `LevelCompleted` as its primary input event.

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect if Too High | Effect if Too Low |
|---|---|---|---|---|
| `load_failed_retry_delay_ms` | 500ms | 250–2000ms | Long delay → visible loading hesitation on Level Data System slow start (coldstart UX regression) | Short delay → CPU polling hammers Level Data System that isn't ready yet on low-end devices |
| `load_failed_max_retries` | 3 | 1–10 | Many retries → player waits longer before seeing error UI on persistent failure; may mask the error | Too few → triggers error UI on transient blips; players see error unnecessarily |
| `instantiation_retry_delay_ms` | 1000ms | 500–3000ms | Long delay → player stares at a frozen screen after memory failure | Short delay → retries before OS has reclaimed any memory; second attempt likely fails too |
| `breather_interval` | 10 | 5–20 | Infrequent breathers → difficulty feels relentless in middle and late tiers; churn risk | Frequent breathers → difficulty curve flattens; expert players may feel progression is padded |
| `prefetch_window` | 3 | 1–5 | Larger window → slightly higher memory and LDS query cost; negligible at current level record size | Window of 1 → roughly every other level completion triggers a visible load on completion screen |
| Navigation event queue depth | 1 | 1 (fixed) | N/A — must stay at 1 per EC-17; larger values create ambiguous multi-input sequences | N/A |

**Difficulty progression schedule:** The per-level parameter values (`color_count`, `stack_depth`, `temp_slot_count` per tier) are the highest-leverage tuning surface in this GDD. They are documented in Section C (Difficulty Progression Schedule) and owned by the Level Progression GDD, but the valid ranges for each are defined by the Level Data System GDD's schema constraints. Tune by adjusting level tier boundaries and parameter targets in the difficulty table; changes require a new set of hand-crafted or solver-validated levels.

**Knob interactions:**
- `load_failed_retry_delay_ms` and `load_failed_max_retries` together determine maximum wait before error UI: `max_wait = load_failed_retry_delay_ms × load_failed_max_retries`. At defaults: 500ms × 3 = 1,500ms maximum transient delay.
- `breather_interval` interacts with tier range lengths: if `breather_interval = 5` but Tier 2 spans 40 levels, players get 8 identical-feeling breathers. Tune tier ranges and breather interval together.

## Visual/Audio Requirements

Not applicable. Level Progression is a pure orchestration system with no direct visual or audio output. Visual and audio requirements are owned by the Level Complete UI GDD (celebration effects, chime arc) and the Animation System GDD (bolt settle effects). LP triggers these systems indirectly by emitting `LevelCompleted` and routing navigation events; it does not own any assets or playback calls.

## UI Requirements

Not applicable. Level Progression owns no UI screens and renders no widgets. Its data is consumed by downstream UI systems:
- **Level Select UI**: reads `IsLocked(level_id)`, `IsBreather(level_id)`, `best_stars[level_id]`
- **Level Complete UI**: subscribes to `LevelCompleted`; emits `coin_reward_granted`, `next_level_requested`, `retry_requested`, `menu_requested`
- **Main Menu UI**: receives routing signal when `menu_requested` is processed in LOAD_PENDING

UI layout and interaction specifications belong in those systems' GDDs.

## Acceptance Criteria

| ID | Level | Criterion |
|---|---|---|
| AC-01 | BLOCKING | On first launch with no save data, `current_level_id` equals 1 and `best_stars` for all level records returns 0. |
| AC-02 | BLOCKING | On cold start, LP reads `current_level_id` from Save & Persistence before issuing any `load_level` command, and the first `load_level` call carries exactly that `current_level_id` as its argument. |
| AC-03 | BLOCKING | On cold start, LP transitions from IDLE to LEVEL_LOADING immediately upon issuing `load_level`, without waiting for a `level_unloaded` event from GSM. |
| AC-04 | BLOCKING | LP transitions from LEVEL_LOADING to LEVEL_ACTIVE on receipt of `level_loaded`, and issues no `load_level` or `exit_level` command while in LEVEL_ACTIVE. |
| AC-05 | BLOCKING | LP transitions from LEVEL_ACTIVE to COMPLETION_FLOW on receipt of `level_complete`, and does not transition on any other event received while in LEVEL_ACTIVE. |
| AC-06 | BLOCKING | LP transitions from COMPLETION_FLOW to LOAD_PENDING only after a navigation event (`next_level_requested`, `retry_requested`, or `menu_requested`) is received; it does not transition on `level_complete` or any coin event. |
| AC-07 | BLOCKING | LP transitions from LOAD_PENDING to LEVEL_LOADING (next or retry) or IDLE (menu) only after `level_unloaded` is received from GSM; it issues no `load_level` before that confirmation. |
| AC-08 | BLOCKING | When `next_level_requested` is received in COMPLETION_FLOW and `GetLevel(N+1)` returns success, LP issues `exit_level`, advances `current_level_id` to N+1 upon receiving `level_unloaded`, then issues `load_level(N+1)`. |
| AC-09 | BLOCKING | When `next_level_requested` is received in COMPLETION_FLOW and `GetLevel(N+1)` returns NOT_FOUND, LP emits `next_level_unavailable`, issues no `exit_level`, and remains in COMPLETION_FLOW. |
| AC-10 | BLOCKING | When `retry_requested` is received in COMPLETION_FLOW, LP issues `exit_level`, then on `level_unloaded` issues `load_level(current_level_id)` with the same level ID; no completion record is written, `coin_balance` is not mutated, and `LevelCompleted` is not emitted. |
| AC-11 | BLOCKING | When `menu_requested` is received in COMPLETION_FLOW, LP issues `exit_level`; on `level_unloaded` it routes to the main menu and enters IDLE without issuing any `load_level`. |
| AC-12 | BLOCKING | `is_locked(level_id)` returns `true` for any `level_id` strictly greater than `current_level_id`, and `false` for any `level_id` ≤ `current_level_id`. Verified by querying at `current_level_id − 1`, `current_level_id`, and `current_level_id + 1`. |
| AC-13 | BLOCKING | `IsLocked(current_level_id)` returns `false`; the Level Select UI must not receive a locked state for the player's current level. |
| AC-14 | BLOCKING | `IsBreather` returns `true` for every `level_id` that is a multiple of 10 (levels 10, 20 … 200) and `false` for all others. Verified by querying levels 9, 10, 11, 19, 20, 21, and 200. |
| AC-15 | BLOCKING | On completion of level N with `stars_earned = S`, `best_stars[N]` is updated to `max(stored_best_stars, S)`. Verified by: (a) first completion with 2 stars → expect 2; (b) replay with 1 star → expect 2; (c) replay with 3 stars → expect 3. |
| AC-16 | BLOCKING | `best_stars` for a completed level never decreases across any number of replays; a level with stored `best_stars = 3` retains 3 regardless of stars earned on subsequent plays. |
| AC-17 | BLOCKING | `completion_version` is written exactly once per level — when `best_stars` transitions from 0 to ≥ 1 — and is never overwritten on subsequent completions of the same level. |
| AC-18 | BLOCKING | The write of `best_stars[N]` and the advance of `current_level_id` to N+1 are atomic: after a simulated mid-write crash and relaunch, either both values are at their post-completion state or both remain at their pre-completion state; no partial write is observable. |
| AC-19 | BLOCKING | `LevelCompleted` is emitted exactly once per level completion in COMPLETION_FLOW; the four fields carry: `stars` = star rating from Level Complete UI; `level_id` = `current_level_id` at completion; `move_count` = value from the `level_complete` GSM payload; `par_moves` = `GetLevel(level_id).par_moves` from LDS. |
| AC-20 | BLOCKING | `LevelCompleted` is not emitted when `retry_requested` is processed; not emitted on app relaunch; not emitted when a duplicate `level_complete` event is discarded. |
| AC-21 | BLOCKING | On receipt of `coin_reward_granted(amount)` in COMPLETION_FLOW, LP calls `CE.AddCoins(amount, level_id, EarnSource.Base)` with the `level_id` from the active `LevelCompleted` event context. LP applies no coin mutation of its own — `LP.GetCoinBalance()` reflects the result of `CE.GetCoinBalance()` after the delegation completes. Verified: LP's internal state contains no `coin_balance` member variable. *(Integration test — requires CE. File: `tests/integration/coin-economy-lp/`)* |
| AC-22 | BLOCKING | On receipt of `coin_bonus_granted(amount)` in COMPLETION_FLOW, LP calls `CE.AddCoins(amount, level_id, EarnSource.AdBonus)` with the correct `level_id`. LP applies no coin mutation of its own. Verified: `EarnSource.AdBonus` is passed (not `EarnSource.Base`) — the distinction is required for CE-12 idempotency guard behavior. *(Integration test — requires CE. File: `tests/integration/coin-economy-lp/`)* |
| AC-23 | BLOCKING | `coin_reward_granted` or `coin_bonus_granted` received outside COMPLETION_FLOW (in LOAD_PENDING, LEVEL_ACTIVE, LEVEL_LOADING, or IDLE) causes no mutation of `coin_balance`; `GetCoinBalance()` returns the same value before and after the out-of-state event. |
| AC-24 | BLOCKING | On `session_load_failed(LEVEL_DATA_UNAVAILABLE)`, LP retries `load_level` after exactly `load_failed_retry_delay_ms` (default 500ms), up to `load_failed_max_retries` (default 3) times; on exhaustion LP shows error UI and does not issue a further `load_level` automatically. |
| AC-25 | BLOCKING | On `session_load_failed(LEVEL_RECORD_ERROR)` with `current_level_id > 1`, LP silently falls back to `current_level_id − 1`, issues `load_level(current_level_id − 1)`, and does not show error UI. |
| AC-26 | BLOCKING | On `session_load_failed(LEVEL_RECORD_ERROR)` with `current_level_id == 1`, LP shows error UI and does not issue a `load_level` or attempt a step-back. |
| AC-27 | BLOCKING | On `session_load_failed(INVARIANT_VIOLATION)`, LP applies the same step-back rule as LEVEL_RECORD_ERROR and additionally emits an analytics event containing the corrupt `level_id`. |
| AC-28 | BLOCKING | On `session_load_failed(INSTANTIATION_ERROR)`, LP waits `instantiation_retry_delay_ms` (default 1000ms) and retries `load_level` exactly once; if the second attempt also fails LP shows error UI and issues no further `load_level`. |
| AC-29 | BLOCKING | On app relaunch when `current_level_id` references a level not found in the catalogue, LP attempts `GetLevel(highest_completed_level + 1)`; if that is also NOT_FOUND, LP issues `load_level(1)` and enters LEVEL_LOADING for level 1. |
| AC-30 | BLOCKING | On app relaunch with a mid-session level in progress (level N loaded, `level_complete` not yet received before kill), LP loads `current_level_id` (level N); Level Complete UI is not shown and `LevelCompleted` is not emitted. |
| AC-31 | BLOCKING | When two or more navigation events arrive while LP is in LEVEL_LOADING, only the first received event is processed when `level_loaded` fires; all subsequent queued events are discarded. Verified by queuing two distinct events and confirming only the first is acted upon. |
| AC-32 | BLOCKING | A `level_complete` event received while LP is in LEVEL_LOADING is discarded and not queued; LP logs a warning and no state machine transition occurs. |
| AC-33 | BLOCKING | A `level_complete` event whose `level_id` does not equal `current_level_id` is discarded; no record is written, `coin_balance` is not mutated, `LevelCompleted` is not emitted, and LP logs a contract violation warning with both received and expected IDs. |
| AC-34 | BLOCKING | A `level_complete` event with `stars_earned = 0` is discarded; no completion record is written, `current_level_id` is not advanced, and LP logs a contract violation warning with the received payload. |
| AC-35 | ADVISORY | `LP.GetCoinBalance()` contains only a delegation to `CE.GetCoinBalance()` — LP holds no independent `coin_balance` field. The returned value is never negative and matches `CE.GetCoinBalance()` exactly. |
| AC-36 | ADVISORY | `IsBreather` is exposed as a callable method on LP (not computed inline by Level Select UI); Level Select UI renders the breather indicator on level 10's tile and no indicator on level 11, confirmed by manual inspection of the Level Select screen. |
| AC-37 | ADVISORY | On `level_unloaded` received while LP is in IDLE (unexpected GSM teardown), LP logs a warning, emits `unexpected_level_unloaded(lp_state: IDLE)` as an analytics event, and does not change state; confirmed by verifying LP remains in IDLE and no `load_level` is issued. |

**Summary: 34 BLOCKING / 3 ADVISORY**

## Open Questions

**OQ-01 — RESOLVED: LP provisional coin ownership transferred to Coin Economy**
Coin Economy GDD is now authored. LP's `coin_balance` field is removed. `GetCoinBalance()` delegates to `CE.GetCoinBalance()`. Coin mutation calls delegate to `CE.AddCoins()` per CE-02. AC-21, AC-22, AC-35 updated accordingly. Cross-GDD LP-01 and LP-02 from CE GDD are satisfied by this update.

**OQ-02 — Save & Persistence GDD: atomic write requirement (EC-16)**
AC-18 requires that `best_stars[N]` and `current_level_id` advance to N+1 are written atomically. Save & Persistence must support this as a single transaction or provide a write-group mechanism. Resolve in Save & Persistence GDD. *Priority: before Save & Persistence GDD is designed.*

**OQ-03 — Save & Persistence GDD: `completion_version` format lock**
`completion_version` is defined here as `YYYY.MM` (e.g., `2026.04`). The Save & Persistence GDD must use this exact format as its schema. If the format changes, this GDD must be updated. *Priority: before Save & Persistence GDD is designed.*

**OQ-04 — Coin Economy GDD: `LevelCompleted` event consumer**
`LevelCompleted(stars, level_id, move_count, par_moves)` is emitted by LP as the event/callback boundary. The Coin Economy GDD must formally specify which fields it consumes and what tier-based or efficiency-based award logic it applies. *Priority: before Coin Economy GDD is designed.*

**OQ-05 — Daily Challenge System: LP integration scope**
The Daily Challenge System is listed as a downstream dependent (soft dependency). The integration surface — whether Daily Challenge re-uses LP's `load_level`/`exit_level` cycle or owns its own session lifecycle — is unresolved. Resolve when Daily Challenge System GDD is authored. *Priority: before Daily Challenge GDD is designed.*

**OQ-06 — Level catalogue cap and OQ-02 interaction**
EC-21 states `current_level_id` may not exceed `max_authored_level`. Where `max_authored_level` is stored (LDS catalogue metadata or LP config) and how LP reads it is unresolved. Must be resolved before or during the Level Data System GDD implementation sprint.

**OQ-07 — Tutorial System: level override protocol**
The Tutorial System (soft downstream dependent) may need to inject a non-linear level sequence during onboarding (e.g., force level 1 even if `current_level_id > 1`). If Tutorial System overrides LP's load-level call, it must do so through a defined interface rather than mutating `current_level_id` directly. Resolve when Tutorial System GDD is authored.
