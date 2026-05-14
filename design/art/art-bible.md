# BoltSort — Art Bible

> **Status**: Complete — Vibrant Precision Machine direction (revised 2026-05-11)
> **Author**: Art Director session (2026-05-10, revised 2026-05-11)
> **Engine**: Unity 6.3 LTS — URP 2D Renderer
> **Platform**: iOS & Android
> **Art Director Sign-Off (AD-ART-BIBLE)**: Pending

---

## 1. Visual Identity Statement

> **Authority note**: This section is the canonical source for visual direction. The Animation System GDD, UI system GDDs, and all asset specifications defer to the values stated here. When a downstream GDD conflicts with this section, this section wins.

### One-Line Visual Rule

**Every bolt sings its color — the machine runs hot with purpose.**

Color is not data decoration. Color is the machine's primary language: bold, saturated, legible from across the room, joyful rather than merely precise. The machine is alive because it is *full of color* — not faintly lit but visibly charged. The absence of color signals the absence of life. This rule governs all conditions: resting, active, completing, failing, winning. Glow amplifies color; it does not replace it.

### Supporting Principles

---

**P1 — Saturated bolts against a dark field: the contrast is where the game lives.**

The background remains deep near-black (`#0B0F14`). The bolt palette shifts from plausible machine hues toward fully saturated jewel tones — cobalt, scarlet, emerald, amber, violet, ice — each at maximum chroma before luminance differentiation breaks down. The dark field is not oppressive; it is a stage. Saturated bolts on a near-black ground produce the highest possible figure-ground contrast without competing backgrounds (Royal Match principle applied to a machine world rather than a castle). The warmth of the game comes entirely from bolt color saturation, not from background warmth.

*Design test*: When evaluating a bolt color, the question is never "is this color accurate to a real bolt?" The question is: "Does this color read instantly at 44px, and does it feel energized rather than muted?" If a swatch needs to be brighter to feel alive, make it brighter. Desaturated or dark bolt colors are palette drift — pull toward vivid.

*Pillar*: Flow Over Friction — high saturation + dark ground reduces the effort of color identification to zero. The eye does not parse; it knows.

---

**P2 — Glow is the amplifier, color is the signal — never reverse this.**

The glow lifecycle (0.4 resting → 1.0 active → 0.4 settling) now serves a different master. Glow does not define the bolt's visual identity; it modulates the bolt's color identity. At idle, the bolt's hue sings at 70% of its maximum intensity. At active (held or in motion), glow pushes that hue toward its most saturated, brightest expression — the color feels charged, not merely lit. At disabled, the bolt's color is suppressed to approximately 25% luminance with near-zero glow — present but cooling.

| Moment | Color expression | Emission |
|--------|-----------------|----------|
| Resting (idle) | Full bolt hue, bold | 0.4 |
| Lift begins | Hue brightens toward maximum saturation | ramps to 1.0 over 80ms |
| In travel | Maximum saturation, charged | holds 1.0 |
| Settle | Returns to full bold idle hue | returns to 0.4 over 200ms |
| Stack completion burst | White → resolves to bolt's jewel hue | 400ms |
| Disabled state | Hue at ~25% luminance, cold | 0.0 |

*Design test*: On a resting board (no tap in progress), cover the screen with a filter that removes all glow. If the board still reads as colorful and legible, the bolt palette is correct. If the board feels grey or cold without glow, the palette is under-saturated — bolts are depending on glow for their color energy, which is the wrong dependency chain.

*Pillar*: The Machine Must Sing — glow is the machine's *emphasis*, not its *voice*. The voice is color. A machine that only sings when active is a machine with poor vocal health. The colors must hold their presence at every moment.

---

**P3 — Rounded geometry signals intelligence; sharp corners signal structure.**

All panels, slots, buttons, and bolt shapes use rounded corners. The machine is advanced enough to have solved right angles.

| Element type | Corner radius |
|---|---|
| Panels, cards, overlays | 12dp |
| Buttons, chips | 8dp |
| Circular elements (coins, indicators) | 50% |
| Structural dividers | 0dp (hard corner permitted — load-bearing only) |

Hard corners are permitted only on structural dividers where they carry load-bearing spatial meaning.

*Design test*: If a shape has a sharp corner, ask: "Is this edge doing structural work — dividing regions, aligning systems?" If yes, the corner is permitted. If the shape is a button, slot, bolt, panel, or any interactive surface, the corner must be rounded. A sharp-cornered button is not a decision — it is a drift.

*Pillar*: Every Pixel Earns Its Place — every sharp corner must justify itself. Rounded geometry is the default because it maintains the aesthetic contract at zero additional cost.

---

**P4 — The machine confirms in white; it celebrates in color.**

When a stack completes: white burst at origin → expands → resolves to the bolt's jewel hue as it fades. When the level completes: board ring fires white → resolves to the dominant bolt color.

The sequence is invariant: **machine confirms (white) → player's work expressed in their color (jewel hue).**

Under the new direction, the bolt-color resolution phase carries more visual weight — because the bolt colors are now vivid, the celebration landing is noticeably richer. The white burst does not compete with this; it precedes and earns it. A pulse that skips white and opens directly in jewel color reads as a paint splash, not a machine acknowledgment.

*Design test*: The first frame of any completion, resolution, or confirmation event must be white. If any test build ships with colored first-frames, the sequence has been accidentally inverted — the machine is celebrating before it has confirmed.

*Pillar*: The Machine Must Sing — the rhythm is: machine speaks first (white), then hands the floor to the player's color (jewel hue). Precision and joy expressed as a single sequence.

---

**P5 — The board is the world; everything else is the frame.**

The game board — stacks, bolts, slots — occupies the visual center of gravity. All HUD chrome lives at the periphery. Under the vibrant direction, this principle requires *active defense*: vivid bolt colors will naturally draw the eye, and the temptation is to match that energy in the HUD. Resist. The HUD must remain the cool, instrument-grade frame around a hot, colorful world.

| Element class | Color expression | Priority |
|---|---|---|
| Active bolts (held or in motion) | Maximum jewel saturation | 1st |
| Board (stacks, slots, resting bolts) | Bold jewel hues at idle | 2nd |
| HUD chrome | Cool grey-blue palette, instrument-grade | 3rd |

*Design test*: On any in-game screenshot, apply a saturation-reduction filter until one region of the screen retains its color first. That region must be the board. If the HUD is the most saturated region of any in-game screenshot, it is too vibrant — dial it back to instrument grade.

*Pillar*: Flow Over Friction — the saturation gradient from board to HUD enforces visual hierarchy without any explicit indicator.

---

### Anti-Direction

BoltSort is not dark cyberpunk with saturated neon-on-black (the Tron problem: colored glows against void produce a nightclub, not a machine). It is not a generic flat-color mobile puzzle toy where bolts are filled shapes with no surface quality and backgrounds are pastel gradients from the default Unity palette. It is not a Royal Match or Candy Crush clone dressed in bolts: it has no mascots, no faces, no organic shapes, no level select map with characters walking across it, no gem or jewel metaphors, no fantasy theming of any kind.

Most specifically: it is not a game where the visual richness is delivered through busy backgrounds, character expressiveness, or interface decoration. BoltSort's visual richness comes from one source — the quality and saturation of the bolt objects themselves, set against a dark, precisely structured field. The background remains controlled and near-black. The HUD remains instrument-grade. The only entities with permission to be vivid are the bolts.

This game occupies the specific space between cold dark-sci-fi (grey or dark-gradient sort game UIs) and warm-toy cartoon (bubbly watercolor sort games): a machine world with jewel-tone color fidelity, where precision and joy are not opposites but the same quality expressed at different frequencies.

An outsourcer who asks "should this element be bright and saturated?" has one answer: if it is a bolt, yes. If it is anything else, probably not — default to the cool-chrome palette and let the bolts carry the color energy.

---

## 2. Mood & Atmosphere

### Reading Guide

Each state defines: **Primary Emotion / Lighting Character / Atmospheric Descriptors / Energy Level / Visual Shift from previous state.** States are ordered chronologically through a typical session. "Lighting character" gives color temperature direction and relative brightness — hex values are in Section 4. All brightness references use the emission scale defined in Section 1 (P2 glow lifecycle table).

---

### State 1 — Level Active: Early (Board Is Chaotic)

**Primary emotion:** Charged readiness

**Lighting character:** Even, full, alive from the first frame. No spotlight logic — every bolt on the board presents its jewel hue at resting 0.4 simultaneously, an array of color waiting to be resolved. Board emission is uniform because the player has not yet committed to a strategy — the machine is loaded and ready. Background emission is at its most neutral (pure instrument-panel dark), which sets the jewel colors in maximum relief.

**Atmospheric descriptors:** Vivid, open, charged, poised, populated

**Energy level:** Attentive

**Visual shift from previous state:** Entry into the Early state resets to uniform color presence — board glow drops to a consistent 0.4 across all stacks, HUD dims to floor opacity. The contrast with the previous screen's resolved glow is intentional: the board has been repopulated with color and the machine is waiting for the player's first move.

---

### State 2 — Level Active: Mid (Partial Progress)

**Primary emotion:** Focused momentum

**Lighting character:** The board's color begins to differentiate. Cleared slot gaps expose the dark background — negative space opens as a progress indicator, making remaining bolt color read more vividly by contrast. Correctly stacked bolts hold fractionally above resting 0.4 — a quiet acknowledgment of progress. Internal board differentiation sharpens: resolved columns (settling glow) versus unsorted columns (uniform resting 0.4). The board is not dimming as it empties — it is concentrating.

**Atmospheric descriptors:** Purposeful, rhythmic, crystallizing, focused, building

**Energy level:** Measured

**Visual shift from previous state:** The board begins to partition itself. Resolved areas carry a faint glow step-up; unsolved areas remain at resting 0.4. Color differentiation across columns is the progress signal — no screen-level brightness change, only structural change within the board.

---

### State 3 — Level Active: Late (Almost Solved)

**Primary emotion:** Precise execution

**Lighting character:** The board is mostly resolved. Dark negative space is dominant, and the remaining unsorted bolts read with heightened presence — their jewel hues are more vivid for having fewer neighbors. The last bolts on the board carry outsized visual gravity. Screen-level brightness feels slightly elevated compared to State 2, not from added emission but from reduced competing mass: fewer bolts means each one has more room to sing.

**Atmospheric descriptors:** Inevitable, exact, taut, crystalline, convergent

**Energy level:** Accelerating

**Visual shift from previous state:** The board's visual density drops. Where State 2 felt charged with color possibility, State 3 feels geometrically spare. The remaining bolt colors are more individually legible, each hue isolated against the dark field. The machine is almost done; the final colors are the most precisely read objects on screen.

---

### State 4 — Stack Completing (Micro-Satisfaction Beat)

**Primary emotion:** Crisp reward

**Lighting character:** Highly local. The completing stack is the only element that changes: white pulse at column origin, expands radially, resolves to the bolt's jewel hue over 400ms — the jewel color landing is visually rich now that bolt saturation is at maximum. All other columns remain at current glow state, undisturbed. The pulse is the brightest single event during active play. Contrast is high but spatially contained.

**Atmospheric descriptors:** Percussive, precise, resonant, confirming, vivid

**Energy level:** Released

**Visual shift from previous state:** Everything holds the taut execution mode of State 3 and then one column fires — white first, then the bolt's full jewel hue blooms outward and settles. The spatial isolation of the pulse is what makes it satisfying: one column answered, the rest still waiting. Glow differential between completing (settling above 0.4) and incomplete (resting 0.4) sharpens.

---

### State 5 — Puzzle Solved (Win Transition)

**Primary emotion:** Machine restored

**Lighting character:** The one moment where screen-level emission climbs. Board ring fires white first — full board acknowledgment — then all columns simultaneously pulse white and resolve to their respective bolt colors over ~600ms. Because the bolt palette is fully saturated jewel tone, the resolution phase is visually rich: the board floods with color all at once, every hue at its most vivid. Background holds dark. After resolve, the board settles into fully-illuminated calm — every stack at a slightly elevated glow above resting 0.4, the machine humming at peak health.

**Atmospheric descriptors:** Resonant, luminous, whole, earned, vivid

**Energy level:** Released

**Visual shift from previous state:** States 3/4 were spare and concentrated. The win transition fills the board — saturated color returns to every column simultaneously. The move from sparse (last bolts settling) to fully populated (all stacks complete, all jewel hues at once) is the core visual beat of the win moment. A held breath released into color. No confetti. No explosion. The machine completes itself in its own colors.

---

### State 6 — Level Complete Screen

**Primary emotion:** Accomplishment, forward pull

**Lighting character:** The first screen where something other than the board is visually primary. Star ratings, move count, and next-level prompt are the new focal points. The board recedes — glow drops toward background. The Level Complete UI uses the cool chrome palette but allows slightly elevated panel emission compared to HUD chrome during play. The "next level" prompt carries the highest emission on screen — forward momentum.

**Coin reward animation:** The coin counter animation fires in amber (`#E8A030`). This is not an exception to the jewel-tone energy — it is jewel-tone energy expressed in the HUD's only sanctioned warm accent. Amber here carries the same saturated-against-dark charge as the bolt colors carry on the board: vivid, momentary, legible. It reads as reward because it is the warm sibling of the bolt palette, not because it contrasts with the board. Amber appears only on the animated element, never as a fill color on structural UI. Resolved in `≤ 1.5s`.

**Atmospheric descriptors:** Accomplished, forward-looking, expansive, rewarding, warm

**Energy level:** Ambient

**Visual shift from previous state:** The board's visual dominance ends. For the first time in the session, a UI element is permitted to be the brightest thing on screen. The board's jewel-tone energy hands off to the reward moment — the player has earned this brief shift in visual hierarchy.

---

### State 7 — Main Menu / Idle

**Primary emotion:** Patient readiness

**Lighting character:** Darkest overall state in terms of active visual event — but not lifeless. Board preview elements (if shown) glow at idle 0.4, jewel hues present and held. The machine is at rest, not off. Idle glow cycle: very slow sine on emission, subliminal rather than animated. Period ~4 seconds, amplitude 0.35–0.45 (never crossing 0.5). Primary CTA (Play / Resume) holds glow at 0.8 — below the 1.0 ceiling reserved for held/active states, but distinctly above ambient. A machine that is idle is still colorful; colors are present at resting tone, not suppressed.

**Atmospheric descriptors:** Still, breathing, patient, luminous at rest, watchful

**Energy level:** Ambient

**Visual shift from previous state:** If arriving from Level Complete, the screen contracts — the forward-facing warmth of the reward moment gives way to a quieter chromatic presence that signals a distinct mode boundary. If arriving cold (first launch), this is the first impression: a machine alive with color that is not yet demanding anything.

---

### State 8 — Deadlock Detected

**Primary emotion:** Deliberate pause

**Lighting character:** Glow suppression, not desaturation (no extra draw call — aligns with ADR-0005 mobile performance budget). Board glow steps down from 0.4 → 0.3 on all columns simultaneously. Against fully saturated jewel-tone bolt colors, this step-down is immediately perceptible — the board dims as a unit, colors cooling one register. The effect is more visible now than under a muted palette: vivid hues stepping back carry clear weight. Undo prompt or path-retrace indicator glows at 0.7. Background unchanged. No pulse event — the absence of event is the signal.

**Atmospheric descriptors:** Deliberate, weighted, held, inquiring, quietly vivid

**Energy level:** Contemplative

**Visual shift from previous state:** Mid-game the player has been moving (measured-to-accelerating energy). The step-down in board glow is the only change — no alarm, no panic UI. The jewel colors are still present; they have simply stepped back one register to communicate that the machine has paused and is waiting. Against the saturated palette, this restraint reads loudly. The machine has detected something and holds its color steady while indicating a step backward is available — a hand on the shoulder, not a buzzer.

---

### Cross-State Continuity Rules

These rules apply across all 8 states:

1. **Background never changes brightness.** Mood shifts happen through board emission changes and UI visibility adjustments, never by lightening or darkening the background field. Background is fixed at near-black.
2. **HUD opacity floor is always maintained during play states (1–4).** HUD elements dim but never disappear. If the player glances up, instrument readings must be available.
3. **Jewel saturation belongs to bolts only; chrome stays cool.** No state (including celebration) uses jewel-tone or warm fills on structural UI. The State 6 coin animation amber is a momentary animated accent, not a structural fill — it is the one sanctioned warm event in the chrome palette.
4. **Glow ceiling (1.0) is reserved for held/active states and completion pulses.** Ambient maximum is 0.8 (State 7 CTA). No ambient state may reach 1.0.
5. **State transitions have audio analogs.** If glow settles over 200ms, the sound envelope closes over the same window. *(Coordination flag for Audio System GDD — not a visual specification.)*

---

## 3. Shape Language

### 3.1 Bolt Silhouette Philosophy

**The canonical bolt shape is a filled circle.**

Top-down cross-section of a bolt head viewed from directly above. Three reasons grounded in visual communication:

1. **Gestalt Simplicity (Pragnanz):** The eye resolves to the simplest closed form. A circle communicates "this is the game object" faster than any other geometry at 44px.
2. **Color region maximization:** A circle maximizes the filled color area within a given bounding box. The bolt's color IS the gameplay information — the shape must serve the color, not compete with it.
3. **Rotation invariance:** A circle carries identical meaning at any device orientation. Hexagons tilted 15° read differently from hexagons at 0°. Circles do not drift.

**Canonical bolt dimensions:**

| Property | Value | Rationale |
|---|---|---|
| Shape | Filled circle | Maximum color region, rotation-invariant, simplest form |
| Diameter | 80% of slot width | Leaves 10% clearance on each side for glow halo to breathe |
| Glow halo | 10–12% of bolt diameter radial margin | Halo occupies the 10% clearance zone; does not extend beyond slot boundary |
| Minimum rendered size | 44px diameter | iOS HIG minimum tap target; minimum legible color-coded circle |
| Corner radius | 50% (true circle) | P3 — circular elements use 50% radius |
| Inner detail | Concentric ring at 60% diameter, 2px stroke, same color at 40% opacity | Subtle depth cue that keeps the bolt from reading as a flat sticker; disappears at thumbnail size (intentional) |

**The circle is the reserved silhouette for gameplay objects.** No skin variant may change this to a non-circular form.

**Color-blind shape variant system (accessibility skin layer):**

When the accessibility preference is enabled, a micro-icon is cut into the bolt's surface. Patterns are centered, same color as the bolt at 40% opacity, visible at 44px:

| Bolt color | Backup pattern |
|---|---|
| Cobalt | Hexagonal recess |
| Scarlet | Cross-slot recess |
| Cobalt-Green | Triangular recess |
| Amber-Orange | Diamond recess |
| Violet | Circle-dot recess |
| Ice | Star recess |

---

### 3.2 Stack and Slot Geometry

**The stack is not a container — it is a column implied by vertical spacing.**

No walls, no tubes, no bounding boxes. The stack is defined by the alignment of bolts in vertical sequence and ghost slot outlines. Explicit containers would fill the negative space between columns, destroying the visual gap the player reads to plan. They would also make BoltSort look like Ball Sort Puzzle — tubes and vessels. BoltSort's stacks are columns in a machine.

**Slot geometry:**

| Property | Value |
|---|---|
| Slot shape | Rounded rectangle, corner radius 8dp (button tier, P3) |
| Slot size | 100% of bolt diameter + 20% padding (10% each side) |
| Slot visual treatment | Stroke only, 1.5px, at 30% opacity of primary interactive color |
| Empty slot fill | None — background shows through |
| Occupied slot | Bolt covers slot area; glow halo defines boundary |

**Vertical rhythm:**

| Property | Value |
|---|---|
| Slot height | Equal to slot width (square grid cell; bolt is circular within) |
| Slot vertical gap | 4dp between slot boundaries |
| Bolt diameter | Fixed — does not change with stack depth |
| Column height growth | Slot height × stack_depth + (4dp × (stack_depth − 1)) |

As stack depth increases (3→8 bolts), the column grows taller, not wider. Bolt size is a fixed constant; column count and depth are variable per level.

---

### 3.3 UI Shape Grammar

**Definitive ruling: The UI is a clean HUD language that contrasts with the board.**

The UI does not echo the mechanical world with hexagonal elements, gear-tooth edges, or decorative chrome.

Reasoning: The HUD must stay shape-simple so the bolt colors dominate visually. BoltSort's visual hierarchy depends on one rule — saturated, filled circles on screen are gameplay objects; everything else is infrastructure. If the HUD adopted curved or circular elements, it would compete with the board for figure-ground attention. The eye would be asked to process two overlapping shape vocabularies simultaneously and would lose the rapid color parsing that "Flow Over Friction" requires. A shape-simple HUD is not a compromise — it is what makes the bolt colors legible across the full screen.

**HUD shape specifications:**

| Element | Shape | Corner radius | Treatment |
|---|---|---|---|
| HUD panel/bar | Rounded rectangle | 12dp (panel tier, P3) | Stroke only or low-opacity fill ≤15% |
| Buttons (undo, hint) | Rounded rectangle | 8dp (button tier, P3) | Stroke + glow state per P2 |
| Coin balance chip | Rounded rectangle | 8dp | Icon left, number right |
| Move counter | Text only, no bounding box | N/A | Monospaced numeral |
| Modal overlays | Rounded rectangle | 12dp (card tier, P3) | Standard overlay treatment |
| Structural dividers | Hard edge | 0dp | Load-bearing spatial signal only |

No gear teeth. No hexagonal cells. No mechanical engravings. The HUD is glass, not metal.

---

### 3.4 Negative Space

**Negative space is active — it is the gap the player reads to plan.**

The dark near-black background is not empty. It is the primary visual channel through which the player reads column separation and board progress. As bolts are cleared, negative space grows — it is the visual representation of work done.

**Spacing ratios:**

| Property | Value | Rationale |
|---|---|---|
| Column gap (edge to edge) | 1 bolt diameter (100% of bolt width) | Prevents glow halo bleed; readable as distinct columns at max zoom-out |
| Minimum column gap | 0.75 bolt diameters | Hard minimum — below this, halos merge and columns read as a single mass |
| Board horizontal margin | 0.5 bolt diameters from screen edge | Consistent breathing room |
| Board vertical margin (board-to-HUD) | 16dp minimum | Structural divider zone; no bolt glow may overlap HUD |

**8-column layout flag (owned here — referenced by Section 7):**

At maximum board width (8 columns) with 1-diameter gaps, the board spans 16 bolt diameters. On a 390pt iPhone screen, this places bolt diameter at approximately 24pt — below the 44pt tap target minimum. The shape spec holds (80% slot, 1-diameter gap); the layout system must enforce a minimum 44pt bolt diameter by capping columns and using a scroll or layout fallback. This is a coordination flag for the unity-ui-specialist — Section 7 (UI/HUD) governs the fallback behavior.

---

### 3.5 Shape Hierarchy

| Tier | Shape | Visual weight mechanism |
|---|---|---|
| 1st — Dominant | Bolt (filled circle, 80% slot width) | The only saturated, filled circle on screen; jewel color at full chroma; emission 0.4–1.0 reinforces this dominance |
| 2nd — Secondary | Stack (implied column of slot outlines) | Stroke-only; lower opacity; structure without mass |
| 3rd — Tertiary | HUD elements | Lowest emission; lowest opacity; rectilinear vs. circular — shape contrast reinforces hierarchy |

**Gestalt principles at work:**
- **Similarity:** All bolts are circles. All HUD elements are rounded rectangles. Shape category immediately sorts gameplay from chrome.
- **Proximity:** Bolts in a column (4dp gap) read as a unit. Columns (1-diameter gap) read as distinct units.
- **Figure-ground:** Dark background = ground. Circular saturated bolts = figure. HUD at lower opacity = infrastructure between figure and ground.

**Eye order on a mid-game board:**
1. Held bolt (emission 1.0, raised above stack)
2. Other active-stack bolts (emission 0.4, circular saturated mass — the only filled color regions on screen)
3. Empty slot outlines (stroke only — potential)
4. HUD elements (lowest emission, rectilinear, peripheral)

Bolts dominate because they are the only saturated, filled circles on screen. Glow reinforces this read — it does not create it. This order must not be violated by any skin, animation, or UI addition.

---

### 3.6 Shape Rules for Skin Design

**What a skin may change:**

| Property | Constraint |
|---|---|
| Bolt color | Must remain distinguishable from all other bolt colors; 3:1 contrast against `#0B0F14` background |
| Inner detail mark | Must be one of the four approved variants (ring, dot, cross, triangle) |
| Glow color | Within ±30° hue rotation of the bolt color — warmth is permitted where the bolt hue supports it |
| Surface texture (shader) | Implemented by Technical Artist; must not increase draw calls beyond ADR-0005 budget; must not reduce color legibility |
| Slot stroke style | Rounded rectangle always; stroke weight 1–2.5px; low-opacity stroke color only |

**What a skin must never change:**

| Property | Why |
|---|---|
| Bolt silhouette — always filled circle | Color-blind variants depend on circle baseline; tap target geometry is fixed |
| Bolt diameter — always 80% of slot width | Spacing ratios and glow clearance are calibrated to this |
| Stack implied structure — no walls, no tubes | Stack = column; this is identity, not aesthetics |
| Inner mark opacity — always 40% of bolt color | High-contrast marks read as a second color, corrupting color-sort signal |
| Glow halo shape — always radial | Directional halos violate P3 and break the glow lifecycle |
| HUD shape language — never adopted into bolt skins | HUD grammar is instrument grammar; they must remain visually distinct |

**Skin gate check (Art Director review required before production):**
1. Bolt reads as circle at 44px rendered size
2. Bolt color passes 3:1 contrast against `#0D1117`
3. Inner mark is invisible at 44px
4. Glow color within ±30° hue rotation of bolt color — warmth is permitted where the bolt hue supports it
5. In a 6-color test board, no two colors are perceptually identical

---

## 4. Color System

> **Authority note**: This section defines canonical color values for BoltSort. All downstream systems — shader parameters, Unity material properties, UI sprites, VFX Graph gradient curves — derive from the values here. When a shader spec, UI mockup, or art asset conflicts with this section, this section wins. The Technical Artist translates hex values into URP 2D Renderer material properties.

---

### 4.1 UI Chrome Palette

"Chrome" = every non-bolt surface: backgrounds, panels, buttons, HUD elements, labels, dividers, overlays. **The chrome palette never appears on a bolt.** Chrome stays cool so bolt colors dominate — the chromatic energy of the game lives entirely in the bolt palette.

**CHROME-01 — Background `#0B0F14`**
Near-black desaturated navy. Role: screen background, camera clear color. Pure black (`#000000`) is forbidden — reads as hardware void on OLED. Warm dark grays are forbidden — read as "unlit room." `#0B0F14` reads as cold, powered machine. Never use as panel fill — panels must step up from this value.

**CHROME-02 — Surface / Panel `#141C24`**
Deep blue-gray. Role: modal backgrounds, HUD area fills, card surfaces. Distinguishable from Background without competing with bolt colors. Use at 100% opacity for modals; 60–70% opacity for in-play HUD chips (per P5 emission hierarchy).

**CHROME-03 — Primary Interactive `#4DCFEF`**
Cool cyan. Role: all interactive buttons, active borders, selection rings, CTA labels. The one color that communicates "you can touch this." Under bloom: shifts toward whitened halo — correct behavior. Minimum 8dp clearance from nearest bolt edge to any chrome interactive element. Use only on interactive elements with a real tap affordance.

**CHROME-04 — Text / Label `#C8D8E8`**
Cold light blue-white. Role: all UI text, numerals, labels. Pure white (`#FFFFFF`) is forbidden for persistent text — it competes with the P4 completion pulse. `#C8D8E8` achieves ~13:1 contrast against Background, ~11:1 against Surface. Minimum text size: 14sp.

**CHROME-05 — Accent Amber `#E8A030`**
Warm amber. Role: coin reward animation in State 6 only. The warm accent that reads as reward because it shares energy with the bolt palette — vivid, momentary, legible against the dark field. Enforcement: animated element only, duration ≤1.5s, never as fill on structural UI, never during play states 1–4. Pity grant glint uses dimmed derivative `#B87820` — "assistance available" signal, not reward.

---

### 4.2 Bolt Color Set — 6-Color Canonical

Progressive introduction: Level 1 (Cobalt + Scarlet) → Level 2 (+ Cobalt-Green) → Level 3 (+ Amber-Orange) → Level 4 (+ Violet) → Level 5 (+ Ice) → Level 6 (all 6).

Introduction order maximizes hue distance between paired introductions. Cobalt and Scarlet are the safest colorblind pair for a two-color start.

| Bolt | Hex | Name | Intro level | Colorblind risk |
|---|---|---|---|---|
| BOLT-01 | `#2A72E8` | Cobalt | Level 1 | Low — blue is safest across all major types |
| BOLT-02 | `#E83030` | Scarlet | Level 1 | High pair risk with Amber-Orange (deuteranopia/protanopia) |
| BOLT-03 | `#28C864` | Cobalt-Green | Level 2 | High risk with Scarlet (deuteranopia/protanopia) |
| BOLT-04 | `#E87820` | Amber-Orange | Level 3 | Moderate; level constraint with Scarlet (see 4.4) |
| BOLT-05 | `#8030D8` | Violet | Level 4 | Moderate; higher saturation compensates for bloom desaturation; luminance darker than Cobalt |
| BOLT-06 | `#78D8F0` | Ice | Level 5 | Low; most bloom-active — approaches near-white at full emission |

**Level design constraint (BOLT-02 + BOLT-04):** Scarlet and Amber-Orange may not be the only two active colors in any level. When both are present (Level 3+), a third active color is always required.

**Hex change rationale:**

*BOLT-05 Violet `#9040D0` → `#8030D8`:* The original hex sits at HSL ~274°, 54% saturation — a muted, dusky purple that reads as insufficient against a high-chroma board. Under bloom it desaturates further, compounding the problem. The revised value moves saturation to ~75% and shifts the hue to 272° (pure amethyst territory), producing a vivid purple that holds its chroma identity at both idle and full-bloom emission. Luminance remains darker than Cobalt — bloom halos will still differ visibly in intensity. The hue shift of 2° is negligible for colorblind differentiation from Cobalt; the luminance gap remains the primary separation mechanism.

---

### 4.3 Semantic Color Rules

**White — Machine Confirmation only.** Appears in exactly two contexts: (1) first frame of any stack/level completion pulse, (2) board ring burst at level complete. White is the machine's confirmation signal — the system received your input and it is correct. White outside a completion pulse is a broken visual language.

**Cyan (`#4DCFEF`) — Interactable / Available.** The cool-chrome interactive signal that anchors the HUD palette. Every interactive surface at rest glows cyan at emission 0.4. Tap ramps to 1.0 — cyan intensifies, does not change color. Cyan on a surface with no tap affordance is a visual lie.

**Amber (`#E8A030`) — Exceptional Momentary Reward.** The warm sibling of the jewel palette — appearing at Level Complete as the HUD's one moment of jewel-energy. Appears only on the coin counter animation in State 6. Players learn the association by contrast — the only warm event in the session. Pity grant glint (`#B87820`) = "assistance available," lower energy than reward.

**Suppressed State.** Any color at emission 0.0 + material pushed to ~40% luminance. Communicates "exists but cannot be interacted with right now." Never hide an inactive element (opacity 0%) — hiding removes spatial information. Suppression communicates "present but inactive."

**No Danger / Warning color.** BoltSort has no fail state, no timer, no health bar. Deadlock is signaled by board-wide glow step-down (0.4→0.3) — absence of event, not added color. No red in chrome: red is BOLT-02 (Scarlet). Red in chrome would contaminate bolt-data semantic.

---

### 4.4 Colorblind Safety Summary

**Backup pattern system:** Off by default, player preference toggle. When enabled, each bolt renders a micro-icon cut pattern on its surface (normal map/mask texture channel, no additional draw calls). Patterns are centered, 40% bolt diameter, same color at 40% opacity, visible at 44px.

| Bolt | Pattern | Asset name |
|---|---|---|
| Cobalt | Hexagonal recess | `bolt_cobalt_pattern_normal.png` |
| Scarlet | Cross-slot recess | `bolt_scarlet_pattern_normal.png` |
| Cobalt-Green | Triangular recess | `bolt_green_pattern_normal.png` |
| Amber-Orange | Diamond recess | `bolt_amber_pattern_normal.png` |
| Violet | Circle-dot recess | `bolt_violet_pattern_normal.png` |
| Ice | Star recess | `bolt_ice_pattern_normal.png` |

Resolution: 256×256px (High/Medium tier), 128×128px (Low tier).

**Deuteranopia / Protanopia (red-green confusion, ~8% of males):**

| Pair | Risk | Mitigation |
|---|---|---|
| Scarlet + Cobalt-Green | HIGH | Both require backup cues (cross-slot + triangle). Luminance differential provides partial passive cue. |
| Scarlet + Amber-Orange | HIGH | Level design constraint (never 2-color-only). Both require backup cues when co-present. |
| Cobalt-Green + Amber-Orange | MODERATE | Diamond (Amber-Orange) backup recommended |
| All other pairs | LOW | No additional cue required |

**Tritanopia (blue-yellow confusion):**

| Pair | Risk | Mitigation |
|---|---|---|
| Cobalt + Cobalt-Green | MODERATE | Luminance differential (Green brighter). Triangle + hexagon backup recommended. |
| Cobalt + Violet | LOW-MODERATE | Luminance differential maintained — Cobalt (`#2A72E8`) is lighter than revised Violet (`#8030D8`). Circle-dot backup recommended. |
| Ice + Cobalt | LOW | Luminance differential large (Ice substantially brighter). Monitor in playtest. |

---

### 4.5 Bloom Behavior Per Bolt Color

Bloom processes before tonemapping in Unity 6.3 URP (per ADR-0005). Sprites above 1.0 HDR trigger bloom. Per-instance emission via `VisualEffect.SetFloat` (per ADR-0010). Values below describe directional behavior under the global bloom system — not independent per-color settings.

| Bolt | Bloom direction | Key risk | Mitigation |
|---|---|---|---|
| Cobalt | Shifts ~10° toward cyan at 1.0; brightens | Converges toward Ice in hue | Ice is luminance-brighter — distinguishable by halo intensity |
| Scarlet | Saturates warm (red intensifies); at over-boosted bloom shifts toward orange | Converges toward Amber-Orange | Calibration-critical — see checklist below |
| Cobalt-Green | Shifts toward cyan-green; moves away from warm siblings | May approach Ice at extreme bloom | Monitor at max emission |
| Amber-Orange | Shifts toward yellow-orange; diverges from Scarlet under bloom | None when bloom correctly calibrated | Works in favor of separation |
| Violet | Revised `#8030D8` starts at 75% saturation. Under bloom it desaturates toward blue-purple as before, but now settles at a visible amethyst rather than near-grey. Halo brightness remains visibly dimmer than Cobalt's halo due to lower luminance. | Risk of approaching Cobalt territory reduced vs. original but not eliminated | Luminance differential + higher saturation starting point keep them distinct. Bloom calibration check #2 remains required. |
| Ice | Halo approaches near-white at 1.0; most bloom-active color | Near-white halo resembles P4 completion pulse | Spatial distinction: pulse expands radially from stack; Ice halo is object-space |

**Bloom Calibration Checklist (Technical Artist — verify on physical device):**
1. Scarlet vs. Amber-Orange at full emission — must read as distinct hues
2. Cobalt vs. Violet — must show luminance differential in halo brightness
3. Cobalt vs. Ice — must show hue and luminance differential (Ice brighter, whiter)
4. Cobalt-Green vs. Ice — must show hue differential (green vs. cyan-white)
5. All 6 bolts side-by-side under deuteranopia simulation filter at max emission — all 6 identifiable

Calibration target: all 5 checks pass without backup patterns enabled. Backup patterns are the accessibility layer, not the resolution for bloom convergence failures.

---

## 5. Component Design Direction

*BoltSort has no characters. The bolt is the protagonist — the primary designed object the player touches, lifts, places, and completes.*

---

### 5.1 The Bolt as Protagonist

**The default bolt is not unfinished — it is a resolved product in its factory finish.**

A cosmetic skin upgrades the bolt's expressive register, not its quality register. Skins are upgrades in personality, not upgrades from poverty to adequacy. The default bolt must read as a jewel-grade component: the quality is in the tightness of its geometry, the correctness of its glow lifecycle, and the chromatic depth of its surface finish.

A gemstone is precise and beautiful simultaneously — precision is not the cold property of machined metal but the defining quality of something luminous and deliberate. The default bolt carries that register. It is factory-finished and jewel-grade from the first frame the player sees it.

**What makes a bolt feel premium vs. default:**
- Default: factory finish — color-saturated radial gradient, single tight specular, glow matches bolt color exactly, inner ring present and faint
- Cosmetic upgrade: different surface character, inner mark variant, glow hue rotation, matched slot treatment

---

### 5.2 Default Bolt Visual Specification (Locked)

**Rendering approach:** 3D-implied 2D sprite. Pre-rendered or hand-authored 2D sprite that implies depth through baked lighting cues. No 3D geometry. Workshop skins use baked sprite textures — no new shader passes required.

**Implied depth lighting setup:**

| Element | Description | Position | Opacity |
|---|---|---|---|
| Base fill | Radial gradient, bolt color at full saturation and 100% brightness at center → subtle darkening (~85%) at edge. Edge falloff is gentle — jewels hold their color to the perimeter; they do not go dark. | Center | Full |
| Ambient darkening | Softened darkening at bolt perimeter, 8–12% luminance reduction, 8px feather. Subtler than a metal edge — the bolt does not recede at its rim. | Edge | — |
| Primary specular | Tight circular highlight, 25% bolt diameter, white, moderate gaussian feather — sharper and more intense than a metal highlight. | 10-o'clock (upper-left) | 75–80% |
| Rim light | Subtle inner-edge glow radiating outward from the fill boundary toward the halo zone. Color: bolt's own hue at high luminance (90–100% lightness). The bolt edge glows with its own color rather than catching ambient light from a directional source. 3–4px soft radius. | Full perimeter, soft | 50% |
| Inner ring | Concentric ring at 60% diameter, 2px stroke, bolt color. Reads as an energy ring — a structural hum of the bolt's own color at rest. | Center | 40% |

The combined result: a circular disc, slightly convex, jewel-like surface, lit from upper-left with a saturated chromatic fill and a bright tight specular. The bolt holds its full color to the edge and glows softly with its own hue. Stylized precision-object, not photorealism.

**Glow relationship:** Halo is always the bolt's canonical color with no hue rotation in default state. At idle (0.4), halo stays within the 10% clearance zone. At active (1.0), may expand 2–3px beyond clearance zone.

**Three techniques producing the jewel/gem read:**
1. **Saturated fill discipline:** Center is the brightest, most saturated point of the bolt's hue. Luminance differential from center to edge is gradual, not steep — color is the primary signal, not shadow. No desaturation toward the edge.
2. **Single-source tight specular:** One highlight, consistent position, brighter and tighter than a metal highlight. Multiple highlights read as plastic or glass. A gem's highlight is intense and singular.
3. **Color self-illumination at the edge:** The rim radiates the bolt's own hue at high luminance rather than catching ambient light from a directional source. This is what separates a jewel from a disc — the edge glows from within, not from outside.

**What the default bolt must NOT produce:**
- Flat uniformly-filled circle (reads as a colored button)
- Chrome mirror effect (reads as liquid; loses color identity)
- Dark stroke around perimeter (reads as cartoon; rim glow handles separation)
- Pure center-point specular at full diameter (reads as marble or sphere; highlight must be off-center and occupy ≤30% of bolt diameter)

---

### 5.3 Skin Tier Visual Targets

**Finish Skin — 300 coins**

Surface finish variation only. No geometry changes, no inner mark changes, no slot changes.

What the player sees: the bolt in a different surface character within the jewel/gem quality register — matte gem, frosted, high-polish. The bolt's color identity is unchanged; its surface character is distinct.

**Allowed:** Specular intensity 0–120% of default; specular shape tighter/softer (not repositioned); gradient ramp ±10% luminance at center and edge; rim light opacity 30–70%.
**Forbidden:** Bolt hue change; inner mark variant; glow hue rotation; slot appearance; new texture channels.

Production: 1–2 hours per color × 6 bolt colors = 6–12 hours per finish set.

---

**Set Skin — 1,200 coins**

Bolt + slot as a matched visual system. The board feels like it was poured from one material.

**Allowed (all Finish tier changes plus):** Inner mark variant (ring/dot/cross/triangle); glow hue ±30° rotation; slot stroke weight 1–2.5px + color shift (cold palette, ≤40% opacity); slot ambient glow at idle ≤0.2 (below bolt's 0.4 floor — slots must never compete); non-radial gradient (e.g., brushed direction).
**Forbidden:** Bolt silhouette; bolt diameter; slot shape (rounded rectangle); stack structure; inner mark opacity; glow halo shape; slot glow ≥0.2 idle.

**Coherence test:** Set skin bolt + slot must read as a matched pair. Must not visually dominate non-Set bolts to a degree that impairs color legibility.

---

**Workshop Skin — 2,800 coins**

Full visual rework within locked constraints. The bolt looks like it was crafted in a different tradition. Texture approach: **baked into sprite — no new shader passes** (no Technical Artist coordination required).

**Allowed (all Set tier changes plus):** New baked sprite texture (new art asset, same shader); inner mark animation (pulse at idle, within glow lifecycle — no new animation system hooks); custom glow tint gradient (inner vs. outer halo temperature difference, still within ±30° hue); elaborate slot stroke (double-stroke, dashed at low opacity, within rounded-rectangle constraint and ≤2.5px total weight).

**Forbidden (absolute, no exceptions):** Bolt silhouette; bolt diameter; stack structure; inner mark opacity (40%); glow halo shape (radial); glow lifecycle floor/ceiling (idle 0.4, active 1.0, disabled 0.0); color legibility at 44px.

**Gate:** Art Director review required before production begins. Brief must include: (1) concept name, (2) inner mark variant, (3) glow hue rotation amount, (4) texture description, (5) slot treatment sketch.

Production: 4–8 hours per color × 6 bolt colors = 24–48 hours per Workshop set.

---

### 5.4 Slot Design Direction

The slot is the secondary designed object — the bolt's setting, not its equal. A gem setting holds and frames without competing; the slot performs that same function here. Everything it does is in service of the bolt it contains.

| State | Stroke opacity | Fill | Glow | Duration |
|---|---|---|---|---|
| Empty / idle | 30% | None | None | — |
| Occupied / idle | 0% (behind bolt) | Behind bolt | Bolt glow only | — |
| Completing (pulse pass) | Brief rise to 70% | None | Reflects bolt completion pulse | 400ms |
| Disabled (locked slot) | 15% | None | None | — |

**Slot depth layering:** Bolt fill (core) → glow halo (inner ring) → clearance zone → slot stroke (outermost boundary). The bolt sits inside the slot, not on top of it.

**What the slot must never do:** Fill with color; animate outside completion events; adopt bolt color as stroke in default skin; glow above 0.2 at idle or 0.7 at peak completion.

---

### 5.5 Thumbnail Legibility Rule

| Bolt diameter | What the eye receives |
|---|---|
| 44px (minimum) | Color identity + glow state (idle vs. active). Inner mark invisible. Color is the only signal. |
| 64px | Mark registers as tonal variation — not identifiable, but "there is something there." |
| 88px | Inner ring fully legible. Mark type identifiable. Specular + rim light register. Reads as jewel-grade object. |
| 128px+ | Full detail: gradient ramp, specular shape, inner mark type, surface texture (Workshop). |

**44px legibility gate (required before any skin enters production):**
1. Color identity named without hesitation in under 1 second against `#0B0F14` background ✓
2. Idle (0.4) vs. active (1.0) brightness difference immediately perceivable ✓
3. All 6 bolt colors side-by-side at 44px, all distinguishable without backup patterns ✓
4. Inner mark detail not legible at 44px (if legible, reduce contrast — it must not be a signal at this size) ✓

---

## 6. Environment Design Language

### 6.1 Design Premise: The Board Is the Only Environment

BoltSort has no world, no level geography, no traversable space. The "environment" is a bounded rectangle of near-black space containing the game board. Environmental design is not about world-building — it is about making that bounded space feel like a specific, physical location rather than an arbitrary arrangement of columns.

From Section 1, P5: **the board is the world; everything else is the frame.**

The dark background is a stage. Its job is to recede completely and let the jewel-tone bolt colors sing. The environment does not compete for visual energy — it holds and presents.

---

### 6.2 Background Design Direction: Lit Machine Interior

**Decision: Ultra-subtle dark grid at 4–6% opacity.**

The background at `#0B0F14` is not an empty void — it is the interior of a machine. A fine orthogonal grid at near-invisible opacity, 1px strokes, spacing calibrated to 1 bolt diameter, communicates "this surface has structure" without competing with the board.

The grid spacing equals the column gap (1 bolt diameter per Section 3.4). The background and the board share one spatial grammar — subliminal machine coherence.

**The grid is invisible at a glance, visible on inspection.** Correct behavior: perceptible when specifically sought; absent when the player is focused on the board. Test: at maximum screen brightness in a well-lit room, the grid is *readable* when sought and *absent* during play.

**Why not the alternatives:**
- Pure dark void: reads as cheap dev placeholder; undermines the machine aesthetic
- Ambient particle field: particles have motion that competes with the glow lifecycle signal system (P2). Reject.
- Vignette/depth gradient: implies a light source narrative; reads as "dramatic" — vignettes draw the eye toward the corners and edges of the screen. The bolt colors must be the only radial energy on screen; anything that pulls focus outward works against them. Reject.

**Background asset specification:**

| Property | Value |
|---|---|
| Asset name | `env_bg_grid_base.png` |
| Grid line color | `#1E2A38` |
| Grid line width | 1px |
| Grid cell size | 1 bolt diameter at reference resolution |
| Grid opacity in Unity | 4–6% (calibrated in-engine — lowest perceptible value on target device at max brightness) |
| Repeat mode | Tiling, full bleed including under notch/home bar areas |
| Camera clear color | `#0B0F14` (set on Camera or URP 2D Renderer asset) |

The grid is a single static sprite on the "Background" sorting layer, below all gameplay elements. No animation, no per-frame updates.

---

### 6.3 Board as Space: Physical Placement and Boundary

**The board is a real location within the machine. It has an implied floor and horizontal margins; it has no walls.**

**Board positioning:** Centered horizontally. Vertical center-of-gravity between the top HUD bar and the bottom safe area (home indicator). "Center of gravity" is a visual judgment — the board's midpoint should read as the center of the usable play area.

**Implied floor:** The lowest slot row in every column forms a visual baseline. This is the board's floor — the player reads it as the surface upon which the columns rest. The floor is implied by alignment, not drawn.

**No bounding box.** No frame, no panel, no card. Columns extend from the floor row upward into open space. The board boundary is defined by the outermost column's glow halo fading into the background — not a drawn edge.

**Structural divider:** A single 1px hairline at the board-to-HUD boundary, color `#1E2A38` (same as grid lines — part of the machine surface). This is the only explicit drawn boundary in the layout, and the only permitted hard-corner element (P3, structural exception).

---

### 6.4 Ambient Environmental Elements: None at Base Tier

**Decision: No ambient particles or environmental motion in the base visual theme.**

Reasons:
1. **Mobile GPU budget.** VFX Graph is already deployed for completion effects. A permanent ambient particle system burns draw calls and fill rate for zero functional signal.
2. **Semantic noise.** The glow lifecycle (P2) is the machine's communication system. Background motion creates false signals — the player's peripheral vision registers motion as a potential gameplay event.
3. **Theme differentiation.** Ambient particles in non-base themes add visual richness around the board without competing with bolt colors — reserved for themes where that additional energy is appropriate.

No motion exceptions in the base theme. Background grid is static. Structural divider is static.

---

### 6.5 Safe Area and Board Layout

The background bleeds; the board and HUD respect safe area. Operationalizes ADR-0008.

| Zone | Treatment |
|---|---|
| Background grid | Full-bleed — extends under notch, Dynamic Island, home indicator, Android navigation bar |
| Game board (columns + slots) | Inside SafeAreaPanel — no bolt or slot extends outside safe area |
| HUD elements | Inside SafeAreaPanel per ADR-0008 |

**Layout specifics:**
- Background grid: full-bleed direct Canvas child (Sort Order 0), sibling of SafeAreaPanel, below all gameplay elements
- Structural divider: inside SafeAreaPanel at top edge of board area — not full-bleed
- On Dynamic Island devices: board columns begin lower than on notchless devices — correct and expected

**Vertical space budget (portrait, 1080×1920 reference):**

| Area | Approximate allocation |
|---|---|
| Top HUD bar (inside SafeAreaPanel) | ~120px |
| Top safe area inset (Dynamic Island worst case) | ~132px |
| Board area (columns + breathing room) | Fills remaining space |
| Bottom safe area inset (home indicator worst case) | ~102px |
| Bottom dead zone below board | ~48px minimum |

Layout system (unity-ui-specialist, ADR-0008) governs exact pixel values. Advisory only for background artwork framing.

---

### 6.6 Visual Theme System (Beta+)

**Base theme is locked. Themes vary texture and particle density — nothing else.**

**Fixed across all themes (invariant):**

| Element | Why fixed |
|---|---|
| Background darkness — always `#0B0F14` camera clear | Cross-state rule #1 (Section 2): background never changes brightness |
| No jewel-tone or warm fills in environment | Environment chrome stays cool; bolt colors are the only saturated, vivid elements |
| No characters, faces, or narrative icons | Section 1 Anti-Direction |
| Board structure — no walls, no tubes | Section 3.2 identity |
| Glow lifecycle system | P2 — themes cannot add glow to non-interactive elements |
| Structural divider | Load-bearing; may restyle color within chrome palette, not remove |

**Variable per theme:**

| Element | Allowed range |
|---|---|
| Background texture | Can swap grid for alternative pattern (circuit trace, hex mesh, dot field) — same 4–6% opacity, cold palette only |
| Slot stroke style | Weight 1–2.5px; color within chrome palette; corner radius locked at 8dp |
| Structural divider color | Any CHROME value (Section 4.1 only) |
| Ambient particle system | Permitted in non-base themes; density governed by QualityTierSystem; cold palette; particles must not occur within 8dp of any bolt column |
| Board area tint | Transparent color wash over board area, opacity ≤8%, cold, no glow in background layer |

**Asset naming:** `env_bg_[theme-name]_[variant]_[size].png`
Example: `env_bg_circuit_base.png`, `env_bg_hex_base.png`

---

### 6.7 Environmental Storytelling: Abstract Machine

**Decision: Abstract — the machine is the metaphor, not a literal place.**

BoltSort communicates an *idea*, not a *place*. No serial numbers, no machine housing, no cables, no components outside the board area, no location names. The environment communicates:

- **Background grid**: "This surface has structure."
- **Near-black color**: "You are inside something, not standing outside it."
- **Glow lifecycle**: "The system is alive, responsive, and full of color."
- **Column alignment and slots**: "There is an ordered system here. It awaits your input."

Background detail in visual themes must remain abstract geometry. An outsourced artist referencing "circuit board photograph" must stop short of adding any readable detail — component labels, visible traces that spell something, glyphs, logos. Background detail is structure only.

Reference holds: Opus Magnum, mechanical watch internals. Neither has narrative — they have structure. BoltSort communicates structure, not story. These references also show that structure and vibrancy are not opposites — a well-lit gemstone under a loupe has both.

---

## 7. UI/HUD Visual Direction

> **HUD GDD Conflict (resolved 2026-05-10):** HUD GDD tuning knobs `coin_pulse_color_positive = #4CAF50` and `coin_pulse_color_negative = #FF9800` conflict with this section. Art bible supersedes — update HUD GDD tuning knobs to: `coin_pulse_color_positive = #4DCFEF` (CHROME-03), `coin_pulse_color_negative = #9BACC0` (neutral dim). No warm colors during play states 1–4.

---

### 7.1 Layout

**Two zones. Display elements at top (glance zone). Interactive controls at bottom (thumb zone).**

```
┌─────────────────────────────────┐
│ [COIN]          [MOVE: 12]      │  ← Glance zone (top, safe area inset)
│                                 │
│           BOARD AREA            │  ← Visually dominant
│                                 │
│ [UNDO]                 [HINT]   │  ← Thumb zone (bottom, safe area inset)
└─────────────────────────────────┘
```

**Rationale:** On a 430pt iPhone Pro Max, top buttons require thumb extension — a flow break. Interactive controls must live where the thumb naturally rests during one-handed play (bottom strip, 0–180pt from safe area bottom). Display-only elements (move counter, coin balance) need only a glance — top placement is appropriate and keeps them visible without competing with the board.

**Button sizing:** Minimum 48×48pt hit area per button (Android 48dp, iOS 44pt — use stricter value). Recommended: 56×56pt to reduce missed taps. Hit area may exceed visual area via RectTransform padding.

**Button separation:** Undo at bottom-left edge (safe area inset + 16pt padding). Hint at bottom-right edge (safe area inset + 16pt padding). Full-width separation (~200pt gap on 390pt screen) eliminates inter-button mis-tap risk and reinforces semantic distinction: undo is escape, hint is resource — spatially distinct actions.

**Top row:** Coin balance chip at top-left. Move counter at top-center (solitary — Gestalt isolation signals primary read). No top-right element.

**HUD band depth:** Determined by button height (56pt recommended). Move counter and coin display center vertically within this height.

**Structural divider:** A 1px `#1E2A38` hairline runs full-width at the bottom edge of the top HUD row, and at the top edge of the bottom button strip. These dividers are the only drawn boundaries in the layout.

---

### 7.2 Typography

**Font family:** IBM Plex Mono (move counter numeral only) + IBM Plex Sans (all other text). Both share identical x-height and vertical rhythm. Fallback: Space Grotesk or Outfit (free, tabular numerals available).

**Weight hierarchy:**

| Element | Font | Weight | Note |
|---|---|---|---|
| Move counter numeral | Plex Mono | SemiBold (600) | Tabular — must not reflow on increment |
| Coin balance numeral | Plex Sans | Regular (400) | ~70% of move counter cap-height |
| Descriptor labels (if required) | Plex Sans | Light (300) | ~60% of coin balance numeral |
| Button labels | None — icons only | — | — |

**Weight rule:** Never Bold (700+) in the HUD. Bold reads as alert. The HUD reports, it does not alert.

**Color:** All text in CHROME-04 `#C8D8E8`. Pure white `#FFFFFF` is forbidden in all persistent HUD text — reserved for completion pulses (Section 1 P4).

**Tabular figures:** The move counter must use tabular (fixed-width) digit glyphs. A proportional digit counter shifts layout on increment; this violates the instrument-grade aesthetic.

**Text contrast requirement:** Text must meet WCAG AA minimums (4.5:1 for normal weight below 18pt) against the HUD panel background at all opacity states, including HINT_PROCESSING and FROZEN. The HUD panel (CHROME-02 `#141C24` at 60–70% opacity) must be a dedicated background layer behind text — not floating text at reduced opacity over live board content.

---

### 7.3 Iconography

**Style:** Outlined, 2dp stroke, rounded line caps, CHROME-03 cyan (`#4DCFEF`). Diagnostic glyphs — one reading at a glance.

**Icon-to-button relationship:** Icon horizontally and vertically centered within button. Icon bounding box at 50–60% of button interior, leaving glow halo and rounded border visible as button frame.

**Undo icon:** Counterclockwise arc-arrow, 270° sweep, left-heavy, arrowhead on leading (left-bottom) end. This is the universal undo glyph — immediately associated with "reverse" regardless of prior app experience. Arrowhead faces left-and-down: backward in reading direction, downward into the slot.

**Hint icon:** Single thin lightbulb outline, geometric. No radiating-ray lines, no filament detail. Glass dome: D-shape stroke, 2dp weight. Base fitting: two horizontal lines, 1.5dp stroke. The stripped-down lightbulb reads as "suggestion/next move" at 24dp without visual noise.

**Disabled state:** Icon stroke opacity drops to ~40% luminance, glow 0.0. Shape remains present (spatial information preserved). Never hide an inactive element — hiding removes spatial information. No color change for disabled state (shape + opacity provides the cue, independent of hue discrimination).

**Two-button differentiation:** Distinct silhouettes (arc-arrow vs. lightbulb) + full-width positional separation satisfies accessibility minimum: two distinguishing dimensions without relying on color alone.

---

### 7.4 HUD State Visual Language

**IDLE — baseline instrument panel.**

All buttons at resting glow: CHROME-03 stroke at emission 0.4. Move counter in CHROME-04, static. Coin balance in CHROME-04, smaller, static. HUD panel (CHROME-02 at 60–70% opacity). Nothing in the IDLE HUD pulses or animates — the HUD observes without speaking; the board does the talking.

**HINT_PROCESSING — pulse on hint button.**

*Required.* A static locked state is a UX failure — the player cannot distinguish "working" from "broken" without motion feedback.

Visual treatment:
- On tap: hint button immediately shifts to locked appearance (emission → 0.2, below idle 0.4). Button background gains a 5% CHROME-02 fill (was stroke-only at idle).
- Simultaneously: a concentric arc ring (2dp stroke, CHROME-03, 90° of button bounding arc) begins rotating clockwise, 1.0s/revolution, sine in/out easing.
- Arc runs continuously for the full HINT_PROCESSING duration (up to hint_timeout_ms = 5000ms).
- On `hint_result` received: arc stops, button resolves to enabled or disabled state over 100ms fade.
- On timeout: identical resolve animation. No error indicator — button simply re-enables.

The arc is diagnostic, not decorative. The machine is scanning. It does not celebrate.

**FROZEN — step-down, not blackout.**

On `level_complete`, all buttons disable immediately (emission 0.0, stroke at 15% opacity, glow 0.0). Move counter holds its final value at CHROME-04 60% opacity. Coin balance display remains fully live — it is the only non-suppressed element in FROZEN, because coins are still being awarded. This is an intentional visual signal: the machine is still counting your reward.

HUD does not fade to invisible. Elements remain present in suppressed form. The spatial grammar of the HUD must be preserved when the Level Complete screen builds over it — no layout jump.

Button suppression applies at `level_complete` receipt, before the board celebration animation completes — the player sees buttons as done while the board still resolves.

---

### 7.5 Coin Display

**Icon:** Stroked circle (not filled — distinct from bolt). Diameter ~60% of bolt diameter. CHROME-03 `#4DCFEF` stroke, stroke-only, no fill. Interior mark: thin horizontal slash or minimal "c" glyph, 1dp stroke, centered. Test mark legibility at 16dp rendered size — if the mark is illegible, use the slash variant.

No warm color on the coin icon during IDLE or any play state (1–4). CHROME-05 amber `#E8A030` is Level Complete (State 6) only — that is when the HUD earns its one moment of jewel-energy. During play states the HUD stays instrument-cool so the board's jewel tones read without competition.

**Coin balance animation (positive delta — `coin_balance_changed`, delta > 0):**
- Numeral cross-fades to new value immediately (not a counting animation — the number is correct immediately).
- Numeral briefly shifts from CHROME-04 → CHROME-03 `#4DCFEF` over 100ms, returns to CHROME-04 over 200ms. "System event touched the display."
- Coin icon scales +15% then returns, ease-in-out, over first 150ms of 300ms pulse window.

**Coin balance animation (negative delta — hint spend):**
- Numeral cross-fades to new value immediately.
- No color shift. Icon scales −5% briefly (mild deflation). Instrument-appropriate.
- No amber, no red, no warm color.

**Rapid-fire events:** On multiple `coin_balance_changed` events in quick succession, each restarts the 300ms pulse from current displayed value. This produces a visible flicker — acceptable behavior per GDD (fire-and-forget restart). Reduce `coin_pulse_duration_ms` to 150ms if the flicker is aesthetically objectionable.

**Pity grant notification:** When `earn_source = EarnSource.PityGrant`, a contextual toast appears (separate from the coin pulse animation):
- Small rounded-rectangle chip (12dp corners, CHROME-02 fill at 85%, CHROME-03 stroke).
- Position: above the bottom button strip or at base of top HUD row.
- Copy: system-voice — "Hint restored."
- Fade in 200ms / hold 2000ms / fade out 300ms.
- Toast appears first; coin balance pulse follows after toast completes (sequential, not overlapping).

---

### 7.6 Button Press State

On tap-down:
- Button scale compresses to 94% (ease-in-cubic, 60ms).
- Glow surges immediately to 1.0 on tap-down frame (instant — human finger contact is instantaneous; the feedback must feel that way).
- Button interior gains 10% CHROME-02 fill (was stroke-only).

On tap-release:
- Scale returns from 94% → 100% (ease-out-cubic, 80ms — spring return).
- Glow begins return from 1.0 → 0.4 over 200ms (consistent with bolt settle glow ramp, P2).
- If action immediately disables the button, glow transitions directly to 0.0 over 200ms (not to 0.4 first).

Total press state: ~140ms (60ms down + 80ms up). Below conscious perception threshold — felt before seen.

No ripple effects, no color change during press. Press feedback uses scale and emission only.

---

## 8. Asset Standards

> **Authority**: This section is binding on all asset production for BoltSort. Art Director preferences and Technical Artist constraints are both documented — where they were in conflict, the resolution is noted. Technical Artist owns import settings enforcement; Art Director owns source file quality.

---

### 8.1 Naming Convention (PascalCase — project-wide)

**Canonical format:** `[Category]_[Name]_[Variant]_[Size].[ext]`

PascalCase throughout — consistent with CLAUDE.md coding standards. All asset filenames must be stable after first import (GUID chain is keyed to filename; renames require re-linking).

| Field | Rule | Examples |
|---|---|---|
| `[Category]` | Asset category, PascalCase | `Bolt`, `Slot`, `BgGrid`, `VFX`, `UI`, `Font` |
| `[Name]` | Descriptive name or bolt color short name | `Cobalt`, `Scarlet`, `CoinIcon`, `SettleRing` |
| `[Variant]` | Skin variant, tier qualifier, or `Default` | `Default`, `MatteSkin`, `Wksp_Plasma` |
| `[Size]` | Width in px. Omit for font assets. | `128`, `64`, `512` |
| `[ext]` | `.png` (source); `.asset` (TMP); `.vfx` (VFX Graph) | — |

**Bolt color short names (canonical):**

| Color | Short name | Example filename |
|---|---|---|
| Cobalt `#2A72E8` | `Cobalt` | `Bolt_Cobalt_Default_128.png` |
| Scarlet `#E83030` | `Scarlet` | `Bolt_Scarlet_Default_128.png` |
| Cobalt-Green `#28C864` | `Green` | `Bolt_Green_Default_128.png` |
| Amber-Orange `#E87820` | `Amber` | `Bolt_Amber_Default_128.png` |
| Violet `#8030D8` | `Violet` | `Bolt_Violet_Default_128.png` |
| Ice `#78D8F0` | `Ice` | `Bolt_Ice_Default_128.png` |

**Skin tier prefixes (name field):**
- Finish: `Bolt_Fin[SkinName]_Cobalt_128.png`
- Set: `Bolt_Set[SkinName]_Cobalt_128.png`
- Workshop: `Bolt_Wksp[SkinName]_Cobalt_128.png`

Skin names: one-word material descriptor, no brand references, unique across all tiers. Examples: `Matte`, `Carbon`, `Obsidian`, `Plasma`.

**Colorblind pattern assets** (Section 4.4): `Bolt_Cobalt_Pattern_128.png` (PascalCase — previously `bolt_cobalt_pattern_normal.png` in Section 4.4, updated here to match canonical convention).

**Addressables addresses:** lowercase with hyphens. Example: `bolt-sprites/Bolt_Cobalt_Default_128`. Addressables label names: `bolt-sprites`, `vfx-textures`, `ui-sprites`, `level-data`, `fonts`.

**VFX Graph assets:** PascalCase, no field structure: `BoltSettleRing.vfx`, `BoltStackComplete.vfx` (matches ADR-0010 naming).

---

### 8.2 File Formats and Platform Compression

Source format for all raster assets: **PNG** (lossless). Platform compression is applied by Unity's importer at build time — never pre-compress source files.

| Asset type | Source | Android compression | iOS compression | Alpha |
|---|---|---|---|---|
| Bolt sprites | PNG-32 (RGBA) | ETC2 RGBA8 | ASTC 6×6 | Required — glow halo soft edge |
| Slot sprites | PNG-32 (RGBA) | ETC2 RGBA8 | ASTC 6×6 | Required |
| Background | Procedural Shader Graph — no texture asset | — | — | N/A |
| VFX particle textures | PNG-32 (RGBA) | ETC2 RGBA8 | ASTC 6×6 | Required |
| VFX ring sprite | PNG-32 (RGBA) | ETC2 RGBA8 | ASTC 6×6 | Required |
| UI sprites / icons | PNG-32 (RGBA) | ETC2 RGBA8 | ASTC 6×6 | Required |
| Font (TMP) | OTF/TTF source → TMP Font Asset (.asset) | TMP manages internally | TMP manages internally | — |

**Background:** Implemented as a procedural Shader Graph material — zero texture memory cost, crisper result at all resolutions, scales to any screen density. No raster background tile asset is produced. Art Director provides: grid line color (`#1E2A38`), grid cell size (1 bolt diameter = project spatial unit), grid line weight (1px at reference resolution), material opacity (4–6%). Technical Artist implements the Shader Graph and configures the opacity parameter as a runtime-adjustable material property.

**ETC2 edge artifact risk:** On small sprites (≤64px rendered), ETC2 block compression can introduce visible artifacts at alpha edges. The glow halo depends on clean alpha gradients. Technical Artist must validate compressed output at final display sizes on Samsung Galaxy A14 before approving the bolt sprite source style. If artifacts are visible: (a) apply 1px alpha dilation baked into the source sprite, or (b) increase source to 256×256 to give the compressor more data, or (c) use ASTC 4×4 on iOS for higher quality.

---

### 8.3 Texture Resolution Tiers

| Asset | Source resolution | Rationale |
|---|---|---|
| Bolt sprites | **128×128px** | Safe for GPU memory budget; inner ring disappears at 44px as designed; upgrade to 256 only post-profiling if device testing reveals detail loss |
| Slot sprites | 128×128px | Stroke-only; 1.5px stroke requires enough resolution to prevent aliasing at High DPI |
| VFX spark particles | 64×64px | Small, fast, ≤350ms lifespan; 64px provides adequate feather resolution |
| VFX ring sprite | 128×128px | Expands to 2.5× stack radius; 128px scales cleanly with bilinear filter |
| UI icons (coin, undo, hint) | 64×64px | 2× headroom for High DPI; icon renders at 24–36pt in HUD |
| Star rating icons (Level Complete) | 64×64px | Primary read on Level Complete screen |
| TMP Font Atlas | 512×512px (body text) | Minimum atlas that fits Basic Latin without packing artifacts |

**Maximum sprite atlas size: 2048×2048 (2K).** Do not use 4K atlases — mobile GPU cache efficiency and upload cost favor 2K. Use multiple 2K atlases rather than one 4K.

**Memory math reference (compressed):**
- 128×128px bolt sprite, ETC2 RGBA8 (1 byte/px): 128 × 128 × 1 × 1.33 (mipmaps off) ≈ 22KB. Twelve sprites: ~264KB. Negligible.

---

### 8.4 Unity Import Settings Per Asset Category

**Bolt Sprites**

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | 100 (project standard — must match across all gameplay sprites) |
| Filter Mode | Bilinear |
| Generate Mipmaps | Off (bolt sprites render at near-fixed screen size) |
| Wrap Mode | Clamp |
| Read/Write Enabled | Off |
| Max Texture Size | 128 |
| Compression (Android) | ETC2 RGBA8 |
| Compression (iOS) | ASTC 6×6 |
| Pack into Atlas | `GameplayAtlas` |

**Slot Sprites:** Same as bolt sprites. Evaluate Tight mesh sprite mode for the stroke-only slot (reduces overdraw from transparent interior) — requires explicit decision before import settings are locked, as Tight mesh must be coordinated with atlas packing.

**VFX Particle Textures (Spark particles)**

| Setting | Value |
|---|---|
| Texture Type | Default (VFX Graph samples Texture2D, not Sprite) |
| Wrap Mode | Clamp |
| Generate Mipmaps | On (particles scale in world space) |
| Filter Mode | Bilinear |
| Max Texture Size | 64 |
| Compression | ETC2 RGBA8 / ASTC 6×6 |

**VFX Ring Sprite:** Same as bolt sprites, Max Texture Size 128, pack into `VFXAtlas` (separate from gameplay bolt atlas).

**UI Sprites**

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Pixels Per Unit | 100 |
| Generate Mipmaps | Off (UI renders at fixed Canvas pixel size) |
| Max Texture Size | 128 (icons), 64 (small icons) |
| Wrap Mode | Clamp |
| Pack into Atlas | `UIAtlas` |

**TMP Font Assets**

| Setting | Value |
|---|---|
| Font Asset Type | SDF (mandatory — scales cleanly at all HUD sizes) |
| Atlas Width/Height | 512×512 (body text MVP) |
| Padding | 5px (prevents SDF gradient clipping at glyph edges) |
| Character Set | Basic Latin (ASCII 32–126) at MVP; expand for localization |
| Mode | Not Bitmap — SDF mandatory |

TextMeshPro font assets are not part of any Sprite Atlas — TMP manages its own atlas texture internally.

---

### 8.5 Sprite Atlas Organization

| Atlas | Contents | Max Size |
|---|---|---|
| `GameplayAtlas` | All bolt sprites (6 colors × default + skin variants), slot sprite | 2048×2048 |
| `UIAtlas` | Coin icon, undo icon, hint icon, star icon, all UI chrome sprites | 2048×2048 |
| `VFXAtlas` | Spark particle textures, ring sprite | 512×512 |

Font atlas textures are TMP-managed — not part of any Sprite Atlas.

**Batching rules:**
- Sprites in the same atlas + same material = single draw call regardless of instance count (SRP Batcher). The full board (up to 44 bolt instances) targets ≤4 draw calls for gameplay sprites.
- Do NOT use `MaterialPropertyBlock` on Sprite Renderers — breaks SRP Batcher for any renderer it touches. Forbidden per ADR-0010.
- Non-atlased sprites cannot batch. Every sprite appearing in gameplay or UI must be in an atlas.
- The GlowOverlay sprite uses `BoltGlow_Additive` material — intentionally separate from the main bolt sprite material. Expected additional draw calls within the 100 draw call budget (ADR-0005).

**Skin variant atlas planning (Beta):** At Beta scope (5 skins × 6 bolt colors = 30 bolt sprites at 128px), the `GameplayAtlas` accommodates this comfortably within 2K. Plan the atlas budget before the skin sprint begins — Addressables skin bundles must align with atlas boundaries (a bolt sprite and its atlas must be in the same Addressables bundle, or the bundle boundary forces a separate atlas per bundle). Resolve with Technical Artist before skin implementation sprint.

---

### 8.6 Export Rules for Artists

- **Alpha channels:** Export bolt sprites with full alpha (PNG-32). Never pre-multiply alpha at export — Unity handles premultiplication in import settings. Delivering pre-multiplied PNG with Unity set to straight-alpha double-premultiplies and produces a dark corona.
- **Color profile:** Export in sRGB. HDR glow values are not baked into the sprite — they are set at runtime on the GlowOverlay material. The sprite source stays in sRGB.
- **Background tiles:** N/A — implemented as procedural Shader Graph. No raster export required.
- **VFX particle textures:** Export PNG-32 with alpha. Deliver at maximum fidelity; Technical Artist handles compression in import settings.
- **Do not:** Apply lossy compression to any source file. Pre-compress textures. Embed non-sRGB color space profiles. Flatten glow layers with the bolt body (glow is a runtime system, not a baked texture value).

---

### 8.7 LOD Strategy

**No sprite LOD system needed or recommended.**

BoltSort uses a fixed-camera 2D layout — bolts always render at approximately the same screen-space size. The bolt diameter varies from ~44px (8-column board, smallest phone) to ~128px (2-column board, large phone) — a 3:1 scale range that Unity's bilinear sprite scaling handles cleanly from a single 128px source.

For tiling textures (background — implemented procedurally) and VFX particles: mipmaps provide hardware-automatic LOD at zero runtime cost.

For bolt sprites: mipmaps are disabled (fixed screen-space size). No explicit LOD swap script required. If profiling on Samsung Galaxy A14 reveals GPU texture bandwidth pressure, revisit — but this is not a problem to solve before verifiable evidence exists.

---

### 8.8 Open Items for Technical Artist (Beta Milestone)

- [ ] Implement background grid as Shader Graph material — receive grid color, cell size, line weight, opacity from art director
- [ ] Validate ETC2/ASTC compressed bolt sprite quality on device (edge artifact check) before skin production begins
- [ ] Determine Addressables bundle boundary alignment with `GameplayAtlas` before skin implementation sprint
- [ ] Decide: Tight mesh sprites for slots (overdraw savings vs. atlas/material complexity) before slot import settings are locked
- [ ] Confirm GPU Resident Drawer is enabled and verified functional in Unity 6.3 (post-cutoff API — verify in-editor)

---

## 9. Reference Direction

> **Note**: Two references — Opus Magnum and mechanical watch internals — were cited first in Section 6.7 (Environmental Storytelling) for their abstract machine metaphor and grid structure lessons. Section 9 draws different, additive lessons from a distinct reference pool. Do not conflate the two applications.

---

Each reference covers exactly one visual dimension. No two references point in the same direction. An outsourcer using this section correctly will find four non-overlapping lessons — taking two lessons from the same reference, or applying a lesson from one reference to the dimension another reference owns, is a misread.

---

### 9.1 Gemstone / Jewelry Photography (Non-Game)
**Dimension covered: Bolt surface and color quality**

**What to draw from:**

The cut gem under a loupe demonstrates two surface principles that no game reference does. First: **color self-illumination from within the form.** A cut amethyst or ruby does not rely on an external light source to appear vivid — the stone refracts light internally, and its color appears to radiate outward from inside the geometry. This is exactly the bolt rim-light mechanism specified in Section 5.2: the bolt's edge glows with its own hue at high luminance rather than catching ambient light from a directional source. Reference gemstone photography directly when calibrating the rim light opacity (Section 5.2: 50% at full perimeter) and the base fill gradient (full saturation held to the edge, no desaturation at the perimeter).

Second: **the tight, singular specular of a cut stone.** A gem has one specular event — bright, small, positioned, and unambiguous. Polished metal has many low-frequency reflections distributed across its surface. Plastic has a broad, soft highlight. The cut gem's highlight is intense and occupies less than 25% of the visible surface. Apply this directly to the primary specular specification (Section 5.2: 25% bolt diameter, white, moderate gaussian feather, 75–80% opacity, 10-o'clock position).

**What to explicitly avoid:**

Jewelry photography at macro scale reveals surface micro-imperfections, metal settings, prong geometry, and mounting hardware. None of this applies. If gem reference photographs are pulling the bolt design toward realistic material simulation (subsurface scatter, facet micro-geometry, metal mount shadows), the reference is being applied too literally. The lesson is surface character and color physics — not photorealistic material reproduction. Stop at the point of "tight specular + color from within." Do not proceed to "physically accurate gemstone rendering."

**What this contributes that no other reference does:**

Every other reference in this section is a game or game-adjacent source. Gemstone photography is the only reference that operates in physical reality — its surface principles are optical physics, not design conventions. This anchors the bolt surface specification in a real-world chromatic phenomenon (cut gem self-illumination) rather than a game-industry convention.

---

### 9.2 Royal Match
**Dimension covered: Board / puzzle legibility**
*User-named reference.*

**What to draw from:**

Royal Match is the standard-bearer for **puzzle legibility at mobile scale**: high-saturation gameplay objects against a controlled background, with the HUD chrome maintaining strict chromatic separation from the board. Study specifically:

1. **Figure-ground discipline.** Royal Match enforces a background that never competes with gameplay objects in saturation or luminance. Even its warm castle backgrounds remain sufficiently low in visual frequency to let gameplay tiles dominate. BoltSort's near-black ground (`#0B0F14`) is this principle taken to its logical extreme — maximum figure-ground contrast, zero background competition.

2. **HUD chrome vs. board color separation.** Royal Match's HUD palette (cool blues, whites, grey-golds) sits categorically apart from the gameplay gem palette (warm, saturated, jewel-tone). The player's eye never confuses HUD elements with gameplay objects because shape, color temperature, and emission are all differentiated simultaneously. Apply this triple-separation principle to BoltSort's HUD: CHROME-03 cyan HUD against jewel-tone bolts achieves shape + color temperature + emission separation in the same stroke.

**What to explicitly avoid:**

Royal Match's backgrounds are warm, richly detailed castle interiors. Its level select map has a world and a walking character. Its completion animations use confetti, hearts, and animated mascots. Copying Royal Match's compositional warmth, organic background detail, or character-forward completion energy produces a mobile toy game, not a precision machine. The legibility lesson transfers; the warmth, the narrative, and the decoration do not. An outsourcer whose work reads as "Royal Match with bolts instead of gems" has extracted the wrong lesson.

**What this contributes that no other reference does:**

Royal Match is the only reference in this section that demonstrates commercial viability of the exact legibility system BoltSort uses — at a production quality level outsourcers can directly evaluate. When legibility is in doubt on any asset, the check is: "Does this element achieve the same figure-ground clarity that Royal Match achieves for its tiles?" If not, it needs contrast or saturation adjustment.

---

### 9.3 Screw Sort 3D
**Dimension covered: Completion and celebration energy**
*User-named reference — direct genre peer.*

**What to draw from:**

Screw Sort 3D is the closest genre peer: a bolt-sorting puzzle using animated bolt objects, with a visible sort-completion beat when a screw type fills its container. Study two specific visual mechanisms:

1. **Bolt-object presence at mobile scale.** Screw Sort 3D's bolt objects are large, saturated, readable, and physically present — they do not feel like abstract colored tokens. At the scale BoltSort bolts render (44px minimum), Screw Sort 3D demonstrates that a bolt object can carry visual personality at small rendered size, provided color is at full saturation and the silhouette is clean. Use this as a calibration reference for the minimum acceptable bolt quality at 44px (Section 5.5 legibility gate).

2. **Completion event rhythm.** When a column fills in Screw Sort 3D, there is a discrete event — a beat of visual confirmation before the game advances. BoltSort's completion pulse follows the same structural rhythm: white confirmation → jewel-hue resolution → settle. Use Screw Sort 3D's completion animation timing as a real-world benchmark for the 400ms stack-complete and 600ms board-complete windows specified in Sections 1 (P4) and 2 (States 4 and 5).

**What to explicitly avoid:**

Screw Sort 3D's bolts are rendered with 3D isometric perspective — barrel depth, cast shadows, and soft rounded surfaces implying volume in three dimensions. This is the exact surface quality BoltSort rejects (Section 5.2: 3D-implied 2D sprite — implied depth only, no geometry). Its surface character reads as a plastic toy: soft highlights, ambient occlusion at the edges, no self-illumination, no chromatic edge glow. Copying its surface treatment produces the "bubbly plastic toy" failure mode named in the Anti-Direction (Section 1). The completion-rhythm lesson and scale-legibility lesson transfer; the material language does not transfer and must be deliberately inverted.

**What this contributes that no other reference does:**

Screw Sort 3D is the only reference in this section that operates in BoltSort's exact game genre. It validates completion timing and minimum bolt scale against a shipped product that players have already experienced — reducing the risk that BoltSort's timings will feel wrong against genre convention. The other references contribute visual quality principles; this reference contributes genre-specific calibration data.

---

### 9.4 Threes!
**Dimension covered: Machine aesthetic and background treatment**

**What to draw from:**

Threes! (Asher Vollmer / Greg Wohlwend, 2014) demonstrates the most important principle in BoltSort's background grammar: **the objects carry all the visual interest; the background earns its restraint.** In Threes!, the playing field is a pale warm grid — almost nothing. The numbered tiles are the entire visual event. Every pixel of background exists only to hold the tiles in a readable arrangement. The game is visually rich because the tiles are richly designed, not because the field adds decoration.

Apply this to BoltSort's background discipline in one specific way: the grid at 4–6% opacity (Section 6.2) should feel the same way Threes!' playing field feels — present as structure, absent as visual event. If the background grid is noticeable during active play, it is too prominent.

Threes!' numeral display is also the clearest existing example of **instrument-grade number legibility** in a mobile puzzle context: tabular figures, consistent weight, high contrast against field, no animation except on state change. This maps directly to the move counter specification in Section 7.2: IBM Plex Mono, SemiBold, CHROME-04, no animation at rest.

**What to explicitly avoid:**

Threes!' palette is warm — cream field, warm tile gradients, soft peach-and-white number panels. It reads as paper, cards, analog objects. Applying Threes!' warmth to BoltSort's background produces exactly the wrong material register: a paper puzzle game, not a machine world. Also avoid Threes!' deliberate "imperfection" quality — slightly organic tile proportions, hand-feeling layout. BoltSort's layout is precise and orthogonal. The restraint lesson transfers; the warmth, the card-game material language, and the analog object metaphor do not.

**What this contributes that no other reference does:**

Threes! is the only reference in this section built entirely around the principle that a near-empty background plus richly-designed objects produces visual richness — without the object being a bolt, a gem, or a machine component. It demonstrates the abstract principle in isolation, in a different genre, at commercial quality. The other references have visual richness in their backgrounds or in their completion effects; Threes! achieves visual richness through restraint alone. This makes it the clearest possible external proof for BoltSort's background grammar.

---

### Cross-Reference Summary

| Reference | Dimension | Core lesson | Explicit avoid |
|---|---|---|---|
| Gemstone / jewelry photography | Bolt surface quality | Color self-illumination + singular tight specular | Photorealistic material simulation, mount geometry |
| Royal Match | Board / puzzle legibility | Figure-ground discipline + HUD/board chromatic separation | Warm backgrounds, mascots, decorative completion effects |
| Screw Sort 3D | Completion / celebration energy | Completion-beat rhythm + bolt-scale legibility calibration | 3D isometric surface, bubbly plastic material language |
| Threes! | Machine aesthetic / background | Background restraint grammar + instrument-grade numeral display | Warm paper palette, analog card material, organic imperfection |

No two rows in this table should produce overlapping creative guidance. If an outsourcer finds two references pulling toward the same decision, re-read the explicit avoid column — one of the two references is being applied outside its dimension.
