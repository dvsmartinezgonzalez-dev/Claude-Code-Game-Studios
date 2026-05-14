# ADR-0009: Bolt Animation Strategy

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Animation |
| **Knowledge Risk** | LOW (Coroutine + Time.unscaledDeltaTime are stable); MEDIUM for glow/VFX (deferred to ADR-0010) |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | None — Coroutine, WaitForSecondsRealtime, Time.unscaledDeltaTime are stable across all Unity versions |
| **Verification Required** | (1) Confirm bolt animation completes in <1500ms on Samsung Galaxy A14 (Low tier, 30fps). (2) Verify `MaterialPropertyBlock` + SRP Batcher compatibility on Sprite Renderers in Unity 6.3 before glow implementation sprint (OQ-04). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (AnimationSystem is a DDOL singleton at SEO 0); ADR-0002 (OnAnimationComplete is `event Action<int>`); ADR-0005 (URP 2D rendering pipeline; bloom triggered by emissive sprite); ADR-0006 (OnBoardRefreshForced, OnBoardStateChanged events from GSM) |
| **Enables** | ADR-0010 (VFX Graph and bloom on mobile — depends on the animation pipeline established here) |
| **Blocks** | Animation System implementation sprint |
| **Ordering Note** | ADR-0005 must be Accepted first (bloom configuration affects glow timing). ADR-0006 must be Accepted first (watchdog and snap behavior come from GSM contracts). |

## Context

### Problem Statement
BoltSort bolt animations must satisfy precise per-phase timing requirements (lift 80ms, travel 80–300ms, settle 70ms) while staying within the 1500ms watchdog budget, responding correctly to board refresh commands (snap without emitting `OnAnimationComplete`), and emitting the sequence-ID-tagged completion signal that SortMechanic uses to exit `MOVE_EXECUTING`. Without an explicit tween strategy, developers may use frame-count-based timing (framerate-dependent), or call `StopAllCoroutines()` on abort (destroying unrelated animation sequences).

### Constraints
- No DOTween or LeanTween — not in allowed-library list (`CLAUDE.md`)
- Unity 6.x has no new first-party tween API for gameplay use (confirmed by engine specialist: `UIElements.ValueAnimation` is UI-only scope)
- Timing must be pause-safe: `Time.unscaledDeltaTime` accumulation required, not `Time.deltaTime` (otherwise animation freezes on `Time.timeScale = 0`)
- `StopAllCoroutines()` is forbidden on AnimationSystem — would abort unrelated sequences (glow ramp, celebration). Surgical per-bolt handle required
- `OnAnimationComplete` must NOT be emitted on abort (watchdog path) — SortMechanic exits MOVE_EXECUTING via `OnBoardRefreshForced` instead
- VFX ring + sparks on stack completion: deferred to ADR-0010

### Requirements
- Lift phase: 80ms
- Travel phase: distance-proportional, 80ms (adjacent) to 300ms (max distance)
- Settle phase: 70ms (plus micro-bounce)
- Total per-move: 230–450ms — within 1500ms watchdog at all travel distances
- `OnAnimationComplete(seqId)` fired exactly once per committed move, after settle completes
- Snap on `OnBoardRefreshForced`: abort in-flight coroutine, immediately set bolt visual to board position, no `OnAnimationComplete`
- Rejection shake: 100ms horizontal coroutine, no `OnAnimationComplete`
- Level complete celebration: ~2000ms multi-phase sequence (GDD F-03 formula)

## Decision

### Tween Architecture: Custom Coroutine + `Time.unscaledDeltaTime`

Each bolt move is driven by a custom Unity Coroutine on the `BoltVisual` MonoBehaviour — not on the central `AnimationSystem`. The coroutine reference is stored in `BoltVisual._moveCoroutine` so `StopCoroutine` targets only that bolt's animation without affecting any other active coroutine.

```csharp
// BoltVisual.cs — per-bolt coroutine ownership
public class BoltVisual : MonoBehaviour
{
    private Coroutine _moveCoroutine;

    public void StartMoveAnimation(Vector3 from, Vector3 to, int seqId,
        Action<int> onComplete)
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
        _moveCoroutine = StartCoroutine(MoveRoutine(from, to, seqId, onComplete));
    }

    public void AbortAndSnap(Vector3 targetPos)
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;       // null BEFORE setting position
        }
        transform.position = targetPos;  // immediate snap; no onComplete called
    }

    private IEnumerator MoveRoutine(Vector3 from, Vector3 to, int seqId,
        Action<int> onComplete)
    {
        yield return StartCoroutine(LiftPhase());
        yield return StartCoroutine(TravelPhase(from, to));
        yield return StartCoroutine(SettlePhase(to));
        _moveCoroutine = null;
        onComplete?.Invoke(seqId);       // emits AnimationSystem.OnAnimationComplete
    }
}
```

### Phase Timing and Easing

All phases use `Time.unscaledDeltaTime` accumulation inside a `yield return null` per-frame loop — pause-safe (unaffected by `Time.timeScale = 0`).

| Phase | Duration | Easing | Description |
|-------|---------|--------|-------------|
| Lift | 80ms fixed | EaseOutCubic | Bolt rises `LiftHeightUnits` above source stack |
| Travel | 80–300ms | EaseInOutQuad | Bolt moves from lift position to above destination; linear distance → linear duration |
| Settle | 70ms | EaseInQuint + micro-bounce | Bolt drops into destination stack; last 20ms reverses slightly for bounce |

**Travel duration formula**: `travel_ms = Lerp(80, 300, dist / MaxDistanceUnits)` — linear proportion of screen distance to duration range. Clamped: `Mathf.Clamp(travel_ms, 80, 300)`.

```csharp
private IEnumerator LiftPhase()
{
    float elapsed = 0f;
    const float duration = 0.080f;  // 80ms
    var startPos = transform.position;
    var liftPos = startPos + Vector3.up * LiftHeight;
    
    while (elapsed < duration)
    {
        elapsed += Time.unscaledDeltaTime;
        float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
        transform.position = Vector3.LerpUnclamped(startPos, liftPos, t);
        yield return null;
    }
    transform.position = liftPos;
}
```

### Sequence ID Management

`AnimationSystem` stores `_activeSequenceId` when a move animation begins. When `OnAnimationComplete(seqId)` fires, the ID matches the committed move. SortMechanic discards the signal if `seqId != currentMoveExecutingSeqId` (stale-signal guard per ADR-0002).

```csharp
// AnimationSystem.cs
public event Action<int> OnAnimationComplete;
private int _activeSequenceId;

// Called by GSM event subscription:
private void HandleMoveCommitted(int src, int dst, int colorId, int seqId)
{
    _activeSequenceId = seqId;
    var bolt = _boltVisuals[src][topIndex];
    bolt.StartMoveAnimation(srcPos, dstPos, seqId, OnBoltAnimationComplete);
    StartWatchdogCoroutine(seqId);  // starts 1500ms safety timer
}

private void OnBoltAnimationComplete(int seqId)
{
    // Called by BoltVisual.MoveRoutine after settle
    CancelWatchdog();
    OnAnimationComplete?.Invoke(seqId);
}
```

### Abort on `OnBoardRefreshForced` (Watchdog Path)

When `GSM.OnBoardRefreshForced(seqId)` fires:
1. AnimationSystem calls `AbortAndSnap()` on the in-flight `BoltVisual` — bolt immediately snaps to board-correct position
2. AnimationSystem does **NOT** emit `OnAnimationComplete`
3. SortMechanic subscribes directly to `GSM.OnBoardRefreshForced` — when it fires, SortMechanic transitions MOVE_EXECUTING → IDLE **without** emitting `OnMoveExecutingExited` (watchdog path; deferred undo discarded)

This is the one state transition where SortMechanic exits MOVE_EXECUTING through a GSM event rather than through `OnAnimationComplete`.

```
Normal path:      OnAnimationComplete(seqId) → SortMechanic → IDLE → OnMoveExecutingExited
WIN path:         OnAnimationComplete(seqId) → SortMechanic → WIN (no OnMoveExecutingExited)
Watchdog path:    OnBoardRefreshForced(seqId) → SortMechanic → IDLE (no OnMoveExecutingExited; no pending tap)
```

### Rejection Shake

On `SortMechanic.OnMoveRejected`: animate the source bolt with a 100ms horizontal shake pattern. No sequence ID involved; no `OnAnimationComplete` emitted. SortMechanic transitions BOLT_SELECTED → INVALID_MOVE → IDLE before the shake completes.

```csharp
// AnimationSystem handles OnMoveRejected
private IEnumerator RejectionShake(BoltVisual bolt)
{
    var origin = bolt.transform.position;
    float elapsed = 0f;
    const float duration = 0.100f;
    while (elapsed < duration)
    {
        elapsed += Time.unscaledDeltaTime;
        float t = elapsed / duration;
        float shake = Mathf.Sin(t * Mathf.PI * 4) * ShakeAmplitude * (1f - t);
        bolt.transform.position = origin + Vector3.right * shake;
        yield return null;
    }
    bolt.transform.position = origin;
    // No OnAnimationComplete — SortMechanic is already in IDLE
}
```

### Level Complete Celebration (~2000ms)

Triggered by `GSM.OnLevelComplete`. A multi-phase sequence:

| Phase | Duration | Description |
|-------|---------|-------------|
| Glow ramp | 400ms | All bolt sprites ramp emissive → bloom pulses |
| Board ring | 600ms | VFX ring plays across full board (ADR-0010) |
| Star cascade | 800ms | Star rating revealed (LevelCompleteUI animates) |
| Silence | 200ms | Pause before Next Level button activates |
| **Total** | **~2000ms** | |

After the celebration sequence, AnimationSystem emits `OnAnimationComplete(_activeSequenceId)` to signal that LevelCompleteUI's next-level button may be enabled.

> **GDD note**: F-03 formula minimum is ~2000ms at default knob values, not 1600ms. The 1600ms floor stated elsewhere was incorrect. The celebration duration is effectively fixed at ~2000ms at default settings.

### Architecture Diagram

```
GSM.OnMoveCommitted(src, dst, colorId, seqId)
    │
    ▼
AnimationSystem.HandleMoveCommitted()
    ├── _activeSequenceId = seqId
    ├── BoltVisual.StartMoveAnimation(srcPos, dstPos, seqId, onComplete)
    │    └── Coroutine: LiftPhase → TravelPhase → SettlePhase
    │         └── onComplete(seqId) → OnAnimationComplete?.Invoke(seqId)
    └── StartWatchdogCoroutine(seqId) [1500ms safety timer]

OnAnimationComplete(seqId):
    → SortMechanic (checks seqId == _currentSeq? act : discard)
    → InGameHUD (re-enables undo button)

GSM.OnBoardRefreshForced(seqId):
    → AnimationSystem: AbortAndSnap() [no OnAnimationComplete]
    → SortMechanic: MOVE_EXECUTING → IDLE (no OnMoveExecutingExited)
    → InGameHUD: re-enables undo button immediately
    → CancelWatchdog()

GSM.OnBoardStateChanged(seqId, moveCount) [undo path]:
    → AnimationSystem: snap all bolt visuals to board state (no tween)
```

### Key Interfaces

```csharp
// AnimationSystem (DDOL MonoBehaviour)
public class AnimationSystem : MonoBehaviour
{
    public static AnimationSystem Instance { get; private set; }
    public event Action<int> OnAnimationComplete;  // (seqId)

    // Subscriptions in Awake(): GSM.OnMoveCommitted, GSM.OnMoveRejected,
    // GSM.OnMoveCancelled, GSM.OnBoardStateChanged, GSM.OnBoardRefreshForced,
    // GSM.OnLevelComplete, GSM.OnLevelLoaded (reset)
}

// BoltVisual (per-bolt MonoBehaviour in game scene)
public class BoltVisual : MonoBehaviour
{
    public void StartMoveAnimation(Vector3 from, Vector3 to, int seqId, Action<int> onComplete);
    public void AbortAndSnap(Vector3 boardPos);
}
```

## Alternatives Considered

### Alternative A: DOTween
- **Description**: Use the DOTween package (`DG.Tweening`) for bolt tweens via `transform.DOMove()` with custom easing
- **Pros**: Rich easing library; sequence chaining (`DOSequence`); battle-tested on mobile
- **Cons**: Not in the allowed-library list; third-party package dependency; adds ~600KB to build; requires license for certain features; not needed for 3 simple phases
- **Rejection Reason**: Not in allowed-library list.

### Alternative B: Unity UIElements ValueAnimation
- **Description**: `UnityEngine.UIElements.Experimental.ValueAnimation<T>` — Unity's internal tween system
- **Pros**: First-party; no dependency
- **Cons**: Scoped to UI Toolkit elements only; cannot drive world-space Transform position on MonoBehaviours; entirely wrong scope for gameplay animation
- **Rejection Reason**: Wrong scope — UI Toolkit only, not applicable to game-world bolt visuals.

### Alternative C: Frame-count-based animation
- **Description**: Advance animation by a fixed percentage per frame, completing in N frames
- **Pros**: Zero external dependencies; simple
- **Cons**: Framerate-dependent — at 30fps the lift phase completes in ~2.4 frames; at 60fps it completes in ~4.8 frames. Timing precision is ±16.6ms per frame step, well above the required precision. Pausing breaks timing. Completely wrong for a precision-timing requirement.
- **Rejection Reason**: Framerate-dependent; cannot meet the 80ms/300ms precision requirements.

## Consequences

### Positive
- `Time.unscaledDeltaTime` accumulation gives frame-rate-independent, pause-safe timing
- Per-bolt coroutine handles enable surgical abort without disturbing other sequences
- Simple 3-phase architecture is easy to test with unit time injection
- No third-party dependency

### Negative
- On very low framerates (e.g., 15fps on an extremely slow device), the shortest phase (80ms lift) gets only ~1-2 samples — animation is correct but visually choppy. Not a target hardware concern.
- The level complete celebration Coroutine is a longer-lived sequence (~2000ms) on a DDOL MonoBehaviour; must be properly cancelled if level load is interrupted mid-celebration
- `MaterialPropertyBlock` compatibility with SRP Batcher on Sprite Renderers (OQ-04) is unresolved — glow implementation cannot begin until verified in Unity 6.3 in-engine

### Risks
- **Risk**: `StopAllCoroutines()` called somewhere in AnimationSystem → aborts glow ramp, celebration, all pending animations. **Mitigation**: `StopAllCoroutines()` banned in control manifest; all stops must target a named coroutine reference.
- **Risk**: BoltVisual `_moveCoroutine` not nulled before `StartMoveAnimation` on rapid second tap → stale handle left active while new coroutine runs → `AbortAndSnap` targets wrong coroutine. **Mitigation**: Pattern in `StartMoveAnimation` above explicitly stops and nulls before starting new coroutine.
- **Risk**: OQ-04 (`MaterialPropertyBlock` + SRP Batcher on Sprite Renderers) blocks glow implementation. **Mitigation**: Verify in-engine before Animation System sprint; if incompatible, fallback is a second sprite layer for glow (ADR-0010 will handle this).
- **Risk**: Level complete celebration Coroutine still running when next `GSM.LoadLevel()` fires (rapid next-level tap) → `OnAnimationComplete` fires for wrong level. **Mitigation**: `AnimationSystem.HandleLevelLoaded()` calls `StopCoroutine(_celebrationCoroutine)` + null before resetting board visuals.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| animation-system.md | TR-ANIM-001: Bolt lift arc + travel + settle (80+80-300+70ms) | Documents 3-phase Coroutine with exact durations and easing functions |
| animation-system.md | TR-ANIM-003: Snap bolt visuals on `board_state_changed`, `board_refresh_forced` | Documents immediate `AbortAndSnap()` pattern; no tween on snap paths |
| animation-system.md | TR-ANIM-004: Level complete celebration ~1600–2020ms | Documents ~2000ms multi-phase celebration sequence (GDD F-03) |
| animation-system.md | TR-ANIM-007: Must emit `OnAnimationComplete` within watchdog_timeout_ms 1500ms | 80+300+70ms max = 450ms; well within 1500ms. Watchdog path: no `OnAnimationComplete` (SortMechanic handles exit via `OnBoardRefreshForced`) |
| animation-system.md | TR-ANIM-008: Rejection shake 100ms; no `animation_complete` | Documents 100ms horizontal shake Coroutine; explicitly states no `OnAnimationComplete` |

## Performance Implications
- **CPU**: Per-frame `while(elapsed < duration)` loop: 3 active statements per frame per animating bolt. At max 11 stacks × 1 moving bolt = ~330 ops/frame during a move. Negligible.
- **Memory**: One `Coroutine` object per active animation (~32 bytes). Max concurrent: ~11 bolts + 1 celebration = ~12 coroutines. Negligible.
- **Load Time**: Coroutine startup: one allocation per animation start. Pre-warm during level load if needed.
- **Network**: N/A

## Migration Plan
No existing code to migrate — written before implementation begins.

## Validation Criteria
1. Unit test: LiftPhase(80ms) — inject mock `unscaledDeltaTime = 0.016f`; verify 5 frames × 16ms ≈ 80ms completes phase; bolt at liftTarget position
2. Unit test: AbortAndSnap — start MoveAnimation, call AbortAndSnap after Lift; verify `OnAnimationComplete` never fires; verify bolt at snapTarget
3. Unit test: Rejection shake — verify no `OnAnimationComplete` on `OnMoveRejected`; bolt returns to origin after 100ms
4. Integration test: watchdog path — simulate `OnBoardRefreshForced` during MOVE_EXECUTING; verify SortMechanic exits to IDLE without `OnMoveExecutingExited`
5. Device test (Galaxy A14, Low tier, 30fps): move animation completes within 500ms

## Related Decisions
- ADR-0002: Event and Signal Architecture — `OnAnimationComplete(seqId)` subscribe-then-check; sequence ID stale-signal guard
- ADR-0005: Rendering Pipeline Configuration — bloom triggered by emissive sprite color > 1.0; HDR enabled
- ADR-0006: Board State Representation — `OnBoardRefreshForced` triggers AbortAndSnap; `OnBoardStateChanged` triggers visual snap
- ADR-0010: VFX Graph and Bloom on Mobile — VFX ring/sparks on stack completion; Low-tier fallback; MaterialPropertyBlock OQ-04
- `design/gdd/animation-system.md` — source of truth for phase timing, easing curves, and celebration sequence
