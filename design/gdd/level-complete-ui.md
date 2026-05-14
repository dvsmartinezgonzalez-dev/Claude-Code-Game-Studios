# Level Complete UI

> **Status**: Designed (pending re-review)
> **Author**: Design session + game-designer + ux-designer + art-director agents
> **Last Updated**: 2026-04-20
> **Implements Pillar**: Flow Over Friction, Respect the Session, Every Pixel Earns Its Place

## Overview

The Level Complete UI is the screen that appears immediately after the Game State Manager emits `level_complete`. It has two jobs: deliver the reward moment (the payoff for solving the puzzle) and return the player to action as fast as possible. It owns four display responsibilities: the star rating (1–3 stars calculated from `move_count` against the level's `par_moves` thresholds), the coin reward earned (derived from the star rating formula, animated as a transfer into the coin balance), the rewarded ad offer (an optional "double your coins" prompt backed by the Rewarded Ad System), and the navigation controls (Next Level, Retry, and Menu). The screen is not a pause — it is a celebration with an exit. It subscribes to `level_complete` from the Game State Manager and to `ad_reward_granted` / `ad_reward_denied` from the Rewarded Ad System. It emits `next_level_requested`, `retry_requested`, and `menu_requested` to Level Progression. Coin reward delivery — the actual mutation of `coin_balance` — is Level Progression's responsibility; this screen only displays the amount and animates the visual transfer. Implementation: UGUI Canvas, Screen Space - Overlay, `Screen.safeArea` anchoring consistent with the In-Game HUD.

## Player Fantasy

The puzzle clicks into place and the machine exhales. Stars arrive one at a time — not as a score, but as a verdict the player already knew was coming. The coins fall into the tally with a satisfying weight, each one earned. There's no ceremony that outstays its welcome: the Next Level button is already there, thumb-ready, before the animation finishes. If I want to watch the stars settle, I can. If I want to move on immediately, nothing stops me. The ad offer — when it appears — feels like a tip jar, not a toll booth: entirely optional, never blocking the exit. The screen doesn't make me feel judged for taking 40 moves. It just shows me what I earned and gets out of the way.

*Primary pillars: Flow Over Friction, Respect the Session*
*MDA target: Achievement (3), Submission (1)*

## Detailed Design

### Core Rules

**Star Rating Display**
- Displays 1, 2, or 3 stars based on `move_count` vs level `par_moves` (formula in Section D)
- Earned stars reveal sequentially — only earned star slots animate (pop animation, `star_reveal_interval_ms` gap between each)
- Unearned star slots appear immediately as empty/dim outlines when the screen opens; they do not animate in sequence
- Always show all 3 slots
- Star reveal begins immediately on screen entry; player does not wait for it to complete before acting

**Coin Reward Display**
- Displays `coins_earned` (integer) derived from star count (formula in Section D)
- Base coin transfer to Level Progression fires immediately on screen entry, regardless of ad offer or navigation speed — the player always receives their earned coins
- Transfer is fire-and-forget: `coin_reward_granted(amount: coins_earned)` emitted to Level Progression at screen entry; Level Progression mutates the balance
- Ad offer, if shown, awards a separate bonus on top: `coin_bonus_granted(amount: coins_earned × (ad_multiplier − 1))` emitted only after `ad_reward_granted`; never replaces or delays the base transfer
- Display shows pending base amount on entry; updates to reflect bonus if ad reward is granted

**Rewarded Ad Offer**
- Shown only when: Rewarded Ad System reports ad is available (`ad_available == true`)
- Offer appears after star reveal completes; never interrupts star animation
- Offer prompt: "Watch an ad — earn bonus coins"
- Player has two options: Watch (enters AD_PROCESSING) or Skip (proceeds to IDLE)
- Skip is always visible and never hidden; ad offer is never a blocking gate
- If ad unavailable: offer is not shown; screen goes directly to IDLE after star reveal

**Navigation Controls**
- Three buttons: Next Level (primary), Retry (secondary), Menu (secondary)
- All three are tappable from the moment the screen appears — do not wait for animations
- Next Level: emits `next_level_requested` to Level Progression
- Retry: emits `retry_requested` to Level Progression
- Menu: emits `menu_requested` to Level Progression
- Tapping any navigation button while AD_PROCESSING is not possible — buttons disabled during ad playback only
- Navigating before ad offer is acted on cancels the offer; base coins already transferred

### States and Transitions

```
HIDDEN ──level_complete──> REVEALING  (coin_reward_granted fires in OnEnable, before first frame)
REVEALING ──navigation tap──> DISMISSED  (remaining animations skipped; coins already delivered)
REVEALING ──animations done + ad unavailable──> IDLE
REVEALING ──animations done + ad available + show_rate roll fails──> IDLE
REVEALING ──animations done + ad available + show_rate roll passes──> AD_OFFER
AD_OFFER ──Watch tap──> AD_PROCESSING
AD_OFFER ──Skip tap──> IDLE
AD_OFFER ──navigation tap──> DISMISSED  (offer cancelled; base coins already delivered)
AD_PROCESSING ──ad_reward_granted──> IDLE  (bonus coins delivered)
AD_PROCESSING ──ad_reward_denied──> IDLE  (no bonus; base coins already delivered)
AD_PROCESSING ──ad_watchdog_timeout_ms elapsed on resume, no result received──> IDLE  (no bonus; identical to ad_reward_denied)
IDLE ──navigation tap──> DISMISSED
DISMISSED ──(Level Progression loads next state)──> HIDDEN
```

- Navigation buttons active in REVEALING, AD_OFFER, and IDLE
- Navigation buttons disabled only in AD_PROCESSING
- `show_rate` roll and `ad_available` check are evaluated at the same moment (after star reveal completes); both conditions must pass for AD_OFFER to be entered

### Interactions with Other Systems

| Event (In) | Source | Action |
|---|---|---|
| `level_complete(move_count, level_id)` | GSM | Enter REVEALING; compute stars + coins_earned; fire base coin transfer immediately |
| `ad_reward_granted` | Rewarded Ad System | Exit AD_PROCESSING; deliver bonus coins; enter IDLE |
| `ad_reward_denied` | Rewarded Ad System | Exit AD_PROCESSING; no bonus; enter IDLE |

| Event (Out) | Receiver | Trigger |
|---|---|---|
| `coin_reward_granted(amount)` | Level Progression | Fired on screen entry — always, unconditionally |
| `coin_bonus_granted(amount)` | Level Progression | Fired only after `ad_reward_granted` |
| `next_level_requested` | Level Progression | Next Level tap |
| `retry_requested` | Level Progression | Retry tap |
| `menu_requested` | Level Progression | Menu tap |
| `ad_watch_requested` | Rewarded Ad System | Watch tap in AD_OFFER |

## Formulas

**F-01 — Star Rating**
```
if move_count <= par_moves:              stars = 3
else if move_count <= par_moves × par_threshold_2star:  stars = 2
else:                                    stars = 1
```
**Evaluate in order; first match wins (if/else if/else chain). Do not implement as three independent if-statements — parallel evaluation overwrites stars and awards 1★ to a par-move performance.**
- `par_moves`: integer defined per level in Level Data System (`par_moves` field, range 1–999). If `par_moves < 1`, apply E-07 fallback (1 star, warning logged) — formula is not evaluated.
- `par_threshold_2star`: tuning knob (default 1.5) — e.g. par=10 → 2 stars up to 15 moves
- Minimum result: 1 (completing a level always awards at least 1 star)
- Example: par=10, threshold=1.5 → 3★ ≤10 moves, 2★ 11–15 moves, 1★ ≥16 moves
- Boundary: `move_count == par_moves` → 3★; `move_count == floor(par_moves × par_threshold_2star)` → 2★

**F-02 — Coin Reward (base)**
```
assert stars >= 1           // guard: stars = 0 must never reach the coin lookup
coins_earned = coin_reward_per_star[stars]
```
- `coin_reward_per_star`: tuning knob array (default: [0, 15, 20, 40] — index = star count) *(updated 2026-05-08 per Cluster A retune — Cross-GDD LCUI-01 from Coin Economy GDD)*
- Index 0 unused; index 1 = 1★, index 2 = 2★, index 3 = 3★
- The `assert stars >= 1` guard is required — if `stars` is 0 (logic failure), index 0 returns 0 coins silently with no crash or log
- Example: 2 stars → 20 coins

**F-03 — Ad Bonus**
```
coin_bonus = floor(coins_earned × (ad_multiplier − 1))
total_after_ad = coins_earned + coin_bonus
```
- `ad_multiplier`: tuning knob (default 2.0) — bonus equals base amount
- Apply `floor()` — formula can produce non-integer results at non-default multiplier values (e.g. coins=5, multiplier=1.5 → 2.5; floor → 2)
- `coin_bonus` is delivered separately via `coin_bonus_granted`; never replaces base
- Example: coins_earned=20, ad_multiplier=2.0 → coin_bonus=20, total=40

## Edge Cases

**E-01 — Player navigates before animations complete**
Navigation tap during REVEALING skips all pending animations instantly; `coin_reward_granted` has already fired at screen entry — base coins are safe. Screen transitions to DISMISSED.

**E-02 — Ad unavailable at screen entry**
Ad offer is never shown. Screen proceeds directly to IDLE after star reveal. No AD_OFFER state entered.

**E-03 — Ad becomes unavailable after screen entry but before offer appears**
HUD queries ad availability after star reveal completes (not at screen entry). If `ad_available == false` at that moment, offer is not shown. Offer is never shown then hidden mid-screen.

**E-04 — `ad_reward_denied` after player watches full ad**
Screen exits AD_PROCESSING and enters IDLE with base coins only. No bonus delivered. No error shown to player — ad denial is silent from the player's perspective.

**E-05 — `ad_reward_granted` arrives after player has already navigated (race condition)**
If navigation fires before `ad_reward_granted` resolves: navigation wins. `coin_bonus_granted` is still emitted to Level Progression if the event arrives before Level Progression fully tears down the session — Level Progression is responsible for accepting or discarding late bonus events. Screen does not re-open.

**E-06 — No next level available (last level in pack)**
`next_level_requested` is still emitted; Level Progression determines routing (e.g. redirect to Level Select UI). Level Complete UI has no knowledge of level sequence — it always shows the Next Level button.

**E-07 — `par_moves` missing or invalid**
If `par_moves` is absent from the level record, or if `par_moves < 1` (zero or negative), stars cannot be computed. Screen defaults to 1 star and logs a warning. `coins_earned = coin_reward_per_star[1]`. Never shows 0 stars or crashes. Note: `par_moves` is a required field in the Level Data System schema; this fallback covers authoring errors and schema version gaps during development.

**E-08 — `level_complete` fires while screen is already visible**
Ignored. Screen is already in a post-HIDDEN state; a second `level_complete` cannot re-trigger REVEALING. Defensive guard required in implementation.

**E-09 — OS interruption during AD_PROCESSING (phone call, home button)**
App goes to background; ad SDK behaviour is platform-defined. On resume: if `ad_reward_granted` or `ad_reward_denied` has not been received, the screen checks whether `ad_watchdog_timeout_ms` (tuning knob, default 30,000ms) has elapsed since AD_PROCESSING was entered. If the timeout has elapsed, the screen exits AD_PROCESSING and enters IDLE with no bonus — identical to the `ad_reward_denied` path. If the timeout has not elapsed, the screen remains in AD_PROCESSING awaiting the SDK result. No error is shown to the player in either case. Rationale: iOS ad SDK callbacks are not guaranteed after backgrounding; without a timeout, a player interrupted mid-ad is permanently soft-locked with navigation buttons disabled.

**E-10 — Retry on a level with no `par_moves` defined**
Same as E-07 — retry is always available; star computation issue is independent of retry path.

**E-11 — `ad_offer_show_rate` suppresses offer when ad is available**
When `ad_offer_show_rate < 1.0`, the RNG roll is evaluated immediately after star reveal completes (the same moment `ad_available` is checked). If the roll fails, the screen proceeds directly to IDLE — identical to the "ad unavailable" path from the player's perspective. No offer is shown; no AD_OFFER state is entered. The distinction is internal only and does not change the IDLE state's visual presentation.

## Dependencies

**Systems this screen depends on (inbound)**

| System | Dependency | Contract |
|---|---|---|
| Game State Manager | `level_complete(move_count: int, level_id: string)` | Must include `move_count` in payload; screen cannot compute star rating without it |
| Level Data System | `par_moves` for the completed level | `par_moves` is included in the `level_complete` event payload by GSM (per ADR-0012 — GSM reads it from LDS before emitting). This screen reads `par_moves` from the event payload, not by querying LDS directly. **Schema requirement**: `par_moves` must be present in the Level Data System schema — it is a Level Data System GDD authoring requirement. Missing value falls back to E-07. |
| Rewarded Ad System | `ad_available: bool` (queried after star reveal); `ad_reward_granted`; `ad_reward_denied` | Must always emit one of the two result events after `ad_watch_requested`; no silent drops |
| Coin Economy | `coin_reward_per_star` default values and safe ranges | Coin reward values must be coordinated with the Coin Economy GDD. Until the Coin Economy GDD is authored, treat these values as `ECONOMY_TBD` placeholders and do not tune beyond the defaults. |

**Systems that depend on this screen (outbound)**

| System | Dependency | Contract |
|---|---|---|
| Level Progression | `coin_reward_granted(amount: int)` — fired unconditionally on screen entry | Must be ready to receive before `level_complete` triggers this screen |
| Level Progression | `coin_bonus_granted(amount: int)` — fired only after `ad_reward_granted` | Must accept or discard gracefully if it arrives after navigation |
| Level Progression | `next_level_requested`, `retry_requested`, `menu_requested` | Level Progression owns all routing; this screen emits and forgets |
| Rewarded Ad System | `ad_watch_requested` | Rewarded Ad System must begin ad playback on receipt |

**Platform dependency**
- Unity UGUI Canvas, Screen Space - Overlay; `Screen.safeArea` anchoring — consistent with In-Game HUD

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `par_threshold_2star` | 1.5 | [1.1, 2.5] | Cutoff between 2★ and 1★; higher = more forgiving |
| `coin_reward_per_star` | [0, 15, 20, 40] | per-index: [0, 10–25, 10–40, 20–80] | Coin payout per star tier; index 0 unused. Index 1 (1★) updated 2026-05-08 from 10→15 per Cluster A retune (Cross-GDD LCUI-01). |
| `ad_multiplier` | 2.0 | [1.5, 3.0] | Ad bonus multiplier; values below 1.5 feel unrewarding; above 3.0 distort economy |
| `star_reveal_interval_ms` | 300 | [150, 600] | Delay between sequential star pop animations; lower = snappier, higher = more dramatic |
| `coin_anim_delay_ms` | 600 | [300, 1200] | Delay after screen entry before the **visual** coin transfer animation begins; does not affect actual delivery — base coins are transferred immediately on screen entry regardless |
| `ad_offer_show_rate` | 1.0 | [0.0, 1.0] | Probability that ad offer is shown when ad is available; 1.0 = always; use to A/B test offer frequency |
| `ad_watchdog_timeout_ms` | 30000 | [10000, 120000] | Maximum time in AD_PROCESSING before watchdog exits to IDLE with no bonus on app resume; guards against silent iOS ad SDK callback drops |

**Notes**
- `coin_reward_per_star` values must be coordinated with Coin Economy GDD — changes affect economy balance
- `ad_multiplier` must be reviewed by Economy Designer before any change; doubling ad payout doubles the effective earn rate for engaged players
- `ad_offer_show_rate` is an A/B lever only; do not use to permanently suppress the offer
- `coin_anim_delay_ms` is purely cosmetic — adjusting it has no effect on when coins are credited

## Acceptance Criteria

**Star Rating**
- AC-01 [BLOCKING] Screen appears on `level_complete`; star reveal begins immediately
- AC-02 [BLOCKING] 3 stars shown when `move_count <= par_moves`
- AC-03 [BLOCKING] 2 stars shown when `move_count <= par_moves × par_threshold_2star`
- AC-04 [BLOCKING] 1 star shown when `move_count > par_moves × par_threshold_2star`
- AC-05 [BLOCKING] All 3 star slots always rendered; unearned stars shown as empty outlines
- AC-06 [BLOCKING] Earned stars reveal sequentially with `star_reveal_interval_ms` gap between each (±50ms tolerance); unearned slots appear immediately as empty outlines without sequential delay
- AC-07 [BLOCKING] When `par_moves` is missing: 1 star awarded, no crash, warning logged

**Coin Reward**
- AC-08 [BLOCKING] `coin_reward_granted(coins_earned)` emitted synchronously in `OnEnable`, before the first rendered frame — Script Execution Order must guarantee Level Progression is subscribed before this fires
- AC-09a [BLOCKING] Base coin delivery is not affected by ad offer state: `coin_reward_granted` amount is identical whether or not an ad is subsequently watched
- AC-09b [BLOCKING] Base coin delivery is not affected by animation state: `coin_reward_granted` amount is identical whether animations complete or are skipped
- AC-09c [BLOCKING] Base coin delivery fires exactly once per level completion regardless of navigation timing
- AC-10 [BLOCKING] Coin amount displayed matches `coin_reward_per_star[stars]`
- AC-11 [ADVISORY] Visual coin transfer animation begins after `coin_anim_delay_ms`; purely cosmetic
- AC-26 [BLOCKING] If player navigates immediately on screen entry, `coin_reward_granted` must have been emitted exactly once — no loss, no duplication

**Rewarded Ad Offer**
- AC-12 [BLOCKING] Ad offer not shown when `ad_available == false`
- AC-13 [BLOCKING] Ad offer appears only after star reveal completes
- AC-14 [BLOCKING] Skip option always visible in AD_OFFER state; tapping Skip proceeds to IDLE without ad
- AC-15 [BLOCKING] Tapping Watch emits `ad_watch_requested` and enters AD_PROCESSING
- AC-16 [BLOCKING] On `ad_reward_granted`: `coin_bonus_granted(coins_earned × (ad_multiplier − 1))` emitted; display updates to show total
- AC-17 [BLOCKING] On `ad_reward_denied`: no bonus emitted; no error shown to player
- AC-18 [BLOCKING] Navigation during AD_OFFER cancels offer; base coins already delivered; no bonus emitted

**Navigation**
- AC-19 [BLOCKING] Next Level, Retry, and Menu buttons are tappable from screen entry
- AC-20 [BLOCKING] Navigation during REVEALING skips all pending animations and dismisses screen
- AC-21 [BLOCKING] Navigation buttons disabled only during AD_PROCESSING
- AC-22 [BLOCKING] Next Level tap emits `next_level_requested`; Retry emits `retry_requested`; Menu emits `menu_requested`

**Guard / Duplicate Events**
- AC-23 [BLOCKING] A second `level_complete` while screen is visible is ignored; no re-trigger of REVEALING

**Layout & Platform**
- AC-24 [BLOCKING] All elements render within `Screen.safeArea` on iOS notch devices
- AC-25 [BLOCKING] All elements render within `Screen.safeArea` on Android cutout devices
- AC-26 [BLOCKING] If player navigates immediately on screen entry, `coin_reward_granted` must have been emitted exactly once — no loss, no duplication

**Ad offer availability check timing**
- AC-27 [BLOCKING] `ad_available` is queried after star reveal completes, not at screen entry; if ad becomes unavailable between screen entry and star reveal completion, the offer is not shown

**Late ad result (race condition)**
- AC-28 [BLOCKING] If `ad_reward_granted` arrives after the player has already navigated: `coin_bonus_granted` is emitted to Level Progression; Level Progression is responsible for accepting or discarding the late event; the screen does not re-open

**Last level in pack**
- AC-29 [BLOCKING] Next Level button is always visible and always emits `next_level_requested` regardless of whether a next level exists; Level Progression owns routing

**ad_offer_show_rate**
- AC-30 [BLOCKING] When `ad_offer_show_rate = 0.0`: ad offer is never shown even when `ad_available == true`; screen proceeds directly to IDLE after star reveal

**Screen dismissal**
- AC-31 [BLOCKING] After any navigation tap in IDLE or AD_OFFER, the screen fully dismisses (canvas deactivated or equivalent) — not just the signal emission; Level Progression takes control

**Star state preservation on skip**
- AC-32 [BLOCKING] If navigation tap fires during REVEALING after some stars have already animated: the final display shows the correct computed star count instantly (not the in-progress partial count); no star is lost or gained by skipping

**AD_PROCESSING exit conditions**
- AC-33 [BLOCKING] Screen never exits AD_PROCESSING except via: `ad_reward_granted`, `ad_reward_denied`, or `ad_watchdog_timeout_ms` elapsed on resume
- AC-34 [BLOCKING] On app resume after OS interruption during AD_PROCESSING: if `ad_watchdog_timeout_ms` has elapsed and no result received, screen enters IDLE with no bonus; if timeout not elapsed, screen remains in AD_PROCESSING awaiting SDK result

**Platform**
- AC-35 [BLOCKING] Android hardware/gesture back button during REVEALING, AD_OFFER, or IDLE: treated as Menu navigation (emits `menu_requested`); disabled during AD_PROCESSING consistent with AC-21
