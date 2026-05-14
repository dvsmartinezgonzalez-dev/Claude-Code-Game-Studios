# Unity — Breaking Changes

Last verified: 2026-05-01 | Engine: Unity 6.3 LTS

Changes between Unity versions post-LLM-cutoff (6.0+).

## Unity 2023.x → Unity 6.0 (HIGH RISK)

| Subsystem | Change | Migration |
|-----------|--------|-----------|
| Core | `Object.FindObjectsOfType()` → `Object.FindObjectsByType(sortMode)` | Add sort mode parameter |
| Core | `Object.FindObjectOfType()` → `Object.FindFirstObjectByType()` or `FindAnyObjectByType()` | Choose appropriate variant |
| Core | `GraphicsFormat.DepthAuto/ShadowAuto/VideoAuto` removed | Compile error — use explicit formats |
| Core | Enlighten Baked GI backend removed | Migrate to Progressive Lightmapper |
| Rendering | Light probes now 100% brightness (was 94%) | Adjust baked probe values if needed |
| Rendering | Metal shader buffer layout changed for half/min16float | Recompile Metal shaders |
| URP | `SetupRenderPasses` deprecated → `AddRenderPasses` + Render Graph | Rewrite Scriptable Renderer Features |
| URP | URP Compatibility Mode deprecated (removed in 6.3) | Migrate all custom render features to Render Graph |
| URP | `RenderPipelineEditorUtility.FetchFirstCompatibleType...` deprecated | Use `GetDerivedTypesSupportedOnCurrentPipeline()` |
| UI Toolkit | `ExecuteDefaultAction/ExecuteDefaultActionAtTarget` → `HandleEventTrickleDown/HandleEventBubbleUp` | Update custom event handlers |
| UI Toolkit | `PreventDefault()` → `StopPropagation()` | Rename calls |
| Android | `UnityPlayer` Java class split into `UnityPlayerForActivityOrService` / `UnityPlayerForGameActivity` | Update Android native plugins |
| Android | `UnityPlayer` no longer extends `FrameLayout`; use `getFrameLayout()` | Update layout access |
| Android | Gradle 8.4, AGP 8.3.0, SDK Build Tools 34, JDK 17 required | Update build environment |
| Lighting | `LightingSettings.filteringGaussRadiusAO/Direct/Indirect` (int) → `filteringGaussianRadius...` (float) | Rename + cast |
| Packages | `UPM_CACHE_PATH` / `UPM_NPM_CACHE_PATH` env vars removed | Use `UPM_CACHE_ROOT` |

## Unity 6.0 → Unity 6.3 (HIGH RISK)

| Subsystem | Change | Migration |
|-----------|--------|-----------|
| URP | **Compatibility Mode fully removed** — code stripped by default | Any code using `enableRenderCompatibilityMode` must be removed |
| URP | `enableRenderCompatibilityMode` property now read-only | Remove all writes to this property |
| URP | Legacy ETC texture compression removed | Textures auto-convert; visual difference possible |
| C# | `[SerializeField]` now only valid on fields — compile error on properties/methods | Move to fields or use `[field: SerializeField]` |
| Accessibility | `AccessibilityRole` converted from flags enum to standard enum | Remove bitwise operations |
| Accessibility | `AccessibilityRole`/`AccessibilityState` types changed from `int` to `byte` | Recompile precompiled assemblies |
| Accessibility | `AccessibilityNode.selected` deprecated | Use `AccessibilityNode.invoked` |
| Android | Round and legacy icons deprecated | Use adaptive icons |
| Android | Android 16 large screen: set App Category to "Game" for orientation control | Add app category metadata |
| UI Toolkit | USS parser upgraded — previously-ignored invalid syntax now errors | Fix USS files with invalid syntax |
| Packages | `UPM_NPM_CACHE_PATH` deprecated | Use `UPM_CACHE_ROOT` |
| Editor | Search Index Manager removed | Use Preferences > Search > Indexing |
| Services | Facebook Instant Games deprecated | Migrate to Web platform |
