# Unity — Version Reference

| Field | Value |
|-------|-------|
| **Engine Version** | Unity 6.3 LTS |
| **Release Date** | 2025 (LTS designation) |
| **Project Pinned** | 2026-05-01 |
| **Last Docs Verified** | 2026-05-01 |
| **LLM Knowledge Cutoff** | May 2025 |
| **Support Until** | December 2027 |

## Knowledge Gap Warning

The LLM's training data covers Unity approximately up to Unity 2023.x / early 6000.0.
Unity 6.0 through 6.3 introduced significant changes the model may not know about.
Always cross-reference this directory before suggesting Unity API calls.

## Post-Cutoff Version Timeline

| Version | Key Theme | Risk Level |
|---------|-----------|------------|
| Unity 6.0 | Render Graph for URP custom passes; Android Java class split; FindObjectsOfType replaced | HIGH |
| Unity 6.1 | Incremental URP/2D improvements | MEDIUM |
| Unity 6.2 | URP Compatibility Mode deprecated; SetupRenderPasses deprecated | HIGH |
| Unity 6.3 (pinned) | URP Compatibility Mode REMOVED; SerializeField restriction enforced; Accessibility enum changes; Android adaptive icons required | HIGH |

## Rendering Pipeline

- **Pipeline**: Universal Render Pipeline (URP) 17.x
- **Render mode**: 2D Renderer asset
- **Backend**: Vulkan (Android), Metal (iOS)
- **Key mobile feature**: On-Tile Post Processing (Tile-Only Mode) — optimizes GPU bandwidth on Android/iOS tile-based GPUs

## Verified Sources

- Upgrade to Unity 6.0: https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity6.html
- Upgrade to Unity 6.3: https://docs.unity3d.com/6000.4/Documentation/Manual/UpgradeGuideUnity63.html
- What's New in URP 17: https://docs.unity3d.com/6000.3/Documentation/Manual/urp/whats-new/urp-whats-new.html
