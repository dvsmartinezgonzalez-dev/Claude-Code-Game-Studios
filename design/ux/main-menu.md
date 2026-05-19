# UX Spec: Main Menu

> **Status**: Committed
> **Author**: ux-design skill — 2026-05-17
> **Last Updated**: 2026-05-17
> **Journey Phase(s)**: Session start / Return from gameplay
> **Template**: UX Spec
> **Milestone**: Beta
> **Input Method**: Touch only (tap — no drag, no hold, no gamepad)
> **Accessibility Tier**: Standard (see `design/accessibility-requirements.md`)
> **Source GDDs**: `design/gdd/level-progression.md`, `design/gdd/coin-economy.md`
> **Source ADRs**: ADR-0008 (UI hierarchy / safe area), ADR-0012 (HUD business logic patterns)
> **Patterns Used**: #6 (Button Press Feedback), #8 (Disabled State), #10 (Contextual Toast), #12 (Live Balance with Pulse)

---

## Purpose & Player Need

The Main Menu is BoltSort's session entry point. It serves one primary need: let the player resume play in a single tap. Secondary needs are orientation (where am I in the game?) and economic awareness (what's my coin balance?). The screen must feel like an airlock — calm, purposeful, minimal — not a destination in itself.

**Primary player goal**: Tap PLAY and begin the next level immediately.
**Secondary goals**: Check progress level number; check coin balance before deciding whether to visit the shop (Beta+).
**What fails without this screen**: Cold launch has no entry point; post-level "menu" navigation has nowhere to land.

The screen does NOT exist to monetize, upsell, or retain — those moments belong to Level Complete UI and (Beta) Shop UI.

---

## Player Context on Arrival

Two arrival contexts:

**1. Cold launch** (app opened from home screen / app switcher)
- Player was last in-game or has not played before
- Emotional state: ready, expectant, mildly impatient — they want to play, not read
- Carries: current level ID (from SaveSystem), coin balance (from CoinEconomy)
- Design assumption: minimize steps between launch and first game interaction

**2. Post-level navigation** (`menu_requested` processed by LevelProgression)
- Player tapped "Menu" from Level Complete UI
- Emotional state: satisfied (just completed a level) or tired (abandoned a hard level)
- Carries: current level ID updated to next (if completed), or unchanged (if abandoned); coin balance updated
- Design assumption: player may want a brief pause before playing again — the menu is a breath, not a speedbump

---

## Navigation Position

Root screen — no parent. All navigation flows out from here.

```
[App Launch / menu_requested]
        ↓
   MAIN MENU  ──── PLAY ──────────────────────► Gameplay (current level)
                ── LEVELS (Beta) ────────────► Level Select UI
                ── SHOP / coin tap (Beta) ───► Shop UI
                ── SETTINGS icon (Launch) ───► Settings UI
```

## Entry & Exit Points

### Entry

| Source | Trigger | State player carries |
|--------|---------|----------------------|
| App cold launch | OS opens app | `current_level_id`, `coin_balance` read from SaveSystem |
| Level Progression (post-level) | `menu_requested` event processed | Updated `current_level_id` (if completed), updated `coin_balance` |

### Exit

| Destination | Trigger | Milestone | Notes |
|-------------|---------|-----------|-------|
| Gameplay — current level | Tap PLAY button | MVP | LP calls `load_level(current_level_id)` |
| Level Select UI | Tap LEVELS button | Beta | Button visible but may be locked/hidden at MVP |
| Shop UI | Tap SHOP button or coin display area | Beta | Button visible but locked at MVP |
| Settings UI | Tap SETTINGS icon | Launch | Icon visible; tap does nothing at MVP (or shows "coming soon" toast) |

---

## Layout Specification

### Information Hierarchy

Ranked by visual priority (1 = highest):

1. **PLAY button** — primary CTA; the screen exists for this one action
2. **Current level number** — answers "where am I?" at a glance
3. **Coin balance** — answers "can I afford a hint?"
4. **Game logo / wordmark** — brand anchor; confirms app identity on cold launch
5. **Secondary navigation** (Levels, Shop, Settings) — discoverable but never competing for attention with PLAY

### Layout Zones

Zone arrangement: **A — Logo top, context mid, PLAY center, nav bottom**

| Zone | Position | Content | Height |
|------|----------|---------|--------|
| Safe area top | Screen top inset | Empty (notch clearance) | `Screen.safeArea.y` |
| Header | Below safe top | Game logo / wordmark | ~15% screen height |
| Context | Below header | Level number + coin balance | ~12% screen height |
| Primary action | Screen center | PLAY button | ~14% screen height |
| Filler | Above/below PLAY | Breathing room | remaining |
| Secondary nav | Above safe bottom | LEVELS · SHOP · ⚙ icons | ~10% screen height |
| Safe area bottom | Screen bottom inset | Empty (home indicator clearance) | `Screen.safeArea.height` |

All zones respect `Screen.safeArea` per ADR-0008. Canvas: Screen Space-Overlay.

### Component Inventory

| Component | Zone | Type | Interactive | Milestone | Pattern |
|-----------|------|------|-------------|-----------|---------|
| Game wordmark / logo | Header | Static image or TextMeshPro | No | MVP | — |
| Level label (`"Level N"`) | Context | Static TextMeshPro | No | MVP | — |
| Coin balance (`"◆ N"`) | Context | Event-driven TextMeshPro | No (read-only) | MVP | #11 Event-Driven Counter |
| PLAY button | Primary action | Button + TextMeshPro | Yes | MVP | #6 Button Press Feedback |
| LEVELS button | Secondary nav | Button (disabled at MVP) | Beta | Beta | #8 Disabled State |
| SHOP button | Secondary nav | Button (disabled at MVP) | Beta | Beta | #8 Disabled State |
| SETTINGS icon (⚙) | Secondary nav | Button (no-op at MVP) | Launch | Launch | #8 Disabled State |

**Tap target minimums**: PLAY ≥ 56×56pt recommended (actual target is ~280×64pt — well over minimum). Secondary nav buttons: ≥ 48×48pt each with padding.

### ASCII Wireframe

Portrait, ~390×844pt (iPhone 14 base reference):

```
┌─────────────────────────────┐
│  ░░░░░ SAFE AREA TOP ░░░░░  │
│                             │
│                             │
│       ╔═══════════╗         │
│       ║ BOLTSORT  ║         │  ← wordmark / logo (glow accent)
│       ╚═══════════╝         │
│                             │
│         Level 42            │  ← CHROME-04 text, tabular numerals
│         ◆  320              │  ← coin icon (CHROME-05 amber) + balance
│                             │
│                             │
│   ╔═══════════════════╗     │
│   ║       PLAY        ║     │  ← full-width, CHROME-03 cyan stroke
│   ╚═══════════════════╝     │
│                             │
│                             │
│  [LEVELS]  [SHOP]   [⚙]    │  ← 48dp tap targets, CHROME-04 / disabled
│  ░░░░░ SAFE AREA BOT ░░░░░  │
└─────────────────────────────┘
```

Visual notes:
- Background: `CHROME-01` (`#0B0F14`) — full bleed
- Logo: glow emission at 0.4 idle (same glow lifecycle as bolt idle state, per art bible §2)
- PLAY: `CHROME-03` cyan border, 8dp rounded corners, `CHROME-04` label — on tap: border brightens to full cyan, immediate scale feedback (see Pattern #6)
- Level / coins: `CHROME-04` body text, tabular numeral font; coin icon uses `CHROME-05` amber (same as Level Complete accent — establishes amber = economy)
- Secondary nav: `CHROME-04` at 60% opacity when MVP-inactive; no glow; no disabled badge needed (they look present but dim)

---

## States & Variants

| State | Trigger | What changes |
|-------|---------|--------------|
| **Default** | SaveSystem ready, `current_level_id` and `coin_balance` populated | All components visible and interactive per wireframe |
| **Loading** | Cold launch, SaveSystem not yet ready (background file read in progress) | Level label: `"Level —"`, coin: `"◆ —"`, PLAY button disabled (Pattern #8); spinner optional on coin row |
| **First install** | `current_level_id == 1`, no completion record, `coin_balance == 150` (starter grant) | No special visual variant — default layout, level shows `"Level 1"`, coins show `"◆ 150"`. Starter grant fires on `OnSaveReady` per CE-11. |
| **Beta nav active** | Beta build flag or milestone | LEVELS + SHOP buttons receive full opacity + cyan stroke; tap-enabled |
| **SaveSystem failure** | `OnSessionLoadFailed` emitted | PLAY disabled; level/coin show `"—"`; toast: `"Unable to load progress. Tap to retry."` (Pattern #10 Contextual Toast). Retry calls `SaveSystem.Reload()` if exposed, or prompts app restart. |

**Loading state note**: The loading window is brief on modern hardware (file read <100ms typical). The loading state is only visible on slow devices or corrupt cache. PLAY must not be tappable until `SaveSystem.IsReady` — a tap during loading would call `LP.LoadLevel(0)` (invalid level ID).

---

## Interaction Map

Input method: Touch only (tap — no drag, no hold, no gamepad). Portrait lock.

| Component | Tap Action | Tap-down Feedback | Outcome |
|-----------|-----------|-------------------|---------|
| PLAY button | Tap | Cyan border brightens to full `CHROME-03`; scale 0.95 on down, 1.0 on up — 80ms (Pattern #6 Button Press Feedback) | `LP.LoadLevel(current_level_id)` → gameplay scene loads |
| LEVELS button (Beta) | Tap | Same scale feedback | Navigate to Level Select UI |
| LEVELS button (MVP) | Tap | Opacity pulse 60%→80%→60%, 150ms | No-op (button is informational — visually present, non-functional) |
| SHOP button (Beta) | Tap | Same scale feedback | Navigate to Shop UI |
| SHOP button (MVP) | Tap | Same opacity pulse | No-op |
| SETTINGS icon (Launch) | Tap | Same scale feedback | Navigate to Settings UI |
| SETTINGS icon (MVP/Beta) | Tap | Same opacity pulse | No-op |

**Disabled PLAY** (loading state): Tap produces no response — Pattern #8 Disabled State. No opacity pulse. No toast.

## Events Fired

| Player Action | Analytics Event | Payload | State Change |
|---------------|----------------|---------|--------------|
| Screen displayed | `main_menu_shown` | `{ source: "cold_launch" \| "menu_requested", level_id: int, coin_balance: int }` | None |
| Tap PLAY | `main_menu_play_tapped` | `{ level_id: int, coin_balance: int }` | LP.LoadLevel called |
| Tap LEVELS (Beta) | `main_menu_levels_tapped` | `{ level_id: int }` | Navigate |
| Tap SHOP (Beta) | `main_menu_shop_tapped` | `{ coin_balance: int }` | Navigate |

MVP no-op taps (LEVELS, SHOP, SETTINGS) fire no events — silent non-action.

## Transitions & Animations

### Screen Enter

| Source | Transition | Duration |
|--------|-----------|----------|
| Cold launch (from splash/black) | Fade in from `CHROME-01` black | 250ms |
| Post-level (`menu_requested`) | Slide in from right (reverse of gameplay → LC UI direction) | 200ms ease-out |

### Screen Exit

| Destination | Transition | Duration |
|-------------|-----------|----------|
| Gameplay (PLAY tapped) | PLAY button scale burst (1.0 → 1.05 → scene cross-fade) | 80ms + 300ms cross-fade |
| Level Select UI | Slide out to left | 200ms ease-in |
| Shop UI | Slide out to left | 200ms ease-in |

### In-Screen Animations

| Element | Behavior | Trigger |
|---------|---------|---------|
| Logo wordmark | Idle glow pulse at emission 0.4 (art bible §2 P2 — resting bolt glow lifecycle) | Always-on, looping |
| Coin balance | Pulse on value change (Pattern #12 Live Balance with Pulse) | `OnCoinBalanceChanged` event |
| PLAY button | No idle animation — static, inviting | Idle state |

---

## Data Requirements

Main Menu is read-only — it consumes state but never writes it.

| Data | Source System | Interface | R/W | Notes |
|------|--------------|-----------|-----|-------|
| `current_level_id` | LevelProgression | `LP.CurrentLevelId` (read property) | Read | Displayed as "Level N"; passed to `LP.LoadLevel()` on PLAY tap |
| `coin_balance` | CoinEconomy | `CE.GetCoinBalance()` + `OnCoinBalanceChanged` event | Read | Initial value read on `Awake`; subscribes to event for live updates (Pattern #12). Subscribe in `Awake`, unsubscribe in `OnDestroy` (ADR-0002 Rule 3). |
| Save readiness | SaveSystem | `SP.IsReady` (bool) | Read | Subscribe-then-check pattern (ADR-0002 Rule 2). PLAY button disabled until `IsReady == true`. |

**Architecture note**: MainMenuUI is a scene-loaded MonoBehaviour at SEO 0 (default). It must use the subscribe-then-check pattern for both `OnSaveReady` and `OnCoinBalanceChanged` — the save system may have already fired before this screen activates on fast devices. See ADR-0002 Rule 2.

---

## Accessibility

Accessibility tier: **Standard** (see `design/accessibility-requirements.md`).

| Requirement | Implementation |
|-------------|---------------|
| Tap targets ≥ 48×48pt | PLAY ≥ 280×64pt (passes easily); nav buttons: ≥ 48×48pt hit area via `RectTransform` padding even if visual is smaller |
| Color not sole differentiator | PLAY disabled state = opacity 0.4 + glow 0.0 (shape + opacity communicate state, not color alone) |
| Contrast ratio ≥ 4.5:1 | CHROME-04 (`#C8D8E8`) on CHROME-01 (`#0B0F14`) = ~12:1. CHROME-03 (`#4DCFEF`) on CHROME-01 = ~8:1. Both pass WCAG AA. |
| Screen reader (TalkBack / VoiceOver) | All interactive elements must have `AccessibilityLabel` set: "Play Level N", "Levels (coming soon)", "Shop (coming soon)", "Settings (coming soon)" |
| No motion required | Idle logo glow is ambient and non-communicative — purely decorative. No information is conveyed by the animation. Reduced-motion mode: suppress idle glow pulse, keep static layout. |
| Focus order | Single interactive screen: PLAY → LEVELS → SHOP → SETTINGS (top-to-bottom visual order) |

## Localization Considerations

| Element | Max chars (English) | Expansion risk | Notes |
|---------|--------------------|--------------|----|
| "PLAY" | 4 chars | LOW — short CTA; German: "SPIELEN" (7) fits in button | Button width is generous |
| "Level N" | 8–10 chars | LOW | Tabular numerals; width scales with number |
| "LEVELS" | 6 chars | MEDIUM — French: "NIVEAUX" (7) | Nav button label; test at 10 chars max |
| "SHOP" | 4 chars | LOW | |
| "◆ N" (coin balance) | Varies | LOW — number only, symbol is not text | Symbol `◆` must be in font or use sprite icon |

Locale format: coin balance uses integer only — no currency symbol, no decimal. Format as plain number (e.g. `320`, not `$320` or `320.00`).

## Acceptance Criteria

- [ ] **[Performance]** Main Menu appears within 500ms of app foreground on mid-range Android (Samsung Galaxy A-series target) when SaveSystem is already ready
- [ ] **[Performance]** PLAY button is tappable within 1500ms of cold launch (covers SaveSystem background read budget)
- [ ] **[Navigation — cold launch]** Tapping PLAY loads the correct level (`current_level_id` from SaveSystem) and transitions to the game board
- [ ] **[Navigation — post-level]** After tapping "Menu" from Level Complete UI, Main Menu displays with the updated level number (post-completion `current_level_id`)
- [ ] **[Loading state]** When SaveSystem is not yet ready, PLAY is visually disabled (opacity 0.4) and tap produces no response
- [ ] **[Error state]** When `OnSessionLoadFailed` fires, level and coin display show `"—"` and a contextual toast appears with "Unable to load progress. Tap to retry."
- [ ] **[Data accuracy]** Coin balance displayed matches `CE.GetCoinBalance()` at time of display and updates without a screen refresh when `OnCoinBalanceChanged` fires
- [ ] **[Accessibility]** PLAY button tap target ≥ 48×48pt (measured via Unity's RectTransform bounds); all nav buttons ≥ 48×48pt
- [ ] **[Accessibility]** VoiceOver on iOS reads PLAY button as "Play Level [N]" (not "PLAY" or unlabeled)
- [ ] **[Disabled nav]** Tapping LEVELS, SHOP, or SETTINGS at MVP produces the defined no-op feedback (opacity pulse) and does not navigate or throw an error

## Open Questions

| ID | Question | Owner | Blocking |
|----|---------|-------|---------|
| OQ-01 | No player journey map exists at `design/player-journey.md`. The arrival emotional states above are assumptions. Create journey map to validate. Template: `.claude/docs/templates/player-journey.md`. | UX / Producer | No (can be created post-spec) |
| OQ-02 | Logo / wordmark asset not yet specified. Does "BOLTSORT" use a custom typeface (per art bible §8 Asset Standards) or TextMeshPro with a licensed font? | Art Director | Before implementation |
| OQ-03 | Do disabled MVP nav buttons (LEVELS, SHOP, SETTINGS) show a "Coming soon" tooltip on tap, or simply do nothing? The opacity-pulse no-op is the current spec — confirm this is preferred over a toast. | Game Designer | Before implementation |
| OQ-04 | Should the coin display on the Main Menu subscribe to `OnCoinBalanceChanged` and pulse on update (Pattern #12), or is this screen considered "static" between sessions? Live subscription is specified above but may be unnecessary if the screen is never visible while coins change. | Lead Programmer | Before implementation |
