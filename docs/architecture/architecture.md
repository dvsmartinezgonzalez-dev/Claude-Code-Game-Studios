# BoltSort — Master Architecture

## Document Status

| Field | Value |
|-------|-------|
| **Version** | 1.0 |
| **Last Updated** | 2026-05-17 |
| **Engine** | Unity 6.3 LTS — URP 2D Renderer |
| **Language** | C# |
| **Target Platforms** | iOS, Android |
| **GDDs Covered** | sort-mechanic, game-state-manager, level-data-system, animation-system, audio-system, in-game-hud, level-complete-ui, save-persistence, coin-economy, quality-tier-system, level-progression |
| **ADRs Referenced** | ADR-0001 through ADR-0013 (13 ADRs, all Accepted — see `docs/architecture/`) |
| **Technical Director Sign-Off** | 2026-05-01 — APPROVED |
| **Lead Programmer Feasibility** | LP-FEASIBILITY skipped — Lean mode |

---

## Engine Knowledge Gap Summary

**Risk Level: HIGH** across three domains. All code reviews must check against `docs/engine-reference/unity/` before suggesting APIs.

| Domain | Change | Impact on BoltSort |
|--------|--------|--------------------|
| URP Render Graph | Compatibility Mode **removed** in Unity 6.3. `SetupRenderPasses` is gone. All custom `ScriptableRendererFeature` must use `AddRenderPasses()` + Render Graph API. | Animation System: any custom bloom/glow render feature must use Render Graph. |
| `[SerializeField]` | Applying to properties or methods is a **compile error** in Unity 6.3. Must use backing fields or `[field: SerializeField]` on auto-properties. | Affects every MonoBehaviour in the project. |
| `FindObjectsOfType` | Removed in Unity 6.0. Use `FindObjectsByType<T>(FindObjectsSortMode.None)`. | No direct `FindObjectsOfType` calls permitted anywhere in the codebase. |
| Input System | Legacy `Input` class deprecated. Must use `com.unity.inputsystem` package. | Sort Mechanic touch detection: `EnhancedTouchSupport` + `Physics2D.OverlapPoint`. |
| Rendering | Glow processes before tonemapping in Unity 6.3. Bloom intensity values set during GDD authoring may need in-engine recalibration. | Animation System: all glow/bloom tuning must be done in-engine, not from GDD numeric values alone. |

---

## Architecture Principles

1. **State lives in one place.** Every piece of game state has exactly one authoritative owner. No system reads state from another system's internals — it reads through the owning system's exposed interface. The Game State Manager is the sole owner of board state; Save & Persistence is the sole owner of persistent data.

2. **Events flow downward, never upward.** Foundation and Core layers emit C# events. Feature and Presentation layers subscribe. No Presentation layer class holds a reference to a Core or Foundation instance — it only subscribes to events and calls exposed methods via interface.

3. **The main thread owns game logic.** All game state mutations happen on Unity's main thread. File I/O is the only operation delegated to a background thread, with a `MainThreadDispatcher` to return results. No `async` call that touches game state may be awaited from a background thread.

4. **Singleton boot order is explicit and auditable.** The `[DontDestroyOnLoad]` manager scene declares Script Execution Order for all singletons. The order is documented here and enforced in Project Settings. Any new singleton must be added to this document before implementation.

5. **The GDD is the contract.** Every TR-ID in the Technical Requirements Baseline maps to an ADR. No implementation story is started without a TR-ID and a corresponding ADR that covers the technical decision. Traceability is maintained through `tr-registry.yaml`.

---

## Engine Knowledge Gap — Systems Risk Matrix

| System | Engine Domain | Risk | Verification Required |
|--------|--------------|------|-----------------------|
| AnimationSystem | URP Render Graph, Glow before tonemapping | HIGH | Verify VFX Graph works in URP 2D Renderer before animation sprint |
| InGameHUD | UGUI Canvas (stable), `Screen.safeArea` (stable) | LOW | None |
| SortMechanic | Input System Package (MEDIUM) | MEDIUM | Confirm `Physics2D.OverlapPoint` with Input System touch coordinates |
| SaveSystem | `System.IO.File` (stable), `Thread` (stable) | LOW | Verify file path on Android (Application.persistentDataPath) |
| QualityTierSystem | `SystemInfo.graphicsMemorySize` (stable) | LOW | Test on low-end Android (Galaxy A14 target) |
| All C# code | `[SerializeField]` restrictions (HIGH) | HIGH | CI must include full compile check in Unity 6.3 |

---

## System Layer Map

```
┌─────────────────────────────────────────────────────────────────────────┐
│  PRESENTATION LAYER                                                     │
│  InGameHUD (UGUI Canvas overlay, Screen Space-Overlay)                  │
│  LevelCompleteUI (UGUI Canvas overlay, Screen Space-Overlay, z+1)       │
│  [Beta: MainMenuUI, LevelSelectUI, ShopUI] [Launch: SettingsUI]         │
├─────────────────────────────────────────────────────────────────────────┤
│  FEATURE LAYER                                                          │
│  SortMechanic  AnimationSystem  AudioSystem  LevelProgression           │
│  [Beta: HintSystem]                                                     │
├─────────────────────────────────────────────────────────────────────────┤
│  CORE LAYER                                                             │
│  GameStateManager  CoinEconomy                                          │
├─────────────────────────────────────────────────────────────────────────┤
│  FOUNDATION LAYER                                                       │
│  SaveSystem  LevelDataSystem  QualityTierSystem                         │
│  [Event Architecture: C# typed events on MonoBehaviour singletons]      │
├─────────────────────────────────────────────────────────────────────────┤
│  PLATFORM LAYER                                                         │
│  Unity 6.3 LTS · URP 2D Renderer · VFX Graph · Shader Graph            │
│  Input System Package (touch) · AudioMixer · Addressables               │
│  System.IO · AdMob SDK (Beta) · Unity IAP (Launch)                     │
└─────────────────────────────────────────────────────────────────────────┘
```

### System-to-Layer Assignment

| System | Layer | Priority | Status |
|--------|-------|----------|--------|
| Save & Persistence | Foundation | MVP | Approved |
| Level Data System | Foundation | MVP | Approved |
| Quality Tier System | Foundation | MVP | Designed |
| Game State Manager | Core | MVP | Approved |
| Coin Economy | Core | Beta | In Review |
| Level Progression | Feature | Beta | Designed |
| Sort Mechanic | Feature | MVP | Approved |
| Animation System | Feature | MVP | Designed |
| Audio System | Feature | MVP | Designed |
| In-Game HUD | Presentation | MVP | Approved |
| Level Complete UI | Presentation | MVP | Designed |
| Hint System | Feature | Beta | Not Started |
| Skin System | Feature | Beta | Not Started |
| Rewarded Ad System | Foundation | Beta | Not Started |
| IAP System | Foundation | Launch | Not Started |
| Daily Challenge System | Feature | Launch | Not Started |
| Tutorial System | Feature | Launch | Not Started |
| Main Menu UI | Presentation | Beta | Not Started |
| Level Select UI | Presentation | Beta | Not Started |
| Shop UI | Presentation | Beta | Not Started |
| Settings UI | Presentation | Launch | Not Started |
| Analytics System | Foundation | Launch | Not Started |

---

## Singleton Boot Sequence

All singletons reside on GameObjects in a `[DontDestroyOnLoad]` manager scene loaded at app start. Script Execution Order is set in **Edit > Project Settings > Script Execution Order**.

| Script Execution Order | Singleton | Reason for Order |
|-----------------------|-----------|-----------------|
| −100 | QualityTierSystem | Must set `Application.targetFrameRate` and `quality_density_multiplier` before any scene or VFX loads |
| −90 | SaveSystem | Must start background file read before other singletons need data; fires `OnSaveReady` |
| −80 | AudioSystem | Reads audio prefs from PlayerPrefs on `Awake`; must be ready before any sound can play |
| −50 | GameStateManager | Board state must exist before SortMechanic or AnimationSystem can operate |
| −40 | CoinEconomy | Subscribes to `SaveSystem.OnSaveReady`; must be registered before the event fires |
| −30 | LevelProgression | Reads level ID from SaveSystem; calls `GSM.LoadLevel()` after OnSaveReady; subscribes to GSM.OnLevelComplete |
| 0 (default) | SortMechanic, AnimationSystem, InGameHUD, LevelCompleteUI | Board-level MonoBehaviours; depend on all singletons being ready |

---

## Module Ownership Map

### Foundation Layer

#### QualityTierSystem

| Attribute | Detail |
|-----------|--------|
| **Owns** | `ActiveTier` (Low/Medium/High), `DensityMultiplier` (0.25/0.65/1.0), `TargetFrameRate` (30/60/60) |
| **Exposes** | `ActiveTier` (read), `DensityMultiplier` (read) — both set once at Awake, never change mid-session without Settings UI interaction |
| **Consumes** | `SystemInfo.graphicsMemorySize`, `SystemInfo.graphicsShaderLevel`, Android Performance Class (via `SystemInfo`), `PlayerPrefs` (player tier override) |
| **Engine APIs** | `SystemInfo.graphicsMemorySize`, `SystemInfo.graphicsShaderLevel`, `Application.targetFrameRate`, `PlayerPrefs.GetInt` |
| **Decision rule** | Evaluate in signal priority order: (1) Android Perf Class, (2) Shader Level, (3) GPU Memory. First signal determining Low or High wins. Default: Medium. |

#### SaveSystem

| Attribute | Detail |
|-----------|--------|
| **Owns** | `save.json` on disk (`Application.persistentDataPath/save.json`), audio preferences in `PlayerPrefs` |
| **Exposes** | `bool IsReady`, `event Action OnSaveReady`, `GetCurrentLevelId(): int`, `GetCompletionRecord(int): CompletionRecord?`, `WriteCompletionAtomic(int, int, string, int)`, `GetCoinBalance(): int`, `SetCoinBalance(int)` |
| **Consumes** | Nothing — root system |
| **Engine APIs** | `System.IO.File.ReadAllText/WriteAllText/Move`, `System.Threading.Thread`, `PlayerPrefs`, `Application.persistentDataPath` |
| **Thread contract** | File I/O runs on a background `Thread`. All callbacks dispatched to main thread via `MainThreadDispatcher`. No game state is read or written from the background thread. |
| **Atomic write** | Write to `save.tmp` → `File.Move(save.tmp, save.json)` — on Android, this operation is atomic within the same partition |

#### LevelDataSystem

| Attribute | Detail |
|-----------|--------|
| **Owns** | `LevelRecord[]` cache (loaded via Addressables) |
| **Exposes** | `bool IsReady`, `LevelRecord GetLevel(int levelId)` |
| **Consumes** | Addressables (level JSON assets), Level content address group |
| **Engine APIs** | `Addressables.LoadAssetAsync<TextAsset>` |
| **Data contract** | `LevelRecord`: `colorStacks: int[][]`, `stackDepth: int`, `tempSlotCount: int`, `tempSlotDepth: int`, `colorCount: int`, `parMoves: int`, `levelId: int` |

---

### Core Layer

#### GameStateManager

| Attribute | Detail |
|-----------|--------|
| **Owns** | `int[][] stackContents`, `int[][] tempSlotContents`, `int stackDepth`, `int tempSlotDepth`, `int tempSlotCount`, `int colorCount`, `int currentSequenceId`, `List<UndoEntry> undoStack`, `int moveCount`, lifecycle FSM (UNLOADED / LOADING / ACTIVE / COMPLETE / TEARDOWN) |
| **Exposes** | Read-only board state properties, all C# events listed below, `LoadLevel(int)`, `UndoRequested()` |
| **Consumes** | `LevelDataSystem.GetLevel()`, `SaveSystem` (lifecycle signal only), all Sort Mechanic events |
| **Engine APIs** | `Coroutine` + `WaitForSecondsRealtime(1.5f)` for watchdog timer |
| **Invariants** | (1) No system other than GSM may write to board state arrays. (2) `currentSequenceId` is monotonically increasing, never decrements. (3) Undo stack is unlimited; frozen on COMPLETE. (4) `bolt_count_invariant` checked at every `LoadLevel`. |

**Events exposed by GSM:**
```
event Action<int, int>          OnLevelLoaded         // (levelId, colorCount)
event Action<int, int>          OnBoardStateChanged   // (sequenceId, moveCount) — on undo, board snap
event Action<int>               OnBoardRefreshForced  // (sequenceId) — watchdog fired
event Action<int, int, int, int> OnLevelComplete      // (levelId, moveCount, parMoves, sequenceId) — canonical per ADR-0012
event Action<int>               OnLevelUnloaded       // (levelId) — emitted on TEARDOWN; consumers release level-scoped resources
event Action                    OnSessionLoadFailed
```

**Events consumed by GSM (from SortMechanic):**
```
OnMoveCommitted(int src, int dst, int colorId, int seqId)  → board mutation (5 steps, synchronous)
OnMoveCancelled(int src, int colorId)                      → no mutation
OnMoveRejected(int src, int dst, int colorId, reason)      → no mutation
OnPuzzleSolved(int moveCount)                              → transition to COMPLETE
OnDeadlockDetected()                                       → no mutation
OnMoveExecutingExited(int seqId)                           → process deferred undo if any
```

#### CoinEconomy

| Attribute | Detail |
|-----------|--------|
| **Owns** | `int coinBalance` (working copy), `EarnSource` idempotency guard (`Dictionary<int, int> lastCreditedLevelId` per source), CE FSM (LOADING/READY) |
| **Exposes** | `int GetCoinBalance()`, `bool AddCoins(int, int, EarnSource)`, `bool SpendCoins(int)`, `event Action<int, int> OnCoinBalanceChanged` |
| **Consumes** | `SaveSystem.OnSaveReady`, `SaveSystem.SetCoinBalance()` |
| **Invariants** | (1) `coinBalance` floor = 0. (2) `AddCoins` with level_id uses idempotency guard. (3) No method callable before READY without non-blocking 2s wait + safe no-op fallback. (4) First install: initialize to 150 (starter grant). |

---

### Feature Layer

#### LevelProgression

| Attribute | Detail |
|-----------|--------|
| **Owns** | `int currentLevelId`, `Dictionary<int, CompletionRecord> bestStars` |
| **Exposes** | `bool IsLocked(int)`, `bool IsBreather(int)`, `int GetBestStars(int)`, `event Action<int,int,int,int> OnLevelCompleted` |
| **Consumes** | `SaveSystem.OnSaveReady` (read level data), `SaveSystem.WriteCompletionAtomic()`, `LevelDataSystem.GetLevel()`, `GSM.OnLevelComplete` |
| **Formulas** | `is_locked = (levelId > currentLevelId)` · `is_breather = (levelId % 10 == 0)` · `best_stars = max(current, earned)` |

#### SortMechanic

| Attribute | Detail |
|-----------|--------|
| **Owns** | Interaction FSM (IDLE / BOLT_SELECTED / MOVE_EXECUTING / CANCELLATION / INVALID_MOVE / WIN), held bolt reference, input buffer (one tap), sequence ID tracking for stale-signal discard |
| **Exposes** | All Sort Mechanic C# events (see GSM "Events consumed" section above, plus `OnAnimationComplete` subscription) |
| **Consumes** | `GSM.StackContents[]` (synchronous pull), `GSM.TempSlotContents[]`, `GSM.StackDepth`, `GSM.TempSlotCount`, `GSM.ColorCount` |
| **Engine APIs** | `EnhancedTouchSupport` (Input System Package), `Physics2D.OverlapPoint()` for bolt/stack tap detection |
| **Input contract** | Touch events read from `UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches`. Tap position converted to world space via `Camera.main.ScreenToWorldPoint()`. `Physics2D.OverlapPoint()` detects which BoltStack collider was tapped. |
| **State invariants** | (1) Only one bolt held at a time. (2) `move_executing_exited` emitted only on MOVE_EXECUTING → IDLE (not WIN, not watchdog). (3) One-tap buffer during MOVE_EXECUTING; discarded on WIN exit. |

#### AnimationSystem

| Attribute | Detail |
|-----------|--------|
| **Owns** | Bolt visual state, VFX pool (ring + spark instances), `activeSequenceId` |
| **Exposes** | `event Action<int> OnAnimationComplete` |
| **Consumes** | `GSM.OnBoardStateChanged` (snap visuals), `GSM.OnBoardRefreshForced` (abort + snap), `GSM.OnLevelComplete` (celebration), `SortMechanic.OnMoveCommitted`, `SortMechanic.OnMoveRejected`, `SortMechanic.OnMoveCancelled`, `QualityTierSystem.DensityMultiplier` |
| **Engine APIs** | `Coroutine` (bolt motion tween), VFX Graph `VisualEffect` component (ring + sparks), `AudioSystem.PlayBoltSettle()` (sole caller), URP `Volume` (bloom via post-processing) |
| **Sequence ID contract** | Stores `activeSequenceId` when bolt animation begins. Emits `OnAnimationComplete(activeSequenceId)`. SortMechanic discards if `sequenceId ≠ current MOVE_EXECUTING sequence_id`. |
| **⚠️ Engine risk** | VFX Graph in URP 2D Renderer: must verify GPU compute availability on Low-tier Android devices before animation sprint. Low-tier fallback: disable VFX Graph, use sprite-based ring animation instead (ADR-10). |

#### AudioSystem

| Attribute | Detail |
|-----------|--------|
| **Owns** | `AudioMixer` reference, pool of 8 `AudioSource` components, volume state |
| **Exposes** | `PlayBoltSettle(bool isValid)`, `SetSFXVolume(float)`, `SetAmbientVolume(float)`, `SetUIVolume(float)` |
| **Consumes** | `PlayerPrefs` (audio prefs, read on `Awake`) |
| **Engine APIs** | `AudioMixer.SetFloat("SFXVolume", db)`, `AudioSource.PlayOneShot()` |
| **Audio bus groups** | SFX (bolt clicks, chimes) · Ambient (machine hum loop) · UI (button taps) |
| **Volume conversion** | `db = volume > 0.001f ? Mathf.Log10(volume) * 20f : -80f` (linear 0–1 → dB) |

---

### Presentation Layer

#### InGameHUD

| Attribute | Detail |
|-----------|--------|
| **Owns** | HUD FSM (INACTIVE / IDLE / FROZEN), move counter, button enabled states, pity grant counter (`int consecutiveZeroStarAttempts`), coin display |
| **Exposes** | Emits `GSM.UndoRequested()` on undo button tap; calls `CE.AddCoins(50, -1, EarnSource.PityGrant)` on pity threshold |
| **Consumes** | `GSM.OnLevelLoaded` (reset), `GSM.OnBoardStateChanged` (move counter), `GSM.OnLevelComplete` (FROZEN), `AnimationSystem.OnAnimationComplete` (re-enable undo), `CoinEconomy.OnCoinBalanceChanged` (coin display), `SortMechanic.OnDeadlockDetected` (hint pulse) |
| **Engine APIs** | `Canvas` (Screen Space - Overlay), `TextMeshProUGUI`, `Button.onClick`, `Screen.safeArea` → `RectTransform.anchorMin/Max` |
| **Undo button lock** | Disabled immediately on tap (optimistic lock). Re-enabled only on `AnimationSystem.OnAnimationComplete(seqId)` where seqId matches the in-flight sequence. |
| **Hint button cost** | `hint_cost = 50` (read from `CoinEconomy` config — not hardcoded in HUD). |

#### LevelCompleteUI

| Attribute | Detail |
|-----------|--------|
| **Owns** | Star rating display, coin animation, ad flow FSM (IDLE / AD_PROCESSING), 30s ad watchdog |
| **Consumes** | `LevelProgression.OnLevelCompleted`, `CoinEconomy` (AddCoins calls), AdMob SDK callbacks |
| **Engine APIs** | `Canvas` (Screen Space - Overlay, sort order +1 above HUD), `Coroutine` (coin counter animation), AdMob `RewardedAd` |

---

## Data Flow Diagrams

### Flow 1: Player Tap → Move → Animation → Win/Idle

```
Touch.activeTouches[0].screenPosition
  → Camera.main.ScreenToWorldPoint()
  → Physics2D.OverlapPoint() → BoltStack collider
  → SortMechanic.HandleTap(stackIndex)
    [synchronous] GSM.StackContents[i] read
    move validation logic
    [LEGAL] OnMoveCommitted(src, dst, colorId, seqId) →
      GSM.OnMoveCommitted():   ← [synchronous, same frame]
        remove bolt from source array
        add bolt to destination array
        push UndoEntry
        currentSequenceId++
        moveCount++
      AnimationSystem: start bolt Coroutine ← [subscribes to OnMoveCommitted]
        ┌── bolt lifts (80ms)
        ├── bolt travels (80–300ms, distance-proportional)
        └── bolt settles + micro-bounce (70ms)
            AudioSystem.PlayBoltSettle(true)
            OnAnimationComplete(seqId)
              → SortMechanic: stale-signal check → win condition check
                [WIN] OnPuzzleSolved(moveCount)
                [NOT WIN] OnMoveExecutingExited(seqId) → GSM deferred undo
```

### Flow 2: App Boot (Initialization Order)

```
QualityTierSystem._Awake() [Order -100]
  SystemInfo → ActiveTier, Application.targetFrameRate set

SaveSystem._Awake() [Order -90]
  File.ReadAllText(persistentDataPath/save.json) → parse JSON [synchronous, blocking]
  iOS cold-start: UnauthorizedAccessException → Thread.Sleep(250) retry (max 5s)
  IsReady = true → OnSaveReady?.Invoke() [synchronous — lower-SEO systems not yet subscribed; subscribe-then-check required]

CoinEconomy._Awake() [Order -40]
  Subscribes to SaveSystem.OnSaveReady
  SaveSystem.IsReady check (subscribe-then-check pattern)
  [On OnSaveReady] read coin_balance; first install → 150 starter

LevelProgression._Awake() [Order -30]
  Subscribes to SaveSystem.OnSaveReady, GSM.OnLevelComplete
  [On OnSaveReady] read current_level_id, completion_records
  GSM.LoadLevel(current_level_id)
    LevelDataSystem.GetLevel(id) → LevelRecord
    bolt_count_invariant check
    board state populated
    GSM.OnLevelLoaded event → AnimationSystem.Reset(), InGameHUD.Reset()
```

### Flow 3: Level Complete

```
SortMechanic.OnPuzzleSolved(moveCount) →
  GSM: COMPLETE state; undo stack frozen; OnLevelComplete(levelId, moveCount)
  AnimationSystem: ~2000ms celebration → OnAnimationComplete(seqId)
  LevelCompleteUI.Show(stars, coinAmount) displayed
  LevelProgression.AdvanceLevel():
    ComputeStarRating(moveCount, parMoves) → stars
    best_stars = max(currentBest, stars)
    SaveSystem.WriteCompletionAtomic(levelId, stars, version, levelId+1)
      → [Background Thread] File.WriteAllText(tmp), File.Move(tmp, save)
    OnLevelCompleted(stars, levelId, moveCount, parMoves) event
      → CoinEconomy subscribes: analytics tracking
  LevelCompleteUI → CE.AddCoins(coin_reward_per_star[stars], levelId, Base)
  [Optional] Ad offer → CE.AddCoins(bonus, levelId, AdBonus)
```

### Flow 4: Save Write (Thread Boundary)

```
[Main Thread]
  SaveSystem.WriteCompletionAtomic(levelId, stars, version, newLevelId):
    Build JSON string (synchronous, main thread)
    new Thread(() => {
      [Background Thread]
      File.WriteAllText(tmpPath, json)     // write to .tmp
      File.Move(tmpPath, savePath)         // atomic rename
      UnityMainThreadDispatcher.Enqueue(OnWriteComplete)
    }).Start()

[Main Thread callback]
  OnWriteComplete() — notify callers, update IsReady state if needed
```

---

## API Boundaries

### ISaveSystem

```csharp
public interface ISaveSystem {
    bool IsReady { get; }
    event Action OnSaveReady;
    int GetCurrentLevelId();
    CompletionRecord? GetCompletionRecord(int levelId);
    void WriteCompletionAtomic(int levelId, int bestStars, string version, int newCurrentLevelId);
    int GetCoinBalance();
    void SetCoinBalance(int balance);
}

public readonly struct CompletionRecord {
    public readonly int BestStars;
    public readonly string CompletionVersion;
}
```

### IGameStateManager

```csharp
public interface IGameStateManager {
    // Board state (read-only, synchronous)
    IReadOnlyList<int>[] StackContents { get; }
    int StackDepth { get; }
    IReadOnlyList<int>[] TempSlotContents { get; }
    int TempSlotDepth { get; }
    int TempSlotCount { get; }
    int ColorCount { get; }
    int MoveCount { get; }
    // C# Events
    event Action<int, int>  OnLevelLoaded;         // (levelId, colorCount)
    event Action<int, int>  OnBoardStateChanged;   // (sequenceId, moveCount)
    event Action<int>       OnBoardRefreshForced;  // (sequenceId) — watchdog
    event Action<int, int>  OnLevelComplete;       // (levelId, moveCount)
    event Action            OnSessionLoadFailed;
    // Commands
    void LoadLevel(int levelId);
    void UndoRequested();
}
```

### ICoinEconomy

```csharp
public interface ICoinEconomy {
    int GetCoinBalance();
    bool AddCoins(int amount, int levelId = -1, EarnSource source = EarnSource.Base);
    bool SpendCoins(int amount);
    event Action<int, int> OnCoinBalanceChanged;  // (newBalance, delta)
}

public enum EarnSource { Base, AdBonus, PityGrant }
```

### ILevelProgression

```csharp
public interface ILevelProgression {
    bool IsLocked(int levelId);
    bool IsBreather(int levelId);
    int GetBestStars(int levelId);
    event Action<int, int, int, int> OnLevelCompleted;  // (stars, levelId, moveCount, parMoves)
}
```

### IAudioSystem

```csharp
public interface IAudioSystem {
    void PlayBoltSettle(bool isValid);
    void SetSFXVolume(float normalizedVolume);    // 0–1
    void SetAmbientVolume(float normalizedVolume);
    void SetUIVolume(float normalizedVolume);
}
```

### SortMechanic Events (C# events on SortMechanic MonoBehaviour)

```csharp
// Emitted by SortMechanic; consumed by GSM, AnimationSystem, HUD
public event Action<int, int, int, int>              OnMoveCommitted;      // (src, dst, colorId, seqId)
public event Action<int, int>                        OnMoveCancelled;      // (src, colorId)
public event Action<int, int, int, MoveRejectReason> OnMoveRejected;       // (src, dst, colorId, reason)
public event Action<int>                             OnPuzzleSolved;       // (moveCount)
public event Action                                  OnDeadlockDetected;
public event Action<int>                             OnMoveExecutingExited;// (seqId) — IDLE path only

// Consumed by SortMechanic; emitted by AnimationSystem
// (AnimationSystem.OnAnimationComplete(int seqId))

public enum MoveRejectReason { DestinationFull, ColorMismatch }
```

---

## ADR Audit

**13 ADRs written, all status: Accepted.** TR registry at 72 active entries, 100% covered. Last full review: `architecture-review-2026-05-12.md` (verdict: CONCERNS — 2 localized contract conflicts; 0 coverage gaps).

| TR Coverage | Count |
|-------------|-------|
| Requirements in baseline | 72 |
| Requirements covered by ADRs | 72 |
| GAP | 0 (0%) |

---

## Accepted ADRs

All 13 ADRs written and Accepted. See `docs/architecture/` for full text. Governing each layer:

### Foundation Layer

| ADR | Title |
|-----|-------|
| ADR-0001 | Singleton Architecture and Boot Sequence |
| ADR-0002 | Event and Signal Architecture |
| ADR-0003 | Save System Design |
| ADR-0004 | Level Data Loading Strategy |
| ADR-0005 | Rendering Pipeline Configuration |

### Core Layer

| ADR | Title |
|-----|-------|
| ADR-0006 | Board State Representation and GSM Design |
| ADR-0007 | Input Handling Strategy |
| ADR-0008 | UI Hierarchy and Safe Area |

### Feature / Presentation Layer

| ADR | Title |
|-----|-------|
| ADR-0009 | Bolt Animation Strategy |
| ADR-0010 | VFX Graph and Bloom on Mobile |
| ADR-0011 | Audio Architecture |
| ADR-0012 | HUD and Level Complete UI Business Logic |
| ADR-0013 | Level Layout Column Cap |

---

## Technical Requirements Baseline

All 56 requirements extracted from 11 GDDs. Full list maintained here as the traceability source until `/architecture-review` populates `docs/architecture/tr-registry.yaml`.

| Req ID | GDD | Requirement | Domain | ADR Coverage |
|--------|-----|-------------|--------|-------------|
| TR-SORT-001 | sort-mechanic | State machine: IDLE/BOLT_SELECTED/MOVE_EXECUTING/WIN/CANCELLATION/INVALID_MOVE | Core | ADR-6 |
| TR-SORT-002 | sort-mechanic | Move validation: empty accepts any, capacity gate, color match | Core | ADR-6 |
| TR-SORT-003 | sort-mechanic | Win condition: all color stacks full + monochromatic | Core | ADR-6 |
| TR-SORT-004 | sort-mechanic | One-tap input buffer during MOVE_EXECUTING; discard on WIN | Input | ADR-7 |
| TR-SORT-005 | sort-mechanic | Shallow deadlock check (depth-1) → emit deadlock_detected | Core | ADR-6 |
| TR-SORT-006 | sort-mechanic | Sequence ID stale-signal guard on animation_complete | Event | ADR-2 |
| TR-SORT-007 | sort-mechanic | move_executing_exited on IDLE exit only (not WIN, not watchdog) | Event | ADR-2 |
| TR-SORT-008 | sort-mechanic | Android back gesture → cancellation in BOLT_SELECTED | Input | ADR-7 |
| TR-SORT-009 | sort-mechanic | Synchronous pull-on-demand read of board state from GSM | Core | ADR-6, ADR-7 |
| TR-SORT-010 | sort-mechanic | Column cap: color_count + temp_slot_count ≤ 8 | UI/Layout | ADR-8 |
| TR-GSM-001 | game-state-manager | Sole owner of board state arrays | State | ADR-6 |
| TR-GSM-002 | game-state-manager | Monotonic sequence ID, never decrements | State | ADR-6 |
| TR-GSM-003 | game-state-manager | Unlimited undo stack; frozen on COMPLETE | State | ADR-6 |
| TR-GSM-004 | game-state-manager | Watchdog 1500ms → board_refresh_forced | Timing | ADR-6 |
| TR-GSM-005 | game-state-manager | Atomic board mutation (5 steps synchronous) | State | ADR-6 |
| TR-GSM-006 | game-state-manager | Deferred undo on MOVE_EXECUTING | State | ADR-6 |
| TR-GSM-007 | game-state-manager | bolt_count_invariant check at level load | Validation | ADR-6 |
| TR-GSM-008 | game-state-manager | Level lifecycle FSM: UNLOADED/LOADING/ACTIVE/COMPLETE/TEARDOWN | State | ADR-1, ADR-6 |
| TR-GSM-009 | game-state-manager | Emit typed C# events | Event | ADR-2 |
| TR-LDS-001 | level-data-system | Level record schema | Data | ADR-4 |
| TR-LDS-002 | level-data-system | bolt_count_invariant at authoring time | Validation | ADR-4 |
| TR-LDS-003 | level-data-system | System readiness query before load | Init | ADR-1, ADR-4 |
| TR-ANIM-001 | animation-system | Bolt lift arc + travel + settle (80+80-300+70ms) | Rendering | ADR-9 |
| TR-ANIM-002 | animation-system | Stack completion glow + VFX ring + sparks | Rendering | ADR-9, ADR-10 |
| TR-ANIM-003 | animation-system | Snap bolt visuals on board_state_changed, board_refresh_forced | Rendering | ADR-9 |
| TR-ANIM-004 | animation-system | Level complete celebration ~1600–2020ms | Rendering | ADR-9 |
| TR-ANIM-005 | animation-system | quality_density_multiplier from QTS at level_loaded | Rendering | ADR-5, ADR-10 |
| TR-ANIM-006 | animation-system | Emit animation_complete(seqId) | Event | ADR-2 |
| TR-ANIM-007 | animation-system | Must emit within watchdog_timeout_ms 1500ms | Timing | ADR-9 |
| TR-ANIM-008 | animation-system | Rejection shake 100ms; no animation_complete | Rendering | ADR-9 |
| TR-AUDIO-001 | audio-system | Three AudioBus groups: SFX, Ambient, UI | Audio | ADR-11 |
| TR-AUDIO-002 | audio-system | AudioMixer.SetFloat for volume control | Audio | ADR-11 |
| TR-AUDIO-003 | audio-system | PlayerPrefs audio keys read on Awake | Audio | ADR-11 |
| TR-AUDIO-004 | audio-system | PlayBoltSettle(bool) — AnimationSystem sole caller | Audio | ADR-11 |
| TR-AUDIO-005 | audio-system | Machine ambient hum loop | Audio | ADR-11 |
| TR-AUDIO-006 | audio-system | Pooled AudioSource (8 sources) for concurrent SFX | Audio | ADR-11 |
| TR-HUD-001 | in-game-hud | UGUI Canvas, Screen Space-Overlay, Screen.safeArea | UI | ADR-8 |
| TR-HUD-002 | in-game-hud | Move counter subscribes to GSM OnBoardStateChanged | UI | ADR-8 |
| TR-HUD-003 | in-game-hud | Undo button: optimistic lock, re-enable on animation_complete | UI | ADR-8 |
| TR-HUD-004 | in-game-hud | Hint button: disabled if balance < 50 or MOVE_EXECUTING | UI | ADR-8 |
| TR-HUD-005 | in-game-hud | Coin display: subscribes to OnCoinBalanceChanged | UI | ADR-8 |
| TR-HUD-006 | in-game-hud | Pity grant counter (5 consecutive 0-star → AddCoins(50)) | Gameplay | ADR-8 |
| TR-HUD-007 | in-game-hud | Emit UndoRequested to GSM | Event | ADR-2 |
| TR-LCUI-001 | level-complete-ui | StarRating(move_count, par_moves) | UI | ADR-8 |
| TR-LCUI-002 | level-complete-ui | Coin animation; coin_reward_per_star=[0,10,20,40] | UI/Economy | ADR-8 |
| TR-LCUI-003 | level-complete-ui | Ad FSM + 30s watchdog | UI/Ad | ADR-8 |
| TR-SP-001 | save-persistence | JSON file; atomic via File.Move | Save | ADR-3 |
| TR-SP-002 | save-persistence | Fields: current_level_id, completion_record[], coin_balance | Save | ADR-3 |
| TR-SP-003 | save-persistence | WriteCompletionAtomic(...) | Save | ADR-3 |
| TR-SP-004 | save-persistence | IsReady + OnSaveReady; subscribe-then-check | Init | ADR-1, ADR-3 |
| TR-SP-005 | save-persistence | PlayerPrefs for audio prefs | Save | ADR-3 |
| TR-SP-006 | save-persistence | Save migration versioning | Save | ADR-3 |
| TR-SP-007 | save-persistence | iOS file protection; cold-start retry | Platform | ADR-3 |
| TR-SP-008 | save-persistence | Background Thread for file I/O | Threading | ADR-3 |
| TR-CE-001 | coin-economy | CE FSM: LOADING/READY | State | ADR-1 |
| TR-CE-002 | coin-economy | AddCoins with idempotency guard | Economy | ADR-6 |
| TR-CE-003 | coin-economy | SpendCoins; floor=0 | Economy | ADR-6 |
| TR-CE-004 | coin-economy | Starter grant 150 on first install | Economy | ADR-3 |
| TR-QTS-001 | quality-tier-system | Detect tier at startup: GPU memory, shader level, Android Perf Class | Platform | ADR-5 |
| TR-QTS-002 | quality-tier-system | Apply density multiplier + framerate before first scene | Init | ADR-1, ADR-5 |
| TR-LP-001 | level-progression | is_locked = (levelId > currentLevelId) | Gameplay | ADR-6 |
| TR-LP-002 | level-progression | best_stars = max(current, earned) | Gameplay | ADR-6 |
| TR-LP-003 | level-progression | Emit LevelCompleted(stars, levelId, moveCount, parMoves) | Event | ADR-2 |

---

## Open Questions

All open questions from initial authoring (2026-05-01) resolved in ADR-0003, ADR-0007, ADR-0009, ADR-0010.

| Question | Resolution | ADR |
|----------|-----------|-----|
| VFX Graph in URP 2D on Low-tier Android | Sprite-based VFX fallback for Low tier; VFX Graph on Medium/High | ADR-0010 |
| MainThreadDispatcher: package vs custom | Custom single-file implementation (no external dependency) | ADR-0003 |
| Android back gesture mechanism | `InputSystem.EnhancedTouch` + `Keyboard.current` back simulation; Android 13+ predictive back handled via manifest flag | ADR-0007 |
| DOTween vs built-in Tween vs Coroutine | Custom Coroutine tween — satisfies 80–300ms precision, zero GC on mobile | ADR-0009 |
| LevelCompleteUI during MVP without IAP | Ad SDK presence checked at runtime; graceful no-op when absent | ADR-0012 |
