# ADR-0008: UI Hierarchy and Safe Area

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | UI (UGUI Canvas) |
| **Knowledge Risk** | LOW — UGUI Canvas, Screen.safeArea, Canvas Scaler, TextMeshProUGUI are all stable APIs unchanged in Unity 6.x |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) Confirm safe area anchors on physical iPhone with notch + home indicator. (2) Confirm safe area on Android device with navigation bar (pill gesture). (3) Validate touch target sizes on Samsung Galaxy A series (≥48dp physical pixels). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (InGameHUD and LevelCompleteUI are MonoBehaviours at SEO 0; EventSystem wired per ADR-0007); ADR-0007 (EventSystem must have InputSystemUIInputModule for UGUI button clicks) |
| **Enables** | None — terminal ADR for UI layer |
| **Blocks** | InGameHUD implementation sprint; LevelCompleteUI implementation sprint |
| **Ordering Note** | ADR-0007 must be Accepted first — InputSystemUIInputModule configuration applies to the EventSystem used by all Canvases. |

## Context

### Problem Statement
BoltSort runs on iOS and Android devices with varying notch/Dynamic Island shapes, gesture navigation bars, and home indicators. UI elements placed in full-screen canvas without safe area handling will be partially obscured or unreachable. Without an explicit Canvas hierarchy and sort order, HUD and Level Complete overlay may render in an undefined order, especially when both are active during the level completion transition.

### Constraints
- iOS notch, Dynamic Island, home indicator — `Screen.safeArea` is the only Unity-provided API for these insets; no platform-specific APIs available in managed C#
- Android navigation bar (gesture pill mode) shrinks the safe area from the bottom
- `Screen.safeArea` fires `OnRectTransformDimensionsChange` on rect change; BoltSort is portrait-locked, so orientation changes are uncommon but must not break layout
- Legacy `UI.Text` is deprecated; all text must use TextMeshProUGUI

### Requirements
- All Canvas overlays must implement safe area handling (technical-preferences.md)
- HUD renders below Level Complete overlay when both visible
- Touch targets ≥ 44pt (iOS HIG) / 48dp (Android Material) on all interactable elements
- Portrait-locked orientation (no landscape support at MVP)
- `InputSystemUIInputModule` on EventSystem (ADR-0007)

## Decision

### Canvas Mode: Screen Space-Overlay

All UI Canvases use **Screen Space-Overlay** mode. No camera reference required; renders on top of all world-space content automatically; eliminates camera-ordering bugs.

### Canvas Sort Order

| Sort Order | Canvas | Contents |
|-----------|--------|---------|
| 0 | InGameHUD | Move counter, undo button, hint button, coin display |
| 1 | LevelCompleteUI | Star rating, coin animation, ad flow, next-level button |
| 2 | *(reserved)* | Future toast/notification layer (e.g., no-network warning, pity grant notice) |

Higher sort order renders on top. LevelCompleteUI (sort order 1) overlays InGameHUD (sort order 0) during the level completion transition — both Canvases may be simultaneously active, which is the intended behavior.

**Two-Canvas architecture rationale**: Separating HUD and LevelCompleteUI into distinct Canvases means UGUI's re-batching only affects one Canvas at a time. HUD element updates (move counter, coin display) never dirty the LevelComplete batch, and vice versa. This is preferable to a single shared Canvas with child panels.

### Canvas Scaler

Both Canvases use the same `CanvasScaler` settings:

| Setting | Value |
|---------|-------|
| UI Scale Mode | Scale with Screen Size |
| Reference Resolution | 1080 × 1920 (portrait 16:9) |
| Screen Match Mode | Match Width or Height |
| Match | 0.5 (balanced) |

Match 0.5 blends width and height scaling equally, handling height variance between tall (21:9) and standard (16:9) devices without distortion.

### Safe Area Implementation

Every Canvas that contains gameplay UI elements (InGameHUD, LevelCompleteUI) must have a **SafeAreaPanel** as the direct child of the Canvas root. All UI elements that must respect device insets are children of SafeAreaPanel, not direct Canvas children.

```csharp
public class SafeAreaPanel : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _lastSafeArea;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (Screen.safeArea != _lastSafeArea)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        _lastSafeArea = Screen.safeArea;
        var safeArea = Screen.safeArea;
        var screenSize = new Vector2(Screen.width, Screen.height);
        _rectTransform.anchorMin = new Vector2(safeArea.x / screenSize.x,
                                               safeArea.y / screenSize.y);
        _rectTransform.anchorMax = new Vector2((safeArea.x + safeArea.width) / screenSize.x,
                                               (safeArea.y + safeArea.height) / screenSize.y);
    }
}
```

`Screen.safeArea` returns pixel coordinates; dividing by `Screen.width`/`Screen.height` converts to normalized anchor coordinates. This matches the pattern confirmed in `docs/engine-reference/unity/current-best-practices.md`.

**Canvas elements that do NOT need safe area**: Full-screen background panels and decorative elements that intentionally bleed under the notch/bars. These are direct Canvas children (siblings of SafeAreaPanel), not children of SafeAreaPanel.

### Text: TextMeshProUGUI

All in-game text uses `TextMeshProUGUI`. Legacy `UI.Text` is **forbidden** — it is deprecated in Unity 6.x and produces inferior rendering on high-DPI mobile screens. All font assets must be TMP_FontAsset.

### Touch Target Size Enforcement

All interactable UI elements (buttons, toggle areas) must have a minimum hit area of:
- **44pt × 44pt** on iOS (per iOS HIG)
- **48dp × 48dp** on Android (per Material Design)

At reference resolution 1080×1920, 1pt ≈ 3px on a 3x screen, 1dp ≈ 3px on a medium-density Android. Minimum button size at reference resolution: ~132×132px (≈44pt/48dp at 3x density).

Enforced by: setting the `RectTransform` size of each button to ≥132×132 in the prefab inspector. Validated on Galaxy A series (mid-range Android target) and iPhone 14 (3x screen).

### Architecture Diagram

```
Canvas [Screen Space-Overlay, Sort Order 0]     Canvas [Screen Space-Overlay, Sort Order 1]
  ├── CanvasScaler (Scale/Screen Size, 1080×1920) ├── CanvasScaler
  ├── EventSystem (InputSystemUIInputModule)      │
  ├── SafeAreaPanel [anchored to Screen.safeArea] ├── SafeAreaPanel [anchored to Screen.safeArea]
  │    ├── MoveCounterText (TMP)                  │    ├── StarRatingDisplay
  │    ├── UndoButton (Button.onClick)            │    ├── CoinAnimationDisplay
  │    ├── HintButton (Button.onClick)            │    ├── AdOfferButton
  │    └── CoinDisplay (TMP)                      │    └── NextLevelButton
  └── Background (full-bleed, no safe area)       └── DimOverlay (full-bleed)

InGameHUD.cs (MonoBehaviour on InGameHUD Canvas)
  subscribes to: GSM.OnLevelLoaded, GSM.OnBoardStateChanged, GSM.OnLevelComplete,
                 AnimSystem.OnAnimationComplete, CE.OnCoinBalanceChanged,
                 SortMechanic.OnDeadlockDetected
  commands: GSM.UndoRequested(), CE.SpendCoins(hintCost)

LevelCompleteUI.cs (MonoBehaviour on LevelCompleteUI Canvas)
  subscribes to: LP.OnLevelCompleted
  commands: CE.AddCoins(reward, levelId, Base/AdBonus)
```

### Key Interfaces

```csharp
// Safe area component — attach to direct Canvas child Panel
public class SafeAreaPanel : MonoBehaviour { /* as above */ }

// InGameHUD component — on InGameHUD Canvas root (or a child)
public class InGameHUD : MonoBehaviour
{
    // All UI references resolved via [SerializeField] private TMP backing fields
    [SerializeField] private TextMeshProUGUI _moveCounterText;
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _hintButton;
    [SerializeField] private TextMeshProUGUI _coinDisplayText;
}
// Note: [SerializeField] on auto-properties is a compile error in Unity 6.3
// All serialized UI references must use [SerializeField] private TMP/Button _field; pattern
```

## Alternatives Considered

### Alternative A: Screen Space-Camera Canvas
- **Description**: Canvas renders via a dedicated UI Camera in Screen Space; camera position/FOV controls canvas size.
- **Pros**: Can use depth of field, custom post-processing on UI; more flexible for 3D-world UI overlaps
- **Cons**: Requires managing a second camera (draw call + culling overhead); camera positioning affects UI scale; no benefit for a pure 2D HUD with no 3D UI integration
- **Rejection Reason**: Added complexity without benefit for a 2D puzzle HUD.

### Alternative B: World Space Canvas
- **Description**: Canvas exists in world space; positioned in front of the camera like a 3D object.
- **Pros**: Can interact with 3D world space (e.g., floating health bars over characters)
- **Cons**: Must be manually positioned relative to camera; scale must match camera FOV; `Screen.safeArea` cannot be applied via RectTransform anchors in World Space mode
- **Rejection Reason**: Incompatible with `Screen.safeArea` anchor approach; inapplicable to a 2D puzzle game HUD.

### Alternative C: Single Canvas with Child Panels for HUD and LevelComplete
- **Description**: One Canvas at sort order 0; InGameHUD and LevelCompleteUI are sibling child panels; LevelCompleteUI's panel enabled/disabled.
- **Pros**: One Canvas = one batch root; slightly simpler EventSystem wiring
- **Cons**: Any update to HUD elements (e.g., move counter text change every move) re-batches the entire Canvas including LevelCompleteUI's elements. Separation of concerns is lost. Sort order within a single Canvas requires additional canvas groups or nested sub-canvases to control rendering priority — more complex, not less.
- **Rejection Reason**: Per-Canvas batching isolation is preferable; two Canvases with explicit sort orders is cleaner and more maintainable.

## Consequences

### Positive
- Safe area is handled at the Canvas level in one reusable component — zero per-element safe area logic
- Explicit sort orders prevent HUD/overlay rendering ambiguity
- Two-Canvas architecture isolates UGUI re-batching between HUD and Level Complete
- TextMeshProUGUI ban on legacy UI.Text prevents rendering regressions on high-DPI screens

### Negative
- Each Canvas has its own EventSystem overhead — negligible for two Canvases, but worth monitoring if more are added
- Safe area panel introduces one extra layout level in the UGUI hierarchy — all UI elements must be children of SafeAreaPanel, not root Canvas children (easy to forget during rapid iteration)
- Portrait lock means safe area testing for landscape is deferred — if landscape is added in a future milestone, safe area code must be re-tested

### Risks
- **Risk**: New UI element added as direct Canvas child (not inside SafeAreaPanel) → visible behind notch/home indicator on iPhone. **Mitigation**: Control manifest rule: "All HUD interactive elements MUST be children of SafeAreaPanel"; PR checklist.
- **Risk**: `UI.Text` used instead of `TextMeshProUGUI` → poor rendering on high-DPI screens, and runtime GC from legacy text mesh rebuild. **Mitigation**: `legacy_ui_text` forbidden pattern registered in architecture registry; code review gate.
- **Risk**: Physical tap target smaller than 44pt/48dp → missed taps on mid-range devices, especially undo button during rapid play. **Mitigation**: Minimum size enforced in prefab inspector; validated on Galaxy A series and iPhone 14 before any HUD story is closed.
- **Risk**: Future sort order collision when a toast/notification layer is added without consulting this ADR → notification renders behind LevelCompleteUI. **Mitigation**: Sort order 2 reserved and documented here; control manifest notes the sort order table.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| in-game-hud.md | TR-HUD-001: UGUI Canvas, Screen Space-Overlay, `Screen.safeArea` → `RectTransform.anchorMin/Max` | Documents Screen Space-Overlay Canvas; SafeAreaPanel implements Screen.safeArea anchor pattern |
| in-game-hud.md | TR-HUD-002: Move counter subscribes to `GSM.OnBoardStateChanged` | HUD subscribes via event pattern (ADR-0002); `_moveCounterText` updated in handler |
| level-complete-ui.md | TR-LCUI-001: `StarRating(move_count, par_moves)` | LevelCompleteUI subscribes to `LP.OnLevelCompleted` which carries `moveCount` and `parMoves`; star rating computed in LevelCompleteUI |

## Performance Implications
- **CPU**: `Screen.safeArea` read in `Awake()` and `OnRectTransformDimensionsChange()`: negligible. UGUI re-batch triggered by move counter text change: ~0.1ms per frame when active (only when move count changes, not per-frame constant).
- **Memory**: Two Canvas objects + CanvasScaler + SafeAreaPanel: ~5–10 KB. TextMeshPro font asset: ~1–2 MB (shared across all text elements).
- **Draw Calls**: UI contributes 1–3 draw calls per Canvas (assuming texture atlas for all UI sprites). Within the ≤100 batch budget from technical-preferences.md.
- **Network**: N/A

## Migration Plan
No existing code to migrate — written before implementation begins.

## Validation Criteria
1. Device test (iPhone with Dynamic Island): HUD elements visible and not obscured; coin display and buttons within safe area bounds
2. Device test (Android with gesture navigation): buttons not hidden behind gesture pill; bottom padding applied by safe area
3. Device test (Galaxy A14): all button hit areas ≥ 48dp physical pixels; undo button tappable during rapid play
4. Scene audit: no `UI.Text` components anywhere in project (automated grep in CI)
5. Layout test: LevelCompleteUI renders fully above InGameHUD during level completion (sort order 1 > 0)
6. Scene audit: all EventSystems use `InputSystemUIInputModule` (from ADR-0007 acceptance criterion)

## Related Decisions
- ADR-0001: Singleton Architecture — InGameHUD and LevelCompleteUI at SEO 0
- ADR-0002: Event and Signal Architecture — HUD subscribes to GSM/CE/AnimSystem events
- ADR-0006: Board State Representation — InGameHUD reads `GSM.OnBoardStateChanged` for move counter
- ADR-0007: Input Handling Strategy — `InputSystemUIInputModule` on EventSystem required
- `design/gdd/in-game-hud.md`, `design/gdd/level-complete-ui.md`
