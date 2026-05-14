# Animation System

> **Status**: In Design
> **Author**: Design session + art-director + systems-designer + gameplay-programmer agents
> **Last Updated**: 2026-04-18
> **Implements Pillar**: Every Pixel Earns Its Place, The Machine Must Sing, Flow Over Friction

## Overview

The Animation System is BoltSort's visual feedback layer — the system that translates Game State Manager events into bolt motion, glow effects, and completion bursts. It owns four animation categories at MVP scope: bolt motion (a smooth arc lift on selection, a glide-and-settle travel to the destination, and a crisp snap-back on cancel or invalid move), stack completion bursts (a glow pulse and VFX particle burst each time a color stack fills completely), board snaps (an instant visual correction to match committed board state when the GSM signals a state jump), and the level complete celebration (the full-board win moment when the final stack resolves). All animation triggers arrive as Game State Manager events — the Animation System subscribes to `level_loaded`, `board_state_changed`, `board_refresh_forced`, and `level_unloaded`, and responds to Sort Mechanic-driven move flow events via the Game State Manager's sequence ID system. At the end of each bolt travel animation, the Animation System emits `animation_complete(sequence_id)` — the signal Sort Mechanic uses to exit `MOVE_EXECUTING` and evaluate the win condition. Animation density scales with the active Quality Tier: all VFX Graph assets expose the `quality_density_multiplier` global property (Low=0.25, Medium=0.65, High=1.0), and the system reads the current tier from the Quality Tier System at scene load. The Animation System is also the sole caller of `PlayBoltSettle(isValid)` on the Audio System, triggered at the bolt's visual arrive keyframe. Its quality is measured by the arc of motion: if the bolt lift, travel, and settle form a single satisfying physical gesture, the sort loop feels like a machine in motion; if any part stutters, arrives late, or plays on a stale move, the loop breaks for that interaction.

## Player Fantasy

The Animation System is the tactile layer of BoltSort — the place where intent becomes sensation. You feel the bolt before you analyze the move: it lifts with a weighted hum, arcs through the air as if pulled by the logic of the machine itself, and settles into its slot with the soft authority of a part that was always meant to be there. The glow that follows isn't decoration — it's the machine confirming your hand.

At the same time, the animation never makes you wait. It moves at exactly the speed your intent travels: lift as you decide, travel as you look ahead, settle as you reach for the next one. A skilled player stops noticing the animation the way a pianist stops noticing the keys. The system earns its invisibility move by move, and the result is pure flow — a quiet accumulation of small, certain victories.

When something doesn't land — a cancel, an invalid drop — the snap-back is equally confident. No scolding, no drama. The board returns you to flow in under a heartbeat, and the machine waits patiently for your next instruction.

The peak moments break that invisibility on purpose: a stack completion pulses outward with a glow that feels physical, and the level-complete burst is the machine finally singing in full — every bolt in its place, every column resolved. Then the next board arrives, and the rhythm begins again.

*Primary pillars: Every Pixel Earns Its Place, Flow Over Friction, The Machine Must Sing*
*MDA target: Sensation (1), Submission (2)*

## Detailed Design

### Core Rules

All bolt motion coroutines use `WaitForSecondsRealtime`. Bolt motion plays on all quality tiers — Low tier reduces VFX particle density but never skips or shortens the arc gesture.

**Group A — Bolt Motion**

| Rule ID | Condition | Outcome |
|---------|-----------|---------|
| **BM-01** | `board_state_changed` received while `IDLE`; GSM has committed a move (sequence_id incremented) | Store the new `active_sequence_id`. Transition to `BOLT_LIFTING`. Play bolt lift arc from source slot to lift apex (BM-02), then travel arc to destination slot (BM-03), then settle (BM-04). The source slot visual empties immediately on BM-01 entry — visual gap reflects committed board state. |
| **BM-02 — LiftArc** | BOLT_LIFTING sub-phase | Translate bolt upward from slot resting position by `bolt_lift_height_px` (1.5× bolt height, default 48px) using ease-in-cubic over `lift_duration_ms` (default 80ms). Glow ramps from idle 0.4 to 1.0 linearly over the same 80ms. On completion, enter BOLT_TRAVELING sub-phase. |
| **BM-03 — TravelArc** | BOLT_TRAVELING sub-phase | Translate bolt on a convex arc: horizontal ease-out-cubic, peak height = lift apex + 0.5× bolt height at mid-distance. Duration = `lerp(travel_min_ms, travel_max_ms, sqrt(slot_separation / max_slot_separation))`, clamped to [80, 300ms]. Glow held at 1.0 throughout travel. On arrival at destination column centerline, enter BOLT_SETTLING sub-phase. |
| **BM-04 — Settle** | BOLT_SETTLING sub-phase | Final 15% of vertical descent uses ease-in-quint (magnetic pull). On reaching rest position: call `Audio.PlayBoltSettle(isValid: true)`. Play micro-bounce: +6% bolt height over 40ms (ease-out), return to rest over 30ms (ease-in). Glow ramps from 1.0 to idle 0.4 over 200ms starting from rest position arrival. After micro-bounce completes (70ms), emit `animation_complete(sequence_id: active_sequence_id)`. Transition to IDLE (or STACK_BURSTING if a stack completion is queued per SB-01). |
| **BM-05** | `move_rejected` received while `IDLE` | Transition to SNAP_BACK. Play rejection shake on destination stack visual: ±`rejection_shake_offset_px` (default 4px) horizontal, 3 oscillations over `rejection_shake_ms` (default 100ms). Call `Audio.PlayBoltSettle(isValid: false)` at shake frame 0. Transition to IDLE when shake completes. No `animation_complete` emitted — `move_rejected` does not enter MOVE_EXECUTING. |
| **BM-06 — CancelReturn** | Sort Mechanic emits move_cancelled; bolt held state was Sort Mechanic-local | Play direct return: bolt travels straight line from hover position to source slot, ease-in-out-cubic, `snap_back_cancel_ms` (default 120ms). No arc. Ease-in-quint deceleration into slot — no micro-bounce. Glow ramps from current level to idle 0.4 linearly over return duration. Emit completion signal on arrival so Sort Mechanic exits CANCELLATION state. |
| **BM-07 — InvalidReturn** | Sort Mechanic remains in BOLT_SELECTED after invalid drop; bolt stays held | Play spring-back to held hover position above last held coordinates: straight line, ease-out-quint, `snap_back_invalid_ms` (default 80ms). Bolt does NOT return to source stack — it stays in hand. Destination stack plays BM-05 rejection shake. No completion signal — Sort Mechanic remains in BOLT_SELECTED and accepts input immediately. |

> **Note on `move_rejected` routing:** Animation System subscribes to `move_rejected` on the event bus (Option A — consistent with the subscription model). Confirm in the IGameStateManager interface ADR before implementation.

---

**Group B — Board Snaps**

| Rule ID | Condition | Outcome |
|---------|-----------|---------|
| **BS-01** | `board_state_changed` received while `IDLE` (undo or GSM-driven correction) | Snap all bolt visuals to match current GSM board state synchronously in the same frame. No arc, no travel, no audio call. No `animation_complete` emitted. |
| **BS-02** | `board_refresh_forced(sequence_id)` received in any state | If in BOLT_LIFTING, BOLT_TRAVELING, or BOLT_SETTLING: immediately stop the in-progress coroutine. Place the bolt at its committed destination position (read from GSM board state). Do NOT emit `animation_complete`. Abort any pending completion burst. Snap all bolt visuals to GSM board state. Transition to IDLE. If already IDLE: snap visuals (same as BS-01). |

---

**Group C — Stack Completion Burst**

| Rule ID | Condition | Outcome |
|---------|-----------|---------|
| **SB-01** | End of BM-04; GSM reports a stack as newly full and monochromatic | After emitting `animation_complete`, transition to STACK_BURSTING. Burst plays **asynchronously** — Sort Mechanic has already exited MOVE_EXECUTING. Player can select the next bolt while the burst plays. |
| **SB-02** | STACK_BURSTING state entered for a given stack | Play two effects in parallel: (1) **Glow pulse** — ramp stack bolt emission from 0.4 to `glow_peak_intensity` (default 1.0) over `glow_ramp_up_ms` (80ms), hold for `glow_hold_ms` (60ms), ramp back to 0.4 over `glow_ramp_down_ms` (160ms). Total: 300ms. (2) **VFX burst** — spawn `StackCompleteBurst` VFX Graph asset at stack center. Ring expands from 0 to 2.5× stack radius over 350ms; color is white (`#E8F4FF`) at start, transitions to bolt color at 60% duration. 8–12 spark particles emit upward. `quality_density_multiplier` governs particle count. VFX tail completes asynchronously. State transitions to IDLE when glow ramp-down completes (300ms). |
| **SB-03** | `board_refresh_forced` received while STACK_BURSTING | Stop glow interpolation coroutine, reset bolt emission to idle 0.4. Do not stop in-flight VFX Graph asset (cosmetic tail). Snap visuals to GSM board state. Transition to IDLE. |

---

**Group D — Level Complete Celebration**

| Rule ID | Condition | Outcome |
|---------|-----------|---------|
| **LC-01** | `level_complete` received while `IDLE` | Transition to LEVEL_COMPLETE. Play celebration sequence, blocking all input: (1) Final stack burst fires normally (350ms VFX + 300ms glow). (2) Cascade wave: all completed stacks emit glow pulse (0.4→0.8→0.4 over 200ms), left-to-right with 60ms stagger between stacks. (3) Board ring: single large ring expands from board center, white first, transitions to dominant bolt color after 200ms, duration 600ms. (4) Glow settle: all bolt glows hold at 0.8 for 500ms, decay to 0.4 over 300ms. (5) Hold for `celebration_ui_delay_ms` (default 200ms). Emit `animation_complete(sequence_id)`. Transition to IDLE. Total block ≈ 1200ms (varies by color_count). |
| **LC-02** | Any GSM event during LEVEL_COMPLETE except `level_unloaded` or `board_refresh_forced` | Silently discarded. |
| **LC-03** | `board_refresh_forced` received during LEVEL_COMPLETE | Abort celebration coroutine. Snap visuals to GSM board state. Transition to IDLE. Watchdog edge case — not expected in normal gameplay. |

---

**Group E — Quality Tier Behavior**

| Rule ID | Tier | Behavior |
|---------|------|----------|
| **QT-01** | All — scene load | On `level_loaded`: query QTS synchronously for `quality_density_multiplier`, cache for session. Set as global VFX Graph property. Do not re-query mid-session. |
| **QT-02** | Low (0.25) | Bolt arc, timing, glow ramps, completion rings: identical to High. Micro-bounce on settle: **disabled** — bolt arrives and stops. Spark particles: disabled (0.25 × authored count rounds to ~2; treat as 0). Ring effects, glow transitions, and audio calls: identical on all tiers. |
| **QT-03** | Medium (0.65) | All motion and glow behavior identical to High. Spark particle count at 65% of authored. All ring effects retained. |
| **QT-04** | High (1.0) | Full authored behavior on all animations, particles, and glow. |

---

**Group F — Sequence ID Safety**

| Rule ID | Condition | Outcome |
|---------|-----------|---------|
| **SQ-01** | `animation_complete(sequence_id)` emitted | Carries `active_sequence_id` stored when the triggering event was received. Animation System does not validate staleness — Sort Mechanic discards stale signals per GSM UND-06. |
| **SQ-02** | Second `board_state_changed` arrives during active animation | Cannot occur under normal operation — Sort Mechanic holds MOVE_EXECUTING until `animation_complete` is received. If received (test or watchdog path): queue it; process when IDLE is next reached. Queue capacity: 1. A third event while one is queued is silently dropped. |

---

### States and Transitions

| State | Description | Entry | Exit |
|-------|-------------|-------|------|
| `UNLOADED` | No board. All visual objects destroyed. | App launch; `level_unloaded` | `level_loaded` → IDLE |
| `IDLE` | Board present. No animation active. | QT-01 complete; any animation completion | Any condition below |
| `BOLT_LIFTING` | Lift arc phase in progress | BM-01 (`board_state_changed` in IDLE) | Lift complete → BOLT_TRAVELING; `board_refresh_forced` → IDLE |
| `BOLT_TRAVELING` | Travel arc phase in progress | BM-02 | Arrive keyframe → BOLT_SETTLING; `board_refresh_forced` → IDLE |
| `BOLT_SETTLING` | Micro-settle phase. `PlayBoltSettle` already called. | BM-03 | Settle complete + `animation_complete` emitted → IDLE or STACK_BURSTING; `board_refresh_forced` → IDLE |
| `SNAP_BACK` | Rejection shake on destination stack | BM-05 (`move_rejected` in IDLE) | Shake complete → IDLE; `board_refresh_forced` → IDLE |
| `STACK_BURSTING` | Completion glow pulse + VFX burst | SB-01 (after BM-04 with newly complete stack) | Glow ramp-down complete (300ms) → IDLE; `board_refresh_forced` → IDLE |
| `LEVEL_COMPLETE` | Full-board celebration sequence | LC-01 (`level_complete` in IDLE) | Sequence complete + `animation_complete` → IDLE; `board_refresh_forced` → IDLE; `level_unloaded` → UNLOADED |

**Transition table:**

| From | Trigger | To | Rule |
|------|---------|-----|------|
| UNLOADED | `level_loaded` | IDLE | QT-01 |
| IDLE | `board_state_changed` (move commit) | BOLT_LIFTING | BM-01 |
| IDLE | `move_rejected` | SNAP_BACK | BM-05 |
| IDLE | `board_state_changed` (undo/snap, no new stack complete) | IDLE | BS-01 |
| IDLE | `board_state_changed` (undo/snap, stack newly complete) | STACK_BURSTING | SB-01 |
| IDLE | `board_refresh_forced` | IDLE | BS-02 |
| IDLE | `level_complete` | LEVEL_COMPLETE | LC-01 |
| IDLE | `level_unloaded` | UNLOADED | — |
| BOLT_LIFTING | Lift complete | BOLT_TRAVELING | BM-02 |
| BOLT_LIFTING | `board_refresh_forced` | IDLE | BS-02 |
| BOLT_TRAVELING | Arrive keyframe | BOLT_SETTLING | BM-03 |
| BOLT_TRAVELING | `board_refresh_forced` | IDLE | BS-02 |
| BOLT_SETTLING | Settle + bounce complete | IDLE or STACK_BURSTING | BM-04, SB-01 |
| BOLT_SETTLING | `board_refresh_forced` | IDLE | BS-02 |
| SNAP_BACK | Shake complete | IDLE | BM-05 |
| STACK_BURSTING | Glow ramp-down complete | IDLE | SB-02 |
| STACK_BURSTING | `board_refresh_forced` | IDLE | SB-03 |
| LEVEL_COMPLETE | Sequence complete | IDLE | LC-01 |
| LEVEL_COMPLETE | `board_refresh_forced` | IDLE | LC-03 |
| Any | `level_unloaded` | UNLOADED | — |

**Illegal — assert or silently discard:**
- `board_state_changed` (new move commit) while in BOLT_LIFTING, BOLT_TRAVELING, or BOLT_SETTLING
- Any event except `level_unloaded` and `board_refresh_forced` while in LEVEL_COMPLETE
- Any event except `level_loaded` while in UNLOADED

---

### Interactions with Other Systems

| System | Direction | Data In | Data Out | Interface |
|--------|-----------|---------|----------|-----------|
| **Game State Manager** | GSM → Anim (event subscription) | `level_loaded(level_id, color_count, stack_depth, temp_slot_count, temp_slot_depth, sequence_id)`, `board_state_changed(sequence_id, move_count)`, `board_refresh_forced(sequence_id)`, `level_unloaded(level_id)`, `level_complete(level_id, move_count, sequence_id)` | `animation_complete(sequence_id: int64)` | Animation System self-subscribes during Awake. All communication event-driven — no direct method calls in either direction. |
| **Sort Mechanic** | Indirect via GSM event relay | `move_committed` and `move_rejected` flow into GSM; Animation System subscribes to GSM events. Sequence IDs become `active_sequence_id`. | `animation_complete(sequence_id)` — Sort Mechanic subscribes to exit MOVE_EXECUTING. Stale IDs discarded per GSM UND-06. | Animation System must emit `animation_complete` within `watchdog_timeout_ms` (1500ms). Neither system calls the other directly. |
| **Audio System** | Anim → Audio (direct method call) | None | `PlayBoltSettle(isValid: bool)` — called at bolt arrive keyframe (BM-04) on all quality tiers. Must NOT be called for animations aborted by `board_refresh_forced`. | Animation System is sole caller of `PlayBoltSettle`. Audio System does not validate sequence IDs (AUD-C-02). |
| **Quality Tier System** | QTS → Anim (synchronous query at load) | `quality_density_multiplier: float` — queried once at `level_loaded`, cached for session. | None | Read-only consumer. Hot-swap mid-session not reflected until next `level_loaded`. |
| **Skin System** | Skin → Anim (synchronous query at load) | Active skin asset (`SkinData` ScriptableObject) — bolt sprite palette per color index. *(Provisional — Skin System GDD not yet authored.)* | None | `ISkinProvider.GetActiveSkin()` call in Start (not Awake). Skin System must be initialized before `level_loaded` fires — Script Execution Order dependency. |

**Open questions resolved by this GDD:**

| Prior open question | Resolution |
|--------------------|-----------|
| Animation completion signal name/signature | `animation_complete(sequence_id: int64)` |
| WaitForSeconds vs WaitForSecondsRealtime | `WaitForSecondsRealtime` on all coroutines |
| Settle audio fallback for Low quality tier | No fallback — `PlayBoltSettle` fires identically on all tiers at the visual arrive keyframe |

## Formulas

### F-01 — Bolt Travel Duration

The bolt travel duration formula is defined as:

`travel_ms = lerp(travel_min_ms, travel_max_ms, (slot_separation / max_slot_separation) ^ travel_distance_curve_exponent)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Minimum travel duration | `travel_min_ms` | float | 50–120ms (default 80ms) | Floor duration applied when source and destination are adjacent columns. Tuning knob. |
| Maximum travel duration | `travel_max_ms` | float | 150–400ms (default 300ms) | Ceiling duration applied at the widest board crossing. Tuning knob. |
| Column separation | `slot_separation` | int | 1 – max_slot_separation | `|source_index - destination_index|`. |
| Maximum column separation | `max_slot_separation` | int | 1 – (color_count + temp_slot_count − 1) | Largest legal slot_separation on this board. Computed once at level load. |
| Distance curve exponent | `travel_distance_curve_exponent` | float | 0.3–1.0 (default 0.5) | Shape of the distance-to-duration mapping. 0.5 = sqrt (short moves get more time proportionally). 1.0 = linear. Tuning knob. |
| **Output: travel_ms** | — | float | [travel_min_ms, travel_max_ms] | Duration of BM-03 travel arc only. Does not include lift (BM-02, 80ms) or settle (BM-04, 70ms). |

**Output Range:** 80ms to 300ms at default knob values. Cannot exceed travel_max_ms or fall below travel_min_ms regardless of board geometry.

**Example (5 color stacks, 2 temp slots, default knobs — max_slot_separation = 6):**
- Cross-board move (col 0 → col 4, separation = 4): `lerp(80, 300, (4/6)^0.5) = lerp(80, 300, 0.817) ≈ 260ms`
- Adjacent move (col 0 → col 1, separation = 1): `lerp(80, 300, (1/6)^0.5) = lerp(80, 300, 0.408) ≈ 170ms`

**Cross-system note (Sort Mechanic GDD):** The Sort Mechanic GDD's "animation duration — bolt move: 180ms (80–300ms)" tuning knob covers travel-only but predates the three-phase bolt gesture. The MOVE_EXECUTING lockout window is `travel_ms + 70ms` (settle). The Sort Mechanic GDD Tuning Knobs section must be updated before implementation to split this into separate `travel_min_ms` and `travel_max_ms` entries. The lift phase (80ms) is pre-commit and does not contribute to the MOVE_EXECUTING lockout.

---

### F-02 — VFX Particle Count

The live particle count formula is defined as:

`live_particle_count = floor(authored_particle_count × quality_density_multiplier)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Authored particle count | `authored_particle_count` | int | per-asset (8–12 for StackCompleteBurst) | Particle count set in the VFX Graph asset. Each asset owns this independently. Tuning knob per asset. |
| Quality density multiplier | `quality_density_multiplier` | float | {0.25, 0.65, 1.0} | Owned by Quality Tier System. See registry constant `quality_density_multiplier`. |
| **Output: live_particle_count** | — | int | [floor(authored × 0.25), authored] | Actual particles spawned at runtime. `floor` prevents fractional particles. |

**Output Range:** Integer. When `live_particle_count` rounds to ≤ 2 for low-authored-count assets, the VFX Graph asset is not spawned (QT-02). The ring effect is retained on all tiers regardless of particle count.

**Example (StackCompleteBurst, authored = 10):**
- Low (0.25): `floor(10 × 0.25) = 2` → disabled (QT-02)
- Medium (0.65): `floor(10 × 0.65) = 6` → 6 particles spawned
- High (1.0): `floor(10 × 1.0) = 10` → 10 particles spawned

---

### F-03 — Level Complete Celebration Total Duration

The celebration total duration formula is defined as:

`celebration_ms = final_burst_ms + max(cascade_wave_ms, board_ring_ms) + glow_settle_ms + celebration_ui_delay_ms`

Where: `cascade_wave_ms = ((color_count − 1) × cascade_stagger_ms) + cascade_pulse_duration_ms`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Final burst duration | `final_burst_ms` | float | Fixed 400ms | Final stack completion burst. VFX (350ms) + glow (300ms) run in parallel; 400ms accounts for audio tail. Not tunable. |
| Cascade stagger interval | `cascade_stagger_ms` | float | 40–100ms (default 60ms) | Delay between successive stack glow pulses during cascade wave. Tuning knob. |
| Cascade pulse duration | `cascade_pulse_duration_ms` | float | 150–300ms (default 200ms) | Per-stack glow pulse duration (0.4→0.8→0.4). Tuning knob. |
| Cascade wave duration | `cascade_wave_ms` | float | 200–820ms | `(color_count − 1) × cascade_stagger_ms + cascade_pulse_duration_ms`. Derived. |
| Board ring duration | `board_ring_ms` | float | Fixed 600ms | Board-wide ring VFX. Runs parallel with cascade wave. Not tunable. |
| Glow settle duration | `glow_settle_ms` | float | Fixed 800ms | All bolt glows hold at 0.8 for 500ms then decay to 0.4 over 300ms. Pillar 5 expression — not tunable. |
| UI delay | `celebration_ui_delay_ms` | float | 100–500ms (default 200ms) | Hold before `animation_complete` emitted and level-complete UI appears. Tuning knob. |
| Board color count | `color_count` | int | 2–8 | Number of color stacks. Sourced from level data. |
| **Output: celebration_ms** | — | float | ≈ 1,600–2,020ms | Total input-blocked duration from LC-01 entry to `animation_complete` emission. |

**Output Range:** ~2,000ms across the full color_count range. The board_ring_ms (600ms) governs the parallel phase for color_count ≤ 7; cascade_wave_ms overtakes at color_count = 8 (620ms vs 600ms). Variation across the full range is ≈ 20ms.

**Example (5 color stacks, default knobs):**
```
cascade_wave_ms  = (5 − 1) × 60 + 200 = 440ms
board_ring_ms    = 600ms  (governs — 600 > 440)
celebration_ms   = 400 + max(440, 600) + 800 + 200 = 2,000ms
```

## Edge Cases

**If `slot_separation = max_slot_separation` (cross-board move at maximum distance):** `travel_ms = travel_max_ms` exactly (300ms). Maximum animation duration is 80ms lift + 300ms travel + 70ms settle = 450ms — well under `watchdog_timeout_ms` (1500ms).

**If `slot_separation = 1` and `max_slot_separation = 1` (2-column board — color_count = 2, temp_slot_count = 0):** F-01 evaluates to `lerp(80, 300, 1.0) = 300ms`. Every move on a 2-column board takes 300ms regardless of distance. Level authors must account for this when designing 2-color levels.

**If `slot_separation = 0` (same-column placement — must not occur):** Sort Mechanic must reject same-column moves before they reach the Animation System. If one arrives, `travel_ms = travel_min_ms` (80ms) and the bolt makes a micro-arc to the same visual position. No crash; board state resolves correctly.

**If `authored_particle_count = 4` at Low tier:** `floor(4 × 0.25) = 1` — below the QT-02 disable threshold of ≤ 2; asset not spawned. Art team must author all particle assets with a minimum of 12 particles if meaningful density at Low tier is required.

**If `level_complete` arrives while Animation System is in `STACK_BURSTING` (winning bolt fills the last stack, but the completion burst is still playing):** Queue the `level_complete` event during STACK_BURSTING (same mechanism as SQ-02). On the 300ms glow ramp-down completing (IDLE entry), immediately process the queued `level_complete` and enter LEVEL_COMPLETE. The celebration is never silently dropped.

**If the SQ-02 queue holds a pending `board_state_changed` and `board_refresh_forced` arrives before IDLE is reached:** Flush the queue — do not process the queued event. The queued `board_state_changed` carries a stale sequence ID; processing it would launch a phantom bolt animation for a move that no longer exists in GSM state.

**If `board_refresh_forced` arrives during `SNAP_BACK` (rejection shake in progress):** Terminate the shake coroutine immediately. Snap visuals to GSM board state. Transition to IDLE. No `animation_complete` emitted (BM-05 never emits one).

**If a second `move_rejected` arrives while already in `SNAP_BACK`:** Silently discarded. Sort Mechanic is in lockout and cannot produce a second rejection during an active shake under normal operation.

**If VFX Graph is unavailable on device (no GPU compute — OpenGL ES 2.0 fallback):** Spark particles suppressed. The ring expansion effect (primary completion signal) must be implemented as a non-VFX-Graph element (UI Image mask-scale or sprite-based ring shader) so it is retained on all hardware per QT-02. The glow pulse (MaterialPropertyBlock + HDR emission) is unaffected — no VFX Graph dependency.

**If `ISkinProvider.GetActiveSkin()` returns null** (Script Execution Order violation): Fall back to the built-in default skin (a ScriptableObject asset serialized directly in the AnimationSystem prefab). In debug builds, assert and log the ordering violation. The Skin System must be assigned an earlier Script Execution Order than the Animation System in Unity Project Settings.

**If `GetActiveSkin()` returns a skin asset missing a color entry for a `color_id` on the current board:** Debug builds: assert and display magenta as a sentinel. Release builds: fall back to white for the missing color. Do not throw — a missing color entry must not crash a live session.

**If the player changes quality tier mid-session via Settings UI:** Cached `quality_density_multiplier` is NOT updated. The new tier takes effect at the next `level_loaded`. This is documented behavior. Real-time hot-swap requires a QTS `tier_changed` event (future scope).

**If a pre-won board is loaded per GSM L-04 (board already in solved state at `level_loaded`):** Animation System enters IDLE and receives `level_complete` immediately without prior bolt animation. LC-01 fires from IDLE — valid by the state table. The "final stack burst" in the celebration fires on a stack that was never animated into completion; this is visually correct and serves as the celebration entry beat.

**If undo fires during `STACK_BURSTING` (undo arrives as `board_state_changed`):** STACK_BURSTING queues the event per SQ-02 logic. On IDLE entry after the 300ms glow ramp-down, the undo board snap (BS-01) fires — all visuals snap to the reverted board state. If the undone move un-completes the bursting stack, the snap immediately removes the visual completion. The snap is authoritative.

**If `Audio.PlayBoltSettle` fires at BM-04 arrive keyframe and `board_refresh_forced` cancels the settle coroutine immediately after:** The audio call has already fired and cannot be recalled. The settle SFX tail plays over the board snap. The sub-70ms desync is imperceptible. No corrective action required.

**If `travel_max_ms` is tuned above `watchdog_timeout_ms − 200ms` (e.g., travel_max = 1350ms with watchdog at 1500ms):** Legitimate settle animations can trigger a false watchdog fire. The `travel_max_ms` tuning knob safe range explicitly prohibits setting it above `watchdog_timeout_ms − 200ms`. An editor validation script must enforce this constraint alongside the existing QTS quality density validation script.

**If `color_count = 8` and two stacks appear to complete simultaneously (corrupted level data or editor error):** Impossible under bolt-count invariant — only one bolt travels at a time. If reached via a defective level, the SQ-02 queue handles at most one queued completion; the second is silently dropped. The level editor must validate the bolt-count invariant before publishing.

## Dependencies

### Upstream (systems this one depends on)

| System | Type | Interface | Status |
|--------|------|-----------|--------|
| **Game State Manager** | Hard | Event subscription: `level_loaded`, `board_state_changed`, `board_refresh_forced`, `level_unloaded`, `level_complete`. Emits: `animation_complete(sequence_id: int64)`. | GDD authored ✓ |
| **Quality Tier System** | Hard | Synchronous `GetQualityDensityMultiplier()` call at `level_loaded`. Default to 1.0 (High) if unavailable. | GDD authored ✓ |
| **Audio System** | Hard | Direct method call `PlayBoltSettle(isValid: bool)` at bolt arrive keyframe. Skip gracefully if unavailable — no crash. | GDD authored ✓ |
| **Skin System** | Soft | `ISkinProvider.GetActiveSkin()` → `SkinData` ScriptableObject. Falls back to default skin ScriptableObject if null. Provisional — Skin System GDD not yet authored. | Not started ✗ |

### Downstream (systems that depend on this one)

| System | What they consume | Status |
|--------|------------------|--------|
| **Sort Mechanic** | `animation_complete(sequence_id)` — used to exit MOVE_EXECUTING state and evaluate win condition | GDD authored ✓ (signal name/sig resolved here) |
| **In-Game HUD** | `animation_complete` — used to re-enable undo button after MOVE_EXECUTING resolves | Not started ✗ — dependency note must be added when HUD GDD is authored |

### Bidirectionality notes

- Sort Mechanic GDD must be updated to list Animation System as a downstream dependent (Sort Mechanic reads `animation_complete`). The open question for animation signal name/signature is resolved: `animation_complete(sequence_id: int64)`.
- In-Game HUD GDD (not yet authored): must list Animation System as a dependency when written.
- Game State Manager GDD: correctly lists Animation System as a subscriber in its event ownership table — no update required.

### Provisional assumptions (Skin System)

The Skin System GDD has not been authored. The Animation System assumes:
1. `ISkinProvider.GetActiveSkin()` returns a synchronously-available `SkinData` ScriptableObject
2. `SkinData` contains `Sprite[] boltSpritesByColorIndex` with a valid entry for every `color_id` in use
3. The Skin System initializes before the Animation System's `Start()` — enforced by Script Execution Order

If the Skin System changes any of these contract points, the Animation System implementation stories must be updated before implementation begins.

## Tuning Knobs

All tuning knobs are authored on a data asset (ScriptableObject) — no values are hardcoded in code.

| Parameter | Default | Safe Range | Effect if Too High | Effect if Too Low |
|-----------|---------|------------|-------------------|-------------------|
| `lift_duration_ms` | 80ms | 50–120ms | Lift feels slow; delay between tap and commitment | Lift imperceptible; bolt loses "weighted selection" feel |
| `travel_min_ms` | 80ms | 50–120ms | Adjacent moves feel slow — pacing inconsistency | Adjacent moves vanish before the arc reads spatially |
| `travel_max_ms` | 300ms | 150–400ms | Long-distance moves break flow. **Must not exceed `watchdog_timeout_ms − 200ms`** | Far moves arrive before arc is perceived as spatial |
| `travel_distance_curve_exponent` | 0.5 | 0.3–1.0 | At 1.0 (linear): far moves feel slow relative to near | At 0.3: all durations converge near travel_min_ms; spatial arc feels uniform |
| `snap_back_cancel_ms` | 120ms | 60–200ms | Cancel feels as heavy as a committed move | Cancel imperceptible; board resets without player acknowledgment |
| `snap_back_invalid_ms` | 80ms | 40–120ms | Invalid spring-back draws too much attention | Spring-back invisible; bolt appears already at held position |
| `rejection_shake_offset_px` | 4px | 2–8px | Shake is visually noisy; board appears unstable | Shake imperceptible; invalid placement has no visual confirmation |
| `rejection_shake_ms` | 100ms | 60–160ms | Shake lingers; input lockout extends noticeably | Shake too fast to register; player misses rejection feedback |
| `glow_ramp_up_ms` (lift) | 80ms | 50–120ms | Glow lags behind bolt motion | Glow precedes motion — uncanny mismatch |
| `glow_peak_intensity` | 1.0 | 0.7–1.0 | Bloom bleeds to adjacent elements at High tier | Completion pulse undersells the stack resolution |
| `glow_ramp_down_ms` (settle) | 200ms | 100–300ms | Glow lingers after settle; board looks "sticky" | Glow snaps off with settle; feels mechanical |
| `glow_ramp_up_ms` (burst) | 80ms | 50–120ms | Burst entry feels sluggish | Burst feels instant — no anticipation |
| `glow_hold_ms` (burst) | 60ms | 30–120ms | Too many simultaneous bursts create visual noise | Burst barely registers before decay begins |
| `glow_ramp_down_ms` (burst) | 160ms | 80–250ms | Decay too slow; adjacent move glow bleeds into burst | Decay abrupt; cutoff reads as a pop |
| `cascade_stagger_ms` | 60ms | 40–100ms | Cascade wave too slow; level end overstays | Stacks pulse near-simultaneously; wave is unreadable |
| `cascade_pulse_duration_ms` | 200ms | 150–300ms | Each stack glow lingers; wave looks like a sustained hold | Pulse too fast to read per-stack |
| `celebration_ui_delay_ms` | 200ms | 100–500ms | Pause feels like a freeze | UI appears during glow settle — overlaps the payoff |
| `bolt_lift_height_px` | 48px | 32–64px | Bolt exits visible board area on small screens | Lift fails to clear the stack; bolt appears still connected to origin |

**Cross-system tuning note:** The Sort Mechanic GDD's "animation duration — bolt move: 180ms (80–300ms)" knob is superseded by `travel_min_ms` and `travel_max_ms` above. The MOVE_EXECUTING lockout = `travel_ms + 70ms (fixed settle)`. The Sort Mechanic GDD must be updated before implementation sprints.

**Quality Tier knobs (read-only — owned by Quality Tier System):** `quality_density_multiplier` values (Low=0.25 / Medium=0.65 / High=1.0) are defined in `design/gdd/quality-tier-system.md` and the entity registry. The Animation System is a read-only consumer.

## Visual/Audio Requirements

*Source: Art Director consultation (Phase C delegation). All values are concrete starting points for the first art pass — feel tuning refines numbers, not replaces them.*

### Art Bible Principles Governing This System

1. **Everything glows faintly — the machine is always alive.** Glow is not decoration. It tracks bolt lifecycle: lift intensifies it, settle returns it, cancel snaps it back. The glow never fully disappears on any interactive element.
2. **Every pixel earns its place.** Animation exists to carry information, not to perform. If a motion beat can be removed without a player noticing, it was not earning its place. Stack completion bursts are the intentional exception — the one moment the animation is allowed to be seen.
3. **Completion bursts pulse white then fade to the bolt's color.** White = machine confirms. Bolt color = identity reasserted. This sequence applies to all resolution moments and must not be reversed.

### Bolt Arc Language

**Lift arc:**
- Type: Two-phase gesture — vertical lift first, travel arc second. Not a single bezier from source to destination.
- Height: 1.5× bolt height above resting position (default 48px).
- Curve: ease-in-cubic. Slow start (weighted), accelerates upward. No pause at apex — transitions directly into travel.
- Duration: 80ms.
- Glow: linear ramp from idle 0.4 to 1.0 over the 80ms lift. Linear, not eased — the glow feels like a switch thrown, not a fade.

**Travel arc:**
- Path: Convex arc. Peak height = lift apex + 0.5× bolt height at mid-horizontal-distance. Low, flat parabola — thrown accurately, not lobbed.
- Horizontal alignment: bolt center tracks the centerline between source and destination column centers.
- Curve: ease-out-cubic (carries momentum from lift, decelerates into destination).
- Duration: F-01 distance-scaled (80–300ms).
- Glow: held at 1.0 throughout travel.

**Settle:**
- Type: Magnetic pull with micro-bounce. At 85% of travel, horizontal easing tightens (bolt locks on to destination column). Final 15% of horizontal and all vertical descent: ease-in-quint.
- Micro-bounce: +6% bolt height over 40ms (ease-out), return to rest over 30ms (ease-in). One bounce only — no oscillation.
- Glow: ramps from 1.0 to idle 0.4 over 200ms, starting from rest position arrival (not from bounce start). Ease-out. The glow fades after motion resolves, not simultaneously.
- Audio trigger: `PlayBoltSettle(isValid: true)` fires at the moment the bolt hits rest position (start of micro-bounce frame).

**Cancel / Invalid snap-back:**
- Path: Straight line return. No arc. No lift above current travel height.
- Curve: ease-in-out-cubic for cancel (120ms). Ease-out-quint for invalid (80ms, springs back to held hover position).
- Landing: ease-in-quint deceleration into slot (cancel) or hover position (invalid). No micro-bounce.
- Glow: ramps from current level (1.0 or wherever interrupted) to idle 0.4 linearly over the return duration. Glow and motion resolve simultaneously.
- Audio: `PlayBoltSettle(isValid: false)` at re-seat moment (cancel only — invalid has no re-seat event).

### Stack Completion Burst

**VFX intent:** Radial ring expand from stack center + short upward spark particles. A pressure release, not a sparkle effect.

**Ring:**
- Expands from 0 to 2.5× stack bounding radius over 350ms.
- Opacity: 1.0 at start, 0.0 at end (linear fade starts at 20% of duration).
- Ring width: 4px fixed (thins visually as it expands — correct behavior).
- Color: white (`#E8F4FF`) at start. Transitions to bolt color at 60% of duration (lerp, not hard cut).
- Implementation: ring must be a non-VFX-Graph element (UI Image mask-scale or sprite-based ring shader) so it is retained even if VFX Graph is unavailable.

**Spark particles:**
- Count: 8–12 authored (see F-02 for quality tier scaling).
- Direction: upward and outward from stack top.
- Travel distance: 0.5–0.75× stack height above top bolt.
- Color sequence: matches ring (white → bolt color).
- Duration: one single burst event — no looping.
- Low tier (quality_density_multiplier = 0.25): disabled (rounds to ≤ 2 per F-02).

**Duration:** 350ms total. Plays asynchronously — does not block input.

### Level Complete Celebration

**Sequence (sequential, with parallel sub-phases):**

1. **Final stack burst** (frames 0–400ms): plays as a normal SB-02 burst (350ms VFX + 300ms glow in parallel).
2. **Cascade wave + board ring** (frames 0–≈600ms, start simultaneously with final burst):
   - Each completed stack emits a secondary glow pulse (0.4→0.8→0.4 over 200ms), staggered left-to-right at 60ms per stack.
   - One board-wide ring expands from board center: same ring mechanics as stack burst but scaled to full board bounding radius, 600ms duration. White first, transitions to dominant bolt color (most-used color on board) at 200ms.
3. **Glow settle** (after cascade completes): all bolt glows hold at 0.8 for 500ms, then decay to 0.4 over 300ms. The machine exhales.
4. **Pre-UI hold**: `celebration_ui_delay_ms` (default 200ms) before `animation_complete` emitted.

**Tone:** Triumphant but resolving. Not explosive. The board resolves, the machine confirms, the player is invited to the next level. No screen flash, no confetti, no camera shake, no color explosion.

### Quality Tier Visual Summary

| Tier | Bolt arc | Micro-bounce | Spark particles | Rings | Glow transitions |
|------|---------|-------------|----------------|-------|-----------------|
| High | Full | Yes | Full authored count | Full | Full |
| Medium | Identical to High | Yes | 65% of authored | Full | Full |
| Low | Identical to High | **No** | Disabled (≤2) | **Retained** | Full (no reduction) |

### Asset Spec Flag

📌 **Asset Spec** — Visual/Audio requirements are defined. After the art bible is approved, run `/asset-spec system:animation-system` to produce per-asset visual descriptions, dimensions, and generation prompts from this section.

## UI Requirements

The Animation System has no direct UI requirements at MVP scope. It drives bolt and stack visual objects on the game board — not HUD elements, menus, or screen-level UI chrome.

**Downstream UI dependency note:** The In-Game HUD depends on `animation_complete` to re-enable the undo button after MOVE_EXECUTING resolves. The HUD GDD must specify how it subscribes to this signal and what visual state the undo button is in during MOVE_EXECUTING. This is a HUD GDD responsibility, not an Animation System UI requirement.

## Acceptance Criteria

**Key:** BLOCKING = must pass before story is done. ADVISORY = visual/feel evidence (screenshot + lead sign-off).

### Bolt Motion — BLOCKING

**AC-BM-01** — GIVEN a committed bolt move, WHEN `board_state_changed` fires, THEN the bolt at the source slot disappears in the same frame, and the bolt begins a visible upward lift arc over approximately 80ms (±10ms) reaching approximately 1.5× the bolt's own height (default 48px). During the same 80ms, the bolt's glow increases visibly from idle to full intensity.

**AC-BM-02** — GIVEN a committed bolt move, WHEN the bolt travel animation completes, THEN `animation_complete(sequence_id)` is emitted and the Sort Mechanic transitions out of MOVE_EXECUTING within the same frame (new bolt taps become responsive).

**AC-BM-03** — GIVEN a committed bolt move, WHEN the bolt reaches destination slot resting position, THEN `Audio.PlayBoltSettle(isValid: true)` is called at or within 1 frame of the arrive keyframe. The bolt plays a micro-bounce (visibly rises ~6% of its height above the slot and returns). `animation_complete` fires after the micro-bounce completes (approximately 70ms after arrive) — NOT at the arrive keyframe itself.

**AC-BM-04** — GIVEN a committed bolt move on a 5-column board (max_slot_separation = 6), WHEN slot_separation = 4, THEN bolt travel duration is 255–265ms (F-01: ≈ 260ms ±5ms).

**AC-BM-05** — GIVEN a committed bolt move on a 5-column board, WHEN slot_separation = 1, THEN bolt travel duration is 165–175ms (F-01: ≈ 170ms ±5ms).

**AC-BM-06** — GIVEN any board configuration, WHEN slot_separation = max_slot_separation (cross-board maximum), THEN bolt travel duration is exactly `travel_max_ms` (default 300ms ±5ms). Travel never exceeds this ceiling.

**AC-BM-07** — GIVEN a `move_rejected` event, WHEN received in IDLE, THEN the destination stack visual shakes horizontally ±4px for 3 oscillations over approximately 100ms. `Audio.PlayBoltSettle(isValid: false)` fires at shake frame 0. No `animation_complete` is emitted.

**AC-BM-08** — GIVEN a bolt cancel (`move_cancelled`), WHEN received, THEN the bolt travels straight (no arc) back to its source slot over approximately 120ms with ease-in-quint deceleration into the slot (no micro-bounce). A completion signal is emitted on arrival, allowing Sort Mechanic to exit CANCELLATION.

**AC-BM-09** — GIVEN a bolt held in BOLT_SELECTED and the player drops it on an invalid destination, WHEN the drop is rejected, THEN the bolt travels straight back to its held hover position (NOT the source stack) over approximately 80ms. The destination plays a rejection shake (AC-BM-07). No `animation_complete` is emitted. The player can immediately attempt another drop.

### Board Snaps — BLOCKING

**AC-BS-01** — GIVEN a `board_state_changed` event representing an undo, WHEN received in IDLE, THEN all bolt visuals snap to match GSM board state in the same frame. No travel arc plays. No audio call. No `animation_complete` emitted.

**AC-BS-02** — GIVEN a `board_refresh_forced` event arriving during an active bolt travel animation, WHEN the event arrives, THEN the bolt travel coroutine stops immediately, the bolt snaps to its committed destination position (GSM board state), and no `animation_complete` is emitted for the aborted sequence.

### Stack Completion Burst — BLOCKING

**AC-SB-01** — GIVEN a committed bolt move that completes a color stack, WHEN the bolt settle micro-bounce completes, THEN `animation_complete(sequence_id)` is emitted **before** the stack glow pulse begins ramping. A tester who taps a new bolt between the `animation_complete` emission and the glow pulse peak successfully lifts the new bolt — new bolt lift plays while the glow pulse is still visible.

**AC-SB-02** — GIVEN a committed bolt move that does NOT complete a stack, WHEN `animation_complete` fires, THEN no stack glow pulse or VFX burst plays.

**AC-SB-03** — GIVEN `board_refresh_forced` arrives during STACK_BURSTING (glow pulse still ramping), WHEN the event arrives, THEN the glow coroutine stops, all bolt emission resets to idle 0.4, and bolt visuals snap to GSM board state in the same frame. In-flight VFX Graph particle tail is allowed to complete cosmetically. Transition to IDLE.

### Level Complete Celebration — BLOCKING

**AC-LC-01** — GIVEN the Level Complete event fires in IDLE on a 5-stack board with default knobs, WHEN the celebration runs, THEN total input-blocked duration is 1,950–2,050ms (F-03: 2,000ms ±50ms). `animation_complete(sequence_id)` is emitted at the end of the block.

**AC-LC-02** — GIVEN the Level Complete celebration is in progress, WHEN a player taps a bolt or stack, THEN the tap is ignored — no bolt lifts, no visual response.

### Quality Tier — BLOCKING

**AC-QT-01** — GIVEN a new level loads, WHEN `level_loaded` fires, THEN the Animation System queries QTS exactly once and caches the result. A mid-session tier change (simulated by modifying QTS PlayerPrefs and playing a move) does NOT change the active `quality_density_multiplier` until the next `level_loaded`.

**AC-QT-02** — GIVEN Quality Tier = Low (0.25), WHEN a bolt move is committed, THEN the bolt arc plays at full timing (same travel_ms as High tier for identical slot_separation). No micro-bounce plays on bolt settle — the bolt arrives and stops.

**AC-QT-03** — GIVEN Quality Tier = Low, WHEN a stack completion burst fires with `authored_particle_count = 8`, THEN `floor(8 × 0.25) = 2` ≤ disable threshold — no spark particles spawn (F-02 + QT-02). Ring expansion effect plays normally. Glow pulse plays at full timing.

**AC-QT-04** — GIVEN Quality Tier = Medium (0.65), WHEN a stack completion burst fires with `authored_particle_count = 10`, THEN `floor(10 × 0.65) = 6` spark particles spawn (F-02).

**AC-QT-05** — GIVEN Quality Tier = High (1.0), WHEN a stack completion burst fires with `authored_particle_count = 10`, THEN 10 spark particles spawn (F-02).

### Sequence ID Safety — BLOCKING

**AC-SQ-01** — GIVEN an undo fires while a second bolt move is already in MOVE_EXECUTING (stale sequence ID condition), WHEN the stale `animation_complete(sequence_id)` from the first move arrives at Sort Mechanic, THEN Sort Mechanic does not exit MOVE_EXECUTING for the second move. Observable: the second move's bolt continues traveling and completes its own `animation_complete` without interruption. The undo takes effect only after the second `animation_complete` fires.

### Scene Load — BLOCKING

**AC-LL-01** — GIVEN a new level loads, WHEN `level_loaded` fires, THEN all bolt visuals instantiate at correct positions matching GSM board state (zero bolt count mismatch). The active skin's bolt sprites are applied to all bolt visual objects before the first frame is rendered.

### Visual / Feel — ADVISORY (screenshot + lead sign-off)

**AC-VIS-01** — GIVEN a committed bolt move on High quality tier, WHEN the bolt travels, THEN the arc is visually convex — the bolt peak height is above both source and destination slots. The bolt does not travel in a straight line.

**AC-VIS-02** — GIVEN a stack completion burst on High tier, WHEN the burst fires, THEN the ring expansion begins white and transitions to the stack's bolt color before reaching full radius. Tester describes the color sequence from observation (white first, then bolt color).

**AC-VIS-03** — GIVEN the level complete celebration on High tier, WHEN it plays, THEN the cascade wave progresses visibly left-to-right — the leftmost column's glow pulse is visibly ahead of the rightmost column's by at least `(color_count − 1) × cascade_stagger_ms`.

## Open Questions

| # | Question | Owner | Resolution target |
|---|----------|-------|-------------------|
| OQ-01 | **`move_rejected` routing** — Should Animation System subscribe to `move_rejected` on the event bus (Option A), or should Sort Mechanic call the Animation System directly (Option B)? Option A is preferred (maintains event-driven boundary). | Lead Programmer | IGameStateManager interface ADR — before implementation sprint |
| OQ-02 | **Skin System interface contract** — 5 open questions: (1) ScriptableObject vs Addressables? (2) Bolts and nuts skinned together or independently? (3) `skin_changed` event needed for mid-session swap? (4) Canonical color index range and null guarantee? (5) Single sprite vs sprite sheet per bolt color? | Skin System GDD author | Before Skin System GDD is authored — Animation System stories cannot be started until this is resolved |
| OQ-03 | **VFX Graph API exact method name** — `VFXManager.SetGlobalFloat` is the expected API for global VFX property setting in Unity 6.3. Verify exact method name and namespace against Unity 6.3 VFX Graph package docs before implementation. | Unity Specialist | Before Animation System implementation stories begin |
| OQ-04 | **SRP Batcher + MaterialPropertyBlock + Sprite Renderer compatibility in Unity 6.3 URP** — In Unity 2022 LTS, MaterialPropertyBlock broke SRP Batcher for Mesh Renderers but was compatible with Sprite Renderers. Confirm this still holds in Unity 6.3 before implementing the glow system. | Unity Specialist / Technical Artist | Before glow system implementation |
| OQ-05 | **DOTween adoption decision** — If the team wants to use DOTween for bolt motion coroutines (instead of native AnimationCurve), it must be added to the Allowed Libraries list and documented in an ADR. Without this decision, implementation must use coroutine + AnimationCurve. | Lead Programmer | Before implementation sprint begins |
| OQ-06 | **Level complete on 2-stack board** — F-03 produces ≈2,000ms on a 2-stack board (board_ring_ms=600ms dominates cascade_wave_ms=260ms). A 2-stack level-complete feels duration-identical to an 8-stack clear. Is that the intent? If not, the parallel-phase design needs a color_count-aware minimum board ring duration. | Game Designer | Before level editor produces any 2-color levels |
| OQ-07 | **Particle disable threshold precision** — QT-02 defines the disable threshold as "`live_particle_count ≤ 2`." For authored counts of 4–7, this threshold may produce a 1-particle artifact rather than a clean disable at Low tier. Consider raising the threshold to `< floor(authored × 0.30)` or setting a minimum authored count of 12 for all VFX assets. | Art Director + QA Lead | Before first VFX asset is authored |
| OQ-08 | **Sort Mechanic GDD tuning knob update** — The "animation duration — bolt move: 180ms (80–300ms)" knob in Sort Mechanic GDD must be replaced with `travel_min_ms` and `travel_max_ms` before implementation stories are written for either system. | Game Designer | Before implementation sprint planning |
| OQ-09 | **Level complete celebration Low-tier device profiling** — The level complete celebration animates all bolts simultaneously. The technical artist flagged this as requiring a profiling pass on a Low-tier device before confirming it is feasible without exceeding the 4ms animation budget. | Technical Artist + Unity Specialist | Before Beta milestone (not blocking MVP) |
| OQ-10 | **`level_loaded` event payload `color_count`** — Must include `color_count` for Audio System F-03 (ambient arc calibration) per the existing cross-GDD open question. This was not the Animation System's responsibility to define, but the Animation System uses `color_count` from `level_loaded` for F-01 and F-03. Confirm the payload is locked before any implementation begins. | GSM GDD author | Resolve in GSM GDD or interface ADR |
