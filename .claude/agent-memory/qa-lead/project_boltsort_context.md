---
name: BoltSort project context
description: Core project facts — genre, platform, engine, and QA review history for the Sort Mechanic GDD
type: project
---

BoltSort is a casual F2P mobile sort puzzle for iOS and Android, built in Unity 6.3 LTS (not Godot — memory previously incorrect).

The Sort Mechanic GDD has gone through eight adversarial QA reviews. Reviews #1 and #2 were on 2026-04-30; review #3 was on 2026-05-05; review #4 was on 2026-05-07; review #5 (Pass 5 re-review) was on 2026-05-07; review #6 was on 2026-05-09; review #7 was on 2026-05-09; review #8 (this review, Pass 8) was on 2026-05-09.

## Resolved since Pass 6

- AC-05 GIVEN: Rewritten to N-1 state (stack A=[1,1], stack B=[2,2,2], player holds color-1 bolt). RESOLVED.
- AC-13 body heading: Now correctly says `*(Integration test)*`. RESOLVED.

## Open BLOCKERS (as of Pass 8, 2026-05-09)

**Carried from Pass 5/6/7 (not resolved):**
- AC-06: Missing concrete `color_count` in GIVEN — tester cannot build fixture without it.
- AC-07: No undo-spy mechanism specified for "no undo entry written" assertion.
- AC-29a (Finding 6, Pass 6): `move_executing_exited` non-emission on watchdog path has no AC.
- AC-29b (Finding 5, Pass 6): Buffered tap on now-empty post-commit stack (S-02 treatment) has no AC.
- AC-05: Mock vs real GSM ambiguity for `move_count` sourcing in THEN. Unit test should use mock GSM returning fixed value, not real GSM with 2 prior committed moves.
- AC-27: Logger assertion missing structured payload `{reason: CORRUPTED_BOARD_STATE}` — inconsistent with AC-18b which does specify it.
- AC-22: No fixture reference for deadlock scenario — tester cannot build reproducible board. Must reference `tests/helpers/sort-mechanic-fixtures`.
- AC-16: No board fixture or layout spec for multi-rejection stability test — not reproducible across testers.
- AC-24: WHEN clause hides the `animation_complete` trigger — risk of state-forcing in test instead of exercising real exit path.
- AC-19: THEN says Sort Mechanic "receives `level_load_failed`" — but Sort Mechanic is the emitter, not receiver. Signal chain is architecturally wrong; must clarify GSM→Sort Mechanic signal path.
- AC-26: Phantom-color validation ownership is split between Sort Mechanic Formulas (initialization assertion #3) and GSM — contradictory. Must pick one owner.
- AC-24 (move_executing_exited): Non-emission on WIN path not explicitly named in AC-24's event-bus spy; "any other Sort Mechanic event" is insufficient — must name `move_executing_exited` explicitly.

**New from Pass 8:**
- Finding 1: `rejection_animation_complete` event is referenced in AC-30 but is undefined in the GDD — not in events table, not in Animation System dependency section, no signature documented. AC-30 is untestable until this signal is defined and the Animation System GDD is updated to confirm it emits it.
- Finding 2: No AC for INVALID_MOVE buffered tap = source stack or empty space tap → CANCELLATION fires on INVALID_MOVE exit. Missing BLOCKING unit test (proposed AC-30c).
- Finding 3: No AC for MOVE_EXECUTING buffered tap → illegal destination → INVALID_MOVE path. BOLT_SELECTED intermediate entered, then INVALID_MOVE. No AC at any tier.
- Finding 4: AC-31 says "no `deadlock_detected()` is emitted" but does not specify spy-based verification method. Non-emission assertion is only testable with an explicit event-bus spy — AC must state this.
- Finding 5: `tests/helpers/sort-mechanic-fixtures` is required by AC-10, AC-22, AC-25 but its creation is only "a named deliverable in the implementation story" — no AC mandates existence as a gate. Three BLOCKING ACs have an unguarded precondition.

## Open MINORS/ADVISORY (as of Pass 8)

**Carried from Pass 5/6/7:**
- AC-02: Missing MOVE_EXECUTING transition assertion.
- AC-04: Missing BOLT_SELECTED return-state assertion.
- AC-15a: Two test cases combined in one AC — recommend splitting.
- AC-18b: Logger + event emission still combined in one AC; only the input-blocking split was done (AC-18c), not the logger/emit split.
- AC-12: No AC for `android:enableOnBackInvokedCallback` opt-in path.
- AC-21: Private field access method for synchrony assertion not specified.
- AC-08a/b/c: No setup path specified for reaching MOVE_EXECUTING in unit test — harness approach unspecified.
- AC-25: "Before first player-facing frame" is not a code-level testable assertion — replace with "before Sort Mechanic processes any player tap event."
- Auto-place feature (Finding 4, Pass 6): No auto-place rule in GDD Detailed Rules. Confirm with designer whether dropped or still intended.
- AC-28: Ordering guarantee (cancel-before-serialize) not verifiable by bolt-count check alone — needs mock GSM with call-order tracking.

**New from Pass 8:**
- Finding 6 (AC-12): Wrong test tier — `Keyboard.current.escapeKey.wasPressedThisFrame` is unreachable in Unity headless. Requires `InputTestFixture` or reclassification to on-device tier.
- Finding 7: AC numbering inconsistency — AC-30 should be AC-30a (no "a" suffix but a matching AC-30b exists); AC-29 uses a/b symmetrically but AC-30 does not. Creates traceability ambiguity, especially if AC-30c is added.
- Finding 9 (AC-03): Missing BOLT_SELECTED return-state assertion in THEN clause (same gap as AC-04 carry-forward, now flagged on AC-03 too).
- Finding 10 (AC-03): INVALID_MOVE intermediate state not verified — no mock-withhold approach specified; a faulty impl skipping INVALID_MOVE entirely would pass.

## Current counts (as of Pass 8, 2026-05-09)

- **Open BLOCKERS:** 18 (13 carried + 5 new)
- **Open ADVISORY:** 14 (10 carried + 4 new)

**Why:** Understanding the review history prevents re-flagging resolved items and calibrates the standard for blocking AC quality.

**How to apply:** When asked about Sort Mechanic ACs, reference eight-review history. Do not re-flag items closed in reviews #1–#6 (AC-05 GIVEN rewrite and AC-13 heading are now RESOLVED). The items above are all open as of review #8.
