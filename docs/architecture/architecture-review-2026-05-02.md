# Architecture Review — BoltSort

## Document Status

| Field | Value |
|---|---|
| **Date** | 2026-05-02 |
| **Engine** | Unity 6.3 LTS |
| **GDDs Reviewed** | 11 (sort-mechanic, game-state-manager, level-data-system, animation-system, audio-system, in-game-hud, level-complete-ui, save-persistence, coin-economy, quality-tier-system, level-progression) |
| **ADRs Reviewed** | 11 (ADR-0001 through ADR-0011, all Proposed) |
| **TR Registry** | Populated this run (56 entries) |
| **Verdict** | **CONCERNS** |

---

## Traceability Summary

| Status | Count |
|---|---|
| ✅ Covered | 47 |
| ⚠️ Partial | 4 |
| ❌ Gap | 5 |
| **Total** | **56** |

---

## Full Traceability Matrix

| TR-ID | GDD | System | Requirement | ADR Coverage | Status |
|---|---|---|---|---|---|
| TR-SORT-001 | sort-mechanic | SortMechanic | State machine: IDLE/BOLT_SELECTED/MOVE_EXECUTING/WIN/CANCELLATION/INVALID_MOVE | ADR-0006 | ✅ |
| TR-SORT-002 | sort-mechanic | SortMechanic | Move validation: empty accepts any, capacity gate, color match | ADR-0006 | ✅ |
| TR-SORT-003 | sort-mechanic | SortMechanic | Win condition: all color stacks full + monochromatic | ADR-0006 | ✅ |
| TR-SORT-004 | sort-mechanic | SortMechanic | One-tap buffer during MOVE_EXECUTING; discard on WIN | ADR-0007 | ✅ |
| TR-SORT-005 | sort-mechanic | SortMechanic | Shallow deadlock check (depth-1) → emit deadlock_detected | ADR-0006 | ✅ |
| TR-SORT-006 | sort-mechanic | SortMechanic | Sequence ID stale-signal guard on animation_complete | ADR-0002 | ✅ |
| TR-SORT-007 | sort-mechanic | SortMechanic | move_executing_exited on IDLE exit only (not WIN, not watchdog) | ADR-0002 | ✅ |
| TR-SORT-008 | sort-mechanic | SortMechanic | Android back gesture → cancellation in BOLT_SELECTED | ADR-0007 | ✅ |
| TR-SORT-009 | sort-mechanic | SortMechanic | Synchronous pull-on-demand read of board state from GSM | ADR-0006, ADR-0007 | ✅ |
| TR-SORT-010 | sort-mechanic | SortMechanic | Column cap: color_count + temp_slot_count ≤ 8 | — | ❌ GAP |
| TR-GSM-001 | game-state-manager | GameStateManager | Sole owner of board state arrays | ADR-0006 | ✅ |
| TR-GSM-002 | game-state-manager | GameStateManager | Monotonic sequence ID, never decrements | ADR-0006 | ✅ |
| TR-GSM-003 | game-state-manager | GameStateManager | Unlimited undo stack; frozen on COMPLETE | ADR-0006 | ✅ |
| TR-GSM-004 | game-state-manager | GameStateManager | Watchdog 1500ms → board_refresh_forced | ADR-0006 | ✅ |
| TR-GSM-005 | game-state-manager | GameStateManager | Atomic board mutation (5 steps synchronous) | ADR-0006 | ✅ |
| TR-GSM-006 | game-state-manager | GameStateManager | Deferred undo on MOVE_EXECUTING | ADR-0006 | ✅ |
| TR-GSM-007 | game-state-manager | GameStateManager | bolt_count_invariant check at level load | ADR-0006 | ✅ |
| TR-GSM-008 | game-state-manager | GameStateManager | Level lifecycle FSM: UNLOADED/LOADING/ACTIVE/COMPLETE/TEARDOWN | ADR-0001, ADR-0006 | ✅ |
| TR-GSM-009 | game-state-manager | GameStateManager | Emit typed C# events | ADR-0002 | ✅ |
| TR-LDS-001 | level-data-system | LevelDataSystem | Level record schema | ADR-0004 | ✅ |
| TR-LDS-002 | level-data-system | LevelDataSystem | bolt_count_invariant at authoring time | ADR-0004 | ✅ |
| TR-LDS-003 | level-data-system | LevelDataSystem | System readiness query before load | ADR-0001, ADR-0004 | ✅ |
| TR-ANIM-001 | animation-system | AnimationSystem | Bolt lift arc + travel + settle (80+80-300+70ms) | ADR-0009 | ✅ |
| TR-ANIM-002 | animation-system | AnimationSystem | Stack completion glow + VFX ring + sparks | ADR-0009, ADR-0010 | ✅ |
| TR-ANIM-003 | animation-system | AnimationSystem | Snap bolt visuals on board_state_changed, board_refresh_forced | ADR-0009 | ✅ |
| TR-ANIM-004 | animation-system | AnimationSystem | Level complete celebration ~1600–2020ms | ADR-0009 | ✅ |
| TR-ANIM-005 | animation-system | AnimationSystem | quality_density_multiplier from QTS at level_loaded | ADR-0005, ADR-0010 | ✅ |
| TR-ANIM-006 | animation-system | AnimationSystem | Emit animation_complete(seqId) | ADR-0002 | ✅ |
| TR-ANIM-007 | animation-system | AnimationSystem | Must emit within watchdog_timeout_ms 1500ms | ADR-0009 | ✅ |
| TR-ANIM-008 | animation-system | AnimationSystem | Rejection shake 100ms; no animation_complete | ADR-0009 | ✅ |
| TR-AUDIO-001 | audio-system | AudioSystem | Three AudioBus groups: SFX, Ambient, UI | ADR-0011 | ✅ |
| TR-AUDIO-002 | audio-system | AudioSystem | AudioMixer.SetFloat for volume control | ADR-0011 | ✅ |
| TR-AUDIO-003 | audio-system | AudioSystem | PlayerPrefs audio keys read on Awake | ADR-0011 | ✅ |
| TR-AUDIO-004 | audio-system | AudioSystem | PlayBoltSettle(bool) — AnimationSystem sole caller | ADR-0011 | ✅ |
| TR-AUDIO-005 | audio-system | AudioSystem | Machine ambient hum loop | ADR-0011 | ✅ |
| TR-AUDIO-006 | audio-system | AudioSystem | Pooled AudioSource (8 sources) for concurrent SFX | ADR-0011 | ✅ |
| TR-HUD-001 | in-game-hud | InGameHUD | UGUI Canvas, Screen Space-Overlay, Screen.safeArea | ADR-0008 | ✅ |
| TR-HUD-002 | in-game-hud | InGameHUD | Move counter subscribes to GSM OnBoardStateChanged | ADR-0008 | ✅ |
| TR-HUD-003 | in-game-hud | InGameHUD | Undo button: optimistic lock, re-enable on animation_complete | ADR-0008 | ⚠️ Partial — ADR-0008 GDD-Requirements section omits this; covered implicitly via ADR-0009 OnAnimationComplete contract |
| TR-HUD-004 | in-game-hud | InGameHUD | Hint button: disabled if balance < 50 or MOVE_EXECUTING | ADR-0008 | ⚠️ Partial — not in ADR-0008's explicit GDD-Requirements; implied by module ownership in architecture.md |
| TR-HUD-005 | in-game-hud | InGameHUD | Coin display: subscribes to OnCoinBalanceChanged | ADR-0002, ADR-0008 | ⚠️ Partial — event pattern covered by ADR-0002 but ADR-0008 doesn't explicitly trace this requirement |
| TR-HUD-006 | in-game-hud | InGameHUD | Pity grant counter (5 consecutive 0-star → AddCoins(50)) | — | ❌ GAP |
| TR-HUD-007 | in-game-hud | InGameHUD | Emit UndoRequested to GSM | ADR-0002 | ✅ |
| TR-LCUI-001 | level-complete-ui | LevelCompleteUI | StarRating(move_count, par_moves) | ADR-0008 | ✅ |
| TR-LCUI-002 | level-complete-ui | LevelCompleteUI | Coin animation; coin_reward_per_star=[0,10,20,40] | — | ❌ GAP |
| TR-LCUI-003 | level-complete-ui | LevelCompleteUI | Ad FSM + 30s watchdog | — | ❌ GAP |
| TR-SP-001 | save-persistence | SaveSystem | JSON file; atomic via File.Move / File.Replace | ADR-0003 | ✅ |
| TR-SP-002 | save-persistence | SaveSystem | Fields: current_level_id, completion_record[], coin_balance | ADR-0003 | ✅ |
| TR-SP-003 | save-persistence | SaveSystem | WriteCompletionAtomic(...) | ADR-0003 | ✅ |
| TR-SP-004 | save-persistence | SaveSystem | IsReady + OnSaveReady; subscribe-then-check | ADR-0001, ADR-0003 | ✅ |
| TR-SP-005 | save-persistence | SaveSystem | PlayerPrefs for audio prefs | ADR-0003 | ✅ |
| TR-SP-006 | save-persistence | SaveSystem | Save migration versioning | ADR-0003 | ✅ |
| TR-SP-007 | save-persistence | SaveSystem | iOS file protection; cold-start retry | ADR-0003 | ✅ |
| TR-SP-008 | save-persistence | SaveSystem | Background Thread for file I/O (W-1 only; cold-start is synchronous) | ADR-0003 | ✅ |
| TR-CE-001 | coin-economy | CoinEconomy | CE FSM: LOADING/READY | ADR-0001 | ✅ |
| TR-CE-002 | coin-economy | CoinEconomy | AddCoins with idempotency guard | ADR-0006 | ✅ |
| TR-CE-003 | coin-economy | CoinEconomy | SpendCoins; floor=0 | ADR-0006 | ✅ |
| TR-CE-004 | coin-economy | CoinEconomy | Starter grant 150 on first install | ADR-0006 | ⚠️ Partial — correctly addressed in ADR-0006 (CE section); architecture.md mapping incorrectly says ADR-3 |
| TR-QTS-001 | quality-tier-system | QualityTierSystem | Detect tier at startup: GPU memory, shader level, Android Perf Class | ADR-0005 | ✅ |
| TR-QTS-002 | quality-tier-system | QualityTierSystem | Apply density_multiplier + framerate before first scene | ADR-0001, ADR-0005 | ✅ |
| TR-LP-001 | level-progression | LevelProgression | is_locked = (levelId > currentLevelId) | ADR-0006 | ✅ |
| TR-LP-002 | level-progression | LevelProgression | best_stars = max(current, earned) | ADR-0006 | ✅ |
| TR-LP-003 | level-progression | LevelProgression | Emit LevelCompleted(stars, levelId, moveCount, parMoves) | ADR-0002 | ✅ |

---

## Coverage Gaps (no ADR covers these)

```
❌ TR-SORT-010: sort-mechanic → SortMechanic → Column cap: color_count + temp_slot_count ≤ 8
   Suggested ADR: Extend ADR-0008 (UI layout rule) or ADR-0006 (GSM validation)
   Domain: UI/Layout  Engine Risk: LOW

❌ TR-HUD-006: in-game-hud → InGameHUD → Pity grant counter (5×0-star → AddCoins(50))
   Suggested ADR: ADR-0012 "HUD and LevelCompleteUI Business Logic"
   Domain: Gameplay/CE  Engine Risk: LOW

❌ TR-LCUI-002: level-complete-ui → LevelCompleteUI → Coin animation; coin_reward_per_star=[0,10,20,40]
   Suggested ADR: ADR-0012 "HUD and LevelCompleteUI Business Logic"
   Domain: Economy/UI  Engine Risk: LOW

❌ TR-LCUI-003: level-complete-ui → LevelCompleteUI → Ad FSM + 30s watchdog
   Suggested ADR: ADR-0012 "HUD and LevelCompleteUI Business Logic"
   Domain: Platform/Ad  Engine Risk: MEDIUM (AdMob SDK integration)
```

---

## Cross-ADR Conflicts

### 🔴 CONFLICT-1: ADR-0001 ↔ ADR-0004 — Singleton Boot Table Incomplete

**Type**: Integration contract

ADR-0001 claims: 6 manager MonoBehaviours at SEO −100…−30; LevelDataSystem is absent from the table.
ADR-0004 claims: LevelDataSystem is a DDOL singleton at SEO −95 (between QTS at −100 and SaveSystem at −90).

**Impact**: Implementation team has no authoritative SEO slot for LDS in ADR-0001; two ADRs disagree on the complete list of DDOL singletons.

**Resolution options**:
1. Add LDS at SEO −95 to ADR-0001's manager table; update architecture.md boot table to match.
2. Have ADR-0004 amend ADR-0001 explicitly (note in ADR-0004 already references SEO −95; ADR-0001 must accept the amendment).

---

### 🔴 CONFLICT-2: ADR-0001 ↔ ADR-0003 — SaveSystem Read Model

**Type**: Integration contract

ADR-0001 (SEO −90 row) states: "Launches background `Thread` for file read; exposes `IsReady`, `OnSaveReady`".
ADR-0003 states: "The entire cold-start read executes synchronously and blocking within `SaveSystem.Awake()`. `async void Awake()` is forbidden. `IsReady = true` set and `OnSaveReady?.Invoke()` fires at the end of `Awake()`, before any other system's `Start()` runs."
ADR-0003 includes an explicit self-correction note: "Architecture doc correction: architecture.md Flow 2 described a background Thread + MainThreadDispatcher for the cold-start read. The GDD overrides this with a synchronous read."

**Impact**: ADR-0001's SEO −90 description is stale; a developer reading ADR-0001 alone will implement a background thread for the read, producing a broken subscribe-then-check pattern (OnSaveReady fires mid-session, not at Awake end).

**Resolution**: Edit ADR-0001 SEO −90 row to: "Synchronous blocking file read in Awake(); sets `IsReady=true` and fires `OnSaveReady` before lower-SEO Awake() runs. iOS cold-start retry uses background thread for sleep only; main read path is synchronous."

---

### ⚠️ CONFLICT-3: ADR-0003 ↔ ADR-0006 — SetCoinBalance Write Semantics Undefined

**Type**: Integration contract

ADR-0006's CoinEconomy calls `SaveSystem.Instance.SetCoinBalance(_coinBalance)` after every AddCoins/SpendCoins. ADR-0003 exposes `SetCoinBalance(int)` in ISaveSystem but does not define whether this call triggers an immediate write, marks `_isDirty = true` (deferred to W-2), or does something else.

**Impact**: If SetCoinBalance triggers a synchronous main-thread write, every coin spend causes a file I/O stall. If it only sets `_isDirty`, a crash between spend and next W-2 loses the coin delta.

**Resolution**: ADR-0003 should specify: `SetCoinBalance(int)` updates the in-memory balance and sets `_isDirty = true` — no immediate write. The balance is persisted on next W-2 (OnApplicationPause) or next W-1 (WriteCompletionAtomic, which captures full state).

---

### ⚠️ COSMETIC: ADR-0005 — Malformed Multi-Line Comment in Diagrams

The per-instance VFX correction text (`// Per-instance VisualEffect.SetFloat() — no global VFX API exists...`) appears verbatim inside the QualityTierSystem.Awake() code block (line 95–97), the Architecture Diagram (lines 169–172), and the GDD Requirements table (lines 237–239), rendering as broken pseudocode in all three locations.

**Resolution**: Collapse to a single inline note: `// Per-instance: VisualEffect.SetFloat("quality_density_multiplier", value)` on a single line in each location.

---

## ADR Dependency Order (Topologically Sorted)

```
Foundation (no dependencies):
  1. ADR-0001: Singleton Architecture and Boot Sequence

Tier 2 (depends ADR-0001 only):
  2. ADR-0002: Event and Signal Architecture
  3. ADR-0003: Save System Design
  4. ADR-0005: Rendering Pipeline Configuration
  5. ADR-0011: Audio Architecture

Tier 3 (depends ADR-0001 + ADR-0002):
  6. ADR-0004: Level Data Loading Strategy

Core (depends ADR-0001, ADR-0002, ADR-0003, ADR-0004):
  7. ADR-0006: Board State Representation and GSM Design

Input (depends ADR-0001, ADR-0006):
  8. ADR-0007: Input Handling Strategy

UI (depends ADR-0007):
  9. ADR-0008: UI Hierarchy and Safe Area

Animation (depends ADR-0002, ADR-0005, ADR-0006):
  10. ADR-0009: Bolt Animation Strategy

VFX (depends ADR-0005, ADR-0009):
  11. ADR-0010: VFX Graph and Bloom on Mobile
```

✅ No dependency cycles detected.
✅ All "Depends On" references resolve to existing ADRs.
⚠️ All 11 ADRs are `Proposed`. Promote ADRs 1, 2, 3, 5, 11 → `Accepted` first (CONFLICT-1 and CONFLICT-2 must be resolved before promoting ADR-0001; CONFLICT-3 before ADR-0003).

---

## GDD Revision Flags

**None** — all GDD assumptions are consistent with verified Unity 6.3 engine behaviour. Prior design reviews absorbed all engine-truth corrections (Coin Economy 5 passes, Sort Mechanic revision, Level Data camelCase field update).

---

## Engine Compatibility

**ADRs with Engine Compatibility section: 11 / 11** ✅

| Check | Result |
|---|---|
| Deprecated API references | None found — SetupRenderPasses, enableRenderCompatibilityMode, FindObjectsOfType all explicitly forbidden |
| Stale version references | None — all ADRs target Unity 6.3 LTS |
| `[SerializeField]` on properties | Correctly forbidden in ADR-0001, ADR-0002; Unity 6.3 compile error documented |
| URP Render Graph compliance | ADR-0005 mandates RecordRenderGraph; SetupRenderPasses banned |
| Input System Package | ADR-0007 mandates Input System Package (New); legacy Input class banned |
| Addressables 2.x API | ADR-0004 correctly uses 3-arg LoadAssetsAsync form |
| Thread-switch API | ADR-0003 correctly uses Awaitable.BackgroundThreadAsync (Unity 6.0+) |
| VFX Graph density API | ADR-0010 correctly uses per-instance SetFloat (no global API in Unity 6.x) |

---

## Architecture Document Coverage

`architecture.md` covers all 11 MVP systems ✅. Issues that should be corrected in a future `architecture.md` revision:

1. Boot Sequence table missing LevelDataSystem at SEO −95 (CONFLICT-1)
2. Flow 2 (App Boot) describes background-thread read for SaveSystem — superseded by ADR-0003 synchronous model (CONFLICT-2)
3. `TR-CE-004` mapped to ADR-3 — should be ADR-6
4. `## ADR Audit` section is stale: "Existing ADRs: None" — all 11 ADRs now exist, 56 requirements covered

---

## Verdict: CONCERNS

**PASS criteria**: All requirements covered, no conflicts, engine consistent.
**FAIL criteria**: Critical gaps (Foundation/Core uncovered) or blocking cross-ADR conflicts.
**CONCERNS**: Foundation and Core layers are fully covered; 5 presentation-layer gaps exist; 2 documentation conflicts will mislead Sprint 1 implementers.

### Blocking Issues (must resolve before advancing to Accepted ADRs)

1. **CONFLICT-1**: Add LevelDataSystem (SEO −95) to ADR-0001's singleton boot table.
2. **CONFLICT-2**: Update ADR-0001 SEO −90 row to reflect synchronous SaveSystem.Awake() read (per ADR-0003).
3. **CONFLICT-3**: Specify `SetCoinBalance` write semantics in ADR-0003 (`_isDirty=true`, deferred to W-2).

### Required ADRs (priority order)

| Priority | ADR | TRs Covered |
|---|---|---|
| 1 | **ADR-0012** — HUD and LevelCompleteUI Business Logic | TR-HUD-006, TR-LCUI-002, TR-LCUI-003, plus explicit trace for TR-HUD-003/004/005 |
| 2 | Extend ADR-0008 or ADR-0006 to address TR-SORT-010 (column cap validation) | TR-SORT-010 |

---

## Next Steps

1. **Resolve CONFLICT-1 and CONFLICT-2**: Edit ADR-0001 to add LDS and correct the SaveSystem description. These take 5 minutes and unblock Accepted promotion.
2. **Resolve CONFLICT-3**: Add a `SetCoinBalance` semantics paragraph to ADR-0003.
3. **Write ADR-0012**: `/architecture-decision hud-and-level-complete-ui-business-logic` — covers pity grant, coin animation, reward table, ad FSM, plus explicitly traces HUD-003/004/005.
4. **Promote ADRs to Accepted**: After conflicts resolved, promote 1 → 2 → 3 → 4 → 5 → ... in topological order.
5. **Run `/gate-check pre-production`**: After all ADRs Accepted (or after ADR-0012 is written and CONCERNS resolved to PASS).
