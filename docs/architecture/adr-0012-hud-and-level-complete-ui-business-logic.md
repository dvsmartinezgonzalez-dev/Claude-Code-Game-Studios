# ADR-0012: HUD and Level Complete UI Business Logic

## Status
Accepted

## Date
2026-05-03

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | UI (business logic layer) |
| **Knowledge Risk** | LOW — C# events, Coroutines, `Button.onClick`, and `OnEnable` are stable APIs unchanged in Unity 6.x. All post-cutoff UI API risks (`[SerializeField]` restriction, `legacy_ui_text`) are addressed by ADR-0008. |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) Confirm `OnCoinRewardGranted` fires before the first rendered frame when LevelCompleteUI is activated — verify via `[UnityTest]` frame boundary assertion. (2) Confirm hint timeout coroutine cancels correctly when `hint_result` arrives at the timeout boundary. (3) Confirm `Time.realtimeSinceStartup` advances correctly during iOS backgrounding on a physical device. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (all event subscriptions follow its pattern); ADR-0006 (coin_balance ownership and `ICoinEconomy` interface); ADR-0008 (Canvas hierarchy — InGameHUD and LevelCompleteUI MonoBehaviours live on these Canvases) |
| **Enables** | None |
| **Blocks** | InGameHUD implementation sprint; LevelCompleteUI implementation sprint |
| **Ordering Note** | ADR-0008 must be Accepted first — this ADR defines behaviour *inside* the components whose hierarchy ADR-0008 defines. ADR-0006 must be Accepted first — the CE interface contract is consumed here. |

## Context

### Problem Statement
ADR-0008 established the Canvas hierarchy, safe area handling, and component placement for InGameHUD and LevelCompleteUI. It did not define *what those components do*: which events they subscribe to, how their internal state machines are implemented, who owns the pity grant counter, how hint timeout and ad watchdog timeouts are enforced, or how coin rewards are delivered without creating a direct UI→CoinEconomy dependency.

A secondary conflict existed between `in-game-hud.md` and `level-complete-ui.md` on the `level_complete` event payload. The HUD GDD required `par_moves` in the event payload; the LevelCompleteUI GDD described reading `par_moves` directly from Level Data System; `game-state-manager.md` WIN-01 omitted `par_moves` entirely. This ADR establishes the canonical payload and resolves the inconsistency.

### Constraints
- `coin_balance` is owned by CoinEconomy; UI layer must not call `CE.AddCoins()` directly — ADR-0006
- All events use C# `Action<T>` with `?.Invoke()` — no UnityEvent on hot paths — ADR-0002
- Subscribe in `Awake`, unsubscribe in `OnDestroy` with null guard on Instance — ADR-0002
- No lambda subscribers — named instance methods only — ADR-0002
- `coin_reward_granted` must fire before navigation is physically possible — GDD AC-08, AC-26
- Pity grant counter is session-only — not persisted per Save System GDD
- `[SerializeField]` only on fields, not properties — Unity 6.3 compile gate (ADR-0008)

### Requirements
- InGameHUD implements FSM: INACTIVE → IDLE → HINT_PROCESSING → FROZEN
- LevelCompleteUI implements FSM: HIDDEN → REVEALING → AD_OFFER / AD_PROCESSING → IDLE → DISMISSED
- Canonical `level_complete` payload: `(int levelId, int moveCount, int parMoves, int sequenceId)`
- Coin delivery routes through Level Progression events, not direct CE calls
- Hint timeout: stored `Coroutine` reference; cancellable on `hint_result`
- Ad watchdog: real-time elapsed (`Time.realtimeSinceStartup`), not frame time — must survive iOS backgrounding

## Decision

### Business Logic Ownership

| Concern | Owner | Notes |
|---------|-------|-------|
| HUD FSM (INACTIVE / IDLE / HINT_PROCESSING / FROZEN) | `InGameHUD.cs` | Private enum + switch; no framework |
| Pity grant counter (`_pityAttempts`, `_activeLevelId`) | `InGameHUD.cs` | Session-only fields; not persisted |
| Hint timeout coroutine | `InGameHUD.cs` | `_hintTimeoutCoroutine` stored ref; cancelled on `hint_result` |
| Level Complete FSM (HIDDEN / REVEALING / AD_OFFER / AD_PROCESSING / IDLE / DISMISSED) | `LevelCompleteUI.cs` | Private enum + switch; no framework |
| Ad watchdog coroutine | `LevelCompleteUI.cs` | `_adWatchdogCoroutine` stored ref; real-time elapsed |
| Coin reward delivery (fire-on-enable) | `LevelCompleteUI.cs` | Fires `OnCoinRewardGranted` in `OnEnable`; Level Progression calls CE |
| Star rating computation | Shared `StarRatingCalculator` | Single static class called by both HUD (pity grant) and LevelCompleteUI (display) |

### Canonical `level_complete` Payload

`game-state-manager.md` WIN-01 is updated by this ADR. GSM reads `par_moves` from Level Data System before emitting `OnLevelComplete`. The canonical C# signature is:

```csharp
// On GameStateManager
public event Action<int, int, int, int> OnLevelComplete;
// args: levelId, moveCount, parMoves, sequenceId
```

Both `InGameHUD` and `LevelCompleteUI` receive `par_moves` from this event. Neither queries Level Data System for `par_moves` independently.

### Coin Reward Delivery Contract

`LevelCompleteUI` must **not** call `ICoinEconomy.AddCoins()` directly. It fires typed C# events to Level Progression, which owns the call into CoinEconomy:

```csharp
// On LevelCompleteUI
public event Action<int> OnCoinRewardGranted;   // fires in OnEnable — always
public event Action<int> OnCoinBonusGranted;    // fires only after ad_reward_granted

// Level Progression subscribes:
void HandleCoinRewardGranted(int amount) =>
    CoinEconomy.Instance.AddCoins(amount, _currentLevelId, EarnSource.Base);
void HandleCoinBonusGranted(int amount) =>
    CoinEconomy.Instance.AddCoins(amount, _currentLevelId, EarnSource.AdBonus);
```

`OnCoinRewardGranted` fires in `OnEnable` — synchronously, before the first rendered frame — guaranteeing delivery before any navigation tap is physically possible.

### InGameHUD Event Subscriptions

```csharp
// Awake — subscribe with named methods
GameStateManager.Instance.OnLevelLoaded       += HandleLevelLoaded;
GameStateManager.Instance.OnBoardStateChanged += HandleBoardStateChanged;
GameStateManager.Instance.OnLevelComplete     += HandleLevelComplete;
GameStateManager.Instance.OnSessionLoadFailed += HandleSessionLoadFailed;
AnimationSystem.Instance.OnAnimationComplete  += HandleAnimationComplete;
CoinEconomy.Instance.OnCoinBalanceChanged     += HandleCoinBalanceChanged;
HintSystem.Instance.OnHintResult              += HandleHintResult;

// OnDestroy — null guard required (Instance may be null on app quit / scene reload order)
void OnDestroy()
{
    if (GameStateManager.Instance != null)
    {
        GameStateManager.Instance.OnLevelLoaded       -= HandleLevelLoaded;
        GameStateManager.Instance.OnBoardStateChanged -= HandleBoardStateChanged;
        GameStateManager.Instance.OnLevelComplete     -= HandleLevelComplete;
        GameStateManager.Instance.OnSessionLoadFailed -= HandleSessionLoadFailed;
    }
    if (AnimationSystem.Instance != null)
        AnimationSystem.Instance.OnAnimationComplete  -= HandleAnimationComplete;
    if (CoinEconomy.Instance != null)
        CoinEconomy.Instance.OnCoinBalanceChanged     -= HandleCoinBalanceChanged;
    if (HintSystem.Instance != null)
        HintSystem.Instance.OnHintResult              -= HandleHintResult;
}
```

Outbound:
```csharp
GameStateManager.Instance.OnUndoRequested?.Invoke();   // undo button tap
HintSystem.Instance.OnHintRequested?.Invoke();         // hint button tap
```

### LevelCompleteUI Event Subscriptions

```csharp
// Awake
GameStateManager.Instance.OnLevelComplete       += HandleLevelComplete;
RewardedAdSystem.Instance.OnAdRewardGranted     += HandleAdRewardGranted;
RewardedAdSystem.Instance.OnAdRewardDenied      += HandleAdRewardDenied;

// OnDestroy — null guard required
void OnDestroy()
{
    if (GameStateManager.Instance != null)
        GameStateManager.Instance.OnLevelComplete   -= HandleLevelComplete;
    if (RewardedAdSystem.Instance != null)
    {
        RewardedAdSystem.Instance.OnAdRewardGranted -= HandleAdRewardGranted;
        RewardedAdSystem.Instance.OnAdRewardDenied  -= HandleAdRewardDenied;
    }
}
```

Outbound (Level Progression subscribes to all of these):
```csharp
public event Action<int> OnCoinRewardGranted;
public event Action<int> OnCoinBonusGranted;
public event Action      OnNextLevelRequested;
public event Action      OnRetryRequested;
public event Action      OnMenuRequested;
```

Outbound command to Rewarded Ad System:
```csharp
RewardedAdSystem.Instance.OnAdWatchRequested?.Invoke();   // Watch tap in AD_OFFER
```

### Hint Timeout Implementation

```csharp
private Coroutine _hintTimeoutCoroutine;

// On hint tap (entering HINT_PROCESSING):
_hintTimeoutCoroutine = StartCoroutine(HintTimeoutRoutine());

// On hint_result received (any result):
if (_hintTimeoutCoroutine != null)
{
    StopCoroutine(_hintTimeoutCoroutine);
    _hintTimeoutCoroutine = null;
}
// StopCoroutine on an already-completed coroutine is safe — Unity silently ignores it.

private IEnumerator HintTimeoutRoutine()
{
    yield return new WaitForSeconds(_hintTimeoutMs / 1000f);
    ExitHintProcessing();   // re-evaluate F-03; no coin deducted
    _hintTimeoutCoroutine = null;
}
```

### Ad Watchdog Implementation

`WaitForSeconds` is frame-dependent and pauses during iOS backgrounding. `Time.realtimeSinceStartup` is wall-clock time and continues advancing while the app is suspended. The watchdog must use real-time elapsed:

```csharp
private Coroutine _adWatchdogCoroutine;
private float     _adProcessingStartRealtime;

// On AD_PROCESSING entry:
_adProcessingStartRealtime = Time.realtimeSinceStartup;
_adWatchdogCoroutine = StartCoroutine(AdWatchdogRoutine());

private IEnumerator AdWatchdogRoutine()
{
    while (Time.realtimeSinceStartup - _adProcessingStartRealtime
           < _adWatchdogTimeoutMs / 1000f)
    {
        yield return null;   // resumes on first frame after app resume
    }
    ExitAdProcessing(bonusGranted: false);
    _adWatchdogCoroutine = null;
}

// On ad result received:
if (_adWatchdogCoroutine != null)
{
    StopCoroutine(_adWatchdogCoroutine);
    _adWatchdogCoroutine = null;
}
```

`yield return null` is intentional: after backgrounding, the coroutine resumes on the next frame, immediately evaluating the elapsed check. `WaitForEndOfFrame` would also resume post-background but runs later in the frame and is semantically incorrect for a timeout check.

### Star Rating Shared Implementation

`StarRating(moveCount, parMoves)` is the canonical formula used by both InGameHUD (pity grant evaluation) and LevelCompleteUI (star display). Implemented once in a shared static class:

```csharp
public static class StarRatingCalculator
{
    public static int Compute(int moveCount, int parMoves, float threshold2Star)
    {
        if (parMoves < 1) return 1;   // E-07 fallback
        if (moveCount <= parMoves) return 3;
        if (moveCount <= Mathf.FloorToInt(parMoves * threshold2Star)) return 2;
        return 1;
    }
}
```

Both `InGameHUD.HandleLevelComplete` and `LevelCompleteUI.HandleLevelComplete` call `StarRatingCalculator.Compute()`. No formula duplication.

### Architecture Diagram

```
GameStateManager                      InGameHUD.cs [SEO 0]
  OnLevelLoaded ──────────────────────► HandleLevelLoaded()       → reset counter; cache levelId; INACTIVE→IDLE
  OnBoardStateChanged ────────────────► HandleBoardStateChanged() → counter delta; undo enable (F-02)
  OnLevelComplete ────────────────────► HandleLevelComplete()     → FROZEN; pity grant check (F-05)
  OnSessionLoadFailed ────────────────► HandleSessionLoadFailed() → error overlay; all buttons disabled

AnimationSystem
  OnAnimationComplete ────────────────► HandleAnimationComplete() → undo re-enable (F-02)

CoinEconomy
  OnCoinBalanceChanged ───────────────► HandleCoinBalanceChanged()→ display update; 300ms pulse (F-04)

HintSystem
  OnHintResult ───────────────────────► HandleHintResult()        → exit HINT_PROCESSING; re-eval F-03

InGameHUD (outbound)
  OnUndoRequested ─────────────────────► GameStateManager         ← undo button tap (IDLE + stack > 0)
  OnHintRequested ─────────────────────► HintSystem               ← hint button tap (ENABLED)

──────────────────────────────────────────────────────────────────────────────────

GameStateManager                      LevelCompleteUI.cs [SEO 0]
  OnLevelComplete ────────────────────► HandleLevelComplete()     → OnEnable; OnCoinRewardGranted fires; REVEALING

RewardedAdSystem
  OnAdRewardGranted ──────────────────► HandleAdRewardGranted()   → OnCoinBonusGranted fires; IDLE
  OnAdRewardDenied ───────────────────► HandleAdRewardDenied()    → IDLE; no bonus

LevelCompleteUI (outbound → Level Progression)
  OnCoinRewardGranted ─────────────────► LP → CE.AddCoins(Base)   ← fires in OnEnable unconditionally
  OnCoinBonusGranted ──────────────────► LP → CE.AddCoins(AdBonus)← fires only after ad_reward_granted
  OnNextLevelRequested ────────────────► LevelProgression         ← Next Level tap
  OnRetryRequested ────────────────────► LevelProgression         ← Retry tap
  OnMenuRequested ─────────────────────► LevelProgression         ← Menu tap / Android back

LevelCompleteUI (outbound → Rewarded Ad System)
  OnAdWatchRequested ──────────────────► RewardedAdSystem         ← Watch tap in AD_OFFER
```

### Key Interfaces

```csharp
// Shared star rating utility — single source of truth for both UI components
public static class StarRatingCalculator
{
    public static int Compute(int moveCount, int parMoves, float threshold2Star);
}

// InGameHUD — MonoBehaviour on InGameHUD Canvas (ADR-0008 hierarchy)
public class InGameHUD : MonoBehaviour
{
    // Outbound events
    public event Action OnUndoRequested;
    public event Action OnHintRequested;

    // FSM
    private enum HudState { Inactive, Idle, HintProcessing, Frozen }
    private HudState _state = HudState.Inactive;

    // Pity grant (session-only, not persisted)
    private int _pityAttempts;
    private int _activeLevelId;

    // Hint timeout (stored ref for surgical cancellation)
    private Coroutine _hintTimeoutCoroutine;

    // Undo optimistic lock (independent of HUD FSM state)
    private int  _pendingSequenceId;
    private bool _undoLocked;

    // Serialized UI refs — [SerializeField] on private fields only (Unity 6.3)
    [SerializeField] private TextMeshProUGUI _moveCounterText;
    [SerializeField] private Button          _undoButton;
    [SerializeField] private Button          _hintButton;
    [SerializeField] private TextMeshProUGUI _coinDisplayText;
}

// LevelCompleteUI — MonoBehaviour on LevelCompleteUI Canvas (ADR-0008 hierarchy)
public class LevelCompleteUI : MonoBehaviour
{
    // Outbound events (Level Progression subscribes)
    public event Action<int> OnCoinRewardGranted;
    public event Action<int> OnCoinBonusGranted;
    public event Action      OnNextLevelRequested;
    public event Action      OnRetryRequested;
    public event Action      OnMenuRequested;

    // FSM
    private enum LcuiState { Hidden, Revealing, AdOffer, AdProcessing, Idle, Dismissed }
    private LcuiState _state = LcuiState.Hidden;

    // Ad watchdog (real-time elapsed, not frame time)
    private Coroutine _adWatchdogCoroutine;
    private float     _adProcessingStartRealtime;

    // Coin reward — computed once in OnEnable, fired immediately
    private int _coinsEarned;

    // Serialized UI refs
    [SerializeField] private GameObject _adOfferPanel;
    [SerializeField] private Button     _watchAdButton;
    [SerializeField] private Button     _skipAdButton;
    [SerializeField] private Button     _nextLevelButton;
    [SerializeField] private Button     _retryButton;
    [SerializeField] private Button     _menuButton;
}
```

## Alternatives Considered

### Alternative A: Separate Controller Classes (MVC)
- **Description**: Split each component into a view (`InGameHUDView.cs`) and a controller (`InGameHUDController.cs`). Controller owns FSM; view owns UI references.
- **Pros**: Controller is unit-testable without Unity runtime; clean separation of concerns
- **Cons**: Requires DI wiring between view and controller in prefab; additional Init() call or two-step setup; FSM logic is simple enough to test via Unity Test Framework MonoBehaviour fixtures without separation
- **Rejection Reason**: Over-engineered for a 4-state and 6-state FSM. Added indirection is not worth the prefab wiring cost.

### Alternative B: LevelCompleteUI Calls CE.AddCoins Directly
- **Description**: LevelCompleteUI holds `ICoinEconomy` reference; calls `AddCoins()` directly, eliminating the `OnCoinRewardGranted` event.
- **Pros**: Fewer moving parts; one fewer event subscription to wire
- **Cons**: Violates ADR-0006 coin_balance ownership. Creates a direct UI→Economy dependency; UI should not know about the Economy layer directly.
- **Rejection Reason**: Contradicts the `coin_balance` state ownership stance registered in `docs/registry/architecture.yaml`.

### Alternative C: WaitForSeconds for Ad Watchdog
- **Description**: `yield return new WaitForSeconds(timeout)` instead of real-time polling loop.
- **Pros**: Simpler coroutine implementation
- **Cons**: `WaitForSeconds` uses scaled game time and pauses during iOS/Android backgrounding. A phone call during an ad watch pauses the coroutine indefinitely — the player is soft-locked in AD_PROCESSING with navigation buttons disabled.
- **Rejection Reason**: Does not satisfy GDD E-09: "on resume, check whether `ad_watchdog_timeout_ms` has elapsed since AD_PROCESSING was entered." Only `Time.realtimeSinceStartup` satisfies this.

### Alternative D: Both HUD and LevelCompleteUI Query LDS Directly for par_moves
- **Description**: GSM payload stays as `(levelId, moveCount, sequenceId)`. Both UI components call `LevelDataSystem.Instance.GetLevel(levelId).ParMoves` independently on receiving `level_complete`.
- **Pros**: No GSM change required; each consumer fetches what it needs
- **Cons**: Two systems taking a synchronous LDS dependency that could otherwise be avoided; inconsistent with HUD GDD's explicit statement "GSM reads par_moves from LDS and includes it in the payload"
- **Rejection Reason**: Having GSM include `par_moves` (one O(1) lookup it already has access to) is preferable to two UI components taking independent LDS dependencies for the same field.

## Consequences

### Positive
- `level_complete` payload is canonical and consistent — GDD inconsistency resolved at the ADR layer before any code is written
- `OnCoinRewardGranted` fires in `OnEnable` before navigation is physically possible — AC-08 and AC-26 are architecturally guaranteed
- Ad watchdog correctly handles iOS backgrounding via `Time.realtimeSinceStartup` — no permanent AD_PROCESSING soft-lock
- `StarRatingCalculator` shared utility eliminates star-rating formula duplication
- LevelCompleteUI→LP event pattern respects `coin_balance` ownership (ADR-0006)

### Negative
- GSM WIN-01 requires a patch: GSM must read `par_moves` from LDS before emitting `OnLevelComplete`. This adds one O(1) dictionary lookup per level completion event to GSM's responsibilities.
- `level-complete-ui.md` dependency table must be updated: `par_moves` no longer sourced from LDS directly.
- Real-time polling watchdog (`yield return null` per frame after resume) is slightly more complex than `WaitForSeconds`.

### Risks
- **Risk**: `LevelProgression` not yet subscribed to `OnCoinRewardGranted` when `LevelCompleteUI.OnEnable` fires → missed coin delivery. **Mitigation**: ADR-0001 SEO table must include LevelProgression at a lower SEO value than LevelCompleteUI so it initializes and subscribes first. Flag this dependency to the ADR-0001 update when implementing.
- **Risk**: Stale `animation_complete(sequenceId)` arrives after `level_complete` (GDD E-11). **Mitigation**: `HandleAnimationComplete` checks `_state != HudState.Frozen` before any re-enable logic — stale signals are no-ops in FROZEN.
- **Risk**: Singleton Instance null during `OnDestroy` (app quit / scene reload order) → NullReferenceException on unsubscribe. **Mitigation**: All `OnDestroy` unsubscribes use null-guard `if (X.Instance != null)` — documented in Key Interfaces above and confirmed safe by engine specialist review.
- **Risk**: `par_moves` payload change breaks any existing GSM consumer expecting the 3-arg signature. **Mitigation**: Pre-implementation ADR — no GSM consumers exist yet. GSM GDD must be updated before any GSM implementation sprint begins.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| in-game-hud.md | HUD FSM: INACTIVE / IDLE / HINT_PROCESSING / FROZEN | Defined as private enum + switch in `InGameHUD.cs`; all transitions specified |
| in-game-hud.md | Pity grant counter (CE-13): session-only, owned by HUD | `_pityAttempts` and `_activeLevelId` fields in `InGameHUD`; not persisted |
| in-game-hud.md | Hint timeout coroutine: HUD-owned; no coin deducted on timeout | `_hintTimeoutCoroutine` pattern with null guard and `ExitHintProcessing()` |
| in-game-hud.md | F-05: star_rating used for pity grant evaluation | Calls `StarRatingCalculator.Compute()` in `HandleLevelComplete` |
| in-game-hud.md | OQ-01: `level_complete` must carry `par_moves` | Resolved — canonical payload `(levelId, moveCount, parMoves, sequenceId)` |
| in-game-hud.md | F-02: undo enabled = stack > 0 AND IDLE | Undo optimistic lock (`_undoLocked`) tracked independently of HUD FSM state |
| level-complete-ui.md | AC-08: `coin_reward_granted` fires in OnEnable before first frame | `OnCoinRewardGranted?.Invoke()` in `OnEnable` — Unity lifecycle guarantee |
| level-complete-ui.md | AC-09a–c: base delivery unaffected by ad / animation / navigation | Fires in `OnEnable` unconditionally before FSM or animation state exists |
| level-complete-ui.md | E-09: ad watchdog survives OS backgrounding | Real-time elapsed implementation; `WaitForSeconds` explicitly rejected |
| level-complete-ui.md | F-01: star rating — shared formula | `StarRatingCalculator.Compute()` used by both components; single implementation |
| game-state-manager.md | WIN-01: `level_complete` payload | Updated payload: `(levelId, moveCount, parMoves, sequenceId)` — GDD updated alongside this ADR |

## Performance Implications
- **CPU**: GSM reads `par_moves` from LDS O(1) dictionary before emitting `OnLevelComplete`: negligible. Pity grant counter: 3 comparisons per `level_complete`. Ad watchdog: 1 float subtraction per frame only during AD_PROCESSING. Hint timeout: 1 WaitForSeconds coroutine per hint tap, cancelled on result.
- **Memory**: `InGameHUD` additions: 1 int + 1 int + 1 Coroutine ref + 1 bool + 1 enum ≈ 20 bytes. `LevelCompleteUI` additions: 1 Coroutine ref + 1 float + 1 int + 1 enum ≈ 16 bytes. Negligible.
- **Load Time**: None.
- **Network**: N/A.

## Migration Plan
Pre-implementation ADR — no existing code to migrate. The following GDDs are updated alongside this ADR:
1. `game-state-manager.md` WIN-01: `level_complete` payload updated to `(level_id, move_count, par_moves, sequence_id)`
2. `level-complete-ui.md` Dependencies, Level Data System row: `par_moves` source updated from LDS direct read to event payload

## Validation Criteria
1. `OnCoinRewardGranted` fires before any `Update()` on `LevelCompleteUI` — `[UnityTest]` with frame boundary assertion
2. `StarRatingCalculator.Compute()` unit tests: all GDD F-01 boundary cases (par=10, threshold=1.5: move=10→3★; move=15→2★; move=16→1★; move=1→3★)
3. Ad watchdog real-time test: record `Time.realtimeSinceStartup` at AD_PROCESSING entry; simulate backgrounding; verify watchdog exits AD_PROCESSING after configured elapsed time on resume
4. Pity grant unit test: 5 consecutive 0-star completions on same level, `coin_balance < hint_cost` → exactly one `CE.AddCoins(50, -1, EarnSource.Base)` call; `_pityAttempts` resets to 0; 6th completion does not re-trigger
5. Hint timeout unit test: no `hint_result` received within `_hintTimeoutMs` → HUD exits HINT_PROCESSING; `hint_button` re-enables; no coin deducted
6. Singleton null guard test: `GameStateManager.Instance` set to null before `InGameHUD.OnDestroy` fires → no `NullReferenceException`

## Related Decisions
- ADR-0001: Boot sequence — LevelProgression SEO must be lower than LevelCompleteUI to guarantee `OnCoinRewardGranted` subscription before first `OnEnable`
- ADR-0002: Event and signal architecture — all subscriptions and event patterns
- ADR-0006: Board state representation — `ICoinEconomy` interface; `coin_balance` ownership
- ADR-0007: Input handling — `InputSystemUIInputModule` required for `Button.onClick`
- ADR-0008: UI hierarchy — Canvas structure and safe area; this ADR defines behaviour within that structure
- `design/gdd/in-game-hud.md`, `design/gdd/level-complete-ui.md`, `design/gdd/game-state-manager.md`
