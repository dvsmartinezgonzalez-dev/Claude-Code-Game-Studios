# Architecture Review Report — BoltSort

> **Generated:** 2026-05-03 | `/architecture-review full`
> **Supersedes:** Architecture Review 2026-05-02

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP 17.x, 2D Renderer) |
| **GDDs Reviewed** | 11 |
| **ADRs Reviewed** | 12 (ADR-0001 – ADR-0012, all status: Proposed) |
| **Engine Specialist** | unity-specialist (second-pass audit) |

---

## Verdict: CONCERNS

**Not FAIL** — No foundation-layer requirements are uncovered; all 12 core architectural decisions have ADRs.

**Not PASS** — Two real conflicts (event signature staleness, tier threshold mismatch), one explicit coverage gap (TR-SORT-010), one compile error (ADR-0010 `GpuTimingProbe`), and one hang risk (ADR-0004 failure path).

---

## Traceability Summary

| Metric | Count |
|--------|-------|
| Total requirements (TR registry after this run) | 72 |
| ✅ Covered | 69 |
| ⚠️ Partial | 2 |
| ❌ Gap | 1 |

*2 new TR-IDs registered during this review (TR-GSM-011, TR-LDS-004) for requirements present in GDDs but not previously indexed.*

---

## Coverage Gaps

### ❌ TR-SORT-010: Column cap `color_count + temp_slot_count ≤ 8`
- **GDD:** sort-mechanic.md → Tuning Knobs ("hard design rule — Column cap constraint")
- **Domain:** UI/Layout + Level Authoring
- **Status in registry:** Active, flagged UNCOVERED since 2026-05-02
- **Engine Risk:** LOW
- **Suggested ADR:** `/architecture-decision level-layout-column-cap` — establishes `color_count + temp_slot_count ≤ 8` as a hard authoring constraint enforced at Level Progression, with ADR-0008's 44pt/48dp tap-target constraint as the rationale. No runtime gate needed; LDS authoring pipeline is the enforcement point.

### ⚠️ TR-GSM-011: GSM serializes board state to SP on app backgrounding
- **GDD:** game-state-manager.md → SER-01/SER-02/SER-03
- **Coverage:** ADR-0003 (Save System) addresses the SP write mechanism; no ADR explicitly addresses *what GSM serializes* and *when*, or the SER-02 foreground restore sequence ID increment.
- **Required ADR action:** ADR-0006 should be updated to document the GSM serialization contract (SER-01 field list) or a new section added to ADR-0003.

### ⚠️ TR-LDS-004: LDS DEGRADED state when `failure_ratio > 0.20`
- **GDD:** level-data-system.md → Catalogue Health Threshold formula
- **Coverage:** ADR-0004 addresses the loading strategy but does not define the DEGRADED state transition or the 20% threshold as an implementation contract. The related blocking risk (no recovery path in `OnAllLevelsLoaded` failure branch) is documented in Blocking Issues below.
- **Required ADR action:** ADR-0004 failure branch must implement DEGRADED state per the GDD formula.

---

## Cross-ADR Conflicts

### 🔴 CONFLICT 1: `OnLevelComplete` Event Signature Mismatch
**Type:** Integration contract
**Documents involved:** ADR-0002, ADR-0006 vs ADR-0012

| Document | Declared signature |
|----------|-------------------|
| ADR-0002 event catalog | `event Action<int, int> OnLevelComplete; // (levelId, moveCount)` |
| ADR-0006 `IGameStateManager` | `event Action<int, int> OnLevelComplete; // (levelId, moveCount)` |
| **ADR-0012 (canonical)** | `event Action<int, int, int, int> OnLevelComplete; // (levelId, moveCount, parMoves, sequenceId)` |

**Impact:** Developers implementing GSM using ADR-0002 or ADR-0006 as reference will build a 2-arg signature that breaks InGameHUD and LevelCompleteUI. TR-HUD-003, TR-LCUI-002, TR-LCUI-005, TR-LCUI-006 all depend on the 4-arg payload.

**Resolution:**
1. Accept ADR-0012 as authoritative for this payload (already its stated intent)
2. Update ADR-0002 Complete Event Catalog and ADR-0006 `IGameStateManager` interface to 4-arg signature
3. No logic change — ADR-0012 already resolves the underlying inconsistency

---

### 🔴 CONFLICT 2: Quality Tier Detection Thresholds
**Type:** Architecture contradicts authoritative GDD
**Documents involved:** ADR-0005 vs quality-tier-system.md (TR-QTS-001)

| Parameter | ADR-0005 | QTS GDD (authoritative) |
|-----------|----------|------------------------|
| GPU memory → Low | < 1024 MB | < **512 MB** |
| GPU memory → Medium | 1024–2047 MB | **512–1535 MB** |
| GPU memory → High | ≥ 2048 MB | ≥ **1536 MB** |
| Shader level → High | ≥ 45 | ≥ **46** |

**Impact:** ADR-0005 thresholds misclassify a large segment of mid-range Android devices (e.g., 1200 MB device is Medium per GDD but Low per ADR). The shader level off-by-one shifts the Medium/High boundary. Device classification errors are invisible in CI and only surface in device QA.

**Resolution:** Update ADR-0005 tier decision rule to match QTS GDD thresholds. GDD is the single source of truth.

---

### 🟡 CONFLICT 3: SaveSystem Cold-Start Architecture Description
**Type:** Stale description in ADR-0001
**Documents involved:** ADR-0001 vs ADR-0003

| Document | Says |
|----------|------|
| ADR-0001 boot table | SaveSystem "Launches background `Thread` for file read" |
| **ADR-0003 (authoritative)** | Synchronous read in Awake(); `async void Awake()` is forbidden |

**Impact:** Not a runtime bug. A developer reading ADR-0001 will expect a background thread and may implement the wrong initialization pattern.

**Resolution:** Update ADR-0001 SaveSystem boot row: "Reads save.json synchronously in Awake(); `IsReady = true` set before any `Start()` runs."

---

### 🟡 CONFLICT 4: VFX Density Multiplier — Global vs Per-Instance
**Type:** Terminology inconsistency
**Documents involved:** ADR-0005 vs ADR-0010

| Document | Says |
|----------|------|
| ADR-0005 Performance section | "VFX Graph density multiplier is a **global float property** — zero memory overhead" |
| **ADR-0010 (authoritative)** | "No `VFXManager.SetGlobalFloat()` — this API does not exist in Unity 6.x" |

**Impact:** Developer searching for `VFXManager.SetGlobalFloat()` based on ADR-0005 language hits a compile error. Actual implementation is per-instance `VisualEffect.SetFloat()` (O(N), N ≤ 11 — negligible but not zero).

**Resolution:** Update ADR-0005 Performance section to reflect per-instance calls.

---

## Engine Specialist Findings (Unity Specialist)

Additional issues identified by the unity-specialist second-pass audit:

### ADR-0003 — W-2 missing `catch(OperationCanceledException)` ⚠️ HIGH
`_writeLock.Wait(destroyCancellationToken)` in `OnApplicationPause` has no `catch(OperationCanceledException)`. If the DDOL MonoBehaviour is destroyed mid-pause, this throws uncaught on the main thread. GDD AC-42 explicitly requires silent catch. ADR code snippet contradicts its own governing GDD.

**Fix:** Wrap semaphore wait in `try/catch(OperationCanceledException)` → silently preserve dirty flag.

### ADR-0003 — iOS retry thread join timing ⚠️ MEDIUM
Cold-start retry loop description doesn't explicitly require `Awake()` to join the background thread before setting `IsReady = true`. An implementation that doesn't join will fire `OnSaveReady` before the retry completes — a race on post-reboot iOS devices.

**Fix:** Add explicit note: "`Awake()` must join the background thread (or complete retry inline) before setting `IsReady = true`."

### ADR-0004 — No recovery path in failure branch 🔴 BLOCKING
`OnAllLevelsLoaded` failure branch logs an error and never sets `IsReady` or emits any event. Systems waiting on `OnLevelDataReady` (LevelProgression, GSM) hang indefinitely. The GDD specifies a DEGRADED state with `error_code = CATALOGUE_LOAD_FAILED`.

**Fix:** Failure branch must enter DEGRADED state: set `IsReady = true` with `loaded_count = 0` and emit a failure diagnostic so LevelProgression can handle gracefully.

### ADR-0007 — `Physics2D.OverlapPoint` code snippet missing layer mask ⚠️ MEDIUM
Prose mandates a `BoltStacks` layer mask; code snippet calls `Physics2D.OverlapPoint(worldPos)` without one. Unmasked call silently picks up unintended colliders.

**Fix:** Update snippet: `Physics2D.OverlapPoint(worldPos, _boltStacksLayerMask)` where `_boltStacksLayerMask = LayerMask.GetMask("BoltStacks")` cached in `Awake()`.

### ADR-0010 — `GpuTimingProbe` is not a real Unity API 🔴 BLOCKING
`GpuTimingProbe.GetLastFrameGpuMs()` does not exist in Unity's public surface — will not compile. The correct Unity 6.x API is `FrameTimingManager.GetLatestTimings()` with `FrameTiming.gpuFrameTime`.

**Fix:** Replace `GpuTimingProbe` with `FrameTimingManager` wrapper, or explicitly document it as a custom class that must be authored with `FrameTimingManager` as the underlying API.

### ADR-0010 — `_activeVFXInstances` list lifecycle undefined ⚠️ MEDIUM
`_activeVFXInstances` list is used in code but Add/Remove/Clear lifecycle is not defined in ADR-0010 or cross-referenced to ADR-0009.

**Fix:** Add lifecycle note: instances added in `HandleLevelLoaded()`, cleared in `HandleLevelUnloaded()`. Cross-reference ADR-0009 bolt visual lifecycle.

---

## ADR Dependency Order (Topological)

No dependency cycles detected.

```
Foundation (no dependencies):
  1. ADR-0001 — Singleton Architecture and Boot Sequence
  2. ADR-0002 — Event and Signal Architecture
  3. ADR-0003 — Save System Design
  4. ADR-0005 — Rendering Pipeline Configuration
  5. ADR-0011 — Audio Architecture

Depends on Foundation:
  6. ADR-0004 — Level Data Loading Strategy    (requires 0001, 0002)
  7. ADR-0006 — Board State Representation     (requires 0001, 0002, 0004)
  8. ADR-0007 — Input Handling Strategy        (requires 0001, 0006)
  9. ADR-0008 — UI Hierarchy and Safe Area     (requires 0001, 0007)
  10. ADR-0009 — Bolt Animation Strategy       (requires 0001, 0002, 0005, 0006)

Feature layer:
  11. ADR-0010 — VFX Graph and Bloom on Mobile (requires 0005, 0009)
  12. ADR-0012 — HUD and Level Complete UI     (requires 0002, 0006, 0008)

Required new ADR:
  13. ADR-0013 — Level Layout Column Cap       (governs TR-SORT-010)
```

**⚠️ ADR-0001 incomplete:** ADR-0004 assigns LevelDataSystem SEO −95 but ADR-0001's boot table does not include it. Update ADR-0001 to add LDS at SEO −95 (between QTS −100 and SaveSystem −90).

**⚠️ All 12 ADRs are `Proposed`:** Per coordination rules, implementation stories referencing a Proposed ADR are auto-blocked. All ADRs must be promoted to `Accepted` before any sprint begins.

---

## GDD Revision Flags

| GDD | Assumption | Reality (from ADR) | Action |
|-----|-----------|---------------------|--------|
| quality-tier-system.md | GPU thresholds: Low < 512 MB, High ≥ 1536 MB; shader High ≥ 46 | ADR-0005 uses different values (1024/2048 MB; shader ≥ 45) | Update ADR-0005 to match GDD. GDD is authoritative. No GDD revision needed. |

No other GDD revision flags. All other GDD assumptions are consistent with Unity 6.3 verified behaviour.

---

## Engine Compatibility Summary

**Deprecated API References in ADRs:** None found.

| Breaking change | Addressed in |
|----------------|-------------|
| `FindObjectsOfType` removed | ADR-0001 (`FindFirstObjectByType` for dev bootstrap only) |
| `SetupRenderPasses` removed | ADR-0005 (Render Graph API throughout) |
| `[SerializeField]` on properties | ADR-0001, ADR-0008 (backing field requirement) |
| URP Compatibility Mode removed | ADR-0005 (`AddRenderPasses` + `RecordRenderGraph`) |
| Legacy `Input` class deprecated | ADR-0007 (Input System Package) |

**ADRs with Engine Compatibility sections:** 12 / 12 ✓

**Pending in-engine verifications (by sprint):**
- Before Save sprint: `File.Replace` atomicity on Android 11+ FUSE path (ADR-0003)
- Before LDS sprint: `JsonUtility` round-trip with nested `ColorStack[]` on IL2CPP (ADR-0004)
- Before Rendering sprint: On-Tile Post Processing option in Unity 6.3 2D Renderer editor; bloom intensity calibration post-tonemapping change (ADR-0005)
- Before Sort Mechanic sprint: `EnhancedTouchSupport` on physical devices; Android 13+ back gesture routing (ADR-0007)
- Before Animation sprint: VFX Graph sorting vs sprites in URP 2D Renderer; `GlowOverlay` HDR trigger; `FrameTimingManager` API availability (ADR-0010)

---

## Blocking Issues (resolve before implementation sprints)

| # | Issue | ADR(s) | Severity |
|---|-------|---------|---------|
| 1 | All 12 ADRs are `Proposed` — stories auto-blocked | All | BLOCKING |
| 2 | ADR-0004: No recovery path in `OnAllLevelsLoaded` failure → systems hang | ADR-0004 | BLOCKING |
| 3 | ADR-0010: `GpuTimingProbe` is not a real Unity API — compile error | ADR-0010 | BLOCKING |
| 4 | ADR-0002 / ADR-0006: Stale 2-arg `OnLevelComplete` (canonical is 4-arg per ADR-0012) | ADR-0002, ADR-0006 | HIGH |
| 5 | ADR-0005: Tier detection thresholds contradict QTS GDD / TR-QTS-001 | ADR-0005 | HIGH |
| 6 | ADR-0003 W-2: Missing `catch(OperationCanceledException)` on main-thread semaphore | ADR-0003 | HIGH |
| 7 | TR-SORT-010: Column cap ≤ 8 has no governing ADR | — | MEDIUM |
| 8 | ADR-0007: `Physics2D.OverlapPoint` code missing layer mask | ADR-0007 | MEDIUM |
| 9 | ADR-0001: Boot table missing LevelDataSystem at SEO −95 | ADR-0001 | MEDIUM |
| 10 | ADR-0010: `_activeVFXInstances` lifecycle undefined | ADR-0010 | MEDIUM |

---

## Required New ADR

`/architecture-decision level-layout-column-cap` (ADR-0013)
- Governs: TR-SORT-010
- Scope: Establishes `color_count + temp_slot_count ≤ 8` as a hard authoring constraint. Rationale: 44pt/48dp tap-target minimum (ADR-0008) on a 375pt wide viewport (iPhone SE floor) permits at most 8 columns. Enforcement: Level Progression authoring rules + LDS authoring pipeline `parMoves` solver. No runtime code gate needed.
- Layer: Foundation / Design constraint
- Effort: 1 session (short ADR)

---

## Immediate Actions (top 3, highest-impact first)

1. **Fix ADR-0004 failure path** — DEGRADED state from the GDD must be expressed in the ADR code contract. A missing Addressables bundle in production freezes the entire boot sequence with no recovery visible to the player.

2. **Fix ADR-0005 QTS thresholds** — Align GPU memory and shader level thresholds to QTS GDD values before any device QA begins. Implementation errors here are invisible in CI.

3. **Update ADR-0002 and ADR-0006 `OnLevelComplete` signature** — Promote the 4-arg ADR-0012 payload before any developer starts on GSM or HUD stories.

---

## Gate Guidance

When all blocking issues are resolved, run `/gate-check pre-production` to advance.
Re-run `/architecture-review` after each ADR fix to verify coverage improvement.
