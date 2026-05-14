# Game Concept: BoltSort

*Created: 2026-04-16*
*Status: Draft*

---

## Elevator Pitch

> It's a sci-fi sort puzzle for mobile where you stack color-coded bolts and nuts into matching columns — smooth, satisfying, and designed for pure flow. Every move clicks into place. Every level feels like a machine coming alive.

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | Casual puzzle / Sort puzzle |
| **Platform** | Mobile (iOS & Android) |
| **Target Audience** | Casual mobile players, 18–45, short-session flow seekers |
| **Player Count** | Single-player |
| **Session Length** | 2–10 minutes (short burst sessions) |
| **Monetization** | F2P — rewarded ads + cosmetic IAP (skin shop) |
| **Estimated Scope** | Medium (5–6 months, solo dev) |
| **Comparable Titles** | Ball Sort Puzzle, Screw Sort 3D, Water Sort Puzzle |

---

## Core Fantasy

You are restoring order to a living machine. Each chaotic column of mismatched bolts and nuts is a problem your hands can solve — tap, move, watch the colors align. The machine hums back to life as you complete each stack. There's no rush, no punishment, no stress — just the quiet satisfaction of everything clicking into its perfect place.

BoltSort gives you something rare in mobile games: a moment of pure control. Small wins, every 30 seconds, for as long as you want to play.

---

## Unique Hook

Like Ball Sort Puzzle, AND ALSO wrapped in a clean sci-fi aesthetic with glowing components, a skin shop for bolts and nuts, and a reward loop designed so good it never needs to feel coercive.

The differentiator isn't the mechanic — it's the *feel*. Where the genre defaults to toy-clutter visuals and aggressive monetization, BoltSort is calm, polished, and premium in presentation while remaining free to play.

---

## Visual Identity Anchor

**Direction**: Sci-fi Clean

**One-line visual rule**: Everything glows faintly — the machine is always alive.

**Supporting principles**:
1. *Blue/cyan/white palette* — no warm colors in the base UI. Cold, precise, technical. Design test: "Does this color feel like it belongs in a futuristic workshop? If not, pull it."
2. *Soft glow on all interactive elements* — bolts, nuts, stacks, and UI buttons all emit a subtle bloom. The glow intensifies on completion. Design test: "Can a player identify what's interactive purely by glow intensity?"
3. *Rounded edges everywhere* — no hard corners on panels, slots, or components. The machine is advanced enough to have solved right angles. Design test: "If a shape has a sharp corner, it should be load-bearing structure — not UI chrome."

**Color philosophy**: The background is dark navy/near-black. The bolt colors (gameplay elements) use the 6–8 distinct hues players must sort. UI chrome is cyan/white with low opacity. Completion bursts pulse white then fade to the bolt's color. The overall impression: a dark screen with precise, glowing components — like looking at a circuit board through a magnifier.

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Sensation** (sensory pleasure) | 1 | Smooth bolt animations, soft glow effects, satisfying audio clicks, completion bursts |
| **Submission** (relaxation, flow) | 2 | No timers, generous temp slots, near-zero frustration design, ambient sound design |
| **Challenge** (mastery) | 3 | Progressive level difficulty, optional "no hint" star rating, harder puzzles in later levels |
| **Expression** (self-expression) | 4 | Skin shop for bolt designs, nut styles, and background themes |
| **Fantasy** (make-believe) | 5 | "You are a machine operator bringing order to a sci-fi system" — ambient narrative |
| **Discovery** (exploration) | N/A | Not a core goal for this game |
| **Fellowship** (social) | N/A | No multiplayer in scope |
| **Narrative** (story arc) | N/A | No story — respects the session |

### Key Dynamics (Emergent player behaviors)

- Players will instinctively sort by color subgroup before making committed moves — planning behavior emerges from the mechanic without tutorial prompting
- Players will experiment with hint usage timing — save for true deadlocks vs. use freely
- Players will check the skin shop after completing levels — the reward loop drives browsing
- Players will develop preferred sorting strategies and feel competent when they work

### Core Mechanics (Systems we build)

1. **Sort mechanic** — tap a bolt to lift it, tap a destination stack to drop it. Only the top bolt can move. A stack accepts a bolt only if it's the same color as the top bolt, or if the stack is empty. A stack completes when it's full of one color.
2. **Stack system** — each level has N color stacks + 1–2 temp slots (overflow buffers). Temp slots can hold any bolt but cannot complete. Level is solved when all color stacks are complete.
3. **Coin & reward system** — coins awarded per level completion (base + star bonus). Coins spent in skin shop. Rewarded ads grant coins, hint refills, or temp slot unlocks.
4. **Hint system** — costs coins or a rewarded ad watch. Highlights the optimal next move. Auto-solves one move. Prevents total deadlock.
5. **Progression system** — numbered levels with increasing complexity (more colors, fewer temp slots, more bolts per stack). Daily challenges with bonus coin rewards.

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** | Skin choices, move order freedom, optional hints, "play at own pace" — no forced paths | Supporting |
| **Competence** | Level completion, star ratings, "perfect solve" bonus (no hints used), visible progress counter | Core |
| **Relatedness** | Daily leaderboard (optional), shareable completions, seasonal skin drops that feel like events | Minimal (future scope) |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** (goal completion, collection, progression) — How: numbered levels, star ratings, skin collection, daily goals
- [ ] **Explorers** (discovery, understanding systems) — Not a primary target
- [ ] **Socializers** (relationships, cooperation) — Not in scope for launch
- [ ] **Killers/Competitors** (domination, PvP) — Not a target audience

### Flow State Design

- **Onboarding curve**: First 5 levels are hand-held — 2 colors, 2 stacks, no temp slots needed. Tutorial is gesture-based (show, don't tell). By level 10 the player is self-sufficient.
- **Difficulty scaling**: Colors increase from 3 → 8 across 200 levels. Temp slots decrease as difficulty rises. Stack depth increases. Pacing: every 10 levels, one easier "breather" level.
- **Feedback clarity**: Completion glow + audio chime per stack. Full celebration + coin pop per level. Star rating shown at end screen. Coin total always visible.
- **Recovery from failure**: No failure state — players can always undo or use a hint. The game never ends in a loss screen. If truly stuck, a rewarded ad unlocks an extra temp slot.

---

## Core Loop

### Moment-to-Moment (30 seconds)

Player taps a bolt — it lifts with a smooth float animation (slight arc, glow intensifies). Player taps destination stack — bolt glides in and settles with a soft click. If the move completes a same-color run at the top of the stack, a subtle pulse fires. When a stack fills completely, a glow burst and chime play. Repeat. The satisfaction is in the arc of motion, the audio landing, and the color grouping resolving visually — three simultaneous rewards per correct move.

### Short-Term (5-15 minutes)

Each level takes 1–3 minutes. On level complete: reward screen (coins animate in, star rating displays, optional rewarded ad for bonus coins). Next level teases immediately — the board fades in, hinting at the new puzzle. "One more level" psychology kicks in because the next board is always visible before the player taps to confirm. The hint system serves as an escape valve — players almost never deadlock.

### Session-Level (30-120 minutes)

A typical session = 3–10 levels. Natural stopping points: daily goal complete ("You've earned 500 coins today"), milestone unlock ("You've unlocked the Neon Bolt skin"), or a harder level the player decides to return to. The meta hook when not playing: "X levels left before the Plasma pack unlocks."

### Long-Term Progression

Numbered levels (200+ at launch, expandable via content updates). Coins earned per level → skin shop (bolt designs, nut finishes, background themes). Cosmetic-only progression — no power gates. Daily challenge system with streak multipliers. Seasonal skin drops tied to real-world events (future scope).

### Retention Hooks

- **Curiosity**: Next skin pack visible but locked — player knows exactly how many levels/coins away they are
- **Investment**: Level progress and skin collection feel like real accumulation; resetting would mean real loss
- **Social**: Optional daily leaderboard (level count, not score) — competition without pressure
- **Mastery**: Star ratings per level; "perfect" playthroughs (no hints) for completionists

---

## Game Pillars

### Pillar 1: Flow Over Friction

Every interaction is effortless. If a player feels stuck or frustrated, the design has failed — not the player.

*Design test*: "If we're debating between a challenging move constraint and a smooth experience, we choose smooth — unless the difficulty itself IS the satisfaction."

### Pillar 2: Every Pixel Earns Its Place

Visual and audio feedback must be intentional and satisfying. Nothing is decoration. Every animation, sound, and particle either amplifies the satisfaction loop or gets cut.

*Design test*: "Remove this effect — does the game feel worse without it? If not, it's cut."

### Pillar 3: Respect the Session

Players have 2–5 minutes. Every feature must serve the short-session player first. Long sessions are a gift from the player, not an expectation.

*Design test*: "Can a player get a complete, satisfying experience in under 3 minutes? If not, redesign."

### Pillar 4: Cosmetic, Not Coercive

Monetization never blocks progress. Players always feel in control of when and whether to spend. The game is complete without spending a cent.

*Design test*: "If we're debating whether to gate a feature, we don't gate it. Rewarded ads offer bonuses, not removal of penalties."

### Pillar 5: The Machine Must Sing

The sci-fi aesthetic isn't decoration — every level is a mechanical symphony resolving into order. The game should feel like a living system, not a toy.

*Design test*: "Does this feature feel like it belongs in a functioning sci-fi workshop? If it feels cartoony or random, reconsider."

### Anti-Pillars (What This Game Is NOT)

- **NOT time-pressured**: No countdown timers, no speed challenges. Timers fight Flow Over Friction.
- **NOT pay-to-win**: No mechanics, levels, or advantages locked behind payment. Violates Cosmetic, Not Coercive.
- **NOT narrative-driven**: No story, no characters, no cutscenes. Story-telling breaks Respect the Session.
- **NOT visually cluttered**: No mascots, no excessive UI chrome, no banner ads interrupting gameplay. Violates Every Pixel Earns Its Place.
- **NOT multiplayer/PvP**: Social pressure contradicts the relaxation goal. Not in scope.

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| Ball Sort Puzzle | Core sort mechanic, level structure, temp slot design | Sci-fi aesthetic, skin shop, non-aggressive monetization | Proves the mechanic has massive mobile audience (50M+ downloads) |
| Screw Sort 3D | Bolt/nut theming, 3D component feel | 2D with shader glow (more performant on low-end), cleaner UI | Validates the bolt metaphor specifically |
| Candy Crush Saga | Level progression, coin economy, daily goals, streak rewards | No energy gates, no pay-to-continue, no social pressure | Proves reward loop and daily habit design at scale |
| Clash Royale | Short session design, reward anticipation, progression clarity | No PvP, no real-money gambling — purely cosmetic | Proves "just one more" psychology at short session lengths |

**Non-game inspirations**: Sci-fi UI design (Dead Space HUD, Mass Effect menus), precision manufacturing aesthetics (watch mechanics, circuit board photography), ASMR satisfying video genre (color sorting, assembly lines).

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 18–45 |
| **Gaming experience** | Casual — plays mobile games, may not identify as a "gamer" |
| **Time availability** | Short bursts: commute, lunch break, before sleep. 2–10 min sessions. |
| **Platform preference** | Mobile (iOS or Android primary device) |
| **Current games they play** | Ball Sort Puzzle, Candy Crush, Merge Mansion, or similar |
| **What they're looking for** | Stress relief, a sense of accomplishment, something to do with their hands |
| **What would turn them away** | Aggressive ads, energy systems, pay-to-continue gates, visual noise |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | Unity — best mobile ad ecosystem (AdMob, Unity LevelPlay), strongest IAP tooling for iOS/Android, C# well-suited for sort mechanic logic |
| **Key Technical Challenges** | Shader-based glow on low-end Android (quality tiers needed); ad integration across iOS + Android; level design pipeline at 200+ levels |
| **Art Style** | 2D with shader-based glow (Shader Graph). Sprite assets for bolts/nuts. Dark background with bloom pass. |
| **Art Pipeline Complexity** | Medium — custom 2D sprites + Shader Graph materials + particle system per stack |
| **Audio Needs** | Moderate — ambient machine hum, per-move click/settle SFX, completion chime, UI sounds |
| **Networking** | None (offline-first). Optional leaderboard via Game Center / Google Play Games (future scope). |
| **Content Volume** | Launch: 200 levels, 10+ bolt skins, 5+ backgrounds, 3–5 daily challenges/week |
| **Procedural Systems** | None at MVP. Level editor tool (internal) may be needed by month 3 to accelerate level authoring. |

---

## Risks and Open Questions

### Design Risks

- Core loop fatigue — sort games can feel repetitive at scale. Mitigation: introduce new mechanic wrinkles every 30–50 levels (locked stacks, fixed bolts, color-blind bolt types).
- Difficulty cliff — players dropping off if a single hard level blocks them. Mitigation: breather levels every 10, generous hint system, no energy gate.

### Technical Risks

- Shader glow performance on budget Android devices — bloom is GPU-heavy. Mitigation: quality tier system (Low/Medium/High), auto-detected on first launch.
- Ad + IAP integration complexity — AdMob + Google Play Billing + App Store IAP across both platforms can eat 1–2 weeks. Mitigation: schedule as a dedicated sprint early.

### Market Risks

- Genre saturation — Ball Sort and Water Sort dominate the category. Differentiation via polish alone is risky. Mitigation: sci-fi aesthetic is genuinely underrepresented; focus on store page and early screenshots as a conversion lever.
- User acquisition cost — mobile puzzle CPIs are rising. Mitigation: organic-first (ASO), rewarded ad monetization reduces pressure on UA spend.

### Scope Risks

- Level content volume — 200 hand-crafted levels is significant work. Mitigation: build internal level editor by month 3; establish level design pipeline early.
- Polish scope creep — "just one more animation" is a real risk when the pillar is Every Pixel Earns Its Place. Mitigation: animation budget per feature, defined in design docs.

### Open Questions

- What's the right number of bolt colors per level at each difficulty tier? — Answer via playtest of MVP levels 1–30.
- Does the "generous" temp slot design (2 slots) make levels too easy? — Answer via blind playtesting of first 10 levels.
- What conversion rate can we expect from rewarded ads → skin shop purchases? — Answer via soft launch data.

---

## MVP Definition

**Core hypothesis**: Players find the bolt sort loop engaging and satisfying in isolation — they want to keep playing without any rewards, shop, or progression system.

**Required for MVP**:
1. Sort mechanic fully functional — tap to lift, tap to drop, valid/invalid move detection, win condition detection
2. 10 hand-crafted levels (3 colors → 6 colors, increasing difficulty)
3. Smooth bolt lift/drop animation with basic glow
4. Level complete screen with a simple "Next Level" button

**Explicitly NOT in MVP** (defer to later):
- Coin economy and reward system
- Skin shop
- Rewarded ads
- Hint system
- Daily challenges
- Sound design (placeholder SFX acceptable)
- Onboarding tutorial

### Scope Tiers

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **MVP** | 10 levels, 3–6 colors | Core mechanic only, basic UI | 6–8 weeks |
| **Beta** | 100 levels, coin system | Rewarded ads, hint system, 5 bolt skins, AdMob | 3 months |
| **Launch** | 200+ levels, full skin shop | Daily challenges, polished UI, onboarding, store assets | 5–6 months |
| **Post-Launch** | New level packs | Seasonal skins, optional leaderboard, Game Center | Ongoing |

---

## Next Steps

- [ ] Run `/setup-engine` — configure Unity, populate version-aware reference docs
- [ ] Run `/art-bible` — establish visual identity before writing any GDDs (sci-fi clean direction is the anchor)
- [ ] Run `/design-review design/gdd/game-concept.md` — validate concept completeness
- [ ] Run `/map-systems` — decompose concept into individual systems with dependencies
- [ ] Run `/design-system` for each system — sort mechanic, progression, coin economy, skin shop, hint system, ad integration
- [ ] Run `/create-architecture` — master architecture blueprint
- [ ] Run `/gate-check pre-production` — validate readiness before committing to production
- [ ] Run `/prototype sort-mechanic` — validate the core loop before full implementation
