# Unity — Deprecated APIs

Last verified: 2026-05-01 | Engine: Unity 6.3 LTS

## Core Object API

| Deprecated | Use Instead | Since | Notes |
|------------|-------------|-------|-------|
| `Object.FindObjectsOfType<T>()` | `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` | 6.0 | Sort mode required |
| `Object.FindObjectOfType<T>()` | `Object.FindFirstObjectByType<T>()` | 6.0 | Or `FindAnyObjectByType<T>()` if order doesn't matter |
| `GraphicsFormat.DepthAuto` | Explicit depth format | 6.0 | Compile error — must migrate |
| `GraphicsFormat.ShadowAuto` | Explicit shadow format | 6.0 | Compile error — must migrate |

## URP / Rendering

| Deprecated | Use Instead | Since | Notes |
|------------|-------------|-------|-------|
| `ScriptableRendererFeature.SetupRenderPasses()` | `ScriptableRendererFeature.AddRenderPasses()` + Render Graph | 6.2 | Removed in 6.3 |
| URP Compatibility Mode | Render Graph API | 6.2 | Fully removed in 6.3 |
| `RenderPipelineEditorUtility.FetchFirstCompatibleTypeUsingScriptableRenderPipelineExtension()` | `GetDerivedTypesSupportedOnCurrentPipeline()` | 6.0 | |
| `CustomEditorForRenderPipelineAttribute` | `[CustomEditor] + [SupportedOnRenderPipeline]` | 6.0 | |
| `VolumeComponentMenuForRenderPipelineAttribute` | `[VolumeComponentMenu] + [SupportedOnRenderPipeline]` | 6.0 | |
| Legacy ETC texture compressor | Default ETC compressor | 6.3 | Auto-converted; check visual output |

## UI Toolkit

| Deprecated | Use Instead | Since | Notes |
|------------|-------------|-------|-------|
| `ExecuteDefaultAction()` | `HandleEventBubbleUp()` | 6.0 | |
| `ExecuteDefaultActionAtTarget()` | `HandleEventTrickleDown()` | 6.0 | |
| `PreventDefault()` | `StopPropagation()` | 6.0 | |

## Accessibility

| Deprecated | Use Instead | Since | Notes |
|------------|-------------|-------|-------|
| `AccessibilityNode.selected` | `AccessibilityNode.invoked` | 6.3 | |

## Patterns (Not Just APIs)

| Deprecated Pattern | Use Instead | Why |
|--------------------|-------------|-----|
| `[SerializeField]` on properties or methods | `[field: SerializeField]` on auto-properties, or use backing fields | Compile error in 6.3+ |
| `FindObjectsOfType` without sort mode | `FindObjectsByType` with explicit sort mode | Removed in 6.0 |
| URP Scriptable Renderer Features using `SetupRenderPasses` | Render Graph-based features | Compatibility Mode removed in 6.3 |
| Bitwise operations on `AccessibilityRole` | Use individual enum values | Flags enum removed in 6.3 |
| Round/legacy Android icons | Adaptive icons | Required for Android 16+ compliance |
