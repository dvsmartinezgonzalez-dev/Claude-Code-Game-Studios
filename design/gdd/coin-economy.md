# Coin Economy

> **Status**: Approved (MVP scope — 2026-05-12). OQ-07 and OQ-11 are Beta-deferred: both require Shop UI GDD authorship and do not block MVP design approval.
> **Author**: Design session + agents
> **Last Updated**: 2026-05-12
> **Implements Pillar**: Flow Over Friction, Cosmetic Not Coercive

## Overview

Coin Economy is BoltSort's soft-currency layer: the system that defines how coins are earned, how they are spent, and how the running balance persists across sessions. It owns four responsibilities. First, **earn rules**: subscribing to `LevelCompleted(stars, level_id, move_count, par_moves)` from Level Progression and routing coin awards through a level-completion idempotency guard that prevents duplicate credits. Second, **spend rules**: deducting coins when the Hint System or Shop UI initiates a purchase, with a hard floor of 0 and a spend-validation guard that prevents overspend. Third, **balance persistence**: formally claiming ownership of `economy.coin_balance` in the save schema from Level Progression, which holds it provisionally until this GDD is authored. Fourth, **starter grant**: initializing new players to 150 coins on first install, ensuring the "never short of resources" promise holds on day one.

Coin Economy exposes `GetCoinBalance(): int`, `AddCoins(amount: int, level_id: int = -1, earn_source: EarnSource = EarnSource.Base): bool`, and `SpendCoins(amount: int): bool` to its consumers — Level Progression, Hint System, Shop UI, and In-Game HUD. No coin is ever behind a paywall; the economy is cosmetic and generous. Five spend contexts exist in Beta scope: a 75-coin Accent cosmetic (reachable in 2–3 sessions for any player), a 50-coin hint, and three skin tiers at 300, 1,200, and 2,800 coins. For the player, Coin Economy is the quiet engine behind every reward moment: the tally that ticks up after a solve, the balance that inches toward the next skin unlock, and the gentle pull that turns one more level into three.

## Player Fantasy

You are almost always able to keep moving. Each solved level tops up a reserve that is yours to spend or to let grow — the game has no interest in pressuring either. Coins are optional leverage: a hint when a puzzle goes quiet on you, a cosmetic swap when you feel like changing the look of your tools. The pop of a coin landing in the counter is small, precise feedback — the kind of readout a technician trusts. You keep working. The balance keeps climbing. When you spend, it's because you chose to, and when you don't, the number still means something: proof of a run of clean, deliberate work.

*Design note: The honest promise is "almost always able to keep moving" — not "never short." Hints are a consumable resource that a 1★ hint-spending player can run low on (net ~−5 coins/session from the 15-coin earn rate). The economy keeps progression (cosmetics, skill, level completion) always available; it does not guarantee unlimited consumables. The pity system (CE-13) provides targeted relief for the genuinely stuck player. This framing is intentional: an honest promise the economy can structurally deliver is more trustworthy to the player than an absolute promise it cannot.*

If a level goes genuinely quiet — attempts accumulating, no forward motion — the system steps in: after five consecutive failed attempts on the same level, a hint is granted at no cost. The system's intervention must be surfaced to the player — a contextual notification ("That one was tough — here's a hint on us") or a distinct coin-animation differentiates pity from a normal earn. Without player-visible feedback, the deposit looks like a bug, not a gift. Specific presentation is delegated to In-Game HUD GDD; CE's contract is to deliver the coins and emit `coin_balance_changed(new_balance, delta, earn_source: EarnSource.PityGrant)`. The `earn_source` field in the event payload gives HUD the signal needed to trigger a differentiated notification. The "almost always able to keep moving" promise is systemic, not front-loaded.

Watching a rewarded ad is an opt-in choice that meaningfully rewards you: it doubles the coins you earned from that level. The game never prompts, reminds, or counts on you watching — but if you choose to, the economy reflects it. The baseline experience (no ads, no IAP) is complete and unpenalized; ads are an accelerator, not a corrective.

*Beta experience (no goal-visibility display): Until Shop UI GDD ships and accepts OQ-07, the Beta experience is a coin counter that accumulates — with no visible earn target. The earn loop is designed for goal-visibility; it must ship before or alongside the skin shop. A player accumulating coins in Beta is doing so without the aspiration loop the full experience delivers.*

*Full experience (with goal-visibility display): "The balance that inches toward the next skin unlock" — with the Shop UI's goal-visibility display active, the balance functions as a visible progress bar toward the next cosmetic. This is the intended emotional experience: small wins every level, always inching toward the next unlock.*

*Known limitations: The "almost always able to keep moving" promise is structurally delivered for progression (levels, cosmetics) but self-limiting for hint consumption. A 1★ player spending one hint per session nets approximately −5 coins/session. After the starter grant is exhausted (~30 sessions at this rate), the player relies on CE-13 pity grants for hint access. CE-13 is a reactive floor for the genuinely stuck player (5 consecutive failures on one level) — it is not a proactive flow guarantee for the 1★ casual drain archetype who uses hints throughout normal play. This is an accepted design choice: the Cosmetic Not Coercive pillar guarantees the baseline cosmetic experience is free and uncoerced; it does not guarantee unlimited consumable access. Telemetry (Analytics System) should track spend velocity by star-rating archetype in early live data to detect if this scarcity becomes coercive in practice.*

*Primary pillars: Cosmetic Not Coercive, Flow Over Friction*
*MDA target: Submission/Flow (2), Expression (4 — enables skin purchase without pressure)*

## Detailed Rules

### Core Rules

**CE-01 — Ownership declaration**
Coin Economy is the sole owner of `economy.coin_balance` in the save schema. Level Progression's provisional ownership (SP C.2, LP Section C) is formally superseded by this document.

**CE-02 — LP delegation model** *(resolves LP OQ-01)*
LP becomes a thin router for all coin operations:
- `LP.GetCoinBalance(): int` → delegates to `CE.GetCoinBalance()`
- `LP.AddCoins(amount: int, level_id: int, earn_source: EarnSource)` → delegates to `CE.AddCoins(amount, level_id, earn_source)`

LP removes its own `coin_balance` field. LP's `coin_balance = max(0, coin_balance + amount)` mutation rule (LP Section C, Non-Formula Rules) is superseded by CE-07 below. LP must pass `level_id` (from the `LevelCompleted` event) and the correct `EarnSource` value on every delegation.

*[Cross-GDD LP-01: LP GDD must update AC-21, AC-22, and AC-35 to reflect that LP delegates to CE and holds no coin state.]*
*[Cross-GDD LP-02: LP GDD must update its `CE.AddCoins` call sites to pass `level_id` and `EarnSource.Base` (EC-05 path) or `EarnSource.AdBonus` (EC-06 path).]*

**CE-03 — Event subscription: LevelCompleted**
CE subscribes to LP's `LevelCompleted(stars: int, level_id: int, move_count: int, par_moves: int)` event for analytics and to preserve design space for future earn-rule extensions (efficiency bonus, tier-based scaling). In the current model the actual coin award does not originate here — it arrives via LP's forwarding of Level Complete UI coin events through `CE.AddCoins`.

**CE-04 — AddCoins flow**
Three callers in Beta scope:
1. **Base award:** Level Complete UI fires `coin_reward_granted(amount)` to LP at screen entry. LP's EC-05 state guard gates delivery. LP calls `CE.AddCoins(amount, level_id, EarnSource.Base)`.
2. **Ad bonus:** Level Complete UI fires `coin_bonus_granted(amount)` to LP after `ad_reward_granted`. LP's EC-06 state guard gates delivery. LP calls `CE.AddCoins(amount, level_id, EarnSource.AdBonus)`.
3. **Pity grant:** In-Game HUD calls `CE.AddCoins(50, level_id=-1, EarnSource.PityGrant)` on pity threshold breach (CE-13). The `EarnSource.PityGrant` path bypasses `bonus_multiplier` and the idempotency guard.

CE is a passive receiver — it trusts each caller's gate. LP state guards (EC-05, EC-06) gate paths 1 and 2. CE-13's suppression check (`GetCoinBalance() >= hint_cost`) is the gate for path 3 and is the HUD's responsibility. The base-award path is additionally guarded by CE's own idempotency check (CE-12). Future callers (daily challenges, achievements) must be added to this document and CE GDD updated before implementation.

**CE-05 — SpendCoins contract** *(validated, not optimistic)*

Execution order:
0. Clamp working copy: `coin_balance = max(0, coin_balance)`. Guards against corrupted negative balance — ensures F-CE-02 evaluates against a non-negative value. Matches CE-07's AddCoins clamp per Edge Cases spec. The clamp is applied before validation; no SP call or event emission occurs as part of the clamp step.
1. If `amount ≤ 0`: return `false`. Log caller bug warning. No mutation, no event.
2. If `coin_balance < amount`: return `false`. No mutation, no event.
3. If `coin_balance ≥ amount`: apply `coin_balance = coin_balance − amount`. Call `SP.SetCoinBalance(coin_balance)`. Emit `coin_balance_changed(coin_balance, delta: −amount, earn_source: EarnSource.Spend)`. Return `true`.

`false` is a terminal denial — not a retry signal. Callers (Hint System, Shop UI) must read `GetCoinBalance()` before enabling spend UI, and must treat `false` as "do not proceed."

**Downstream spend-denial UX contract (Cosmetic Not Coercive pillar):** When `SpendCoins` returns `false`, the calling system must not display IAP prompts, ad-watch prompts, urgency messaging, or scarcity language. The denial must be passive — a disabled or greyed button state only. CE cannot enforce this at the API level, but it is a binding pillar obligation on all downstream callers (Hint System, Shop UI, any future spend context).

**CE-06 — GetCoinBalance contract**
`GetCoinBalance(): int` — synchronous read, available only in READY state. Never returns a negative value; if `coin_balance` is somehow below 0 in memory, repair the working copy: set `coin_balance = 0` in memory, log a contract violation warning, then return 0. By repairing the working copy on detection, CE ensures any pending W-2 (app-pause) flush observes the corrected in-memory state rather than a stale or corrupted value. The getter does **not** call `SP.SetCoinBalance` — the repair is in-memory only; the next mutation will write the corrected value to SP via the normal path. Subsequent `AddCoins` and `SpendCoins` calls continue to clamp with `max(0, coin_balance)` as a defensive measure regardless.

**CE-07 — Balance mutation rules**

```
AddCoins(amount: int, level_id: int = -1, earn_source: EarnSource = EarnSource.Base): bool
  if amount < 0: log caller bug warning; return false
  // EarnSource contract validation
  if earn_source == EarnSource.Spend:
    log "CE: caller bug — EarnSource.Spend is not a valid AddCoins earn_source; use SpendCoins for deductions."
    return false                                         // caller bug; Spend is a SpendCoins-only enum value
  if earn_source == EarnSource.PityGrant AND level_id != -1:
    log "CE: caller bug — EarnSource.PityGrant must use level_id=-1; received level_id={level_id}."
    return false                                         // caller bug; no credit, no guard advance
  if earn_source == EarnSource.Base AND level_id == -1:
    log "CE: advisory — EarnSource.Base with level_id=-1 bypasses idempotency guard; confirm this is intentional."
  // Idempotency guard (CE-12) — base-award path only
  if earn_source == EarnSource.Base AND level_id >= 0:
    if level_id == last_credited_level_id: log duplicate-credit warning; return false
  // Apply bonus multiplier only on base earn path (CE-10)
  if earn_source == EarnSource.Base:
    coin_award = floor(amount × bonus_multiplier)        // F-CE-01 applied to base earn
  else:
    coin_award = amount                                   // AdBonus / PityGrant: no multiplier applied
  if coin_award < 0: log "CE: internal error — negative coin_award; earn_source={earn_source}, amount={amount}, bonus_multiplier={bonus_multiplier}"; return false
  if coin_award == 0:
    if earn_source == EarnSource.PityGrant:
      log "CE: caller bug — EarnSource.PityGrant delivered 0 coins; pity_grant_amount or hint_cost tuning may be misconfigured"
    return false
  // Clamp working copy before add in case of prior corruption
  working_balance = max(0, coin_balance)
  // Overflow-safe earn: use long for intermediate arithmetic to avoid C# int overflow
  new_balance_long = (long)working_balance + coin_award
  coin_balance = (int)min(new_balance_long, (long)INT_MAX)   // INT_MAX clamp per SP contract
  actual_delta = coin_balance - working_balance               // actual change applied
  // Persist BEFORE updating idempotency guard — if SP throws, guard stays uncommitted and retry is clean
  try:
    SP.SetCoinBalance(coin_balance)                      // SP's internal dirty flag set as side effect
  except:
    coin_balance = working_balance                       // rollback working copy
    log "CE: SP.SetCoinBalance failed in AddCoins — balance rolled back, guard not advanced"
    return false                                         // caller should retry; guard NOT updated
  // Guard update ONLY after successful SP write
  if earn_source == EarnSource.Base AND level_id >= 0:
    last_credited_level_id = level_id
  // NOTE: emit occurs after SP write and guard update. Do not reorder — actual_delta is computed
  // relative to working_balance (pre-mutation), and the event must never fire on rollback.
  emit coin_balance_changed(new_balance: coin_balance, delta: actual_delta, earn_source: earn_source)
  return true
```

The floor of 0 is enforced by CE-05's `coin_balance >= amount` guard on `SpendCoins`, not by a clamp in `AddCoins`. An `AddCoins` call with `amount = 0` (or `coin_award = 0` after F-CE-01) is a no-op: no mutation, no SP call, no event.

*[Cross-GDD SP-01: SP GDD Interactions table must transfer `economy.coin_balance` ownership from Level Progression (provisional) to Coin Economy. SP interface must expose `SetCoinBalance(value: int)` (or equivalent) so CE can write `coin_balance` without calling the now-retired `AddCoins` provisional method on SP. Calling `SP.SetCoinBalance` must atomically mark SP's internal dirty flag — CE does not maintain a separate dirty field.]*

**CE-08 — Dirty flag and save trigger**
CE does not maintain its own dirty flag. Every successful mutation calls `SP.SetCoinBalance(coin_balance)`, which marks SP's internal dirty flag as a side effect. SP's W-1 (level completion) and W-2 (app pause) write paths flush `economy.coin_balance` as part of their normal write cycles.

**CE-09 — Spend contexts (Beta scope)**

| Context | Caller | Coin cost | Validation |
|---|---|---|---|
| Accent cosmetic | Shop UI | 75 coins | `SpendCoins(75)` — Shop UI must read `GetCoinBalance() >= 75` before enabling button |
| Hint purchase | Hint System | 50 coins | `SpendCoins(50)` — Hint System must read `GetCoinBalance() >= 50` before enabling button |
| Skin — Finish tier | Shop UI | 300 coins | `SpendCoins(300)` |
| Skin — Set tier (4 finishes) | Shop UI | 1,200 coins | `SpendCoins(1200)` |
| Skin — Workshop (set + background) | Shop UI | 2,800 coins | `SpendCoins(2800)` |

All deductions go through `SpendCoins()`. No system may mutate `coin_balance` through any other path. `SpendCoins` emits `coin_balance_changed` with `earn_source: EarnSource.Spend` so HUD and Shop UI subscribers can distinguish spend events from earn events. The `coin_reward_per_star` earn values ([0, 15, 20, 40]) and `ad_multiplier` (2.0) are owned by the Level Complete UI GDD as tuning knobs; this GDD documents them as the upstream earn parameters CE receives.

*[Cross-GDD LCUI-01: Level Complete UI GDD must update `coin_reward_per_star[1]` from 10 → 15 coins to reflect the Cluster A earn retune (2026-05-07). Priority: before either CE or Level Complete UI implementation sprint.]*

**CE-10 — Bonus multiplier**
CE exposes `bonus_multiplier: float` (default 1.0, safe range [1.0, 2.0]). When `bonus_multiplier > 1.0`, the base award becomes `floor(base_coins × bonus_multiplier)` before CE applies it. CE does not own streak or daily bonus logic — `bonus_multiplier` is set externally by a future system (e.g., Daily Challenge System). In Beta scope, `bonus_multiplier` is always 1.0.

**`bonus_multiplier` applies only to `EarnSource.Base` calls.** The ad-bonus path (`EarnSource.AdBonus`) and the pity grant path (`EarnSource.PityGrant`) are not scaled by `bonus_multiplier`. The ad bonus is computed from base rates by Level Complete UI (`floor(base_coins × (ad_multiplier − 1))`); applying an additional `bonus_multiplier` would compound incorrectly. The pity grant is a fixed safety-net amount — scaling it by `bonus_multiplier` would violate the intent that pity grants always deliver exactly `hint_cost` coins regardless of active multiplier events.

The setter must clamp at write time: values below 1.0 are set to 1.0 and a warning is logged (a sub-1.0 multiplier would silently under-reward players and violate the Cosmetic Not Coercive pillar). Values above 2.0 are set to 2.0 and a warning is logged.

**CE-11 — Starter coin grant**
On first-ever install, when `economy.coin_balance` is absent from the SP save file, CE initializes `coin_balance` to **150** (the starter grant) rather than 0. This ensures the "never short of resources" promise holds from day one: a new player has enough for 3 hints (3 × 50) or 2 Accent cosmetics (2 × 75) before earning a single coin in-game. The dirty flag on SP is set after initialization and `SP.SetCoinBalance(150)` is called before CE transitions to READY. This replaces the first-install-default of 0 described in earlier Edge Cases language.

*Rationale: A 1★ player earns 10 coins per level. Without a starter grant, the first hint requires 5 sequential completions of the very level they may be stuck on — breaking the core promise. The 150-coin starter is a one-time floor mechanism, not a recurring reward.*

**CE-12 — Level-completion idempotency guard**
CE maintains `last_credited_level_id: int = -1` in its working state (not persisted to SP). This guard applies only to `EarnSource.Base` calls with a valid `level_id ≥ 0`.

Execution:
1. If `earn_source == EarnSource.Base AND level_id >= 0`:
   - If `level_id == last_credited_level_id`: **duplicate base credit detected.** Log warning: `"CE: duplicate base AddCoins for level_id={level_id} — ignoring."` Return no-op. No mutation, no dirty, no event.
   - Otherwise: credit proceeds normally. After crediting, set `last_credited_level_id = level_id`.
2. If `earn_source == EarnSource.AdBonus` or `level_id == -1`: bypass the guard entirely.

The ad-bonus path (`EarnSource.AdBonus`) is not guarded by `last_credited_level_id` because LP's EC-06 state machine (gating on `ad_reward_granted`) is the structural protection for that path. CE trusts LP's EC-06 guard for ad-bonus deduplication.

`last_credited_level_id` is session-scoped (resets to -1 on cold launch). This guards against LP regressions that double-fire `coin_reward_granted` for the same level within a session — the most realistic duplicate-credit scenario. Cross-session protection is an unresolved dependency on LP EC-05. **CE MUST NOT enter its implementation sprint until LP GDD explicitly documents that EC-05 blocks `coin_reward_granted` forwarding for previously completed levels across sessions (Cross-GDD LP-03).** Until LP-03 is confirmed in the LP GDD, the cross-session level-replay scenario is an unguarded economy leak.

*[Cross-GDD LP-02: LP GDD must confirm that `AddCoins` call sites pass the correct `level_id` from the active `LevelCompleted` event context.]*

*[Cross-GDD LP-03 — Resolved 2026-05-08: LP GDD EC-05 now explicitly documents that it blocks `coin_reward_granted` forwarding for any level whose star data is already persisted in SP, regardless of session (cold relaunch, save corruption, migration). CE-12's hard implementation gate is satisfied. AC-55 provides a CI signal for this cross-system invariant.]*

---

**CE-13 — Pity hint grant (stuck-player recovery)**
After `pity_grant_attempt_threshold` consecutive 0-star attempts on the same level, the player receives a pity hint grant of 50 coins. CE does not own the attempt-count tracking — **In-Game HUD GDD is the assigned owner** (OQ-08 resolved 2026-04-27). The HUD subscribes to `GSM.level_complete(par_moves: int)` — the existing GSM event that fires for all level completions, including 0-star — and computes `star_rating` via F-05 (`StarRating(move_count, par_moves)`), where `par_moves` comes from the event payload and `move_count` is read from GSM's current state at event-fire time (e.g., `GSM.GetMoveCount()` or equivalent synchronous query). *[Cross-GDD GSM-02: GSM must expose the current move count as a readable value when `level_complete` fires. If GSM GDD does not yet document this exposure, it must be added before HUD implementation sprint.]* The HUD increments a counter when `star_rating == 0` and `level_id == active_level_id`, resetting on `star_rating > 0` or level change. On threshold breach, HUD calls `CE.AddCoins(50, level_id=-1, EarnSource.PityGrant)`.

*(Implementation note: LP AC-34 discards 0-star `LevelCompleted` events before LP's subscribers receive them. This does not affect the HUD's pity counter — the HUD subscribes to `GSM.level_complete` directly, not LP's `LevelCompleted`. GSM must emit `level_complete` for all completions including 0-star for the HUD's IDLE→FROZEN state transition to function, so no new GSM event is required for CE-13.)*

CE treats this call with no special handling; `level_id=-1` bypasses the idempotency guard. `EarnSource.PityGrant` bypasses `bonus_multiplier`, ensuring the pity grant always delivers exactly 50 coins regardless of active multiplier events (see CE-10).

The calling system is responsible for suppressing the pity grant if `CE.GetCoinBalance() >= hint_cost` (the player already has enough for a hint). The calling system also controls how frequently the grant fires — CE imposes no rate limit.

`pity_grant_attempt_threshold`: default 5, safe range [3, 10]. See Tuning Knobs.

*[Cross-GDD HUD-01 — Partially resolved 2026-04-28: In-Game HUD GDD has implemented: (a) subscription to `GSM.level_complete`, (b) consecutive 0-star attempt counter (resets on non-zero star or level change), and (c) `CE.AddCoins(50, level_id=-1, EarnSource.PityGrant)` on threshold breach, with BLOCKING ACs AC-30–34. Counter and AddCoins call satisfied. Remaining obligation: HUD GDD must add a BLOCKING AC requiring that when `coin_balance_changed` is received with `earn_source == EarnSource.PityGrant`, HUD triggers a differentiated player notification (e.g., "That one was tough — here's a hint on us") distinct from the normal coin pulse. Without this, the pity grant is invisible to the player. Priority: before HUD implementation sprint.]*

*[Cross-GDD GSM-01 — Revised 2026-04-28: Originally required a new `LevelAttemptCompleted(stars: int, level_id: int)` event. This is no longer needed — the HUD uses the existing `GSM.level_complete(par_moves: int)` event and computes star_rating via F-05. GSM GDD does not need to add `LevelAttemptCompleted`. GSM GDD must confirm that `level_complete` fires for ALL level completions, including 0-star (a requirement already implied by HUD's IDLE→FROZEN state transition). If `level_complete` is ever gated to non-zero-star only, CE-13 pity tracking breaks.]*

---

### States and Transitions

Coin Economy is a balance register gated on Save & Persistence initialization. Two states only.

```
LOADING → READY
```

| State | Entry | Exit | Behavior |
|---|---|---|---|
| `LOADING` | CE `Awake()` | `SaveSystem.IsReady == true` | All public methods gated. Debug builds: `GetCoinBalance()`, `AddCoins()`, `SpendCoins()` throw `InvalidOperationException` immediately. Release builds: calls wait via a **non-blocking coroutine (`WaitForSecondsRealtime`) or `async Task`** for up to 2 seconds of wall-clock time (not affected by `Time.timeScale`). A synchronous busy-wait or `Thread.Sleep` is **not permitted** — it would freeze Unity's main thread and trigger ANR (Android) / OS watchdog (iOS). If `OnSaveReady` does not fire within 2 wall-clock seconds, the call logs a timeout warning and returns a safe no-op result — `GetCoinBalance` returns 0, `AddCoins` returns `false` with no mutation, `SpendCoins` returns `false`. **Backgrounding during wait:** On iOS/Android, `WaitForSecondsRealtime` continues to advance during OS suspension. If the app is backgrounded during the 2-second LOADING wait, the timeout may fire on resume even if SP has already initialized. This is accepted behavior — CE will return a no-op result for that in-flight call, and subsequent calls (after READY transition completes on resume) will succeed normally. |
| `READY` | `SaveSystem.IsReady == true` | Never (process lifetime) | CE reads `economy.coin_balance` from SP into its working copy. All public methods live. |

**Note on "process lifetime":** READY is never exited within a single app process. If the OS kills the app in the background and the player relaunches, Unity starts a new process — CE begins in LOADING again and must re-initialize. Systems that cache CE's state must not assume CE is READY across cold launches.

**Initialization sequence:**
1. CE `Awake()` subscribes to `SP.OnSaveReady` event.
2. **Immediately after subscribing**, CE checks `SaveSystem.IsReady` synchronously. If already `true` (SP initialized before CE's `Awake()`), call the initialization logic directly without waiting for the event. This subscribe-then-check pattern prevents the race condition where CE misses the event because SP fired before CE subscribed.
3. On `OnSaveReady` (or synchronous fallback): CE reads `economy.coin_balance` from SP. If absent (first install), initialize to 150 per CE-11 and call `SP.SetCoinBalance(150)`. Transitions to READY.
4. On transition to READY: emit `coin_balance_changed(new_balance: coin_balance, delta: 0)` so In-Game HUD initializes its display with the correct balance before the first level loads.

*[Cross-GDD SP-02: SP GDD C.5 mentions an "IsReady awaitable" but does not define a named `OnSaveReady` event. CE requires a confirmed callback name or exact polling pattern. SP GDD must document the exact integration contract before CE's implementation sprint. CE's subscribe-then-check pattern requires both a subscribable event AND a synchronous `IsReady` bool.]*

---

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| **Save & Persistence** | Bidirectional | CE reads `economy.coin_balance` via SP on `OnSaveReady`. Writes via `SP.SetCoinBalance(int)`. SP owns the dirty flag (set as side effect of `SetCoinBalance`). CE must not access SP before `IsReady`. |
| **Level Progression** | Inbound (events) + Outbound (delegation) | LP calls `CE.AddCoins(amount, level_id, earn_source)` and `CE.GetCoinBalance()` as thin delegates. CE subscribes to LP's `LevelCompleted(stars, level_id, move_count, par_moves)` for analytics. LP state guards (EC-05, EC-06) remain in LP — not re-implemented in CE. LP must pass `level_id` and `EarnSource` on all `AddCoins` delegations. |
| **Level Complete UI** | Indirect (via LP) | LC UI fires `coin_reward_granted(amount)` and `coin_bonus_granted(amount)` to LP; LP forwards to CE via `CE.AddCoins`. CE is not a direct subscriber. LC UI GDD does not need to change its subscription table. |
| **In-Game HUD** | Outbound (event) + Inbound (read) | CE emits `coin_balance_changed(new_balance: int, delta: int, earn_source: EarnSource)` on every successful mutation. HUD reads `CE.GetCoinBalance()` on `level_loaded` for initial display. HUD subscribes to `coin_balance_changed` for 300ms pulse animation (fire-and-forget). **Subscriber contract**: use `new_balance` for balance display; do not use `delta` as a "coins earned this level" amount — `delta` reflects the actual balance change after INT_MAX clamping, which may differ from the requested award when the clamp fires. When `earn_source == EarnSource.PityGrant`, trigger a differentiated player notification distinct from the normal coin pulse (see Cross-GDD HUD-01). When `earn_source == EarnSource.Spend`, do not play a coin-earn animation — play a spend or deduction visual instead. **Emotional register contract:** the deduction moment must feel like *agency* (the player chose to use this resource), not punishment or loss. A "coin flows to the action" animation is preferred over a counter-decrement flash or red-number format. This is a Cosmetic Not Coercive pillar obligation. |
| **Hint System** (future) | Inbound (spend) | Calls `CE.SpendCoins(50): bool`. Must read `CE.GetCoinBalance()` to gate hint button enabled state. Must not call `SpendCoins` before checking balance. |
| **Shop UI** (future) | Inbound (spend) | Calls `CE.SpendCoins(price): bool` for each tier (75, 300, 1200, 2800). Subscribes to `coin_balance_changed` to keep displayed balance live without polling. **Subscriber contract**: use `new_balance` for balance display; do not use `delta` as a transaction amount. Filter `earn_source`: animate balance down on `EarnSource.Spend`, animate up on earn sources. |

## Formulas

### Reference: Upstream Formulas (Level Complete UI GDD)

F-01 (Star Rating), F-02 (Coin Reward base), and F-03 (Ad Bonus) are **owned by the Level Complete UI GDD** (Section D). Coin Economy must not redefine them. CE receives their outputs via LP routing and treats them as inputs. CE expects:

- `stars: int` in range [1, 3]. Stars = 0 never reaches CE — Level Progression discards `LevelCompleted` events with `stars_earned = 0` (LP AC-34).
- `base_coins: int` in range [0, 40] at default tuning (`coin_reward_per_star = [0, 15, 20, 40]`). Arrives via LP's EC-05-gated forwarding as `CE.AddCoins(base_coins, level_id, EarnSource.Base)`.
- `coin_bonus: int` in range [0, 40] at default tuning (`floor(base_coins × (ad_multiplier − 1))` where `ad_multiplier = 2.0`). Arrives as a separate `CE.AddCoins(coin_bonus, level_id, EarnSource.AdBonus)` call from LP's EC-06-gated forwarding of `coin_bonus_granted`.

If Level Complete UI GDD changes `coin_reward_per_star` or `ad_multiplier`, those are the authoritative values and the output ranges in F-CE-01 below change accordingly.

---

### CE-Owned Formulas

---

**F-CE-01 — Coin Award**

The `coin_award` formula is defined as:

`coin_award = floor(base_coins × bonus_multiplier)`  *(EarnSource.Base only)*

`coin_award = base_coins`  *(EarnSource.AdBonus or EarnSource.PityGrant — bonus_multiplier not applied)*

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Base coins | `base_coins` | int | [0, 40] (both base and ad-bonus paths at default tuning) | Coin amount arriving at CE from Level Complete UI via LP routing. Both paths produce values in [0, 40] at default tuning — the base earn is `coin_reward_per_star[stars]` ([0, 15, 20, 40] at 0/1/2/3★), the ad bonus is `floor(base_coins × (ad_multiplier − 1))`. These arrive as two separate `AddCoins` calls. |
| Bonus multiplier | `bonus_multiplier` | float | [1.0, 2.0] | External scaling factor applied only to `EarnSource.Base` calls. Default 1.0 in Beta scope. |
| Earn source | `earn_source` | EarnSource | {Base, AdBonus, PityGrant, Spend} | Determines whether `bonus_multiplier` is applied. Base: multiplier applied. AdBonus and PityGrant: multiplier not applied. PityGrant caller: In-Game HUD (CE-13). Spend: emitted by SpendCoins — not an AddCoins path; included here because `earn_source` is a field on `coin_balance_changed` for all mutation paths. |
| Coin award | `coin_award` | int | [0, 40] Base default (bonus_multiplier=1.0); [0, 80] Base at bonus_multiplier=2.0; [0, 40] AdBonus (multiplier never applied); [25, 100] PityGrant (equals `hint_cost` tuning knob, never multiplied) | Integer amount passed to F-CE-03 for crediting to `coin_balance`. PityGrant range exceeds the AdBonus ceiling at `hint_cost=100`; tune together. |

**Output Range:** [0, 40] for both paths at default Beta tuning (bonus_multiplier=1.0). The `floor()` is a no-op when `base_coins` is an integer and `bonus_multiplier=1.0`. `floor()` becomes meaningful only at non-integer `bonus_multiplier` values such as 1.5 or 1.75.

**Examples:**
- 2★ base award, earn_source=Base, bonus_multiplier=1.0 → `floor(20 × 1.0) = 20`
- 2★ base award, earn_source=Base, bonus_multiplier=1.5 → `floor(20 × 1.5) = 30`
- 3★ ad bonus, earn_source=AdBonus, bonus_multiplier=1.5 → `coin_award = 40` (multiplier NOT applied)
- 1★ base award, earn_source=Base, bonus_multiplier=1.5 → `floor(15 × 1.5) = floor(22.5) = 22` (floor applies to non-integer product)

---

**F-CE-02 — Spend Validation**

The `spend_validation` formula is defined as:

`spend_valid = (amount > 0) AND (coin_balance ≥ amount)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Requested spend amount | `amount` | int | 1–INT_MAX (valid input); ≤0 is a caller bug caught by clause 1 | Coin cost of the action (Accent=75; hint=50; Finish=300; Set=1,200; Workshop=2,800). |
| Current balance | `coin_balance` | int | 0–INT_MAX | CE's working copy read from SP on `OnSaveReady`. |
| Spend valid | `spend_valid` | bool | {false, true} | True only when both clauses pass. Controls whether SpendCoins proceeds to F-CE-03. |

**Output Range:** Boolean. Evaluated fail-fast: clause 1 checked before clause 2. `false` is terminal — no retry state, no queued-deduction path.

**Examples:**
- `amount=50, coin_balance=120` → clause 1: true; clause 2: true → `spend_valid = true` → deduction proceeds
- `amount=300, coin_balance=120` → clause 1: true; clause 2: false → `spend_valid = false` → no mutation
- `amount=0, coin_balance=120` → clause 1: false → `spend_valid = false` → no mutation, log caller bug warning

---

**F-CE-03 — Coin Balance Update**

The `coin_balance_update` formula has two variants:

*Earn path (AddCoins — after F-CE-01):*

`coin_balance' = (int)min((long)coin_balance + coin_award, (long)INT_MAX)`

**Implementation note:** The addition **must use `long` arithmetic** before the `min()` clamp. In C# default (unchecked) arithmetic, `int + int` wraps to a negative value when the result exceeds `INT_MAX` — the `min()` clamp evaluates after the overflow and cannot protect against it. The correct implementation is:
```csharp
long newBalanceLong = (long)coinBalance + coinAward;
coinBalance = (int)Math.Min(newBalanceLong, (long)int.MaxValue);
```

*Spend path (SpendCoins — only when F-CE-02 returns true):*

`coin_balance' = coin_balance − amount`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Prior balance | `coin_balance` | int | 0–INT_MAX | CE working copy before the operation (after `max(0, coin_balance)` clamp for corrupted values). |
| Coin award (earn) | `coin_award` | int | 0–80 (Base at max bonus); 0–40 (AdBonus) | Output of F-CE-01. |
| Spend amount (spend) | `amount` | int | 1–`coin_balance` | Validated by F-CE-02. Guaranteed ≤ `coin_balance`. |
| Updated balance | `coin_balance'` | int | 0–INT_MAX | New `coin_balance` value. Written via `SP.SetCoinBalance(coin_balance')`. |

**Output Range:**
- Earn: [coin_balance, INT_MAX]. Balance can only stay the same (`coin_award = 0` is a no-op per CE-07) or increase. `long` intermediate prevents overflow before the INT_MAX clamp.
- Spend: [0, coin_balance]. Floor of 0 is structural — F-CE-02 uses `≥` ensuring `amount ≤ coin_balance` before this formula runs; spending exactly `coin_balance` coins (your last coin) is permitted and produces `coin_balance' = 0`.

**Examples:**
- Earn: `coin_balance=80, coin_award=20` → `(int)min(100L, INT_MAX_L) = 100`
- Earn INT_MAX clamp (near-max): `coin_balance=2147483640, coin_award=20` → `(int)min(2147483660L, 2147483647L) = 2147483647`; `actual_delta = 2147483647 − 2147483640 = 7`
- Earn INT_MAX clamp (at max): `coin_balance=INT_MAX, coin_award=20` → `(int)min(2147483667L, 2147483647L) = 2147483647`; `actual_delta = INT_MAX − INT_MAX = 0`
- Spend: `coin_balance=120, amount=50` (validated) → `120 − 50 = 70`

**Post-mutation (both paths):** CE emits `coin_balance_changed(new_balance: coin_balance', delta: coin_balance' − prior_balance, earn_source: earn_source)` after writing the updated value. The delta is the **actual change applied**, not the raw requested amount — this matters when the INT_MAX clamp fires on the earn path. For SpendCoins, `earn_source` is always `EarnSource.Spend`. For AddCoins, `earn_source` is the value passed by the caller (Base, AdBonus, or PityGrant). **Emit ordering is load-bearing:** the emit must occur after `SP.SetCoinBalance` succeeds and after the idempotency guard is updated. A refactor that moves the emit before the SP write or rollback block will cause HUD to display a balance that was never persisted.

**Delta at INT_MAX clamp:** `delta = INT_MAX − old_balance`. This is 0 only when `old_balance` was already INT_MAX. When `old_balance < INT_MAX` and the clamp fires (e.g., old_balance=2147483640, coin_award=20), the delta is 7, not 0 and not 20.

---

### Formula Dependency Chain

```
LevelCompleted(stars, level_id, ...)    [LP event — Level Progression GDD]
    ↓
F-01 Star Rating              [Level Complete UI GDD — upstream]
    ↓
F-02 Coin Reward (base)       [Level Complete UI GDD — upstream]
    ↓ base_coins, level_id, EarnSource.Base
F-CE-01 Coin Award            [Coin Economy GDD — CE-owned, bonus_multiplier applied]
    ↓ coin_award
F-CE-03 Balance Update        [Coin Economy GDD — CE-owned, earn path]
    ↓ coin_balance'
economy.coin_balance          [Save & Persistence — persisted]

F-03 Ad Bonus                 [Level Complete UI GDD — upstream]
    ↓ coin_bonus, level_id, EarnSource.AdBonus
F-CE-01 Coin Award            [Coin Economy GDD — coin_award = coin_bonus, no multiplier]
    ↓ coin_award
F-CE-03 Balance Update        [Coin Economy GDD — earn path]
    ↓ coin_balance'
economy.coin_balance          [Save & Persistence — persisted]

SpendCoins(amount) call
    ↓
F-CE-02 Spend Validation      [Coin Economy GDD — CE-owned]
    ↓ spend_valid = true
F-CE-03 Balance Update        [Coin Economy GDD — spend path]
    ↓ coin_balance'
economy.coin_balance          [Save & Persistence — persisted]
```

## Edge Cases

- **If any public method (`GetCoinBalance`, `AddCoins`, `SpendCoins`) is called while CE is in LOADING state**: In debug builds, throws `InvalidOperationException` immediately. In release builds, the call waits via a **non-blocking coroutine (`WaitForSecondsRealtime`) or `async Task`** for up to 2 wall-clock seconds (not affected by `Time.timeScale`). If `OnSaveReady` fires during this wait, CE transitions to READY and the in-flight call completes normally as a READY-state invocation. If 2 wall-clock seconds elapse without `OnSaveReady` firing, the call logs a timeout warning and returns a safe no-op result — `GetCoinBalance` returns 0, `AddCoins` returns `false` with no mutation, `SpendCoins` returns `false`. No SP call and no event emission occurs on timeout. **Backgrounding behavior:** `WaitForSecondsRealtime` continues advancing during OS suspension on iOS/Android. If the app is backgrounded during the 2-second wait, the timeout may fire on resume even if SP has already initialized. This is accepted behavior — CE returns a no-op result for that in-flight call, and the next call (after READY) succeeds. Do not use `OnApplicationPause` to pause the countdown unless this acceptance changes.

- **If CE reads `economy.coin_balance` on `OnSaveReady` and the field is absent (first-ever install)**: CE initializes `coin_balance` to **150** (starter grant per CE-11). The dirty flag is set and `SP.SetCoinBalance(150)` is called before CE transitions to READY. This differs from SP Case R-3 defaults (SP AC-15, which default to 0) — CE's 150-coin starter overrides the SP default.

- **If CE reads `economy.coin_balance` from SP and the loaded value is negative (corrupted save data)**: CE clamps `coin_balance` to 0, calls `SP.SetCoinBalance(0)` immediately to repair the save, and logs the anomaly. SP AC-27 already clamps at the SP read layer; this is CE's own defensive read on arrival. The clamp is applied to CE's working copy before CE transitions to READY and emits the init `coin_balance_changed`.

- **If `GetCoinBalance()` is called when `coin_balance` is negative in the working copy (internal invariant violation post-initialization)**: Repairs the working copy to 0 in memory, logs a contract violation warning, and returns 0. By repairing in memory, CE ensures any pending W-2 (app-pause) flush uses the corrected state. The getter does NOT call `SP.SetCoinBalance` — the repair is in-memory only; the next mutation writes the corrected value to SP. Subsequent `AddCoins` or `SpendCoins` calls continue to clamp with `max(0, coin_balance)` defensively: `AddCoins(20)` on a post-getter-repair working copy sees 0, clamps to 0, adds 20, result is 20.

- **If LP's EC-05 guard fails and `CE.AddCoins(amount, level_id, EarnSource.Base)` is called a second time for the same level (LP bug or test harness misfire)**: CE-12's idempotency guard rejects the duplicate. `last_credited_level_id == level_id` is true; CE logs a duplicate-credit warning and returns no-op. No double-credit occurs.

- **If `AddCoins(0)` is called**: No-op. No mutation, no SP call, no `coin_balance_changed` event. This is a legitimate code path — not a caller bug. No warning is logged.

- **If `AddCoins(amount)` is called with `amount < 0`**: No-op. Log a caller bug warning. No mutation, no event. Negative amounts passed to `AddCoins` are a contract violation; CE does not negate them or treat them as spend operations.

- **If `coin_balance` is at `INT_MAX` and `AddCoins(coin_award)` is called**: The `(long)` intermediate prevents overflow: `(long)INT_MAX + coin_award` is evaluated as a `long`, clamped to `(long)INT_MAX`, then cast back to `int`. Balance stays at `INT_MAX`. `actual_delta = INT_MAX − INT_MAX = 0`. `coin_balance_changed(new_balance: INT_MAX, delta: 0)` is emitted. No overflow or exception.

- **If `SpendCoins(amount)` is called where `amount == coin_balance` exactly (spend your last coin)**: F-CE-02: `amount > 0` (true) AND `coin_balance >= amount` (true — equal counts). Spend proceeds. `coin_balance = 0`. SP.SetCoinBalance(0). `coin_balance_changed(new_balance: 0, delta: −amount)` emitted. Returns `true`. The `>=` in F-CE-02 is intentional.

- **If `SpendCoins(300)` and `SpendCoins(50)` are both dispatched in the same Unity frame with `coin_balance = 300`**: Unity's main loop is single-threaded — both calls execute sequentially. First call: F-CE-02 passes, balance → 0, returns `true`. Second call: F-CE-02 fails, returns `false`, no mutation. Outcome is deterministic. Callers must pre-check balance before enabling spend UI and treat `false` as terminal denial.

- **If `SP.SetCoinBalance` throws during `AddCoins` (SP write failure)**: CE rolls back `coin_balance` to the pre-mutation `working_balance`. The idempotency guard (`last_credited_level_id`) is NOT advanced. `coin_balance_changed` is NOT emitted. The caller may retry — CE-12 will allow a fresh credit for the same `level_id` because the guard was never committed. This failure path is silent to the player (no UI feedback from CE). SP's write-failure handling (dirty flag, retry on next W-1/W-2) is responsible for eventual persistence; CE's rollback ensures its working copy remains consistent with the last successfully persisted state.

- **If `coin_balance_changed` is emitted and no subscriber is registered**: Safe no-op. CE must use null-conditional invocation (`?.Invoke(...)` in C#) to prevent `NullReferenceException`.

- **If `SP.SetCoinBalance` throws on every call in READY state (persistent SP failure — e.g., device storage full):** CE rolls back the working copy and returns `false` on every `AddCoins` and `SpendCoins` mutation. The player's in-session coin balance appears functional (reads return the working copy) but nothing new persists to disk. CE has no circuit-breaker or READY → ERROR state transition — SP's failure handling owns the persistence recovery layer (dirty flag retry, write-then-swap). CE's behavior in this state: every mutation returns `false`; `GetCoinBalance()` returns the in-memory working copy matching the last successfully persisted state; `coin_balance_changed` is never emitted. **Player impact:** the balance the player sees in-session resets to the last saved value on next cold launch. CE cannot notify the player of this condition — that obligation belongs to SP GDD's persistent-failure error reporting. *Accepted scope boundary: CE defers to SP's failure handling rather than owning a degraded mode. Document for SP GDD cross-reference when SP error reporting is designed.*

- **If an external system sets `bonus_multiplier` to a value outside [1.0, 2.0]**: CE clamps at write time. Values below 1.0 are set to 1.0 and a warning is logged. Values above 2.0 are set to 2.0 and a warning is logged.

- **If `AddCoins` is in its 2-second wait (release/LOADING) and `OnSaveReady` fires at t=1.8s**: CE transitions to READY and the in-flight call proceeds as a READY-state invocation. The call is not discarded. The result is equivalent to calling the method after READY transition completed.

## Dependencies

**Systems this GDD depends on (upstream):**

| System | Direction | Nature | Hard/Soft | Interface |
|---|---|---|---|---|
| Save & Persistence | Bidirectional | CE reads `economy.coin_balance` on cold start; writes `coin_balance` changes via `SP.SetCoinBalance(int)` | Hard (cross-session) | `SP.OnSaveReady` (callback/event — see Cross-GDD SP-02), `SP.SetCoinBalance(int)` (write), `SaveSystem.IsReady` (bool for synchronous fallback check), initial `coin_balance` read on `OnSaveReady` |

**Systems that depend on this GDD (downstream):**

| System | Direction | Nature | Hard/Soft | Interface |
|---|---|---|---|---|
| Level Progression | Inbound (events) + delegation | LP delegates `GetCoinBalance()` and `AddCoins()` to CE; calls CE on each `coin_reward_granted`/`coin_bonus_granted` receipt with `level_id` and `EarnSource`; CE subscribes to LP's `LevelCompleted` for analytics. LP must check `AddCoins()` return value — `false` means SP write failure or caller bug; LP should log and may retry | Hard (when CE authored — LP's provisional coin handling is superseded) | `CE.GetCoinBalance(): int`, `CE.AddCoins(amount: int, level_id: int, earn_source: EarnSource): bool` |
| In-Game HUD | Outbound (event) + read | HUD subscribes to `coin_balance_changed` for pulse animation; reads `GetCoinBalance()` on `level_loaded` for initial display | Soft | `CE.GetCoinBalance(): int`, `coin_balance_changed(new_balance: int, delta: int, earn_source: EarnSource)` event subscription |
| Hint System (future) | Inbound (spend) | Hint System calls `SpendCoins` to deduct hint cost; reads balance to gate button enabled state | Hard (when authored) | `CE.SpendCoins(50): bool`, `CE.GetCoinBalance(): int` |
| Shop UI (future) | Inbound (spend) | Shop UI calls `SpendCoins` for skin and Accent purchases; subscribes to balance-change for live display | Hard (when authored) | `CE.SpendCoins(price): bool`, `CE.GetCoinBalance(): int`, `coin_balance_changed` subscription |
| Level Complete UI | Indirect (via LP) | LC UI fires `coin_reward_granted`/`coin_bonus_granted` to LP; LP forwards to CE | Soft | No direct CE interface — routing via LP |
| Main Menu UI (future) | Read-only | Reads coin balance for display on home screen | Soft | `CE.GetCoinBalance(): int` |

**Bidirectional consistency:**
- Save & Persistence GDD: must update its Interactions table to replace the provisional LP/CE entry with CE as the formal `economy.coin_balance` owner, expose `SP.SetCoinBalance(int)` per Cross-GDD SP-01, and document `SaveSystem.IsReady` as a synchronous bool per Cross-GDD SP-02.
- Level Progression GDD: must update AC-21, AC-22, AC-35 (coin mutation logic moves to CE), update `AddCoins` delegation to pass `level_id` and `EarnSource`, update the Section C coin ownership note per Cross-GDD LP-01 and LP-02, and explicitly document that EC-05 blocks `coin_reward_granted` forwarding for previously completed levels regardless of session per Cross-GDD LP-03.
- In-Game HUD GDD: ✓ Resolved 2026-04-28. HUD GDD lists Coin Economy in its Dependencies section. CE-13 pity grant counter implemented in HUD Detailed Design; AC-30–34 present as BLOCKING ACs. Cross-GDD HUD-01 satisfied. Remaining open item: HUD GDD Tuning Knobs must remove `hint_cost = 10` (temp value) and reference CE GDD's canonical `hint_cost = 50` per Cross-GDD HUD-02 (BLOCKING against HUD implementation sprint — see OQ-09).
- Shop UI GDD: MUST specify as BLOCKING requirements: (a) the first-spend onboarding beat that directs new players to spend their starter coins on an Accent cosmetic, and (b) the goal-visibility display showing players how many coins they need for the next unlockable cosmetic tier. CE cannot deliver its Player Fantasy ("the balance that inches toward the next skin unlock") without these UI features. **CE MUST NOT enter its Beta implementation sprint until the Shop UI GDD has accepted these obligations.**

**Hard vs. soft:**
- Save & Persistence: hard — CE cannot read or persist `coin_balance` without it
- Level Progression: hard (when CE authored)
- Hint System: hard (when authored)
- Shop UI: hard (when authored)
- In-Game HUD, Level Complete UI, Main Menu UI: soft

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect if Too High | Effect if Too Low |
|---|---|---|---|---|
| `starter_coins` | 150 | 75–300 | New players unlock Accent cosmetic on install with no play required; reduces sense of earning | Not enough for even one hint on first install; "never short of resources" promise breaks on day 1 |
| `coin_reward_per_star[1]` | 15 | 10–25 | 1★ earns near-2★ rates; star tier distinction weakens | Below 10: casual players accumulate too slowly; hints feel inaccessible even with starter grant. At 10: 1-hint/session player runs net −20/session, balance drains to 0 in ~7 sessions — violates "never short of progression" promise |
| `coin_reward_per_star[2]` | 20 | 10–50 | Flattens the earn gap between 2★ and 3★ | Too small a reward for a solid performance |
| `coin_reward_per_star[3]` | 40 | 20–100 | 3★ players hit skin prices very fast; reduces aspiration pull of Workshop tier | Expert play barely exceeds casual play; mastery feels unrewarded |
| `skin_price_accent` | 75 | 50–150 | Accent reachable only after several sessions; loses "quick win" purpose | At current default (75), players can unlock 2 Accent cosmetics from the 150-coin starter grant alone — this is intentional (Accent is the day-one quick-win anchor). Raising above 75 defeats its purpose; the "Too High" effect applies to values substantially above this. |
| `pity_grant_attempt_threshold` | 5 | 3–10 | Pity hints arrive too frequently; reduces challenge perception | Stuck players accumulate too many 0-coin failed attempts before receiving relief; "never short" promise breaks for the exact players who most need it |
| `hint_cost` | 50 | 25–100 | Hints feel prohibitively expensive on hard tiers for 1★ players | Hints are near-free; no meaningful resource decision |
| `skin_price_finish` | 300 | 150–600 | First full skin purchase out of reach in 1–2 sessions | First skin unlocked in a single session; undercuts aspiration |
| `skin_price_set` | 1,200 | 600–2,400 | Short-term goal feels out of reach for casual players | Set reachable before Easy tier |
| `skin_price_workshop` | 2,800 | 1,500–5,000 | Late-game premium requires excessive grind even with ad engagement | Reachable mid-game without ads; loses premium feel |
| `ad_multiplier` | 2.0 | 1.5–3.0 | Ad-watching players hit Workshop in under 40 levels | Below 1.5 the bonus feels trivial |
| `bonus_multiplier` | 1.0 | 1.0–2.0 | At 2.0, 3★ base earn is 80 coins/level (base path only; ad bonus unaffected by this multiplier) | Cannot go below 1.0; clamped at write time |

*`ad_offer_show_rate` is owned by the Level Complete UI GDD. Operating intent: 0.5–0.7 for normal play sessions; 1.0 reserved for A/B testing. At ad_multiplier=2.0, show_rate=0.5: a 3★ player earns ~60 coins/level average; Workshop reachable in ~47 levels. At show_rate=1.0: ~35 levels. Tune these together.*

**Opt-in earn accelerator (ad_multiplier design intent):** The ad bonus doubles the base earn for that level. At 1★, this raises earn from 10→20 coins/level; at 3★, from 40→80. Workshop is reachable ~34% faster for consistent ad-watchers at 3★ (47 vs 70 levels). This earn advantage is intentional and accepted — the "Cosmetic Not Coercive" pillar applies to the no-ad baseline: a player who never watches a single ad can unlock all cosmetics through normal play with no penalty. Ads are an accelerator, not a correction for artificial scarcity. Do not compress `ad_multiplier` below 1.5 — below that threshold the bonus feels trivial relative to the gap between cosmetic tiers.

**Knob interactions:**
- `skin_price_accent` is the day-one quick-win anchor, deliberately priced so new players can unlock it from the starter grant alone. The general "sessions-to-unlock" guideline does not apply to Accent. When tuning Accent, target: reachable from starter grant with coins left over.
- `coin_reward_per_star` and non-Accent skin prices are a coupled pair. Guideline: `skin_price_finish ≈ 7–10 sessions × average coins earned per session`.
- `hint_cost` should stay in the range [3, 8] × `coin_reward_per_star[1]`. At defaults: `50 / 15 ≈ 3.3`. Economy projections assume 3 levels/session (unverified — confirm against level duration data from Level Data System GDD when available; if average session is 2 levels, all Workshop projections lengthen by 33%). **1★ player projections (majority audience — use these as the design reference, not 3★):** At 0 hints/session: net earn = 45 coins/session; Finish skin in ~7 sessions from 0 coins, or ~3–4 sessions from the 150-coin starter grant. At 1 hint/session: net earn ≈ −5 coins/session — balance drains; pity grant (CE-13) provides targeted relief for stuck players; the Promise is "almost always able to keep moving," not "unlimited consumables." At 0 hints, no ads: Workshop in ~62 sessions (~21 weeks at 3 sessions/week). This is the majority-segment ceiling; it is accepted as "aspirational" — a player who never uses hints reaches Workshop through consistent play. At 1★ + ads at show_rate=0.5: ~41 sessions (~14 weeks). **3★ projections (strong minority segment):** At 0 hints, no ads: Workshop in ~24 sessions. At show_rate=0.5: ~47 levels. 1★ + ads every level (show_rate=1.0): 30 coins/level × 3 levels = 90 coins/session → Workshop in ~31 sessions.
- `bonus_multiplier` applies to the base earn path only. Maximum base earn at bonus_multiplier=2.0 and 3★: `floor(40 × 2.0) = 80` coins per level. Workshop reachable in: `2800 / 80 = 35 levels` (no ads). At bonus_multiplier=1.0 + 3★ + ads at show_rate=0.5: ~47 levels. **Combined ceiling (both multipliers active):** At bonus_multiplier=2.0 + 3★ + ads at show_rate=1.0: base = 80 coins/level, ad bonus = 40 (multiplier not applied to AdBonus path), total = 120 coins/level → Workshop in ~24 levels. This is the economy ceiling; the Daily Challenge System must bound `bonus_multiplier` tuning with this ceiling in view. These are the economy ceiling projections — tune together.

**Non-tunable design decisions (conscious locks):**
- `coin_balance` floor = 0 (hard — no negative balance permitted by design)
- `coin_balance` cap = `INT_MAX` (no hard game cap; only a safety overflow guard using `long` arithmetic)
- Spend model = validated (not optimistic)
- `bonus_multiplier` cannot apply to ad-bonus path (CE-10, CE-12 architectural decision)

## Visual/Audio Requirements

Not applicable. Coin Economy is a pure data and logic layer with no visual or audio output. The coin pulse animation and HUD display are owned by the In-Game HUD GDD. The coin reward animation on the Level Complete screen is owned by the Level Complete UI GDD. CE only emits `coin_balance_changed(new_balance, delta)` — the visual response to that event belongs to the subscribing UI systems.

## UI Requirements

Not applicable. Coin Economy owns no UI screens and renders no widgets. Its data is consumed by:
- **In-Game HUD**: displays live `coin_balance` and subscribes to `coin_balance_changed` for pulse animation
- **Level Complete UI**: displays `coins_earned` and `coin_bonus` amounts (computed upstream, not by CE)
- **Shop UI** (future): displays current balance, calls `SpendCoins` for all five spend tiers, subscribes to `coin_balance_changed`
- **Main Menu UI** (future): displays `GetCoinBalance()` on home screen

UI layout and interaction specifications belong in those systems' GDDs.

## Acceptance Criteria

**Test infrastructure note — negative SP call assertions:** ACs asserting `"SP.SetCoinBalance is NOT called"` or `"no SP call"` require SP to be wrapped in a call-recording test double (mock or spy). The test must assert `spy.SetCoinBalanceCallCount == 0`. A test that does not configure the call-recording double cannot verify this assertion and must be marked CANNOT VERIFY. This applies to: AC-04b, AC-06, AC-09, AC-12, AC-13, AC-20, AC-21, AC-35, AC-35b, AC-42, AC-49, AC-54, and any future AC asserting no-SP-call.

### Initialization

| ID | Level | Criterion |
|---|---|---|
| AC-01 | BLOCKING | GIVEN CE is in LOADING state (SP `OnSaveReady` has not fired), WHEN `GetCoinBalance()` is called in a debug build, THEN an `InvalidOperationException` is thrown immediately; no return value is produced and no event is emitted. |
| AC-02 | BLOCKING | GIVEN CE is in LOADING state, WHEN `AddCoins(10)` is called in a debug build, THEN an `InvalidOperationException` is thrown immediately; `coin_balance` is not mutated, the dirty flag is not set, and `coin_balance_changed` is not emitted. |
| AC-03 | BLOCKING | GIVEN CE is in LOADING state, WHEN `SpendCoins(50)` is called in a debug build, THEN an `InvalidOperationException` is thrown immediately; `coin_balance` is not mutated and `SpendCoins` does not return `true`. |
| AC-04 | BLOCKING | GIVEN this is the first install and `economy.coin_balance` is absent from the SP save file, WHEN `SP.OnSaveReady` fires and CE transitions to READY, THEN `GetCoinBalance()` returns `150` (starter grant per CE-11); the dirty flag is set; `SP.SetCoinBalance(150)` is called before CE enters READY. |
| AC-04b | BLOCKING | GIVEN `economy.coin_balance = 75` is present in the SP save file (key exists and is non-absent — simulating a player who earned and partially spent coins after first install), WHEN CE initializes on cold launch and `OnSaveReady` fires, THEN `GetCoinBalance()` returns `75`; the starter grant (150 coins) is NOT applied; `SP.SetCoinBalance` is NOT called at all during initialization (no repair write, no starter grant); `coin_balance_changed(new_balance: 75, delta: 0, earn_source: EarnSource.Base)` is emitted exactly once on READY transition. Verifies that the one-time initialization check is "key absent from save file" not "balance == 0". *File location: `tests/unit/coin-economy/`* |
| AC-05 | BLOCKING | GIVEN SP returns a negative value for `economy.coin_balance` on `OnSaveReady` (corrupted save), WHEN CE reads that value and transitions to READY, THEN CE's working `coin_balance` is clamped to `0`; `SP.SetCoinBalance(0)` is called before CE enters READY; the anomaly is logged; `GetCoinBalance()` subsequently returns `0`. |
| AC-06 | BLOCKING | GIVEN `economy.coin_balance = 500` is loaded from SP on `OnSaveReady` (non-zero, non-absent, non-corrupted — distinguishable from the corrupted-save AC-05 and first-install AC-04 paths), WHEN CE transitions to READY, THEN CE emits `coin_balance_changed(new_balance: 500, delta: 0, earn_source: EarnSource.Base)` exactly once (verified via event spy); `GetCoinBalance()` returns `500`; `SP.SetCoinBalance` is NOT called (normal load path — no repair write, no starter grant). |

### Balance Reads

| ID | Level | Criterion |
|---|---|---|
| AC-07 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 120`, WHEN `GetCoinBalance()` is called, THEN the return value is `120`; `coin_balance` remains `120`; no event is emitted. |
| AC-08 | BLOCKING | GIVEN CE is in READY state, WHEN `GetCoinBalance()` is called three times consecutively without any mutation, THEN all three return values are identical; no mutation, no SP call, and no event emission occurs. |
| AC-09 | BLOCKING | GIVEN CE is in READY state and `coin_balance` is somehow `−5` in the working copy (internal invariant violation), WHEN `GetCoinBalance()` is called, THEN the return value is `0`; a contract violation warning is logged; the working copy `coin_balance` IS repaired to `0` in memory by the getter; `SP.SetCoinBalance` is NOT called (in-memory repair only). A subsequent call to `GetCoinBalance()` returns `0` (not `−5`). |
| AC-09b | BLOCKING | GIVEN CE is in READY state and `coin_balance` is `−5` in the working copy (corruption injected directly without calling `GetCoinBalance()` first), WHEN `AddCoins(20)` is called, THEN CE clamps the working copy to `max(0, coin_balance) = 0` before applying the award; `GetCoinBalance()` returns `20`; `coin_balance_changed(new_balance: 20, delta: +20, earn_source: EarnSource.Base)` is emitted; `SP.SetCoinBalance(20)` is called. The result is 20, not 15. (If `GetCoinBalance()` were called first, the getter would repair to 0 before `AddCoins` runs — the AddCoins clamp is a second defensive line.) |
| AC-10 | BLOCKING (Integration — requires LP refactor LP-01 complete) | GIVEN CE is in READY state, WHEN `LP.GetCoinBalance()` is called, THEN LP makes no independent coin read; LP's return value equals the result of `CE.GetCoinBalance()`; LP holds no `coin_balance` field of its own. *Note: verifies LP's internal structure post-LP-01 refactor. Cannot be verified as a CE unit test — requires LP integration. **File location: `tests/integration/coin-economy-lp/` — do NOT place in `tests/unit/coin-economy/`; will fail CI until LP-01 is merged.*** |

### AddCoins

| ID | Level | Criterion |
|---|---|---|
| AC-11 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80`, WHEN `AddCoins(20)` is called (default earn_source=Base, level_id=-1), THEN the return value is `true`; `GetCoinBalance()` returns `100`; `coin_balance_changed(new_balance: 100, delta: +20, earn_source: EarnSource.Base)` is emitted exactly once; `SP.SetCoinBalance(100)` is called; the dirty flag on SP is set. |
| AC-12 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80`, WHEN `AddCoins(0)` is called, THEN the return value is `false`; `GetCoinBalance()` still returns `80`; no SP call; `coin_balance_changed` is not emitted. |
| AC-13 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80`, WHEN `AddCoins(−10)` is called, THEN the return value is `false`; `GetCoinBalance()` still returns `80`; no SP call; `coin_balance_changed` is not emitted; a caller bug warning is logged. |
| AC-14 | BLOCKING | GIVEN CE is in READY state with `coin_balance = INT_MAX` (`2147483647`), WHEN `AddCoins(20)` is called, THEN `GetCoinBalance()` returns `2147483647`; `coin_balance_changed(new_balance: 2147483647, delta: 0)` is emitted (delta = actual change = 0); no arithmetic overflow or exception occurs. *Note: CE must use `(long)` arithmetic per F-CE-03 implementation note — verifying "no overflow" requires confirming the `(long)` cast or equivalent overflow guard is in the implementation.* |
| AC-15 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 2147483640`, WHEN `AddCoins(20)` is called, THEN `GetCoinBalance()` returns `2147483647` (clamped, not `2147483660`); `coin_balance_changed(new_balance: 2147483647, delta: 7)` is emitted (delta = actual change = `2147483647 − 2147483640 = 7`). |
| AC-16 | BLOCKING (Integration — requires LP with EC-05 guard) | GIVEN LP receives `coin_reward_granted(40)` and its EC-05 state guard passes, WHEN LP calls `CE.AddCoins(40, level_id, EarnSource.Base)`, THEN `GetCoinBalance()` increases by exactly `40`; LP does not apply any mutation of its own. *Requires LP integration test environment. **File location: `tests/integration/coin-economy-lp/` — do NOT place in `tests/unit/coin-economy/`; will fail CI until LP-01 is merged.*** |
| AC-17 | BLOCKING | GIVEN `bonus_multiplier = 1.5` and `earn_source = EarnSource.Base`, WHEN `AddCoins(20, level_id=-1, earn_source=Base)` is called, THEN CE applies F-CE-01 internally: `coin_award = floor(20 × 1.5) = 30`; `GetCoinBalance()` increases by `30`, not `20`. |
| AC-17b | BLOCKING | GIVEN `bonus_multiplier = 1.5` and `earn_source = EarnSource.AdBonus`, WHEN `AddCoins(20, level_id=-1, earn_source=AdBonus)` is called, THEN CE does NOT apply `bonus_multiplier`; `coin_award = 20`; `GetCoinBalance()` increases by exactly `20`. |

### SpendCoins

| ID | Level | Criterion |
|---|---|---|
| AC-18 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 120`, WHEN `SpendCoins(50)` is called, THEN returns `true`; `GetCoinBalance()` returns `70`; `coin_balance_changed(new_balance: 70, delta: −50)` is emitted exactly once; `SP.SetCoinBalance(70)` is called. |
| AC-19 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 120`, WHEN `SpendCoins(300)` is called (amount exceeds balance), THEN returns `false`; `GetCoinBalance()` returns `120`; `coin_balance_changed` is not emitted. |
| AC-20 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 120`, WHEN `SpendCoins(0)` is called, THEN returns `false`; `GetCoinBalance()` returns `120`; `coin_balance_changed` is not emitted; a caller bug warning is logged. |
| AC-21 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 120`, WHEN `SpendCoins(−50)` is called, THEN returns `false`; `GetCoinBalance()` returns `120`; `coin_balance_changed` is not emitted; a caller bug warning is logged. |
| AC-22 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 300`, WHEN `SpendCoins(300)` is called (exact-balance spend), THEN returns `true`; `GetCoinBalance()` returns `0`; `coin_balance_changed(new_balance: 0, delta: −300)` is emitted; `SP.SetCoinBalance(0)` is called. |
| AC-23 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 300`, WHEN `SpendCoins(300)` is dispatched first and `SpendCoins(50)` is dispatched second in the same Unity frame, THEN the first call returns `true`; `coin_balance` becomes `0`; `coin_balance_changed(new_balance: 0, delta: −300)` is emitted exactly once; the second call returns `false` (F-CE-02 fails: `0 < 50`); no second mutation or SP call occurs. Verifies that the second call observes the post-first-mutation balance rather than the pre-frame snapshot. |
| AC-24 | BLOCKING | GIVEN five **independent** test cases each starting with `coin_balance` equal to exactly the spend amount (e.g., `coin_balance=75` for `SpendCoins(75)`) — state is NOT shared between cases — WHEN each of the five Beta spend contexts is exercised in its own test case: `SpendCoins(75)`, `SpendCoins(50)`, `SpendCoins(300)`, `SpendCoins(1200)`, `SpendCoins(2800)`, THEN each call returns `true`; `GetCoinBalance()` returns 0; `coin_balance_changed(new_balance: 0, delta: −[spend_amount])` is emitted exactly once; `SP.SetCoinBalance(0)` is called. *Running these as a single sequential test is incorrect — use independent test cases with fresh state per call.* |

### Events

| ID | Level | Criterion |
|---|---|---|
| AC-25 | BLOCKING | GIVEN no subscribers are registered for `coin_balance_changed`, WHEN `AddCoins(10)` is called, THEN no `NullReferenceException` is thrown; `GetCoinBalance()` returns the updated value. |
| AC-26 | BLOCKING | GIVEN no subscribers are registered for `coin_balance_changed`, WHEN `SpendCoins(50)` is called with sufficient balance, THEN no `NullReferenceException` is thrown; `SpendCoins` returns `true`. |
| AC-27 | BLOCKING | GIVEN a subscriber to `coin_balance_changed` reads `GetCoinBalance()` inside the handler, WHEN CE emits `coin_balance_changed(new_balance: 100, delta: +20, earn_source: EarnSource.Base)` after `AddCoins(20, level_id=-1, earn_source=Base)`, THEN the subscriber observes `coin_balance = 100` (post-mutation value); the event payload `new_balance` matches `GetCoinBalance()`; the payload `earn_source` is `EarnSource.Base`. |
| AC-27b | BLOCKING | GIVEN a subscriber to `coin_balance_changed` reads `GetCoinBalance()` inside the handler, WHEN CE emits `coin_balance_changed(new_balance: 70, delta: −50, earn_source: EarnSource.Spend)` after `SpendCoins(50)`, THEN the subscriber observes `coin_balance = 70`; the payload `delta` is `−50`; the payload `earn_source` is `EarnSource.Spend`. |
| AC-28 | BLOCKING (Integration — requires LP instantiation or LP event stub) | GIVEN CE subscribes to LP's `LevelCompleted(stars, level_id, move_count, par_moves)` event AND a call counter is registered on CE's handler AND LP is instantiated as a real or stub dependency, WHEN LP fires `LevelCompleted(stars:2, level_id:5, move_count:12, par_moves:10)` through LP's own event dispatch mechanism, THEN the call counter increments by exactly 1; no exception propagates to LP's dispatch site; `CE.GetCoinBalance()` is unchanged after the handler (earn arrives separately via `CE.AddCoins` forwarding). *Distinction from AC-46: AC-28 fires through LP's dispatch path (integration); AC-46 fires directly via test harness (unit). File location: `tests/integration/coin-economy-lp/`* |

### Bonus Multiplier

| ID | Level | Criterion |
|---|---|---|
| AC-29 | BLOCKING | GIVEN `bonus_multiplier` is set to `1.5` (within [1.0, 2.0] range), WHEN the value is written, THEN `bonus_multiplier` reads back as `1.5` with no warning logged. |
| AC-30 | BLOCKING | GIVEN `bonus_multiplier` is set to `0.5` (below minimum), WHEN the write is applied, THEN CE clamps to `1.0`; a warning is logged; `bonus_multiplier` reads back as `1.0`. |
| AC-31 | BLOCKING | GIVEN `bonus_multiplier` is set to `3.0` (above maximum), WHEN the write is applied, THEN CE clamps to `2.0`; a warning is logged; `bonus_multiplier` reads back as `2.0`. |
| AC-32 | BLOCKING | GIVEN `bonus_multiplier = 1.25` and `AddCoins(10, level_id=-1, earn_source=Base)` is called, WHEN CE applies F-CE-01 internally, THEN `coin_award = floor(10 × 1.25) = floor(12.5) = 12` (not 13); `GetCoinBalance()` increases by exactly `12`. |
| AC-33 | BLOCKING | GIVEN `bonus_multiplier = 1.0` (default Beta value), WHEN `AddCoins(20, level_id=-1, earn_source=Base)` is called, THEN `GetCoinBalance()` increases by exactly `20`; no fractional rounding artifact is introduced. |

### Edge Cases

| ID | Level | Criterion |
|---|---|---|
| AC-34 | BLOCKING (Unit — uses SP test double; no multi-system integration required) | GIVEN CE is in LOADING state in a release build AND SP is configured with a test double (mock/stub) that never fires `OnSaveReady`, WHEN `GetCoinBalance()` is called and `2 wall-clock seconds` of real time elapse (`WaitForSecondsRealtime` — not affected by `Time.timeScale`), THEN `GetCoinBalance()` returns `0`; a timeout warning is logged; no mutation or SP call occurs. *Note: the test double must prevent `OnSaveReady` from firing; without it the test is non-deterministic on fast hardware where SP may initialize in milliseconds. **This test must run as a Play Mode test (Unity Test Framework play-mode suite) — `WaitForSecondsRealtime` does not advance in Edit Mode and the test will hang indefinitely if placed in Edit Mode.** File location: `tests/unit/coin-economy/`* |
| AC-35 | BLOCKING (Unit — uses SP test double; no multi-system integration required) | GIVEN CE is in LOADING state in a release build AND SP is configured with a test double that never fires `OnSaveReady`, WHEN `SpendCoins(50)` is called and `2 wall-clock seconds` of real time elapse, THEN `SpendCoins` returns `false`; a timeout warning is logged; no mutation or SP call occurs. ***Play Mode only** — see AC-34 note. File location: `tests/unit/coin-economy/`* |
| AC-35b | BLOCKING (Unit — uses SP test double; no multi-system integration required) | GIVEN CE is in LOADING state in a release build AND SP is configured with a test double that never fires `OnSaveReady`, WHEN `AddCoins(40)` is called and `2 wall-clock seconds` of real time elapse, THEN `AddCoins` performs no mutation; dirty flag on SP is not set; `SP.SetCoinBalance` is not called; `coin_balance_changed` is not emitted; a timeout warning is logged. ***Play Mode only** — see AC-34 note. File location: `tests/unit/coin-economy/`* |
| AC-35c | BLOCKING | GIVEN CE is in LOADING state in a release build AND SP is configured with a test double that fires `OnSaveReady` (with `coin_balance = 200`) at t=1.8s (simulated via a controlled delay), WHEN `GetCoinBalance()` is called at t=0 and its 2-second wait begins, THEN when `OnSaveReady` fires at t=1.8s CE transitions to READY; `GetCoinBalance()` returns `200` (not `0` — the call resolves as a READY-state invocation with the SP-provided balance, not as a timeout no-op). *The 0.2s margin (event at t=1.8s, timeout at t=2.0s) is safe because `OnSaveReady` is delivered synchronously on Unity's main thread — there is no race condition. Do not narrow the margin below 0.1s on CI hardware.* ***Play Mode only** — `WaitForSecondsRealtime` does not advance in Edit Mode and the test will hang indefinitely if placed in Edit Mode. File location: `tests/unit/coin-economy/`* |
| AC-35d | BLOCKING | GIVEN CE is in LOADING state in a release build AND SP is configured with a test double that fires `OnSaveReady` (with `coin_balance = 200`) at t=1.8s, WHEN `SpendCoins(50)` is called at t=0 and its 2-second wait begins, THEN when `OnSaveReady` fires at t=1.8s CE transitions to READY; `SpendCoins(50)` returns `true`; `GetCoinBalance()` returns `150`; `coin_balance_changed(new_balance: 150, delta: −50, earn_source: EarnSource.Spend)` is emitted. ***Play Mode only** — File location: `tests/unit/coin-economy/`* |
| AC-35e | BLOCKING | GIVEN CE is in LOADING state in a release build AND SP is configured with a test double that fires `OnSaveReady` (with `coin_balance = 200`) at t=1.8s, WHEN `AddCoins(40)` is called at t=0 and its 2-second wait begins, THEN when `OnSaveReady` fires at t=1.8s CE transitions to READY; `AddCoins(40)` returns `true`; `GetCoinBalance()` returns `240`; `coin_balance_changed(new_balance: 240, delta: +40, earn_source: EarnSource.Base)` is emitted. ***Play Mode only** — File location: `tests/unit/coin-economy/`* |
| AC-36 | BLOCKING (Integration — requires LP refactor LP-01 complete) | GIVEN CE is in READY state and LP's provisional coin ownership has been superseded by this GDD, WHEN a test inspects LP's internal state, THEN LP has no `coin_balance` member variable; `LP.GetCoinBalance()` contains only a delegation to `CE.GetCoinBalance()`; `LP.AddCoins()` contains only a delegation to `CE.AddCoins()`. *Note: verifies LP internal structure — belongs in LP GDD integration suite.* |
| ~~AC-37~~ | *(Removed — merged into AC-06, which already tests the 500-coin load path with event emission assertion. AC-37 was a strict subset of AC-06 with no additional assertion.)* | — |
| AC-38 | BLOCKING | GIVEN CE is integrated with SP on a physical device (Samsung Galaxy A13 / iPhone with iOS minimum spec) and `coin_balance = X` (note X before test), WHEN `AddCoins(40)` is called (raising balance to X+40) and the app is force-quit after confirming `SP.SetCoinBalance` was called (via device log or debug output), THEN on cold relaunch `GetCoinBalance()` returns X+40. Pass condition: observed balance = X+40. Fail condition: observed balance = X. Evidence: `production/qa/evidence/ac-38-persistence-device.md` (must document: **build type: development build required** — production builds have logging disabled and SetCoinBalance call confirmation is impossible; device model; OS version; kill method; logging method used (Android: `adb logcat` connected, iOS: Xcode Console open); confirmation that SetCoinBalance log line appeared before kill; balance X before test; balance observed in HUD on cold relaunch; explicit PASS or FAIL verdict). *Implementation note: timestamp logging on `SetCoinBalance` is recommended for debugging tight-window kills but is not required to execute this AC — any method of confirming the call occurred before the kill is acceptable.* |
| AC-39 | BLOCKING | GIVEN `economy.coin_balance = 500` is loaded from SP on `OnSaveReady` and CE transitions to READY, WHEN the transition completes, THEN CE emits `coin_balance_changed(new_balance: 500, delta: 0, earn_source: EarnSource.Base)` exactly once, verified via event spy in unit test. This AC verifies CE's emission — independent of HUD. *File location: `tests/unit/coin-economy/`.* |
| AC-39b | BLOCKING (Integration — requires In-Game HUD) | GIVEN CE has transitioned to READY with `coin_balance = 500` and emitted the init `coin_balance_changed` event, WHEN the In-Game HUD is loaded before any level completion occurs, THEN the HUD coin display shows `500`. Evidence: screenshot in `production/qa/evidence/ac-39b-hud-init-balance.png` (must show: HUD coin display value, no level completions in session — state verifiable by level counter at 0). |
| AC-40 | BLOCKING | GIVEN CE is in READY state with `coin_balance = amount`, WHEN `SpendCoins(amount)` and a second `SpendCoins(amount)` are dispatched in the same Unity frame, THEN exactly one call returns `true` and one returns `false`; `coin_balance` reaches `0` exactly once; `coin_balance_changed` is emitted exactly once; no negative balance results. |
| AC-41 | ADVISORY | GIVEN the In-Game HUD subscribes to `coin_balance_changed`, WHEN CE emits the event due to a successful mutation, THEN the HUD coin counter triggers its 300ms pulse animation exactly once per event. Evidence: Play Mode screen recording in `production/qa/evidence/ac-41-coin-pulse.md`. |

### Idempotency Guard

| ID | Level | Criterion |
|---|---|---|
| AC-42 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80` and `last_credited_level_id = 42`, WHEN `AddCoins(40, level_id=42, earn_source=Base)` is called (same level_id as previous credit), THEN the return value is `false`; `GetCoinBalance()` remains `80`; `coin_balance_changed` is not emitted; no SP call is made; a duplicate-credit warning is logged. |
| AC-43 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80` and `last_credited_level_id = 42`, WHEN `AddCoins(40, level_id=43, earn_source=Base)` is called (new level_id), THEN the return value is `true`; `GetCoinBalance()` becomes `120`; `last_credited_level_id` is updated to `43`; `coin_balance_changed(new_balance: 120, delta: +40, earn_source: EarnSource.Base)` is emitted. |
| AC-44 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80` and `last_credited_level_id = 42`, WHEN `AddCoins(75, level_id=-1, earn_source=Base)` is called (EarnSource.Base with level_id=-1 — bypasses idempotency guard), THEN the return value is `true`; credit proceeds normally: `GetCoinBalance()` returns `155`; `last_credited_level_id` remains `42` (not updated when level_id=-1); CE logs advisory: `"CE: advisory — EarnSource.Base with level_id=-1 bypasses idempotency guard; confirm this is intentional."`; `coin_balance_changed(new_balance: 155, delta: +75, earn_source: EarnSource.Base)` is emitted. |
| AC-45 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80` and `last_credited_level_id = 42`, WHEN `AddCoins(40, level_id=42, earn_source=AdBonus)` is called (same level_id, but ad-bonus path), THEN the return value is `true`; credit proceeds normally (ad-bonus path is not guarded by `last_credited_level_id`); `GetCoinBalance()` becomes `120`; `last_credited_level_id` remains `42` (not updated by AdBonus calls); `coin_balance_changed(new_balance: 120, delta: +40, earn_source: EarnSource.AdBonus)` is emitted. |
| AC-45b | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80`, `last_credited_level_id = 42`, and `bonus_multiplier = 2.0`, WHEN `AddCoins(50, level_id=-1, earn_source=PityGrant)` is called, THEN the return value is `true`; `GetCoinBalance()` becomes `130` (50 added, not 100 — `bonus_multiplier` is NOT applied to `EarnSource.PityGrant`); `coin_balance_changed(new_balance: 130, delta: +50, earn_source: EarnSource.PityGrant)` is emitted; `last_credited_level_id` remains `42` (PityGrant calls do not advance the guard). A second identical call also returns `true`; `GetCoinBalance()` returns `180`; `last_credited_level_id` remains `42` after both calls. |
| AC-55 | BLOCKING (Integration — requires LP + SP with `has_completion_record()` — LP-03 resolved 2026-05-08) | GIVEN CE is in READY state AND level 5 was completed in a prior session (star record persisted in SP, `SP.has_completion_record(5) == true`) AND the session has since ended (cold relaunch, `last_credited_level_id` reset to -1), WHEN the player replays level 5 and LP's COMPLETION_FLOW processes `coin_reward_granted(20)` for level 5, THEN LP's EC-05 cross-session guard must block the forwarding: `CE.AddCoins` is NOT called; `GetCoinBalance()` is unchanged; no duplicate cross-session credit occurs. *BLOCKED until LP GDD documents EC-05 cross-session block (Cross-GDD LP-03 — resolved 2026-05-08 in LP GDD Pass 8 revision). File location: `tests/integration/coin-economy-lp/` — requires LP + SP with has_completion_record().* |

### Subscription Verification

| ID | Level | Criterion |
|---|---|---|
| AC-46 | BLOCKING | GIVEN CE has transitioned to READY (confirming `Awake()` subscriptions completed), WHEN LP fires `LevelCompleted(stars: 2, level_id: 5, move_count: 12, par_moves: 10)` via a test harness, THEN CE's `LevelCompleted` handler is invoked exactly once (verified via event spy or handler call counter registered before the test begins). This AC verifies that CE's subscription is actually wired — not merely that no exception propagated. *File location: `tests/unit/coin-economy/` — CE's subscription can be verified without LP integration by firing the event directly.* |

### Pity Grant Integration

| ID | Level | Criterion |
|---|---|---|
| AC-47 | BLOCKING (Unit) | GIVEN CE is in READY state with `coin_balance = 0` and `bonus_multiplier = 2.0`, WHEN `CE.AddCoins(50, level_id=-1, EarnSource.PityGrant)` is called directly, THEN the return value is `true`; `GetCoinBalance()` returns `50`; `coin_balance_changed(new_balance: 50, delta: +50, earn_source: EarnSource.PityGrant)` is emitted; `last_credited_level_id` is NOT updated; `bonus_multiplier` is NOT applied (pity grant always delivers exactly 50 regardless of active multiplier). A second identical call also returns `true`: `GetCoinBalance()` returns `100`. Confirms CE imposes no rate limit and the calling system owns suppression. *File location: `tests/unit/coin-economy/`* |
| AC-47b | BLOCKING (Integration — requires In-Game HUD with CE-13 counter implemented and GSM available) | GIVEN CE is in READY state with `coin_balance = 0` AND the In-Game HUD's pity counter is at `pity_grant_attempt_threshold - 1`, WHEN `GSM.level_complete(par_moves=10)` fires with `GSM.GetMoveCount()` returning a value producing `star_rating=0` via F-05 (e.g., `move_count=30` yields `star_rating=0`), THEN the HUD counter reaches threshold and calls `CE.AddCoins(50, level_id=-1, EarnSource.PityGrant)`; `GetCoinBalance()` returns `50`; `coin_balance_changed(new_balance: 50, delta: +50, earn_source: EarnSource.PityGrant)` is emitted. Verifies the full HUD counter → CE path, not just CE's internal behavior. *File location: `tests/integration/coin-economy-hud/`* |

### AddCoins Guards & Atomicity

| ID | Level | Criterion |
|---|---|---|
| AC-48 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80`, `last_credited_level_id = 10`, and SP configured with a test double that throws on `SetCoinBalance`, WHEN `AddCoins(40, level_id=11, earn_source=Base)` is called, THEN the return value is `false`; `GetCoinBalance()` returns `80` (working copy rolled back); `last_credited_level_id` remains `10` (guard not advanced); `coin_balance_changed` is not emitted; CE logs `"CE: SP.SetCoinBalance failed in AddCoins — balance rolled back, guard not advanced"`. The caller (LP) should detect `false` and may retry. *File location: `tests/unit/coin-economy/`* |
| AC-49 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80`, `last_credited_level_id = 10`, WHEN `AddCoins(0, level_id=11, earn_source=Base)` is called, THEN the return value is `false`; `GetCoinBalance()` remains `80`; `last_credited_level_id` remains `10` (guard NOT advanced for a 0-amount no-op — rationale: a 0-amount call is not a credit; guard treats it as if the call never occurred); no SP call; no event. A subsequent `AddCoins(20, level_id=11, earn_source=Base)` succeeds: return value is `true`; `GetCoinBalance()` returns `100`; `last_credited_level_id` updated to `11`. *File location: `tests/unit/coin-economy/`* |
| AC-50 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80` and `bonus_multiplier` has been injected as a negative value via CE's test seam `SetBonusMultiplierUnclamped(float)` (exposed via `[InternalsVisibleTo("CoinEconomy.Tests")]` — this method bypasses the setter clamp and is the only approved mechanism for this AC), WHEN `AddCoins(40, level_id=-1, earn_source=Base)` is called (producing a negative `coin_award` via F-CE-01), THEN the return value is `false`; `GetCoinBalance()` remains `80`; no mutation; no SP call; no event; CE logs the internal error. *Note: this defensive guard is dead code in production — `bonus_multiplier` setter always clamps at [1.0, 2.0] and `amount` guard prevents negative inputs. This AC tests the guard exists; it requires test-injection-only access to reach the path. File location: `tests/unit/coin-economy/`* |
| AC-51 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 0`, WHEN `AddCoins(0, level_id=-1, earn_source=PityGrant)` is called (0-amount EarnSource.PityGrant — e.g., if `hint_cost` tuning knob is misconfigured to 0), THEN the return value is `false`; CE performs no mutation; no SP call; no `coin_balance_changed` event; CE logs a caller bug warning: `"CE: caller bug — EarnSource.PityGrant delivered 0 coins; pity_grant_amount or hint_cost tuning may be misconfigured"`. Verifies that 0-amount PityGrant is not silent. *File location: `tests/unit/coin-economy/`* |
| AC-52 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80`, WHEN `AddCoins(50, level_id=5, earn_source=PityGrant)` is called (PityGrant with a real level_id — caller bug), THEN the return value is `false`; `GetCoinBalance()` remains `80`; no mutation; no SP call; `coin_balance_changed` is not emitted; CE logs `"CE: caller bug — EarnSource.PityGrant must use level_id=-1; received level_id=5."`; `last_credited_level_id` is NOT updated. Verifies the early-return behavior: PityGrant caller bugs are rejected, not warned-and-continued. *File location: `tests/unit/coin-economy/`* |
| AC-53 | BLOCKING | GIVEN AC-48 scenario has just executed (SP threw, `coin_balance` rolled back to `80`, `last_credited_level_id = 10`, return was `false`) AND SP is now configured to succeed, WHEN `AddCoins(40, level_id=11, earn_source=Base)` is called again (retry after rollback), THEN the return value is `true`; `GetCoinBalance()` returns `120` (80 + 40); `last_credited_level_id` is updated to `11`; `coin_balance_changed(new_balance: 120, delta: +40, earn_source: EarnSource.Base)` is emitted. Verifies that the guard not-advancing on SP failure means the retry is treated as a fresh first-time credit. *File location: `tests/unit/coin-economy/`* |
| AC-54 | BLOCKING | GIVEN CE is in READY state with `coin_balance = 80`, WHEN `AddCoins(50, level_id=-1, earn_source=EarnSource.Spend)` is called (EarnSource.Spend passed to AddCoins — caller bug), THEN the return value is `false`; `GetCoinBalance()` remains `80`; no mutation; no SP call (verified via call-recording spy); `coin_balance_changed` is not emitted; CE logs `"CE: caller bug — EarnSource.Spend is not a valid AddCoins earn_source; use SpendCoins for deductions."`. Verifies that the EarnSource.Spend guard in CE-07 rejects the call before any mutation occurs, before the coin_award computation, and before any idempotency guard advance. *File location: `tests/unit/coin-economy/`* |

**Summary: 64 BLOCKING / 1 ADVISORY = 65 total** *(Pass 8 revisions: EarnSource.Spend guard added to CE-07 pseudocode; AC-54 added (EarnSource.Spend rejection); AC-55 added (LP-03 cross-session integration, BLOCKED pending LP-03 — now resolved); AC-45b second-call guard-state assertion added; AC-38 dev-build requirement added; cross-cutting test infrastructure note added for negative-SP-call ACs; OQ-09 hint_cost resolved; OQ-12 CE-07 guard noted; Known Limitations paragraph added to Player Fantasy; SP persistent-failure edge case added; OQ-07 deadline and acceptance spec added; spend-denial UX contract added to CE-05; deduction emotional register contract added to HUD Interactions; HUD-02 resolved; LP-03 resolved via LP GDD EC-05 cross-session block. Pass 7 revisions: Player Fantasy reframed to remove "never" — honest "almost always" framing; Player Fantasy reframed to remove "never" — honest "almost always" framing; AddCoins return type changed to bool (false on SP failure, caller bug, duplicate, 0-amount); coin_balance_changed event signature extended with earn_source: EarnSource; EarnSource enum extended with Spend value; CE-06 getter now repairs working copy in memory on negative detection; PityGrant caller-bug path (level_id != -1) is now an early-return false instead of warn-and-continue; LOADING backgrounding behavior explicitly documented as accepted; 1★ Workshop timeline computed and added to Tuning Knobs as majority-segment design reference; Beta vs. Full Player Fantasy sub-states added; AC-35c split into AC-35c/AC-35d/AC-35e with Play Mode labels; AC-37 removed (merged into AC-06); AC-47 split into unit AC-47 and integration AC-47b; AC-50 test seam mechanism specified (InternalsVisibleTo + SetBonusMultiplierUnclamped); added AC-52 (PityGrant caller-bug early-return), AC-53 (retry-after-SP-rollback); AC-04b emission assertion added; AC-06 SP write negative assertion added; earn_source added to all event payload assertions; advisory log assertion added to AC-44; OQ-11 (milestone gap — deferred to Shop UI GDD) and OQ-12 (EarnSource.Spend) added)*

## Open Questions

**OQ-01 — CROSS-GDD: LP GDD update required (LP-01, LP-02)**
LP GDD Sections C (Non-Formula Rules) and AC-21, AC-22, AC-35 reference LP-owned coin mutation logic. These must be updated to reflect that LP delegates to CE and holds no coin state (LP-01). Additionally, LP's `AddCoins` call sites must be updated to pass `level_id` and `EarnSource` (LP-02). *Priority: before implementation sprint for either LP or CE.* Assign to: Lead Programmer + LP GDD author.

**OQ-02 — CROSS-GDD: SP interface update required (SP-01)**
Save & Persistence GDD Interactions table lists Level Progression as provisional owner of `economy.coin_balance`. When CE is implemented, this entry must be updated to Coin Economy, and SP must expose `SetCoinBalance(value: int)` with the semantics that calling it atomically marks SP's internal dirty flag. The provisional `AddCoins` method on SP should be retired. *Priority: before CE implementation sprint.* Assign to: Lead Programmer + SP GDD author.

**OQ-03 — CROSS-GDD: SP `OnSaveReady` event contract (SP-02)**
SP GDD C.5 mentions an "IsReady awaitable" and "OnSaveReady event or poll" but does not define a named event. CE's initialization sequence requires: (a) a confirmed callback name or event that CE can subscribe to, and (b) a synchronous `SaveSystem.IsReady` bool that CE can check immediately after subscribing to handle the race condition where SP fires before CE's `Awake()`. CE uses a subscribe-then-synchronous-check pattern — both the event and the bool must be confirmed in the SP GDD. *Priority: before CE implementation sprint.*

**OQ-04 — `ad_offer_show_rate` operating intent**
The economy analysis recommends `ad_offer_show_rate = 0.5–0.7` for normal play sessions to avoid ad fatigue and prevent Workshop from being reachable too quickly. This should be documented as a constraint in the Level Complete UI GDD's tuning knobs section. *Priority: before Beta build; assign to Level Complete UI GDD author.*

**OQ-05 — Registry entries needed after downstream GDDs are authored**
The following should be registered in `design/registry/entities.yaml` when the named systems are designed:
- `coin_balance_changed` event (source: this GDD) — add when In-Game HUD GDD or Shop UI GDD is authored
- `bonus_multiplier` constant (source: this GDD) — add when Daily Challenge System GDD is authored
- `hint_cost` constant (value: 50) — add when Hint System GDD is authored
- `last_credited_level_id` field (source: this GDD, session-scoped, not persisted) — add when CE implementation sprint begins
- `EarnSource` enum (source: this GDD) — add when CE implementation sprint begins

**OQ-06 — `coin` registry item ownership transfer**
The entity registry `items.coin` currently lists `source: design/gdd/level-complete-ui.md`. The `source` field should be updated to `design/gdd/coin-economy.md` and `design/gdd/coin-economy.md` added to `referenced_by`.

**OQ-07 — First-spend experience assigned to Shop UI GDD**
The 150-coin starter grant ensures a new player has coins on day 1. The Shop UI GDD is the assigned owner of: (a) the onboarding beat directing new players to spend starter coins on an Accent cosmetic, and (b) the goal-visibility display ("X coins until [next cosmetic]") that surfaces the earn target after each level completion. These are BLOCKING requirements on Shop UI GDD per the bidirectional consistency section above. CE MUST NOT enter its Beta implementation sprint until Shop UI GDD accepts these obligations.

*Deadline: Shop UI GDD must be authored and must accept these obligations **before Beta milestone kickoff**. Minimal acceptance criteria for Shop UI GDD: (a) a documented first-spend onboarding beat with a BLOCKING AC, and (b) a goal-visibility display with a BLOCKING AC. Without both, CE's Player Fantasy ("the balance that inches toward the next skin unlock") is undeliverable. Mark CE-implementation status as BLOCKED until this OQ is resolved. Assign to: Producer (track deadline) + Shop UI GDD author (accept obligations).* *Priority: before Beta build.*

**OQ-08 — Pity hint grant system owner (CE-13)** *(Resolved 2026-04-27)*
Owner assigned: **In-Game HUD GDD**. See Cross-GDD HUD-01 in CE-13 for the full implementation obligation. CE MUST NOT be marked Approved until HUD GDD accepts HUD-01 with a Detailed Design rule and BLOCKING AC.

**OQ-10 — Level Complete UI GDD must update coin_reward_per_star[1]** *(Added 2026-05-07 — BLOCKING against Level Complete UI implementation sprint)*
Level Complete UI GDD owns `coin_reward_per_star` tuning knobs. The 1★ earn rate was retuned from 10 → 15 coins as part of the Cluster A resolution (Pass 6). Level Complete UI GDD must update `coin_reward_per_star = [0, 15, 20, 40]` before either CE or Level Complete UI enters its implementation sprint. Economy projections in both GDDs must be consistent with this value. *Assign to: Level Complete UI GDD author + Lead Programmer.*

**OQ-09 — hint_cost ownership conflict with In-Game HUD GDD** *(Resolved 2026-05-08 — HUD GDD updated in Pass 8 design review)*
CE GDD is the authoritative owner of `hint_cost`. The canonical value is **50 coins** (safe range [25, 100]). In-Game HUD GDD Tuning Knobs have been updated to remove the `hint_cost = 10` temp-owner entry and replace it with a reference to CE GDD's canonical value (50). HUD GDD AC-30 updated to use `EarnSource.PityGrant` (previously erroneously `EarnSource.Base`). *[Cross-GDD HUD-02 — Resolved 2026-05-08.]*

**OQ-11 — Milestone gap between Accent (75 coins) and Finish skin (300 coins)** *(Added 2026-05-08 — known design gap)*
After spending the 150-coin starter grant on Accent cosmetics, a 1★ no-ads player faces a 7-session gap to the Finish skin with no intermediate cosmetic milestone. This creates a fixed-interval structure across a long timeline with no variable-ratio reinforcement in between. The gap is documented as a known design risk. **Owner: Shop UI GDD.** When the Shop UI GDD is authored, it must either: (a) introduce a mid-tier cosmetic priced ~150–175 coins to close the gap, or (b) explicitly accept the gap and describe the goal-visibility display as the mitigation mechanism. CE cannot close this gap unilaterally — it requires a new spend context that must be coordinated with Shop UI and Skin System GDDs. *Priority: before Shop UI GDD authorship sprint.*

**OQ-12 — EarnSource.Spend added to enum** *(Added 2026-05-08; CE-07 guard added 2026-05-08)*
`EarnSource` enum now includes `{Base, AdBonus, PityGrant, Spend}`. The `Spend` value is emitted by `SpendCoins` as the `earn_source` field in `coin_balance_changed`; it is never a valid `AddCoins` input. CE-07 AddCoins pseudocode now includes an explicit guard rejecting `EarnSource.Spend` at the top of the EarnSource validation block (returns false + caller bug log). AC-54 tests this rejection. All downstream systems (HUD, Shop UI) must handle `EarnSource.Spend` in their `coin_balance_changed` subscribers — it indicates a deduction, not a credit. When future earn paths are added (Daily Challenge, achievement rewards, etc.), new `EarnSource` values must be added to this enum and documented here before implementation. The enum is CE's canonical list of all coin-balance-change sources.
