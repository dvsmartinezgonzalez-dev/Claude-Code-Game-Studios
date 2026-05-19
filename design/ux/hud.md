# HUD Design: In-Game HUD

> **Status**: Committed
> **Author**: ux-design skill — 2026-05-12
> **Last Updated**: 2026-05-12
> **Template**: HUD Design
> **Source GDD**: `design/gdd/in-game-hud.md` (Approved 2026-05-10)
> **Source Art**: `design/art/art-bible.md` Sections 7.1–7.6, 4.4
> **Source ADRs**: ADR-0008 (UI hierarchy / safe area), ADR-0012 (HUD business logic)
> **Patterns Used**: #2 (Optimistic Lock), #3 (Multi-State Processing), #6 (Button Press Feedback), #7 (Async Processing Indicator), #8 (Disabled State), #9 (System Freeze), #10 (Contextual Toast), #11 (Event-Driven Counter), #12 (Live Balance with Pulse), #13 (Thumb Zone / Glance Zone)
> **Resolved OQs**: OQ-02 (coin seed on level_loaded), OQ-03 (HINT_PROCESSING treatment), OQ-06 (error overlay — tap to retry)

---

## HUD Philosophy

> **"The HUD is an instrument panel. It reports. It does not alert."**

BoltSort's HUD is the machine's readout, not a participant in play. The board is the game — every bolt, every stack, every glow is the experience. The HUD exists at the periphery of attention: the player glances at the move counter between moves, checks coin balance before requesting a hint, and taps undo when they want to walk something back. Nothing in the HUD competes for attention that belongs to the board.

This drives three constraints that apply to every element and every state:

1. **Instrument-grade, not alarm-grade.** No bold text, no pulsing urgency, no red. The HUD reports facts in the same visual register the machine uses at rest: cool chrome, controlled luminance, tabular numerals.
2. **IDLE HUD does not animate.** In IDLE state, the HUD is static. Motion in the HUD means the system is doing something on the player's behalf (HINT_PROCESSING spinning arc) or the player's economy changed (coin pulse). Motion as decoration is forbidden.
3. **The board's bolt colors are the game's chromatic energy.** Every chrome decision protects that energy. CHROME-03 cyan is the only non-bolt color permitted on interactive elements. No warm fills during play states 1–4.

---

## Information Architecture

### Full Information Inventory

All information the game systems need to communicate to the player through the HUD, sourced from GDD UI Requirements:

| # | Information | Source System | Nature |
|---|---|---|---|
| 1 | Move count (cumulative, undo-adjusted) | GSM `board_state_changed(delta_move_count)` | Live — changes every committed move and every undo |
| 2 | Coin balance (current, post-all-mutations) | Coin Economy `coin_balance_changed(new_balance, delta)` | Live — changes on earn, spend, pity grant |
| 3 | Undo availability (stack depth and FSM state) | GSM `board_state_changed(undo_stack_depth)` | Live — enabled/disabled per move and state |
| 4 | Hint availability (balance and FSM state) | F-03: `coin_balance >= hint_cost AND hud_state == IDLE` | Derived — re-evaluated on `coin_balance_changed`, `hint_result`, `level_loaded` |
| 5 | Hint processing state | HUD FSM (HINT_PROCESSING) | State — active during open hint request |
| 6 | Level load failure | GSM `session_load_failed` | Event — triggers error overlay |
| 7 | Pity grant notification | CE `coin_balance_changed` with `earn_source = EarnSource.PityGrant` | Event — triggers contextual toast |

**What is deliberately NOT in the HUD:**
- Par moves — judgment is deferred to Level Complete; showing par during play creates unnecessary pressure
- Star rating target — same reasoning; the game is designed for flow, not optimization anxiety
- Level number / level ID — no narrative significance during play; belongs in Level Select UI
- Hint result / optimal move — Hint System displays the highlighted bolt, not the HUD

### Categorization

| Information | Category | Rationale |
|---|---|---|
| Move count | **Must Show** | Primary player self-assessment signal; absent from comparable titles (Ball Sort Puzzle) but present here because BoltSort's "pure control" fantasy includes knowing your efficiency. Instrument-grade presentation prevents it from reading as pressure. |
| Coin balance | **Must Show** | Directly gates hint availability (F-03). Player needs it available during play without requesting it. |
| Undo button | **Must Show** | Unlimited undo is a core design promise. The physical button must always be visible so the player knows the promise is kept. |
| Hint button | **Must Show** | Coin-gated feature — the button's enabled/disabled state is information. Visibility during play communicates "this option exists, this is its current cost." |
| Hint processing state | **Must Show** (inline on hint button) | A locked button with no motion feedback reads as broken. HINT_PROCESSING visual treatment is information, not decoration. |
| Level load failure | **Must Show** (error overlay) | Without an error state, a failed load produces a blank screen with no affordance. |
| Pity grant | **Contextual** (toast, triggered by event) | The player did not ask for this grant — it fires automatically. A toast communicates "the system did something on your behalf" without interrupting play. |

---

## Layout Zones

From Art Bible 7.1. The layout is fixed — not an option being proposed, but the committed direction.

```
┌──────────────────────────────────────────────────┐
│ ← safe area top inset + 16pt padding →           │
│ ┌────────────────────────────────────────────┐   │
│ │  ●  142          [MOVE: 000]     [‖]     │   │  ← GLANCE ZONE
│ │  COIN CHIP       MOVE COUNTER   PAUSE    │   │     CHROME-02 @ 60–70% opacity
│ └────────────────────────────────────────────┘   │
│  ── hairline 1px #1E2A38 ──────────────────────  │
│                                                  │
│                                                  │
│              BOARD AREA                          │  ← CONTENT ZONE
│          (no HUD elements)                       │     visually dominant
│                                                  │
│                                                  │
│  ── hairline 1px #1E2A38 ──────────────────────  │
│ ┌────────────────────────────────────────────┐   │
│ │  [↩ UNDO]                      [💡 HINT]  │   │  ← THUMB ZONE
│ └────────────────────────────────────────────┘   │     CHROME-02 @ 60–70% opacity
│ ← safe area bottom inset + 16pt padding →        │
└──────────────────────────────────────────────────┘
```

**Zone rules:**
- **Glance zone** (top): Primarily display. Coin chip at top-left. Move counter at top-center. Pause button at top-right — only interactive element in the glance zone; small footprint, low visual weight (instrument register, not action register).
- **Content zone** (center): Board area. No HUD element intrudes. The board is sovereign.
- **Thumb zone** (bottom): Interactive only. Undo at bottom-left, Hint at bottom-right. ~200pt separation on a 390pt screen eliminates inter-button mis-tap and reinforces semantic distinction: undo is escape (left), hint is resource (right).
- **Hairlines**: 1px `#1E2A38` at glance-zone bottom and thumb-zone top. Only drawn boundaries in the layout.
- **HUD panel**: CHROME-02 `#141C24` at 60–70% opacity behind all HUD elements. Not floating text over board content — always a panel layer.
- **Safe area**: `Screen.safeArea` applied via `SafeAreaPanel` (ADR-0008). All zone measurements from safe area edges.
- **Visual Budget**: Maximum 5 simultaneous HUD elements (coin, move counter, pause, undo, hint). Error overlay is mutually exclusive with all play states — not counted against budget. HUD panels (glance + thumb zones) occupy ≤20% of screen height combined. Board area ≥60% screen height unobstructed.

---

## HUD Elements

### Element 1 — Move Counter

| Attribute | Value |
|---|---|
| **Category** | Must Show |
| **Pattern** | #11 — Event-Driven Counter |
| **Position** | Top-center, glance zone |
| **Content** | Integer `move_count`, range `[0, ∞)` |
| **Font** | IBM Plex Mono, SemiBold (600), tabular figures |
| **Color** | CHROME-04 `#C8D8E8` |
| **Size** | Primary display — largest text element in HUD |
| **Update trigger** | `board_state_changed(delta_move_count)`: `move_count += delta_move_count`; floor at 0 |
| **Update animation** | None — instantaneous cross-fade to new value |
| **Reset** | 0 on `level_loaded` (instantaneous) |
| **FROZEN** | Holds final value; opacity drops to 60% CHROME-04 |

**Design notes**: Tabular figures are required — proportional digit glyphs shift layout on increment and violate the instrument aesthetic. Never Bold (700+) — bold reads as alert. The move counter is not an alarm; it is a logbook entry.

---

### Element 2 — Coin Balance Display

| Attribute | Value |
|---|---|
| **Category** | Must Show |
| **Pattern** | #12 — Live Balance Display with Pulse |
| **Position** | Top-left, glance zone |
| **Content** | Integer `coin_balance` from Coin Economy; icon + numeral chip |
| **Font** | IBM Plex Sans, Regular (400), ~70% of move counter cap-height |
| **Color** | CHROME-04 `#C8D8E8` at rest |
| **Icon** | Stroked circle, CHROME-03 `#4DCFEF` stroke, stroke-only. Interior mark: horizontal slash or minimal "c" glyph, 1dp stroke. If mark illegible at 16dp rendered: use slash variant. |
| **Seed** | `ICoinEconomy.GetBalance()` called directly at `level_loaded` |
| **Update trigger** | `coin_balance_changed(new_balance, delta)` — numeral cross-fades to `new_balance` immediately |
| **Positive delta animation** | Numeral: CHROME-04 → CHROME-03 over 100ms, returns CHROME-04 over 200ms (300ms total). Icon: +15% scale ease-in-out over 150ms. No warm color during play. |
| **Negative delta animation** | Numeral: no color shift. Icon: -5% scale deflation, 100ms. |
| **Pulse cap** | 300ms (`coin_pulse_duration_ms` tuning knob); rapid events restart from current value |
| **Pity grant path** | When `earn_source == EarnSource.PityGrant`: Contextual Toast (Pattern #10, "Hint restored.") fires first; coin pulse follows after toast begins fade-out. Sequential — not overlapping. |
| **FROZEN** | Fully live. Continues responding to `coin_balance_changed`. Only non-suppressed element in FROZEN. |

**Art Bible override on tuning knobs**: HUD GDD shows `coin_pulse_color_positive = #4CAF50` (green) and `coin_pulse_color_negative = #FF9800` (amber). Art Bible 7 supersedes: positive pulse = CHROME-03 `#4DCFEF` (cyan numeral shift); negative pulse = no color shift (icon deflation only). No warm colors during play states 1–4. Implementation must use Art Bible values.

---

### Element 3 — Undo Button

| Attribute | Value |
|---|---|
| **Category** | Must Show |
| **Pattern** | #2 — Optimistic Lock Button |
| **Position** | Bottom-left, thumb zone (safe area inset + 16pt padding) |
| **Hit area** | 56×56pt recommended; 48×48pt minimum |
| **Icon** | Counterclockwise arc-arrow, 270° sweep, left-heavy, arrowhead at leading (left-bottom) end. 2dp stroke, CHROME-03 `#4DCFEF`. Rounded line caps. |
| **Icon box** | 50–60% of button interior (leaves glow halo and rounded border visible as frame) |
| **Enabled condition** | `undo_stack_depth > 0` AND `hud_state == IDLE` (F-02) |
| **Disabled condition** | `undo_stack_depth == 0` OR `hud_state == MOVE_EXECUTING` OR `level_complete` received |
| **Tap-down feedback** | Pattern #6: scale 94% / 60ms ease-in-cubic; glow 1.0 instant; 10% CHROME-02 interior fill |
| **Lock behavior** | Disabled immediately on tap-up (Pattern #2). Emits `undo_requested`. Re-enables on `animation_complete(seqId)` where seqId matches `_pendingSequenceId` AND F-02 re-evaluates true. |
| **Disabled appearance** | Pattern #8: stroke opacity ~40%, glow 0.0, emission 0.0. Shape and position preserved. |
| **FROZEN** | Emission 0.0, stroke 15% opacity (more severe than standard Disabled). Pattern #9. |

---

### Element 4 — Hint Button

| Attribute | Value |
|---|---|
| **Category** | Must Show |
| **Pattern** | #3 — Multi-State Processing Button |
| **Position** | Bottom-right, thumb zone (safe area inset + 16pt padding) |
| **Hit area** | 56×56pt recommended; 48×48pt minimum |
| **Icon** | Single lightbulb outline: D-shape stroke 2dp for glass dome; two horizontal lines 1.5dp for base. No filament, no radiating rays. Geometric, legible at 24dp. CHROME-03 `#4DCFEF`. |
| **Icon box** | 50–60% of button interior |
| **ENABLED condition** | `coin_balance >= hint_cost` AND `hud_state == IDLE` (F-03) |
| **ENABLED re-evaluation** | On `level_loaded`, `coin_balance_changed`, `hint_result` |
| **Tap-down feedback** | Pattern #6: scale 94% / 60ms; glow 1.0 instant; 10% CHROME-02 fill |
| **→ HINT_PROCESSING** | Emission 0.2 (below idle 0.4). Interior: 5% CHROME-02 fill (partial fill signals locked). Spinning arc begins: 2dp stroke CHROME-03, 90° segment, clockwise, 1.0s/revolution, sine in/out easing. Arc runs until `hint_result` or timeout. `hint_requested` fires. |
| **→ resolve from HINT_PROCESSING** | Arc stops instantly. Button fades over 100ms to ENABLED or DISABLED depending on re-evaluated F-03. No error indicator on timeout. |
| **Timeout** | `hint_timeout_ms` = 5000ms default. HUD enforces via stored Coroutine reference (`_hintTimeoutCoroutine`). On expiry: exit HINT_PROCESSING, re-enable button, no coin deducted. |
| **DISABLED appearance** | Pattern #8: stroke ~40% opacity, glow 0.0, emission 0.0. |
| **FROZEN** | Emission 0.0, stroke 15% opacity. Pattern #9. |

---

### Element 5 — Error Overlay (INACTIVE / session_load_failed)

| Attribute | Value |
|---|---|
| **Trigger** | `session_load_failed` event received while HUD is INACTIVE |
| **Coverage** | Full-screen overlay. CHROME-02 `#141C24` at 95% opacity. Board and all HUD elements hidden behind overlay. |
| **Icon** | Warning triangle (⚠), CHROME-03 `#4DCFEF` stroke, 2dp. Centered, ~32dp |
| **Primary text** | "Unable to load level." IBM Plex Sans, Regular (400), CHROME-04 `#C8D8E8`, 18sp |
| **Secondary text** | "Tap anywhere to retry." IBM Plex Sans, Light (300), CHROME-04 at 60% opacity, 14sp |
| **Tap to dismiss** | Tap anywhere on overlay → dismiss overlay → re-trigger level load (LevelProgression re-requests `GSM.LoadLevel()`). |
| **Retry cap** | No cap in MVP. If retry also fails, overlay reappears. Future: add max retry count with "Something's wrong — please restart the app." after 3 failures. |
| **Animation** | Overlay fades in over 200ms. On tap: fades out over 150ms simultaneously as retry begins. |

---

### Element 6 — Pause Button

| Attribute | Value |
|---|---|
| **Category** | Must Show |
| **Pattern** | #6 (Button Press Feedback), #8 (Disabled State) |
| **Position** | Top-right, glance zone (safe area inset + 16pt padding, right-aligned) |
| **Hit area** | 48×48pt minimum; recommended 48×48pt (consistent with minimum — this is a secondary control, not a primary thumb-zone control) |
| **Icon** | Two vertical bars (‖), 2dp stroke, CHROME-04 `#C8D8E8`. Geometric pause symbol. No circle border — bare icon only, consistent with minimalist instrument aesthetic. |
| **Icon box** | 50% of button interior |
| **Icon opacity** | 60% at rest (lower than undo/hint at their emission 0.4 level — pause is less frequently used; lower weight preserves board focus) |
| **Enabled condition** | HUD state == IDLE or HINT_PROCESSING |
| **Disabled condition** | HUD state == FROZEN (level complete — no pause during celebration) |
| **Tap-down feedback** | Pattern #6: scale 94% / 60ms ease-in-cubic; icon opacity 60% → 100% instant. No glow surge (no CHROME-03 glow on pause button — it is not a game action, it is a navigation action). |
| **Outcome** | Opens pause menu overlay (`PauseMenuUI.Show()`). GSM does not pause — board state is preserved but no `Time.timeScale` change at MVP. |
| **Disabled appearance** | Pattern #8: icon opacity ~25%, no tap response. |
| **FROZEN** | Same as DISABLED — Pattern #9 severity; emission 0.0. |

**Design note**: The pause button is intentionally lower visual weight than undo and hint. It does not use CHROME-03 cyan because it is not a game action — it is a system navigation. This is consistent with the "instrument panel" philosophy: the pause button is infrastructure, not feature.

**GDD update required**: `design/gdd/in-game-hud.md` UI Requirements section must be updated to add: "Pause button: tap during IDLE or HINT_PROCESSING opens pause overlay. Disabled in FROZEN."

---

## Dynamic Behaviors

### HUD States

| State | Entry Trigger | Exit Trigger | Visual Treatment |
|---|---|---|---|
| **INACTIVE** | Startup | `level_loaded` | All elements absent. Error overlay may appear on `session_load_failed`. |
| **IDLE** | `level_loaded` | Hint tap / `level_complete` | All elements at resting state. Move counter CHROME-04 static. Coin display CHROME-04 static. Buttons at emission 0.4 CHROME-03. HUD panel CHROME-02 at 60–70% opacity. Nothing pulses. |
| **HINT_PROCESSING** | Hint tap (from IDLE) | `hint_result` or `hint_timeout_ms` | All other elements interactive and unchanged. Hint button: emission 0.2 + 5% fill + spinning arc (Pattern #7). |
| **FROZEN** | `level_complete` | `level_loaded` (next level / retry) | Undo and hint buttons: emission 0.0, stroke 15% opacity. Move counter: final value, CHROME-04 60%. Coin display: fully live (Pattern #9 exception). Coin balance changes still animate. |

### State Transition Rules

- INACTIVE → IDLE: on `level_loaded`. All elements initialize from their `level_loaded` reset state (counter = 0, buttons re-evaluated, coin seeded).
- IDLE → HINT_PROCESSING: on hint tap (only valid from IDLE; if button is disabled, no transition).
- HINT_PROCESSING → IDLE: on `hint_result` (any result) or `hint_timeout_ms` expiry. Button resolves to ENABLED or DISABLED per F-03.
- IDLE / HINT_PROCESSING → FROZEN: on `level_complete`. Immediate — no delay waiting for animation. FROZEN takes priority over any in-progress HINT_PROCESSING.
- FROZEN → IDLE: on `level_loaded`.
- Any state → INACTIVE + error overlay: on `session_load_failed` (only occurs before first `level_loaded`).

### Undo Optimistic Lock (independent of HUD FSM)

The undo button's lock state is independent of the HUD FSM state. It does not correspond to a HUD state — it is a per-button overlay behavior:

- Button disables immediately on tap (Pattern #2 optimistic lock).
- Stores `_pendingSequenceId` from the in-flight move.
- Re-enables when `animation_complete(seqId)` received with matching seqId AND F-02 evaluates true.
- During MOVE_EXECUTING (GSM state, not HUD state): HUD remains in IDLE. The undo button is locked, but hint button and coin display are fully active.

### Coin Display Live During FROZEN

The coin balance display is the only HUD element that remains at full operational capacity during FROZEN. This is a deliberate signal: the machine is still counting the player's reward (coins from `OnCoinRewardGranted` firing in LevelCompleteUI's `OnEnable`) even as the rest of the HUD steps back. The player sees the HUD quieting while coins arrive — a spatial reading of "done playing, receiving reward."

---

## Platform & Input Variants

| Variant | Handling |
|---|---|
| **iOS — notch / Dynamic Island** | `SafeAreaPanel` anchors to `Screen.safeArea`. Top HUD row clears Dynamic Island. Glance zone top padding: `Screen.safeArea.yMin` from screen top → convert to normalized anchor. |
| **iOS — home indicator** | Thumb zone bottom padding: `Screen.height - Screen.safeArea.yMax` → convert. Gesture area below buttons. |
| **Android — notch / punch-hole** | Same `SafeAreaPanel` approach. Verified on Samsung Galaxy A series. |
| **Android — gesture pill (swipe-up nav)** | `Screen.safeArea` bottom inset accounts for pill. Thumb zone above pill. |
| **Tall screens (21:9, 20:9)** | Content zone (board area) expands vertically. Glance and thumb zones maintain fixed height from safe area edges. No layout break. |
| **Standard screens (16:9)** | Reference resolution. CanvasScaler Match Width or Height 0.5 (balanced) per ADR-0008. |
| **Landscape** | Not supported in MVP (portrait lock). |
| **Gamepad / keyboard** | Not applicable (touch only). |
| **Low-tier device (QTS Low, 30fps)** | HUD frame rate reduced to 30fps. Animations (coin pulse, button press) still complete in their defined ms durations — all animations use `Time.unscaledDeltaTime` (per ADR-0009 pattern). At 30fps, the 60ms button press completes in 2 frames — imperceptible. No HUD-specific Low-tier variant. |

**Canvas configuration** (ADR-0008):
- Canvas: Screen Space-Overlay, Sort Order 0
- CanvasScaler: Scale with Screen Size, Reference 1080×1920, Match 0.5

---

## Accessibility

Per `design/accessibility-requirements.md` — Standard tier.

### Touch Targets

Both undo and hint buttons: **56×56pt recommended** hit area (above 44pt iOS / 48dp Android minimum). Hit area may exceed icon visual size via `RectTransform` padding. Validated during layout: on iPhone SE (375pt wide), the ~200pt button separation provides adequate clearance with 56pt buttons occupying bottom-left and bottom-right anchors.

### Text Contrast

| Element | Foreground | Background (at 70% opacity panel over CHROME-01) | Effective Ratio | Pass? |
|---|---|---|---|---|
| Move counter | CHROME-04 `#C8D8E8` | ~`#0E1720` (panel blend) | ~10:1 | ✅ WCAG AA (4.5:1) |
| Coin numeral | CHROME-04 `#C8D8E8` | ~`#0E1720` | ~10:1 | ✅ |
| Error overlay primary | CHROME-04 `#C8D8E8` | CHROME-02 at 95% | ~11:1 | ✅ |
| Error overlay secondary | CHROME-04 at 60% ≈ `#788898` | CHROME-02 at 95% | ~4.7:1 | ✅ |

All text meets WCAG AA minimum (4.5:1 for body text below 18pt). No Pure White `#FFFFFF` in persistent HUD text.

### Non-Color State Differentiation

All state changes communicate via luminance/opacity + shape, not color:
- Disabled buttons: opacity drop to ~40% + glow 0.0. Icon shape preserved. No color change. (Pattern #8)
- FROZEN buttons: emission 0.0, stroke 15%. Distinct from standard disabled by severity. (Pattern #9)
- HINT_PROCESSING: emission 0.2 + motion (spinning arc). Motion is primary cue — the arc color (CHROME-03) matches the resting button stroke; only the animation distinguishes it.
- Coin pulse direction: icon scale direction (+15% vs −5%) communicates positive/negative delta. No warm color during play.

### Colorblind Bolt Differentiation

Per Art Bible 4.4, the game implements a **micro-icon recess pattern** system on bolt surfaces. Six unique patterns (hexagonal, cross-slot, triangular, diamond, circle-dot, star) are rendered on bolt sprites as a normal map / mask texture channel overlay. The system is:

- **Off by default** — player preference toggle
- **No additional draw calls** — mask channel in existing bolt sprite
- **Visible at 44px** — patterns occupy 40% bolt diameter at 40% opacity

The HUD has no responsibility for this system in MVP scope. The toggle control belongs to Settings UI (Launch scope). For Pre-Production and Beta, the system is developer-toggle only.

The HUD UX spec must be updated when the Settings UI toggle is designed to include a reference to the settings path.

### Reduced Motion (Future)

If `reduced_motion_mode` is implemented (planned Standard tier feature):
- Hint button HINT_PROCESSING arc: replace rotating arc with static arc pulsing opacity (0.2 → 0.5 → 0.2, 1.0s cycle, sine easing)
- Coin pulse: reduce to numeral cross-fade only (no icon scale)
- Error overlay: fade-in 0ms (instant appear)
- Button press: scale suppress to 100% (no scale change); glow surge still fires (glow is not motion)

---

## Localization Considerations

| Element | Text | Max safe length | 40% expansion risk |
|---|---|---|---|
| Move counter | Numeric only (no label) | N/A | None — numbers are language-neutral |
| Coin display | Numeric only (no label) | N/A | None |
| Undo button | Icon only (no label per Art Bible 7.2) | N/A | None |
| Hint button | Icon only | N/A | None |
| Error overlay — primary | "Unable to load level." (22 chars EN) | ~34 chars (2 lines max) | "Impossible de charger le niveau." (FR: 33 chars) — fits on 2 lines |
| Error overlay — secondary | "Tap anywhere to retry." (23 chars EN) | ~35 chars (1 line at 14sp on 390pt screen) | "Appuyez n'importe où pour réessayer." (FR: 37 chars) — may wrap to 2 lines on narrow screens (iPhone SE 375pt). Design for 2-line fallback. |

**Layout-critical strings:** Error overlay text only. Both strings should be authored with a 35-character cap guidance for translators. No other text in the HUD is layout-critical.

**Right-to-left (RTL):** For Arabic / Hebrew locales (future), the layout zone arrangement inverts: coin chip to top-right, move counter to top-center (stays centered), undo to bottom-right, hint to bottom-left. RTL layout deferred to localization sprint. Flag for `SafeAreaPanel` RectTransform mirroring.

---

## Acceptance Criteria

All blocking ACs from HUD GDD (AC-01 through AC-35) are inherited. The following UX-layer ACs supplement them:

**Layout & Safety**
- [ ] AC-UX-01 [BLOCKING] On iPhone 14 Pro (Dynamic Island device): all HUD elements visible above Dynamic Island and below home indicator; no clipping at any safe area edge
- [ ] AC-UX-02 [BLOCKING] On Samsung Galaxy A14 (Android gesture nav): all HUD elements visible above gesture pill; undo and hint buttons tappable without triggering Android back gesture
- [ ] AC-UX-03 [BLOCKING] Undo button hit area ≥44pt (iOS) / ≥48dp (Android) measured on-device with overlay tool
- [ ] AC-UX-04 [BLOCKING] Hint button hit area ≥44pt / ≥48dp

**Button Feedback**
- [ ] AC-UX-05 [BLOCKING] Undo button scale-to-94% animation completes ≤60ms from touch receipt (measured via frame profiler on Samsung Galaxy A14)
- [ ] AC-UX-06 [BLOCKING] Hint button scale-to-94% animation completes ≤60ms from touch receipt
- [ ] AC-UX-07 [BLOCKING] Hint button shows spinning arc (2dp CHROME-03, 90°, 1.0s/rev) during HINT_PROCESSING; arc is absent in ENABLED and DISABLED states

**Error State**
- [ ] AC-UX-08 [BLOCKING] On `session_load_failed`: full-screen CHROME-02 overlay appears within 1 frame; move counter and buttons are not visible behind it
- [ ] AC-UX-09 [BLOCKING] Tapping anywhere on the error overlay dismisses it and re-triggers level load (verified by observing `level_loaded` event after tap)
- [ ] AC-UX-10 [BLOCKING] If retry also fails, error overlay reappears; no crash, no blank screen

**Visual Language Compliance**
- [ ] AC-UX-11 [ADVISORY] No warm color (amber/orange/red) appears in any HUD element during play states 1–4 (INACTIVE → IDLE → HINT_PROCESSING). Verified by screenshot comparison at each state against Art Bible 4.3 semantic color rules.
- [ ] AC-UX-12 [ADVISORY] CHROME-04 text on CHROME-02 panel achieves ≥4.5:1 contrast ratio (WCAG AA) — verified with automated contrast checker tool at 60% panel opacity over CHROME-01 background

**Pity Grant**
- [ ] AC-UX-13 [BLOCKING] On `coin_balance_changed` with `earn_source == EarnSource.PityGrant`: "Hint restored." toast (Pattern #10) appears above button strip before coin pulse; sequential, not simultaneous (verified by frame-level recording showing toast fade-in completing before coin icon scale begins)

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| OQ-00 — Pause button trigger | Lead programmer | Before pause-menu implementation sprint | Element 6 added to this spec 2026-05-17. Requires GDD update (in-game-hud.md UI Requirements). |
| OQ-01 — GSM payload contracts (delta_move_count, undo_stack_depth in board_state_changed; par_moves in level_complete; level_id in level_loaded) | Lead programmer | Before HUD implementation sprint | ADR-0006 documents these; verify against actual GSM implementation before story sign-off |
| OQ-03 — HINT_PROCESSING visual treatment | ux-designer + art-director | Resolved | Spinning arc, 2dp CHROME-03, 90°, 1.0s/rev, sine in/out — Art Bible 7.4 |
| OQ-06 — Error overlay content and dismiss | ux-designer | Resolved this session | Tap to retry — "Unable to load level." + "Tap anywhere to retry." |
| UI tap SFX clip | audio-director | Before Audio System implementation sprint | Clip for HUD button taps (undo, hint tap-down). Must route to `UIVolume` AudioMixer bus. Distinct from `PlayBoltSettle`. |
| Colorblind bolt toggle UI surface | ux-designer | Settings UI spec session (Launch scope) | Art Bible 4.4 specifies the pattern system; toggle placement deferred to Settings UI spec |
| Error overlay retry cap | game-designer | Before HUD implementation sprint | Current: no cap. Future consideration: show "please restart the app" after 3 failed retries |
