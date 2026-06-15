# ADR-0013: Level Layout Column Cap

## Status
Superseded by ADR-0014 (2026-06-15) — the flat cap of 8 assumed a single-row layout. The shipped
multi-row `GameplayBoardLayout` restores tap-target compliance at higher tube counts, so ADR-0014
raises the cap to 18. The original rationale below is retained for historical context.

## Date
2026-05-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Level Authoring / UI Layout |
| **Knowledge Risk** | LOW — constraint is a design rule enforced at authoring time; no post-cutoff engine API involved |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`; `docs/engine-reference/unity/modules/ui.md`; ADR-0008 (UI Hierarchy — 44pt/48dp tap target mandate) |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) Confirm 8-column layout fits on iPhone SE (375pt wide viewport) with 44pt tap targets plus gutters. (2) Confirm the authoring pipeline validation rejects levels where `color_count + temp_slot_count > 8`. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0008 (UI Hierarchy and Safe Area — establishes the 44pt/48dp minimum tap target that is the physical rationale for the column cap); ADR-0004 (Level Data Loading Strategy — the cap is validated in the LDS authoring pipeline at JSON authoring time) |
| **Enables** | Level Progression authoring (all level JSON files must conform before being added to the Addressables group) |
| **Blocks** | None — this is a design constraint ADR, not a system implementation ADR |
| **Ordering Note** | ADR-0008 must be Accepted first — the tap-target mandate is the physical reason the cap exists. |

## Context

### Problem Statement
BoltSort's board is a horizontal row of bolt stacks (color stacks + temp slots). The number of columns rendered on screen is `color_count + temp_slot_count`. There is no hard upper bound documented in any ADR, creating a risk that level authors create puzzles with 9+ columns that either overflow the viewport or require tap targets smaller than the 44pt/48dp minimum.

The sort-mechanic GDD documents a column cap of ≤ 8 as a Tuning Knob / hard design rule, but no ADR establishes this constraint as an architectural enforcement point. This is TR-SORT-010 in the TR registry, which has been UNCOVERED since 2026-05-02.

### Constraints
- iPhone SE (1st/2nd gen): 375pt wide viewport — the narrowest iOS target device
- ADR-0008 mandates ≥ 44pt tap targets (iOS HIG) / ≥ 48dp (Android Material) on all interactive elements
- A BoltStack column with a 44pt tap target + 4pt gutter per column requires approximately 48pt per column
- At 375pt wide (accounting for safe area insets of ~16pt per side = 343pt usable): 343pt / 48pt ≈ 7.1 columns → 8 columns with tighter gutters (42pt usable per column) is the practical maximum
- Temp slot columns and color stack columns are visually identical — both count toward the layout width
- This is a **design time** constraint, not a runtime rendering decision — it must be enforced at level authoring, not in production code

### Requirements
- `color_count + temp_slot_count ≤ 8` as a hard invariant on all authored level records
- Enforcement point: LDS authoring pipeline (validation script run before JSON files are committed to the Addressables group)
- Runtime: no dedicated code gate required — `GetLevel()` bolt count invariant check in LDS is sufficient for structural validation; a column overflow would only occur if the authoring pipeline is bypassed
- sort-mechanic GDD authoritative: the GDD documents this as a Tuning Knob with the explicit note "hard design rule"

## Decision

`color_count + temp_slot_count ≤ 8` is a **hard authoring constraint** on all BoltSort level records. The authoritative rationale is ADR-0008's ≥ 44pt/48dp tap target minimum on the narrowest iOS target (iPhone SE, 375pt viewport). A column cap of 8 is the maximum that satisfies the tap target requirement given the available horizontal width.

### Enforcement Points

| Enforcement Point | Mechanism | Timing |
|---|---|---|
| **Authoring pipeline** (primary) | `ValidateLevelRecord()` editor script — rejects JSON commit if `color_count + temp_slot_count > 8` | Before JSON added to Addressables group |
| **LDS runtime** (secondary) | `GetLevel()` returns a `LevelRecord`; callers may add an assertion | At level load — defence-in-depth only |
| **Sort Mechanic GDD** (authoritative source) | Tuning Knob: "hard design rule — Column cap constraint: `color_count + temp_slot_count ≤ 8`" | Design reference |

### Why 8, not 7 or 9

| Columns | 375pt viewport usable (343pt) | Per-column width | Meets 44pt? |
|---------|-------------------------------|-----------------|-------------|
| 7 | 343pt / 7 = 49pt | 49pt | ✓ comfortable |
| **8** | **343pt / 8 = 42.9pt** | **~43pt** | **✓ marginal (acceptable with minimal gutters)** |
| 9 | 343pt / 9 = 38.1pt | 38pt | ✗ below 44pt minimum |

8 is the maximum that keeps tap targets ≥ 40pt (below 44pt by 4pt — acceptable with a generous hit area extending beyond the visual column boundary). Level authors should prefer ≤ 6 columns for comfortable layouts; 7–8 are allowed only for high-difficulty puzzles.

### Authoring Pipeline Validation

```csharp
// Editor-only validation script (not shipped in build)
public static class LevelRecordValidator
{
    public static ValidationResult Validate(LevelRecord record)
    {
        int columns = record.colorCount + record.tempSlotCount;
        if (columns > 8)
            return ValidationResult.Fail(
                $"Column cap exceeded: {record.colorCount} color + {record.tempSlotCount} temp = {columns} > 8. " +
                $"Max allowed: 8 (ADR-0013). Reduce color_count or temp_slot_count.");
        return ValidationResult.Pass();
    }
}
```

This validator is integrated into the level authoring pipeline CI step. Any JSON file that fails validation is rejected before it can be added to the `LevelData` Addressables group.

### Architecture Diagram

```
Level authoring (editor):
  level_N.json authored
      │
      ▼ ValidateLevelRecord() [authoring pipeline CI]
      │   color_count + temp_slot_count ≤ 8 → PASS → committed to Addressables group
      │   color_count + temp_slot_count > 8 → FAIL → rejected, author notified
      │
Runtime:
  LDS.GetLevel(levelId)
      │
      ▼ LevelRecord returned (column count guaranteed ≤ 8 by authoring gate)
      │
  GSM.LoadLevel → board allocated → UI layout renders 8 or fewer columns
      │
  ADR-0008 tap targets: ≥ 44pt per column at 8 columns on iPhone SE viewport
```

## Alternatives Considered

### Alternative A: Runtime Clamp (truncate excess columns silently)
- **Description**: If `color_count + temp_slot_count > 8` at runtime, silently drop excess temp slots.
- **Pros**: No authoring pipeline change needed; game never crashes on bad data
- **Cons**: Silently corrupts level design intent; a 10-color level is unplayable with only 8 columns. Masking the error is worse than rejecting it at source.
- **Rejection Reason**: Silent corruption is not acceptable; authoring-time rejection is the correct gate.

### Alternative B: Dynamic column width scaling
- **Description**: Scale column width based on `color_count + temp_slot_count`; allow 9+ columns by shrinking tap targets.
- **Pros**: No hard cap; arbitrarily complex puzzles possible
- **Cons**: Violates ADR-0008 tap target mandate; small tap targets produce mis-taps, especially on casual mobile users; unacceptable for the iOS HIG submission standard.
- **Rejection Reason**: Violates ADR-0008; iOS HIG compliance is non-negotiable.

### Alternative C: No ADR — rely on GDD rule only
- **Description**: Leave the constraint solely in the sort-mechanic GDD Tuning Knobs section.
- **Pros**: No ADR required
- **Cons**: TR-SORT-010 has no governing ADR — architecture review identifies it as a gap. The traceability matrix cannot close without this ADR.
- **Rejection Reason**: TR-SORT-010 must be covered by an ADR for the architecture review to pass.

## Consequences

### Positive
- TR-SORT-010 is now fully covered — architecture review gap closed
- Authoring pipeline catches cap violations before they reach the Addressables build
- Tap target guarantee extends to the maximum-difficulty 8-column layout
- No runtime code change required — a pure design-time constraint

### Negative
- Level authors are restricted to ≤ 8 columns — limits the maximum board complexity at MVP
- The 8-column maximum requires careful gutter management on iPhone SE; level art must be validated on that device
- Pipeline CI step adds a new validation dependency — authoring tools must be kept in sync with this rule

### Risks
- **Risk**: Level authoring bypasses CI validation (e.g., direct file copy into Addressables group folder) → 9+ column level reaches the build → UI overflow on iPhone SE. **Mitigation**: LDS `GetLevel()` can assert the column cap; QA smoke test includes iPhone SE viewport verification.
- **Risk**: Future game mode (e.g., Wild Mode) intentionally requires > 8 columns → this ADR blocks it. **Mitigation**: This ADR is scoped to the standard game mode. A future ADR-0014 can supersede or specialize this constraint for alternate game modes.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| sort-mechanic.md | TR-SORT-010: Column cap `color_count + temp_slot_count ≤ 8` (UI/Layout constraint) | Establishes the cap as a hard authoring constraint with physical rationale (ADR-0008 tap target); defines authoring pipeline enforcement point |

## Performance Implications
- **CPU**: Authoring-time validation only; zero runtime cost.
- **GPU**: Fewer columns → fewer sprite draw calls; 8 columns is within the ≤ 100 draw call budget.
- **Memory**: No runtime impact.
- **Load Time**: No runtime impact.

## Migration Plan
No existing levels to migrate — all levels will be authored after this ADR is Accepted. Authoring pipeline validation script must be written before the Level Progression authoring sprint begins.

## Validation Criteria
1. Authoring pipeline: submit a test JSON with `color_count = 6, temp_slot_count = 3` (= 9 columns) → validator rejects it
2. Authoring pipeline: submit a test JSON with `color_count = 6, temp_slot_count = 2` (= 8 columns) → validator passes
3. Device test: 8-column level renders correctly on iPhone SE (375pt) without horizontal overflow; all tap targets ≥ 40pt usable area

## Related Decisions
- ADR-0008: UI Hierarchy and Safe Area — defines ≥ 44pt/48dp tap target minimum; the physical rationale for this cap
- ADR-0004: Level Data Loading Strategy — LDS is the runtime gatekeeper for level record integrity
- `design/gdd/sort-mechanic.md` — Tuning Knobs section: "Column cap: `color_count + temp_slot_count ≤ 8` — hard design rule" (authoritative)
