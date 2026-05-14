# Architecture Review Report — BoltSort

> **Generated:** 2026-05-12 | `/architecture-review full`
> **Supersedes:** Architecture Review 2026-05-03

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP 17.x, 2D Renderer) |
| **GDDs Reviewed** | 11 |
| **ADRs Reviewed** | 13 (ADR-0001 – ADR-0013, all status: **Accepted**) |
| **TR Registry** | v4 — 72 active entries |

---

## Verdict: CONCERNS

**Not FAIL** — Every active TR resolves to at least one Accepted ADR. All five blocking issues, four conflicts, and one missing ADR from the 2026-05-03 review have been closed. All 13 ADRs are Accepted; no dependency cycles.

**Not PASS** — Two localized contract conflicts remain (`OnLevelUnloaded` undeclared in GSM events; star-rating contradiction within ADR-0006) plus two staleness items (architecture.md severely outdated; TR-GSM-011 ADR field).

---

## Traceability Summary

| Status | Count |
|--------|-------|
| ✅ Covered | **72** (100%) |
| ⚠️ Partial | 0 |
| ❌ Gap | 0 |
| **Total** | **72** |

No new TR-IDs added in this review (TR registry v4 from 2026-05-04 is current). One TR registry entry needs a stale-ADR-field update (see Phase 6).

---

## Prior Review Closure (2026-05-03 → 2026-05-12)

| Prior issue | Resolution |
|---|---|
| All 13 ADRs `Proposed` → auto-blocked | All 13 now `Accepted` |
| ADR-0004 failure path hangs LevelProgression | DEGRADED state implemented (ADR-0004 lines 185–198) |
| ADR-0010 `GpuTimingProbe` non-existent API | Replaced with `FrameTimingManager.GetLatestTimings()` |
| ADR-0002/0006 stale 2-arg `OnLevelComplete` | Both updated to 4-arg canonical `(levelId, moveCount, parMoves, sequenceId)` |
| ADR-0005 tier thresholds vs QTS GDD | Aligned: <512 MB → Low; ≥1536 MB + shader ≥46 → High |
| ADR-0003 W-2 missing `catch(OperationCanceledException)` | try/catch added (ADR-0003 line 187) |
| ADR-0003 iOS retry thread join timing | Explicit thread join requirement (ADR-0003 lines 110–111) |
| ADR-0007 `Physics2D.OverlapPoint` missing layer mask | `_boltStacksLayerMask` cached + passed to `OverlapPoint` (ADR-0007 line 88) |
| ADR-0001 boot table missing LDS at SEO −95 | LDS added at SEO −95 (ADR-0001 line 57) |
| ADR-0010 `_activeVFXInstances` lifecycle undefined | `HandleLevelLoaded`/`HandleLevelUnloaded` lifecycle documented (ADR-0010 lines 158–189) |
| TR-SORT-010 (column cap) uncovered | ADR-0013 written and Accepted |

---

## Cross-ADR Conflicts

### 🔴 CONFLICT 1: `GSM.OnLevelUnloaded` consumed by ADR-0010 but not declared by ADR-0006 / ADR-0002

**Type:** Integration contract — missing event declaration

| Document | Statement |
|---|---|
| ADR-0010 line 167 | `GameStateManager.Instance.OnLevelUnloaded += HandleLevelUnloaded;` (clears `_activeVFXInstances` on TEARDOWN) |
| ADR-0006 `IGameStateManager` (line 388) | Events: `OnLevelLoaded`, `OnBoardStateChanged`, `OnBoardRefreshForced`, `OnLevelComplete`, `OnSessionLoadFailed`. **No `OnLevelUnloaded`.** |
| ADR-0002 Complete Event Catalog (lines 185–191) | GSM event list omits `OnLevelUnloaded`. |
| `game-state-manager.md` line 198 / `architecture.md` line 198 | Both reference `level_unloaded(level_id)` as a GSM-emitted event. |

**Impact:** ADR-0010's `Awake()` snippet does not compile against the `IGameStateManager` interface as defined in ADR-0006. The animation system VFX cleanup path is broken at the contract layer. The GDD says the event exists; ADR-0010 consumes it; ADR-0006/ADR-0002 don't declare it — a three-way drift.

**Resolution options:**
1. **(Recommended)** Add `event Action<int> OnLevelUnloaded; // (levelId)` to ADR-0006 GSM event catalog + `IGameStateManager` interface, and to ADR-0002 event catalog. GDD already specifies it; ADR-0010 already consumes it.
2. Change ADR-0010 to clear `_activeVFXInstances` at the start of the next `OnLevelLoaded` instead.

---

### 🔴 CONFLICT 2: Star rating ownership contradiction within ADR-0006, inconsistent with ADR-0012

**Type:** Integration contract — internal contradiction + cross-ADR signature mismatch

ADR-0006 contradicts itself within 12 lines:

| ADR-0006 location | Statement |
|---|---|
| Line 327 | `int stars = ComputeStarRating(moveCount, parMoves);` — LP **computes** stars itself; 2-arg signature |
| Line 339 | "Star rating formula is owned by the Level Complete UI GDD (not GSM). **LP receives it from LC UI via the `OnLevelCompleted` event parameter.**" |

These are mutually exclusive. Additionally, ADR-0012 (the authoritative shared-utility ADR) defines:

```csharp
StarRatingCalculator.Compute(int moveCount, int parMoves, float threshold2Star)  // 3-arg
```

…called by both HUD and LevelCompleteUI per ADR-0012's GDD requirements table (lines 411, 414). ADR-0006's `ComputeStarRating(moveCount, parMoves)` references a **different function name** and **drops the `threshold2Star` argument**.

**Impact:** LP cannot be implemented from ADR-0006 alone. Line 327 invents a non-existent function with the wrong signature; line 339 mandates an event payload that does not exist — LCUI's outbound events in ADR-0012 (`OnCoinRewardGranted(int)`, etc.) carry only coin amounts, never stars.

**Resolution:**
1. ADR-0006 line 327 — change `ComputeStarRating(moveCount, parMoves)` to `StarRatingCalculator.Compute(moveCount, parMoves, _threshold2Star)` (3-arg, shared utility per ADR-0012).
2. ADR-0006 line 339 — delete the "owned by LC UI / LP receives it from LC UI" sentence. Replace with: "Star rating uses the shared `StarRatingCalculator.Compute()` utility (ADR-0012). LP computes stars from the GSM `OnLevelComplete` payload."

---

## Stale Documentation (Not Blocking)

### ⚠️ `docs/architecture/architecture.md` is severely out of date

| Location | Says | Reality |
|---|---|---|
| Line 13 | "ADRs Referenced: None yet — 11 required" | 13 ADRs Accepted |
| Line 473 | "Existing ADRs: None. TR Registry is empty." | 13 ADRs; 72 active TRs |
| Lines 475–479 | Coverage table: 56 / 0 / 56 (100% GAP) | 72 / 72 / 0 (100% covered) |
| Lines 487–511 | "Required ADRs" lists 11 to be written | All Accepted |
| Lines 587–595 | Open Questions: DOTween, MainThreadDispatcher, VFX Graph, etc. | All resolved in ADRs 0003 / 0009 / 0010 |
| Top metadata | Last Updated: 2026-05-01 | 11 days stale |

This is a documentation hygiene item — code/architecture work is unaffected, but the master architecture summary misleads new contributors.

### ⚠️ TR Registry — TR-GSM-011 ADR field

- Current value: `"ADR-0003 (partial — SP write mechanism; GSM contract for what fields serialize is undocumented)"`
- Reality: ADR-0006 lines 202–274 (SER-01/02/03) now explicitly document the GSM serialization fields, the seqId increment on restore, and the `OnSessionLoadFailed` failure path.
- **Update to:** `"ADR-0006, ADR-0003"` and clear the "partial" parenthetical.

---

## Informational (Minor — Documentation Polish)

### ℹ️ ADR-0009 attributes SortMechanic events to GSM

ADR-0009 line 138 comment ("Called by GSM event subscription") and lines 245–248 ("Subscriptions in Awake(): GSM.OnMoveCommitted, GSM.OnMoveRejected, GSM.OnMoveCancelled") are mislabeled. Per ADR-0002 lines 197–203, `OnMoveCommitted` / `OnMoveCancelled` / `OnMoveRejected` are **SortMechanic** events. Implementation intent is clear; the labels are wrong.

### ℹ️ ADR-0009 line 205 — orphan LCUI subscription claim

"AnimationSystem emits `OnAnimationComplete(_activeSequenceId)` to signal that LevelCompleteUI's next-level button may be enabled." LCUI does not subscribe to `OnAnimationComplete` per ADR-0012 (lines 138–141). LCUI's button-enable flow runs through its own FSM transitions. The ADR-0009 sentence is stale.

---

## Engine Compatibility — PASS

- 13 / 13 ADRs have Engine Compatibility sections
- Zero references to deprecated APIs (verified against `deprecated-apis.md`)
- Zero references to removed APIs (`SetupRenderPasses`, `enableRenderCompatibilityMode`, `FindObjectsOfType`, legacy `Input`, `UI.Text`)
- Post-cutoff APIs documented per-ADR with correct usage:

| ADR | Post-Cutoff API | Status |
|---|---|---|
| ADR-0001 | `FindFirstObjectByType<T>()` (dev bootstrap only) | ✓ |
| ADR-0003 | `Awaitable.BackgroundThreadAsync()` / `Awaitable.MainThreadAsync()`; `destroyCancellationToken` | ✓ |
| ADR-0004 | `Addressables.LoadAssetsAsync<T>` 3-arg form (2.x) | ✓ |
| ADR-0005 | `ScriptableRendererFeature.AddRenderPasses()` + `RecordRenderGraph()` | ✓ |
| ADR-0007 | `EnhancedTouchSupport`, `Touch.activeTouches`, `Keyboard.current.escapeKey` | ✓ |
| ADR-0010 | `FrameTimingManager.CaptureFrameTimings()` / `GetLatestTimings()`; `VisualEffect.SetFloat()` | ✓ |

**Pending in-engine verifications** (scoped to pre-sprint device tests, not blocking gate):
- ADR-0003: `File.Replace` atomicity on Android 11+ FUSE; iOS post-reboot retry
- ADR-0004: `JsonUtility` round-trip of nested `ColorStack[]` on IL2CPP
- ADR-0005: On-Tile Post Processing in Unity 6.3 2D Renderer editor; bloom intensity post-tonemapping recalibration
- ADR-0007: `EnhancedTouchSupport` on physical devices; Android 13+ back gesture routing
- ADR-0010: VFX Graph sorting in URP 2D; `FrameTimingManager` availability

### Engine Specialist Findings

No new specialist consultation needed for this review pass — the prior Unity-specialist findings from 2026-05-03 are all closed in the current ADR set. The two remaining conflicts (Conflict 1, Conflict 2) are documentation/contract issues internal to the architecture set; they do not implicate engine-version risk.

---

## ADR Dependency Order (Topologically Sorted)

No cycles. All ADRs `Accepted`.

```
Foundation (no deps):
  1. ADR-0001 — Singleton Architecture and Boot Sequence

Depends on 0001:
  2. ADR-0002 — Event and Signal Architecture
  3. ADR-0003 — Save System Design
  4. ADR-0005 — Rendering Pipeline Configuration
  5. ADR-0011 — Audio Architecture

Depends on 0001 + 0002:
  6. ADR-0004 — Level Data Loading Strategy

Depends on 0001 + 0002 + 0004:
  7. ADR-0006 — Board State Representation

Depends on 0001 + 0006:
  8. ADR-0007 — Input Handling Strategy

Depends on 0001 + 0007:
  9. ADR-0008 — UI Hierarchy and Safe Area

Depends on 0001 + 0002 + 0005 + 0006:
  10. ADR-0009 — Bolt Animation Strategy

Depends on 0005 + 0009:
  11. ADR-0010 — VFX Graph and Bloom on Mobile

Depends on 0002 + 0006 + 0008:
  12. ADR-0012 — HUD and Level Complete UI Business Logic

Depends on 0008 + 0004:
  13. ADR-0013 — Level Layout Column Cap
```

---

## GDD Revision Flags

**None.** All GDD assumptions are consistent with verified Unity 6.3 behaviour and Accepted ADR contracts. The Unity 6.3 reality (HDR pipeline, Render Graph, `SerializeField` restriction, etc.) is correctly captured in the engine reference docs and reflected in every ADR.

---

## Architecture Document Coverage

`docs/architecture/architecture.md` exists and predates the ADR set (last updated 2026-05-01). It captures the system layer map, singleton boot sequence, module ownership map, data flow diagrams, and API boundaries — which remain factually correct. However, its **ADR Audit**, **Required ADRs**, **TR Coverage**, and **Open Questions** sections are stale (see Stale Documentation above). The layer/ownership content is still valid as a reference; only the audit/status sections need a refresh.

**Orphaned architecture:** None. Every system in the architecture's layer map corresponds to a GDD or is explicitly marked as Beta/Launch scope (HintSystem, SkinSystem, RewardedAdSystem, etc.) consistent with the systems index.

---

## Summary Table

| Severity | Count | Items |
|---|---|---|
| 🔴 Conflict (must resolve before story creation for affected systems) | 2 | `OnLevelUnloaded` undeclared; ADR-0006 star rating contradiction |
| ⚠️ Stale (should refresh) | 2 | architecture.md severely outdated; TR-GSM-011 ADR field |
| ℹ️ Minor (documentation polish) | 2 | ADR-0009 GSM/SortMechanic event mislabeling; orphan LCUI `OnAnimationComplete` claim |

This is a stronger state than the prior review: all foundation-layer blockers from 2026-05-03 are closed, all 13 ADRs are Accepted, and traceability is 100%. The remaining 🔴 conflicts are localized — they block specific stories (AnimationSystem VFX cleanup; LP star rating) but not the gate as a whole.

---

## Required Actions

| # | Action | ADR(s) | Effort |
|---|---|---|---|
| 1 | Add `event Action<int> OnLevelUnloaded` to ADR-0006 GSM events + `IGameStateManager` interface; add to ADR-0002 event catalog | ADR-0002, ADR-0006 | 5 min |
| 2 | Fix ADR-0006 line 327: `ComputeStarRating(moveCount, parMoves)` → `StarRatingCalculator.Compute(moveCount, parMoves, _threshold2Star)`. Delete contradictory sentence at line 339. | ADR-0006 | 5 min |
| 3 | Update TR-GSM-011 `adr` field from `"ADR-0003 (partial — ...)"` to `"ADR-0006, ADR-0003"` | tr-registry.yaml | 1 min (applied in this run) |
| 4 | Refresh `architecture.md` — ADR count, TR count (72/72), strike resolved Open Questions, refresh ADR Audit section | architecture.md | 15 min |
| 5 | *(Polish, optional)* Fix ADR-0009 line 138 + 245–248 (GSM → SortMechanic relabel); remove ADR-0009 line 205 (orphan LCUI claim) | ADR-0009 | 5 min |

---

## Gate Guidance

None of the 🔴 conflicts block `/gate-check pre-production` as a phase gate. They block individual story files that reference the affected contracts:

- Action #1 must precede any **AnimationSystem VFX teardown** story.
- Action #2 must precede any **LevelProgression star-rating / completion** story.
- Action #4 (architecture.md refresh) can run asynchronously — pure documentation.

Recommended: complete actions #1, #2, #3 before `/create-stories`. Re-run `/architecture-review` after the fixes to confirm a clean PASS verdict.

---

## Next Steps

1. **Apply action #1** — small edit across ADR-0002 + ADR-0006 to declare `OnLevelUnloaded`
2. **Apply action #2** — fix the ADR-0006 star rating contradiction
3. **Apply action #4** — refresh `architecture.md` (separate session — defer if needed)
4. Re-run `/architecture-review` to confirm verdict moves to **PASS**
5. Run `/gate-check pre-production` to advance to the Production stage
