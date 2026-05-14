# In-Game HUD

> **Status**: In Design
> **Author**: Design session + game-designer + ux-designer + art-director agents
> **Last Updated**: 2026-04-19
> **Implements Pillar**: Flow Over Friction, Every Pixel Earns Its Place, Respect the Session

## Overview

The In-Game HUD is BoltSort's real-time game overlay — a persistent screen layer that displays live puzzle state and exposes the two direct player controls during a level: undo and hint. It owns four display responsibilities: the move counter (the running tally of committed moves the player has made), the undo button (the single-tap escape hatch that reverses the last committed bolt placement), the hint button (which triggers the Hint System's optimal-next-move suggestion at a coin cost), and the coin balance (the running total that tells the player where they stand relative to their next skin unlock). Each element earns its place on the pillar test — the move counter is the player's performance signal, the undo button is the game's commitment to no punishment, the hint button is the last escape valve before a deadlock, and the coin balance is the perpetual gentle pull of the reward loop. The HUD data layer subscribes to Game State Manager events: `level_loaded` resets the move counter and locks the undo button at zero; `board_state_changed` updates the move counter and refreshes the undo button's enabled state; `level_complete` freezes the counter and disables both buttons; `session_load_failed` surfaces an error state. The HUD also subscribes to `animation_complete(sequence_id: int32)` from the Animation System to re-enable the undo button precisely when MOVE_EXECUTING exits — not before. The HUD emits a single event: `undo_requested`, received by the Game State Manager. Implementation: UGUI Canvas, Screen Space - Overlay, using `Screen.safeArea` RectTransform anchoring to avoid hardware notch cutouts on iOS and Android.

## Player Fantasy

I'm at the console of a calm, humming machine. The counter marks each committed move the way an engineer watches a gauge — not a score, not a judgment, just an honest readout. The undo glyph rests nearby like a safety valve: I know it's there, and that knowledge makes me bolder. In flow, the whole panel retreats to the edge of my attention — I don't read it so much as sense it, the way a pilot senses altitude without looking down. When my thumb drifts toward the undo glyph, it's already ready or already dimmed, as if the machine anticipated my next move. The HUD doesn't speak. It breathes alongside the work, reporting in without demanding to be heard.

*Primary pillars: Flow Over Friction, Every Pixel Earns Its Place, The Machine Must Sing*
*MDA target: Submission (2), Sensation (1)*

## Detailed Design

### Core Rules

**Move Counter**
- Displays `move_count` as an integer; no leading zeros, no units
- Increments on `board_state_changed` when `delta_move_count > 0`
- Decrements on `board_state_changed` when `delta_move_count < 0` (undo reflects current committed state, not total history)
- Minimum displayed value: 0 (never negative)
- Resets to 0 on `level_loaded`
- Frozen (no further updates) on `level_complete`

**Undo Button**
- Enabled when: `undo_stack_depth > 0` AND system state is IDLE
- Disabled when: `undo_stack_depth == 0` OR system state is MOVE_EXECUTING OR `level_complete` fired
- Tap: disabled immediately (optimistic lock), then emits `undo_requested` to GSM
- Remains disabled until `animation_complete(sequence_id)` received matching the in-flight sequence
- If a second `board_state_changed` fires before `animation_complete`, the in-flight sequence_id takes precedence; undo re-evaluates enabled state only after that sequence resolves
- HUD does not perform the undo; it only signals and mirrors state

**Hint Button**
- States: ENABLED → HINT_PROCESSING → ENABLED or DISABLED
- ENABLED when: `coin_balance >= hint_cost` AND system state is IDLE AND level is active
- Tap: enters HINT_PROCESSING immediately — button locked, spinner/pulse visual active
- Remains in HINT_PROCESSING until `hint_result` received from Hint System
- On `hint_result(HINT_AVAILABLE)`: returns to ENABLED (or DISABLED if coin_balance now < hint_cost)
- On `hint_result(HINT_UNAVAILABLE)`: returns to ENABLED; no coin deducted
- Coin deduction is Hint System's responsibility; HUD reads updated balance from `coin_balance_changed`
- On `level_complete`: disabled permanently for that session

**Pity Grant Counter (CE-13)**
- Tracks consecutive 0-star completions on the current level; `pity_attempts` is not persisted across app sessions
- HUD caches `active_level_id` on each `level_loaded`
- On `level_complete(par_moves)`: compute `star_rating` via F-05; if `star_rating == 0`, increment `pity_attempts`; else reset `pity_attempts = 0`
- On `level_loaded(level_id)`: if `level_id != active_level_id` (level change), reset `pity_attempts = 0` and update `active_level_id`; if `level_id == active_level_id` (retry), keep current `pity_attempts` and update `active_level_id`
- When `pity_attempts >= pity_grant_attempt_threshold` (default 5) AND `ICoinEconomy.GetBalance() < hint_cost`: call `CE.AddCoins(50, level_id = -1, EarnSource.PityGrant)`; then reset `pity_attempts = 0`
- Pity grant fires at most once per threshold crossing; counter resets immediately after the grant call

**Coin Balance**
- Displays current `coin_balance` (integer); always floored, no fractional values
- Updates on `coin_balance_changed(new_balance, delta)`
- Animates on change: +N pulses green, −N pulses amber (color-blind safe — shape/icon secondary cue)
- Animation duration capped at 300ms; runs fire-and-forget and never blocks input or event processing

### States and Transitions

```
INACTIVE ──level_loaded──> IDLE
IDLE ──hint tap──> HINT_PROCESSING
HINT_PROCESSING ──hint_result──> IDLE
IDLE / HINT_PROCESSING ──level_complete──> FROZEN
FROZEN ──level_loaded──> IDLE   (next level / retry)
```

- **Undo button uses an independent optimistic lock** — not a HUD FSM state. On undo tap: button disabled immediately. Re-enabled on `animation_complete(seq_id)`. The HUD remains in IDLE during undo animation execution; MOVE_EXECUTING is GSM state, not HUD state.
- HINT_PROCESSING is HUD-local state; all other HUD elements remain interactive
- In FROZEN: all buttons disabled, counter locked, coin display still live (updates allowed)

### Interactions with Other Systems

| Event (In) | Source | HUD Action |
|---|---|---|
| `level_loaded(level_id: int)` | GSM | Reset move counter to 0; undo → disabled; hint → evaluate F-03 (requires initial coin balance — see Blocker 4 resolution); state → IDLE; cache `active_level_id`; reset `pity_attempts` if level changed |
| `board_state_changed(delta_move_count)` | GSM | Adjust counter by delta; refresh undo enabled state |
| `animation_complete(seq_id)` | Animation System | Re-enable undo if IDLE and stack > 0 |
| `level_complete(par_moves: int)` | GSM | Freeze counter; disable undo + hint; compute star_rating (F-05); evaluate pity grant counter |
| `coin_balance_changed(new_balance, delta)` | Coin Economy | Update display; fire 300ms pulse animation (non-blocking) |
| `hint_result(status)` | Hint System | Exit HINT_PROCESSING; re-evaluate hint enabled state |
| `session_load_failed` | GSM | Show error overlay; disable all interactive elements |

| Event (Out) | Receiver | Trigger |
|---|---|---|
| `undo_requested` | GSM | Undo button tap while IDLE + stack > 0 |
| `hint_requested` | Hint System | Hint button tap while ENABLED |

## Formulas

**F-01 — Move Counter**
```
move_count = move_count + delta_move_count
move_count = max(0, move_count)
```
- `delta_move_count`: integer from `board_state_changed`; positive on commit, negative on undo
- Range: [0, ∞) — no upper cap enforced by HUD

**F-02 — Undo Button Enabled**
```
undo_enabled = (undo_stack_depth > 0) AND (hud_state == IDLE)
```
- `undo_stack_depth`: integer provided by GSM via `board_state_changed` payload
- Evaluated on every `board_state_changed` and every state transition

**F-03 — Hint Button Enabled**
```
hint_enabled = (coin_balance >= hint_cost) AND (hud_state == IDLE)
```
- `hint_cost`: tuning knob (see Section G); read from config, not hardcoded
- Evaluated on `level_loaded`, `coin_balance_changed`, and `hint_result`
- `level_active` is implicit: IDLE only exists after `level_loaded`, so a third condition is redundant (OQ-05 resolved)

**F-04 — Coin Pulse Duration**
```
pulse_duration_ms = min(300, coin_pulse_duration_ms)
```
- `coin_pulse_duration_ms` = 300 (default tuning knob); cap enforces non-blocking guarantee even if knob is set above 300
- Fire-and-forget; if a second `coin_balance_changed` fires mid-animation, running animation is interrupted and restarted from current display value

**F-05 — Star Rating at Level Complete**
```
star_rating = StarRating(move_count, par_moves)
```
- `move_count`: HUD's internally tracked value at the moment `level_complete` fires
- `par_moves`: integer carried in `level_complete` payload from GSM
- `StarRating()` is the canonical formula defined in the Coin Economy GDD — **do not duplicate it here**; reference and call the shared implementation
- HUD uses only `star_rating == 0` to evaluate the pity grant counter; full star display is Level Complete UI's responsibility

## Edge Cases

**E-01 — `animation_complete` arrives before `board_state_changed`**
Sequence_id from `animation_complete` is buffered. When `board_state_changed` fires, undo enabled state is re-evaluated immediately using the buffered signal. Buffer holds only the latest sequence_id.

**E-02 — Rapid undo taps (double-tap race)**
Undo button is disabled on first tap (optimistic lock). OS touch events arriving before the disable frame are swallowed. No second `undo_requested` can be emitted until `animation_complete` re-enables the button.

**E-03 — `hint_result` never arrives (Hint System timeout)**
Hint button remains in HINT_PROCESSING indefinitely unless a timeout is enforced. HUD waits `hint_timeout_ms` (tuning knob); on expiry, exits HINT_PROCESSING, re-enables hint button, no coin deducted. Timeout is HUD's responsibility to enforce via coroutine.

**E-04 — `coin_balance_changed` fires during HINT_PROCESSING**
Coin display updates normally (fire-and-forget pulse). Hint enabled state is not re-evaluated until `hint_result` is received and HINT_PROCESSING exits.

**E-05 — `level_complete` fires during HINT_PROCESSING**
HUD transitions to FROZEN immediately. In-flight hint request is abandoned; hint button stays disabled. No coin re-credit — Hint System is responsible for its own transaction integrity.

**E-06 — `level_complete` fires during MOVE_EXECUTING**
HUD transitions to FROZEN. Pending `animation_complete` is ignored — counter and buttons remain frozen. No further state transitions until `level_loaded`.

**E-07 — coin_balance drops below hint_cost mid-level**
`coin_balance_changed` triggers re-evaluation of F-03. Hint button disables in IDLE state. If currently HINT_PROCESSING, remains locked until `hint_result`; then F-03 is evaluated and button stays disabled.

**E-08 — `session_load_failed` at startup**
HUD never receives `level_loaded`. Remains in INACTIVE. Error overlay rendered; all buttons disabled. No counter displayed.

**E-09 — undo_stack_depth = 0 after undo resolves**
After `animation_complete` fires: F-02 evaluates to false; undo button remains disabled. Counter reflects decremented value from `board_state_changed`.

**E-10 — Coin animation interrupted by rapid balance changes**
Each `coin_balance_changed` restarts the 300ms pulse from current displayed value toward new value. No queuing; the last received value always wins.

**E-11 — `animation_complete` arrives after `level_complete`**
Signal is ignored. HUD remains in FROZEN; no state re-evaluation or button re-enable occurs.

**E-12 — Pity grant threshold reached but coin balance >= hint_cost**
The pity grant condition requires `ICoinEconomy.GetBalance() < hint_cost`. If `pity_attempts >= threshold` but the balance is sufficient, no grant fires and the counter resets to 0 anyway. The threshold crossing is consumed regardless — do not re-fire when balance later drops below hint_cost.

**E-13 — Pity grant fires mid-HINT_PROCESSING**
If `pity_attempts` reaches threshold on a `level_complete` that occurs during HINT_PROCESSING (E-05 covers this state), the HUD is already in FROZEN. The pity grant check runs after transitioning to FROZEN. The coin balance update from the grant arrives via `coin_balance_changed`, which the coin display processes normally in FROZEN (coin display stays live).

**E-14 — Retry vs. level change: pity counter behavior on `level_loaded`**
On retry (same `level_id` as previous `active_level_id`): `pity_attempts` is preserved — the player is still failing the same level. On level change (different `level_id`): `pity_attempts` resets to 0. Design intent: pity grant is for players genuinely stuck on one level; moving forward resets the slate.

## Dependencies

**Systems this HUD depends on (inbound)**

| System | Dependency | Contract |
|---|---|---|
| Game State Manager | `level_loaded(level_id: int)`, `board_state_changed(delta_move_count, undo_stack_depth)`, `level_complete(par_moves: int)`, `session_load_failed` | GSM must include `undo_stack_depth` and `delta_move_count` in `board_state_changed` payload; `level_complete` must include `par_moves`; `level_loaded` must include `level_id`. GSM reads `par_moves` from Level Data System and includes it in the `level_complete` payload — HUD does not call Level Data System directly. |
| Animation System | `animation_complete(sequence_id: int64)` | Signal name and signature locked (see Animation System GDD) |
| Coin Economy | `coin_balance_changed(new_balance: int, delta: int)` | Must fire on every balance mutation, including hint deductions. `new_balance` is the full post-mutation balance (not a delta); the HUD coin display must show `new_balance`, not compute it from prior state + delta. **Initial balance**: HUD calls `ICoinEconomy.GetBalance()` directly at `level_loaded` time to seed the coin display and evaluate F-03; `coin_balance_changed` handles all subsequent updates (OQ-02 resolved). |
| Hint System | `hint_result(status: HINT_AVAILABLE \| HINT_UNAVAILABLE)` | Must always respond; HUD enforces `hint_timeout_ms` fallback |
| Save & Persistence | Initial `coin_balance` on `level_loaded` | HUD reads balance from Coin Economy, not directly from save; Save & Persistence must restore economy state before `level_loaded` fires |

*[Cross-GDD HUD-01 — Resolved 2026-04-28]: Pity grant counter fully implemented in this GDD: counter logic in Detailed Design (Pity Grant Counter section), formula F-05, edge cases E-12–14, and acceptance criteria AC-30–34. Obligation from Coin Economy GDD CE-13 is satisfied.*

**Systems that depend on this HUD (outbound)**

| System | Dependency | Contract |
|---|---|---|
| Game State Manager | `undo_requested` | GSM must process within one frame; HUD has already locked the button |
| Hint System | `hint_requested` | Hint System must always emit `hint_result`; no silent drops |

**Platform dependency**

- Unity UGUI Canvas (Screen Space - Overlay); `Screen.safeArea` RectTransform anchoring required for iOS notch and Android cutout compliance — no alternative layout path

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `hint_cost` | 50 | — | Authoritative value defined in Coin Economy GDD (`design/gdd/coin-economy.md`, CE-09). Canonical value: 50 coins (safe range [25, 100]). HUD reads from CE config; does not own a default. Ownership transfers to Hint System GDD on authoring. *Cross-GDD HUD-02 — Resolved 2026-05-08 per Pass 8 design review.* |
| `hint_timeout_ms` | 5000 | [2000, 10000] | How long HUD waits for `hint_result` before forcing exit from HINT_PROCESSING |
| `coin_pulse_duration_ms` | 300 | [100, 300] | Duration of coin balance change animation; capped at 300 to preserve non-blocking guarantee |
| `coin_pulse_color_positive` | #4CAF50 (green) | — | Color of pulse on coin gain; must pass WCAG AA contrast on background |
| `coin_pulse_color_negative` | #FF9800 (amber) | — | Color of pulse on coin spend; must pass WCAG AA contrast on background; must be distinguishable from positive without relying on hue alone |
| `undo_button_lock_frame_budget` | 1 frame | [1, 3] | Frames after tap before optimistic lock takes effect; 1 is ideal; raise only if touch latency issues observed on low-end devices |

**Notes**
- `hint_cost` is read from external config at `level_loaded`; never hardcoded
- Color knobs are art/accessibility director decisions; engineering must not override without sign-off
- `hint_timeout_ms` must be greater than the Hint System's own internal computation budget; coordinate with Hint System GDD when authored

## Visual/Audio Requirements

**Gate: UX Designer + Art Director spec required before UI implementation sprint.**
Pending `design/ux/in-game-hud.md` for: element sizing, icon/glyph specs, HINT_PROCESSING visual treatment (OQ-03), typography, animation curves.
Audio cue assignments pending Audio System GDD cross-reference.

## UI Requirements

**Gate: UX Designer spec required before UI implementation sprint.**
Pending `design/ux/in-game-hud.md` for: element layout/positioning, safe-area anchoring diagram, error overlay content and dismiss behavior (OQ-06).
Platform constraints already captured in AC-25 through AC-27.

## Acceptance Criteria

**Move Counter**
- AC-01 [BLOCKING] Counter displays 0 on `level_loaded`
- AC-02 [BLOCKING] Counter increments by `delta_move_count` on each committed move
- AC-03 [BLOCKING] Counter decrements by `|delta_move_count|` on undo; never displays a negative value
- AC-04 [BLOCKING] Counter does not change after `level_complete`

**Undo Button**
- AC-05 [BLOCKING] Undo button is disabled on `level_loaded` (stack depth = 0)
- AC-06 [BLOCKING] Undo button enables after first committed move (stack depth = 1)
- AC-07 [BLOCKING] Undo button disables immediately on tap before next frame renders
- AC-08 [BLOCKING] Undo button remains disabled until `animation_complete(sequence_id)` is received
- AC-09 [BLOCKING] A second tap during MOVE_EXECUTING emits no `undo_requested` event
- AC-10 [BLOCKING] Undo button stays disabled after undo resolves if stack depth = 0
- AC-11 [BLOCKING] Undo button disabled on `level_complete`; does not re-enable on subsequent `animation_complete`

**Hint Button**
- AC-12 [BLOCKING] Hint button disabled on `level_loaded` when `coin_balance < hint_cost`
- AC-13 [BLOCKING] Hint button enabled on `level_loaded` when `coin_balance >= hint_cost`
- AC-14 [BLOCKING] Hint button enters HINT_PROCESSING visual state immediately on tap
- AC-15 [BLOCKING] Hint button remains locked during HINT_PROCESSING; no second `hint_requested` emitted
- AC-16 [BLOCKING] On `hint_result(HINT_UNAVAILABLE)`: button exits HINT_PROCESSING, no coin deducted, button re-enables
- AC-17 [BLOCKING] On `hint_result(HINT_AVAILABLE)`: button exits HINT_PROCESSING, re-evaluates F-03
- AC-18 [BLOCKING] On `hint_timeout_ms` expiry with no `hint_result`: button exits HINT_PROCESSING, re-enables, no coin deducted
- AC-19 [BLOCKING] Hint button disabled on `level_complete`

**Coin Balance**
- AC-20 [BLOCKING] Coin display updates on every `coin_balance_changed` event
- AC-21 [BLOCKING] Coin display never shows fractional values
- AC-22 [ADVISORY] Positive delta triggers green pulse; negative delta triggers amber pulse
- AC-23 [BLOCKING] Coin pulse completes within 300ms and does not block button input during animation
- AC-24 [ADVISORY] Rapid `coin_balance_changed` events restart animation from current displayed value; no queuing artifact

**Layout & Platform**
- AC-25 [BLOCKING] All HUD elements render within `Screen.safeArea` bounds on iOS notch devices
- AC-26 [BLOCKING] All HUD elements render within `Screen.safeArea` bounds on Android cutout devices
- AC-27 [BLOCKING] HUD renders on Screen Space - Overlay canvas; not affected by camera transforms

**Error State**
- AC-28 [BLOCKING] On `session_load_failed`: error overlay shown, all buttons disabled, counter not displayed *(Full test requires UX spec resolution of OQ-06 — error overlay content and dismiss behavior)*

**Frozen State**
- AC-29 [BLOCKING] Any tap input while HUD is in FROZEN state produces no visual response and emits no events

**Pity Grant Counter (CE-13)**
- AC-30 [BLOCKING] After `pity_grant_attempt_threshold` consecutive `level_complete` events with `star_rating == 0` on the same level, if `coin_balance < hint_cost`, `CE.AddCoins(50, level_id = -1, EarnSource.PityGrant)` is called exactly once and `pity_attempts` resets to 0
- AC-35 [BLOCKING] GIVEN CE emits `coin_balance_changed(new_balance, delta, earn_source: EarnSource.PityGrant)`, WHEN the HUD receives this event, THEN HUD displays a differentiated player notification distinct from the normal coin pulse — e.g., "That one was tough — here's a hint on us" contextual toast or equivalent. The normal coin pulse animation alone is NOT sufficient; the `EarnSource.PityGrant` signal must trigger a separate notification path. Evidence: screenshot in `production/qa/evidence/ac-35-pity-notification.png` showing the notification text/visual, distinct from a normal earn pulse. *(Cross-GDD HUD-01 — Pass 8 obligation satisfied. Required by Coin Economy GDD CE-13.)*
- AC-31 [BLOCKING] Retrying the same level (same `level_id` on `level_loaded`) preserves the current `pity_attempts` value — it does not reset
- AC-32 [BLOCKING] Advancing to a new level (`level_id` changes on `level_loaded`) resets `pity_attempts` to 0
- AC-33 [BLOCKING] A non-zero-star `level_complete` on any attempt resets `pity_attempts` to 0; subsequent 0-star completions start a new count from 1
- AC-34 [ADVISORY] If `pity_attempts >= threshold` but `coin_balance >= hint_cost`, no grant fires; counter still resets to 0

## Open Questions

**OQ-01 — GSM payload contracts** *(Pre-sprint gate — must resolve before HUD implementation sprint)*
HUD requires: `board_state_changed(delta_move_count, undo_stack_depth)`, `level_complete(par_moves: int)`, `level_loaded(level_id: int)`. Confirm all three payloads are present in GSM GDD before sprint begins. Do not start the HUD implementation sprint until this is verified.

**OQ-02 — `level_loaded` payload and initial coin balance** *(Resolved)*
HUD calls `ICoinEconomy.GetBalance()` directly at `level_loaded` time to seed the coin display and evaluate F-03. No `initial_coin_balance` field needed in `level_loaded` payload.

**OQ-03 — HINT_PROCESSING visual treatment**
Spec says "spinner/pulse visual" — exact treatment (spinner overlay, button desaturate, animated ring) is unspecified. Assign to UX Designer + Art Director before UI implementation sprint.

**OQ-04 — `hint_timeout_ms` coordination with Hint System**
`hint_timeout_ms` default is 5000ms. Hint System GDD must specify its own max computation budget; HUD timeout must exceed it with margin. Resolve when Hint System GDD is authored.

**OQ-05 — `level_active` ownership in F-03** *(Resolved)*
IDLE state is sufficient. IDLE only exists after `level_loaded`, so `level_active` is always true in IDLE. The term has been removed from F-03.

**OQ-06 — Error overlay design**
`session_load_failed` triggers an error overlay, but its content, copy, and dismiss behavior are unspecified. Assign to UX Designer before UI sprint.

**OQ-07 — `hint_result` payload scope**
HUD only reads `status` from `hint_result`. Does the payload also carry the suggested move for the Hint System's visual overlay? Confirm payload shape in Hint System GDD — HUD must not break if extra fields are present.
