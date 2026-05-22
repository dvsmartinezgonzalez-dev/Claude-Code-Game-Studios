# Sprint 3 — 2026-05-30 to 2026-06-13

> **Generated**: 2026-05-22
> **Updated**: 2026-05-22
> **Review mode**: lean

## Sprint Goal

Deliver the Save & Persistence system (foundation data layer) and scaffold the Quality Tier System, completing BoltSort's boot-time infrastructure and unblocking Level Progression and Coin Economy implementation.

## Capacity

- Total days: 10
- Buffer (20%): 2 days reserved for unplanned work
- Available: 8 days

---

## Sprint Progress (as of 2026-05-22)

| ID | Story | Status |
|----|-------|--------|
| S3-01 | Create Save & Persistence stories | 🔲 Ready — run `/create-stories save-persistence` |
| S3-02 | SP: SaveData schema + SaveSystem boot | 🔲 Backlog — blocked on S3-01 |
| S3-03 | SP: Atomic write — W-1 background thread | 🔲 Backlog — blocked on S3-02 |
| S3-04 | SP: iOS cold-start file protection retry | 🔲 Backlog — blocked on S3-02 |
| S3-05 | SP: W-2 synchronous OnApplicationPause write | 🔲 Backlog — blocked on S3-03 |
| S3-06 | SP: Schema versioning + migration runner | 🔲 Backlog — blocked on S3-02 |
| S3-07 | SP: PlayerPrefs audio prefs integration | 🔲 Backlog — blocked on S3-02 |
| S3-08 | SP ↔ GSM integration test — board persistence round-trip | 🔲 Backlog — blocked on S3-03, S3-05 |
| S3-09 | Create Quality Tier System stories | 🔲 Backlog — unblocked |
| S3-10 | QTS: GPU tier detection + adaptive quality settings | 🔲 Backlog — blocked on S3-09 |
| S3-11 | Create Audio System stories | 🔲 Backlog — unblocked |
| S3-12 | TD-CI-001 investigation | 🔲 Backlog — unblocked |

### Next immediate actions

1. **S3-01** — Run `/create-stories save-persistence` (carryover from S2-10; 0.5d; unblocked)
2. **QA plan** — Run `/qa-plan sprint` before any implementation begins
3. **S3-02** — Begin SaveSystem boot implementation after stories are created

---

## Tasks

### Must Have (Critical Path)

| ID | Story | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|-------|-------------|-----------|--------------|---------------------|
| S3-01 | **Create Save & Persistence stories** — run `/create-stories save-persistence` → story files at `production/epics/save-persistence/`; update `production/epics/index.md` | unity-specialist | 0.5d | — (carryover S2-10) | Story files exist with TR-SP-* IDs, ADR refs, and ACs; index updated |
| S3-02 | **SP: SaveData schema + SaveSystem boot** — `SaveSystem.cs` as DDOL singleton at SEO −90; synchronous `Awake` read of `save.json`; `IsReady = true` before any SEO −90+ `Awake`; `event Action OnSaveReady`; save schema v1 (`schema_version`, `current_level_id`, `completion_record[]`, `coin_balance`, `undo_stack[]`) (TR-SP-001, TR-SP-002, TR-SP-004) | unity-specialist | 1.5d | S3-01 | SaveSystem initializes at SEO −90; `IsReady` true before GSM/CE `Awake`; `OnSaveReady` fires; subscribe-then-check works; unit tests pass |
| S3-03 | **SP: Atomic write — W-1 background thread** — `WriteCompletionAtomic(levelId, bestStars, version, newCurrentLevelId)`: snapshot on main thread → `Awaitable.BackgroundThreadAsync()` → `_writeLock.WaitAsync()` → FileStream + `Flush(flushToDisk:true)` → `File.Replace` (when exists) or `File.Move` (first write); no 3-arg `File.Move` overload (TR-SP-001, TR-SP-003, TR-SP-008) | unity-specialist | 1.0d | S3-02 | Write completes off main thread; `File.Replace` used when save exists; `File.Move` for first write; concurrent call test via `_writeLock`; no `File.Move(src, dst, overwrite: true)` |
| S3-04 | **SP: iOS cold-start file protection retry** — catch `UnauthorizedAccessException` separately from `IOException` (sibling .NET types — not caught by `catch(IOException)`); 250ms retry; 5-second total timeout; thread joined before `IsReady = true` (TR-SP-007) | unity-specialist | 1.0d | S3-02 | Simulated `UnauthorizedAccessException` triggers retry; simulated `IOException` triggers separate catch; timeout fires after 5s; `IsReady` never set true if timeout reached; unit tests for each path |
| S3-05 | **SP: W-2 synchronous OnApplicationPause write** — `OnApplicationPause(true)` triggers synchronous file flush; no `async void`; no `await` anywhere on pause path; write completes before `OnApplicationPause` returns (TR-SP-008) | unity-specialist | 0.5d | S3-03 | Integration test: pause → file exists and is valid JSON with current board state; `async void` forbidden on method |
| S3-06 | **SP: Schema versioning + migration runner** — integer `schema_version`; sequential migrators (`migrate_v0_to_v1`, etc.); migrators run synchronously on read path (R-2); `completion_version` is write-once — migrators must not set it on empty records (TR-SP-006) | unity-specialist | 1.0d | S3-02 | Migrators run in sequence; v0→v1 migrator test; already-v1 save skips migration; empty record `completion_version` not set by migrator; unit tests pass |

### Should Have

| ID | Story | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|-------|-------------|-----------|--------------|---------------------|
| S3-07 | **SP: PlayerPrefs audio prefs** — `audio.sfx_volume`, `audio.ambient_volume`, `audio.ui_volume` stored in `PlayerPrefs`; SaveSystem exposes read/write helpers but does NOT mediate write calls from AudioSystem (TR-SP-005) | unity-specialist | 0.5d | S3-02 | PlayerPrefs keys match spec; SaveSystem read helpers unit-tested; AudioSystem writes directly (not via SaveSystem) |
| S3-08 | **SP ↔ GSM integration test: board persistence round-trip** — board state serialized on `OnApplicationPause` → SaveSystem re-initialized → GSM loads same board state; bolt-count invariant maintained across serialization boundary | unity-specialist | 0.5d | S3-03, S3-05 | Integration test at `tests/integration/save-persistence/`; bolt count before pause = bolt count after reload; test is deterministic |
| S3-09 | **Create Quality Tier System stories** — run `/create-stories quality-tier-system` → story files at `production/epics/quality-tier-system/`; update `production/epics/index.md` | unity-specialist | 0.5d | — | Story files exist with TR-QTS-* IDs; index updated |
| S3-10 | **QTS: GPU tier detection + adaptive quality settings** — GPU tier enum (`Low`/`Med`/`High`) detected on boot via `SystemInfo`; URP quality level set accordingly; PlayerPrefs override (`qts.override_tier`) supported; no 3D physics quality settings (2D only) | unity-specialist | 1.0d | S3-09 | Tier detected and set; unit test for tier classification formula; PlayerPrefs override test; URP quality level assignment verified in EditMode |

### Nice to Have

| ID | Story | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|-------|-------------|-----------|--------------|---------------------|
| S3-11 | **Create Audio System stories** — run `/create-stories audio-system` → story files at `production/epics/audio-system/`; update `production/epics/index.md` | unity-specialist | 0.5d | — | Story files exist; index updated |
| S3-12 | **TD-CI-001: GameCI license investigation** — research root cause; document fix path in `production/tech-debt.md` (no CI workflow changes in this sprint) | devops-engineer | 0.5d | — | Root cause identified; fix approach documented; no CI file edits |

---

## Carryover from Sprint 2

| Task | Reason | New Sprint ID |
|------|--------|---------------|
| Create Save & Persistence stories (S2-10) | Deferred — sprint 2 closed before implementation | S3-01 |
| Device evidence: S2-05 (input coordinate space on physical Android) | No physical device available | Advisory — carry to Alpha gate |
| Device evidence: S2-09 (app-pause on physical iOS/Android) | No physical device available | Advisory — carry to Alpha gate |

---

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| `Awaitable.BackgroundThreadAsync` API behavior differs in Unity 6.3 | Medium | High | Cross-reference `docs/engine-reference/unity/VERSION.md` and ADR-0003 before implementation; test in isolation first |
| SP stories (post-S3-01) come back with >8 stories, exceeding capacity | Medium | Medium | Cap Must Have at 8 stories; mark excess Should Have; carry to Sprint 4 |
| `IOException`/`UnauthorizedAccessException` sibling-catch confusion | Low | High | ADR-0003 and EPIC.md both flag this — enforce in code review; specific unit tests for each exception type |
| QTS GPU detection not verifiable headlessly | Medium | Low | Unit test the classification formula; mark rendering output tests Visual/Advisory |
| TD-CI-001 fix estimate grows if root cause is Unity license server scope | Low | Medium | Investigation only in Sprint 3; no fix until scope is clear |

---

## Dependencies on External Factors

- Physical device needed for final S2-05 / S2-09 QA evidence — deferred to Alpha gate; no blocker on Sprint 3 work
- `Awaitable` (Unity 6.3 async API) — documented in ADR-0003; no third-party dependency

---

## Implementation Notes — Save & Persistence

Key forbidden patterns (ADR-0003 + EPIC.md):

- **Never** `File.Move(source, dest, overwrite: true)` — 3-arg overload does not exist in .NET Standard 2.1 (Unity 6.3 BCL). Use `File.Replace(tmp, save, null)` when save exists; `File.Move(tmp, save)` for first write.
- **Never** `async void Awake()` — Unity does not await Awake; `IsReady = true` must be synchronous.
- **Never** `async void OnApplicationPause()` — Unity returns control to OS at first `await`; W-2 must be fully synchronous.
- `catch(IOException)` does **NOT** catch `UnauthorizedAccessException` — they are sibling .NET types. Always catch both separately.

---

## Definition of Done for Sprint 3

- [ ] All Must Have tasks completed
- [ ] `SaveSystem.cs` present with all TR-SP-001 through TR-SP-008 acceptance criteria met
- [ ] QA plan exists (`production/qa/qa-plan-sprint3-*.md`)
- [ ] All Logic/Integration stories have passing unit/integration tests (local suite ≥ 309)
- [ ] Smoke check passed (`/smoke-check sprint`)
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`)
- [ ] No S1 or S2 bugs in delivered features
- [ ] `production/epics/index.md` updated with SP and QTS story counts
- [ ] Design documents updated for any deviations from TR-SP-* specs

> ⚠️ **No QA Plan**: This sprint was started without a QA plan. Run `/qa-plan sprint`
> before the first story implementation begins. The Production → Polish gate requires
> a QA sign-off report, which requires a QA plan.
