# Multicolor Ball Animation + Thunder Undo Effect — Implementation & Verification

_Date: 2026-06-18 · Type: Visual/Feel + UI (ADVISORY evidence: manual walkthrough)_

Additive only. **No** gameplay/undo/move-validation/level/win logic was modified. The thunder
effect wraps the existing undo; the multicolor animation reuses the existing runtime frame-swap.

## Changes

| File | Change |
|------|--------|
| `Scripts/Gameplay/ThunderUndoEffect.cs` | **NEW.** Self-contained overlay effect (own Canvas @ sortingOrder 150). `Play(onImpact, onComplete)` coroutine: dim → lightning frames + SFX + flash → runs the supplied undo at the impact frame → fade out → done. Full failsafes. |
| `Scripts/Gameplay/HUDController.cs` | Creates the effect once in `BuildHud`; `OnUndoButtonTapped` now routes its success path through the effect (impact frame → existing `_onUndo`), with a `_thunderPlaying` re-entry guard. Gating (budget + `CanUndo`) is unchanged and still happens *before* any thunder. |
| `Scripts/Audio/AudioManager.cs` | Registers the `thunder` SFX (`Resources/Audio/thunder`). Respects the existing `bs.sfx_on` mute flag — no global audio settings touched. |
| `Editor/ThunderSheetImporter.cs` | **NEW.** `BoltSort > Import Thunder Sheet` — slices `thunder_sheet.png` into 16 frames (8×2, 192×512). Mirrors the existing multicolor importer. |
| `Resources/Sprites/Effects/thunder_sheet.png` | **NEW** (copied from `assets_admin/Levels/thunder.png`, 1536×1024). |
| `Resources/Audio/thunder.mp3` | **NEW** (copied from `assets_admin/Sounds_effects/thunder.mp3`). |

**Part 1 (multicolor) needs no code:** the runtime already frame-swaps `BallMulticolorFrames` at
8fps in `BoardView`. The art in `Resources/Sprites/Balls/ball_multicolor_sheet.png` is already the
correct sheet (md5 identical to the source). It looked static only because its `.meta` is
`spriteMode: 1` (Single, 0 sub-sprites) → `LoadAll` returns 1 frame. Slicing it fixes it.

## Slicing (now baked into the .meta files)

Update 2026-06-18: both sheets are now sliced via **hand-authored `.meta` files** (`spriteMode: 2`,
16 sub-sprites each — 4×4 ×128px for multicolor, 8×2 ×192×512 for thunder), so no Editor menu is
required. The Unity 6 menu importers (`BoltSort > Import Multicolor/Thunder Sheet`) remain available
but the legacy `TextureImporter.spritesheet` path they use is unreliable in Unity 6 — which is why
the sheets stayed single/un-sliced before (multicolor static; thunder showing the whole sheet as
"many tiny bolts"). On next Unity focus the textures reimport from the new metas → `LoadAll` returns
16 frames each. `thunder.mp3` imports automatically with default settings.

There is **no Animator / prefab** for the multicolor ball — it is a runtime `SpriteRenderer`
frame-swapped at 8fps in `BoardView` (sized by per-frame bounds normalization, so PPU is cosmetic;
set to 128 = 1 unit to match other balls). The only fix needed was slicing the sheet.

## Verification checklist (run in Play mode)

| Item | Expected |
|------|----------|
| ✓ multicolor animation loops | smooth 8fps color cycle on every wildcard ball (level 14/23/37/66/93), no frame jump |
| ✓ no performance issues | frame-swap is a multiply+mod+ref assignment per ball; negligible |
| ✓ thunder animation plays | on Undo: dim → lightning → flash; ONE full-screen Image (stretch 0,0→1,1, Simple, preserveAspect OFF, raycast OFF), one frame at a time @ ~30fps, above board+HUD, below settings; fills top→bottom on any resolution |
| ✓ thunder sound plays | `thunder.mp3` fires at the impact frame (synced to the strike); silent if SFX muted |
| ✓ undo behaves exactly as before | ball returns to its source tube; same result as today, just delayed to the impact frame (~0.3s) |
| ✓ no duplicate undos | `onImpact` is guarded exactly-once; re-press blocked by `_thunderPlaying` |
| ✓ no softlocks | effect always reaches its `finally` → re-enables; reset on level load |
| ✓ no input lock remaining | `_thunderPlaying` cleared in `onComplete` and on level load |
| ✓ gameplay responsive | total ≈ 0.6s; undo fires at ≈ 0.3s |

Test on: **normal**, **multicolor (L14)**, **frozen (L29)**, **mixed-capacity (L114)** levels.

## Failsafe behavior

| Condition | Behavior |
|-----------|----------|
| Undo history empty / `!CanUndo` / budget 0 | nothing happens (no thunder, no sound) — guarded before the effect |
| Thunder frames missing (sheet not sliced) | plain undo runs immediately, no overlay |
| Audio missing or SFX disabled | effect plays silently; undo still fires |
| Effect throws mid-play | `finally` still runs the undo + re-enables (undo > visuals) |
| Re-press during effect | ignored (no duplicate undo) |

## Tuning (constants in `ThunderUndoEffect.cs`)

`OverlayFadeInSec 0.14` · `OverlayHoldAlpha 0.45` (≈45% dim) · `FrameSec 0.024` (~42fps) ·
`ImpactFraction 0.45` (strike frame) · `OverlayFadeOutSec 0.10` · `FlashSec 0.07` /
`FlashPeakAlpha 0.55` (set `FlashPeakAlpha 0` to disable the flash) · `SortingOrder 150`.

## Notes

- Board (Physics2D) taps are not blocked during the ~0.3s before impact (spec STEP 1 only blocks
  *Undo* re-presses). A stray board move in that window is harmless — the undo simply reverts the
  latest move; `GSM.UndoRequested` handles all edge cases. No corruption or softlock.
- `thunder.mp3` (other SFX are `.wav`) — Unity imports mp3 fine; kept as-is to avoid re-encoding.
