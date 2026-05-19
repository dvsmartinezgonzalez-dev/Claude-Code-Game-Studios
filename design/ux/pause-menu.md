# UX Spec: Pause Menu

> **Status**: Committed
> **Author**: ux-design skill — 2026-05-17
> **Last Updated**: 2026-05-17
> **Journey Phase(s)**: Mid-session interruption
> **Template**: UX Spec
> **Milestone**: MVP
> **Input Method**: Touch only (tap — no drag, no hold, no gamepad)
> **Accessibility Tier**: Standard (see `design/accessibility-requirements.md`)
> **Source GDDs**: `design/gdd/in-game-hud.md`, `design/gdd/level-progression.md`
> **Source ADRs**: ADR-0008 (UI hierarchy / safe area), ADR-0012 (HUD patterns)
> **Patterns Used**: #6 (Button Press Feedback), #8 (Disabled State) — new pattern needed: Destructive Action Confirm (see OQ-02)

---

## Purpose & Player Need

The Pause Menu interrupts gameplay cleanly. It serves three player needs: (1) step away temporarily and resume later without losing board state, (2) abandon the current level attempt and return to the main menu, (3) restart the current level from scratch. The screen must make RESUME the obvious first choice — not because it's forced, but because the design makes it the path of least resistance.

This screen must never upsell, guilt, or create friction around leaving. Per Pillar 3 (Respect the Session): the player paused; that was their decision; honor it without drama.

**Primary player goal**: Resume the current level after a brief interruption.
**Secondary goals**: Exit to main menu; restart the level.
**What fails without this screen**: Players have no clean way to exit mid-level without losing progress or being forced to kill the app.

---

## Player Context on Arrival

Always mid-gameplay, always voluntary (player tapped the pause button). Emotional state: interrupted, possibly mid-focus. The design assumption is that the player wants to briefly step away — not necessarily quit. Board state is fully preserved by GSM; there is nothing to lose by pausing.

The player never arrives here from a cold launch or from a game event — only from an explicit pause action.

## Navigation Position

Overlay on top of the active game scene. The board remains mounted and visible (dimmed) behind the overlay. This is NOT a separate scene — the pause menu renders on the existing Canvas (z-order: above HUD, below nothing).

```
Root → Main Menu → Gameplay (Game Scene)
                        └── [Pause overlay] ← player taps pause button
```

## Entry & Exit Points

### Entry

| Source | Trigger | State player carries |
|--------|---------|----------------------|
| In-Game HUD | Tap pause button (⚙ or ‖ icon — **to be added to hud.md**) | Current board state (fully preserved in GSM) |
| Android back gesture | During ACTIVE or COMPLETE GSM state (back gesture not in BOLT_SELECTED — that is handled by ADR-0007 as CANCELLATION) | Current board state |

### Exit

| Destination | Trigger | Confirm? | Notes |
|-------------|---------|---------|-------|
| Gameplay (resume) | Tap RESUME | No | Board state unchanged; overlay dismisses |
| Gameplay (resume) | Tap backdrop (outside panel) | No | Same as RESUME — tap-anywhere-to-dismiss |
| Main Menu | Tap EXIT TO MENU → confirm | Yes | LP routes to main menu via `menu_requested` |
| Gameplay (restart) | Tap RESTART → confirm | Yes | LP calls `load_level(current_level_id)` — resets board |

---

## Layout Specification

### Information Hierarchy

Ranked by visual priority:
1. **RESUME button** — primary CTA; full-width, maximum visual weight
2. **Level label ("Level N")** — orientation anchor at top of panel
3. **RESTART LEVEL button** — secondary action; same width as RESUME, less visual weight
4. **EXIT TO MENU button** — tertiary / destructive; visually distinct (lower opacity or outline-only treatment)

No coin balance, no score, no progress data. This screen communicates only the available actions.

### Layout Zones

**Modal overlay** pattern — centered panel on a dimmed backdrop.

| Zone | Content | Notes |
|------|---------|-------|
| Backdrop | Dimmed game board | `CHROME-01` at 72% opacity; tap anywhere on backdrop = RESUME |
| Panel | All pause content | `CHROME-02` fill, 12dp rounded corners (art bible §3 P3); ~80% screen width, auto height |
| Panel header | "Level N" label | `CHROME-04` text, small size, centered |
| Panel body | RESUME, RESTART LEVEL, EXIT TO MENU | Stacked vertically; 16dp gap between buttons |

### Component Inventory

| Component | Zone | Type | Interactive | Pattern |
|-----------|------|------|-------------|---------|
| Backdrop dim | Screen | Non-interactive overlay | Yes (tap = resume) | — |
| Level label ("Level N") | Panel header | Static TextMeshPro | No | — |
| RESUME button | Panel body | Button (primary) | Yes | #6 Button Press Feedback |
| RESTART LEVEL button | Panel body | Button (secondary) | Yes | #6 Button Press Feedback |
| EXIT TO MENU button | Panel body | Button (destructive / outline) | Yes | #6 Button Press Feedback |
| Confirm dialog (modal-within-modal) | Screen | Overlay panel | Yes | New — see OQ-02 |

**RESUME**: `CHROME-03` cyan border + fill at low opacity, `CHROME-04` label. Full panel width.
**RESTART LEVEL**: `CHROME-04` border only (outline), `CHROME-04` label. Same width. Visually lighter.
**EXIT TO MENU**: Same as RESTART treatment — outline only. These two are peers; neither is more dangerous than the other visually, but both trigger a confirm dialog before acting.

### ASCII Wireframe

```
┌─────────────────────────────┐
│ ░░░░░░ DIM BACKDROP ░░░░░░  │  ← tap anywhere = RESUME
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░  │
│                             │
│    ╔═══════════════════╗    │
│    ║    Level 42       ║    │  ← level label (CHROME-04, small)
│    ╠═══════════════════╣    │
│    ║                   ║    │
│    ║     RESUME        ║    │  ← primary (CHROME-03 fill/border)
│    ║                   ║    │
│    ╠───────────────────╣    │
│    ║  RESTART LEVEL    ║    │  ← outline only (CHROME-04 border)
│    ╠───────────────────╣    │
│    ║  EXIT TO MENU     ║    │  ← outline only (CHROME-04 border)
│    ╚═══════════════════╝    │
│                             │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░  │
└─────────────────────────────┘

Confirm dialog (when RESTART or EXIT tapped):
    ╔═══════════════════╗
    ║  Restart level?   ║   (or "Exit to menu?")
    ║  Progress lost.   ║
    ║ [CANCEL] [CONFIRM]║
    ╚═══════════════════╝
```

---

## States & Variants

| State | Trigger | What changes |
|-------|---------|--------------|
| **Default** | Pause button tapped during ACTIVE GSM state | Full panel visible; all three buttons enabled |
| **Confirm: Restart** | Tap RESTART LEVEL | Confirm dialog overlays panel: "Restart level? Your progress will be lost." [CANCEL] [RESTART] |
| **Confirm: Exit** | Tap EXIT TO MENU | Confirm dialog overlays panel: "Exit to main menu?" [CANCEL] [EXIT] |
| **GSM COMPLETE state** | Player taps pause after winning (unlikely but possible if pause triggers before Level Complete fires) | RESTART and EXIT available; RESUME label changes to "BACK" (returns to Level Complete) — or disable pause during COMPLETE |

**COMPLETE state note**: Pause during COMPLETE is an edge case. Simplest resolution: disable the pause button after `OnLevelComplete` fires (HUD enters FROZEN state per `hud.md` — confirm this disables the pause trigger too). Flag in OQ-03.

## Interaction Map

Input: Touch only. No gamepad. No back-gesture customization needed (Android back = RESUME from pause, consistent with OS convention).

| Component | Tap Action | Tap-down Feedback | Outcome |
|-----------|-----------|-------------------|---------|
| Backdrop (dim area) | Tap anywhere outside panel | No visual feedback on backdrop | RESUME — dismiss overlay, return to gameplay |
| RESUME button | Tap | Scale 0.95 → 1.0, 80ms; cyan border brightens (Pattern #6) | Dismiss overlay; GSM resumes (no state change needed — board was never suspended) |
| RESTART LEVEL button | Tap | Scale 0.95 → 1.0, 80ms (Pattern #6) | Show RESTART confirm dialog |
| EXIT TO MENU button | Tap | Scale 0.95 → 1.0, 80ms (Pattern #6) | Show EXIT confirm dialog |
| Confirm dialog — CANCEL | Tap | Scale feedback | Dismiss confirm dialog; return to default pause panel |
| Confirm dialog — RESTART | Tap | Scale feedback | LP calls `load_level(current_level_id)`; pause overlay dismisses; level reloads |
| Confirm dialog — EXIT | Tap | Scale feedback | LP processes `menu_requested`; navigate to Main Menu |
| Android back gesture | System | None | Same as RESUME tap — dismiss overlay |

**Confirm dialog backdrop**: Tapping outside the confirm dialog (but inside the pause panel) = CANCEL. Consistent with tap-outside-to-dismiss established by the pause overlay itself.

## Events Fired

| Player Action | Analytics Event | Payload | State Change |
|---------------|----------------|---------|--------------|
| Pause menu shown | `pause_menu_shown` | `{ level_id: int, move_count: int }` | None (board state unchanged) |
| Tap RESUME | `pause_menu_resume` | `{ level_id: int }` | None |
| Tap RESTART → confirm | `pause_menu_restart_confirmed` | `{ level_id: int, move_count: int }` | LP.LoadLevel called |
| Tap RESTART → cancel | `pause_menu_restart_cancelled` | `{ level_id: int }` | None |
| Tap EXIT → confirm | `pause_menu_exit_confirmed` | `{ level_id: int }` | LP.menu_requested |
| Tap EXIT → cancel | `pause_menu_exit_cancelled` | `{ level_id: int }` | None |

## Transitions & Animations

### Overlay Enter
Scale-from-center: panel scales 0.85 → 1.0, opacity 0 → 1, 180ms ease-out. Backdrop fades to 72% opacity simultaneously.

Rationale: scale-in communicates "something appeared here" more clearly than a slide (which implies navigation). A slide would read as going to a new screen; scale-in confirms it's an overlay.

### Overlay Exit (RESUME)
Scale-out: panel scales 1.0 → 0.85, opacity 1 → 0, 140ms ease-in. Backdrop fades out simultaneously.

### Confirm Dialog Enter / Exit
Same scale pattern, faster: 120ms enter, 100ms exit. Sits above pause panel without dismissing it.

### No in-screen idle animations
The pause overlay has no idle animation — the game board behind it is visible and dimmed. Any looping animation on the pause panel itself would compete visually with the dim board. The logo glow on the Main Menu is appropriate there because it's the primary focal point; here the board is the focal point.

## Data Requirements

| Data | Source | R/W | Notes |
|------|--------|-----|-------|
| `current_level_id` | LevelProgression | Read | Displayed as "Level N" in panel header; passed to `LP.LoadLevel()` on RESTART |
| `move_count` | GameStateManager | Read | Used in analytics event payload only — not displayed on screen |

No writes. The pause menu observes but does not modify game state. GSM board state is preserved automatically while the overlay is visible (no special serialization needed during pause — that's `OnApplicationPause`, a separate path per ADR-0006 SER-01).

## Accessibility

Tier: Standard.

| Requirement | Implementation |
|-------------|---------------|
| Tap targets ≥ 48×48pt | All three buttons are full panel width (~312pt on a 390pt screen) — comfortably pass |
| Confirm dialog buttons ≥ 48×48pt | CANCEL + CONFIRM side by side, each ≥ 48×48pt; 16dp gap between |
| Color not sole differentiator | RESUME vs RESTART/EXIT distinguished by fill (RESUME has cyan fill at low opacity) AND by position (top = primary). Not color only. |
| Screen reader | RESUME: "Resume game"; RESTART LEVEL: "Restart level, requires confirmation"; EXIT TO MENU: "Exit to main menu, requires confirmation" |
| Confirm dialog screen reader | Dialog role with "Restart level? Your progress will be lost." announcement on open. CANCEL before CONFIRM in focus order (safer default). |
| Reduced motion | Scale transitions can be suppressed; opacity-only fade is the fallback |
| Back gesture | Android system back = RESUME — this is the expected OS behavior; no override needed |

## Localization Considerations

| Element | English | Max expected | Concern |
|---------|---------|-------------|---------|
| "RESUME" | 6 chars | "REPRENDRE" (FR, 9) | LOW — button is full panel width |
| "RESTART LEVEL" | 13 chars | "NIVEAU RECOMMENCER" (FR, 18) | MEDIUM — may need to wrap; allow 2-line button label at reduced font size |
| "EXIT TO MENU" | 12 chars | "QUITTER LE MENU" (FR, 15) | LOW |
| "Level N" (panel header) | 7–9 chars | "Niveau N" (DE/FR, 8–9) | LOW |
| Confirm: "Restart level? Your progress will be lost." | 43 chars | ~60 chars in DE/FR | MEDIUM — confirm dialog must accommodate 3 lines of body text |

## Acceptance Criteria

- [ ] **[Trigger]** Pause menu appears when the pause button is tapped in the HUD during ACTIVE GSM state; game board is visible and dimmed behind the overlay
- [ ] **[Resume — primary]** Tapping RESUME dismisses the overlay and gameplay resumes immediately (board state unchanged, move count unchanged)
- [ ] **[Resume — backdrop]** Tapping anywhere on the dimmed backdrop (outside the panel) produces the same result as tapping RESUME
- [ ] **[Android back]** Android back gesture while pause menu is open = RESUME (not app exit, not navigate-back)
- [ ] **[Restart — confirm]** Tapping RESTART LEVEL shows the confirm dialog; tapping CANCEL returns to the pause panel without action; tapping RESTART reloads the current level with a fresh board
- [ ] **[Exit — confirm]** Tapping EXIT TO MENU shows the confirm dialog; tapping CANCEL returns; tapping EXIT navigates to Main Menu
- [ ] **[Accessibility]** All three pause panel buttons have accessibility labels readable by VoiceOver/TalkBack; confirm dialog announces its content on open
- [ ] **[Accessibility]** Tap targets for all buttons (including confirm dialog) ≥ 48×48pt
- [ ] **[Performance]** Pause overlay appears within 100ms of the pause button tap
- [ ] **[Edge case]** Tapping pause button during LOADING or COMPLETE GSM state either (a) does nothing (button disabled) or (b) displays the panel with RESUME disabled — never crashes

## Open Questions

| ID | Question | Owner | Blocking |
|----|---------|-------|---------|
| OQ-01 | **CRITICAL: No pause button exists in `design/ux/hud.md` or `design/gdd/in-game-hud.md`.** Before this spec can be implemented, a pause trigger must be added to the HUD. Proposed: small icon button (⚙ or ‖) in the top-right corner of the HUD, 48×48pt, below the safe area top. Requires an update to `hud.md` Component Inventory and Interaction Map. | UX Lead / HUD implementer | YES — blocks pause menu story |
| OQ-02 | Confirm dialogs are a new pattern not yet in `design/ux/interaction-patterns.md`. Should the confirm dialog be formalized as a reusable pattern (e.g., "Pattern #14: Destructive Action Confirm") or is it one-off? | UX Lead | Before implementation — not before spec approval |
| OQ-03 | Should the pause button be disabled after `OnLevelComplete` fires (during Level Complete UI flow)? Currently `hud.md` specifies the HUD enters FROZEN state on level complete, but doesn't explicitly address the pause button. Proposed: pause button is disabled (Pattern #8) in FROZEN state. | HUD implementer | Before implementation |
| OQ-04 | No player journey map exists (`design/player-journey.md`). Arrival emotional state above is assumed. | UX / Producer | No |
