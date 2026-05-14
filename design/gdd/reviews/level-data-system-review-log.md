# Review Log: Level Data System

## Review — 2026-04-28 — Verdict: APPROVED (blockers resolved in-session)
Scope signal: L
Specialists: lean mode — no specialist agents
Blocking items: 3 | Recommended: 5
Summary: Substantial improvement over prior two MAJOR REVISION NEEDED verdicts. Serialization substrate (Newtonsoft.Json), Addressables two-tier design, state machine, and AC coverage are all correctly specified. Three remaining blockers: EC-07 stated READY-only for ReloadAsync but state machine and contract said READY or DEGRADED (text contradiction, now fixed); schema_version "known" versions were unspecified (launch set {1} now defined); GetRange inverted parameters were undefined (empty array, now EC-16 and AC-35). All blockers were text/spec fixes applied in-session. Five recommended items also resolved: level_id range added to validation table, open questions #3 and #5 closed, UniTask ADR tracked, AC BLOCKING header corrected, AC-36 added for absent display_name defaulting.
Prior verdict resolved: Yes — 13 blockers from 2026-04-20 second review fully addressed

## Review — 2026-04-20 — Verdict: MAJOR REVISION NEEDED

Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, level-designer, unity-specialist, creative-director
Blocking items: 22 | Recommended: 22
Summary: Five specialists converged with zero disagreement — issues are structural, not opinion-based. The GDD has the shape of a Foundation data contract but fails the Foundation test in three ways: (1) ambiguous semantics in shared fields (`par_moves` with no solver cross-check, `hint_override=0` vs null foot-gun, `is_tutorial` boolean losing beat identity, `added_version` unstructured string) force defensive coding across 6 consumers; (2) unresolved behavioral contracts (DEGRADED silent-gap breaks Level Progression contiguous expectation, 8-request queue is a nominal-case boot race with no retry SLA, EC-07 in-flight reload needs explicit handle-pinning); (3) unspecified serialization substrate (Open Question #5) makes the data contract literally incomplete — JsonUtility cannot deserialize nested `color_stacks`, Addressables catalog reload invalidates in-flight guarantees, boot order unspecified, iOS background-resume conflicts with cache policy. Additional blockers: EC-09 pre-solved board should be hard-reject at authoring time; missing BLOCKING ACs for EC-03, EC-07, EC-12, par_moves=0; AC-10 UNINITIALIZED invariant not marked BLOCKING; solver tooling deferral to "month 2" incompatible with Beta 100-level target; `color_stacks` nested int array unreadable at scale without authoring tool. Foundation integrity principle: cost-of-wrongness scales with fanout (22 downstream systems × ambiguity cost). Bones are good, structure sound, but Critical Seven are non-negotiable. User elected to stop and revise in fresh session.
Prior verdict resolved: N/A — first review

## Review — 2026-04-20 — Verdict: MAJOR REVISION NEEDED
Scope signal: L
Specialists: systems-designer, qa-lead, game-designer, unity-specialist, creative-director
Blocking items: 13 | Recommended: 9
Summary: Serialization stack is completely broken — LevelRecord cannot round-trip with JsonUtility (int[][], private setters, int? all fail silently). Remote-only Addressables group breaks first mobile launch. Three design pillar violations ship as features: hint_override=0 has no policy, par_moves is hand-authored with no solver cross-check, DEGRADED state creates invisible progression walls. AC-13 through AC-17 remain stubs with no GIVEN/WHEN/THEN; 4 required validation ACs missing; 3 edge cases (EC-05, EC-07, EC-12) have no coverage. Creative-director: bones are sound but Foundation system cannot guarantee downstream systems never receive data that forces undefined player states.
Prior verdict resolved: Partial — structural revision visible, but serializer and AC completeness are new critical failures
