# Interaction Pattern Library

> **Status**: Committed
> **Author**: ux-design skill — 2026-05-12
> **Last Updated**: 2026-05-12
> **Template**: Interaction Pattern Library
> **Input Method**: Touch only (tap — no drag, no hold, no gamepad)
> **Platforms**: iOS + Android (portrait lock)
> **Accessibility Tier**: Standard (see `design/accessibility-requirements.md`)
> **Source Documents**: `design/gdd/in-game-hud.md`, `design/gdd/level-complete-ui.md`, `design/gdd/sort-mechanic.md`, `design/art/art-bible.md` (Sections 7.1–7.6), `design/gdd/coin-economy.md`, `.claude/docs/technical-preferences.md`

---

## Overview

BoltSort is a tap-only mobile sort puzzle in portrait orientation. Every interaction in the game reduces to a single gesture: **a tap**. This constraint is a feature — tap-only eliminates the motor barriers of drag, hold, and multi-touch, and it means every pattern in this library must be designed for a single finger, a short session, and a phone held in one hand.

All patterns in this library obey three global rules:

1. **Tap targets are never smaller than 48×48pt** (Android 48dp, iOS 44pt — the stricter value applies). Recommended minimum: 56×56pt. Hit area may exceed visual area via `RectTransform` padding.
2. **Disabled states never use color as the sole differentiator.** Disabled = opacity drop + glow 0.0. Shape remains visible. This satisfies the Standard accessibility tier's colorblind mode requirement.
3. **Every interactive element provides immediate feedback on tap-down** — not on tap-release. The bolt game requires sub-frame confirmation; delayed feedback reads as a missed tap.

**Color vocabulary used throughout this document:**
- `CHROME-01` `#0B0F14` — near-black background
- `CHROME-02` `#141C24` — HUD panel / overlay fill
- `CHROME-03` `#4DCFEF` — cyan interactive signal / primary stroke
- `CHROME-04` `#C8D8E8` — body text / static display values
- `CHROME-05` `#E8A030` — amber reward accent (Level Complete only)

---

## Pattern Catalog

| # | Pattern | Category | Used In |
|---|---------|----------|---------|
| 1 | [Two-Tap Move](#1-two-tap-move) | Input | Game board |
| 2 | [Optimistic Lock Button](#2-optimistic-lock-button) | Input / Feedback | In-Game HUD (undo) |
| 3 | [Multi-State Processing Button](#3-multi-state-processing-button) | Input / Feedback | In-Game HUD (hint) |
| 4 | [One-Tap Input Buffer](#4-one-tap-input-buffer) | Input | Sort Mechanic |
| 5 | [Back Gesture Cancel](#5-back-gesture-cancel) | Input | Game board (bolt selection) |
| 6 | [Button Press Visual Feedback](#6-button-press-visual-feedback) | Feedback | All interactive buttons |
| 7 | [Async Processing Indicator (Spinning Arc)](#7-async-processing-indicator-spinning-arc) | Feedback | In-Game HUD (hint), future loading states |
| 8 | [Disabled State (Non-Color)](#8-disabled-state-non-color) | Feedback | All buttons in any disabled condition |
| 9 | [System Freeze State](#9-system-freeze-state) | Feedback | In-Game HUD (FROZEN on level complete) |
| 10 | [Contextual Toast](#10-contextual-toast) | Feedback | HUD (pity grant), future system notices |
| 11 | [Event-Driven Counter](#11-event-driven-counter) | Data Display | In-Game HUD (move counter) |
| 12 | [Live Balance Display with Pulse](#12-live-balance-display-with-pulse) | Data Display | In-Game HUD (coin balance) |
| 13 | [Thumb Zone / Glance Zone Layout](#13-thumb-zone--glance-zone-layout) | Layout | In-Game HUD, all gameplay screens |
| 14 | [Sequential Reveal Animation](#14-sequential-reveal-animation) | Overlay / Flow | Level Complete UI (star cascade) |
| 15 | [Skippable Animation Overlay](#15-skippable-animation-overlay) | Overlay / Flow | Level Complete UI |
| 16 | [Primary / Secondary Navigation Hierarchy](#16-primary--secondary-navigation-hierarchy) | Overlay / Flow | Level Complete UI (Next / Retry / Menu) |
| 17 | [Opt-In Ad Offer](#17-opt-in-ad-offer) | Overlay / Flow | Level Complete UI |
| 18 | [Destructive Action Confirm](#18-destructive-action-confirm) | Modal / Safety | Pause Menu (restart, exit) |

---

## Patterns

---

### 1. Two-Tap Move

**Category**: Input
**Used In**: Game board (SortMechanic)

**Description**: The core game interaction. The player selects a bolt stack with a first tap, then selects a destination with a second tap. Selection and commitment are two distinct taps — never a single gesture. This intentional two-step prevents accidental moves, supports unlimited undo, and matches the considered-move design intent of the puzzle genre.

**Specification**:
- **Tap 1 — Select**: Player taps a bolt stack. Top bolt lifts to `LiftHeight` over 80ms (EaseOutCubic). SortMechanic enters `BOLT_SELECTED`.
- **Tap 2 — Valid destination**: Bolt travels to destination stack (80–300ms, distance-proportional, EaseInOutQuad) and settles (70ms + micro-bounce, EaseInQuint). `OnMoveCommitted` fires.
- **Tap 2 — Same stack (cancel)**: Bolt returns to source. `OnMoveCancelled` fires. SortMechanic returns to `IDLE`. No undo entry created.
- **Tap 2 — Invalid destination**: Horizontal rejection shake (100ms). `OnMoveRejected` fires. SortMechanic transitions `BOLT_SELECTED → INVALID_MOVE → IDLE`. No undo entry.
- **Visual selection state**: Lifted bolt holds at `LiftHeight`, glow ramps from 0.4 → 1.0 over the lift duration. Held bolt is the only board element at emission 1.0 during selection.
- **Audio**: `PlayBoltSettle(isValid)` fires at the visual arrive keyframe (after travel, before settle phase completes). `isValid = false` on rejection.

**Accessibility**:
- The lifted bolt distinguishes the selected state via position and glow intensity — not color. Colorblind players have the same selection signal as all other players.
- Tap targets are bolt stack colliders sized to the minimum tap target (Pattern 7 — Minimum Tap Target is embedded in board layout constraints, ADR-0013).

**When to Use**: All board-object move interactions.
**When NOT to Use**: Any interaction outside the game board. HUD buttons use direct-action patterns (Patterns 2, 3), not two-tap.

---

### 2. Optimistic Lock Button

**Category**: Input / Feedback
**Used In**: In-Game HUD (undo button)

**Description**: A button that disables itself immediately on tap, before the operation it triggers is confirmed as complete. It re-enables only when the async operation resolves — confirmed by a specific event carrying a matching sequence ID. This pattern prevents double-firing an action during an async execution window (e.g., undo during bolt animation).

**Specification**:
- **Enabled state**: Standard active appearance — CHROME-03 stroke, emission 0.4, full icon opacity.
- **Tap-down**: Apply Pattern 6 (Button Press Visual Feedback) — scale 94%, glow surge 1.0, 10% CHROME-02 fill. Simultaneous with input receipt.
- **Tap-up**: Button immediately transitions to Disabled appearance (Pattern 8) — emission 0.0, stroke 40% opacity, glow 0.0. `undo_requested` event fires.
- **Lock duration**: Button remains disabled until `animation_complete(sequence_id)` is received **and** the received `sequence_id` matches `_pendingSequenceId`. Stale `sequence_id` values (from a previous move) are discarded — button stays locked.
- **Re-enable condition**: `animation_complete(matching seqId)` received AND `undo_stack_depth > 0` AND HUD state is IDLE. If any condition fails, button remains disabled.
- **Override condition**: `level_complete` received — button stays disabled permanently for the session (enters System Freeze State, Pattern 9).

**Accessibility**:
- Disabled state uses Pattern 8 (opacity + glow, no color change) — colorblind safe.
- The button remains visually present while disabled (spatial information preserved). It is not hidden.
- Re-enable is confirmed by the visual transition back to active appearance — no additional notification required.

**When to Use**: Any button that triggers an async operation where a duplicate tap would cause incorrect game state. Specifically: any button whose action has a defined completion event carrying a sequence ID.
**When NOT to Use**: Buttons with synchronous, instantaneous outcomes. Navigation buttons (Next Level, Retry) are direct-action, not optimistic-lock — their outcomes are immediate.

---

### 3. Multi-State Processing Button

**Category**: Input / Feedback
**Used In**: In-Game HUD (hint button)

**Description**: A button with three distinct visual states — ENABLED, HINT_PROCESSING, and DISABLED — each communicating a different system condition. Unlike Pattern 2 (Optimistic Lock), the processing state here has an explicit visual indicator (spinning arc) because the wait is open-ended: the player must understand the system is working, not frozen.

**Specification**:

**ENABLED state**:
- CHROME-03 stroke, emission 0.4. Full icon opacity. Glow halo present at resting level.
- Entry condition: `coin_balance >= hint_cost` AND HUD state is IDLE AND level is active.

**Tap → HINT_PROCESSING**:
- Apply Pattern 6 (tap-down) on the same frame the tap is received.
- Immediately after: shift to HINT_PROCESSING appearance (do not wait for the operation to begin).
- HINT_PROCESSING appearance: emission → 0.2 (below idle 0.4; button reads as partially suppressed). Button interior gains 5% CHROME-02 fill (stroke-only → partially filled, communicating locked status).
- Spinning arc: 2dp stroke, CHROME-03 color, 90° arc segment, rotates clockwise, 1.0s/revolution, sine in/out easing. Arc runs continuously for the full HINT_PROCESSING duration (up to `hint_timeout_ms` = 5000ms default).
- `hint_requested` event fires.
- All other HUD elements remain fully interactive during HINT_PROCESSING (it is a button-local state, not a global freeze).

**HINT_PROCESSING → resolve**:
- On `hint_result` received (or timeout): arc stops. Button transitions over 100ms to either ENABLED or DISABLED appearance, depending on updated `coin_balance >= hint_cost`.
- No error indicator on timeout. Machine-voice: the button simply resolves. Silence is not failure.

**DISABLED state** (same as Pattern 8):
- Stroke opacity ~40%, glow 0.0, emission 0.0. Icon shape preserved. No color shift.
- Entry: `coin_balance < hint_cost` OR level inactive.

**Accessibility**:
- Three states distinguished by emission level (0.4 / 0.2 / 0.0) and motion (arc present only in PROCESSING). No color change between states.
- The spinning arc is a motion indicator — if `reduced_motion_mode` is ever enabled (future), the arc should be replaced by a static pulsing opacity (0.2 → 0.4 → 0.2, 1.0s cycle).

**When to Use**: Any button where the outcome of a tap requires a non-trivial async wait (network request, solver computation, ad load) and the wait duration is open-ended enough to require a "working" indicator.
**When NOT to Use**: Short deterministic async waits (<300ms). For those, Pattern 2 (Optimistic Lock) is sufficient — the wait is imperceptible, and an arc would be distracting.

---

### 4. One-Tap Input Buffer

**Category**: Input
**Used In**: Sort Mechanic (during MOVE_EXECUTING)

**Description**: During a bolt move animation (MOVE_EXECUTING), the system cannot process a new move. Rather than silently discarding taps during this window, the game buffers one tap. When the animation resolves, the buffered tap is replayed — producing a fluid double-tap experience where the second move begins as soon as the first completes.

**Specification**:
- **Buffer capacity**: Exactly one tap. If multiple taps arrive during MOVE_EXECUTING, only the most recent is retained. Buffer is overwritten on each new tap.
- **Buffer activation**: Any tap on any board target during MOVE_EXECUTING stores `(_pendingTap = true, _pendingTapStackIndex = stackIndex)`. No visual confirmation of buffering — the bolt animation already communicates "working."
- **Buffer replay**: On MOVE_EXECUTING → IDLE transition (normal `animation_complete` path), `ProcessPendingTap()` is called immediately. The buffered tap is replayed as if it had occurred in IDLE.
- **Buffer discard**:
  - On WIN path: `DiscardPendingTap()`. The level is complete — queued moves are irrelevant.
  - On watchdog path (`OnBoardRefreshForced`): `DiscardPendingTap()`. Board state has snapped; the buffered target may no longer be valid.

**Accessibility**:
- No additional accessibility considerations. The buffer is invisible — players with tremor or slower tap speed benefit from it because their follow-up tap is more likely to fall within the MOVE_EXECUTING window and still register.

**When to Use**: Any animation-gated interaction where the player has a logical "next move" ready and discarding it would feel punishing.
**When NOT to Use**: Non-game-board interactions. HUD buttons (undo, hint) are not buffered — they have their own locking patterns (2, 3).

---

### 5. Back Gesture Cancel

**Category**: Input
**Used In**: Sort Mechanic (BOLT_SELECTED state)

**Description**: The Android hardware back button / back gesture maps to a semantic "cancel" action when the player is mid-selection. This gives Android users a natural escape from the BOLT_SELECTED state without requiring a second tap on the source stack.

**Specification**:
- **Input**: `Keyboard.current.escapeKey.wasPressedThisFrame` (Input System Package).
- **Active states**: BOLT_SELECTED only. In all other Sort Mechanic states (IDLE, MOVE_EXECUTING, WIN), the back gesture is a no-op — the game does not exit.
- **Action**: `CancelHeldBolt()` — bolt snaps back to source, `OnMoveCancelled(src, colorId)` fires, SortMechanic returns to IDLE.
- **Platform note**: Works correctly on Android 13+ as long as the project does NOT opt into `android:enableOnBackInvokedCallback` in the AndroidManifest. If that flag is ever added (required for Android 16+ large-screen compliance), this pattern must migrate to `Application.onBackReceived`. Flag in release checklist.
- **iOS**: `escapeKey` does not fire on iOS (no hardware back button). No action taken. iOS users use the tap-same-stack cancel (Pattern 1) exclusively.

**Accessibility**:
- Provides a cancel path for Android users without requiring precise re-tapping of the source stack — relevant for users with tremor where hitting the exact source collider may be difficult.

**When to Use**: Any state where the player has "committed to a path" and should have an escape without needing to hit a specific target.
**When NOT to Use**: Do not map back gesture to level exit, menu navigation, or any destructive action. The back gesture in BoltSort means "cancel selection" only.

---

### 6. Button Press Visual Feedback

**Category**: Feedback
**Used In**: All interactive buttons (undo, hint, Next Level, Retry, Menu, Watch Ad, Skip)

**Description**: Immediate physical feedback on tap-down, before the action executes. Communicates "I received your tap" before any async operation begins. Because mobile touch has no physical click, this micro-animation is the only tactile substitute available.

**Specification**:
- **Tap-down** (fires on the same frame the touch input is received):
  - Scale: compress to **94%** of normal size, ease-in-cubic, **60ms**.
  - Glow: surge to **emission 1.0** instantaneously (0ms delay — human finger contact must feel immediate).
  - Fill: interior gains **10% CHROME-02** (`#141C24`) fill. Stroke-only buttons become slightly filled, communicating "pressed."
- **Tap-release**:
  - Scale: spring back from 94% → 100%, ease-out-cubic, **80ms**.
  - Glow: returns to the button's appropriate resting state (0.4 for ENABLED; stays at 1.0 briefly then resolves if the action triggers a state change).
  - Fill: interior fill clears on release.
- **Audio**: Bolt settle SFX (`PlayBoltSettle`) fires on board actions. For HUD buttons, a dedicated UI tap SFX fires on `UIVolume` AudioMixer bus (exact clip TBD in Audio System implementation sprint).

**Accessibility**:
- Scale + glow change provides a non-audio confirmation of tap receipt. Players who mute audio (common in casual mobile) still receive feedback.
- The scale change is visible at all button sizes ≥48pt.

**When to Use**: Every tappable element in the game. No button should lack this feedback.
**When NOT to Use**: Non-interactive display elements (counters, labels, coin display). A tappable-looking display element that does not respond to tap is a false affordance.

---

### 7. Async Processing Indicator (Spinning Arc)

**Category**: Feedback
**Used In**: In-Game HUD (hint button HINT_PROCESSING), future: any open-ended async wait

**Description**: A partial-circle arc that rotates continuously around a button to signal ongoing background work. Replaces static "loading" language with a machine-appropriate kinetic indicator. The arc runs for the full duration of the wait — the player knows the system is working, not frozen, as long as the arc is moving.

**Specification**:
- **Shape**: 90° arc segment (one quarter circle), centered on the button's bounding circle.
- **Stroke**: 2dp, CHROME-03 `#4DCFEF`.
- **Rotation**: Clockwise. Full revolution in **1.0 seconds**. Easing: sine in/out per revolution (accelerates into and decelerates out of each 360° cycle — not constant-speed, which reads as mechanical rather than intelligent).
- **Start**: Arc begins rotating on the same frame the button enters HINT_PROCESSING — no delay.
- **Stop**: Arc stops on `hint_result` received or timeout. Stopping is instantaneous — the arc does not decelerate or fade. It simply stops. The button then resolves (100ms fade to ENABLED or DISABLED).
- **Reduced motion alternative**: If `reduced_motion_mode` is implemented (Standard tier — planned): replace rotating arc with a static arc that pulses opacity (0.2 → 0.5 → 0.2, 1.0s cycle, sine easing).

**Accessibility**:
- The arc is CHROME-03 (cyan) against the button's CHROME-02 fill — a 4:1+ contrast ratio against the dark background. Passes WCAG AA for non-text graphical elements (3:1 minimum).
- The arc communicates "working" via motion alone — it does not carry color-only information (the arc is always CHROME-03; the absence of the arc means "not processing").

**When to Use**: Any async wait lasting more than ~500ms where the outcome is unknown. For short waits (<300ms), the Optimistic Lock (Pattern 2) is sufficient.
**When NOT to Use**: Full-screen loading states (those use a different pattern — TBD in Main Menu UX spec). The arc is a button-local indicator, not a screen-level one.

---

### 8. Disabled State (Non-Color)

**Category**: Feedback
**Used In**: All buttons in any disabled condition

**Description**: A standardized disabled visual that communicates "unavailable" without relying on color. Shape, position, and spatial grammar are fully preserved — the player can see where the button is and what it does, even when they cannot tap it. This is the project-wide standard for all disabled UI; no button may implement its own disabled appearance.

**Specification**:
- **Icon/label**: Stroke opacity drops to **~40% luminance** of the enabled stroke. Shape is fully preserved — silhouette, icon detail, and position are unchanged.
- **Glow**: **0.0** (halo completely suppressed).
- **Emission**: **0.0** (no self-illumination).
- **Fill**: No interior fill. Stroke-only appearance.
- **Color**: **No color change.** The icon remains CHROME-03 at reduced opacity — it does not shift to grey. Color-shift disabled states fail colorblind accessibility because the color difference is the only signal.
- **Hit area**: Disabled buttons must still have their full hit area active in the collider — the `Button` component's `interactable = false` state prevents the `onClick` from firing, but the collider should not be resized or hidden. This preserves layout stability.

**When to Use**: Any button that is temporarily or permanently unavailable:
- Undo button when `undo_stack_depth == 0`
- Hint button when `coin_balance < hint_cost`
- All HUD buttons during FROZEN state
- Navigation buttons during AD_PROCESSING

**When NOT to Use**: Do not hide buttons when they are unavailable. Hidden buttons create layout jumps and remove spatial information. Disabled appearance is always preferable to hidden.

---

### 9. System Freeze State

**Category**: Feedback
**Used In**: In-Game HUD (FROZEN on `level_complete`)

**Description**: A global HUD state in which all interactive elements are simultaneously suppressed, communicating that the player's session is over and a transition is imminent. Unlike Pattern 8 (per-button Disabled State), System Freeze is triggered by a single game event and applies to all interactive elements as a unit. One element — the coin balance display — remains live during Freeze to signal that rewards are still being counted.

**Specification**:
- **Trigger**: `level_complete` event received by HUD. Freeze applies immediately — before the board completion animation finishes.
- **All buttons**: Transition to **emission 0.0, stroke 15% opacity, glow 0.0** simultaneously. This is a more severe suppression than the standard Disabled State (Pattern 8) — FROZEN signals a session boundary, not just unavailability.
- **Move counter**: Holds its final value. Opacity drops to **CHROME-04 at 60%**. Static; no further updates.
- **Coin balance display**: Remains **fully live** and fully opaque. Continues to respond to `coin_balance_changed` events with its standard pulse animation (Pattern 12). This is the only element not suppressed — its liveness communicates "your coins are still arriving."
- **HUD panel**: Remains fully opaque and positioned. The layout does not change. The Level Complete UI overlay builds over the HUD without causing a layout jump.
- **Exit condition**: HUD remains FROZEN until `level_loaded` (next level or retry). On `level_loaded`, all elements reset to IDLE-appropriate states.

**Accessibility**:
- The simultaneous suppression of all buttons provides a clear boundary between "game in progress" and "game complete." No element reads as interactable during FROZEN.
- The coin display's continued liveness creates a visual focal point — the player's eye is drawn to the one moving element, which is also the most important post-completion signal (reward delivery).

**When to Use**: Any terminal game event that ends the player's ability to interact with the current game session and triggers a transition to a result or completion screen.
**When NOT to Use**: Mid-session loading states (e.g., waiting for a level to load). Those use a different pattern — the HUD is not yet active, so Freeze does not apply.

---

### 10. Contextual Toast

**Category**: Feedback
**Used In**: In-Game HUD (pity grant: "Hint restored."), future: low-bandwidth notices, system warnings

**Description**: A small, transient notification chip that appears in a fixed position above the interaction zone, delivers a short system-voice message, and auto-dismisses. Toasts are not alerts — they are whispers. They acknowledge something the system did on the player's behalf, without requiring acknowledgement or blocking interaction.

**Specification**:
- **Shape**: Rounded rectangle, **12dp corner radius**, full-width chip. Width: fits content with 16pt horizontal padding. Not full-screen-width.
- **Fill**: CHROME-02 `#141C24` at **85% opacity**. Semi-transparent — the board content behind is visible but de-emphasized.
- **Stroke**: CHROME-03 `#4DCFEF`, 1dp.
- **Typography**: IBM Plex Sans, Regular (400), CHROME-04 `#C8D8E8`. System-voice register — declarative, not exclamatory. No bold, no all-caps, no punctuation emphasis. Example: `"Hint restored."` not `"Hint Restored!"`.
- **Position**: Above the bottom button strip (between thumb zone top edge and board bottom edge), or at the base of the top HUD row. Never overlaps the board content zone or the interactive buttons.
- **Animation sequence**:
  - Fade in: **200ms**, linear.
  - Hold: **2000ms** (legibility window).
  - Fade out: **300ms**, linear.
  - Total: 2500ms from appearance to invisible.
- **Stacking**: Toasts do not stack. If a second toast fires while one is visible, the first fades out immediately (cut) and the second begins its 200ms fade-in.
- **Coin pulse relationship (pity grant)**: Toast appears first; the coin balance pulse (Pattern 12) fires after the toast begins its fade-out — sequential, not overlapping. This ensures the player reads the reason before seeing the balance change.

**Accessibility**:
- Toast text meets WCAG AA contrast (4.5:1) against CHROME-02 fill at 85% opacity over any board background.
- The 2000ms hold is the minimum for a player with slow reading speed (~5 characters/second) to read a 10-character message. Messages must not exceed 25 characters.
- Toasts are supplementary — they communicate something the coin display or bolt animation also communicates. If a toast is missed, the player loses no critical information.

**When to Use**: Background system events that the player didn't explicitly trigger but should be briefly aware of (pity grant, future: daily bonus, network reconnect).
**When NOT to Use**: Errors or warnings that require player action. Those need a modal or persistent indicator, not a self-dismissing toast.

---

### 11. Event-Driven Counter

**Category**: Data Display
**Used In**: In-Game HUD (move counter)

**Description**: A numeric display that updates in response to specific game events rather than polling game state each frame. The counter is authoritative — it reflects committed game state, not visual animation state. It updates on `board_state_changed` (commit or undo), not on `animation_complete` (which is a visual confirmation, not a state change).

**Specification**:
- **Font**: IBM Plex Mono, SemiBold (600). Tabular figures — digit glyphs are fixed-width so the counter does not reflow on increment. This is not a stylistic choice; non-tabular counters shift layout and violate the instrument-grade aesthetic.
- **Color**: CHROME-04 `#C8D8E8`. Never bold (700+). Bold reads as alert; the move counter reports, it does not warn.
- **Update trigger**: `board_state_changed(delta_move_count)` received. Counter adjusts by `delta_move_count` (positive on commit, negative on undo).
- **Update animation**: None. The counter updates instantaneously (cross-fade to new value, 0ms delay). A counting animation would be misleading — the new value is accurate immediately; animating to it implies the intermediate values have meaning.
- **Reset**: On `level_loaded`, counter resets to 0 instantly.
- **FROZEN behavior**: On `level_complete`, counter holds its final value at 60% CHROME-04 opacity (per System Freeze, Pattern 9). No further updates.
- **Range**: `[0, ∞)`. No upper cap displayed. The counter never shows a negative value.

**Accessibility**:
- Tabular figures prevent layout shifts that could confuse screen-reader users and players tracking the count peripherally.
- Pure CHROME-04 text on CHROME-02 panel background — contrast ratio ≥4.5:1 (WCAG AA).

**When to Use**: Any numeric value that represents committed game state and should be updated by events from the authoritative system (not derived locally).
**When NOT to Use**: Do not use this pattern for animated counters (e.g., counting up coin rewards during Level Complete). That is the Animated Counter variant — a separate pattern not yet formalized (flagged in Gaps).

---

### 12. Live Balance Display with Pulse

**Category**: Data Display
**Used In**: In-Game HUD (coin balance)

**Description**: A live numeric display with a short animation on change. The balance updates immediately to the new value (no counting animation), then plays a brief visual pulse to confirm the change was received. The pulse direction and amplitude communicate the sign of the delta without using warm/alarm colors.

**Specification**:
- **Font**: IBM Plex Sans, Regular (400). ~70% of move counter cap-height. Smaller, subordinate to the counter.
- **Color**: CHROME-04 `#C8D8E8` at rest.
- **Coin icon**: Stroked circle, diameter ~60% of bolt diameter. CHROME-03 `#4DCFEF` stroke, stroke-only (not filled — distinct from bolt form). Interior mark: thin horizontal slash or minimal "c" glyph, 1dp stroke, centered. If the mark is illegible at 16dp rendered size, use the slash variant.
- **Update**: `coin_balance_changed(new_balance, delta)` received. Numeral cross-fades to `new_balance` immediately (0ms — the number is correct at the moment of the event, not after animation).

**Positive delta (coins earned)**:
- Numeral: CHROME-04 → CHROME-03 `#4DCFEF` over 100ms, returns CHROME-04 over 200ms. "System event touched the display." Total: 300ms.
- Icon: scales +15% then returns, ease-in-out, over first 150ms. Total: 150ms (within the 300ms pulse window).
- No warm color (`#E8A030` amber is Level Complete only — never during gameplay).

**Negative delta (hint spend)**:
- Numeral: no color shift. Cross-fades to new value only.
- Icon: scales −5% briefly (mild deflation, 100ms). No expansion. Instrument-appropriate debit signal.
- No red, no amber, no alarm color.

**Rapid-fire events**: Each `coin_balance_changed` restarts the 300ms pulse from the current displayed value. This produces a visible flicker on multiple rapid changes — acceptable behavior per Coin Economy GDD (fire-and-forget restart). If the flicker is objectionable aesthetically, reduce `coin_pulse_duration_ms` to 150ms in the tuning knob.

**FROZEN exception**: Coin balance display remains live during System Freeze State (Pattern 9). It continues to respond to `coin_balance_changed` with the standard pulse animation. It is the only HUD element not suppressed during FROZEN.

**Accessibility**:
- Positive/negative delta communicated by icon scale direction (+15% vs −5%), not by color alone. Colorblind safe.
- 300ms pulse duration is within the non-blocking guarantee from HUD GDD (no animation blocks event processing).

**When to Use**: Any balance or resource that can change both up and down during a session and should give the player a non-intrusive confirmation of each change.
**When NOT to Use**: Do not use this pattern for one-directional resource displays (e.g., progress bars that only fill). Those should use a different display pattern appropriate to their semantic.

---

### 13. Thumb Zone / Glance Zone Layout

**Category**: Layout
**Used In**: In-Game HUD, all gameplay overlay screens

**Description**: A two-zone layout principle for all gameplay screens on portrait mobile. Interactive controls live at the bottom (thumb zone) where the player's thumb naturally rests during one-handed play. Display-only information lives at the top (glance zone) where it is visible without requiring interaction. No interactive element belongs in the glance zone; no decision-critical display belongs only in the thumb zone.

**Specification**:

```
┌─────────────────────────────────────┐
│  [COIN ●]         [MOVE: 12]        │  ← GLANCE ZONE
│                                     │     top of safe area + 16pt padding
│  ─────────────────────────────────  │     1px #1E2A38 hairline divider
│                                     │
│            BOARD AREA               │  ← CONTENT ZONE (visually dominant)
│                                     │
│  ─────────────────────────────────  │     1px #1E2A38 hairline divider
│  [UNDO ↩]                [HINT 💡]  │  ← THUMB ZONE
└─────────────────────────────────────┘     bottom of safe area + 16pt padding
```

- **Glance zone**: Safe area top inset + 16pt padding. Display-only. Coin balance chip (top-left), move counter (top-center). No top-right element — Gestalt isolation on the counter signals primary read.
- **Thumb zone**: Safe area bottom inset + 16pt padding. Interactive only. Undo (bottom-left), Hint (bottom-right). Full-width separation (~200pt gap on a 390pt screen) eliminates inter-button mis-tap risk.
- **Content zone**: Between the two hairline dividers. Visually dominant — the board fills this area. No HUD element intrudes into the content zone during normal play.
- **Dividers**: 1px `#1E2A38` hairlines at glance-zone bottom and thumb-zone top. The only drawn boundaries in the layout.
- **HUD panel**: CHROME-02 `#141C24` at 60–70% opacity behind all HUD elements. A dedicated background layer — text is never floating over live board content.
- **Safe area**: `Screen.safeArea` applied via `SafeAreaPanel` (ADR-0008). All zone measurements are from the safe area edges, not the screen edges.

**Rationale**: On a 430pt iPhone Pro Max, a top-placed button requires thumb extension — a flow break that interrupts the "pure control" core fantasy. Bottom placement keeps the player in their grip. Display elements need only a glance — top placement is appropriate and keeps them visible without competing with the board.

**Accessibility**:
- Bottom-zone buttons satisfy the 44pt iOS HIG requirement within the player's natural thumb reach — no extension or awkward grip required.
- Hairline dividers provide structural separation without color — they are visible at any contrast setting.

**When to Use**: Any gameplay overlay screen (HUD, mini-overlay, contextual overlay). This layout is the mandatory structure for all screens displayed during an active game session.
**When NOT to Use**: Modal screens (Level Complete) are overlays over this structure, not replacements for it. Non-gameplay screens (Main Menu, Level Select) have their own layout language — TBD in those specs.

---

### 14. Sequential Reveal Animation

**Category**: Overlay / Flow
**Used In**: Level Complete UI (star cascade)

**Description**: A series of elements that animate in sequentially with a fixed interval between each, rather than all at once. Sequential reveal creates rhythm and anticipation — the player waits for the next element, which makes each feel like a distinct reward. Applied to BoltSort's star rating display.

**Specification**:
- **Elements**: 3 star slots. Earned stars animate; unearned slots appear immediately as dim outlines (no animation).
- **Reveal order**: Left to right (star 1 → star 2 → star 3). Only earned stars get the pop animation.
- **Pop animation** (per star): Scale from 0% to 110% ease-out-back, then settle to 100% (spring effect). Duration: ~200ms per star.
- **Interval**: `star_reveal_interval_ms` (default: 300ms) between the start of each star's animation. Stars overlap slightly — star 2 begins before star 1 finishes.
- **Non-blocking**: Player can tap navigation buttons before all stars have revealed. Navigation during REVEALING skips remaining animations and transitions to the appropriate next state.
- **Unearned slots**: Appear at opacity 40%, dim outline, immediately on screen open. No animation. Always 3 slots displayed — 1-star result always shows 2 dim outlines.

**Accessibility**:
- Star count is also communicated by the coin reward amount and by any numeric display. Color is not the only signal — dim vs. bright communicates earned vs. unearned (brightness differential, not hue differential).
- Players with reduced vision can still identify earned star count from brightness contrast.

**When to Use**: Small finite sequences (2–5 elements) where each element carries individual reward value and the player will enjoy the rhythm.
**When NOT to Use**: Long lists or loading sequences. Sequential reveal adds time — only use it when the wait is itself part of the reward.

---

### 15. Skippable Animation Overlay

**Category**: Overlay / Flow
**Used In**: Level Complete UI

**Description**: A result screen that plays celebration animations by default but allows the player to bypass them at any moment by tapping a navigation button. The skip is instant — the overlay jumps to its post-animation state (IDLE or AD_OFFER) immediately. Critically: all gameplay-critical state changes (coin delivery, star recording) fire at screen entry, before any animation plays, so skipping never costs the player their reward.

**Specification**:
- **State changes fire on screen entry** (`OnEnable`): `coin_reward_granted` fires immediately, before any animation begins. Coins are in the player's balance before the first rendered frame of the overlay.
- **Animation plays independently**: Star cascade (Pattern 14), coin counter animation, board celebration — all play for players who wait. These are purely cosmetic.
- **Navigation always available**: Next Level, Retry, and Menu buttons are tappable from the moment the screen appears. Tapping any navigation button while animations are playing causes:
  - Remaining animations are cut (not faded — immediate cut).
  - Screen transitions to DISMISSED.
  - No coins are lost; they were delivered on entry.
- **AD_PROCESSING exception**: Navigation is disabled during AD_PROCESSING only. The ad SDK has the screen — the player cannot leave until the ad resolves or the watchdog fires.
- **AD_OFFER is not blocking**: The Skip button in AD_OFFER is always visible and never hidden. The ad offer is a choice, not a gate.

**Accessibility**:
- Immediate navigation availability serves players with cognitive fatigue or time constraints — they do not have to wait for animations.
- Fire-on-entry coin delivery guarantees that fast dismissal never disadvantages the player.

**When to Use**: Any result or summary screen where celebration animations add delight for attentive players but should not be required viewing.
**When NOT to Use**: Do not apply "skippable" to state-changing sequences. If watching something to completion is required for the game to proceed correctly, it must not be skippable. In BoltSort, all state changes happen before animations — making everything truly skippable.

---

### 16. Primary / Secondary Navigation Hierarchy

**Category**: Overlay / Flow
**Used In**: Level Complete UI (Next Level, Retry, Menu)

**Description**: A button hierarchy where one action is the expected, flow-continuing choice (primary) and alternative actions are available but visually subordinate (secondary). The primary button communicates momentum; secondary buttons communicate options without competing.

**Specification**:
- **Primary button** (Next Level): Largest hit area. Highest emission (0.6–0.8 at rest, surges to 1.0 on tap-down per Pattern 6). Full label. Centered or top-of-action-zone position.
- **Secondary buttons** (Retry, Menu): Smaller hit area (still ≥48×48pt). Lower emission (0.3 at rest). Shorter or icon-only label. Flanking or below-primary position.
- **Ratio guidance**: Primary button should occupy ≥2× the visual prominence of any individual secondary button. Do not make secondary buttons so small they are hard to tap — reduce their visual weight, not their tap target.
- **Disabled state** (during AD_PROCESSING): All three buttons use Pattern 8. They are suppressed simultaneously — there is no "more disabled" or "less disabled" during this state.

**Accessibility**:
- Hierarchy communicated by size and emission level, not color alone. All three buttons are CHROME-03 at varying opacities — no primary-specific color.
- Secondary buttons meet minimum tap target (48×48pt) despite reduced visual size.

**When to Use**: Any decision point with one "expected" path and one or more "alternative" paths. The primary/secondary split should reflect the game's design intent — if the designer wants the player to proceed, Next Level is primary. If this were a game where replay is encouraged, Retry might be primary.
**When NOT to Use**: Binary equal-weight choices (Watch Ad / Skip). Those use a different hierarchy — Watch is opt-in, Skip is default — which reverses the normal primary/secondary model. See Pattern 17.

---

### 17. Opt-In Ad Offer

**Category**: Overlay / Flow
**Used In**: Level Complete UI (AD_OFFER state)

**Description**: A rewarded ad offer where the ad watch is the opt-in action and skipping is the default-path action. The Skip button is always visible, never hidden, and never requires the player to wait before it becomes tappable. This inverts the normal primary/secondary hierarchy — here, Skip is the "no-change" path, and Watch is the voluntary enhancement.

**Specification**:
- **Entry condition**: AD_OFFER state is entered only if `ad_available == true` AND `ad_offer_show_rate` roll passes. Both must be true; either false means the player proceeds directly to IDLE.
- **Always-visible Skip**: The Skip button is present and tappable from the moment AD_OFFER is entered. It is never hidden, never has a countdown timer, never has a delayed enable.
- **Watch button**: Opt-in. Labeled clearly with the reward: "Watch — earn [2× coins]" (or equivalent). Tapping Watch enters AD_PROCESSING.
- **No pressure signals**: No timer, no urgency copy, no "Limited time!" framing. The offer is calm and clear. The reward is stated once; it is not repeated or emphasized.
- **AD_PROCESSING**: Navigation buttons disabled. Watch and Skip buttons disabled. Async Processing Indicator (Pattern 7) may be shown on the Watch button during ad load if ad load time is non-trivial. The player waits for the ad SDK.
- **Resolution** (either outcome — ad grant or deny): Screen exits AD_PROCESSING to IDLE. Navigation buttons re-enable. If ad was denied or watchdog fired, no bonus is delivered and no negative feedback is shown. The player simply proceeds.

**Accessibility**:
- Skip is always reachable without waiting — serves players who cannot or choose not to watch ads.
- No cognitive pressure (timers, countdown, repeated prompts) — serves players with anxiety or cognitive load concerns.
- AD_PROCESSING is the only time navigation is blocked; it is unambiguously communicated by the suppressed button states.

**When to Use**: Rewarded ad offers where the player should feel in control of the choice. The offer must feel like a bonus opportunity, not a friction gate.
**When NOT to Use**: Do not use this pattern for any required interaction. If something must happen before the player proceeds, it is a gate — not an offer — and should be designed as such (without a skip option).

---

---

### 18. Destructive Action Confirm

**Category**: Modal / Safety
**Used In**: Pause Menu (Restart Level, Exit to Menu)

**Description**: A two-step confirmation flow for actions that are irreversible or destructive (e.g., losing level progress, navigating away mid-game). The first tap shows a confirmation dialog; the action only fires on a second explicit tap of CONFIRM. A single tap can never accidentally trigger a destructive action.

**Specification**:
- **Trigger**: Player taps a button marked as destructive (e.g., RESTART LEVEL, EXIT TO MENU).
- **Dialog appearance**: Modal panel scales in (0.85→1.0, opacity 0→1, 180ms ease-out) centered on screen, above the current overlay. Backdrop of current screen dims by additional 20% opacity.
- **Dialog content**:
  - Title: One short sentence stating the consequence. ("Restart level?" / "Exit to main menu?")
  - Body (optional): One short sentence clarifying the loss. ("Your progress will be lost." — omit if title is already explicit.)
  - Two buttons: **CANCEL** (left/top) and **CONFIRM** (right/bottom).
- **Button hierarchy**: CANCEL is the **primary visual weight** or equal weight — never smaller than CONFIRM. The safer option is visually dominant or equal.
- **CANCEL**: Outline style (`CHROME-04` border, `CHROME-04` label). Tap dismisses dialog; returns to parent screen unchanged.
- **CONFIRM**: Outline style, same visual weight as CANCEL. Tap executes the destructive action and dismisses both dialog and parent overlay.
- **Backdrop tap = CANCEL**: Tapping anywhere outside the dialog panel triggers CANCEL. Consistent with the global tap-outside-to-dismiss convention.
- **No timeout**: Dialog does not auto-dismiss. Player must make a choice.
- **Dialog exit animation**: Scale-out (1.0→0.85, opacity 1→0, 140ms ease-in).

**Accessibility**:
- Focus order: CANCEL before CONFIRM (keyboard/screen reader — safer default first).
- Screen reader announces dialog content on open: title + body text as an alert.
- Both buttons ≥48×48pt; 16dp gap minimum between them.
- No color-only differentiation: both buttons use the same `CHROME-04` outline treatment — neither is "red/destructive" color.

**When to Use**: Any action that cannot be undone and causes loss of data or progress — level restart, mid-level exit, account deletion (future).
**When NOT to Use**: Actions that can be undone (undo a move, cancel a hint — these are reversible so no confirm needed). Navigation to screens where the player can return (e.g., opening settings — no progress lost).

---

## Animation Standards

Cross-pattern timing reference. All durations in milliseconds. Use `Time.unscaledDeltaTime` for UI animations to survive pause/timeScale changes.

| Animation type | Duration | Easing | Notes |
|---|---|---|---|
| Button tap-down (scale) | 60ms | EaseInCubic | Scale 1.0 → 0.94 |
| Button tap-up (scale return) | 80ms | EaseOutCubic | Scale 0.94 → 1.0 |
| Overlay / dialog enter (scale-in) | 180ms | EaseOutCubic | 0.85 → 1.0 + opacity 0 → 1 |
| Overlay / dialog exit (scale-out) | 140ms | EaseInCubic | 1.0 → 0.85 + opacity 1 → 0 |
| Screen enter (slide from right) | 200ms | EaseOut | Navigation forward |
| Screen exit (slide to left) | 200ms | EaseIn | Navigation forward |
| Screen enter (fade from black) | 250ms | Linear | Cold launch / transition from splash |
| Coin balance pulse (positive) | 300ms total | Custom | CHROME-04→CHROME-03 100ms, return 200ms |
| Toast appear / disappear | 200ms / 300ms | EaseOut / EaseIn | See Pattern #10 |

---

## Sound Standards

> **Status**: Deferred — assignments pending Audio System GDD authoring.
> **Owner**: audio-director
> **Deadline**: Before HUD implementation sprint

| Interaction | Sound slot | Bus | Notes |
|---|---|---|---|
| Button tap (HUD buttons) | `ui_tap` | UIVolume | Shared clip for undo, hint, pause; distinct from bolt settle |
| Button tap (nav buttons) | `ui_tap_nav` | UIVolume | Main menu PLAY, pause menu buttons |
| Coin pulse (positive) | `coin_gain` | UIVolume | Short chime; fires at pulse start |
| Toast appear | `toast_chime` | UIVolume | Soft, non-intrusive |
| Overlay open | *(silence or ambient duck)* | — | Pause menu: ambient hum ducks; no discrete SFX |

---

## Gaps & Patterns Needed

The following interaction patterns have been identified as needed based on planned screens and systems not yet specced. They should be formalized when the relevant UX spec is authored.

| # | Pattern Name | Needed For | Priority |
|---|---|---|---|
| ~~18~~ | ~~Destructive Action Confirm~~ | ~~Pause Menu~~ | *Formalized 2026-05-17 — see Pattern #18* |
| A | **Animated Counter (Counting Up)** | Level Complete UI coin reward animation, future: score displays | High — needed before Level Complete UX spec |
| B | **Bolt Color Differentiation (Colorblind Mode)** | All board screens; critical for Standard accessibility tier | **Blocking** — must be designed before colorblind modes can be implemented. Decision: icon/number/pattern on bolt face. |
| C | **Hint Highlight / Arrow Overlay** | In-Game HUD (hint result — bolt and destination highlighted) | High — needed before Hint System implementation sprint |
| D | **Level Tile (Locked / Unlocked / Stars)** | Level Select UI | Medium — Beta scope |
| E | **Coin Shop Item Card** | Shop UI | Medium — Beta scope |
| F | **Skin Preview Carousel** | Shop UI | Medium — Beta scope |
| G | **Full-Screen Loading / Transition** | Main Menu → Game, cold start | Medium — Beta scope |
| H | **Settings Toggle / Slider** | Settings UI (audio volume, quality tier, accessibility toggles) | Low — Launch scope |

**Pattern B is the highest-priority gap.** The accessibility requirements doc (`design/accessibility-requirements.md`) identifies bolt color differentiation as a critical unresolved design question. This pattern must be designed before any colorblind mode implementation begins. Options: per-bolt icon (triangle/circle/cross/square), numeric label (1–8), or directional pattern overlay. Decision to be made in the `/ux-design hud` session.

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| What non-color differentiator to use for bolt colors in colorblind modes? (icon, number, pattern) | ux-designer | Before `/ux-design hud` session | Unresolved |
| What is the UI tap SFX clip? AudioSystem needs a `UIVolume`-bus sound for HUD button taps — distinct from `PlayBoltSettle`. | audio-director | Before HUD implementation sprint | Unresolved |
| Does the pity grant toast fire before or replace the coin balance pulse? Or do both fire? | game-designer | Before HUD implementation sprint | Partially resolved: Art Bible 7.5 states toast first, then coin pulse sequential (not overlapping). Confirm in HUD GDD and HUD UX spec. |
| Should Watch Ad and Skip be equal-weight buttons or does Watch take primary size? | game-designer | Before Level Complete UX spec | Unresolved — Pattern 17 leaves this open. If the game wants ad engagement, Watch should be larger; if respecting the "Cosmetic Not Coercive" pillar, equal weight or Skip-primary may be more honest. |
