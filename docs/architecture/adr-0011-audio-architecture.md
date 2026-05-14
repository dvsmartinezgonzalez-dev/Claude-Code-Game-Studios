# ADR-0011: Audio Architecture

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Audio |
| **Knowledge Risk** | LOW — AudioMixer, AudioSource, PlayerPrefs are stable APIs unchanged in Unity 6.x |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | Confirm AudioMixer routing on iOS (Audio Session category must be set to `AVAudioSessionCategoryAmbient` for correct behavior when other audio is playing — e.g., music app); verify ambient hum loop on physical Android with auto-suspend behavior |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (AudioSystem is a DDOL singleton at SEO −80; reads PlayerPrefs before any sound plays) |
| **Enables** | None — terminal ADR |
| **Blocks** | Audio System implementation sprint |
| **Ordering Note** | ADR-0001 must be Accepted first (AudioSystem boot slot and DDOL pattern). |

## Context

### Problem Statement
BoltSort needs three distinct audio bus groups (SFX, Ambient, UI) with independent runtime volume control and player-preference persistence. Without an explicit audio architecture, volume controls will be implemented inconsistently across systems, and the AudioMixer routing will be undefined when Settings UI is added in a future milestone.

### Constraints
- `PlayerPrefs.Save()` must be called after every audio preference write — Android OOM kill bypasses `OnApplicationQuit` (established in ADR-0003 / save-persistence GDD)
- AudioMixer exposed parameter names (`SFXVolume`, `AmbientVolume`, `UIVolume`) are registered in `design/registry/entities.yaml` — they must not be renamed post-launch without a coordinated update
- AudioSystem must be ready at SEO −80 so any system that plays a sound after Awake (e.g., AnimationSystem) has a valid AudioMixer reference

### Requirements
- Three bus groups: SFX, Ambient, UI — independent volume control
- Pool of 8 AudioSources for concurrent SFX (bolt clicks, chimes)
- `PlayBoltSettle(bool isValid)` — AnimationSystem is the sole caller
- Machine ambient hum loop — starts on Awake, plays continuously
- Volume conversion: linear 0–1 → dB (AudioMixer parameter)
- PlayerPrefs keys read on Awake; `PlayerPrefs.Save()` after every write

## Decision

### AudioMixer Bus Architecture

```
AudioMixer
  ├── Master Group
  │    ├── SFX Group (exposed param: "SFXVolume")
  │    │    ├── bolt clicks, chimes
  │    │    └── [source pool: 8 AudioSources, outputAudioMixerGroup = SFX]
  │    ├── Ambient Group (exposed param: "AmbientVolume")
  │    │    └── machine hum loop [1 dedicated AudioSource, loop = true]
  │    └── UI Group (exposed param: "UIVolume")
  │         └── button tap sounds
  └── (Snapshot: Default — applied on Awake)
```

### Volume Control API

```csharp
// AudioSystem.cs
private const float SilenceDb = -80f;

// Linear 0–1 → dB (same formula as entities.yaml audio_mixer_sfx_param notes)
private float LinearToDb(float linear)
    => linear > 0.001f ? Mathf.Log10(linear) * 20f : SilenceDb;

public void SetSFXVolume(float normalizedVolume)
{
    _audioMixer.SetFloat("SFXVolume", LinearToDb(normalizedVolume));
    PlayerPrefs.SetFloat("audio.sfx_volume", normalizedVolume);
    PlayerPrefs.Save();  // required — Android OOM kill bypass (ADR-0003)
}
// Identical pattern for SetAmbientVolume / SetUIVolume
```

### AudioSource Pool (8 Sources for SFX)

```csharp
// Pre-created on AudioSystem GameObject; all routed to SFX mixer group
private AudioSource[] _sfxPool;
private int _poolIndex;

private void Awake()
{
    _sfxPool = new AudioSource[8];
    for (int i = 0; i < 8; i++)
    {
        _sfxPool[i] = gameObject.AddComponent<AudioSource>();
        _sfxPool[i].outputAudioMixerGroup = _sfxGroup;
        _sfxPool[i].playOnAwake = false;
    }
    _ambientSource = gameObject.AddComponent<AudioSource>();
    _ambientSource.outputAudioMixerGroup = _ambientGroup;
    _ambientSource.loop = true;
    _ambientSource.clip = _machineHumClip;
    _ambientSource.Play();
}

private AudioSource NextPoolSource()
{
    var source = _sfxPool[_poolIndex];
    _poolIndex = (_poolIndex + 1) % _sfxPool.Length;
    return source;
}
```

### PlayBoltSettle API

```csharp
// IAudioSystem interface
public void PlayBoltSettle(bool isValid)
{
    var clip = isValid ? _boltSettleValidClip : _boltSettleInvalidClip;
    NextPoolSource().PlayOneShot(clip);  // no allocation; schedules playback on source
}
// Called exclusively by AnimationSystem (architecture principle — sole caller)
```

### PlayerPrefs Read on Awake

```csharp
private void Awake()
{
    Instance = this;
    DontDestroyOnLoad(gameObject);

    float sfxVol     = PlayerPrefs.GetFloat("audio.sfx_volume", 1f);
    float ambientVol = PlayerPrefs.GetFloat("audio.ambient_volume", 1f);
    float uiVol      = PlayerPrefs.GetFloat("audio.ui_volume", 1f);

    _audioMixer.SetFloat("SFXVolume",     LinearToDb(sfxVol));
    _audioMixer.SetFloat("AmbientVolume", LinearToDb(ambientVol));
    _audioMixer.SetFloat("UIVolume",      LinearToDb(uiVol));

    // ... pool init + ambient hum start ...
}
```

### Architecture Diagram

```
AudioSystem.Awake() [SEO -80]
    ├── Read PlayerPrefs → apply to AudioMixer (SetFloat × 3)
    ├── Create 8 pooled SFX AudioSources → outputAudioMixerGroup = SFX
    ├── Create 1 Ambient AudioSource → loop = true → Play()
    └── DontDestroyOnLoad(gameObject)

AnimationSystem.OnMoveCommitted → bolt lift/travel/settle →
    PlayBoltSettle(true) → NextPoolSource().PlayOneShot(_boltSettleValidClip)

AnimationSystem.OnMoveRejected →
    PlayBoltSettle(false) → NextPoolSource().PlayOneShot(_boltSettleInvalidClip)

Settings UI (future milestone):
    → AudioSystem.SetSFXVolume(0.8f) → AudioMixer.SetFloat + PlayerPrefs.Save()
```

### Key Interfaces

```csharp
public interface IAudioSystem
{
    void PlayBoltSettle(bool isValid);
    void SetSFXVolume(float normalizedVolume);      // 0–1
    void SetAmbientVolume(float normalizedVolume);
    void SetUIVolume(float normalizedVolume);
}
```

## Alternatives Considered

### Alternative A: Direct `AudioSource.volume` per source
- **Description**: Set `audioSource.volume` directly instead of routing through AudioMixer groups
- **Pros**: No AudioMixer setup required; simpler
- **Cons**: Cannot apply volume changes to all sources of a type simultaneously; no AudioMixer snapshot support for future (e.g., pause menu ducking); no master volume control; no future Settings UI hook without code changes
- **Rejection Reason**: No path to Settings UI volume sliders without significant refactor; AudioMixer is the correct architecture for Unity audio.

### Alternative B: Unity Audio System (2D Mode, no mixer)
- **Description**: All sounds via `AudioSource.PlayOneShot()` on a single default source, no mixer routing
- **Pros**: Minimal setup
- **Cons**: Cannot control bus volumes independently; no Settings UI hook; no ducking or crossfade support
- **Rejection Reason**: Does not satisfy TR-AUDIO-001 (three bus groups with independent control).

## Consequences

### Positive
- AudioMixer parameter names match entities.yaml registry — Settings UI can wire volume sliders directly to `AudioSystem.SetXxxVolume()` without changes to audio architecture
- Pool of 8 sources handles all concurrent SFX at ≤60fps tap rate without allocation
- `PlayerPrefs.Save()` after every write ensures preferences survive Android OOM kill

### Negative
- AudioMixer exposed parameter names (`SFXVolume`, `AmbientVolume`, `UIVolume`) must not be renamed post-launch — they are stored in PlayerPrefs keys indirectly (via the mapping in entities.yaml)
- 8-source pool uses round-robin eviction — if >8 simultaneous SFX fire, oldest is cut off. Not a practical concern for a tap-only puzzle game.
- iOS Audio Session category must be set appropriately — if set to `AVAudioSessionCategorySoloAmbient` (Unity default), other apps' audio is interrupted. Should be `AVAudioSessionCategoryAmbient` for a casual puzzle game.

### Risks
- **Risk**: Settings UI added in a future milestone uses `AudioMixer.SetFloat("SFXVolume")` directly instead of calling `AudioSystem.SetSFXVolume()` → PlayerPrefs not updated → preference lost on restart. **Mitigation**: All volume changes must go through `IAudioSystem` interface methods; control manifest enforces it.
- **Risk**: Ambient hum loop audio clip not assigned in inspector → `NullReferenceException` in Awake on Clip.Play(). **Mitigation**: AudioSystem has a null guard on `_machineHumClip`; logs a warning if missing rather than crashing.
- **Risk**: iOS interruption (phone call, Siri) stops all `AudioSources`; ambient hum does not restart on resume. **Mitigation**: `OnApplicationFocus(true)` in AudioSystem restarts ambient source if `!_ambientSource.isPlaying`.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| audio-system.md | TR-AUDIO-001: Three AudioBus groups: SFX, Ambient, UI | AudioMixer with 3 exposed parameter groups; all sources routed via `outputAudioMixerGroup` |
| audio-system.md | TR-AUDIO-002: `AudioMixer.SetFloat` for volume control | `SetXxxVolume()` calls `_audioMixer.SetFloat(paramName, LinearToDb(volume))` |
| audio-system.md | TR-AUDIO-003: PlayerPrefs audio keys read on Awake | PlayerPrefs read at SEO −80; all three keys applied to mixer in Awake |
| audio-system.md | TR-AUDIO-004: `PlayBoltSettle(bool)` — AnimationSystem sole caller | `PlayBoltSettle(bool)` documented as sole API for bolt SFX; documented constraint |
| audio-system.md | TR-AUDIO-005: Machine ambient hum loop | Dedicated looping `AudioSource` on Ambient group; starts in Awake |
| audio-system.md | TR-AUDIO-006: Pooled AudioSource (8 sources) for concurrent SFX | 8-source pool on AudioSystem; round-robin selection via `NextPoolSource()` |

## Performance Implications
- **CPU**: `AudioMixer.SetFloat()`: 2 calls at startup (negligible). Per-SFX: `PlayOneShot()` on existing source (zero allocation). `NextPoolSource()`: 1 array index, 1 modulo op — negligible.
- **Memory**: 8 AudioSource components + 1 ambient: ~9 × 800 bytes ≈ ~7.2 KB. AudioClip assets (loaded separately via Addressables): budget per clip ~50KB.
- **Load Time**: Awake at SEO −80: ~0.1ms.
- **Network**: N/A

## Migration Plan
No existing code to migrate — written before implementation begins.

## Validation Criteria
1. Device test (iOS): Ambient hum plays; bolt settle SFX plays on tap; does not interrupt other audio apps when `AVAudioSessionCategoryAmbient` is set
2. Device test (Android): Volume preferences persist after force-kill + relaunch
3. Unit test: `LinearToDb(0f) == -80f`; `LinearToDb(1f) ≈ 0f`; `LinearToDb(0.5f) ≈ -6f`
4. Manual test: Settings UI (future) sets SFX volume to 0; bolt SFX silent; restart app; still silent
5. Unit test: Pool round-robin — fire 9 SFX simultaneously; verify 9th overlaps source 0 (oldest)

## Related Decisions
- ADR-0001: Singleton Architecture — AudioSystem at SEO −80; DDOL
- ADR-0002: Event Architecture — no audio events (push model via `PlayBoltSettle`); AnimationSystem calls directly
- ADR-0003: Save System Design — `PlayerPrefs.Save()` after every write (Android OOM kill rule)
- `design/gdd/audio-system.md`, `design/registry/entities.yaml` (audio mixer parameter names)
