# ADR-0005: Rendering Pipeline Configuration

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Rendering (URP 2D Renderer, URP 17.x) |
| **Knowledge Risk** | HIGH — URP Compatibility Mode removed in Unity 6.3; Render Graph mandatory; glow-before-tonemapping change |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | `ScriptableRendererFeature.AddRenderPasses()` + `ScriptableRenderPass.RecordRenderGraph()` (Render Graph API, Unity 6.x); `Awaitable.BackgroundThreadAsync()` (not used here); `SystemInfo.supportsComputeShaders` (stable) |
| **Verification Required** | (1) Confirm HDR enabled on 2D Renderer Data asset; bolt settle sprite color > 1.0 triggers bloom. (2) Confirm On-Tile Post Processing visible and toggled in 2D Renderer Data asset in Unity 6.3 editor. (3) Confirm `QualitySettings.vSyncCount = 0` before `Application.targetFrameRate` on iOS/Android. (4) Verify bloom intensity visually after recalibration (glow-before-tonemapping change). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (QualityTierSystem is a DDOL singleton at SEO −100 that sets `targetFrameRate` and `DensityMultiplier`) |
| **Enables** | ADR-0009 (Bolt Animation Strategy — depends on knowing the rendering pipeline for coroutine + VFX Graph approach); ADR-0010 (VFX Graph and Bloom on Mobile — depends on this ADR's pipeline configuration) |
| **Blocks** | Animation System sprint, Quality Tier System sprint — both depend on knowing the rendering pipeline before implementing visual systems |
| **Ordering Note** | ADR-0001 must be Accepted before this (QualityTierSystem boot slot is established there). ADR-0009 and ADR-0010 cannot begin until this ADR is Accepted. |

## Context

### Problem Statement
BoltSort requires explicit rendering pipeline decisions before any visual system can be implemented. Unity 6.3 removed URP Compatibility Mode, making Render Graph mandatory for all custom render features. Without these decisions documented, programmers may write `SetupRenderPasses`-based render features that cause compile errors in Unity 6.3, or fail to enable mobile-specific optimizations that significantly affect GPU bandwidth on target hardware.

### Constraints
- **Unity 6.3 BREAKING**: `URP Compatibility Mode` is fully removed — `ScriptableRendererFeature.SetupRenderPasses()` and `URPRenderPipelineAsset.enableRenderCompatibilityMode` are gone. Any code referencing them causes compile errors.
- **Unity 6.3 CHANGE**: Glow/bloom processes before tonemapping — GDD-authored bloom intensity values must be recalibrated in-engine, not taken from pre-6.3 reference values.
- Target platforms: iOS (Metal) + Android (Vulkan) — both use tile-based GPUs that benefit from On-Tile Post Processing
- 60fps target (Medium/High tiers), 30fps (Low tier)
- VFX Graph requires GPU compute shaders — unavailable on some Low-tier Android devices

### Requirements
- URP 2D Renderer (2D puzzle game — 2D Renderer Data asset, not Forward Renderer)
- All custom `ScriptableRendererFeature` implementations must use Render Graph API: `AddRenderPasses()` on feature + `RecordRenderGraph()` on pass
- On-Tile Post Processing enabled for mobile tile-based GPU bandwidth optimization
- HDR enabled on 2D Renderer Data asset — required for bloom to trigger from sprite color values above 1.0
- QualityTierSystem sets `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate` at SEO −100
- Render scale fixed at 1.0 (native resolution) across all tiers — quality scaling via VFX density, not resolution
- VFX Graph GPU compute availability checked at QualityTierSystem startup

## Decision

### Render Pipeline Asset Configuration

| Setting | Value |
|---------|-------|
| **Pipeline** | Universal Render Pipeline (URP) 17.x |
| **Renderer** | 2D Renderer (2D Renderer Data asset) |
| **GPU Backend** | Vulkan (Android), Metal (iOS) |
| **HDR** | Enabled on 2D Renderer Data asset |
| **Render Scale** | 1.0 (fixed; all quality tiers) |
| **On-Tile Post Processing** | Enabled on 2D Renderer Data asset (Tile-Only Mode) |
| **Depth Texture** | Enabled only if required by a specific feature; default off (saves bandwidth) |
| **Opaque Texture** | Disabled (not needed for BoltSort's opaque sprite pipeline) |

**On-Tile Post Processing** (Tile-Only Mode): Configured on the URP 2D Renderer Data asset — not on the URP Pipeline Asset. This keeps post-processing entirely on the GPU's tile cache, avoiding a full framebuffer roundtrip to main memory on tile-based GPUs (Mali, Apple GPU). Bloom, color grading, and other post-processing passes stay on-tile. This is the single most impactful mobile rendering optimization for this project.

### Quality Tier System — Rendering Responsibility

`QualityTierSystem.Awake()` [SEO −100] sets all rendering targets before any scene or VFX initializes:

```csharp
// In QualityTierSystem.Awake() [SEO -100]
QualitySettings.vSyncCount = 0;              // must be 0 — otherwise targetFrameRate is ignored on mobile
Application.targetFrameRate = _tier.TargetFrameRate;  // 30 (Low) or 60 (Medium/High)
_densityMultiplier = _tier.DensityMultiplier;         // 0.25 / 0.65 / 1.0

// VFX Graph compute check — stored for ADR-0010 fallback decision
SupportsVFXGraph = SystemInfo.supportsComputeShaders;
```

| Tier | Condition | Target FPS | Density Multiplier | vSyncCount |
|------|-----------|-----------|-------------------|-----------|
| Low | Android Perf Class = Low, OR Shader Level < 35, OR GPU Memory < 512 MB | 30 | 0.25 | 0 |
| Medium | Default (no signal determines Low or High) | 60 | 0.65 | 0 |
| High | Android Perf Class = High, OR Shader Level ≥ 46, AND GPU Memory ≥ 1536 MB | 60 | 1.0 | 0 |

**Tier decision rule** (signal priority order, aligned with quality-tier-system.md GDD — authoritative):
1. Android Performance Class (if available): Low class → Low tier; High class → High tier
2. Shader Level (`SystemInfo.graphicsShaderLevel`): < 35 → Low tier; ≥ 46 → High tier
3. GPU Memory (`SystemInfo.graphicsMemorySize`): < 512 MB → Low tier; ≥ 1536 MB → High tier
4. Default: Medium tier

**Player override**: `PlayerPrefs.GetInt("qts.tier", -1)` — −1 = auto-detect; 0/1/2 = Low/Medium/High forced (for future Settings UI).

**`DensityMultiplier` application**: AnimationSystem reads `QualityTierSystem.Instance.DensityMultiplier` when `OnLevelLoaded` fires and calls `// Per-instance VisualEffect.SetFloat() — no global VFX API exists; each VisualEffect must be set individually
foreach (var vfx in _activeVFXInstances)
    vfx.SetFloat("quality_density_multiplier", value)`. All VFX Graph assets must expose this property. Set once per level load, not per-frame.

### Post-Processing — Bloom Configuration

Bolt settle glow is implemented as URP post-processing Bloom via a `Volume` component in the game scene. Bloom triggers from sprite/material emissive color values above 1.0 (HDR).

```
Volume (Global, priority 0)
  └── Volume Profile
       └── Bloom override
            ├── Intensity: [to be calibrated in-engine — see Verification Required]
            ├── Threshold: [to be calibrated in-engine]
            └── Mode: Dual Kawase (default URP 17.x; efficient on tile-based GPU)
```

> **Unity 6.3 note**: Glow/bloom is now applied **before** tonemapping in the URP post-processing stack. Any bloom intensity values carried over from pre-6.3 prototypes will appear visually different. All bloom settings must be calibrated in-engine in Unity 6.3, not from GDD-specified numeric values. The GDD's bloom intensity tuning knobs are advisory starting points only.

### Custom ScriptableRendererFeature Pattern

All custom render features (if needed, e.g., for post-processing effects in ADR-0010) must follow Render Graph pattern:

```csharp
// ScriptableRendererFeature subclass
public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
{
    renderer.EnqueuePass(_pass);  // enqueue the pass
}

// ScriptableRenderPass subclass — Render Graph required
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
{
    // Schedule work on the render graph here
    // Do NOT override Execute() for new features
}
```

**FORBIDDEN in all render feature code:**
- `SetupRenderPasses()` — removed in Unity 6.3, compile error
- `enableRenderCompatibilityMode` property — removed in Unity 6.3, compile error
- `Execute(ScriptableRenderContext, ref RenderingData)` override — old compatibility-mode path; use `RecordRenderGraph` instead

### VFX Graph Compute Check

`SystemInfo.supportsComputeShaders` is checked in `QualityTierSystem.Awake()`. Result stored as `QualityTierSystem.SupportsVFXGraph`. AnimationSystem reads this flag when deciding whether to use VFX Graph or fall back to sprite-based animation (fallback decision → ADR-0010).

### Architecture Diagram

```
Device startup
    │
    ▼
QualityTierSystem.Awake() [SEO -100]
    ├── Read SystemInfo (graphicsMemorySize, graphicsShaderLevel, Android Perf Class)
    ├── PlayerPrefs("qts.tier") → override if set
    ├── Determine ActiveTier (Low/Medium/High)
    ├── QualitySettings.vSyncCount = 0
    ├── Application.targetFrameRate = tier.TargetFrameRate
    ├── DensityMultiplier = tier.Value (0.25 / 0.65 / 1.0)
    └── SupportsVFXGraph = SystemInfo.supportsComputeShaders
    
Scene loads
    │
    ▼
URP 2D Renderer Data asset (configured in editor):
    ├── HDR: enabled
    ├── On-Tile Post Processing: enabled
    └── Render Scale: 1.0
    
GSM.OnLevelLoaded fires
    │
    ▼
AnimationSystem reads QualityTierSystem.DensityMultiplier
    └── // Per-instance VisualEffect.SetFloat() — no global VFX API exists; each VisualEffect must be set individually
foreach (var vfx in _activeVFXInstances)
    vfx.SetFloat("quality_density_multiplier", value)
         └── All VFX Graph assets apply density at next particle spawn
```

### Key Interfaces

```csharp
public class QualityTierSystem : MonoBehaviour
{
    public static QualityTierSystem Instance { get; private set; }

    public QualityTier ActiveTier { get; private set; }       // Low / Medium / High
    public float DensityMultiplier { get; private set; }       // 0.25 / 0.65 / 1.0
    public int TargetFrameRate { get; private set; }           // 30 / 60
    public bool SupportsVFXGraph { get; private set; }         // SystemInfo.supportsComputeShaders
}

public enum QualityTier { Low, Medium, High }
```

## Alternatives Considered

### Alternative A: URP Forward Renderer (3D)
- **Description**: Use the URP Forward (3D) Renderer Data asset instead of the 2D Renderer Data asset.
- **Pros**: More flexibility for 3D-style lighting; easier to find online resources
- **Cons**: 2D Renderer includes 2D-specific features (2D Lights, Pixel Perfect Camera, 2D shadows) that provide better tooling for 2D puzzle game art direction. 2D Renderer's tile-based optimization path is better characterized for the target GPU backends.
- **Rejection Reason**: This is a 2D game. 2D Renderer is the correct tool.

### Alternative B: Built-in Render Pipeline
- **Description**: Use Unity's legacy built-in pipeline instead of URP.
- **Pros**: Largest set of online tutorials; no Render Graph complexity
- **Cons**: No Shader Graph support (required for custom bolt materials); no On-Tile Post Processing; no per-renderer quality settings; deprecated path in Unity 6.x; no future engine support direction.
- **Rejection Reason**: Shader Graph and URP are in the allowed-technology list; built-in pipeline is not.

### Alternative C: Reduce Render Scale on Low-Tier
- **Description**: Set URP render scale to 0.75 on Medium and 0.5 on Low tier to reduce fill rate.
- **Pros**: Significant GPU fill rate reduction on Low tier
- **Cons**: Sub-pixel shimmer on pixel-art sprites; TextMeshPro UI elements become blurry; unacceptable visual quality for a clean puzzle game. Density multiplier on VFX Graph achieves equivalent performance gain without quality loss.
- **Rejection Reason**: Visual quality regression; density multiplier is the better mechanism for this game type.

## Consequences

### Positive
- Render Graph compliance is enforced from day one — no migration debt when a future developer writes a render feature
- On-Tile Post Processing + bloom on tile-based GPU (Mali, Apple GPU): eliminates the most expensive memory bandwidth operation in the pipeline
- Fixed 1.0 render scale preserves crisp pixel-art and UI text across all tiers
- VFX density multiplier is data-driven (per-instance `VisualEffect.SetFloat()` — no global VFX API in Unity 6.x) — no code changes when tuning quality tiers

### Negative
- Bloom intensity values from any pre-6.3 prototype work are invalid in Unity 6.3 — visual recalibration required in-engine before animation sprint
- Developers must write `RecordRenderGraph()` (not `Execute()`) for all custom render passes — unfamiliar pattern for devs coming from pre-6.0 Unity
- HDR requirement means the 2D Renderer Data asset must be specifically configured; default project setup in Unity 6.3 may not have HDR enabled

### Risks
- **Risk**: Developer writes `SetupRenderPasses()` override on a custom renderer feature → compile error in Unity 6.3, blocks build. **Mitigation**: `setup_render_passes_forbidden` forbidden pattern registered in architecture registry; CI compile gate; control manifest.
- **Risk**: `QualitySettings.vSyncCount` not set to 0 before `Application.targetFrameRate` → `targetFrameRate` silently ignored on mobile; game runs at display refresh rate (60/120Hz), ignoring tier-appropriate 30fps target. **Mitigation**: Both calls documented in this ADR; control manifest requires `vSyncCount = 0` before `targetFrameRate` in QualityTierSystem.Awake().
- **Risk**: VFX Graph enabled on device where `supportsComputeShaders = true` but compute performance is marginal → frame time spikes on some Low-tier devices. **Mitigation**: ADR-0010 defines the fallback path; budget check after warm-up frame recommended in ADR-0010.
- **Risk**: On-Tile Post Processing option not visible in Unity 6.3 editor for 2D Renderer Data assets (UI may differ from older version docs). **Mitigation**: Verification Required item in Engine Compatibility table; test in-editor before animation sprint.
- **Risk**: Bloom doesn't trigger if HDR is disabled on 2D Renderer Data → bolt settle glow is invisible; animation sprint looks broken. **Mitigation**: HDR toggle documented here; Verification Required checklist item.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| quality-tier-system.md | TR-QTS-001: Detect tier at startup: GPU memory, shader level, Android Perf Class | Documents tier decision rule with signal priority order; `SystemInfo` API calls confirmed |
| quality-tier-system.md | TR-QTS-002: Apply `density_multiplier` + framerate before first scene | Mandates `vSyncCount = 0` + `targetFrameRate` + `DensityMultiplier` set in `QualityTierSystem.Awake()` [SEO −100] |
| animation-system.md | TR-ANIM-005: `quality_density_multiplier` from QTS at `level_loaded` | Documents that AnimationSystem reads `DensityMultiplier` on `GSM.OnLevelLoaded` and calls `// Per-instance VisualEffect.SetFloat() — no global VFX API exists; each VisualEffect must be set individually
foreach (var vfx in _activeVFXInstances)
    vfx.SetFloat("quality_density_multiplier", value)` |

## Performance Implications
- **CPU**: Tier detection in `Awake()` (6 `SystemInfo` reads): ~0.1ms. One-time cost at startup.
- **GPU**: On-Tile Post Processing: eliminates the full-screen framebuffer copy for post-processing on tile-based GPUs — estimated 10–30% GPU bandwidth reduction on bloom-active frames on Mali/Apple GPU hardware.
- **Memory**: VFX Graph density multiplier applied per-instance via `VisualEffect.SetFloat()` — O(N) where N ≤ 22 active instances at max board; negligible. URP 2D Renderer Data asset: ~1MB (bundled with project).
- **Load Time**: No load-time impact — all configuration is set in `Awake()` before first render.

## Migration Plan
No existing code to migrate — this ADR is written before implementation begins. All rendering configuration must follow this ADR from the first sprint.

## Validation Criteria
1. Editor: Confirm 2D Renderer Data asset has HDR = enabled, On-Tile Post Processing = enabled, Render Scale = 1.0
2. Device (Galaxy A14, Android): At Low tier, `targetFrameRate` = 30 confirmed via `Application.targetFrameRate` log in first frame
3. Device (iPhone): Bolt settle animation triggers bloom; glow visible and intensity calibrated to design intent
4. CI: Full compile check confirms zero references to `SetupRenderPasses`, `enableRenderCompatibilityMode`
5. Profiler (Android, Low tier): Frame time ≤ 33ms (30fps budget); bloom does not spike beyond 3ms GPU time per frame

## Related Decisions
- ADR-0001: Singleton Architecture and Boot Sequence — QualityTierSystem at SEO −100 owns rendering targets
- ADR-0009: Bolt Animation Strategy — depends on this pipeline for coroutine + VFX timing
- ADR-0010: VFX Graph and Bloom on Mobile — Low-tier fallback depends on `QualityTierSystem.SupportsVFXGraph`
- `docs/architecture/architecture.md` — Engine Knowledge Gap section (Rendering domain)
- `design/gdd/quality-tier-system.md` — Source of truth for tier thresholds and density values
- `design/gdd/animation-system.md` — Bloom and VFX Graph consumer
