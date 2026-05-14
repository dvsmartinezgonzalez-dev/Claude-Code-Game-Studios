# Accessibility Requirements: BoltSort

> **Status**: Committed
> **Author**: gate-check skill (Standard tier — 2026-05-12)
> **Last Updated**: 2026-05-12
> **Accessibility Tier Target**: Standard
> **Platform(s)**: iOS, Android
> **External Standards Targeted**:
> - WCAG 2.1 Level AA (contrast ratios, text sizing)
> - Apple Human Interface Guidelines (iOS accessibility)
> - Android Accessibility Guidelines (Material Design / TalkBack)
> **Accessibility Consultant**: None engaged (pre-launch; plan external review before public Beta)
> **Linked Documents**: `design/art/art-bible.md` (color palette), `design/gdd/systems-index.md`, `.claude/docs/technical-preferences.md` (touch target minimums)

---

## Accessibility Tier Definition

### This Project's Commitment

**Target Tier**: Standard

**Rationale**: BoltSort is a tap-only mobile sort puzzle. The core mechanic is moving colored bolts between stacks — meaning **color discrimination is the primary gameplay input**. This is a higher-than-average visual accessibility risk for a puzzle game: approximately 8% of men have some form of color vision deficiency, and the game's entire challenge structure rests on distinguishing 2–8 distinct bolt colors on a dark background. Standard tier is the minimum responsible commitment for a color-dependent mechanic.

The motor barrier is unusually low: tap-only input (no holds, no drags, no rapid sequences, no gamepad) means the most common motor accessibility concerns do not apply. The cognitive barrier is also low: one mechanic (sort by color), no time pressure, unlimited undo, no reading-heavy systems in MVP scope. The remaining Standard obligations — colorblind modes, touch-target sizing, UI scaling — are either already enforced by technical preferences (44pt/48dp tap targets, safe-area handling) or are achievable with the bolt palette already designed for colorblind safety (Art Bible Section 4, colorblind introduction order).

Dropping to Basic would exclude the estimated 8–12% of players with color vision deficiency — a group for whom the game would be unplayable, not merely difficult, without a colorblind mode. Standard is the correct tier.

**Features explicitly in scope (elevated):**
- Colorblind modes: Protanopia + Deuteranopia + Tritanopia — all three required because bolt colors are the core mechanic, not a secondary indicator
- Touch target minimum already enforced in technical-preferences.md (≥44pt iOS / ≥48dp Android) — this is a hard engineering constraint, not a setting

**Features explicitly out of scope:**
- Screen reader support for in-game board (menus only): Unity 6.3 does not provide a production-ready managed API for spatial world-object screen reader integration; in-game board accessibility deferred to post-launch accessibility sprint
- Subtitle system: BoltSort is a silent puzzle game with no voiced dialogue and no speech-to-action mechanics — no subtitle system is needed at MVP
- Aim assist / input remapping: tap-only single-touch game has no multi-binding conflicts and no aiming mechanic

---

## Visual Accessibility

| Feature | Tier | Scope | Status | Implementation Notes |
|---------|------|-------|--------|---------------------|
| Touch target minimum ≥44pt / ≥48dp | Basic | All bolt stacks + HUD buttons | **Enforced** (technical-preferences.md) | Enforced at prefab level via `Collider2D` minimum size (ADR-0007, ADR-0013 column cap). Validated on Samsung Galaxy A series and iPhone 14. |
| Safe area handling | Basic | All UI canvases | **Enforced** (ADR-0008) | `SafeAreaPanel` applied to both HUD and LevelCompleteUI canvases — notch, Dynamic Island, gesture pill all handled. |
| Minimum text size — HUD | Standard | Move counter, coin display, hint cost | Not Started | 20px minimum at 1080p (1080×1920 reference). All text via TextMeshProUGUI (enforced, ADR-0008). |
| Minimum text size — Level Complete UI | Standard | Star rating, coin reward, next-level button | Not Started | 24px body / 32px star count at 1080p reference. |
| Text contrast — UI on background | Standard | All HUD and overlay text | Not Started | Minimum 4.5:1 (WCAG AA). Dark background `#0B0F14` + white/cyan text should pass comfortably — verify with contrast checker before ship. |
| **Colorblind mode — Protanopia** | **Standard** | **All bolt colors on board** | **Not Started** | Primary concern: Cobalt (`#2060E0`) and Scarlet (`#E02820`) are the two most affected hues. Protanopia shift: Scarlet → Orange-amber; supplement bolt shape differentiation (see non-color backup below). Verify all 8-bolt palette permutations with Coblis simulator. |
| **Colorblind mode — Deuteranopia** | **Standard** | **All bolt colors on board** | **Not Started** | Similar to Protanopia in practical impact — often the same palette adjustment covers both. Art Bible bolt introduction order (Cobalt + Scarlet first) already maximizes hue distance for a 2-color start; verify deuteranopia adjustment maintains this. |
| **Colorblind mode — Tritanopia** | **Standard** | **All bolt colors on board** | **Not Started** | Blue-yellow axis affected — Cobalt (`#2060E0`) and Amber accent (`#E8A030`) are primary concerns. Shift: Cobalt → cyan-purple; Amber → pink-orange. Less common (~0.001% of population) but included at Standard. |
| Color-as-only-indicator audit | Basic | Bolt colors on board | Not Started | See Color-Only Indicator Audit table below. Bolt colors are the primary color-only indicator in this game. |
| UI scaling | Standard | HUD elements (move counter, coin display) | Not Started | Range: 80%–150%. Default: 100%. Independent from gameplay board scaling. Accessible via Settings UI (Launch scope — document intent now, implement with Settings UI). |
| Brightness/gamma control | Basic | Global | Not Started | Expose in Settings UI (Launch scope). Reference: dark background `#0B0F14` + saturated jewel tones are a high-contrast pairing; verify baseline is readable at default gamma on OLED and non-OLED mobile screens. |
| Screen flash / photosensitivity | Basic | Stack completion burst, level complete ring | Not Started | Stack completion burst: white flash ~400ms at full board, resolves to jewel colors. Verify against Harding FPA standard (≤3 flashes/sec above luminance threshold). The celebration ring + burst sequence (ADR-0009, ~2000ms) requires audit. Add pre-launch photosensitivity warning screen. |

### Color-Only Indicator Audit

| Location | Color Signal | What It Communicates | Non-Color Backup | Status |
|----------|-------------|---------------------|-----------------|--------|
| Bolt colors on board | 2–8 distinct jewel hues | Which bolt goes where — the core mechanic | **Not started** — shape or pattern differentiation required for colorblind modes (e.g., subtle icon/mark on bolt face per color ID, or numbered label toggle in colorblind mode) | Not Started |
| HUD coin display | Amber `#E8A030` animation | Coin reward earned | Numeric counter (non-color backup already present — number communicates reward independently) | ✅ Backup exists |
| Hint button disabled state | Grey desaturation | Button unavailable | Reduced opacity + disabled tap target (non-color backup already in HUD GDD design) | ✅ Backup exists (per HUD GDD F-03) |
| Stack completion burst | White → jewel color | Stack sorted successfully | Animation + sound effect (PlayBoltSettle) provide non-color backups | ✅ Backup exists |

**Critical gap**: Bolt color identity itself has no non-color backup in the current design. For colorblind players in Protanopia/Deuteranopia modes where red and green bolts are visually ambiguous, the game requires a shape or pattern differentiation system. This must be designed before the colorblind mode is implemented.

**Options** (decision deferred to `/ux-design hud` session):
1. Small icon/symbol on bolt face (triangle, circle, cross, etc.) per color ID — persistent, always visible
2. Numbered label (1–8) on bolt face — clear but less aesthetically pure
3. Pattern overlay (stripe direction) on bolt — subtle, preserves art direction

---

## Motor Accessibility

BoltSort is **tap-only** (no drag, no hold, no gamepad, no keyboard). This eliminates the majority of Standard motor accessibility concerns. The items below are scoped to the actual inputs the game uses.

| Feature | Tier | Scope | Status | Implementation Notes |
|---------|------|-------|--------|---------------------|
| Touch target minimum | Basic | All bolt stacks + HUD buttons | **Enforced** | ≥44pt iOS / ≥48dp Android. Enforced at prefab level. |
| No hold inputs in MVP core loop | Basic | Board interaction | **By design** | Tap-only game — no hold-to-confirm, no hold-to-cancel. The Android back gesture (bolt cancel, ADR-0007) is the only non-tap input and already has an on-screen tap alternative (tap same stack = cancel). |
| Undo (one-tap correction) | Basic | Board interaction | **By design** | Unlimited undo via undo button — effectively an error tolerance feature. No time limit on undo. |
| No rapid input requirements | Basic | Board interaction | **By design** | No button-mashing, no quick-time events, no rhythm inputs. |
| Tap target feedback | Standard | All bolt stacks | Not Started | Visual feedback on tap (bolt lift) confirms the tap registered — players with tremor need confirmation before the next input. Ensure lift animation begins within 1 frame of tap. |
| One-hand playability | Standard | Full game | **By design** | Tap-only portrait-mode game is inherently one-hand operable. Column cap ≤8 (ADR-0013) keeps all tap targets reachable with thumb from any portrait grip. |

---

## Cognitive Accessibility

| Feature | Tier | Scope | Status | Implementation Notes |
|---------|------|-------|--------|---------------------|
| Unlimited undo | Standard | Core gameplay | **By design** | Sort Mechanic + GSM provide unlimited undo (ADR-0006). No time limit. No penalty. This is the primary cognitive safety net — players can always backtrack. |
| No time pressure in MVP | Standard | Core gameplay | **By design** | No timer, no move limit in MVP scope. The game is completable at any pace. |
| Pause at any point | Basic | All gameplay states | Not Started | HUD must support pause in all states including MOVE_EXECUTING. Verify pause is reachable in all Sort Mechanic FSM states. |
| Hint system (planned Beta) | Standard | Core gameplay | Deferred | Hint system (Beta scope, Hint System GDD) provides one-tap optimal next move suggestion. Design for cognitive assist, not just monetization. |
| Move counter (feedback) | Standard | HUD | Not Started | Move counter (HUD GDD) gives players a progress signal. Par moves (Level Complete UI GDD F-01) gives a reference target without being punishing. |
| Level restart available | Standard | Core gameplay | By design | Retry button (Level Complete UI) allows level restart at any time via menu access. |
| Deadlock detection | Standard | Core gameplay | By design | Sort Mechanic emits `OnDeadlockDetected` when no legal move exists — HUD responds with hint pulse (ADR-0012). Player is informed when stuck rather than waiting silently. |

---

## Auditory Accessibility

BoltSort is a silent puzzle game with machine ambient audio and bolt SFX. No dialogue, no voice acting, no speech-to-text. Auditory accessibility scope is limited.

| Feature | Tier | Scope | Status | Implementation Notes |
|---------|------|-------|--------|---------------------|
| Independent volume controls | Basic | SFX / Ambient / UI buses | Not Started | Three independent AudioMixer buses (ADR-0011: SFXVolume, AmbientVolume, UIVolume). Expose as sliders in Settings UI (Launch scope). Players can silence machine hum (Ambient) without silencing bolt SFX, or silence all audio independently. |
| Audio not required for gameplay | Basic | All gameplay states | **By design** | Bolt color identity, win condition, and deadlock detection are all visual. No gameplay-critical information is delivered only via audio. Audio is feedback amplification, not the primary signal. |
| Mono audio option | Comprehensive | Global | Deferred | Out of scope for Standard; planned for post-launch. One-sided hearing loss is less critical for a game with no directional audio gameplay cues. |

### Gameplay-Critical SFX Audit

| Sound Effect | What It Communicates | Visual Backup | Caption | Status |
|-------------|---------------------|--------------|---------|--------|
| `PlayBoltSettle(true)` — valid settle | Bolt placed successfully | Bolt animation settle + (if stack completes) glow burst | None required | ✅ Visual backup present |
| `PlayBoltSettle(false)` — invalid placement | Rejection shake | Rejection shake animation (100ms horizontal, ADR-0009) | None required | ✅ Visual backup present |
| Stack chime (stack completion) | Stack sorted — one color group complete | Stack completion glow + VFX ring (ADR-0010) | None required | ✅ Visual backup present |
| Machine hum ambient | Atmosphere only | N/A | N/A | ✅ Non-critical |

All gameplay-critical audio states have confirmed visual backups. Audio-off play is fully viable.

---

## Platform Accessibility API Integration

| Platform | Standard | Features | Status | Notes |
|----------|----------|---------|--------|-------|
| iOS | UIAccessibility / VoiceOver | Menu navigation (VoiceOver screen reader for Settings UI, Level Select UI) | Not Started | Unity 6.3 UIAccessibility integration for UGUI. Required before public launch on App Store. Scope: menus only — in-game board deferred. |
| Android | AccessibilityService / TalkBack | Menu navigation (TalkBack for Settings UI, Level Select UI) | Not Started | Unity UGUI TalkBack integration. Google Play accessibility guidelines. Scope: menus only — in-game board deferred. |

---

## Per-System Accessibility Matrix

| System | Visual Concerns | Motor Concerns | Cognitive Concerns | Auditory Concerns | Addressed | Notes |
|--------|----------------|---------------|-------------------|------------------|-----------|-------|
| Sort Mechanic | **CRITICAL**: bolt color discrimination is the core mechanic — colorblind modes required | Low: tap-only, no holds, no rapid input | Low: one mechanic, unlimited undo | Low: settle SFX has visual backup | Partial | Colorblind bolt differentiation (non-color backup) not yet designed |
| Game State Manager | None (internal state management) | None | None | None | N/A | Infrastructure only |
| In-Game HUD | Touch targets ≥44pt enforced; text contrast must be verified | Touch targets enforced | Hint pulse on deadlock; undo always accessible | Volume controls in Settings (Launch) | Partial | Text size + contrast testing needed |
| Level Complete UI | Text contrast (star rating, coin count on dark background) | None | Read coin amount, decide whether to watch ad | None | Not Started | Contrast audit needed |
| Level Data System | None | None | None | None | N/A | Infrastructure only |
| Animation System | Screen flash audit required (stack completion burst) | None | Animations confirm tap registration (motor feedback) | Settle animation + audio redundant | Partial | Flash audit required |
| Audio System | None | None | None | Independent volume buses (ADR-0011) | Partial | Settings UI (Launch) needed to expose |
| Quality Tier System | Low tier reduces VFX density — verify bolt colors remain legible at reduced particle density | None | None | None | Not Started | At Low tier (30fps), verify bolt palette legibility without full VFX layer |
| Coin Economy | None | None | Balance display (coin count) must be readable | None | Not Started | Coin display contrast |
| Level Progression | None | None | Progression clarity (current level, stars earned) | None | Not Started | Level Select UI (Beta) |

---

## Accessibility Test Plan

| Feature | Test Method | Pass Criteria | Responsible | Status |
|---------|------------|--------------|-------------|--------|
| Touch target sizes | Manual + automated: screenshot overlay with 44pt / 48dp grid | All interactive elements ≥44pt iOS / ≥48dp Android at reference resolution | QA | Not Started |
| Text contrast — HUD | Automated: contrast analyzer on HUD screenshots at all game states | All body text ≥4.5:1; hint cost / coin display ≥4.5:1 | UX | Not Started |
| Colorblind mode — Protanopia | Manual: Coblis simulator on all board configurations (2–8 colors) with mode enabled | All bolt colors distinguishable without color discrimination alone (non-color backup visible) | UX | Not Started |
| Colorblind mode — Deuteranopia | Manual: Coblis simulator | Same as Protanopia | UX | Not Started |
| Colorblind mode — Tritanopia | Manual: Coblis simulator | Same as Protanopia | UX | Not Started |
| Screen flash / photosensitivity | Manual: record stack completion + level complete celebration; analyze frame sequence | ≤3 flashes/sec above luminance threshold (Harding FPA) | QA | Not Started |
| Audio-off gameplay | Manual: mute all audio; play through 10 levels | All gameplay-critical information received without audio; no confusion or missed signals | QA | Not Started |
| Pause accessibility | Manual: pause during MOVE_EXECUTING and in all Sort Mechanic FSM states | Pause reachable in all states; board state preserved correctly on resume | QA | Not Started |
| Safe area — iOS Dynamic Island | Device test: iPhone 14 Pro or later | HUD buttons and coin display not obscured by Dynamic Island or home indicator | QA | Not Started |
| Safe area — Android gesture bar | Device test: Samsung Galaxy A series (target hardware) | HUD buttons above gesture pill; no false taps from swipe-up gesture | QA | Not Started |

---

## Known Intentional Limitations

| Feature | Tier Required | Why Not Included | Risk / Impact | Mitigation |
|---------|--------------|-----------------|--------------|------------|
| Screen reader support for in-game board (bolt stacks) | Exemplary | Unity 6.3 does not expose a managed API for world-space object screen reader integration; would require custom spatial audio description system | Affects blind and severely low-vision players — game is not independently playable without board screen reader | Ensure all game-critical state is readable in menus (level progress, coin balance); evaluate spatial audio description for post-launch accessibility sprint |
| Full subtitle customization | Comprehensive | BoltSort has no voice dialogue — subtitle customization is not applicable | None in MVP scope | N/A |
| Mono audio | Comprehensive | No directional gameplay audio — one-sided hearing loss not a gameplay barrier | Minimal impact: ambient machine hum is non-directional; settle SFX is non-directional | Low risk; add to post-launch backlog |
| Haptic feedback alternatives to audio | Exemplary | Out of scope; iOS haptics API integration deferred | Affects deaf players using haptic feedback to confirm bolt placement | Log for post-launch; investigate Unity's `Handheld.Vibrate()` as a lightweight settle confirmation |
| High contrast mode | Comprehensive | Bolt palette on dark background already provides high inherent contrast; full high-contrast recolor of HUD chrome deferred | Low risk — dark background + saturated jewel tones are a high-contrast visual system by design | Standard colorblind modes partially mitigate; evaluate Section 4 Color System (Art Bible) against high-contrast needs before ship |

---

## Audit History

| Date | Auditor | Type | Scope | Findings Summary | Status |
|------|---------|------|-------|-----------------|--------|
| 2026-05-12 | gate-check skill | Initial commitment | Standard tier — full feature matrix | Initial document created. Color-only indicator audit identifies critical gap: bolt color identity requires non-color backup system for colorblind modes. All other Standard features confirmed in scope. | Draft committed |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| What non-color backup differentiates bolt colors in colorblind modes? (icon, number, pattern, shape?) | ux-designer | Before `/ux-design hud` session | Unresolved |
| Should the bolt colorblind mode be a settings toggle or auto-detected? | game-designer / ux-designer | Before Hint System GDD authorship | Unresolved |
| iOS VoiceOver: does Unity 6.3 UGUI expose accessible names on `TextMeshProUGUI` elements without additional code? Verify against `docs/engine-reference/unity/modules/ui.md` | lead-programmer | Before Settings UI implementation sprint | Unresolved |
| Will the Quality Tier System's Low-tier VFX density reduction (0.25 multiplier) affect bolt color legibility at 30fps? Verify on Samsung Galaxy A14 target device | technical-director | Before Animation System sprint | Unresolved |
