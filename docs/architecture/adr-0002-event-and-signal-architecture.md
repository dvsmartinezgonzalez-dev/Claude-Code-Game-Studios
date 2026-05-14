# ADR-0002: Event and Signal Architecture

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW — C# `event Action<T>` is a language feature; no Unity 6.x breaking changes affect it |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md` |
| **Post-Cutoff APIs Used** | None — `event Action<T>`, `?.Invoke()`, and delegate subscription are stable across all Unity versions |
| **Verification Required** | Confirm subscribe-then-check pattern for `OnLevelLoaded` on a physical Android device with a cached save file (fast OnSaveReady path — verify HUD initializes correctly) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (singletons initialized in SEO-enforced order — events are subscribed in manager Awake methods that rely on this ordering) |
| **Enables** | ADR-0006 (GSM design assumes typed C# events as the communication channel), ADR-0009 (Animation Strategy assumes `OnAnimationComplete(seqId)` contract) |
| **Blocks** | All implementation sprints — no inter-system communication may be implemented until this ADR is Accepted |
| **Ordering Note** | ADR-0001 must be Accepted before this ADR can be implemented (subscription relies on static Instance access). |

## Context

### Problem Statement
BoltSort has 11+ system MonoBehaviours that must communicate asynchronously across layer boundaries (board state changes, move processing, animation completion, coin balance updates, save readiness). Without a standardized communication pattern, implementations will mix direct method calls, `UnityEvent`, and ad-hoc callbacks — producing inconsistent coupling, untestable systems, and hidden dependencies.

### Constraints
- Unity 6.3 LTS: `[SerializeField]` on a property or method is a **compile error** — event field declarations must use `event Action<T>` field syntax, never with `[SerializeField]`
- No third-party messaging frameworks in the allowed-library list (`CLAUDE.md`)
- 60 fps target on mobile: event invocation pattern must allocate zero GC per call on the hot path
- All game state mutations must occur on the main thread; no event callbacks may mutate state from a background thread
- Events flow directionally: Presentation layer never holds a direct reference to Core or Foundation instances

### Requirements
- Typed event signatures: no `object` or `params` arguments — all parameter types must be concrete
- Null-safe invocation: `?.Invoke()` pattern mandatory — not `if (event != null) event(...)`
- Subscribe in `Awake`, not `Start` — ensures subscription is in place before the first frame's Update
- Scene-loaded MonoBehaviours must unsubscribe in `OnDestroy` — prevents `MissingReferenceException`
- Sequence ID guard: consumers of timed/async events that carry a sequence ID must validate it before acting

## Decision

All inter-system communication in BoltSort uses **C# typed `event Action<T>` delegates declared on MonoBehaviour classes**. No `UnityEvent`, ScriptableObject event channels, or central EventBus.

### Subscription Rules

**Rule 1 — Subscribe in Awake, using static Instance access (from ADR-0001).**

```csharp
// In InGameHUD.Awake() [SEO 0]
GameStateManager.Instance.OnLevelLoaded       += HandleLevelLoaded;
GameStateManager.Instance.OnBoardStateChanged += HandleBoardStateChanged;
GameStateManager.Instance.OnLevelComplete     += HandleLevelComplete;  // (levelId, moveCount, parMoves, sequenceId)
AnimationSystem.Instance.OnAnimationComplete  += HandleAnimationComplete;
CoinEconomy.Instance.OnCoinBalanceChanged     += HandleCoinBalanceChanged;
SortMechanic.Instance.OnDeadlockDetected      += HandleDeadlockDetected;
```

**Rule 2 — Subscribe-then-check for all events fired from async callbacks.**

Any event that can fire as a result of an async callback (e.g., `OnSaveReady` dispatched from a background thread, or `OnLevelLoaded` fired from `LevelProgression.OnSaveReady`) may arrive before or after SEO-0 `Awake` consumers subscribe on fast devices. Consumers of these events must use subscribe-then-check:

```csharp
// In InGameHUD.Awake() — subscribe-then-check for OnLevelLoaded
GameStateManager.Instance.OnLevelLoaded += HandleLevelLoaded;
// Catch-up: if level is already loaded before we subscribed (e.g., fast save read)
if (GameStateManager.Instance.CurrentState == GSMLifecycleState.Active)
    HandleLevelLoaded(GameStateManager.Instance.CurrentLevelId,
                      GameStateManager.Instance.ColorCount);
```

Events requiring subscribe-then-check in their consumers:
- `SaveSystem.OnSaveReady` — fired from MainThreadDispatcher after background file read
- `GSM.OnLevelLoaded` — fired from `LevelProgression.OnSaveReady` callback; may complete before SEO-0 Awake on fast devices

**Rule 3 — Unsubscribe in OnDestroy for scene-loaded MonoBehaviours.**

Failure to unsubscribe causes `MissingReferenceException` — not a silent GC leak. When a scene-loaded MonoBehaviour is destroyed without unsubscribing, the delegate list on the DDOL producer retains a reference to the destroyed instance. Unity's `==` operator on a destroyed MonoBehaviour returns `true` for null comparison, but the delegate itself is not null — the next event fire invokes the destroyed object's method, throwing `MissingReferenceException`.

```csharp
// Scene-loaded MonoBehaviours must implement:
private void OnDestroy()
{
    GameStateManager.Instance.OnLevelLoaded       -= HandleLevelLoaded;
    GameStateManager.Instance.OnBoardStateChanged -= HandleBoardStateChanged;
    // ... all subscriptions from Awake
}
```

**Rule 4 — DDOL-to-DDOL subscriptions: no unsubscribe needed.**

Manager singletons that are `DontDestroyOnLoad` and subscribe to other DDOL managers do not need to unsubscribe — both persist for the app's lifetime. Examples: `CoinEconomy` subscribing to `SaveSystem.OnSaveReady`, `LevelProgression` subscribing to `GSM.OnLevelComplete` (4-arg: levelId, moveCount, parMoves, sequenceId).

**Important scope boundary for Beta:** When Beta UI screens (MainMenuUI, LevelSelectUI, ShopUI) are added, they are scene-loaded and will consume events from DDOL producers. They MUST unsubscribe in `OnDestroy`. The DDOL exemption applies only to DDOL-to-DDOL subscriptions, not to any scene-loaded consumer.

### Invocation Rules

**Rule 5 — Use `?.Invoke()` for all event invocations.**

```csharp
// CORRECT
OnLevelLoaded?.Invoke(levelId, colorCount);

// FORBIDDEN — race condition pattern (even on single thread, bad habit)
if (OnLevelLoaded != null) OnLevelLoaded(levelId, colorCount);
```

`?.Invoke()` captures a snapshot of the delegate reference before the null check, preventing the race condition where a subscriber unsubscribes between the null check and invocation. It is also shorter and the idiomatic modern C# form.

**Rule 6 — No lambda or anonymous method subscribers.**

```csharp
// FORBIDDEN — allocates a closure object every time this line executes
GSM.OnLevelLoaded += (id, count) => _levelId = id;

// REQUIRED — named instance method, zero GC allocation at subscription time
GSM.OnLevelLoaded += HandleLevelLoaded;
```

Lambda subscribers allocate a closure object. Subscriptions made in `Awake` (which runs once) do not cause per-frame allocation, but using lambdas prevents unsubscription in `OnDestroy` (the lambda reference is not stored) — this causes the `MissingReferenceException` failure described in Rule 3.

**Rule 7 — Never place `[SerializeField]` on an event field.**

Unity 6.3 LTS generates a compile error if `[SerializeField]` is applied to a property or method. While `event Action<T>` is a field (not a property), the same restriction applies — events are not inspector-serializable in any Unity version, and attempting to mark one with `[SerializeField]` is a compile error in 6.3. No event declaration in this project may carry `[SerializeField]`.

### Layer Communication Rules

Presentation never holds a direct reference to Core or Foundation instances. It subscribes to their events and calls methods only via exposed interfaces (see `IGameStateManager`, `ICoinEconomy`, etc. in ADR-0006).

| From → To | Allowed? | Pattern | Example |
|-----------|---------|---------|---------|
| Foundation → Core | ✓ | event subscription | `SaveSystem.OnSaveReady` → `CoinEconomy`, `LevelProgression` |
| Foundation → Feature | ✓ | event subscription | `SaveSystem.OnSaveReady` → `LevelProgression` |
| Core → Feature | ✓ | event subscription | `GSM.OnBoardStateChanged` → `AnimationSystem` |
| Core → Presentation | ✓ | event subscription | `GSM.OnLevelLoaded` → `InGameHUD`; `CE.OnCoinBalanceChanged` → `InGameHUD` |
| Feature → Core | ✓ | event subscription | `SortMechanic.OnMoveCommitted` → `GSM` (move processing pipeline) |
| Feature → Feature | ✓ | event subscription | `AnimationSystem.OnAnimationComplete` → `SortMechanic` |
| Feature → Presentation | ✓ | event subscription | `AnimationSystem.OnAnimationComplete` → `InGameHUD` |
| Presentation → Core | ✗ | FORBIDDEN | HUD must not hold a direct `GameStateManager` reference; calls `GSM.UndoRequested()` via `IGameStateManager` interface only |
| Presentation → Foundation | ✗ | FORBIDDEN | — |

### Sequence ID Guard

For events that carry a sequence ID to coordinate timed async flows, consumers must discard stale signals:

```csharp
// In SortMechanic: receives AnimationSystem.OnAnimationComplete
private void HandleAnimationComplete(int seqId)
{
    if (seqId != _currentMoveExecutingSeqId) return;  // stale — discard
    // proceed with MOVE_EXECUTING exit logic
}
```

Events requiring sequence ID guard in their consumers:
- `AnimationSystem.OnAnimationComplete(seqId)` — consumed by SortMechanic (MOVE_EXECUTING exit) and InGameHUD (undo button re-enable)
- `GSM.OnBoardStateChanged(sequenceId, moveCount)` — AnimationSystem should guard if it tracks `activeSequenceId`
- `GSM.OnBoardRefreshForced(seqId)` — AnimationSystem consumes; seqId should match the in-flight animation. Guard recommended against race with new level load.

### `OnMoveExecutingExited` Constraint (TR-SORT-007)

`SortMechanic.OnMoveExecutingExited(seqId)` is emitted **only** on the MOVE_EXECUTING → IDLE state transition. It is **not** emitted on:
- MOVE_EXECUTING → WIN (win path — `OnPuzzleSolved` is emitted instead)
- Watchdog-triggered board refresh (GSM fires `OnBoardRefreshForced` instead)

This distinction is critical: GSM's deferred undo logic triggers on `OnMoveExecutingExited`. Emitting it on the WIN path would attempt undo processing after a completed level — a correctness bug.

### Complete Event Catalog

```csharp
// FOUNDATION LAYER

// SaveSystem
public event Action OnSaveReady;

// CORE LAYER

// GameStateManager
public event Action<int, int> OnLevelLoaded;         // (levelId, colorCount)
public event Action<int, int> OnBoardStateChanged;   // (sequenceId, moveCount) — fires on every board mutation + undo
public event Action<int>      OnBoardRefreshForced;  // (sequenceId) — watchdog fired
public event Action<int, int, int, int> OnLevelComplete;  // (levelId, moveCount, parMoves, sequenceId) — canonical per ADR-0012
public event Action<int>      OnLevelUnloaded;       // (levelId) — emitted on TEARDOWN; consumers release level-scoped resources
public event Action           OnSessionLoadFailed;

// CoinEconomy
public event Action<int, int> OnCoinBalanceChanged;  // (newBalance, delta)

// FEATURE LAYER

// SortMechanic
public event Action<int, int, int, int>              OnMoveCommitted;       // (src, dst, colorId, seqId)
public event Action<int, int>                        OnMoveCancelled;       // (src, colorId)
public event Action<int, int, int, MoveRejectReason> OnMoveRejected;        // (src, dst, colorId, reason)
public event Action<int>                             OnPuzzleSolved;        // (moveCount)
public event Action                                  OnDeadlockDetected;
public event Action<int>                             OnMoveExecutingExited; // (seqId) — IDLE exit path ONLY

// AnimationSystem
public event Action<int> OnAnimationComplete;  // (seqId)

// LevelProgression
public event Action<int, int, int, int> OnLevelCompleted;  // (stars, levelId, moveCount, parMoves)
```

### Architecture Diagram

```
FOUNDATION                   CORE                          FEATURE                    PRESENTATION
─────────                    ────                          ───────                    ────────────
SaveSystem                   GameStateManager              SortMechanic               InGameHUD
  OnSaveReady ──────────────► CoinEconomy.HandleSaveReady    OnMoveCommitted ────────► GSM.HandleMoveCommitted
              ──────────────► LevelProgression.HandleReady   OnMoveRejected ─────────► AnimationSystem.HandleRejected
                               └── GSM.LoadLevel()           OnMoveCancelled ────────► AnimationSystem.HandleCancelled
                                    └── OnLevelLoaded ──────► AnimationSystem.Reset    OnPuzzleSolved ─────────► GSM.HandleSolved
                                         ──────────────────► InGameHUD.HandleLoaded★   OnDeadlockDetected ─────► InGameHUD.HandleDeadlock
                              OnBoardStateChanged ──────────► AnimationSystem.Snap      OnMoveExecutingExited ──► GSM.HandleExited
                               ──────────────────────────► InGameHUD.UpdateMoveCount
                              OnBoardRefreshForced ────────► AnimationSystem.Abort    AnimationSystem
                              OnLevelComplete ─────────────► AnimationSystem.Celebrate  OnAnimationComplete ────► SortMechanic.HandleAnim★
                               ──────────────────────────► InGameHUD.Freeze              ──────────────────────► InGameHUD.ReEnableUndo★

★ = subscribe-then-check required    ★★ = sequence ID guard required (all OnAnimationComplete consumers)
```

## Alternatives Considered

### Alternative A: `UnityEvent`
- **Description**: Replace `event Action<T>` with Unity's `UnityEvent<T>` (serializable, inspector-hookable, usable in `[SerializeField]` fields)
- **Pros**: Inspector-visible; non-programmers can wire up responses in the editor; supports persistent (serialized) listeners
- **Cons**: Generates GC allocations per `Invoke()` call (internal iterator object); ~3–5× slower than C# delegates in benchmarks; adds serialization bloat to MonoBehaviours; inspector-wired events are invisible to code search ("Find All References")
- **Rejection Reason**: GC allocation per move event is unacceptable at 60fps on mobile. The project has no visual-scripting users who need inspector wiring.

### Alternative B: ScriptableObject Event Channels
- **Description**: Each event is a ScriptableObject asset (e.g., `OnLevelLoadedEvent.asset`). Producers call `asset.Raise(...)`. Consumers register a `GameEventListener` component referencing the asset.
- **Pros**: Complete decoupling — producers and consumers never reference each other; events are browsable assets; testable in isolation
- **Cons**: Requires 10+ extra ScriptableObject assets; subscription order is not guaranteed; harder to trace subscribers at runtime; additional tooling needed to view all listeners; overkill for a fixed, documented, 6-singleton architecture
- **Rejection Reason**: Complexity cost exceeds benefit for a project with a stable, documented system hierarchy. The singleton pattern (ADR-0001) already makes system discovery trivial via static `Instance`.

### Alternative C: Central EventBus / Message Broker
- **Description**: A singleton `EventBus` with generic `Subscribe<T>` / `Publish<T>` methods. Systems never reference each other — only the bus.
- **Pros**: Full decoupling; easy to add new consumers without modifying producers
- **Cons**: Subscription order not guaranteed; event type identity relies on `System.Type` comparison (runtime overhead or boxing); debugging subscribers requires custom tooling; no compile-time guarantee that publishers and subscribers use the same type parameter; not in allowed-library list
- **Rejection Reason**: Type safety degrades under refactoring; subscription ordering is non-deterministic; adds infrastructure complexity without meaningful benefit over typed events on a documented singleton hierarchy.

## Consequences

### Positive
- Zero GC allocation per event invocation after initial subscription
- Strong typing: compiler catches signature mismatches at build time
- Easy subscriber discovery: IDE "Find All References" on any event field shows all consumers
- No third-party dependencies
- Subscribe-then-check pattern (established in ADR-0001) applies uniformly to all async-fired events — one mental model

### Negative
- Scene-loaded consumers must unsubscribe in `OnDestroy` — forgetting causes `MissingReferenceException` (not a silent failure; the bug is loud but must be fixed per-class)
- Sequence ID guards add per-event boilerplate to timed consumers; incorrect seqId checks cause silent state machine bugs
- The `event` keyword on fields prevents external invocation (only the declaring class can call `?.Invoke()`), which is correct and intentional but can surprise developers who expect to fire events from test code — test harnesses must use a test double or expose a dedicated `RaiseXxxForTesting()` method

### Risks
- **Risk**: Developer adds `[SerializeField]` to an event field → Unity 6.3 compile error, blocks entire build. **Mitigation**: Rule-7 in this ADR; control manifest will list this as FORBIDDEN; CI full-compile gate on every push.
- **Risk**: Scene-loaded Beta UI (ShopUI, MainMenuUI, LevelSelectUI) added without implementing `OnDestroy` unsubscription → `MissingReferenceException` in production on level transitions. **Mitigation**: Control manifest will list "every scene-loaded MonoBehaviour that subscribes in Awake MUST unsubscribe in OnDestroy" as a REQUIRED rule. Code review gate enforces it.
- **Risk**: `OnLevelLoaded` fires before a new HUD consumer subscribes (fast save-read path on repeat launches). **Mitigation**: Subscribe-then-check pattern (Rule 2) is mandatory for all `OnLevelLoaded` and `OnSaveReady` consumers — catch-up call handles the already-fired case.
- **Risk**: Lambda subscriber used in a hot-path → GC pressure at 60fps. **Mitigation**: Rule-6 bans lambdas as subscribers; code review enforces named method pattern.
- **Risk**: `OnMoveExecutingExited` emitted on wrong state transition (WIN or watchdog) → GSM deferred undo triggered after a solved level → board corruption. **Mitigation**: TR-SORT-007 constraint documented explicitly in this ADR; `OnMoveExecutingExited` must be guarded with a state check (`currentState == MOVE_EXECUTING && nextState == IDLE`) in SortMechanic's FSM transition table.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| sort-mechanic.md | TR-SORT-006: Sequence ID stale-signal guard on `animation_complete` | Mandates sequence ID guard for all `OnAnimationComplete` consumers; defines the check pattern |
| sort-mechanic.md | TR-SORT-007: `move_executing_exited` on IDLE exit only (not WIN, not watchdog) | Explicitly documents this constraint; failure mode described in Risks |
| game-state-manager.md | TR-GSM-009: Emit typed C# events | Establishes `event Action<T>` as the project-wide event type; defines all GSM event signatures |
| animation-system.md | TR-ANIM-006: Emit `animation_complete(seqId)` | Defines `AnimationSystem.OnAnimationComplete(int seqId)` signature and the seqId contract |
| in-game-hud.md | TR-HUD-007: Emit `UndoRequested` to GSM | Documents that HUD calls `GSM.UndoRequested()` via `IGameStateManager` interface — a direct method call, not an event, consistent with the layer communication table |

## Performance Implications
- **CPU**: Zero GC allocation per event invocation after subscription. `?.Invoke()` is a single virtual dispatch. At 3–5 taps/second, total event overhead is negligible vs. 16.6ms frame budget.
- **Memory**: Each subscription stores one delegate object (~64 bytes). 20–30 total subscriptions across all systems ≈ ~2KB.
- **Load Time**: Subscriptions in Awake complete in <1ms. No load-time impact.
- **Network**: N/A

## Migration Plan
No existing code to migrate — this ADR is written before implementation begins. All inter-system communication from the first commit follows this pattern.

## Validation Criteria
1. Unit test: Verify `OnLevelLoaded` subscribe-then-check — fire `OnLevelLoaded` before subscribing; consumer handler must still be called correctly
2. Unit test: Verify sequence ID guard — emit `OnAnimationComplete(0)` when consumer expects seqId 1; handler must not fire
3. Unit test: Verify `OnMoveExecutingExited` is NOT emitted on WIN path — transition to WIN state; event must not fire
4. CI: Compile check confirms no `[SerializeField]` on any event field in the codebase
5. Manual: Destroy a scene-loaded HUD object and verify no `MissingReferenceException` on next `OnBoardStateChanged` fire

## Related Decisions
- ADR-0001: Singleton Architecture and Boot Sequence — subscription relies on `System.Instance` static access established there
- ADR-0006: Board State Representation and GSM Design — GSM event implementations depend on this pattern
- ADR-0009: Bolt Animation Strategy — `OnAnimationComplete(seqId)` contract is foundational to the animation pipeline
- `docs/architecture/architecture.md` — Event catalog and data flow diagrams are the source this ADR formalizes
