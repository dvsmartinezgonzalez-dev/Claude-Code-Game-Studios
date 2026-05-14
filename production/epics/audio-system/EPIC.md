# Epic: Audio System

> **Layer**: Foundation
> **GDD**: design/gdd/audio-system.md
> **Architecture Module**: AudioSystem
> **Status**: Ready
> **Manifest Version**: 2026-05-12
> **Stories**: Not yet created — run `/create-stories audio-system`

## Overview

The Audio System is BoltSort's sound manager: the infrastructure layer that owns audio clip loading, playback routing, and volume control. It manages all sound categories at MVP scope — per-move bolt SFX (lift click on selection, settle click on placement), stack completion chimes (four-note progression indexed by completion order), the ambient machine hum loop that makes the board feel like a living machine, and UI button tap sounds. All audio routes through a Unity AudioMixer with three independent groups (SFX, Ambient, UI) each with exposed volume parameters for future Settings UI integration. The AudioSystem is a DDOL singleton at SEO −80, initializing before any scene is playable: it reads PlayerPrefs audio preferences in `Awake`, applies them to the AudioMixer, creates a pool of 8 AudioSources for concurrent SFX playback, and starts the ambient hum loop. No system plays AudioSource clips directly — all audio requests go through the AudioSystem's named-method interface.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Singleton Architecture and Boot Sequence | AudioSystem is a DDOL singleton at SEO −80; `DontDestroyOnLoad` in Awake; no external scene scan | HIGH |
| ADR-0011: Audio Architecture | Three AudioMixer bus groups (SFX/Ambient/UI); linear→dB conversion; 8-source pool; `PlayBoltSettle(bool)` as sole bolt SFX API; `PlayerPrefs.Save()` after every volume write | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-AUDIO-001 | Three AudioMixer bus groups with independent volume control: SFX (`"SFXVolume"`), Ambient (`"AmbientVolume"`), UI (`"UIVolume"`) | ADR-0011 ✅ |
| TR-AUDIO-002 | Volume via `AudioMixer.SetFloat(paramName, dB)`; linear 0–1 → dB conversion: `dB = log10(vol)*20` or `−80` at floor (0.001f threshold) | ADR-0011 ✅ |
| TR-AUDIO-003 | PlayerPrefs audio preference keys (`audio.sfx_volume`, `audio.ambient_volume`, `audio.ui_volume`) read on Awake at SEO −80 and applied to AudioMixer | ADR-0011 ✅ |
| TR-AUDIO-004 | `PlayBoltSettle(bool isValid)` API; AnimationSystem is the sole caller — no other system calls this method | ADR-0011 ✅ |
| TR-AUDIO-005 | Machine ambient hum loop: dedicated looping AudioSource on Ambient group; starts in Awake; restarts on `OnApplicationFocus(true)` if not playing (iOS audio session interruption recovery) | ADR-0011 ✅ |
| TR-AUDIO-006 | Pool of 8 AudioSources for concurrent SFX playback; round-robin selection via `NextPoolSource()`; `PlayOneShot()` — zero allocation | ADR-0011 ✅ |

## Additional GDD Rules (from audio-system.md — inform story acceptance criteria)

- `PlayBoltLift()` — bolt lift clip on SFX group; random pitch ±10% per call (AUD-A-02)
- `PlayStackComplete(int stackIndex, bool isFinal)` — 4 indexed chime clips; final adds harmony tail (AUD-A-04)
- `PlayUIClick()` — UI tap on UI group; no pitch variation (AUD-A-05)
- Ambient volume arc: −3dB (0 stacks complete) → 0dB (all stacks complete); 3dB total range (AUD-C-04)
- AudioMixer sidechain duck on stack chime: −4 to −6dB, attack 50ms, release 300ms (AUD-C-05)
- `AudioListener.pause = true` on game pause and `OnApplicationPause(true)` (AUD-D-01)
- All named-method calls while `AudioListener.pause = true` are silently discarded (AUD-D-03)

## Key Implementation Notes

- All volume changes must go through `IAudioSystem.SetSFXVolume()` / `SetAmbientVolume()` / `SetUIVolume()` — **never** call `AudioMixer.SetFloat()` from game code directly
- `PlayerPrefs.Save()` after every `SetXxxVolume()` — Android OOM kill bypasses `OnApplicationQuit`
- AudioMixer exposed parameter names (`SFXVolume`, `AmbientVolume`, `UIVolume`) must not be renamed post-launch — stored in PlayerPrefs keys via the mapping in `design/registry/entities.yaml`
- iOS Audio Session: set `AVAudioSessionCategoryAmbient` (not `SoloAmbient`) to avoid interrupting other audio apps
- Null guard on `_machineHumClip`: if null, log warning (do not throw) — prevents crash on missing clip reference

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/audio-system.md` are verified
- Unit tests pass: `LinearToDb(0f) == -80f`; `LinearToDb(1f) ≈ 0f`; `LinearToDb(0.5f) ≈ -6f`; pool round-robin (9th SFX overlaps source 0)
- Device test (iOS): ambient hum plays; bolt settle fires on tap; does not interrupt other audio apps
- Device test (Android): volume preferences persist after force-kill + relaunch
- Manual test: Settings UI sets SFX volume to 0; bolt SFX silent; restart app; still silent

## Next Step

Run `/create-stories audio-system` to break this epic into implementable stories.
