# Cross-GDD Review Report — BoltSort

> **Date**: 2026-05-12
> **Skill**: `/review-all-gdds full`
> **GDDs Reviewed**: 11 MVP system GDDs + `game-concept.md` + `systems-index.md`
> **Systems Covered**: Sort Mechanic, Game State Manager, Level Data System, Animation System, Audio System, In-Game HUD, Level Complete UI, Save & Persistence, Coin Economy, Quality Tier System, Level Progression
> **Pillars**: Flow Over Friction · Every Pixel Earns Its Place · Respect the Session · Cosmetic, Not Coercive · The Machine Must Sing
> **Anti-Pillars**: Not time-pressured · Not pay-to-win · Not narrative-driven · Not visually cluttered · Not multiplayer/PvP
> **Registry Baseline**: 1 item, 5 formulas, 10 constants (consistent per `/consistency-check` 2026-05-12)
> **Verdict**: **CONCERNS** — no design-theory blockers; 1 cross-doc signature conflict + 3 minor inconsistencies

---

## Verdict: CONCERNS

No design-theory blockers. All 5 pillars are coherently served by all 11 MVP systems with no anti-pillar violations. Player fantasy is coherent across systems. Economic loop is calibrated with Beta-deferred risks formally accepted.

Remaining issues are localized contract drift:
- 1 🔴 conflict blocking the HUD pity grant story (not blocking the gate)
- 2 ⚠️ warnings (color tuning stale; level ID advance asymmetry; SEO ordering note)
- 1 ℹ️ info (0-delta event behavior)

This mirrors the 2026-05-12 architecture review's verdict — no foundation blockers, well-defined per-story fixes.

---

## Consistency Issues

### 🔴 Blocking

#### C-01 — `coin_balance_changed` event signature stale (2-arg vs 3-arg)

| Source | Signature | Status |
|---|---|---|
| `coin-economy.md` (canonical, Approved 2026-05-12) | `coin_balance_changed(new_balance: int, delta: int, earn_source: EarnSource)` — **3-arg** | Authoritative |
| `in-game-hud.md` AC-35 (BLOCKING) | Requires `earn_source: EarnSource.PityGrant` → assumes 3-arg | Consistent with CE |
| `in-game-hud.md` Dependencies table (line 187) | `coin_balance_changed(new_balance: int, delta: int)` — **2-arg** | **Stale** |
| `docs/architecture/adr-0002` event catalog (line 193) | `event Action<int, int> OnCoinBalanceChanged; // (newBalance, delta)` — **2-arg** | **Stale** |

**Impact**: HUD AC-35 (pity grant notification) cannot be implemented or tested against the architecture as currently documented. A HUD story for pity grant would compile but the `EarnSource.PityGrant` discriminator is absent from the event payload, breaking AC-35's BLOCKING test ("when HUD receives this event THEN HUD displays a differentiated player notification").

**Resolution**:
1. Update ADR-0002 event catalog: `event Action<int, int, EarnSource> OnCoinBalanceChanged; // (newBalance, delta, earnSource)`
2. Update `in-game-hud.md` Dependencies table to match CE canonical signature
3. Same pattern as the 2026-05-03 `OnLevelComplete` 2→4-arg resolution

---

### ⚠️ Warnings

#### C-02 — `coin_pulse_color_*` tuning knob defaults stale (Art Bible 7 supersedes)

`in-game-hud.md` Tuning Knobs table (lines 211–212) lists:
- `coin_pulse_color_positive = #4CAF50` (green)
- `coin_pulse_color_negative = #FF9800` (amber)

`art-bible.md` Section 7 (line 812) explicitly overrides:
- Positive: CHROME-03 `#4DCFEF` (cyan numeral shift over 100/200ms)
- Negative: no color shift; icon −5% deflation only

**Impact**: An implementer following HUD GDD tuning knob defaults would produce green/amber pulses that violate the "no warm colors during play states 1–4" rule from Art Bible Section 4.3 and contradict the recently authored `design/ux/hud.md` (Element 2).

**Resolution**: Update HUD GDD tuning knob defaults to match Art Bible. Same pattern as the LCUI `coin_reward_per_star` retune (Cross-GDD LCUI-01).

---

## Game Design Issues

### Progression Loop Competition: ✅ PASS
One dominant loop (complete levels → earn coins → buy skins). Star rating is a parallel mastery loop, not a competing primary. No system claims competing primary status.

### Player Attention Budget: ✅ PASS
During core moment (single bolt move): 1 active system (Sort Mechanic), 2 passive (move counter glance, coin display glance). Well within the 3–4 active limit. Tap-only input + portrait + thumb-zone layout keeps cognitive load minimal.

### Dominant Strategy: ✅ PASS
- Hint at 50 coins is an escape valve, not optimal play. No-hint runs are explicitly rewarded with 3-star rating (per CE GDD).
- Skins are cosmetic-only — no gameplay advantage. Cosmetic Not Coercive pillar enforced.
- Ad bonus (2× multiplier) is opt-in with always-visible Skip; not exploitable.

### Economic Loop: ⚠️ CONCERN (formally accepted)

| Resource | Sources | Sinks |
|---|---|---|
| Coin balance | Per-level [0, 15, 20, 40] per star; × ad_multiplier 2.0; pity 50; starter 150 (first install) | Hint 50; Finish skin 300; Set 1,200; Workshop 2,800 |
| Move count | Pure tracking — no economy | N/A |
| Stars | Progression marker (max(current, earned)) | N/A — non-economic |
| Undo stack | Unlimited, free | N/A — design promise |

Documented Beta-deferred risks (acceptable for MVP):
- **OQ-07**: 7-session gap between 75-coin Accent and 300-coin Finish after starter spend
- **OQ-11**: Fixed-interval structure without variable-ratio reinforcement between Accent and Finish

Both formally accepted as Beta-deferred per Coin Economy GDD (Approved 2026-05-12). Shop UI GDD authorship will close them.

### Difficulty Curve: ✅ PASS
- Colors 3 → 8 across 200 levels (monotonic)
- Stack depth increases; temp slots decrease — single direction
- 8-column hard cap (ADR-0013) prevents unbounded escalation
- Every 10 levels = "breather" level (per game-concept)
- Animation duration capped at 300ms travel (per Animation System GDD F-01) — bounded regardless of board complexity
- QTS Low tier preserves bolt color legibility at 30fps with VFX density 0.25 (verification deferred to device test per Animation System GDD)

### Pillar Alignment: ✅ PASS — every MVP system serves at least one pillar

| System | Pillars Served |
|---|---|
| Sort Mechanic | Flow Over Friction · Every Pixel Earns Its Place · Respect the Session |
| Game State Manager | Flow Over Friction (watchdog) · Cosmetic Not Coercive (unlimited undo) |
| Level Data System | Respect the Session (fast load) |
| Animation System | Every Pixel Earns Its Place · The Machine Must Sing |
| Audio System | Every Pixel Earns Its Place · The Machine Must Sing |
| In-Game HUD | Flow Over Friction (minimal) · Respect the Session |
| Level Complete UI | Flow Over Friction (skippable) · Cosmetic Not Coercive (ad always skippable) · Respect the Session |
| Save & Persistence | Respect the Session (instant resume from any device state) |
| Coin Economy | Cosmetic Not Coercive (explicit in header) · Flow Over Friction (pity grant) |
| Quality Tier System | The Machine Must Sing · Flow Over Friction (60fps target) |
| Level Progression | Respect the Session · Every Pixel Earns Its Place (breather cadence) |

### Anti-Pillar Violations: ✅ PASS
- **No timers** in any MVP system. The 1500ms watchdog (GSM) is crash recovery, not gameplay pressure. Hint timeout (5000ms) is system-internal, not surfaced as countdown.
- **No pay-to-win** — IAP confirmed cosmetic-only per CE GDD. Ad bonus doubles cosmetically.
- **No narrative** — no dialogue, no story, no characters.
- **HUD visual clutter** — minimalist 4-element layout (move counter, coin chip, undo, hint) enforced by Art Bible Section 7.1. Thumb zone / glance zone separation prevents accumulation.
- **No multiplayer** — confirmed single-player only.

### Player Fantasy Coherence: ✅ PASS
All systems serve "restoring order to a living machine":
- Sort Mechanic ("pure control" — your hands solve the problem)
- HUD ("instrument panel" — reports, does not alarm)
- Animation System ("the machine sings" — alive, mechanical)
- Coin Economy ("calm cosmetic accumulation")
- Level Complete UI ("reward arrives quietly")

No identity conflicts. No system fights the core fantasy.

---

## Cross-System Scenario Issues

**Scenarios walked**: 4 of 5. Scenario 5 (Hint during MOVE_EXECUTING with watchdog firing) deferred — Hint System is Beta scope, not yet authored.

1. Level completion with star rating + coin reward + ad offer
2. Pity grant trigger (5 consecutive 0-star)
3. App backgrounding mid-BOLT_SELECTED
4. Cold-start app launch with cached save

---

### ⚠️ Warnings

#### S-01 — LP._currentLevelId advances before LC UI fires OnCoinRewardGranted (Scenario 1)

**Order of operations** on level completion with non-zero stars:

1. GSM emits `OnLevelComplete(levelId=N, ...)`
2. LP (SEO −30, subscribes first per ADR-0002 FIFO event order): computes stars via `StarRatingCalculator.Compute()`, kicks off `WriteCompletionAtomic`, advances `_currentLevelId = N+1`, emits `OnLevelCompleted`
3. LC UI (SEO 0, subscribes second): activates, `OnEnable` fires `OnCoinRewardGranted(amount)`
4. LP.HandleCoinRewardGranted: calls `CE.AddCoins(amount, _currentLevelId=N+1, EarnSource.Base)` — credits against **N+1**, not the just-completed **N**

**Functionally works** because:
- Idempotency check is `levelId <= last`
- Level IDs increase monotonically
- `AddCoins(0, N, Base)` (the 0-star path where LP doesn't advance) is functionally a no-op

But the asymmetry creates two distinct semantic models:
- 0-star path: credit recorded against `N`
- Non-zero-star path: credit recorded against `N+1`

CE's `_lastCreditedLevelId[Base]` tracks "highest level whose credit was recorded" — not "highest level completed." A future maintainer reading the code without this context could introduce a real bug.

**Resolution options:**

| Option | Approach | Effort |
|---|---|---|
| 1 (Recommended) | Add `levelId` to `OnCoinRewardGranted` payload: `event Action<int, int> OnCoinRewardGranted; // (amount, levelId)`. LP uses event's levelId, not `_currentLevelId`. | Update ADR-0012 + LC UI GDD |
| 2 | LP defers `_currentLevelId` advance until LC UI screen is dismissed | Larger refactor |
| 3 | Document the asymmetry as intentional in CE GDD CE-12 with explicit comment | Doc only |

Severity: WARNING — recommend Option 1 before HUD/LCUI implementation sprint.

---

#### S-02 — ADR-0001 SEO vs ADR-0006 SER-01 OnApplicationPause ordering (Scenario 3)

ADR-0006 SER-01 (line 206) requires:
> "SortMechanic.OnApplicationPause must execute before GSM.OnApplicationPause [...] ADR-0001's Script Execution Order must include an explicit entry: SortMechanic executes before GSM in OnApplicationPause."

ADR-0001's SEO table places:
- GameStateManager at SEO **−50** (runs first per Unity lifecycle)
- SortMechanic at SEO **0** (default — runs last)

Unity calls `OnApplicationPause` in SEO order. Lower SEO number → runs first. So GSM's pause handler runs **before** Sort Mechanic's, contradicting ADR-0006 SER-01.

**Architectural reality**: Sort Mechanic cannot have SEO < GSM because it depends on GSM Awake-initialized board state. The SEO mechanism cannot satisfy both Awake-order (GSM first) and pause-order (Sort Mechanic first) requirements simultaneously.

**Functional analysis**: Sort Mechanic's BOLT_SELECTED state does NOT mutate GSM's `stack_contents` — the held bolt remains in `stack_contents[source]` (it's a Sort Mechanic-local visual/interaction reference). So GSM's serialization is in fact correct regardless of which runs first. ADR-0006 SER-01 may be over-specified defense.

**Resolution**: Replace SER-01's SEO requirement with a direct-call pattern:
> "GSM.OnApplicationPause(true) handler must explicitly call `SortMechanic.Instance.CancelHeldBolt()` synchronously **before** invoking `SP.SetBoardSnapshot()`. This guarantees Sort Mechanic FSM resets to IDLE before serialization regardless of Unity's SEO-driven lifecycle order."

Severity: WARNING — documentation clarity issue, not a runtime bug. Implementer would likely discover this and apply the direct-call pattern naturally.

---

### ℹ️ Info

#### S-03 — 0-delta `coin_balance_changed` event handling undefined

When `stars == 0` on a level completion:
- LC UI fires `OnCoinRewardGranted(0)` unconditionally
- LP calls `AddCoins(0, levelId, Base)`
- CE updates idempotency (or skips if guard rejects) and emits `OnCoinBalanceChanged(new_balance, 0, Base)`
- HUD receives a 0-delta event

HUD GDD AC-20 states "Coin display updates on every `coin_balance_changed` event." Pattern 12 (Live Balance Display with Pulse) doesn't define explicit 0-delta behavior. A 0-delta pulse is animation-without-meaning.

**Severity**: INFO

**Recommendation**: Add explicit rule to `in-game-hud.md` AC-20 or Pattern 12: "delta == 0 events are silently skipped — no pulse animation fires."

---

### Passed Scenarios

✅ **Scenario 2 — Pity grant trigger**: Clean flow. HUD's `_pityAttempts` counter, `coin_balance < hint_cost` gate, `AddCoins(50, -1, EarnSource.PityGrant)` call, CE's `levelId == -1` idempotency bypass, and HUD's Contextual Toast (Pattern 10) all interlock correctly. Sequential toast-then-pulse ordering per Art Bible 7.5.

✅ **Scenario 4 — Cold-start app launch**: ADR-0001 SEO sequence (QTS −100 → LDS −95 → SP −90 → Audio −80 → GSM −50 → CE −40 → LP −30 → scene MonoBehaviours 0) is clean. Subscribe-then-check pattern (ADR-0001) + dual-ready guard (ADR-0004 LP waiting for both `OnSaveReady` and `OnLevelDataReady`) handles race conditions correctly.

---

## GDDs Flagged for Revision

| GDD | Reason | Type | Priority |
|---|---|---|---|
| `in-game-hud.md` | Dependencies `coin_balance_changed` signature stale (2-arg vs CE 3-arg with EarnSource) | Consistency | **Blocking** |
| `in-game-hud.md` | Tuning knob `coin_pulse_color_*` defaults superseded by Art Bible 7 | Consistency | Warning |
| `coin-economy.md` | (Optional, if not changing OnCoinRewardGranted payload) Document LP._currentLevelId advance asymmetry in CE-12 with explicit comment | Design | Warning |

**ADR updates also required (not GDDs, but architecture):**
- **ADR-0002** — event catalog: `OnCoinBalanceChanged` → 3-arg with `EarnSource`
- **ADR-0001** — replace OnApplicationPause SEO requirement with direct-call pattern from ADR-0006 SER-01
- **ADR-0012** (Option 1 recommended) — `OnCoinRewardGranted` payload: add `levelId` parameter

---

## Summary

| Severity | Count | Items |
|---|---|---|
| 🔴 Blocking (story-level) | 1 | `coin_balance_changed` signature mismatch (C-01) — blocks HUD pity grant story |
| ⚠️ Warning | 3 | Color tuning stale (C-02); LP `_currentLevelId` advance timing (S-01); SEO vs SER-01 (S-02) |
| ℹ️ Info | 1 | 0-delta coin event handling (S-03) |

This is a **CONCERNS verdict, not FAIL** — no foundation issues, no pillar drift, no economy imbalance, no dominant strategy. The cross-GDD consistency state is the strongest it has been across this project's review history.

---

## Required Actions Before `/create-stories`

| # | Action | Files | Effort |
|---|---|---|---|
| 1 | Update ADR-0002 event catalog: `OnCoinBalanceChanged` → `Action<int, int, EarnSource>` | `docs/architecture/adr-0002-event-and-signal-architecture.md` | 5 min |
| 2 | Update HUD GDD Dependencies table `coin_balance_changed` signature to match CE | `design/gdd/in-game-hud.md` line 187 | 5 min |
| 3 | Update HUD GDD tuning knob defaults: `coin_pulse_color_positive = #4DCFEF`, `coin_pulse_color_negative = no color shift` | `design/gdd/in-game-hud.md` lines 211–212 | 5 min |
| 4 *(Recommended)* | Update ADR-0012 + LC UI GDD: `OnCoinRewardGranted` payload adds `levelId` | `docs/architecture/adr-0012-...md`, `design/gdd/level-complete-ui.md` | 15 min |
| 5 *(Recommended)* | Update ADR-0001 + ADR-0006 SER-01: replace SEO requirement with GSM-calls-SortMechanic direct-call pattern | `docs/architecture/adr-0001-...md`, `docs/architecture/adr-0006-...md` | 15 min |
| 6 *(Polish)* | Add explicit "delta == 0 silently skipped" rule to HUD GDD AC-20 or Pattern 12 | `design/gdd/in-game-hud.md` or `design/ux/interaction-patterns.md` | 2 min |

Actions 1–3 are MVP-blocking for the HUD implementation sprint. Actions 4–6 are recommended but can be deferred if implementation teams accept the documented warnings.
