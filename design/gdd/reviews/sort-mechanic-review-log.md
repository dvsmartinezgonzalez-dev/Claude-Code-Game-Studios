# Review Log: Sort Mechanic

## Review — 2026-05-10 (pass 8) — Verdict: APPROVED
Scope signal: L
Specialists: lean mode (no specialist agents)
Blocking items: 0 | Recommended: 2
Summary: Lean re-validation confirming all four pass 7 resolutions correctly integrated: held-bolt visual committed as canonical (line 36 "committed spec"), sequence_id typed int64 across all event signatures with overflow rationale documented, pre-won board auto-advances via puzzle_solved(move_count:0) on level_loaded (EC-04, AC-31), INVALID_MOVE buffers one tap with AC-30/AC-30b covering both paths. No blocking issues found. Two advisory items: (1) ADR-0001 may not yet include Sort Mechanic/GSM OnApplicationPause SEO ordering required by EC-14 — verify before implementation story is written; (2) Animation System GDD BM-06 correction (no completion signal on cancel path) flagged in pass 7 remains a cross-doc obligation. All 8 required sections present. All dependency GDD files confirmed on disk. GDD promoted to Approved.
Prior verdict resolved: Yes (pass 7 — 14 blockers all confirmed resolved)

## Review — 2026-05-09 (pass 7) — Verdict: NEEDS REVISION — all blockers resolved in-session
Scope signal: L (document scope); M (remaining revision work)
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director
Blocking items: 14 | Recommended: 8
Summary: Full specialist review (pass 7) collapsed 47 raw findings to 14 unique blockers after deduplication. Three cross-agent convergence blockers: (1) BM-06 / CANCELLATION contract contradiction — Animation System GDD says emit completion signal on cancel path; Sort Mechanic GDD says no handshake; Sort Mechanic GDD declared authoritative, BM-06 must be corrected; (2) sequence_id int32 overflow → permanent MOVE_EXECUTING softlock on wrap to negative — changed to int64 across all signatures; (3) initialization assertion 3 ownership ambiguity — GSM L-03 declared primary; Sort Mechanic assertion is defensive backstop; HUD subscribes to Sort Mechanic's `level_load_failed`. Additional blockers resolved: held-bolt visual committed (removed hypothesis framing); `all_same_color` defined; win check runtime array length assertion added; deadlock complexity corrected O(121)→O(N(N-1))=O(110); move legality formula constraint note added; OnApplicationPause SEO ordering requirement added to EC-14; watchdog frame-gap discard rule added; AC-12 scoped Android-only with required null guard; 9 AC rewrites (AC-06/07/16/19/22/24/26/27/28); 7 new ACs added (AC-29a, AC-29b, AC-30, AC-30b, AC-31, AC-05a/05b split). Design decisions: EC-04 now auto-wins at level_loaded on pre-won board (pre-won check before deadlock check; puzzle_solved(move_count:0)); INVALID_MOVE now buffers one tap (matching MOVE_EXECUTING model); EC-05 rewritten to reference EC-14; Open Question 3 marked Resolved.
Prior verdict resolved: Yes (pass 6 — 5 blockers all confirmed resolved)

## Review — 2026-05-09 (pass 6) — Verdict: NEEDS REVISION — all blockers resolved in-session
Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, ux-designer, unity-specialist, creative-director
Blocking items: 5 | Recommended: 9
Summary: Full specialist review (pass 6) found five blockers — all resolved in-session. B1: GDD Open Questions resolution for Android back gesture referenced `Application.wantsToQuit`/`BackGestureHandler` which contradicted accepted ADR-0007; tap definition `TouchPhase.Began` + drag guard was dead spec against ADR-0007; reconciled to `Keyboard.current.escapeKey.wasPressedThisFrame` + no drag guard. B2: Auto-place fallback had no state machine entry, no ACs, no UX spec — cut from this pass. B3: Backgrounding during BOLT_SELECTED lost the held bolt permanently due to S-01 immediate removal + GSM non-serialization of held state; EC-14 + AC-28 added. B4: AC-13 body still labeled BLOCKING after 3-cycle reclassification — fixed. B5: Win condition allowed phantom color_ids to pass initialization checks, creating structurally unreachable win states; third initialization assertion added to win formula + AC-26 expanded + AC-27 added. Advisories applied: Animation System interrupt contract for cancel+re-lift race; MOVE_EXECUTING exit sequence added to state table; INVALID_MOVE timing invariant; Android 48dp tap target; sequence_id session-global policy; AC-21 Unity-precise synchronous language; Hint System non-binding dependency note; AC-18b split into AC-18b/18c; AC-27 (temp_slot_depth assertion). Held-bolt-at-source visual labeled as unvalidated design hypothesis to validate in first playtest.
Prior verdict resolved: Yes (pass 5 — 7 blockers all confirmed resolved)

## Review — 2026-05-07 (pass 5) — Verdict: NEEDS REVISION — all blockers resolved in-session
Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, ux-designer, unity-specialist, creative-director
Blocking items: 7 | Recommended: 6
Summary: Full specialist review (pass 5) surfaced four AC specification gaps (AC-05 GIVEN described pre-won board — unexercisable; missing AC for buffered-tap-discard on WIN exit; missing AC for level_loaded deadlock path; missing AC for per-color bolt imbalance creating silent unsolvable boards), two missing assertions (temp_slot_depth ≤ stack_depth init check; AC-18b logger spec untestable), and one unacknowledged design hypothesis (two-tap vs. execution-mode Player Fantasy — auto-place fallback documented). Unity API mismatches (TouchPhase.Began, Key.AndroidBack, Application.wantsToQuit) deferred to implementation story per creative-director ruling. All 7 blockers resolved in-session. AC-13 reclassified from BLOCKING to integration tier.
Prior verdict resolved: Yes (pass 4 10 blockers all confirmed resolved)

## Review — 2026-05-07 (pass 4) — Verdict: NEEDS REVISION — all blockers resolved in-session
Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, ux-designer, unity-specialist, creative-director
Blocking items: 10 | Recommended: 9
Summary: Full-mode specialist review surfaced two critical spec defects missed by prior lean passes: (1) EC-08 watchdog path bypassed win check — a puzzle won during animation crash could never transition to WIN, producing a softlock on a solved board; (2) invariant trust gap — a malformed level with a missing bolt created an unsolvable puzzle that never triggered deadlock_detected(), trapping the player indefinitely. Both fixed in-session. Five ghost ACs (AC-19–23) referenced in the preamble had no body entries; all five authored. AC-08 split into three independently testable assertions (08a/b/c). AC-18 split into 18a/18b. AC-12 BLOCKING label added. Tap semantics defined (TouchPhase.Began + drag threshold guard). Android back gesture resolved (BackGestureHandler MonoBehaviour, both API paths). Init assertion failure disposition specified (soft block + level_load_failed event). Player Fantasy rewritten to match mechanics: "technician/pattern-solver" exploration model replacing "calibration engineer/committed direction" framing that contradicted S-04 probing and unlimited undo behavior. INVALID_MOVE → BOLT_SELECTED highlight re-activation specified.
Prior verdict resolved: Yes (prior APPROVED was on lean-mode pass; full specialist review found new issues in the 2026-05-05 revision)

## Review — 2026-05-01 (pass 3) — Verdict: APPROVED
Scope signal: L
Specialists: lean mode (no specialist agents)
Blocking items: 1 | Recommended: 5
Summary: Single blocker: `move_executing_exited` was being emitted on WIN path, which would cause GSM to process a deferred undo before `puzzle_solved` arrived — silently corrupting board state on win. Fixed to IDLE-path-only; WIN path uses `puzzle_solved` + GSM AC-GSM-15 clear behavior instead. Recommended revisions applied: stale PROVISIONAL headers updated, dependency note updated, Visual/Audio animation reference corrected, AC-15 split into unit (AC-15a) and integration (AC-15b) tiers, status header updated to Approved. GDD is implementation-ready.
Prior verdict resolved: Yes (5 blockers from 2026-05-01 pass 2 all confirmed resolved)

## Review — 2026-05-01 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: lean mode (no specialist agents)
Blocking items: 5 | Recommended: 2
Summary: All 5 blockers were cross-GDD integration artifacts introduced by the Animation System and GSM GDDs being authored after the first review. Fixes: animation completion signal renamed from `bolt_placed_complete` to `animation_complete`; watchdog signal renamed from `board_state_refreshed` to `board_refresh_forced` with correct `sequence_id` signature; `move_executing_exited(sequence_id)` added to events table and GSM dependency row; Animation System dependency row updated with full `move_committed` signature; BLOCKING AC preamble reconciled with individual entry labels (AC-07, AC-11, AC-15, AC-16 added). All 5 blockers resolved in-session. Re-review recommended in clean session.
Prior verdict resolved: Yes (17 blockers from 2026-04-30 all confirmed resolved)

## Review — 2026-04-30 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: game-designer, systems-designer, gameplay-programmer, ux-designer, qa-lead
Blocking items: 17 | Recommended: 11
Summary: The core mechanic design is sound — state machine, formulas, and interaction model rationale are all well-specified. Blockers were primarily event contract gaps (5 undefined signals/mechanisms: S-02 pulse, hint pulse, EC-08 watchdog, EC-11 stale signal, animation completion signal TBD), AC/test coverage failures (3 misclassified as advisory, 2 missing ACs, 1 untestable), and UX specification gaps (tap target cap, destination highlighting, input buffer policy). All 17 blockers resolved in-session: one-tap input buffer added, `deadlock_detected()` event defined, `sequence_id` stale-signal protocol established, column cap (≤8) enforced, valid-destination highlighting required from HUD.
Prior verdict resolved: No — first review
