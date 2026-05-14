# Systems Index: BoltSort

> **Status**: Draft
> **Created**: 2026-04-16
> **Last Updated**: 2026-05-10
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

BoltSort is a mobile sort puzzle with F2P monetization. Its mechanical scope spans a tight core gameplay loop (tap-lift-drop bolt sorting), a coin economy that feeds a cosmetic skin shop, rewarded ad integration, and the meta systems that drive daily retention (daily challenges, streaks, progression). The system set is intentionally constrained — pillars "Flow Over Friction" and "Respect the Session" prevent feature sprawl. Every system either directly serves the 30-second sort loop or supports the reward/retention spine that brings players back. Foundation systems (Level Data, Save & Persistence) are the highest-risk bottlenecks and must be designed and locked before anything else is built on top of them.

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|---|---|---|---|---|---|
| 1 | Level Data System | Core | MVP | Approved | design/gdd/level-data-system.md | — |
| 2 | Sort Mechanic | Gameplay | MVP | Approved | design/gdd/sort-mechanic.md | Level Data System, Game State Manager |
| 3 | Game State Manager | Core | MVP | Approved | design/gdd/game-state-manager.md | Level Data System |
| 4 | Quality Tier System | Technical | MVP | Designed | design/gdd/quality-tier-system.md | — |
| 5 | Audio System | Audio | MVP | Designed | design/gdd/audio-system.md | — |
| 6 | Animation System | Presentation | MVP | Designed | design/gdd/animation-system.md | Game State Manager, Skin System, Audio System, Quality Tier System |
| 7 | In-Game HUD | UI | MVP | Needs Revision | design/gdd/in-game-hud.md | Game State Manager, Hint System, Coin Economy, Animation System |
| 8 | Level Complete UI | UI | MVP | Designed | design/gdd/level-complete-ui.md | Level Progression, Coin Economy, Rewarded Ad System, Game State Manager, Level Data System |
| 9 | Save & Persistence | Core | Beta | Approved | design/gdd/save-persistence.md | — |
| 10 | Coin Economy | Economy | Beta | Approved | design/gdd/coin-economy.md | Save & Persistence |
| 11 | Rewarded Ad System | Economy | Beta | Not Started | — | — |
| 12 | Hint System | Gameplay | Beta | Not Started | — | Game State Manager, Coin Economy, Rewarded Ad System |
| 13 | Skin System | Content | Beta | Not Started | — | Save & Persistence, IAP System |
| 14 | Level Progression | Meta | Beta | Designed | design/gdd/level-progression.md | Level Data System, Save & Persistence, Coin Economy, Game State Manager |
| 15 | Main Menu UI | UI | Beta | Not Started | — | Level Progression, Coin Economy, Daily Challenge System |
| 16 | Level Select UI | UI | Beta | Not Started | — | Level Progression, Save & Persistence |
| 17 | Shop UI | UI | Beta | Not Started | — | Skin System, Coin Economy, IAP System |
| 18 | IAP System | Economy | Launch | Not Started | — | — |
| 19 | Daily Challenge System | Meta | Launch | Not Started | — | Level Data System, Level Progression, Save & Persistence |
| 20 | Tutorial System | Onboarding | Launch | Not Started | — | Level Data System, Game State Manager, Level Progression |
| 21 | Settings UI | UI | Launch | Not Started | — | Audio System, Quality Tier System, Save & Persistence |
| 22 | Analytics System | Technical | Launch | Not Started | — | — |

---

## Categories

| Category | Description | BoltSort Systems |
|---|---|---|
| **Core** | Foundation systems everything depends on | Level Data System, Game State Manager, Save & Persistence |
| **Gameplay** | Systems that define the fun | Sort Mechanic, Hint System |
| **Meta** | Systems outside the core loop driving retention | Level Progression, Daily Challenge System |
| **Economy** | Resource creation and consumption | Coin Economy, Rewarded Ad System, IAP System |
| **Content** | Player-facing cosmetic content | Skin System |
| **Onboarding** | First-session guidance | Tutorial System |
| **Presentation** | Visual and audio feedback | Animation System |
| **Audio** | Sound and music | Audio System |
| **UI** | Player-facing screens and HUDs | In-Game HUD, Level Complete UI, Main Menu UI, Level Select UI, Shop UI, Settings UI |
| **Technical** | Infrastructure and platform | Quality Tier System, Analytics System |

---

## Priority Tiers

| Tier | Definition | BoltSort Milestone | Design Urgency |
|---|---|---|---|
| **MVP** | Core sort loop must function and feel good | 6–8 weeks — tests "is the sort loop fun?" | Design FIRST |
| **Beta** | Coin economy, rewarded ads, skins, level progression — F2P loop live | 3 months | Design SECOND |
| **Launch** | IAP, daily challenges, tutorial, settings, analytics | 5–6 months | Design THIRD |
| **Post-Launch** | Seasonal skins, leaderboard, new level packs | Ongoing | Design as needed |

---

## Dependency Map

### Foundation Layer (no dependencies — design and build first)

1. **Level Data System** — defines the bolt config format everything loads from; without it no level can exist
2. **Save & Persistence** — serializes all player state; must exist before any economy or progression system stores data
3. **Audio System** — standalone SFX manager; animation system references it for sync
4. **Rewarded Ad System** — external SDK integration; hint system and coin economy depend on ad callbacks
5. **IAP System** — external SDK integration; skin system and shop UI depend on purchase confirmations
6. **Quality Tier System** — device performance detection; must run at startup before any shader/animation code executes

### Core Layer (depends on Foundation only)

1. **Game State Manager** — depends on: Level Data System. Owns board state, move history (undo), win detection. Everything interactive depends on it.
2. **Coin Economy** — depends on: Save & Persistence. Earn/spend rules, star rating formula, coin balance persistence.
3. **Skin System** — depends on: Save & Persistence, IAP System. Cosmetic data, equip state, rendering application.

### Feature Layer (depends on Core + Foundation)

1. **Sort Mechanic** — depends on: Game State Manager, Level Data System. The core tap-lift-drop rules including move validation and win condition.
2. **Hint System** — depends on: Game State Manager, Coin Economy, Rewarded Ad System. Solver algorithm + cost deduction.
3. **Level Progression** — depends on: Level Data System, Save & Persistence, Coin Economy, Game State Manager. Level sequence, unlock flow, win event → coin reward.
4. **Animation System** — depends on: Game State Manager, Skin System, Audio System, Quality Tier System. Bolt motion, glow transitions, completion bursts.
5. **Daily Challenge System** — depends on: Level Data System, Level Progression, Save & Persistence. Daily level selection, streak tracking.
6. **Tutorial System** — depends on: Level Data System, Game State Manager, Level Progression. Gesture overlays for levels 1–5.

### Presentation Layer (UI + analytics — designed last)

1. **Main Menu UI** — depends on: Level Progression, Coin Economy, Daily Challenge System
2. **In-Game HUD** — depends on: Game State Manager, Hint System, Coin Economy, Animation System
3. **Level Select UI** — depends on: Level Progression, Save & Persistence
4. **Level Complete UI** — depends on: Level Progression, Coin Economy, Rewarded Ad System
5. **Shop UI** — depends on: Skin System, Coin Economy, IAP System
6. **Settings UI** — depends on: Audio System, Quality Tier System, Save & Persistence
7. **Analytics System** — leaf node; listens to all systems, nothing depends on it

---

## Recommended Design Order

| Order | System | Priority | Layer | Est. Effort |
|---|---|---|---|---|
| 1 | Level Data System | MVP | Foundation | S |
| 2 | Sort Mechanic | MVP | Feature | M |
| 3 | Game State Manager | MVP | Core | M |
| 4 | Quality Tier System | MVP | Foundation | S |
| 5 | Audio System | MVP | Foundation | S |
| 6 | Animation System | MVP | Feature | M |
| 7 | In-Game HUD | MVP | Presentation | S |
| 8 | Level Complete UI | MVP | Presentation | S |
| 9 | Save & Persistence | Beta | Foundation | M |
| 10 | Coin Economy | Beta | Core | M |
| 11 | Rewarded Ad System | Beta | Foundation | S |
| 12 | Hint System | Beta | Feature | M |
| 13 | Skin System | Beta | Core | S |
| 14 | Level Progression | Beta | Feature | M |
| 15 | Main Menu UI | Beta | Presentation | S |
| 16 | Level Select UI | Beta | Presentation | S |
| 17 | Shop UI | Beta | Presentation | S |
| 18 | IAP System | Launch | Foundation | M |
| 19 | Daily Challenge System | Launch | Feature | S |
| 20 | Tutorial System | Launch | Feature | M |
| 21 | Settings UI | Launch | Presentation | S |
| 22 | Analytics System | Launch | Presentation | S |

*Effort: S = 1 session (~1–2 hours), M = 2–3 sessions, L = 4+ sessions.*

*Parallel design note: Systems at the same layer with no shared dependencies can be designed in parallel. E.g., Audio System and Quality Tier System (both Foundation, no overlap) can be designed simultaneously.*

---

## Circular Dependencies

None found.

**Closest risk**: Level Progression and Coin Economy interact bidirectionally (level complete → coins awarded; coins spent → hints/skins). Resolved by event/callback boundary: Level Progression emits `LevelCompleted(stars: int, level_id: int, move_count: int, par_moves: int)`; Coin Economy subscribes. Neither hard-depends on the other's interface at the design level.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|---|---|---|---|
| Game State Manager | Design + Technical | Bottleneck — 6 systems depend on it. If the state model is wrong, downstream systems break. | Design GDD before Sort Mechanic. Lock the move history and win-detection API surface early. |
| Level Data System | Design | Data contract affects all 22 systems. A schema change after Beta is expensive. | Design the schema first. Validate with 10 MVP levels before Beta work begins. |
| Sort Mechanic | Design | The hint solver (optimal-next-move algorithm) is non-trivial — backtracking search on a variable-depth stack. May be slow on large boards. | Prototype the solver in isolation before the Beta milestone. Define board size limits per difficulty tier. |
| Save & Persistence | Technical | Mobile save serialization has platform-specific pitfalls (iOS file protection, Android backup rules, cloud save conflicts). | Design the schema conservatively. Add versioning from day one to handle future schema migrations. |
| Animation System | Scope | "Every Pixel Earns Its Place" creates scope creep risk — one more animation per feature adds up fast. | Define an animation budget per system in the GDD. No animation added outside the budget without explicit approval. |
| Rewarded Ad System | Technical | AdMob fill rates, GDPR consent flow, and testing on both platforms are time-consuming. | Schedule as a dedicated sprint. Use Unity Ads for testing (same SDK). Budget 1–2 weeks for platform certification. |

---

## Progress Tracker

| Metric | Count |
|---|---|
| Total systems identified | 22 |
| Design docs started | 11 |
| Design docs in review | 0 |
| Design docs approved | 6 (Level Data System, Sort Mechanic, Game State Manager, In-Game HUD, Save & Persistence, Coin Economy) |
| MVP systems designed | 8 / 8 |
| Beta systems designed | 4 / 9 |
| Launch systems designed | 0 / 5 |

---

## Next Steps

- [ ] Design MVP systems in order (run `/design-system level-data-system` first)
- [ ] Run `/design-review design/gdd/[system].md` after each GDD is authored
- [ ] Prototype Sort Mechanic solver algorithm early — validate hint system feasibility
- [ ] Run `/gate-check pre-production` when all MVP GDDs are authored and reviewed
- [ ] Update the Progress Tracker above as each system moves from Not Started → Approved
