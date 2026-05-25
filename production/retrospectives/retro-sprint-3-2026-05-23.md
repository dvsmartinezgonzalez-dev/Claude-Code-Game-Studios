# Retrospective: Sprint 3 — Save & Persistence
Period: 2026-05-22 – 2026-05-23 (nominal: 2026-05-30 – 2026-06-13)
Generated: 2026-05-23

---

## Metrics

| Metric | Planned | Actual | Delta |
|--------|---------|--------|-------|
| Must-Have Stories | 5 impl. | 5 | 0 |
| Should/Nice-to-Have Stories | 6 | 0 | −6 |
| Must-Have Completion Rate | 100% | 100% | — |
| Overall Sprint Completion | — | 45% (5/11 total) | — |
| Estimated Days (must-have) | 5.0d | ~2d actual | −3d |
| Bugs Found / Filed | — | 0 | — |
| Code Review Blocking Issues | — | 1 found + fixed | — |
| CI Commits (unplanned fix attempts) | 0 | 5 | +5 |
| Total Commits | — | 7 | — |
| TODO/FIXME in codebase | — | 1 / 0 | Very low |

---

## Velocity Trend

| Sprint | Must-Have Stories Planned | Completed | Rate |
|--------|--------------------------|-----------|------|
| Sprint 1 | ~14 (LDS + GSM) | 14 | ~100% |
| Sprint 2 | 7 of 11 | 7 | 64% total / 100% must-have |
| Sprint 3 | 5 of 11 | 5 | 45% total / 100% must-have |

**Trend**: Must-have completion is 100% for the third sprint running. Total completion drops as should-have/nice-to-have backlog accumulates. Story complexity per unit is increasing as the project moves into infrastructure work.

---

## What Went Well

- **100% must-have delivery, third sprint running.** All 5 SP implementation stories delivered with full test evidence in ~2 days of actual work, well under the 8d capacity budget.
- **Code review process caught a real threading bug.** `/code-review` flagged `JsonUtility.ToJson` on a background thread (ADR-0003 violation). Fix applied before story-done; `WriteAtomicCore` now accepts `byte[]`, serialization correctly on main thread.
- **IFileSystem injection seam is paying dividends.** All 5 test files use `FakeFileSystem` cleanly — fast, deterministic EditMode tests with no filesystem mocking gymnastics.
- **Tech debt is tracked, not hidden.** 7 items (TD-SP-001 through TD-SP-007) logged explicitly during story-done reviews.
- **Zero bugs in delivered features.** QA sign-off: APPROVED WITH CONDITIONS — no S1/S2 issues.

---

## What Went Poorly

- **CI pipeline (TD-CI-001) still unresolved — sprint 2 carryover.** 5 additional fix commits made with no green run achieved. Local EditMode confirmation is the fallback gate. Highest-risk ongoing issue.
- **Should-have stories carried 100%.** S3-07 through S3-10 not started because work was completed before the sprint's official start date (plan: May 30; actual work: May 22–23). The planning vs. execution date mismatch creates a structural gap.
- **Sprint dates are disconnected from actual work cadence.** Velocity metrics are unreliable when the sprint date window doesn't reflect when work happens.
- **Physical device evidence outstanding since Sprint 2.** AC-08b and AC-28 deferred to Alpha gate for two sprints without a device provisioning plan.

---

## Blockers Encountered

| Blocker | Duration | Resolution | Prevention |
|---------|----------|------------|------------|
| GameCI license activation (TD-CI-001) | Sprint 2 + Sprint 3 | Not resolved — waived per DoD | Self-hosted runner with cached seat, or Unity Pro license |
| No physical iOS/Android device | Both sprints | Deferred to Alpha gate | Cloud device farm (BrowserStack, Firebase Test Lab) |
| JsonUtility threading violation (code review) | ~15 min | Resolved — `WriteAtomicCore(byte[])` | Pre-implementation Unity API thread-safety checklist |

---

## Estimation Accuracy

| Story | Estimated | Notes |
|-------|-----------|-------|
| S3-02 Boot + Schema | 1.5d | Completed in same session as other stories |
| S3-03 Atomic Write W-1 | 1.0d | On target |
| S3-04 iOS Retry + R-4 | 1.0d | On target |
| S3-05 W-2 Pause Write | 0.5d | On target |
| S3-06 Schema Migration | 1.0d | On target |

**Overall**: Must-haves completed in ~40% of capacity. Estimates appear consistently generous for infrastructure stories. **Recommendation**: reduce infrastructure story estimates by ~30% in Sprint 4, or pack more should-haves into the must-have bucket.

---

## Carryover Analysis

| Story | Times Carried | Reason | Sprint 4 Action |
|-------|---------------|--------|-----------------|
| S3-07 PlayerPrefs | 0 (new carryover) | Not started | Promote to must-have |
| S3-08 GSM round-trip | 0 (new carryover) | Not started | Promote to must-have |
| S3-09 Create QTS stories | 0 (new carryover) | Not started | Should-have |
| S3-10 QTS tier detection | 0 (new carryover) | Blocked on S3-09 | Should-have |
| TD-CI-001 | 2 sprints | Root cause not isolated | Timebox 0.5d in Sprint 4 |

---

## Technical Debt Status

- TODO: 1 | FIXME: 0 | HACK: 0 — **Trend: Stable / Low**
- Registered items: TD-SP-001 through TD-SP-007 (all advisory), TD-CI-001 (medium severity)
- Highest priority: TD-SP-006 (PlayMode W-1+W-2 race test), TD-CI-001

---

## Previous Action Items Follow-Up

*First retrospective — no prior items.*

---

## Action Items for Next Sprint

| # | Action | Priority | Deadline |
|---|--------|----------|----------|
| 1 | Resolve TD-CI-001: self-hosted runner or Unity Pro license — timebox 0.5d max | High | Sprint 4 start |
| 2 | Promote S3-07 (PlayerPrefs) + S3-08 (GSM round-trip) to Sprint 4 must-have | High | Sprint 4 planning |
| 3 | Establish physical device access plan for AC-08b + AC-28 | Medium | Sprint 4 |
| 4 | Implement TD-SP-006: PlayMode W-1+W-2 concurrent race test (`[UnityTest]`) | Medium | Sprint 4 (with S3-08) |
| 5 | Align sprint plan dates to actual work start — fix velocity metric reliability | Low | Sprint 4 planning |

---

## Process Improvements

- **Promote should-haves more aggressively.** Must-haves finish in ~40% of available time consistently. Pack Sprint 4 must-have with S3-07 and S3-08 — don't leave them in should-have where they'll slide again.
- **CI gate must be resolved before Sprint 5.** Two sprints without working CI means the automated test safety net is theoretical. Hard deadline: CI green by Sprint 4 end.

---

## Summary

Sprint 3 delivered its entire must-have scope — the complete Save & Persistence foundation — with zero bugs and a clean code review. The system handles mobile atomicity, iOS file-protection, and schema migration correctly. The sprint's main weakness is planning hygiene: dates didn't match the actual work window, should-haves carried forward untouched, and CI remains broken for a second sprint. The single highest-priority improvement for Sprint 4 is fixing CI — every sprint without it is a sprint where regressions could ship undetected.
