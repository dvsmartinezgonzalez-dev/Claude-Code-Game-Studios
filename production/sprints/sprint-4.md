# Sprint 4 — 2026-06-14 to 2026-06-27

> **Generated**: 2026-05-23
> **Updated**: 2026-05-23
> **Review mode**: lean

## Sprint Goal

Resolve the CI gate (TD-CI-001), close the remaining Save & Persistence integration gaps, ship the Quality Tier System, and lay the Coin Economy foundation — completing BoltSort's MVP infrastructure and opening the Beta feature track.

## Capacity

- Total days: 10
- Buffer (20%): 2 days reserved for unplanned work
- Available: 8 days

---

## Tasks

### Must Have (Critical Path)

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S4-01 | **TD-CI-001: Resolve CI pipeline** — switch to self-hosted runner or Unity Pro license; achieve a green CI run; update `production/tech-debt.md` (timebox: 0.5d max — if not resolved in 0.5d, document path and carry to Sprint 5) | devops-engineer | 0.5d | — | GitHub Actions runs Unity tests; green run artifact captured; TD-CI-001 closed or scope doc updated |
| S4-02 | **SP: PlayerPrefs audio prefs integration** (S3-07 carryover) — `audio.sfx_volume`, `audio.ambient_volume`, `audio.ui_volume` stored in `PlayerPrefs`; SaveSystem read helpers; AudioSystem writes directly | unity-specialist | 0.5d | S3-06 ✓ | PlayerPrefs keys match spec; read helpers unit-tested; AudioSystem write path confirmed |
| S4-03 | **SP ↔ GSM integration test: board persistence round-trip** (S3-08 carryover) — board state serialized on `OnApplicationPause` → SaveSystem re-initialized → GSM loads same board state; bolt-count invariant maintained | unity-specialist | 0.5d | S3-03 ✓, S3-05 ✓ | Integration test at `tests/integration/save-persistence/`; bolt count before pause = bolt count after reload; deterministic |
| S4-04 | **TD-SP-006: PlayMode W-1+W-2 concurrent race test** — `[UnityTest]` PlayMode harness; fire W-1 (async background write) and W-2 (pause write) concurrently; assert dirty-flag is not corrupted and final file is valid JSON | unity-specialist | 0.5d | S4-03 | PlayMode test at `tests/integration/save-persistence/SaveSystem_Pause_Race_Test.cs`; test passes in PlayMode; both W-1 and W-2 complete without file corruption |
| S4-05 | **Create QTS stories** (S3-09 carryover) — run `/create-stories quality-tier-system` → story files at `production/epics/quality-tier-system/`; update `production/epics/index.md` | unity-specialist | 0.5d | — | Story files exist with TR-QTS-* IDs; ADR refs; ACs; index updated |
| S4-06 | **QTS: GPU tier detection + adaptive quality settings** (S3-10 carryover) — GPU tier enum (`Low`/`Med`/`High`) detected on boot via `SystemInfo`; URP quality level set accordingly; PlayerPrefs override (`qts.override_tier`) supported | unity-specialist | 1.0d | S4-05 | Tier detected and set on boot; unit test for tier classification formula; PlayerPrefs override test; URP assignment verified in EditMode |
| S4-07 | **Create Coin Economy stories** — run `/create-stories coin-economy` → story files at `production/epics/coin-economy/`; update `production/epics/index.md` | unity-specialist | 0.5d | — | Story files exist with TR-CE-* IDs; ADR refs; ACs; index updated |
| S4-08 | **CE: SpendCoins / EarnCoins core** — `CoinEconomyManager` singleton; `EarnCoins(int amount)`, `SpendCoins(int amount) → bool`; coin balance persisted via SaveSystem; balance never goes negative; `OnBalanceChanged` event | unity-specialist | 1.0d | S4-07, S3-06 ✓ | SpendCoins returns false if insufficient; balance persists across boot cycle; unit tests: earn, spend-success, spend-fail, persistence round-trip |

### Should Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S4-09 | **Physical device access plan** (retro AC-08b/AC-28 action item) — evaluate cloud device farm options (BrowserStack, Firebase Test Lab); document provisioning plan in `production/qa/device-plan.md`; choose a path for Alpha gate evidence | lead-programmer | 0.5d | — | Plan document at `production/qa/device-plan.md`; cost and timeline estimated; decision recorded |
| S4-10 | **Create Audio System stories** (S3-11 carryover) — run `/create-stories audio-system` → story files at `production/epics/audio-system/`; update `production/epics/index.md` | audio-director | 0.5d | — | Story files exist; index updated |
| S4-11 | **CE: LevelCompleted coin reward** — subscribe to GSM `LevelCompleted` event; call `EarnCoins(stars * CE_STAR_MULTIPLIER)`; emit `OnCoinsEarned(amount, source)`; star-multiplier is data-driven config | unity-specialist | 1.0d | S4-08 | Coins awarded on level complete; multiplier loaded from config (not hardcoded); event emitted; integration test with GSM stub |

### Nice to Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S4-12 | **Create Level Progression stories** — run `/create-stories level-progression` → story files at `production/epics/level-progression/`; update `production/epics/index.md` | unity-specialist | 0.5d | — | Story files exist with TR-LP-* IDs; index updated |
| S4-13 | **LP: Level unlock core** — unlock next level on `LevelCompleted`; sequence defined by `LevelCatalogue`; `CurrentLevelId` updated in SaveSystem | unity-specialist | 1.0d | S4-12, S4-08 | Next level unlocks after completion; state persists; unit test for sequential unlock |

---

## Carryover from Sprint 3

| Story | Times Carried | Reason | Sprint 4 Status |
|-------|---------------|--------|-----------------|
| S3-07 SP: PlayerPrefs | 0 (new) | Not started — actual work before sprint window | Promoted to must-have (S4-02) |
| S3-08 SP ↔ GSM round-trip | 0 (new) | Not started — same reason | Promoted to must-have (S4-03) |
| S3-09 Create QTS stories | 0 (new) | Not started | Promoted to must-have (S4-05) |
| S3-10 QTS tier detection | 0 (new) | Blocked on S3-09 | Promoted to must-have (S4-06) |
| S3-11 Create Audio stories | 0 (new) | Not started | Should-have (S4-10) |
| S3-12 TD-CI-001 investigation | 1 sprint | Not resolved | Promoted to must-have as full fix (S4-01) |

---

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| TD-CI-001 fix requires Unity Pro license (cost/admin) | Medium | High | Timebox 0.5d; document self-hosted runner path as backup and defer to Sprint 5 if cost blocks |
| PlayMode race test (S4-04) reveals actual race condition in W-1/W-2 | Low | High | If found, create a blocking bug story before closing sprint; do not skip test |
| Coin Economy story scope expands after `/create-stories` | Medium | Medium | S4-07 (create-stories) runs first; if scope > 1.5d for Sprint 4, defer S4-11 to should-have |
| Sprint dates again don't match actual work window | High | Low | If work starts before 2026-06-14, update sprint start date before first commit |

---

## Dependencies on External Factors

- Unity CI license or self-hosted runner machine — needed for S4-01
- Cloud device farm account — needed for S4-09 (BrowserStack/Firebase Test Lab evaluation)

---

## Definition of Done for Sprint 4

- [ ] All Must Have tasks completed
- [ ] CI pipeline green with a captured run artifact (TD-CI-001 resolved or formally deferred with documented path)
- [ ] SP ↔ GSM integration test passing at `tests/integration/save-persistence/`
- [ ] PlayMode W-1+W-2 race test passing
- [ ] QTS implemented and unit-tested
- [ ] CE SpendCoins/EarnCoins implemented with persistence round-trip test
- [ ] QA plan exists (`production/qa/qa-plan-sprint4-*.md`)
- [ ] All Logic/Integration stories have passing unit/integration tests
- [ ] Smoke check passed (`/smoke-check sprint`)
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`)
- [ ] No S1 or S2 bugs in delivered features
- [ ] `production/epics/index.md` updated with QTS and CE story counts
