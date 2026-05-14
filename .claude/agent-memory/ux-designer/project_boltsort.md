---
name: BoltSort Project Identity
description: Core identity, pillars, audience, and platform context for BoltSort — the project this agent works on
type: project
---

BoltSort is a mobile casual sort puzzle for iOS and Android (F2P). Players sort color-coded bolts into matching columns using a tap-only interaction model. Engine: Unity 6.3 LTS (URP, 2D Renderer). Target audience: casual players 18–45, short sessions 2–10 min.

**Why:** This context should inform every UX decision — interaction patterns, accessibility standards, onboarding, and feedback design all serve a casual, short-session mobile player.

**How to apply:** Default all UX recommendations toward minimal friction, one-handed thumb play, and forgiving input tolerances. The "Flow Over Friction" pillar is the primary UX override when tradeoffs arise.

## Game Pillars
1. Flow Over Friction — every interaction must be effortless; frustration is a design failure
2. Every Pixel Earns Its Place — all visual/audio feedback must be intentional; nothing decorative
3. Respect the Session — 2–5 min session design; short session player comes first
4. Cosmetic, Not Coercive — monetization never blocks progress
5. The Machine Must Sing — sci-fi aesthetic; game feels like a living system

## Interaction Model
- Single-bolt, two-tap: tap to lift topmost bolt, tap destination to place
- Strictly tap-only (no drag) — deliberate design decision
- Tap source or empty space while holding = cancellation
- Tap wrong destination while holding = bolt stays in hand (no cancel), try again

## Target Platforms
- iOS and Android mobile
- Primary input: touch (tap)
- Unity UGUI Canvas, Screen Space - Overlay

## Comparable Titles
Ball Sort Puzzle, Screw Sort 3D, Water Sort Puzzle
