## QA Sign-Off Report: Sprint 2 — Sort Mechanic
**Date**: 2026-05-21
**Sprint**: Sprint 2 (2026-05-19 – 2026-05-30)
**QA Plan**: `production/qa/qa-plan-sprint2-2026-05-21.md`
**Smoke Check**: `production/qa/smoke-2026-05-20.md` — PASS WITH WARNINGS

---

### Test Coverage Summary

| Story | Type | Auto Test | Manual QA | Result |
|-------|------|-----------|-----------|--------|
| S2-04 FSM Core | Logic | 19 unit — PASS (CI 2026-05-19, 303/303) | Not required | PASS |
| S2-05 Input Handling | Logic | 29 unit — PASS (CI 2026-05-19, 303/303) | Touch coords on device — BLOCKED (no device) | PASS WITH NOTES |
| S2-06 Move Validation | Logic | 15 unit — PASS (CI 2026-05-19, 303/303) | Not required | PASS |
| S2-07 Win Condition | Logic | 18 unit — PASS (CI 2026-05-19, 303/303) | Not required | PASS |
| S2-08 Deadlock Detection | Integration | 13 (unit + integration) — PASS (CI 2026-05-19, 303/303) | Not required | PASS |
| S2-09 App-Pause Cancellation | Integration | 6 integration — written 2026-05-20, CI re-run pending | Android device home-button mid-move — BLOCKED (no device) | PASS WITH NOTES |

**Total automated tests on last confirmed CI run**: 303/303 (2026-05-19)
**Expected total after S2-09**: ~309
**S2-11 GSM Full Integration**: Nice to Have — backlog, out of sprint scope, not evaluated

---

### Bugs Found

| ID | Story | Severity | Status |
|----|-------|----------|--------|
| — | — | — | No bugs filed this sprint |

---

### Open Advisory Items

| # | Item | Blocks Release? |
|---|------|----------------|
| 1 | CI re-run required to confirm ~309/309 green after S2-09 test addition (2026-05-20) | No — advisory |
| 2 | `production/qa/evidence/sort_mechanic_input_device_test.md` not created — S2-05 on-device touch coordinate verification (TC-S205-01–TC-S205-04) | No — advisory |
| 3 | `production/qa/evidence/sort_mechanic_pause_device_test.md` not created — S2-09 Android home-button mid-move verification (TC-S209-01–TC-S209-04) | No — advisory |
| 4 | S2-08 deviations: misleading test name `AC10_..._DeadlockBefore_MoveExecutingExited`; corrupt-board AC-25 suppression path untested; `SubscribeToGsmForTesting()` fragility; ADR-0006 typo | No — S3/S4 polish items |
| 5 | S2-09 deviations: no dedicated TR for EC-14 app-pause cancellation; SEO −55 physical device verification required before ship; `OnApplicationPause(false)` path tested indirectly only | No — advisory; SEO verification required at ship gate |

---

### Verdict: APPROVED WITH CONDITIONS

No S1 or S2 bugs are open. All Must Have and Should Have Logic/Integration stories have automated test coverage satisfying the DoD blocking gate. No story fails.

**Conditions to resolve before milestone hand-off:**

1. **CI re-run** — Run `game-ci/unity-test-runner@v4` after the S2-09 test commit and confirm ~309/309 green. Record run date and pass count.

2. **On-device evidence — defer or document** — Physical device testing for S2-05 (touch coordinate mapping) and S2-09 (Android app-pause under real OS suspension) is blocked by device availability. Before shipping to any external build (Alpha, Beta, or store): either (a) complete device tests and file evidence docs at `production/qa/evidence/`, or (b) obtain producer sign-off explicitly deferring device evidence to a later milestone with documented risk acceptance. The SEO −55 ordering guarantee (S2-09 deviation) is in this same category — must be verified on device before ship.

---

### Next Step

Trigger a CI re-run targeting `main` HEAD (post-S2-09 merge). Once ~309/309 is confirmed green, Condition 1 is cleared and the sprint is ready for sprint review. Carry the two device-evidence items forward as tracked advisory items into Sprint 3 backlog. Schedule device access during Sprint 3 to clear them before the Alpha milestone gate — `/gate-check` will surface them as open items at that point.
