# Audio System

> **Status**: Designed
> **Author**: Design session + audio-director + systems-designer agents
> **Last Updated**: 2026-04-18
> **Implements Pillar**: Every Pixel Earns Its Place, The Machine Must Sing

## Overview

The Audio System is BoltSort's sound manager — the infrastructure layer that owns audio clip loading, playback routing, and volume control. It manages four sound categories at MVP scope: per-move bolt SFX (a lift click on selection and a settle click on placement), the stack completion chime (a satisfying tone when a color stack fills completely), the ambient machine hum loop (the background audio layer that makes the board feel like a living machine), and UI sounds (light tap feedback for button interactions). All sounds route through a Unity Audio Mixer with three groups (SFX, Ambient, UI), each with independently exposed volume parameters that the Settings UI controls. The Audio System is a `DontDestroyOnLoad` singleton that initializes before any scene is playable and persists across level transitions — no system plays `AudioSource` clips directly; all audio requests go through the Audio System's event interface. Its quality is measured by the tactile feedback of the bolt click: if it sounds right, the sort loop feels responsive; if it sounds wrong, the experience breaks regardless of animation quality.

## Player Fantasy

Every tap you make gets an answer. The bolt lifts with a precise click and lands with a settled thud, and in that two-beat exchange the machine is telling you "received" and "done." The board doesn't feel like a puzzle grid — it feels like a control panel that is listening. When a stack completes and the chime resolves, you haven't just won a small victory; the machine has come back online, one subsystem at a time. The audio arc of each level mirrors the emotional arc: background noise becoming signal, chaos resolving into a chord.

The Audio System is directly felt by the player on every move. Its failure mode is immediate: a missing click is a missed confirmation, a late click is a broken loop, no ambient hum is a dead board. Unlike infrastructure systems that fail silently, this system's quality is perceptible within the first three seconds of play.

## Detailed Design

### Core Rules

**Group A — Interface Contract**

- **AUD-A-01**: No system plays `AudioSource` clips directly. All audio requests go through the Audio System's named-method interface. Any class that bypasses this interface is a bug.
- **AUD-A-02**: `PlayBoltLift()` — plays the bolt lift clip routed to the SFX mixer group. Random pitch offset ±10% (0.9–1.1) applied per call.
- **AUD-A-03**: `PlayBoltSettle(bool isValid)` — plays the bolt settle clip routed to the SFX mixer group. `isValid = true`: full settle clip, random pitch offset ±6% (0.94–1.06). `isValid = false`: muted variant of settle clip with no pitch variation. No buzzer or negative feedback sound is played.
- **AUD-A-04**: `PlayStackComplete(int stackIndex, bool isFinal)` — plays a completion chime routed to the SFX mixer group. `stackIndex` selects from 4 indexed clips (root / mid / penultimate / final). `isFinal = true`: final clip adds 0.5–0.8s sustain tail and a two-voice harmony layer. No random pitch variation on any chime clip.
- **AUD-A-05**: `PlayUIClick()` — plays the UI tap clip routed to the UI mixer group. No pitch variation.
- **AUD-A-06**: `StartAmbientLoop()` — starts the ambient machine hum loop routed to the Ambient mixer group. No-op if ambient is already playing.
- **AUD-A-07**: `StopAmbientLoop()` — stops the ambient loop with a short fade (≤200ms). No-op if ambient is not playing.
- **AUD-A-08**: Any call made before the Audio System has completed `Awake` initialization is silently discarded. No error is thrown; the sound is simply not played.
- **AUD-A-09**: If a clip reference assigned to any method is null at call time, the call is silently discarded and an error is logged to the Unity console. No exception is thrown.

**Group B — Volume Control**

- **AUD-B-01**: Volume is controlled via three Audio Mixer group exposed parameters: `SFXVolume`, `AmbientVolume`, `UIVolume`. These parameters accept linear values (0.0–1.0) mapped to decibel range internally by the mixer.
- **AUD-B-02**: The Settings UI writes directly to Audio Mixer group exposed parameters via `AudioMixer.SetFloat()`. The Audio System does not mediate volume writes after initialization — Settings UI is the sole owner of runtime volume state.
- **AUD-B-03**: No mute logic exists inside the Audio System. Silence is achieved by the Audio Mixer driving the group to -80dB at volume=0. The Audio System has no concept of "muted."
- **AUD-B-04**: On `Awake`, the Audio System reads `audio.sfx_volume`, `audio.ambient_volume`, and `audio.ui_volume` from `PlayerPrefs` and applies them to their respective Audio Mixer group parameters. If a key is absent, the default value is 1.0 (full volume).
- **AUD-B-05**: Volume persistence is owned by the Settings UI (write) and the Audio System's `Awake` (read). No other system reads or writes these keys.

**Group C — Timing and Call Ownership**

- **AUD-C-01**: The Sort Mechanic calls `PlayBoltLift()` immediately when `BOLT_SELECTED` state is entered — on the same frame the player's tap is confirmed.
- **AUD-C-02**: The Animation System calls `PlayBoltSettle(isValid)` at the bolt's visual arrive keyframe — the moment the bolt reaches its destination position in the animation curve. The caller (Animation System) must validate the sequence ID before calling to confirm the animation is not stale. The Audio System does not validate sequence IDs.
- **AUD-C-03**: The Sort Mechanic calls `PlayStackComplete(stackIndex, isFinal)` immediately when an individual stack fill is detected — once per stack completion event, not once per full puzzle solve. `stackIndex` is the 0-based completion order index (first stack = 0, second = 1, etc.). `isFinal` is true only when this completion event also satisfies the win condition.
- **AUD-C-04**: The ambient machine hum responds perceptually to progress: volume scales from -3dB (0 stacks complete) to 0dB (all stacks complete). The total dynamic range is 3dB. This is a perceptual arc, not a gameplay mechanic — the Audio System applies it internally as stacks are completed.
- **AUD-C-05**: On any stack completion chime, the ambient loop ducks -4 to -6dB for the duration of the chime using an Audio Mixer sidechain. Attack: 50ms. Release: 300ms. This is implemented in the Audio Mixer, not in code.
- **AUD-C-06**: Simultaneous `PlayBoltSettle()` and `PlayStackComplete()` calls (settle sound coinciding with a stack completion on the same move) are both played. The two sounds overlap in the mix. Resolution is owned by audio asset design (clip volume and EQ), not by the Audio System.
- **AUD-C-07**: `PlayUIClick()` is called on button press, before the button's action executes. It does not fire on disabled buttons or on actions that are prevented by validation.
- **AUD-C-08**: The Audio System self-subscribes to the `level_loaded` event from the Game State Manager during `Awake`. On `level_loaded`, it calls `StartAmbientLoop()`.
- **AUD-C-09**: The Audio System self-subscribes to the `level_unloaded` event from the Game State Manager during `Awake`. On `level_unloaded`, it calls `StopAmbientLoop()`.

**Group D — Pause Behavior**

- **AUD-D-01**: On game pause, the Audio System sets `AudioListener.pause = true`. All audio output is suspended — ambient loop position is preserved.
- **AUD-D-02**: On game resume, the Audio System sets `AudioListener.pause = false`. The ambient loop resumes from the preserved position; no clip restarts from the beginning.
- **AUD-D-03**: Any named-method call received while `AudioListener.pause = true` is silently discarded. No sounds are queued for playback on resume.
- **AUD-D-04**: App backgrounding (iOS home button, Android back gesture, focus loss) is treated identically to game pause: `AudioListener.pause = true` on `OnApplicationPause(true)`, `AudioListener.pause = false` on `OnApplicationPause(false)`.
- **AUD-D-05**: The Audio System does not own pause state. It reacts to events from the Game State Manager (game pause) and Unity's `OnApplicationPause` callback (OS-level background). It does not call pause itself.

**Group E — Lifecycle**

- **AUD-E-01**: On `Awake`, the Audio System loads all clip references from serialized fields (assigned in the Inspector), reads persisted volumes from `PlayerPrefs`, applies them to the Audio Mixer, and subscribes to `level_loaded` and `level_unloaded` events. It is ready to receive calls immediately after `Awake` completes.
- **AUD-E-02**: The Audio System uses the `DontDestroyOnLoad` singleton pattern. If a duplicate instance is detected on `Awake`, the duplicate destroys itself and the original persists.
- **AUD-E-03**: The Audio System persists across all scene transitions via `DontDestroyOnLoad`. It does not unload or reinitialize on scene changes.
- **AUD-E-04**: No teardown or cleanup is required on application quit. Unity's audio system handles AudioSource cleanup. The Audio System does not implement `OnApplicationQuit`.

### States and Transitions

| State | Description | Enters From | Exits To |
|-------|-------------|-------------|----------|
| `UNINITIALIZED` | Before `Awake` completes. All calls discarded. | — (initial) | `READY` |
| `READY` | Initialized, no ambient playing. All SFX methods active. | `UNINITIALIZED` (on Awake complete), `PLAYING_AMBIENT` (on `level_unloaded`) | `PLAYING_AMBIENT` (on `level_loaded`), `PAUSED` (on pause/background) |
| `PLAYING_AMBIENT` | Ambient loop running. All methods active. | `READY` (on `level_loaded`) | `READY` (on `level_unloaded`), `PAUSED` (on pause/background) |
| `PAUSED` | `AudioListener.pause = true`. All calls discarded. | `READY` or `PLAYING_AMBIENT` (on pause/background) | Previous state (on resume) |

On resume from `PAUSED`: return to whichever state was active before pause (either `READY` or `PLAYING_AMBIENT`). If ambient was playing before pause, it resumes from its preserved position.

### Interactions with Other Systems

| System | Direction | Interface | Notes |
|--------|-----------|-----------|-------|
| Sort Mechanic | Sort Mechanic → Audio System | `PlayBoltLift()`, `PlayStackComplete(stackIndex, isFinal)` | Sort Mechanic calls on BOLT_SELECTED and on each stack completion event |
| Animation System | Animation System → Audio System | `PlayBoltSettle(isValid)` | Called at visual arrive keyframe; Animation System validates sequence ID before calling |
| Game State Manager | GSM → Audio System (events) | `level_loaded`, `level_unloaded` events | Audio System self-subscribes on Awake; no direct method calls from GSM |
| Settings UI | Settings UI → Audio Mixer (direct) | `AudioMixer.SetFloat("SFXVolume")`, `AudioMixer.SetFloat("AmbientVolume")`, `AudioMixer.SetFloat("UIVolume")` | Settings UI writes directly to mixer — Audio System is not in this path after init |
| Quality Tier System | No interaction | — | Audio System has no quality tier dependency; all clips play regardless of tier |

## Formulas

### F-01: Bolt Lift Pitch Variation

```
pitch_bolt_lift = 1.0 + U(−0.1, 0.1)
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `U(a, b)` | float | uniform draw | Random value drawn uniformly in [a, b] per call |
| `pitch_bolt_lift` | float | [0.90, 1.10] | Applied to `AudioSource.pitch` on PlayBoltLift() |

*Example: draw = 0.07 → pitch = 1.07.*

---

### F-02: Bolt Settle Pitch Variation

```
pitch_bolt_settle(isValid) =
  1.0 + U(−0.06, 0.06)   if isValid = true
  1.0                     if isValid = false
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `isValid` | bool | — | Whether the bolt placement was accepted by the Sort Mechanic |
| `pitch_bolt_settle` | float | [0.94, 1.06] (valid) or 1.0 (invalid) | Applied to `AudioSource.pitch` on PlayBoltSettle() |

The muted invalid variant plays at fixed pitch 1.0; its volume is reduced in the clip asset, not by formula.

---

### F-03: Ambient Volume Response

```
ambient_db(stacks_complete, total_stacks) = lerp(−3.0, 0.0, stacks_complete / total_stacks)
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `stacks_complete` | int | [0, total_stacks] | Number of color stacks filled this level |
| `total_stacks` | int | [2, ∞) | Total number of color stacks in the level (equals color_count from Level Data) |
| `ambient_db` | float | [−3.0, 0.0] dB | Applied to Ambient mixer group volume parameter via `AudioMixer.SetFloat()` |

*Example: 3 of 6 stacks complete → lerp(−3.0, 0.0, 0.5) = −1.5 dB.*

Applied once per stack completion event, not continuously. At level start: −3.0 dB. At final completion: 0.0 dB.

---

### F-04: Chime Clip Selection

```
chime_clip_index(stackIndex, isFinal) =
  3                           if isFinal = true
  clamp(stackIndex, 0, 2)     if isFinal = false
```

| Variable | Type | Range | Description |
|----------|------|-------|-------------|
| `stackIndex` | int | [0, ∞) | 0-based completion order index passed by Sort Mechanic |
| `isFinal` | bool | — | True only when this completion event also satisfies the win condition |
| `chime_clip_index` | int | [0, 3] | Index into the 4-clip chime array: 0=root, 1=mid, 2=penultimate, 3=final |

Clip 3 (final) is reserved for `isFinal = true`. Non-final stacks cycle through clips 0–2, clamped at 2 for stackIndex ≥ 2. Handles any puzzle with ≥ 2 stacks.

## Edge Cases

**EC-AU-01 — F-03 division by zero (`total_stacks = 0`)**
`total_stacks` equals the level's `color_count` from Level Data. A well-formed level always has `color_count ≥ 2`. If `total_stacks ≤ 0` is ever received (malformed level data that passed the GSM's initialization check), skip F-03 and hold ambient at −3.0 dB for the duration of the level. Log a critical error identifying the level ID.

**EC-AU-02 — Audio Mixer parameter name mismatch on startup**
`AudioMixer.SetFloat()` returns `false` silently if the exposed parameter name is not found (e.g., a rename in the Mixer asset not reflected in code). On `Awake`, call `SetFloat` for all three parameters (`SFXVolume`, `AmbientVolume`, `UIVolume`) and check return values. If any returns `false`, log a critical error identifying the missing parameter name. Do not throw an exception; the game continues with Audio Mixer at its authored defaults.

**EC-AU-03 — Stale animation calls `PlayBoltSettle()` without sequence ID validation**
The Audio System has no sequence ID awareness (AUD-C-02). If the Animation System fails to validate its sequence ID before calling `PlayBoltSettle()`, the stale audio plays. This is an Animation System bug, not an Audio System bug. The Audio System does not detect, prevent, or log this condition.

**EC-AU-04 — `PlayStackComplete()` with `stackIndex ≥ 2` and `isFinal = false`**
Formula F-04 clamps to index 2 (penultimate clip). Puzzles with more than 3 stacks will play the penultimate clip for all intermediate completions after the first two (stackIndex ≥ 2). This is intended — the penultimate clip acts as the "progress continues" indicator for any number of intermediate stacks.

**EC-AU-05 — Ambient loop clip reference is null**
`StartAmbientLoop()` falls under AUD-A-09 (null clip → silently discard + error log). The game continues without ambient audio. Not a crash condition; not a recoverable runtime error — fixing requires assigning the clip in the Inspector.

**EC-AU-06 — No `AudioListener` in scene**
`AudioListener.pause` and all audio output require a scene `AudioListener`. If absent (test scene misconfiguration), all audio is silent and `AudioListener.pause` assignments are no-ops. The Audio System does not detect or recover from this; it is a scene authoring error surfaced during QA.

**EC-AU-07 — Rapid double-tap issues two `PlayBoltLift()` calls**
Both play via `PlayOneShot()` and overlap. No deduplication is performed. The brief stutter communicates "two selection events occurred," which is accurate. If the Sort Mechanic prevents the second selection, it simply will not call `PlayBoltLift()` a second time — deduplication is owned by the caller, not the Audio System.

**EC-AU-08 — App backgrounds mid-chime**
`OnApplicationPause(true)` fires → `AudioListener.pause = true` → chime is cut off. On resume, the chime is not replayed (AUD-D-03). The interrupted chime represents a genuine interrupted session; no recovery is needed.

**EC-AU-09 — First launch with no audio `PlayerPrefs` keys**
`PlayerPrefs.GetFloat("audio.sfx_volume", 1.0f)` — and equivalent for `ambient` and `ui` — returns 1.0 when the key is absent. All channels start at full volume on first launch. No special initialization path required.

**EC-AU-10 — Scene transition while ambient loop is playing**
`level_unloaded` fires → `StopAmbientLoop()` begins a ≤200ms fade. Because the Audio System and its `AudioSource` are `DontDestroyOnLoad`, they persist across the scene load. The fade completes cleanly independent of the scene transition. The next `level_loaded` event fires `StartAmbientLoop()` fresh.

## Dependencies

### Upstream — systems the Audio System depends on

| System | Dependency Type | What is needed |
|--------|-----------------|----------------|
| Game State Manager | Event subscription | Audio System subscribes to `level_loaded` (→ StartAmbientLoop) and `level_unloaded` (→ StopAmbientLoop) during Awake |
| Sort Mechanic | Design contract (caller) | Sort Mechanic must call `PlayBoltLift()` on BOLT_SELECTED and `PlayStackComplete(stackIndex, isFinal)` on each stack completion. No code import dependency — contract enforced by design |
| Animation System | Design contract (caller) | Animation System must call `PlayBoltSettle(isValid)` at the visual arrive keyframe, after sequence ID validation. No code import dependency |
| Level Data System | Indirect — via Sort Mechanic | `color_count` from Level Data determines `total_stacks` used in F-03. Audio System does not read Level Data directly |
| Unity Audio Mixer | Unity infrastructure | Audio Mixer asset with three exposed parameters (`SFXVolume`, `AmbientVolume`, `UIVolume`) must exist and be assigned in the Audio System's Inspector fields |

### Downstream — systems that depend on Audio System

| System | What it uses |
|--------|-------------|
| Animation System | Calls `PlayBoltSettle(isValid)` — design dependency on the named method existing |
| Settings UI | Calls `AudioMixer.SetFloat("SFXVolume")`, `AudioMixer.SetFloat("AmbientVolume")`, `AudioMixer.SetFloat("UIVolume")` — depends on exposed parameter names being stable |

### Non-dependencies

Quality Tier System: the Audio System has no quality tier dependency. All clips play at the same quality regardless of detected tier. Audio quality differentiation (if any) is an audio asset concern, not an Audio System code concern.

All economy, progression, and cosmetic systems (Coin Economy, Level Progression, Hint System, Skin System, IAP System) have no interaction with the Audio System.

## Tuning Knobs

| Knob | Current Value | Safe Range | What it affects | Where configured |
|------|--------------|------------|-----------------|-----------------|
| `bolt_lift_pitch_range` | ±10% (0.9–1.1) | ±5% – ±15% | How "alive" the lift sound feels. Too narrow: mechanical, identical. Too wide: chaotic, unpleasant. | Code constant in Audio System |
| `bolt_settle_pitch_range` | ±6% (0.94–1.06) | ±3% – ±10% | Texture of valid placements. Tighter than lift — settle should feel more precise. Invalid variant is always fixed pitch 1.0. | Code constant in Audio System |
| `ambient_volume_floor_db` | −3.0 dB | −6.0 – −1.0 dB | How quiet ambient is at puzzle start. Too low: board feels dead. Too high: no arc, no sense of progress. | Code constant (F-03) |
| `ambient_duck_depth_db` | −4 to −6 dB | −2 – −8 dB | How much ambient steps back during chimes. Too shallow: chime fights ambient. Too deep: ambient disappears unnaturally. | Audio Mixer sidechain parameter |
| `ambient_duck_attack_ms` | 50 ms | 20–100 ms | How quickly ambient ducks on chime hit. Shorter = snappier. Longer = softer crossfade. | Audio Mixer sidechain parameter |
| `ambient_duck_release_ms` | 300 ms | 150–600 ms | How quickly ambient recovers after chime. Too short: ambient snaps back. Too long: ambient is suppressed between moves. | Audio Mixer sidechain parameter |
| `ambient_stop_fade_ms` | ≤200 ms | 50–500 ms | Fade duration when `StopAmbientLoop()` is called (level unload or manual stop). | Code constant in Audio System |
| `final_chime_sustain_s` | 0.5–0.8 s | 0.3–1.2 s | Sustain tail on the final completion chime. Sets how long the "win moment" hangs in the air. | Audio clip asset design |

*Knobs marked "Audio Mixer sidechain parameter" are adjusted in the Unity Audio Mixer graph without code changes. Knobs marked "Code constant" require a recompile. Knobs marked "Audio clip asset design" are baked into the audio file.*

## Visual/Audio Requirements

The Audio System requires the following audio clip assets. All clips must be assigned in the Audio System's Inspector fields before any scene is playable.

| Clip Slot | Format | Loop | Notes |
|-----------|--------|------|-------|
| Bolt lift SFX | ADPCM | No | Short percussive click. ≤100ms. Designed for ±10% pitch variation without artifacts. |
| Bolt settle (valid) | ADPCM | No | Heavier settle thud. ≤150ms. Designed for ±6% pitch variation. |
| Bolt settle (invalid) | ADPCM | No | Muted variant of settle — quieter, no pitch variation applied. Signals "move absorbed, not confirmed." |
| Chime clip [0] — root | ADPCM | No | Lowest-pitched stack completion tone. Establishes tonal foundation. |
| Chime clip [1] — mid | ADPCM | No | Mid-register completion tone. Builds on root. |
| Chime clip [2] — penultimate | ADPCM | No | Higher-register "almost there" tone. |
| Chime clip [3] — final | ADPCM | No | Highest-register resolution. Includes 0.5–0.8s sustain tail and two-voice harmony layer baked in. |
| Ambient machine hum | Vorbis (streaming) | Yes | Low-frequency mechanical hum. Seamless loop. Neutral in mix at 0dB; designed to sit −3dB below SFX. |
| UI tap SFX | ADPCM | No | Light, brief button tap. Distinct from bolt sounds. ≤80ms. |

*ADPCM: low-latency, low-memory format for short SFX. Vorbis streaming: appropriate for looping ambient to avoid memory overhead.*

## UI Requirements

The Audio System has no direct UI. Volume controls are owned by the Settings UI (Launch milestone) which writes to the Audio Mixer group exposed parameters directly.

The following parameter names are part of the Audio System's stable public interface and must not change after the Settings UI is built:

| Parameter Name | Mixer Group | Controls |
|---------------|-------------|---------|
| `SFXVolume` | SFX | Bolt lift, bolt settle, stack chimes |
| `AmbientVolume` | Ambient | Machine hum loop |
| `UIVolume` | UI | Button taps |

The Settings UI's design doc must reference these names. Any rename requires updating both the Audio System, the Settings UI, and all `PlayerPrefs` keys.

## Acceptance Criteria

### Initialization & Singleton

**AC-AU-01** `[BLOCKING-UNIT]` A second Audio System instance spawned at scene load destroys itself; the original persists. Verified by loading a scene that contains a second Audio System GameObject and confirming only one remains.

**AC-AU-02** `[BLOCKING-UNIT]` Script Execution Order places Audio System before Sort Mechanic, Animation System, and Game State Manager. Verified by reading Unity's Script Execution Order settings in the test environment.

**AC-AU-03** `[BLOCKING-UNIT]` On `Awake`, `AudioMixer.SetFloat()` is called for `SFXVolume`, `AmbientVolume`, and `UIVolume` using values read from `PlayerPrefs` (`audio.sfx_volume`, `audio.ambient_volume`, `audio.ui_volume`). If any key is absent, value defaults to 1.0. Verified with a mock `PlayerPrefs` containing: (a) all three keys set to 0.5, (b) no keys (assert all default to 1.0).

**AC-AU-04** `[BLOCKING-UNIT]` If `AudioMixer.SetFloat()` returns `false` for any parameter on `Awake`, a critical error is logged (Unity `Debug.LogError`). Verified by providing a Mixer with a missing or renamed parameter.

### Named Method Interface

**AC-AU-05** `[BLOCKING-PLAYMODE]` `PlayBoltLift()` triggers audio playback on the SFX `AudioSource` within the same frame. Pitch value is within [0.90, 1.10]. Verified over 20 calls — all pitches must fall in range; no two consecutive calls may have identical pitch.

**AC-AU-06** `[BLOCKING-PLAYMODE]` `PlayBoltSettle(true)` triggers audio playback at pitch within [0.94, 1.06]. `PlayBoltSettle(false)` triggers audio playback at fixed pitch 1.0. No additional negative-feedback sound is played on `isValid = false`. Verified by comparing two recorded playback events.

**AC-AU-07** `[BLOCKING-PLAYMODE]` `PlayStackComplete(0, false)` plays clip index 0. `PlayStackComplete(1, false)` plays clip index 1. `PlayStackComplete(2, false)` plays clip index 2. `PlayStackComplete(5, false)` plays clip index 2 (clamped). `PlayStackComplete(*, true)` plays clip index 3 regardless of stackIndex. Verified by reading the `AudioSource.clip` reference after each call.

**AC-AU-08** `[BLOCKING-PLAYMODE]` `PlayUIClick()` triggers audio playback routed to the UI `AudioSource` (UI mixer group). Verified by reading the `AudioMixerGroup` assigned to the UI `AudioSource`.

**AC-AU-09** `[BLOCKING-PLAYMODE]` `StartAmbientLoop()` begins ambient loop playback. A second `StartAmbientLoop()` call while ambient is playing produces no change (no restart, no second instance). `StopAmbientLoop()` stops ambient within 200ms. A second `StopAmbientLoop()` while not playing is a no-op. All verified by reading `AudioSource.isPlaying`.

**AC-AU-10** `[BLOCKING-UNIT]` Any named method call made before `Awake` completes returns without action and without throwing an exception. Verified by calling all 6 methods from a `BeforeSceneLoad` hook before the Audio System initializes.

**AC-AU-11** `[BLOCKING-UNIT]` Calling any named method when the assigned clip field is null produces no exception and emits a `Debug.LogError`. Verified by setting clip fields to null in the test scene and calling each method.

### Mixer Routing

**AC-AU-12** `[BLOCKING-UNIT]` Bolt lift, bolt settle, and stack chime `AudioSource` components are assigned to the SFX mixer group. Ambient `AudioSource` is assigned to the Ambient mixer group. UI `AudioSource` is assigned to the UI mixer group. Verified by reading `AudioSource.outputAudioMixerGroup` for each source.

### Ambient Volume Response

**AC-AU-13** `[BLOCKING-UNIT]` F-03 is a pure function (no side effects, no Unity dependencies). Unit test verifies: `ambient_db(0, 6) = −3.0`, `ambient_db(3, 6) = −1.5`, `ambient_db(6, 6) = 0.0`.

**AC-AU-14** `[BLOCKING-PLAYMODE]` After each `PlayStackComplete()` call, the Ambient mixer group parameter matches the F-03 output for the updated `stacks_complete` count. Verified with a 4-stack test level (assert Ambient parameter at stacks 0, 1, 2, 3, 4).

**AC-AU-15** `[BLOCKING-UNIT]` If `total_stacks ≤ 0` is passed to F-03, ambient is held at −3.0 dB and a `Debug.LogError` is emitted. Verified by constructing a malformed level state and triggering a stack completion.

### Pause and Background

**AC-AU-16** `[BLOCKING-PLAYMODE]` On game pause, `AudioListener.pause` is `true`. On resume, `AudioListener.pause` is `false`. Verified by reading `AudioListener.pause` after each state transition.

**AC-AU-17** `[BLOCKING-PLAYMODE]` On `OnApplicationPause(true)`, `AudioListener.pause` is `true`. On `OnApplicationPause(false)`, `AudioListener.pause` is `false`. Verified by simulating the Unity callback.

**AC-AU-18** `[BLOCKING-PLAYMODE]` Ambient loop playback position is preserved across pause/resume — the loop does not restart from the beginning. Verified by reading `AudioSource.time` before pause and after resume (must match within ±0.05s).

**AC-AU-19** `[BLOCKING-UNIT]` Named method calls while `AudioListener.pause = true` return without playing audio. Verified by calling all 6 methods while paused and confirming no `AudioSource.Play()` or `PlayOneShot()` was invoked.

### Lifecycle

**AC-AU-20** `[BLOCKING-PLAYMODE]` `level_loaded` event from Game State Manager triggers `StartAmbientLoop()`. `level_unloaded` event triggers `StopAmbientLoop()`. Verified by firing the events manually in a PlayMode test and reading `AudioSource.isPlaying`.

**AC-AU-21** `[BLOCKING-PLAYMODE]` Audio System and all its `AudioSource` components survive a scene load (via `DontDestroyOnLoad`). Ambient loop continues uninterrupted across the transition. Verified by loading a new scene while ambient is playing and confirming `AudioSource.isPlaying` is still `true`.

### Advisory

**AC-AU-22** `[ADVISORY-LISTEN]` Bolt lift click followed by bolt settle thud within the same move produces a distinct two-beat "received / done" confirmation. No perceptible latency between tap and first click. Verified by audio director listen test.

**AC-AU-23** `[ADVISORY-LISTEN]` The ambient machine hum perceptibly grows from quiet to present as stacks complete within a 6-stack level. The 3dB arc is noticeable in a quiet listening environment. Verified by audio director listen test.

**AC-AU-24** `[ADVISORY-LISTEN]` The ambient duck during stack completion chimes is clean — the chime is audible and clear, and the ambient returns without a noticeable "snap." Verified by audio director listen test.

---

*Total: 24 ACs — 21 BLOCKING + 3 ADVISORY.*

*BLOCKING-UNIT tests run in Unity EditMode (no scene required). BLOCKING-PLAYMODE tests run in Unity PlayMode (require a test scene with a configured Audio System). ADVISORY tests require a human listen test with the final audio assets.*

## Open Questions

**OQ-AU-01 — `level_loaded` event payload**
The Audio System subscribes to `level_loaded` to start the ambient loop (AUD-C-08) and needs `total_stacks` (= `color_count`) to compute F-03 on each stack completion. What data does the `level_loaded` event carry? If the event payload does not include `color_count`, the Audio System needs a separate call to the Game State Manager to retrieve it. *Resolve in the Game State Manager GDD or a joint interface decision before implementation.*

**OQ-AU-02 — Audio Mixer sidechain topology**
The ambient duck on chime (AUD-C-05) requires a sidechain Send/Receive in the Unity Audio Mixer graph. The specific topology (which Send target, which Receive, duck curve shape) is not specified here. A mixer diagram or spec must be authored before the Audio System's Mixer asset is built. *Assign to Audio Director + Unity Specialist before implementation sprint.*

**OQ-AU-03 — Settle audio when Animation System is absent or degraded**
AUD-C-02 requires the Animation System to call `PlayBoltSettle()` at the visual arrive keyframe. If the Animation System is disabled or bypassed (e.g., a Low-tier path that skips bolt animation), the settle sound is never called. Is silent settle acceptable on Low tier, or should a fallback caller (Sort Mechanic on GSM commit) exist? *Resolve in Animation System GDD.*

**OQ-AU-04 — Chime clip coverage for 2-stack puzzles**
With exactly 2 stacks, `PlayStackComplete(0, false)` plays clip 0, `PlayStackComplete(1, true)` plays clip 3. Clips 1 and 2 are never used. The audio arc jumps directly from root to final — no mid or penultimate progression. Is this acceptable, or should 2-stack puzzles use a 2-clip path (root → final only)? *Resolve when level difficulty tiers are defined; consult Audio Director on minimum stack count for full arc.*
