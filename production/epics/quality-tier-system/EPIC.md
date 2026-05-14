# Epic: Quality Tier System

> **Layer**: Foundation
> **GDD**: design/gdd/quality-tier-system.md
> **Architecture Module**: QualityTierSystem
> **Status**: Ready
> **Manifest Version**: 2026-05-12
> **Stories**: Not yet created — run `/create-stories quality-tier-system`

## Overview

The Quality Tier System detects device performance at app launch, assigns one of three tiers (Low / Medium / High), and configures rendering and VFX settings accordingly before any scene loads. It is BoltSort's safety valve for visual quality across the fragmented Android market: by setting `Application.targetFrameRate` (30fps for Low, 60fps for Medium/High), `DensityMultiplier` (0.25 / 0.65 / 1.0 for VFX particle density), and checking `SystemInfo.supportsComputeShaders` (used by AnimationSystem to gate VFX Graph), it ensures the bolt-sort experience remains performant on a Samsung Galaxy A14 while still delivering the full glow-driven aesthetic on flagship devices. Detection runs once at startup using a priority-ordered signal chain: Android Performance Class first, then GPU shader level, then GPU memory — the first signal determining Low or High wins, with Medium as the fallback. The result is persisted in PlayerPrefs for all subsequent launches; players can override via the Settings UI. This system runs at Script Execution Order −100 — the first Awake of any session.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Singleton Architecture and Boot Sequence | QualityTierSystem is a DDOL singleton at SEO −100; must set `vSyncCount = 0` + `targetFrameRate` before any other Awake runs | HIGH |
| ADR-0005: Rendering Pipeline Configuration | Tier detection signal priority; `QualitySettings.vSyncCount = 0` must precede `Application.targetFrameRate`; `SupportsVFXGraph = SystemInfo.supportsComputeShaders`; URP 2D Renderer + HDR + On-Tile Post Processing configured in editor | HIGH |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-QTS-001 | Detect tier at startup: Android Perf Class (priority 1) → `graphicsShaderLevel` (priority 2) → `graphicsMemorySize` (priority 3); default Medium; result persisted in PlayerPrefs | ADR-0005 ✅ |
| TR-QTS-002 | Apply `DensityMultiplier` (0.25/0.65/1.0) and `targetFrameRate` (30/60) at SEO −100 before any scene or VFX initializes; `QualitySettings.vSyncCount = 0` must be set first | ADR-0001, ADR-0005 ✅ |

## Tier Profiles (from GDD — govern story acceptance criteria)

| Tier | Bloom Intensity | VFX Density Multiplier | Target FPS |
|------|----------------|----------------------|-----------|
| Low | 0.0 (disabled) | 0.25 | 30 |
| Medium | 0.6 | 0.65 | 60 |
| High | 1.0 | 1.0 | 60 |

**Tier decision thresholds:**
- Low: Android Perf Class = LOW, OR `graphicsShaderLevel` < 35, OR `graphicsMemorySize` < 512 MB
- High: Android Perf Class = HIGH, OR `graphicsShaderLevel` ≥ 46, AND `graphicsMemorySize` ≥ 1536 MB
- Medium: default (all other cases)

## Key Implementation Notes

- `QualitySettings.vSyncCount = 0` MUST be set before `Application.targetFrameRate` — if vSyncCount is non-zero, `targetFrameRate` is silently ignored on mobile
- `SupportsVFXGraph = SystemInfo.supportsComputeShaders` — stored as a public property; AnimationSystem reads this flag at `OnLevelLoaded` to decide VFX Graph vs ParticleSystem fallback
- Player override stored in `PlayerPrefs.GetInt("qts.tier", -1)` — `−1` = auto-detect; 0/1/2 = Low/Medium/High forced
- `DensityMultiplier` is applied per-instance via `VisualEffect.SetFloat("quality_density_multiplier", value)` — no global VFX API exists in Unity 6.x
- All three parameters (targetFrameRate, DensityMultiplier, SupportsVFXGraph) must be set atomically in Awake — no parameter set in isolation
- This system has no events — it is a read-only configuration source after Awake; `ActiveTier`, `DensityMultiplier`, and `SupportsVFXGraph` are readable as static properties

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/quality-tier-system.md` are verified
- Unit test: tier detection logic — supply mock `SystemInfo` values; verify correct tier for each signal combination and priority order
- Unit test: PlayerPrefs override — `qts.tier = 0` forces Low tier regardless of SystemInfo signals
- Device test (Galaxy A14, Low tier): `Application.targetFrameRate == 30` confirmed in first frame log; `DensityMultiplier == 0.25`
- Device test (high-end device, High tier): `targetFrameRate == 60`; `DensityMultiplier == 1.0`
- CI: `QualitySettings.vSyncCount = 0` set before `Application.targetFrameRate` (code review gate, not automated)

## Next Step

Run `/create-stories quality-tier-system` to break this epic into implementable stories.
