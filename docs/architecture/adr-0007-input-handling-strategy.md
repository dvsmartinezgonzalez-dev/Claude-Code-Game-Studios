# ADR-0007: Input Handling Strategy

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Input |
| **Knowledge Risk** | MEDIUM — Legacy `Input` class is deprecated in Unity 6.x; Input System Package API is post-LLM-cutoff |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | `UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport`, `UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches`, `Keyboard.current.escapeKey.wasPressedThisFrame` — all Input System Package 1.x+ |
| **Verification Required** | (1) Confirm all scene EventSystems use `InputSystemUIInputModule` (not `StandaloneInputModule`). (2) Confirm `EnhancedTouchSupport.Enable()` is called before first `Update()` on physical Android/iOS device. (3) Confirm Android back button triggers escape key event on Android 13+ device (predictive back not opted-in). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (SortMechanic is a MonoBehaviour; its Awake/Update/OnDestroy are governed by SEO pattern); ADR-0006 (SortMechanic reads board state from GSM synchronously on tap) |
| **Enables** | None — terminal ADR for this input layer |
| **Blocks** | Sort Mechanic implementation sprint |
| **Ordering Note** | ADR-0006 must be Accepted first — SortMechanic reads `IReadOnlyList<int>[]` from GSM on each tap. |

## Context

### Problem Statement
BoltSort is a tap-only mobile game. Touch input must be converted from screen coordinates to world-space game objects (bolt stacks) via physics overlap queries, and the Android back button must map to move cancellation. Without explicit decisions on the input pipeline, developers may reach for the deprecated legacy `Input` class, missing the Input System Package requirement, or fail to configure UGUI correctly with the new input system.

### Constraints
- Legacy `Input` class is deprecated in Unity 6.x — `com.unity.inputsystem` is required
- `StandaloneInputModule` must be replaced with `InputSystemUIInputModule` on all scene EventSystems when using the Input System Package; UGUI buttons silently fail if this is missed
- Android 13+ introduced predictive back gesture; Unity's default (not opted-in) still routes back as `escapeKey` — opt-in would break this
- Touch targets must be ≥ 44pt (iOS HIG) / 48dp (Android) — enforced at prefab level via Collider2D size
- `Camera.main` in Unity 6+ uses a cached lookup (optimized from prior versions) — still recommended to cache in `Awake()` for clarity

### Requirements
- All touch detection via Input System Package — no legacy `Input.*` calls
- `EnhancedTouchSupport.Enable()` called before first `Update()` on SortMechanic
- Tap detection via `Physics2D.OverlapPoint()` — no scene-graph traversal or `FindObjectsByType`
- Android back button → cancellation of held bolt in BOLT_SELECTED state
- One-tap buffer during MOVE_EXECUTING animation — process on IDLE exit, discard on WIN/watchdog
- UGUI buttons (undo, hint, HUD) respond to touch via `InputSystemUIInputModule`

## Decision

### Input System Package Configuration

Project Settings → Player → Active Input Handling: **"Input System Package (New)"** (not "Both" — the legacy path should not be enabled to prevent accidental legacy usage).

All scenes with an `EventSystem` must have `InputSystemUIInputModule` on the EventSystem GameObject (not `StandaloneInputModule`). Unity replaces this automatically at package installation time, but any EventSystem created afterward requires manual replacement.

### Touch Input Pipeline

```csharp
// SortMechanic.Awake()
private Camera _mainCamera;
private LayerMask _boltStacksLayerMask;

private void Awake()
{
    EnhancedTouchSupport.Enable();   // required before Touch.activeTouches is available
    _mainCamera = Camera.main;       // cache — avoids FindAnyObjectByType per frame
    _boltStacksLayerMask = LayerMask.GetMask("BoltStacks");  // cache layer mask — avoids string lookup per frame
}

private void OnDestroy()
{
    EnhancedTouchSupport.Disable();  // balance the ref count; required if MonoBehaviour is destroyed
}

// SortMechanic.Update()
private void Update()
{
    // Android back gesture — process before tap to avoid both firing on same frame
    if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        HandleBackGesture();

    foreach (var touch in Touch.activeTouches)
    {
        if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;

        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(touch.screenPosition);
        var hit = Physics2D.OverlapPoint(worldPos, _boltStacksLayerMask);  // restricted to BoltStacks layer — avoids false positives on background/UI colliders
        if (hit == null) continue;

        var boltStack = hit.GetComponent<BoltStack>();
        if (boltStack != null)
            HandleTap(boltStack.StackIndex);
    }
}
```

**Tap pipeline:**
```
touch.screenPosition  (Input System — screen pixels)
    │
    ▼ Camera.main.ScreenToWorldPoint() [cached in Awake()]
    │
    ▼ Physics2D.OverlapPoint(worldPos) [layer mask: "BoltStacks"]
    │
    ▼ Collider2D → BoltStack component → StackIndex
    │
    ▼ SortMechanic.HandleTap(stackIndex)
         └── switch(currentState) { IDLE → try_select; BOLT_SELECTED → try_move; MOVE_EXECUTING → buffer; }
```

**Physics layer**: All `BoltStack` colliders must be on a dedicated `BoltStacks` physics layer. `Physics2D.OverlapPoint()` should be called with a `layerMask` argument to restrict to that layer only — avoids false positives on UI or other world-space colliders.

### One-Tap Buffer During MOVE_EXECUTING

```csharp
private bool _pendingTap;
private int _pendingTapStackIndex;

private void HandleTap(int stackIndex)
{
    switch (_currentState)
    {
        case SortMechState.MoveExecuting:
            // Buffer one tap; discard any existing buffer
            _pendingTap = true;
            _pendingTapStackIndex = stackIndex;
            break;
        // ... other states
    }
}

// Called on OnMoveExecutingExited (IDLE path only — not WIN, not watchdog)
private void ProcessPendingTap()
{
    if (!_pendingTap) return;
    _pendingTap = false;
    HandleTap(_pendingTapStackIndex);
}

// Called on WIN path and watchdog trigger
private void DiscardPendingTap()
{
    _pendingTap = false;
}
```

**Pending tap lifecycle:**
- Stored: when tap arrives during MOVE_EXECUTING
- Processed: when `OnMoveExecutingExited` fires (IDLE transition only) — `ProcessPendingTap()`
- Discarded: on WIN state (puzzle solved), on watchdog trigger (`OnBoardRefreshForced`) — `DiscardPendingTap()`

### Android Back Gesture

```csharp
private void HandleBackGesture()
{
    if (_currentState == SortMechState.BoltSelected)
        CancelHeldBolt();  // → CANCELLATION → IDLE; emits OnMoveCancelled
    // In other states: no-op (don't exit game on back press)
}
```

`Keyboard.current.escapeKey.wasPressedThisFrame` fires on the Android hardware back button press in Unity's Input System Package. This works correctly on Android 13+ as long as the project does NOT opt into `android:enableOnBackInvokedCallback` in the AndroidManifest.

> **Future risk**: Android 16 large-screen compliance requirements may require opting into predictive back. If that flag is added to the manifest, back gesture handling must migrate to `Application.onBackReceived` delegate or the Android `OnBackPressedCallback` API via a Unity plugin. Document this in the release checklist.

### UGUI Button Integration

HUD buttons (undo, hint) use UGUI `Button.onClick`. The Input System Package routes touch through the EventSystem, which must have `InputSystemUIInputModule` (not `StandaloneInputModule`). No custom input code is needed for UGUI buttons — they work through the Event System automatically.

**Acceptance criterion** (from engine specialist note): All scenes containing an EventSystem must be verified to have `InputSystemUIInputModule` on the EventSystem GameObject before the Sort Mechanic story can be marked Done.

### Touch Target Size Enforcement

All tappable `BoltStack` colliders must have `Collider2D` dimensions ≥ the minimum touch target:
- iOS HIG: ≥ 44 points × 44 points
- Android Material: ≥ 48dp × 48dp

These are enforced at the prefab level — the `BoltStack.prefab` `Collider2D` size must satisfy both constraints at the minimum level layout (most stack columns per screen). Validated by a visual layout audit at each new level tier boundary.

### Architecture Diagram

```
Physical touch
    │
    ▼
[iOS/Android OS touch layer]
    │
    ▼
Unity Input System Package
    ├── EnhancedTouch.Touch.activeTouches → phase.Began events → SortMechanic.Update()
    └── Keyboard.current.escapeKey → SortMechanic.Update() → HandleBackGesture()

SortMechanic.Update() (per-frame, SEO 0)
    │
    ├── Back gesture → BOLT_SELECTED → CANCELLATION [OnMoveCancelled event]
    │
    └── Tap → Physics2D.OverlapPoint(worldPos)
                 └── Collider2D hit
                      └── BoltStack component
                           └── HandleTap(stackIndex)
                                ├── IDLE → try select bolt
                                ├── BOLT_SELECTED → move validation → MOVE_EXECUTING [OnMoveCommitted]
                                └── MOVE_EXECUTING → buffer tap

[After OnMoveExecutingExited (IDLE path)]
    └── ProcessPendingTap() → HandleTap(_pendingTapStackIndex)

UGUI (undo, hint buttons)
    └── InputSystemUIInputModule on EventSystem
         └── Button.onClick → GSM.UndoRequested() / CoinEconomy.SpendCoins(hintCost)
```

### Key Interfaces

```csharp
// Required: EnhancedTouchSupport namespace
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

// Required: Input System Package in Project Settings
// Active Input Handling: "Input System Package (New)"

// All scenes: EventSystem must have InputSystemUIInputModule (not StandaloneInputModule)
```

## Alternatives Considered

### Alternative A: Legacy `Input` Class
- **Description**: `Input.GetMouseButtonDown(0)` + `Input.mousePosition` for tap; `Input.GetKeyDown(KeyCode.Escape)` for back
- **Pros**: Familiar; zero configuration
- **Cons**: Deprecated in Unity 6.x; will be removed in a future version; does not support multi-touch correctly on mobile; cannot coexist cleanly with Input System Package on the same project
- **Rejection Reason**: Deprecated. Technical-preferences.md explicitly requires Input System Package.

### Alternative B: Unity UI `IPointerDownHandler` / `OnPointerClick` on World-Space Colliders
- **Description**: Implement `IPointerDownHandler` on each `BoltStack` MonoBehaviour; Unity's EventSystem handles the tap-to-object mapping via Raycasting
- **Pros**: No explicit `Physics2D.OverlapPoint()` call; cleaner component design for simple taps
- **Cons**: Requires a `Physics2D Raycaster` component on Camera to route EventSystem 2D hits; adds a raycaster + EventSystem overhead per frame even when no tap occurs; `OverlapPoint` is more direct and has lower overhead for single-tap detection; `OnPointerDown` fires on touch-down, not touch-began filter
- **Rejection Reason**: `Physics2D.OverlapPoint` is architecturally simpler and more explicit; the architecture doc specified this approach; EventSystem raycasting adds unnecessary overhead for a single-touch puzzle game.

### Alternative C: Touch input via Unity's new `InputAction` asset (Action Maps)
- **Description**: Define a `TouchAction` in an Input Action Asset (`.inputactions` file); bind to `<Touchscreen>/primaryTouch/position`; read via `InputAction.ReadValue<Vector2>`
- **Pros**: Cleanly separates input bindings from code; supports rebinding; more idiomatic for complex input schemes
- **Cons**: Overkill for a tap-only single-touch game; adds `.inputactions` asset configuration; generating action callbacks for position-based touch requires additional plumbing that `Touch.activeTouches` handles directly
- **Rejection Reason**: `EnhancedTouchSupport` is simpler and more direct for a single-touch puzzle game. Action Maps add configuration overhead without benefit.

## Consequences

### Positive
- Input System Package is the forward-compatible path in Unity 6.x
- `Touch.activeTouches` is zero-allocation per frame
- `Physics2D.OverlapPoint()` is a single physics API call per tap — no scene traversal
- One-tap buffer correctly gates MOVE_EXECUTING animation without stacking input

### Negative
- `InputSystemUIInputModule` must be verified on every scene's EventSystem — a common forget-and-debug trap when creating new scenes
- Android 13+ predictive back gesture will require migration if the manifest opt-in is added (must be tracked as a release checklist item)
- `EnhancedTouchSupport.Enable()` / `Disable()` balance must be maintained — if SortMechanic is destroyed and recreated mid-session (unlikely), a mismatch will suppress touch events

### Risks
- **Risk**: EventSystem in a new scene created after package installation has `StandaloneInputModule` → UGUI buttons don't respond to touch → silent failure (buttons visually present but non-functional). **Mitigation**: Acceptance criterion on every story touching scene creation: verify `InputSystemUIInputModule` on EventSystem; control manifest rule.
- **Risk**: `EnhancedTouchSupport.Disable()` not called in `OnDestroy()` → ref count leak → if `Enable()` is called again (e.g., test scene reloads), count is doubled; `activeTouches` may behave unexpectedly. **Mitigation**: `Disable()` in `OnDestroy()` is documented here; code review enforces it.
- **Risk**: Android 16 large-screen manifest requirement adds `enableOnBackInvokedCallback` → back gesture stops firing as escapeKey → bolt cancellation silently breaks. **Mitigation**: Release checklist must track this flag; if added, migrate to `Application.onBackReceived`.
- **Risk**: `Physics2D.OverlapPoint()` hits a collider not on the `BoltStacks` layer (e.g., background sprite with a physics collider) → wrong BoltStack component (null → tap silently ignored, or wrong component). **Mitigation**: Layer mask parameter on `OverlapPoint()` restricts hits to `BoltStacks` layer only.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| sort-mechanic.md | TR-SORT-004: One-tap buffer during MOVE_EXECUTING; discard on WIN | `_pendingTap` buffer documented; process on `OnMoveExecutingExited` (IDLE), discard on WIN and watchdog |
| sort-mechanic.md | TR-SORT-008: Android back gesture → cancellation in BOLT_SELECTED | `Keyboard.current.escapeKey.wasPressedThisFrame` → `CancelHeldBolt()` |
| sort-mechanic.md | TR-SORT-009: Synchronous pull of board state from GSM | `SortMechanic.HandleTap()` calls `GSM.StackContents[i]` synchronously on same frame as tap |
| in-game-hud.md | TR-HUD-001: UGUI Canvas, Screen Space-Overlay | `InputSystemUIInputModule` ensures UGUI buttons route through Input System Package touch |

## Performance Implications
- **CPU**: `Touch.activeTouches` read: zero allocation per frame (native buffer). `Physics2D.OverlapPoint()`: single physics query per tap (~0.01ms). `Camera.main.ScreenToWorldPoint()`: single matrix multiply (cached camera). Total input processing per frame: negligible.
- **Memory**: `_pendingTap` bool + int: 5 bytes. `_mainCamera` reference: 8 bytes (pointer). No collections.
- **Load Time**: `EnhancedTouchSupport.Enable()` in `Awake()`: ~0.01ms.
- **Network**: N/A

## Migration Plan
No existing code to migrate — written before implementation begins.

## Validation Criteria
1. Device test (iOS + Android): tap on BoltStack triggers `HandleTap(stackIndex)` — verified by log
2. Device test (Android): hardware back button in BOLT_SELECTED state triggers CANCELLATION (`OnMoveCancelled` fires)
3. Device test: tap during MOVE_EXECUTING stores pending tap; tap is processed after animation completes (IDLE exit)
4. Device test: tap during MOVE_EXECUTING discarded if level completes (WIN path)
5. Scene audit: all EventSystems in project use `InputSystemUIInputModule` — verified before story Done
6. Device test (Android 13+): back button fires escape key event; predictive back NOT intercepted

## Related Decisions
- ADR-0001: Singleton Architecture — SortMechanic is at SEO 0, not a DDOL singleton
- ADR-0006: Board State Representation — SortMechanic reads `StackContents` synchronously on tap
- ADR-0008: UI Hierarchy and Safe Area — UGUI Canvas configuration; touch targets and safe area
- `design/gdd/sort-mechanic.md` — FSM states and input contract
- `design/gdd/in-game-hud.md` — HUD button touch handling
