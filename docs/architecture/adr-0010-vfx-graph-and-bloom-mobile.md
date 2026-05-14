# ADR-0010: VFX Graph and Bloom on Mobile

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Rendering (VFX Graph, URP 2D, bloom, mobile fallback) |
| **Knowledge Risk** | HIGH — VFX Graph in URP 2D Renderer on mobile is post-LLM-cutoff; sorting behavior specific to 2D Renderer |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | `VisualEffect.SetFloat()` (per-instance VFX property API, Unity VFX Graph package); VFX Graph output context sorting layer assignment; `FrameTimingManager.CaptureFrameTimings()` / `GetLatestTimings()` with `FrameTiming.gpuFrameTime` (Unity 6.x GPU profiling API — replaces non-existent `GpuTimingProbe`) |
| **Verification Required** | (1) Confirm VFX Graph ring + sparks sort correctly against bolt sprites in URP 2D Renderer — specify `Sorting Layer = Effects, Order In Layer = 10`. (2) Confirm `VisualEffect.SetFloat("quality_density_multiplier")` applies correctly at runtime on iOS and Android. (3) Benchmark VFX Graph first-playback GPU cost on Samsung Galaxy A14 — if >10ms frame spike, enable automatic fallback to Low-tier path. (4) Confirm overlay sprite layer bloom (replacing MaterialPropertyBlock) triggers URP Bloom at target intensity. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0005 (URP 2D + HDR + On-Tile Post Processing; `QualityTierSystem.SupportsVFXGraph`); ADR-0009 (VFX ring triggered by bolt settle + stack completion events from AnimationSystem) |
| **Enables** | None — terminal ADR for VFX/bloom layer |
| **Blocks** | Animation System implementation sprint (VFX components); Level Complete animation |
| **Ordering Note** | ADR-0005 must be Accepted first (pipeline configuration); ADR-0009 must be Accepted first (ring triggered from AnimationSystem). |

## Context

### Problem Statement
BoltSort's bolt settle and stack completion effects require VFX ring + spark particles that depend on GPU compute shaders (VFX Graph). Some Low-tier Android devices either lack compute shaders or have compute performance too low to run VFX Graph without frame spikes. Additionally, bolt settle glow (bloom) requires driving HDR emissive color on Sprite Renderers; `MaterialPropertyBlock` with SRP Batcher on Sprite Renderers breaks batching in URP 2D. Both concerns need explicit decisions before the Animation System sprint begins.

### Constraints
- **VFX Graph**: requires compute shaders (`SystemInfo.supportsComputeShaders`); unavailable on some Low-tier Android devices
- **VFX sorting in URP 2D**: VFX Graph particles render in the transparent pass of URP 2D; sorting against Sprite Renderers requires explicit `Sorting Layer` + `Order In Layer` on the `VisualEffect` output context
- **No `VFXManager.SetGlobalFloat()`**: this API does not exist in Unity 6.x VFX Graph. Global property application requires per-instance `VisualEffect.SetFloat()` iteration
- **MaterialPropertyBlock + SRP Batcher on Sprite Renderers**: `MaterialPropertyBlock` writes properties outside the `CBUFFER`, breaking SRP Batcher batching for the affected sprite. Must use a second sprite overlay layer instead of MaterialPropertyBlock for emissive glow
- **Allowed-library list**: No third-party VFX package (Shader Graph and VFX Graph are both first-party and already in the project)

### Requirements
- VFX ring + sparks on stack completion when GPU compute is available
- Sprite-based fallback (no GPU compute required) for Low-tier devices
- Bloom via HDR emissive sprite without breaking SRP Batcher batching
- `quality_density_multiplier` applied to all active VFX Graph instances at `OnLevelLoaded`
- Automatic runtime degradation if VFX Graph causes >10ms GPU spike on first playback

## Decision

### Tier Decision Summary

| Condition | VFX Path | Glow Path |
|-----------|---------|---------|
| `SupportsVFXGraph == true` (Medium/High tier) | VFX Graph ring + sparks | Overlay sprite layer (HDR color > 1.0) |
| `SupportsVFXGraph == false` (Low tier) | ParticleSystem ring + sparks | Overlay sprite layer (HDR color > 1.0) |
| Runtime spike > 10ms on first VFX playback | Downgrade to ParticleSystem path | Same as Low-tier |

Note: the glow (bloom) path is identical across all tiers — the same overlay sprite technique works whether VFX Graph is enabled or not.

### VFX Graph Path (Medium/High Tier)

**Bolt settle ring**: A pre-authored VFX Graph asset (`BoltSettleRing.vfx`) with an additive output context. Instantiated per BoltStack (pooled), activated on `OnMoveCommitted`.

**Sorting layer**: VFX Graph `VisualEffect` component must have `Sorting Layer = Effects` and `Order In Layer = 10` — renders above bolt sprite layers (e.g., `Order In Layer = 0`) but below UI Canvas.

**Stack completion sparks**: A burst particle `BoltStackComplete.vfx`, activated once when the stack becomes full and monochromatic (win condition check in AnimationSystem).

**`quality_density_multiplier` application**:
```csharp
// In AnimationSystem.HandleLevelLoaded() [subscribes to GSM.OnLevelLoaded]
float density = QualityTierSystem.Instance.DensityMultiplier;  // 0.25 / 0.65 / 1.0
foreach (var vfx in _activeVFXInstances)
    vfx.SetFloat("quality_density_multiplier", density);
```

All VFX Graph assets must expose a float property named `"quality_density_multiplier"` in their Blackboard. The `density_multiplier` parameter scales: particle count (`SpawnCount = baseCount * density`), ring thickness, and spark radius. At Low-tier (0.25), effects are reduced 75% but still present for Medium-tier devices that have compute shaders.

**Runtime spike downgrade**:
```csharp
// In AnimationSystem, on first VFX playback (development builds only):
// Use FrameTimingManager — the correct Unity 6.x GPU timing API.
// GpuTimingProbe does not exist in Unity's public surface and will not compile.
FrameTimingManager.CaptureFrameTimings();
var timings = new FrameTiming[1];
uint count = FrameTimingManager.GetLatestTimings(1, timings);
float gpuCostMs = count > 0 ? (float)timings[0].gpuFrameTime : 0f;

if (gpuCostMs > 10f)
{
    _useVFXGraph = false;
    foreach (var vfx in _activeVFXInstances)
        vfx.gameObject.SetActive(false);
    // ParticleSystem fallback activated by AnimationSystem state machine
}
```
`FrameTimingManager` requires `Application.targetFrameRate` to be set (guaranteed by `QualityTierSystem` at SEO −100). In release builds, GPU timing data may not be available — if `count == 0`, skip the downgrade check and trust `QualityTierSystem` tier classification.

### Low-Tier Fallback Path (ParticleSystem)

When `SupportsVFXGraph == false`, the ring effect uses a `ParticleSystem` with:
- Single burst emission (`Burst: count = 1, time = 0`)
- `startSize` curve: expand from 0 to `RingRadius` over `RingDuration`
- `startColor` curve: full white → alpha fade to 0 over lifetime
- `RenderMode = Billboard, Material = VFX_Ring_Additive`
- No `Update()` polling — `ParticleSystem.Play()` is fire-and-forget

This requires zero sprite sheet animation or Animator Controller overhead. Lower memory, lower CPU, no compute shader dependency.

### Glow / Bloom Implementation (OQ-04 Resolved)

`MaterialPropertyBlock` breaks SRP Batcher batching on Sprite Renderers. **The primary bloom technique is a second sprite overlay, not MaterialPropertyBlock.**

**Implementation**: Each BoltStack prefab contains a `GlowOverlay` child `SpriteRenderer`:
- Same sprite as the bolt, same sorting order as the bolt's main sprite + 1
- Default: `color = Color.clear` (invisible, zero alpha)
- On settle: `color = new Color(glowR, glowG, glowB, 1f)` where R/G/B > 1.0 (HDR) → triggers URP Bloom

```csharp
// In BoltVisual, on settle complete:
_glowOverlayRenderer.color = new Color(1.4f, 1.4f, 0.6f, 1f);  // HDR warm glow
yield return new WaitForSecondsRealtime(GlowFadeDuration);
// Lerp color back to clear over 200ms (via coroutine)
```

The `GlowOverlay` SpriteRenderer uses a dedicated `BoltGlow_Additive` material — different from the main bolt material — which prevents it from batch-merging with the bolt body sprite (separate materials = separate batches; this is expected and acceptable since glow events are infrequent).

**Why not MaterialPropertyBlock**: The SRP Batcher requires all per-material properties to live in a `UnityPerMaterial` CBUFFER. `MaterialPropertyBlock` writes outside this CBUFFER, disabling SRP Batcher for that renderer for the frame. For a puzzle game with 11 bolt stacks all being batched together, each MaterialPropertyBlock call breaks one batch — up to 11 batch splits per frame during animation. The overlay layer approach adds a second sprite object per bolt (one extra draw call per glow event) but preserves all batching on the primary bolt sprites.

### Architecture Diagram

```
OnMoveCommitted(src, dst, colorId, seqId)
    │
    ├── [Medium/High: SupportsVFXGraph] BoltSettleRing.vfx.Play()
    │    ├── Sorting Layer = "Effects", Order In Layer = 10
    │    └── quality_density_multiplier applied via vfx.SetFloat()
    │
    └── [Low: !SupportsVFXGraph] ParticleSystem.Play() [ring burst]

OnAnimationComplete (after settle)
    │
    └── BoltVisual._glowOverlayRenderer.color = HDR glow color
         └── URP Volume Bloom fires → glow visible on screen

OnBoltStackComplete (stack full + monochromatic)
    ├── [Medium/High] BoltStackComplete.vfx.Play() [sparks burst]
    └── [Low] ParticleSystem.Play() [spark burst]

GSM.OnLevelLoaded
    └── AnimationSystem: foreach vfx in _activeVFXInstances
             vfx.SetFloat("quality_density_multiplier", QTS.DensityMultiplier)
```

### Key Interfaces

```csharp
// In AnimationSystem — VFX instance registry
// Lifecycle: populated in HandleLevelLoaded(); cleared in HandleLevelUnloaded().
// Cross-reference: ADR-0009 bolt visual lifecycle owns BoltVisual instantiation timing.
private readonly List<VisualEffect> _activeVFXInstances = new();
private bool _useVFXGraph;

private void Awake()
{
    _useVFXGraph = QualityTierSystem.Instance.SupportsVFXGraph;
    GameStateManager.Instance.OnLevelLoaded   += HandleLevelLoaded;
    GameStateManager.Instance.OnLevelUnloaded += HandleLevelUnloaded;
}

// Called on GSM.OnLevelLoaded — BoltVisual prefabs have been instantiated by this point.
// Collect all VisualEffect components from active BoltStack prefabs; apply density multiplier.
private void HandleLevelLoaded(int levelId, int colorCount)
{
    _activeVFXInstances.Clear();  // clear any residue from prior level
    if (_useVFXGraph)
    {
        // Each BoltStack prefab's VisualEffect child is registered here.
        foreach (var vfx in FindObjectsByType<VisualEffect>(FindObjectsSortMode.None))
            _activeVFXInstances.Add(vfx);
    }
    float density = QualityTierSystem.Instance.DensityMultiplier;
    foreach (var vfx in _activeVFXInstances)
        vfx.SetFloat("quality_density_multiplier", density);
}

// Called on GSM.OnLevelUnloaded (TEARDOWN state transition) — release registry.
private void HandleLevelUnloaded()
{
    _activeVFXInstances.Clear();
}

// In BoltVisual prefab hierarchy:
// BoltVisual
//   ├── SpriteRenderer (bolt body) — SortingLayer: Default, Order: 0
//   ├── GlowOverlayRenderer — SpriteRenderer, SortingLayer: Default, Order: 1
//   └── [if Medium/High] VisualEffect (BoltSettleRing.vfx) — SortingLayer: Effects, Order: 10
```

## Alternatives Considered

### Alternative A: Shader Graph Emissive Overlay (MaterialPropertyBlock)
- **Description**: Drive bloom via `MaterialPropertyBlock.SetColor("_EmissiveColor", hdrColor)` on the main bolt material
- **Pros**: No extra GameObject; single sprite handles both appearance and glow
- **Cons**: Breaks SRP Batcher for all affected sprites (up to 11 batch splits during animation); confirmed incompatible pattern per engine specialist
- **Rejection Reason**: SRP Batcher breakage; overlay layer is simpler and batch-preserving.

### Alternative B: Animated Sprite Sheet for Ring Fallback
- **Description**: Pre-rendered ring expand animation as a sprite sheet; play via Animator Controller on ring-expand
- **Pros**: Exact artistic control over ring shape and timing
- **Cons**: Requires sprite sheet import + slicing; Animator Controller overhead; more complex than `ParticleSystem`; identical visual result achievable with simpler implementation
- **Rejection Reason**: Added pipeline complexity without visual quality gain for a simple ring expand+fade.

### Alternative C: Shader-Based Ring (Custom ScriptableRendererFeature)
- **Description**: Render an expanding ring using a custom ScriptableRendererFeature that writes a full-screen or viewport-clipped ring quad
- **Pros**: No GameObject hierarchy; precise GPU control
- **Cons**: Must use Render Graph API (ADR-0005); significant implementation complexity for a simple ring effect; overkill for a puzzle game at this fidelity level
- **Rejection Reason**: Disproportionate implementation cost.

## Consequences

### Positive
- `SupportsVFXGraph` computed at startup by QualityTierSystem — zero runtime branching cost during gameplay
- ParticleSystem fallback requires no compute shaders and runs on all target devices
- Overlay sprite layer for bloom preserves SRP Batcher on all bolt sprites
- `quality_density_multiplier` applied per-instance via `SetFloat()` — all VFX assets scale uniformly

### Negative
- VFX Graph requires every asset to expose `"quality_density_multiplier"` in its Blackboard — art pipeline must enforce this; no compile-time check
- GlowOverlay SpriteRenderer adds 1 extra draw call per glowing bolt — up to 11 extra draw calls during a full-board glow event. At ≤100 batch budget (ADR-0005), this is within budget but narrows the headroom
- Runtime spike downgrade (>10ms threshold) is only measurable in development builds — release builds rely on QualityTierSystem tier classification

### Risks
- **Risk**: VFX Graph asset missing `"quality_density_multiplier"` property → `SetFloat()` silently no-ops; incorrect density on that asset. **Mitigation**: Editor script validates all VFX Graph assets expose this property before build; CI build gate.
- **Risk**: VFX Graph ring sorts behind bolt sprites in URP 2D because Sorting Layer not set → ring invisible. **Mitigation**: ADR specifies `Sorting Layer = Effects, Order In Layer = 10`; prefab review enforces it.
- **Risk**: First VFX playback on marginal hardware causes >10ms GPU spike → stutter on first move. **Mitigation**: Runtime downgrade check after first playback (development builds); in production, QualityTierSystem tier classification provides the primary gate.
- **Risk**: `GlowOverlay` SpriteRenderer using a different material than the bolt body → unexpected visual layering if bloom bleeds between layers. **Mitigation**: GlowOverlay material uses additive blend mode (not alpha-blend); additive compositing is order-independent.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| animation-system.md | TR-ANIM-002: Stack completion glow + VFX ring + sparks | VFX Graph ring+sparks (Medium/High); ParticleSystem fallback (Low); HDR overlay glow (all tiers) |
| animation-system.md | TR-ANIM-005: `quality_density_multiplier` from QTS at `level_loaded` | `VisualEffect.SetFloat("quality_density_multiplier")` per-instance at `HandleLevelLoaded` |
| quality-tier-system.md | TR-QTS-002: Apply density multiplier before first scene | `SupportsVFXGraph` evaluated at SEO −100 (QualityTierSystem); density multiplier applied on first `OnLevelLoaded` |

## Performance Implications
- **CPU**: `foreach vfx.SetFloat()` at level load: O(N) where N = active VFX instances (≤22 at max board). Negligible.
- **GPU**: VFX Graph ring + sparks: ~1–3ms per stack completion event (Medium/High tier, Galaxy S-class). ParticleSystem ring: ~0.2ms (Low tier). GlowOverlay sprite: 1 extra draw call per glowing bolt — within ≤100 batch budget.
- **Memory**: VFX Graph `VisualEffect` instances: ~50–100 KB per instance × up to 11 rings = ~1 MB peak. Released when level resets. ParticleSystem instances: ~20KB each.
- **Load Time**: VFX Graph asset loading via Addressables: deferred to level load (within LevelDataSystem async load window).

## Migration Plan
No existing code to migrate — written before implementation begins.

## Validation Criteria
1. Device test (Samsung Galaxy A14, Low tier): `SupportsVFXGraph == false`; ParticleSystem ring plays correctly on stack completion
2. Device test (Samsung Galaxy S22, High tier): VFX Graph ring visible, sorted above bolt sprites, density at 1.0
3. Device test (Medium tier device): density at 0.65; ring visually reduced but present
4. Profiler: GlowOverlay draw calls ≤ 11 additional draws during full-board glow event; SRP Batcher not broken on bolt body sprites
5. Visual test: HDR overlay sprite color triggers URP Bloom; glow visible and calibrated to design intent
6. Editor validation: all VFX Graph assets expose `"quality_density_multiplier"` property (automated editor script)

## Related Decisions
- ADR-0005: Rendering Pipeline Configuration — HDR on 2D Renderer; `SupportsVFXGraph` flag; On-Tile Post Processing
- ADR-0009: Bolt Animation Strategy — VFX ring triggered by AnimationSystem on settle; celebration sequence timing
- `design/gdd/animation-system.md`, `design/gdd/quality-tier-system.md`
