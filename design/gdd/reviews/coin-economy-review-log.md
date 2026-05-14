# Review Log: Coin Economy

## Review — 2026-05-08 (Pass 9, lean) — Verdict: APPROVED
Scope signal: L
Specialists: lean mode — no specialist agents
Blocking items: 0 | Recommended: 3 (advisory cleanup only)
Summary: All 5 Pass 8 blockers confirmed closed. EarnSource.Spend guard verified in CE-07. coin_reward_per_star[1]=15 consistent across CE GDD, LC UI GDD, and entities.yaml. HUD GDD hint_cost=50, AC-30 PityGrant, AC-35 pity notification BLOCKING AC confirmed present. LP GDD EC-05 cross-session block confirmed. SP call assertion observability note and AC-38 dev build spec confirmed. Two minor corrections applied during lean pass: AC count corrected 65→64 BLOCKING; AC-55 stale "BLOCKED pending LP-03" label updated to reflect LP-03 resolved. Systems index updated: Coin Economy → Approved.
Prior verdict resolved: Yes — 5 blockers from 2026-05-08 (Pass 8) all closed

---

## Review — 2026-05-08 (Pass 8) — Verdict: NEEDS REVISION (all blockers resolved in-session)
Scope signal: L
Specialists: economy-designer, game-designer, systems-designer, qa-lead, creative-director (senior synthesis)
Blocking items: 5 | Recommended: 5
Summary: CE-internal design confirmed sound by systems-designer (formulas, pseudocode, state machine). Five blocking items resolved: (1) EarnSource.Spend guard added to CE-07 pseudocode + AC-54; (2) Three-way coin_reward_per_star[1] conflict resolved — LC UI GDD and entities.yaml updated to 15; (3) HUD-02 hint_cost conflict resolved — HUD Tuning Knobs updated to canonical 50, AC-30 fixed to EarnSource.PityGrant, AC-35 pity notification BLOCKING AC added; (4) LP-03 cross-session gate resolved — LP GDD EC-05 expanded with explicit cross-session block, AC-55 CI signal added; (5) Negative-SP-call AC observability — cross-cutting test infrastructure note added, AC-38 dev-build spec added. Five recommended revisions applied: Known Limitations section in Player Fantasy; OQ-07 deadline and minimal acceptance spec; spend-denial UX contract; deduction animation emotional register; SP persistent-failure degraded state documented. AC count: 62+1 → 65+1 = 66 total. Next step: lean re-review in clean session to confirm all blockers closed.
Prior verdict resolved: Yes — 14 blockers from 2026-05-08 (Pass 7) confirmed resolved

---

## Review — 2026-05-08 (7th pass) — Verdict: MAJOR REVISION NEEDED (14 blockers all resolved in-session)
Scope signal: L
Specialists: economy-designer, game-designer, systems-designer, qa-lead, creative-director (senior synthesis)
Blocking items: 14 | Recommended: ~15 (most applied in-session)
Summary: Creative director escalated from NEEDS REVISION to MAJOR REVISION NEEDED on the basis of a pillar-level contradiction that had survived 6 passes: the Player Fantasy "never short of progression" absolute was mathematically undeliverable for the 1★ hint-spending majority segment. User decision: honor the math — reframe to "almost always able to keep moving." All four specialists independently converged on two additional structural gaps: (1) coin_balance_changed carrying no EarnSource field, preventing HUD from distinguishing pity grants from normal earns; (2) AddCoins being void with no SP-failure return signal, causing permanent silent coin loss on write failures. Both resolved: EarnSource added to event payload (enum extended with Spend value); AddCoins now returns bool. PityGrant caller-bug path changed from warn-and-continue to early-return false. CE-06 getter now repairs negative working copy in memory. LOADING backgrounding behavior documented as accepted. AC set: 5 new ACs (35d, 35e, 47b, 52, 53), AC-37 removed, AC-35c and AC-47 split, AC-50 test seam specified. Milestone gap (75→300 coin jump) deferred to Shop UI GDD as OQ-11. Re-review in clean session required.
Prior verdict resolved: Yes — 2 blockers from 2026-04-28 (5th pass) confirmed resolved

---

## Review — 2026-04-28 (5th pass) — Verdict: NEEDS REVISION (2 blockers resolved in-session → pending clean re-review)
Scope signal: L
Specialists: lean mode — no specialist agents
Blocking items: 2 | Recommended: 3
Summary: Both prior blockers confirmed resolved (hint_cost, CE-13 event name in body). Two new blockers surfaced: (1) OQ-07 still read "CE is NOT approved until Shop UI GDD accepts these obligations" contradicting the Dependencies section and the 2026-04-28 stated intent to change to a sprint-gate — fixed by aligning OQ-07 to match Dependencies; (2) AC-47 referenced stale event name `GSM.LevelAttemptCompleted` after CE-13 was updated to `GSM.level_complete` — fixed in AC-47 GIVEN clause. Three recommended revisions noted (section header name, AC numbering disorder, CE-13 move_count clarification) — not applied in-session. Both blockers are targeted text fixes; no design decisions were required. Re-review in clean session.
Prior verdict resolved: Yes — 2 blockers from 2026-04-28 (4th pass) confirmed resolved

---

## Review — 2026-04-28 — Verdict: NEEDS REVISION (2 blockers resolved in-session → pending clean re-review)
Scope signal: L
Specialists: lean mode — no specialist agents
Blocking items: 2 | Recommended: 2 (all resolved in-session)
Summary: All 8 prior blockers confirmed resolved. Two new cross-GDD contract gaps surfaced from the HUD GDD being approved in the same session: (1) hint_cost value conflict — CE (50) vs HUD GDD's just-approved temp value (10), producing an active economy bug in pity grant suppression; CE's 50 declared canonical, HUD-02 added as BLOCKING against HUD implementation sprint. (2) CE-13 event contract mismatch — CE said HUD subscribes to `GSM.LevelAttemptCompleted` but HUD GDD uses `GSM.level_complete` + F-05; CE-13 updated to match HUD GDD; GSM-01 revised to "no new event required." Two recommended revisions applied: CE-13 HUD-01 note updated to Resolved; Shop UI blocking gate changed from design-approval to sprint-gate. Re-review in clean session before CE can be marked Approved.
Prior verdict resolved: Yes — 8 blockers from 2026-04-27 confirmed resolved

---

## Review — 2026-04-27 — Verdict: NEEDS REVISION (resolved in-session → pending clean re-review)

Scope signal: L
Specialists: economy-designer, game-designer, systems-designer, qa-lead, creative-director (senior synthesis)
Blocking items: 8 | Recommended: 9
Prior verdict resolved: Yes — prior 10 blockers confirmed resolved; 8 new blockers found

Summary: Third clean review pass. Technical implementation contract from prior sessions remains solid. New blockers fell into three categories: (1) specification contradictions — CE-04 "two callers only" not updated to reflect CE-13's third caller, CE-12 normative text claiming LP protection while LP-03 called it unconfirmed; (2) critical design gap — 0-star attempt counter had no viable signal source because LP AC-34 discards 0-star LevelCompleted events before HUD sees them, breaking the pity trigger's event chain; (3) implementation contract gaps — CE-13 pity grant silently doubled by bonus_multiplier (fixed by adding EarnSource.PityGrant), CE-07 missing negative coin_award guard, Interactions table missing subscriber contract for delta semantics, hint_cost value conflict between CE (50) and HUD GDD (10). All 8 blockers resolved in-session: EarnSource.PityGrant added; CE-13 updated with GSM.LevelAttemptCompleted pre-filter event + Cross-GDD GSM-01; HUD GDD assigned as CE-13 owner (OQ-08 resolved); CE-12 rewritten as hard implementation gate pending LP-03; OQ-09 added for hint_cost conflict; defensive coin_award guard added to CE-07; Interactions table subscriber contract added. AC set updated: 4 new ACs (AC-45b, AC-48, AC-49, AC-50), 6 existing ACs revised (AC-24, AC-28, AC-34/35/35b routing, AC-38, AC-47). Recommend /design-review in a new clean session (context high).

---

## Review — 2026-04-26 — Verdict: NEEDS REVISION (resolved in-session → pending clean re-review)

Scope signal: L
Specialists: economy-designer, game-designer, systems-designer, qa-lead, creative-director (senior synthesis)
Blocking items: 10 | Recommended: 14
Prior verdict resolved: Partial — prior 19 blockers confirmed resolved; 10 new blockers found across design and AC quality

Summary: The technical implementation contract from the prior session (overflow-safe arithmetic, non-blocking coroutine, EarnSource idempotency guard) is fully sound. New issues emerged in three categories: (1) design-level — Player Fantasy structurally front-loaded for hint-spending casual players with no stuck-player recovery path, ad earn disparity (100% at 1★) in tension with "Cosmetic Not Coercive" pillar, and no GDD owning first-spend moment or goal-visibility display; (2) implementation contract — CE-12 cross-session defense relied on unquoted LP EC-05 contract; (3) AC quality — AC-23 missing, AC-38/39 evidence gaps, AC-06 ambiguous GIVEN, AC-10/16 misrouted to unit suite. All 10 blockers resolved in-session: CE-13 pity grant rule (5-failure threshold, HUD/Tutorial implementer), Player Fantasy and Tuning Knobs reframed for ad-as-accelerator, Cross-GDD LP-03 added, Shop UI GDD assigned as hard dep for first-spend and goal-visibility, AC-23 added, AC-38 rewritten with binary pass condition, AC-39 split into unit (AC-39) + integration (AC-39b), AC-06 GIVEN anchored to specific value, AC-10/AC-16 given integration suite routing notes. Recommend /design-review in a clean session for final sign-off.

---

## Review — 2026-04-25 — Verdict: MAJOR REVISION NEEDED

Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, creative-director (senior); economy-designer timed out (synthesized directly)
Blocking items: 19 | Recommended: 10
Prior verdict resolved: No — first review

Summary: The implementation mechanics were competent (clean state machine, correct formulas structurally), but the GDD failed on three fronts simultaneously. First, the Player Fantasy ("never short of resources") was structurally undeliverable for the game's own stated target audience — a 1★ casual player needed 5 completions for a single hint and 10 sessions for the cheapest cosmetic, contradicting "optional leverage." Second, the implementation contract contained a mobile ship-blocker (2-second synchronous main thread stall triggering ANR/iOS watchdog) and a silent data-corruption bug (C# integer overflow in F-CE-03 before the min() clamp, producing negative balances). Third, the AC set had 11 blocking QA issues including mislabeled cross-system tests, missing AddCoins LOADING timeout coverage, ambiguous wall-vs-game-time semantics, and a duplicate AC pair. All 19 blocking items were resolved in-session: 150-coin starter grant (CE-11), Accent cosmetic tier at 75 coins (CE-09), CE-level idempotency guard via EarnSource enum and last_credited_level_id (CE-12), overflow-safe long arithmetic in F-CE-03, non-blocking coroutine stall spec, OQ-03 race condition analysis, and 9 new/revised ACs. GDD updated to 48 BLOCKING / 1 ADVISORY. Recommended next step: /design-review in a clean session.
