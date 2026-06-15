# ADR-0014: Multi-Row Layout Column Cap (raises cap to 18)

## Status
Accepted (supersedes ADR-0013)

## Date
2026-06-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Level Authoring / UI Layout |
| **Knowledge Risk** | LOW — pure design/layout constraint; no post-cutoff engine API involved |
| **References Consulted** | ADR-0013 (the cap this supersedes); ADR-0008 (44pt/48dp tap-target mandate); `My project/Assets/_Project/Scripts/Gameplay/GameplayBoardLayout.cs`; `GameplayBoardLayout_Test.cs` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) Confirm an 18-tube level (4 rows) keeps tap targets ≥ 40pt usable on iPhone SE (375pt). (2) Confirm `GameplayBoardLayout` distributes ≤18 tubes across ≤4 rows with no horizontal/vertical overflow (covered by `GameplayBoardLayout_Test` up to 20 columns). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Supersedes** | ADR-0013 (Level Layout Column Cap — flat cap of 8) |
| **Depends On** | ADR-0008 (tap-target minimum — still the physical rationale, now satisfied per-row instead of per-board); ADR-0004 (LDS authoring pipeline — enforcement point unchanged) |
| **Enables** | Phase-2 level progression (levels 81–200) which require multi-row boards of 8–18 tubes; see `design/gdd/phase2-technical-design.md` §1.5, §3 |
| **Ordering Note** | ADR-0013 must already be Accepted (now Superseded) for the supersession chain to be valid. |

## Context

ADR-0013 set a hard cap of `color_count + temp_slot_count ≤ 8`. Its rationale was that a
**single horizontal row** of 9+ tubes on the narrowest iOS target (iPhone SE, 375pt) forces tap
targets below the 44pt/48dp minimum (ADR-0008).

Since ADR-0013 was accepted, `GameplayBoardLayout` shipped a **responsive multi-row layout**
(`RowsForColumnCount`: ≤6→1 row, 7–10→2, 11–14→3, 15+→4 rows; `DistributeColumns` balances tubes
across rows). Multiple rows restore per-tube width at high tube counts: 18 tubes across 4 rows is
≤5 tubes per row, which is *wider* per tube than the old 8-in-one-row maximum. The single-row width
premise behind the cap of 8 therefore no longer holds. The cap of 8 now blocks the entire Phase-2
progression (levels 81+ need 8–18 tubes) for a reason that the layout engine has already solved.

`GameplayBoardLayout_Test` already exercises layouts up to 20 columns (4 rows) and asserts: balanced
distribution, non-deformed (square) bolts, horizontal containment within the side margin, and rows
filling the play band without spilling into HUD/buttons.

## Decision

Raise the column cap to **`color_count + temp_slot_count ≤ 18`**.

The tap-target guarantee (ADR-0008) is now satisfied by the **layout's row distribution** (≤4 rows ×
≤5 tubes/row) plus the `MinBoltDiameter` floor in `GameplayBoardLayout`, **not** by a flat
single-row column count. The cap of 18 is the ceiling of the existing row model (4 rows, the
recommended ≤5 tubes/row authoring rule for readability).

### Enforcement Points (unchanged from ADR-0013, value updated 8 → 18)

| Enforcement Point | Mechanism | Timing |
|---|---|---|
| Authoring pipeline (primary) | `LevelRecordValidator.Validate` (Editor) — rejects `> 18` columns | Before JSON committed |
| Runtime (defence-in-depth) | `SortMechanic` Assertion 4 (`MaxColumnCount = 18`) → `level_load_failed` if exceeded | At level load |
| Layout | `GameplayBoardLayout` distributes ≤18 tubes across ≤4 rows | At render |

### Authoring guidance
- Prefer ≤6 tubes (single row) for early/easy levels.
- 7–18 tubes are reserved for levels 81+ where multi-row boards are introduced (§3 of the Phase-2 TDD).
- Recommended ≤5 tubes per row for readability even though the math permits more.

## Alternatives Considered

- **Keep cap at 8** — rejected: blocks the entire Phase-2 progression for a premise (single-row
  width) the layout engine no longer obeys.
- **Unbounded cap with dynamic shrink** — rejected: beyond ~18 tubes the `MinBoltDiameter` clamp
  produces sub-44pt targets even across 4 rows; an explicit ceiling is safer than silent shrink.
- **Allow vertical scroll for >18 tubes** — rejected: scrolling breaks `Physics2D.OverlapPoint` tap
  resolution and is out of scope for 51–200.

## Consequences

### Positive
- Unblocks multi-row Phase-2 levels (81–200).
- Tap-target compliance now provably holds via the tested layout engine, not a hand-derived width.

### Negative
- Larger boards increase solver state space (colors × depth is the cost driver; mitigations in
  Phase-2 TDD §7).
- iPhone SE 4-row / 18-tube layouts must be QA-verified on device for tap comfort.

### Risks
- **Risk:** an 18-tube board at high depth approaches the 1.5M solver budget. **Mitigation:** keep
  distinct colors + temp slots ≤8 for the *search* (extra tubes are interchangeable empties the
  solver already collapses); see Phase-2 TDD §7.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| sort-mechanic.md | TR-SORT-010: column cap constraint | Redefines the cap as `≤ 18`, justified by the multi-row layout rather than a single-row width budget |
| level-progression / phase2-technical-design.md | §1.5, §3: levels 81+ require 8–18 tubes | Removes the cap-of-8 blocker gating those ranges |
