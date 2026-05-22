# Sprint 2 — 2026-05-19 to 2026-05-30

> **Generated**: 2026-05-17
> **Updated**: 2026-05-19
> **Review mode**: lean

## Sprint Goal

Close Sprint 1 CI conditions and implement the Sort Mechanic — BoltSort's core interactive system — delivering a playable game loop from tap input to win detection.

## Capacity

- Total days: 10
- Buffer (20%): 2 days reserved for unplanned work
- Available: 8 days

---

## Sprint Progress (as of 2026-05-19)

**7/11 stories done — ~2.0d remaining work**

| ID | Story | Status |
|----|-------|--------|
| S2-01 | Close Sprint 1 CI conditions | ✅ Done (2026-05-19) |
| S2-02 | Create Sort Mechanic stories | ✅ Done (2026-05-17) |
| S2-03 | QA plan for Sprint 2 | ✅ Done (2026-05-21) |
| S2-04 | FSM core + board state read | ✅ Done (2026-05-17) |
| S2-05 | Input handling + back gesture | ✅ Done (2026-05-18) |
| S2-06 | Move validation + column cap | ✅ Done (2026-05-18) |
| S2-07 | Win detection + seqId guard | ✅ Done (2026-05-18) |
| S2-08 | Deadlock detection | ✅ Done (2026-05-19) |
| S2-09 | App-pause cancellation + SEO | ✅ Done (2026-05-22) — local 309/309 pass; device evidence deferred to Sprint 3 backlog |
| S2-10 | Create Save & Persistence stories | 🔲 Backlog — deferred to Sprint 3 |
| S2-11 | GSM ↔ Sort Mechanic integration | ✅ Done (2026-05-22) — local tests pass; CI unblocked per user instruction |

**Remaining execution order:**
1. **S2-03** — Run `/qa-plan sprint` (required for DoD; unblocked since Day 1)
2. **S2-09** — App-pause cancellation + SEO contract (Story 006, 0.5d)
3. **S2-11** — GSM full integration test (Story 007, 0.5d) — blocked on S2-09

---

## Tasks

### Must Have — Critical Path (6.5 days)

| ID | Task | Est. | Dependencies | Acceptance Criteria |
|----|------|------|-------------|-------------------|
| S2-01 | **Close Sprint 1 CI conditions** — run `game-ci/unity-test-runner@v4` on main; confirm all tests PASS in CI; confirm `Tests.Integration.GameStateManager.asmdef` compiles without error in Unity Editor; update `production/qa/smoke-2026-05-16.md` with results | 0.5d | CI secrets live (commit ddfdf1c) | CI shows green; no compile errors; smoke log updated |
| S2-02 | **Create Sort Mechanic stories** — run `/create-stories sort-mechanic` → story files at `production/epics/sort-mechanic/`; update `production/epics/index.md` | 0.5d | `sort-mechanic/EPIC.md` Ready; GSM Done | Story files exist with TR-IDs, ADR refs, ACs |
| S2-03 | **QA plan for Sprint 2** — run `/qa-plan sprint` before any implementation begins; defines test evidence requirements per story | 0.5d | S2-02 (stories exist) | `production/qa/qa-plan-sprint2-YYYY-MM-DD.md` exists covering all Sort Mechanic stories |
| S2-04 | **Sort Mechanic: FSM core + board state read** — IDLE / BOLT_SELECTED / MOVE_EXECUTING / CANCELLATION / INVALID_MOVE / WIN state machine; synchronous `IReadOnlyList<int>[]` read from GSM (`StackContents[]`, `TempSlotContents[]`) (TR-SORT-001, TR-SORT-009) | 1.5d | S2-02; GSM Done | All 6 FSM states transition correctly; unit tests pass for each state and transition; `int64` seqId used (not int32 — overflow softlock prevention) |
| S2-05 | **Sort Mechanic: Input handling** — `EnhancedTouchSupport` + `Physics2D.OverlapPoint` with cached layer mask; one-tap buffer during MOVE_EXECUTING (discarded on WIN exit); Android back gesture cancellation in BOLT_SELECTED (`Keyboard.current?.escapeKey.wasPressedThisFrame` with iOS null guard) (TR-SORT-004, TR-SORT-008) | 1.5d | S2-04 | Tap dispatches to correct stack via OverlapPoint; back gesture cancels BOLT_SELECTED; buffered tap fires on IDLE re-entry; no iOS crash on `Keyboard.current` null |
| S2-06 | **Sort Mechanic: Move validation + column cap** — empty destination accepts any color; full destination rejects (`MoveRejectReason.DestinationFull`); non-full non-empty accepts only if top bolt matches (`MoveRejectReason.ColorMismatch`); guarded conditional (read `destination_top_color` only after empty guard passes); `color_count + temp_slot_count ≤ 8` assertion at init → `level_load_failed(CORRUPTED_BOARD_STATE)` on violation (TR-SORT-002, TR-SORT-010) | 1.0d | S2-04 | All 3 validation cases have unit tests; column cap assertion fires on violation; no eager bool path |
| S2-07 | **Sort Mechanic: Win detection + sequence ID guard + OnMoveExecutingExited** — win check on every MOVE_EXECUTING exit: all color stacks full + monochromatic, temp slots excluded; `OnMoveExecutingExited(seqId)` on IDLE path only (not WIN, not watchdog); seqId guard discards stale `animation_complete` where `seqId != _currentMoveExecutingSeqId` (TR-SORT-003, TR-SORT-006, TR-SORT-007) | 1.0d | S2-04, S2-06 | Win condition unit tests: solved board fires `OnPuzzleSolved`; temp-slot board does not; stale seqId discarded; `OnMoveExecutingExited` absent on WIN path |

### Should Have (1.5 days)

| ID | Task | Est. | Dependencies | Acceptance Criteria |
|----|------|------|-------------|-------------------|
| S2-08 | **Sort Mechanic: Deadlock check** — depth-1 check on every `OnMoveExecutingExited` (not WIN); if no legal move exists across all held bolts → emit `OnDeadlockDetected`; canonical deadlock fixture at `tests/helpers/sort-mechanic-fixtures/` (TR-SORT-005) | 1.0d | S2-04, S2-07 | Canonical fixture emits `OnDeadlockDetected`; non-deadlock board does not; fixture file exists |
| S2-09 | **Sort Mechanic: App-pause cancellation + SEO** — `OnApplicationPause(true)` cancels held bolt (emits `OnMoveCancelled`) before GSM serializes board; Sort Mechanic SEO set lower (higher priority) than GSM in Script Execution Order settings; integration test in `tests/integration/gsm-sort-mechanic/` | 0.5d | S2-04; GSM Done | Integration test: app-pause during BOLT_SELECTED produces valid `bolt_count_invariant` in serialized board snapshot |

### Nice to Have (1.0 day)

| ID | Task | Est. | Dependencies | Acceptance Criteria |
|----|------|------|-------------|-------------------|
| S2-10 | **Create Save & Persistence stories** — run `/create-stories save-persistence` → story files at `production/epics/save-persistence/` | 0.5d | `save-persistence/EPIC.md` Ready | Story files exist; linked from `production/epics/index.md` |
| S2-11 | **GSM ↔ Sort Mechanic integration test** — full move cycle (tap → GSM mutation → `OnBoardStateChanged` → IDLE) verified end-to-end in `tests/integration/gsm-sort-mechanic/` | 0.5d | S2-04–S2-09 | Integration test passes; board state changes confirmed via GSM events triggered by Sort Mechanic |

---

## Carryover from Sprint 1

| Task | Reason | Action |
|------|--------|--------|
| CI green run | Not executed before QA signoff | → S2-01 ✅ Done |
| `Tests.Integration.GameStateManager.asmdef` compile confirmation | New asmdef created during Sprint 1, not confirmed in Unity Editor | → S2-01 ✅ Done |
| LDS-005 coverage gaps (AC-25, AC-26, failed-reload path, `OnLevelDataReady` non-emission on failure) | Advisory from Sprint 1 QA signoff | → Sprint 3 backlog |

---

## Risks

| Risk | Prob | Impact | Mitigation |
|------|------|--------|------------|
| `Physics2D.OverlapPoint` + Input System touch coordinate mismatch on device | Medium | High — blocks S2-05 | Test coordinate space conversion (screen → world) on physical device early in S2-05; reference ADR-0007 layer mask caching |
| Android back gesture: hardware Back vs predictive back (Android 13+) | Low-Medium | Medium | ADR-0007 prescribes `Keyboard.current?.escapeKey.wasPressedThisFrame` — verify on physical Android device; null guard is mandatory |
| `int32` seqId overflow softlock | Low | Critical | Epic note mandates `long` (int64) in C# for all seqId event signatures — enforce in S2-04 code review |
| Unity CI license scope | Low | High | Commit ddfdf1c configured secrets; S2-01 is the first task and will surface any CI misconfiguration before other work begins |
| Sort Mechanic story count exceeds estimate | Low-Medium | Medium | If `/create-stories` produces >6 stories, promote S2-08/S2-09 to Should Have ceiling and carry excess to Sprint 3 |

---

## Dependencies on External Factors

- Physical Android device required for S2-05 (input system test) and S2-09 (app-pause test)
- Unity Editor available for S2-01 (asmdef compile confirmation) and throughout S2-03–S2-09
- `game-ci/unity-test-runner@v4` GitHub Actions runner available (S2-01)

---

## Definition of Done for Sprint 2

- [x] All Must Have tasks (S2-01 through S2-07) complete
- [x] CI passes green on `main` — 303 tests passing (Sprint 1 + Sort Mechanic stories 001–005)
- [x] Sort Mechanic FSM, input handling, move validation, win detection, and deadlock detection have passing unit/integration tests
- [ ] QA plan exists at `production/qa/qa-plan-sprint2-*.md` — **run `/qa-plan sprint` (S2-03)**
- [x] All Logic stories have BLOCKING unit tests; Integration stories have tests in `tests/integration/`
- [ ] Smoke check passes: `/smoke-check sprint` — PASS or PASS WITH WARNINGS
- [ ] QA sign-off: `/team-qa sprint` — APPROVED or APPROVED WITH CONDITIONS
- [ ] No S1 or S2 bugs in delivered features
- [x] Story files updated with final status and test counts via `/story-done`
- [x] `production/sprint-status.yaml` updated at sprint close

---

## QA Plan

> ⚠️ **No QA Plan yet for Sprint 2.** Run `/qa-plan sprint` as task S2-03 now — this is required before the sprint can reach DoD sign-off.
