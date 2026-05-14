# Quality Tier System

> **Status**: Designed (pending /design-review in a fresh session)
> **Author**: Design session + systems-designer agent
> **Last Updated**: 2026-04-18
> **Implements Pillar**: Every Pixel Earns Its Place, Flow Over Friction

## Overview

The Quality Tier System detects device performance at app launch, assigns one of three tiers (Low, Medium, High), and configures rendering and gameplay feedback settings accordingly. It is BoltSort's safety valve for visual quality on a fragmented Android market: by capping bloom intensity, particle density, and target frame rate on Low-tier devices, it ensures the glow-driven sci-fi aesthetic remains performant across a wide hardware range without requiring artists to author separate asset sets. Detection runs once at startup using `SystemInfo` data (GPU memory, shader capability level, and — if available — Android performance class), stores the result in a persistent preference, and applies the appropriate configuration profile before any scene loads. Players can override the auto-detected tier from the Settings UI at any time; the override is persisted across sessions. All other systems — Animation System and Settings UI — treat the Quality Tier System as a read-only configuration source: they query the current active tier and apply its parameters; they never set the tier themselves.

## Player Fantasy

No matter what device you hold, the machine hums at the same rhythm. The bolts slide with the same weight, the stacks complete with the same satisfying snap, and nothing ever stutters between your finger and the result. The player on a three-year-old phone and the player on a flagship both get the same game — just tuned to their workshop. When this system succeeds, it disappears; the player only notices the puzzle.

This system has no player fantasy of its own. Its success is measured by its invisibility — when the Quality Tier System is correct, players experience only the game at its best for their device. When it fails (wrong tier detected, wrong profile applied, stutter on first load), it breaks the opening moment before the player has even formed a positive impression. The first five seconds on a new device are the most fragile in the session.

## Detailed Design

### Core Rules

**Group A — Detection Logic**

Detection establishes the active tier on first launch. All subsequent launches load the persisted result. Detection never runs more than once per install lifetime unless the player explicitly resets to auto-detected.

| Rule | Condition | Outcome |
|---|---|---|
| QT-01 | First launch: no persisted tier value exists in storage | Run detection (QT-02–QT-13). Write result to storage immediately. |
| QT-02 | Subsequent launch: persisted tier exists | Load persisted tier and source. Skip detection entirely. |
| QT-03 | Detection — signal priority order | Evaluate signals in this order: (1) Android Performance Class, (2) Shader Level, (3) GPU Memory. First signal that produces Low or High terminates evaluation. Fallback = Medium. |
| QT-04 | Android only, API 31+: `devicePerformanceClass` = HIGH | Detected tier = High. Terminate detection. |
| QT-05 | Android only, API 31+: `devicePerformanceClass` = LOW | Detected tier = Low. Terminate detection. |
| QT-06 | Android below API 31 or iOS, or `devicePerformanceClass` = UNKNOWN | Skip to QT-07. Signal is unavailable or inconclusive. |
| QT-07 | `graphicsShaderLevel` < 35 | Detected tier = Low. Terminate detection. |
| QT-08 | `graphicsShaderLevel` ≥ 46 | Detected tier = High. Terminate detection. |
| QT-09 | `graphicsShaderLevel` 35–45 inclusive | Inconclusive. Fall through to QT-10. |
| QT-10 | `graphicsMemorySize` < 512 MB | Detected tier = Low. Terminate detection. |
| QT-11 | `graphicsMemorySize` 512–1535 MB inclusive | Detected tier = Medium. Terminate detection. |
| QT-12 | `graphicsMemorySize` ≥ 1536 MB | Detected tier = High. Terminate detection. |
| QT-13 | All signals inconclusive or unavailable | Detected tier = Medium (fallback). |

**iOS note:** `devicePerformanceClass` is Android-only. On iOS, QT-04–QT-06 are always skipped; detection proceeds directly to QT-07 (Shader Level). This is not an error condition — it is the defined iOS path.

---

**Group B — Tier Profiles**

Each tier defines exactly three parameters. All three are applied atomically — no parameter is set in isolation.

| Tier | Bloom intensity | VFX density multiplier | Target frame rate |
|---|---|---|---|
| **Low** | 0.0 (disabled) | 0.25 | 30 fps |
| **Medium** | 0.6 (see Tuning Knobs) | 0.65 | 60 fps |
| **High** | 1.0 (authored maximum) | 1.0 | 60 fps |

**Bloom:** Controls the URP Volume post-processing Bloom effect's intensity value. At 0.0, bloom is fully disabled — sprites must read clearly by their intrinsic luminance and color contrast alone. The art team must validate that the dark-background aesthetic is still legible without bloom on Low-tier devices.

**VFX density multiplier:** A global float property (`quality_density_multiplier`) that all VFX Graph assets expose and read at initialization. Values below 1.0 reduce particle emission rate and max particle count proportionally. VFX systems must not cache their authored counts before this value is applied (see Profile Application).

**Frame rate:** Applied via `Application.targetFrameRate`. Requires `QualitySettings.vSyncCount = 0` on mobile — if vSyncCount is non-zero, `targetFrameRate` is ignored. The URP asset must have vSyncCount disabled for mobile platforms.

---

**Group C — Override Logic**

| Rule | Condition | Outcome |
|---|---|---|
| QT-14 | Player selects a tier in Settings UI | Write tier value + source = `player-override` to storage. Apply the new tier's profile immediately (hot-swap, no restart). Emit `tier_changed(tier, source)`. |
| QT-15 | Player selects "Reset to Auto-Detected" | Re-run detection (QT-02–QT-13). Write result + source = `auto-detected` to storage. Apply the new tier's profile immediately. Emit `tier_changed(tier, source)`. |
| QT-16 | Player override active | Source = `player-override` → auto-detection is irrelevant until player explicitly resets. Source = `auto-detected` → detection may update the tier on reset. |
| QT-17 | App uninstall / reinstall | Persisted prefs cleared. First launch after reinstall runs detection from scratch. |
| QT-18 | iOS PlayerPrefs flush (rare OS event) | Treat as no persisted value. Re-run detection on next launch. No error is shown to the player. |

Storage persists two fields: `tier_value` (Low / Medium / High) and `tier_source` (auto-detected / player-override). The Settings UI uses `tier_source` to label the current setting: "Auto-detected: Medium" vs "Manual: High."

---

**Group D — Profile Application Sequence**

Profile application is split across two Unity startup hooks. `BeforeSceneLoad` runs before any scene objects exist (no Volume, no VFX systems available).

| Step | Hook | Action |
|---|---|---|
| D-01 | `BeforeSceneLoad` | Read persisted tier and source from storage. If none: run detection (Group A). Write result to storage. |
| D-02 | `BeforeSceneLoad` | Set `Application.targetFrameRate` to the tier's value. This requires no scene objects. |
| D-03 | First scene `Awake` (persistent manager) | Instantiate a runtime copy of the global URP Volume Profile (do NOT modify the shared asset). Set `bloom.intensity.value` to the tier's bloom value. |
| D-04 | First scene `Awake` | Set the global `quality_density_multiplier` property. All VFX Graph assets read this value when they initialize — they must not cache authored counts before `Awake` has run. |
| D-05 | `tier_changed` event (runtime) | When player changes tier in Settings UI: re-apply D-02, D-03, D-04 immediately. The runtime Volume Profile copy is already instantiated; update its values in place. |

---

### States and Transitions

```
UNINITIALIZED → DETECTING → APPLYING → ACTIVE_AUTO
                           ↑                    ↓ (player overrides)
              LOADING_PERSISTED → APPLYING → ACTIVE_OVERRIDE
                                                    ↓ (player resets)
                                              RESETTING → DETECTING
```

| State | Entry | Exit |
|---|---|---|
| `UNINITIALIZED` | App launch before startup hook runs | → `DETECTING` (no persisted value) or → `LOADING_PERSISTED` (value found) |
| `DETECTING` | No persisted tier; detection rules execute | → `APPLYING` when a tier result is produced |
| `LOADING_PERSISTED` | Persisted tier and source found in storage | → `APPLYING` when loaded |
| `APPLYING` | Tier value available; write parameters to runtime | → `ACTIVE_AUTO` (source = auto) or `ACTIVE_OVERRIDE` (source = player-override) |
| `ACTIVE_AUTO` | Profile active; auto-detected source | → `ACTIVE_OVERRIDE` on player manual override; → `RESETTING` on "Reset to Auto-Detected" |
| `ACTIVE_OVERRIDE` | Profile active; player-override source | → `RESETTING` on "Reset to Auto-Detected" |
| `RESETTING` | "Reset to Auto-Detected" triggered | → `DETECTING` to re-run detection; result → `APPLYING` → `ACTIVE_AUTO` |

The system is passive in `ACTIVE_AUTO` and `ACTIVE_OVERRIDE` — it holds the active tier and responds only to events from Settings UI.

---

### Interactions with Other Systems

**Animation System (downstream)**

| Interface | Direction | Details |
|---|---|---|
| `GetActiveTier()` | Animation System reads from QTS | Synchronous. Returns the active tier enum at scene load. Animation System uses this to determine its particle effect budget and any tier-specific animation choices. |
| `tier_changed(tier, source)` | QTS emits; Animation System subscribes | Hot-swap: Animation System re-reads VFX density multiplier and adjusts any running VFX systems mid-session if player changes tier in Settings. |

**Settings UI (downstream)**

| Interface | Direction | Details |
|---|---|---|
| `GetActiveTier()` | Settings UI reads | For display: show current tier and source label. |
| `set_tier(value)` | Settings UI emits | Player-initiated tier override. QTS applies immediately (QT-14). |
| `reset_tier()` | Settings UI emits | Player reset. QTS re-runs detection (QT-15). |
| `tier_changed(tier, source)` | QTS emits; Settings UI subscribes | Update display after override or reset completes. |

**Level Data System / Sort Mechanic / Game State Manager:** No interaction. QTS is infrastructure — game logic systems do not read from it.

**Save & Persistence (future migration):** QTS uses `PlayerPrefs` directly for tier persistence. It does not route through the Save & Persistence system (Beta-scope). When Save & Persistence is designed, the tier prefs should migrate into it. Flag as a migration item in the Save & Persistence GDD.

## Formulas

### F-1: `detect_tier()` — Piecewise Detection Function

The tier detection function is a priority-ordered piecewise lookup — not an algebraic expression. It is formally defined as:

```
detect_tier(platform, perf_class, shader_level, gpu_mem_mb) → {Low, Medium, High}

= High,   if platform = Android ∧ API ≥ 31 ∧ perf_class = HIGH
= Low,    if platform = Android ∧ API ≥ 31 ∧ perf_class = LOW
  (else fall through)
= Low,    if shader_level < 35
= High,   if shader_level ≥ 46
  (else fall through)
= Low,    if gpu_mem_mb < 512
= Medium, if 512 ≤ gpu_mem_mb ≤ 1535
= High,   if gpu_mem_mb ≥ 1536
= Medium  (fallback — all signals inconclusive or unavailable)
```

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Runtime platform | `platform` | enum | {Android, iOS} | `Application.platform` at startup |
| Android Performance Class | `perf_class` | enum | {HIGH, LOW, UNKNOWN, UNAVAILABLE} | `devicePerformanceClass` on Android API 31+; UNAVAILABLE on iOS or pre-API 31 |
| Shader capability level | `shader_level` | int | 0–500 (Unity-defined) | `SystemInfo.graphicsShaderLevel` |
| GPU memory | `gpu_mem_mb` | int | 0–unbounded | `SystemInfo.graphicsMemorySize` in MB |
| Result | — | enum | {Low, Medium, High} | Active tier; persisted immediately after detection |

**Output range:** Always one of {Low, Medium, High}. The fallback branch guarantees no null result regardless of signal availability.

**Worked example — mid-range Android device, API level 30:**
- `perf_class` = UNAVAILABLE (API < 31) → skip Performance Class branch
- `shader_level` = 40 → 35–45 range → inconclusive → fall through
- `gpu_mem_mb` = 1024 → 512–1535 range → result = **Medium**

---

### F-2: Tier Profile Parameter Table

Tier profiles are static constant mappings — there is no algebraic expression. The table below is the authoritative specification of what each tier sets.

| Tier | `bloom_intensity` | `quality_density_multiplier` | `target_frame_rate` |
|---|---|---|---|
| Low | 0.0 | 0.25 | 30 |
| Medium | 0.6 (tuning knob — see Tuning Knobs) | 0.65 | 60 |
| High | 1.0 | 1.0 | 60 |

**Output ranges per parameter:**

| Parameter | Min | Max | Unit | Notes |
|---|---|---|---|---|
| `bloom_intensity` | 0.0 | 1.0 | — | URP Volume Bloom intensity. 0.0 = disabled. 1.0 = authored maximum. |
| `quality_density_multiplier` | 0.25 | 1.0 | — | VFX Graph global float property. All VFX assets must expose this property name and read it at initialization. |
| `target_frame_rate` | 30 | 60 | fps | Set via `Application.targetFrameRate`. Only valid when `vSyncCount = 0`. |

---

### F-3: Detection Tier Invariant

After `detect_tier()` completes or a persisted tier is loaded, the following must hold:

> Exactly one of {Low, Medium, High} is the active tier, and `tier_source` ∈ {auto-detected, player-override}. No combination of signal values, including all signals being unavailable, can produce a null tier or an unset source. The fallback branch guarantees a result.

This is a postcondition contract that downstream systems (`GetActiveTier()` callers) rely on. `GetActiveTier()` never returns null in ACTIVE_AUTO or ACTIVE_OVERRIDE states.

## Edge Cases

**EC-QT-01 — `graphicsMemorySize` returns 0 (unified memory devices):** If `SystemInfo.graphicsMemorySize` returns 0, treat this value as UNAVAILABLE — do NOT apply QT-10 (which would assign Low). Skip QT-10 through QT-12 and fall through to QT-13 (fallback = Medium). Rationale: some Android devices with unified CPU/GPU memory report 0 MB from Unity's API; 0 is a measurement failure, not a genuine low-memory device. **This case amends QT-10** — the check `gpu_mem_mb < 512` only applies when `gpu_mem_mb > 0`.

**EC-QT-02 — `graphicsShaderLevel` returns 0:** Rule QT-07 (`shader_level < 35 → Low`) handles this correctly. Shader level 0 means the GPU does not support shaders — Low is the correct and intentional outcome. This is documented to clarify that `shader_level = 0` is a valid Low-tier trigger, not a missing-data condition requiring special handling.

**EC-QT-03 — `devicePerformanceClass` returns an unexpected value:** If the Android API returns a `devicePerformanceClass` value that is not HIGH, LOW, or UNKNOWN (e.g., a future MEDIUM value, a binding error, or a platform-specific extension), treat it as UNAVAILABLE and skip to QT-07 (Shader Level). The `perf_class` switch must have an explicit catch-all default case — do not allow an unrecognized value to fall through silently.

**EC-QT-04 — Storage write fails during detection or override:** If the PlayerPrefs write fails after QT-01 detection completes, the in-memory tier is correct for this session. No error is shown to the player. The failure is logged to the device log. On the next launch, detection re-runs (QT-01 fires again — no persisted value). If the write fails after a QT-14 player override, the override applies for this session only and is lost on next launch. Acceptable — storage write failures are rare, and the correct behavior is silent re-detection, not a user error.

**EC-QT-05 — PlayerPrefs key namespace:** PlayerPrefs keys for this system are `qts.tier_value` and `qts.tier_source`. These are the canonical key names and must not be changed post-launch (changing keys loses all existing player preferences). The `qts.*` namespace must be reserved by the Save & Persistence GDD when it is authored.

**EC-QT-06 — iOS `devicePerformanceClass` path:** On iOS, `devicePerformanceClass` is an Android-only API and is never called. Detection begins directly at QT-07 (Shader Level). `SystemInfo.graphicsMemorySize` on iOS reflects total device RAM (not dedicated VRAM, which does not exist on Apple Silicon). High tier assignment for modern iPhones (≥6 GB RAM, Metal shader level) is the expected and correct outcome. No special iOS handling required beyond the Android skip.

**EC-QT-07 — VFX Graph asset initializes before D-04 runs:** The QTS persistent manager must have its Script Execution Order set to execute before all VFX-bearing GameObjects in Project Settings. This is an implementation constraint enforced at build time. If the ordering is violated (a VFX asset's `Awake` fires before D-04 sets `quality_density_multiplier`), the VFX runs at authored density for that session. This is a silent misconfiguration, not a crash — but it defeats the purpose of the system on Low-tier devices.

**EC-QT-08 — `tier_changed` fires mid-burst on a VFX Graph particle system:** When the player changes tier in Settings UI during an active VFX burst (e.g., stack completion particles), live particles complete at their current emission density. `quality_density_multiplier` applies to the next emission initialization — VFX Graph does not retroactively cull active particles. This behavior is intentional: interrupting a burst mid-frame would produce a visual pop that is more noticeable than density inconsistency for one burst. Animation System subscribers to `tier_changed` must not attempt to reinitialize VFX systems mid-burst.

**EC-QT-09 — `vSyncCount` non-zero defeats `targetFrameRate`:** If `QualitySettings.vSyncCount` is non-zero at the time D-02 runs, Unity ignores `Application.targetFrameRate`. Low-tier devices run at display refresh rate (60 Hz or higher) instead of 30 fps. D-02 must explicitly set `QualitySettings.vSyncCount = 0` before setting `Application.targetFrameRate`. This is a proactive guard — do not assume the URP mobile quality level has vSyncCount disabled.

**EC-QT-10 — Rapid tier changes in Settings UI:** Multiple consecutive `set_tier()` calls within a single frame are idempotent — the final tier value is applied, earlier calls are overwritten. Storage reflects the last write. If `tier_changed` fires multiple times before the Animation System processes the previous event, the Animation System must apply last-write-wins: discard unprocessed `tier_changed` events and apply only the most recent tier. No visual transition animation should be started for a tier that was immediately superseded.

**EC-QT-11 — Volume Profile shared asset modified at runtime:** D-03 requires instantiating a runtime copy of the URP Volume Profile. If `volume.sharedProfile` is modified directly at runtime, the modification persists to the project asset on disk in the editor and may affect other scenes in a build. D-03 must use `volume.profile` (which returns a runtime instance on first access in Unity URP). The persistent manager is `DontDestroyOnLoad` — the runtime copy persists across all scene transitions for the session. It is never shared with the project asset.

**EC-QT-12 — `GetActiveTier()` called before QTS reaches ACTIVE state:** If any system calls `GetActiveTier()` before the QTS persistent manager's `Awake` has completed (e.g., Animation System has a higher Execution Order than the persistent manager, or calls during its own `Awake`), the system has not yet entered ACTIVE state. `GetActiveTier()` must return Medium (the fallback tier) and log a warning. It must not throw or return null. The Animation System GDD must document that `GetActiveTier()` should not be called before the caller's `Start()` to avoid this window.

**EC-QT-13 — OS update changes `devicePerformanceClass` availability between sessions:** A device updates from Android API 30 to API 31 between sessions. QT-02 fires on subsequent launches and skips detection — the persisted tier from the pre-upgrade session remains authoritative. The system does not re-detect on OS upgrade. If the persisted tier is wrong for the post-upgrade detection environment, the player can reset via Settings UI. Accepted limitation of the "detect once per install" design.

**EC-QT-14 — `reset_tier()` called when source is already `auto-detected`:** If the player triggers "Reset to Auto-Detected" while `tier_source` is already `auto-detected`, detection re-runs and produces the same result, writes it back, and emits `tier_changed`. This is a valid no-op code path. The Settings UI display label does not change. No special handling required — the re-detection is cheap and deterministic.

**EC-QT-15 — VFX Graph asset missing `quality_density_multiplier` property:** If a VFX Graph asset does not expose the `quality_density_multiplier` float property, D-04's global set is silently ignored for that asset — it runs at authored density regardless of tier. This is a process failure that must be caught by an editor validation tool (not a runtime guard). Acceptance criterion AC-QT-14 requires a validation script that enumerates all VFX Graph assets and verifies the property is exposed.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| Animation System | Downstream — depends on QTS | Read + event. Animation System reads `GetActiveTier()` at scene load and subscribes to `tier_changed` for mid-session updates. Owns all VFX Graph assets that must expose `quality_density_multiplier`. | Exposes: `GetActiveTier()`. Emits to Animation System: `tier_changed(tier, source)`. Constraint: Animation System must not call `GetActiveTier()` before its `Start()` (EC-QT-12). |
| Settings UI | Downstream — depends on QTS | Read + command. Settings UI reads active tier for display, emits `set_tier()` and `reset_tier()` commands. Subscribes to `tier_changed` for display updates. | Exposes: `GetActiveTier()`. Subscribes from Settings UI: `set_tier(value)`, `reset_tier()`. Emits to Settings UI: `tier_changed(tier, source)`. |
| Save & Persistence | Implicit / future migration | QTS uses `PlayerPrefs` directly under the `qts.*` namespace. When Save & Persistence (Beta) is designed, the `qts.*` namespace must be reserved and tier prefs migrated at that time. | No current interface — direct PlayerPrefs access. Future: Save & Persistence GDD must reserve `qts.*` key namespace. |

**Hard vs. soft dependencies:**
- Animation System: **soft** — QTS emits events regardless. If Animation System is absent (test environment), `GetActiveTier()` still works and `tier_changed` fires to no subscriber.
- Settings UI: **soft** — QTS functions fully without Settings UI. The override path is simply unavailable to the player.
- Save & Persistence: **soft** (currently not connected; future migration only).

**QTS has no upstream dependencies.** It reads only from Unity `SystemInfo` (a platform API, not a game system) and `PlayerPrefs`. It does not depend on Level Data System, Game State Manager, Sort Mechanic, or any other BoltSort system.

**Bidirectional consistency:** The Animation System GDD must list Quality Tier System in its Dependencies section when authored. The Settings UI GDD must also list it.

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect if Too High | Effect if Too Low |
|---|---|---|---|---|
| `shader_level` Low ceiling (QT-07 threshold) | 35 | 25–40 | Wider Low range — more capable devices receive Low | Narrower Low range — some weak GPU devices escape to Medium |
| `shader_level` High floor (QT-08 threshold) | 46 | 42–50 | Narrower High range — capable devices land in Medium | Wider High range — mid-range devices receive High, potential frame drops |
| `gpu_mem_mb` Low ceiling (QT-10 threshold) | 512 MB | 256–768 MB | More devices receive Low | Fewer devices receive Low — some weak devices receive Medium |
| `gpu_mem_mb` High floor (QT-12 threshold) | 1536 MB | 1024–2048 MB | Fewer devices receive High | More devices receive High — potential frame drops on mid-range hardware |
| Medium tier `bloom_intensity` | 0.6 | 0.3–0.9 | Bloom indistinguishable from High (tier differentiation collapses) | Bloom too faint to read — visual identity weakens on Medium |
| Medium tier `quality_density_multiplier` | 0.65 | 0.4–0.85 | VFX density indistinguishable from High | VFX feels sparse relative to audio feedback |
| Low tier `quality_density_multiplier` | 0.25 | 0.1–0.4 | VFX feels heavy for a frame-rate-constrained device | VFX barely visible — sort completion feels flat |

**Knob interactions:**
- The `shader_level` Low ceiling (35) and High floor (46) define a Medium band (35–45). Widening or narrowing this band changes how many devices land in Medium. Too wide = tier system loses differentiation; too narrow = effectively binary detection.
- The `gpu_mem_mb` thresholds are a tiebreaker when shader level is inconclusive. If shader thresholds are made more aggressive, GPU memory is consulted less often.
- `bloom_intensity` at Medium must be calibrated on real mid-range Android hardware before shipping. The art team must validate that 0.6 reads as "intentionally reduced," not "broken."

**All four detection thresholds require device matrix validation before Beta milestone.** Current values are recommendations based on published GPU capability tiers, not measured data. Validate against 5–10 representative Android devices (budget to flagship) before locking.

## Visual/Audio Requirements

[To be designed]

## UI Requirements

[To be designed]

## Acceptance Criteria

> **Test type**: Logic (detection rules, profile application, null-safety). BLOCKING tests (unit + integration) are required before implementation stories can be marked Done. ADVISORY tests (device, editor script) require documented evidence in `production/qa/evidence/`.
>
> Unit tests: `tests/unit/quality-tier-system/` (EditMode, injected `ISystemInfoProvider` + `IPrefsProvider` stubs)
> Integration tests: `tests/integration/quality-tier-system/` (PlayMode, full Unity startup sequence)

**AC-QT-01 — First launch triggers detection** *(BLOCKING — Unit)*
**GIVEN** no `qts.tier_value` key exists in the prefs stub, **WHEN** `TierDetectionService.Resolve(prefs, systemInfo)` is called, **THEN** the service evaluates detection signals (Performance Class, Shader Level, GPU Memory) and returns exactly one of {Low, Medium, High} — never null.

**AC-QT-02 — Subsequent launch skips detection entirely** *(BLOCKING — Unit)*
**GIVEN** `qts.tier_value = "Medium"` and `qts.tier_source = "auto-detected"` exist in the prefs stub, **WHEN** `TierDetectionService.Resolve(prefs, systemInfo)` is called, **THEN** return value is Medium and the `systemInfo` stub records zero calls (no detection signals were read).

**AC-QT-03 — Android Performance Class HIGH terminates detection as High** *(BLOCKING — Unit)*
**GIVEN** platform = Android, API = 31, `devicePerformanceClass` = HIGH, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is High and the stub records no calls to `graphicsShaderLevel` or `graphicsMemorySize`.

**AC-QT-04 — Android Performance Class LOW terminates detection as Low** *(BLOCKING — Unit)*
**GIVEN** platform = Android, API = 31, `devicePerformanceClass` = LOW, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is Low and the stub records no calls to lower-priority signals.

**AC-QT-05 — Shader level below 35 maps to Low tier** *(BLOCKING — Unit)*
**GIVEN** platform = iOS, `graphicsShaderLevel` = 20, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is Low and stub records no call to `graphicsMemorySize`.

**AC-QT-05b — Shader level 0 maps to Low tier, not treated as UNAVAILABLE** *(BLOCKING — Unit)*
**GIVEN** `graphicsShaderLevel` = 0, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is Low. (Confirms that 0 is a valid Low trigger — distinct from gpu_mem = 0 behavior in AC-QT-12.)

**AC-QT-06 — Shader level 46 or above maps to High tier** *(BLOCKING — Unit)*
**GIVEN** `graphicsShaderLevel` = 46, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is High and stub records no call to `graphicsMemorySize`. A second case with `shader_level = 500` must also return High.

**AC-QT-07 — Shader level 35–45 is inconclusive and reads GPU memory** *(BLOCKING — Unit)*
**GIVEN** `graphicsShaderLevel` = 40, `graphicsMemorySize` = 1024, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is Medium and the stub records a call to `graphicsMemorySize`.

**AC-QT-08 — GPU memory below 512 MB maps to Low tier** *(BLOCKING — Unit)*
**GIVEN** `graphicsShaderLevel` = 40 (inconclusive), `graphicsMemorySize` = 256, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is Low. Boundary cases: 511 → Low; 512 → Medium. Both must pass.

**AC-QT-09 — GPU memory 512–1535 MB maps to Medium tier** *(BLOCKING — Unit)*
**GIVEN** `graphicsShaderLevel` = 40 (inconclusive), `graphicsMemorySize` = 1024, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is Medium. Boundary cases: 512 → Medium; 1535 → Medium; 1536 → High. All three must pass.

**AC-QT-10 — GPU memory 1536 MB or above maps to High tier** *(BLOCKING — Unit)*
**GIVEN** `graphicsShaderLevel` = 40 (inconclusive), `graphicsMemorySize` = 2048, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is High. Boundary: 1536 → High must also pass.

**AC-QT-11 — All signals inconclusive produces Medium fallback** *(BLOCKING — Unit)*
**GIVEN** platform = iOS, `graphicsShaderLevel` = 40 (inconclusive), `graphicsMemorySize` = 0 (UNAVAILABLE), no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is Medium (fallback). This is the all-signals-exhausted path.

**AC-QT-12 — gpu_mem = 0 treated as UNAVAILABLE, not Low** *(BLOCKING — Unit)*
**GIVEN** `graphicsShaderLevel` = 40 (inconclusive) and `graphicsMemorySize` = 0, no persisted tier, **WHEN** `TierDetectionService.Resolve()` is called, **THEN** returned tier is Medium (fallback), not Low. This explicitly tests the EC-QT-01 amendment to QT-10.

**AC-QT-13 — Player override applies immediately and persists both fields** *(BLOCKING — Unit)*
**GIVEN** the system is in ACTIVE_AUTO state with tier = Low, **WHEN** `QualityTierService.SetTier(High)` is called, **THEN** (a) prefs stub writes `qts.tier_value = "High"` and `qts.tier_source = "player-override"`, (b) profile applier mock is called with High tier parameters, (c) `tier_changed(High, player-override)` is emitted. All three assertions must hold independently.

**AC-QT-14 — Reset to Auto-Detected re-runs detection** *(BLOCKING — Unit)*
**GIVEN** the system is in ACTIVE_OVERRIDE state with player-set tier = High, **WHEN** `QualityTierService.ResetTier()` is called, **THEN** (a) the systemInfo stub is called (detection runs), (b) prefs writes `qts.tier_source = "auto-detected"`, (c) the detected tier's profile is applied immediately (not the previous override's High).

**AC-QT-15 — GetActiveTier() never returns null in ACTIVE state** *(BLOCKING — Unit)*
**GIVEN** the system has completed detection or loaded a persisted tier (any detection path: QT-04, QT-05, QT-07, QT-08, QT-10, QT-11, QT-12, QT-13 fallback, player-override), **WHEN** `QualityTierService.GetActiveTier()` is called, **THEN** return value is exactly one of {Low, Medium, High} — never null. Each detection path is a separate sub-case; all must pass.

**AC-QT-16 — GetActiveTier() returns Medium with a warning before ACTIVE state** *(BLOCKING — Unit)*
**GIVEN** a `QualityTierService` instance that has not yet completed initialization, **WHEN** `GetActiveTier()` is called, **THEN** return value is Medium, a warning is written to the Unity log, and no exception is thrown.

**AC-D-01 — Frame rate set during BeforeSceneLoad** *(BLOCKING — Integration / PlayMode)*
**GIVEN** a fresh Unity session with a persisted Low tier, **WHEN** `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` executes, **THEN** `Application.targetFrameRate` = 30 before any scene's Awake runs. Verified by reading the value at the start of the first scene's Awake on a test object with a higher Execution Order than the QTS manager.

**AC-D-02 — Bloom intensity applied in Awake to a runtime Volume Profile copy** *(BLOCKING — Integration / PlayMode)*
**GIVEN** High tier is active and the persistent manager's Awake runs, **WHEN** Awake completes, **THEN** (a) `volume.profile != volume.sharedProfile` (runtime copy confirmed), (b) `bloom.intensity.value` = 1.0. Run three sub-cases (Low=0.0, Medium=0.6, High=1.0); all must pass independently.

**AC-D-03 — VFX density multiplier set before VFX systems initialize** *(BLOCKING — Integration / PlayMode)*
**GIVEN** the persistent manager has lower Script Execution Order than VFX-bearing GameObjects and Low tier is active, **WHEN** a sentinel VFX Graph asset's OnEnable fires, **THEN** `quality_density_multiplier` = 0.25 at OnEnable time. Verified via the sentinel asset recording the property value at OnEnable.

**AC-EC-01a — vSyncCount = 0 set before targetFrameRate in call sequence** *(BLOCKING — Unit)*
**GIVEN** any tier is being applied via the profile applier, **WHEN** frame rate parameters are written, **THEN** `QualitySettings.vSyncCount` is set to 0 before `Application.targetFrameRate` is set in the recorded call sequence. Verified via a `QualitySettings` wrapper stub that records call order.

**AC-EC-01b — Low-tier device runs at approximately 30 fps** *(ADVISORY — Device)*
**GIVEN** a Low-tier Android device (60 Hz display), **WHEN** the app launches and QTS assigns Low tier, **THEN** measured frame rate ≈ 30 fps (± 2 fps over a 10-second idle window). Evidence: frame time overlay screenshot saved to `production/qa/evidence/`.

**AC-EC-02 — All VFX Graph assets expose quality_density_multiplier** *(ADVISORY — Editor)*
**GIVEN** the full project VFX Graph asset set, **WHEN** the editor validation script is run (`Tools > QTS > Validate VFX Properties`), **THEN** zero assets are missing `quality_density_multiplier`. Evidence: script output screenshot saved to `production/qa/evidence/`. Must be re-run after any new VFX asset is added.

## Open Questions

| Question | Owner | Target Resolution | Resolution |
|---|---|---|---|
| Are all four detection thresholds (`shader_level` 35/46, `gpu_mem_mb` 512/1536) correct? These are estimates, not measured values. | QA / Technical Director | Before Beta milestone — device matrix testing on 5–10 representative Android devices | Open |
| What does Unity 6.3 return for `SystemInfo.devicePerformanceClass` on iOS — UNAVAILABLE, an exception, or something else? | Lead Programmer | Before implementation sprint — verify against Unity 6.3 docs | Open |
| Does `QualitySettings.vSyncCount = 0` reliably allow `Application.targetFrameRate = 30` on all target iOS versions in Unity 6.3? | Lead Programmer | Before implementation sprint | Open |
| Does Medium `bloom_intensity = 0.6` read as "intentionally reduced" (not "broken") on a Samsung Galaxy A-series device? | Art Director / QA | Before Beta — requires device playtest | Open |
| The editor validation script (`Tools > QTS > Validate VFX Properties`) needs to be built. Who owns it and when? | Tools Programmer | Before any VFX assets are created for the project | Open |
| When Save & Persistence (Beta) is designed, should `qts.*` PlayerPrefs keys migrate into it, or remain as a permanent PlayerPrefs exception? | Technical Director | During Save & Persistence GDD authoring | Open |
| Should a 120fps path be added for ProMotion flagship devices (iPhone 15 Pro, etc.)? Currently excluded — High tier caps at 60fps. | Game Designer | Post-Beta if player feedback warrants | Deferred |
