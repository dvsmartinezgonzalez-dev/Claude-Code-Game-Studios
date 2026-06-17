# Work Log — Header Redesign + Extra Tube Mechanic

<!--
═══════════════════════════════════════════════════════════════════════════
PHASE 0 — AUDIT FINDINGS (read-only; no code changed)
═══════════════════════════════════════════════════════════════════════════

KEY ARCHITECTURAL FACT: All gameplay UI is built PROCEDURALLY in C#, not as
serialized GameObjects in Gameplay.unity. The scene only hosts GameBootstrap,
which spawns Board (BoardView), HUD (HUDController), etc. So "scene hierarchy"
edits = code edits in HUDController/BoardView. No .unity/.prefab surgery needed.

1. HEADER / BOTTOM BAR / BOARD CONTAINER
   - Header  = "TopBar" panel, built in HUDController.BuildUI() (HUDController.cs
     ~L506-567). Children: LevelText (centered), MovesText (top-right), CoinPill
     (below moves). Anchored to top, height topBarH=110 logical px + safeTop.
     Background color = BoltSortTheme.HUDBackground (semi-opaque) + 2px BarBorder.
   - Settings button: built separately on the root Canvas, top-left corner
     (HUDController.cs ~L619-629), opens SettingsPanel.
   - Bottom bar = "BottomBar" panel (HUDController.cs ~L581-617). Children:
     ResetButton (_onReset), UndoButton (_onUndo), MenuButton (_onMenu).
     Height botBarH=140 logical px + safeBottom.
   - Board/tube container = "Board" GameObject (BoardView). World-space sprites,
     NOT canvas UI. Columns rebuilt in BoardView.RebuildColumns().

2. SCRIPTS
   - Retry/Undo/Home logic: actions are owned by GameBootstrap (ResetLevel,
     OnUndoClicked->_gsm.UndoRequested(), OnMenuClicked) and passed into
     HUDController.Initialize() as Action callbacks. HUD buttons just invoke them.
   - Settings popup: SettingsPanel.cs (built in code; modal overlay).
   - Board layout (placement/scale/rows): GameplayBoardLayout.cs (pure math) +
     BoardView.RebuildColumns() which consumes it.
   - Tube creation at runtime: BoardView.RebuildColumns() (one Column_i GameObject
     per GSM flat column; tube sprite via GameAssets.TubeSprite(depth, selected)).

3. HEADER STATE
   - Semi-transparent bg = the TopBar's own Image (BoltSortTheme.HUDBackground).
   - Anchors: top-stretch (anchorMin (0,1) max (1,1)), height = topBarH + safeTop.

4. BOARD LAYOUT
   - Play area is WORLD-SPACE, decoupled from the HUD canvas. In
     BoardView.RebuildColumns(): totalH = 2*camHalfH; hudH = 0.10*totalH (top
     reserve); buttonH = 0.20*totalH (bottom reserve); boardTop = camHalfH - hudH;
     boardBot = -camHalfH + buttonH. The board NEVER reads the bottom bar's
     RectTransform — it reserves fixed FRACTIONS of camera height. So "recovering
     bottom-bar space" = shrinking the bottom reserve fraction (kept as a named
     fraction, not a hardcoded pixel). Tubes rescale automatically.

EXTRA-TUBE INTEGRATION DECISION (gates Phase 3):
   SortMechanic reads board shape LIVE from GSM every evaluation
   (StackContents/TempSlotContents/ColorCount/TempSlotCount/GetColumnCapacity).
   IsWon() and IsLegalMove() iterate colorCount+tempSlotCount generically.
   => A helper tube = a TEMP-SLOT column appended at the END of the flat
      namespace. Appending at the end means NO existing flat index shifts, so
      undo entries (store From/To flat indices) stay valid, and movement / color
      / win / completion logic is REUSED UNCHANGED (satisfies the "do not change
      ball movement / color matching / tube completion detection" constraint).
   => New GSM API (additive): AddHelperTube(capacity), GrowLastHelperTube(),
      RemoveHelperTubes(), HelperTubeCount, materializing _columnCapacities from
      the uniform fallback on first add so per-tube capacity is exact.
   MAX_CAPACITY = _gsm.StackDepth (standard color-stack depth of current level).
   BoardView rebuilds columns on a helper-tube-changed event; helper columns
   (index >= colorCount + originalTempSlotCount) get a distinct blue tint.

CONSTRAINT CHECK: this approach touches GSM (heavily tested) ADDITIVELY only;
existing methods/tests untouched. Save/restore already keys off _tempSlotCount so
helper tubes survive pause for free (spec only requires reset on retry/win).
-->

## Progress
- [x] Phase 0 — audit (this block)
- [x] Phase 1 — remove bottom bar, recover board space (commit: hide BottomBar, BottomReserveFrac 0.20→0.05)
- [x] Phase 2 — redesign header + settings Shop/Levels + undo budget (GSM.CanUndo)
- [x] Phase 3 — extra tube mechanic (GSM.ApplyExtraTube + OnBoardShapeChanged + BoardView relayout)
- [x] Phase 4 — integration & safety checks

## Phase 4 — verification notes (no Unity runtime available here)
- Win condition: SortMechanic.IsWon() iterates colorCount+tempSlotCount live from GSM →
  helper tubes (extra temp slots) are included with ZERO SortMechanic changes. Mixed
  helper blocks win; empty helper skipped; full+uniform helper counts complete. ✓ by design.
- Retry: ResetLevel → ExitLevel (ClearAllState zeroes _helperTubeCount, arrays null) →
  LoadLevel (reallocates original arrays, _helperTubeCount=0). HUD resets undo/extra
  budgets in OnLevelLoaded. BoardView resets _originalTempSlotCount + rebuilds. ✓
- Undo with helpers: helper moves are ordinary undo entries (helper is a real column at a
  fixed high index → no shift). Tube ADD pushes no undo entry → not undoable. Covered by
  unit test test_gsm_apply_extra_tube_preserves_existing_indices_and_undo. ✓
- Settings popup: Shop/Levels options added only when wired (gameplay HUD); MainMenu popup
  unchanged. Navigation via SceneTransitionManager.TransitionTo("Shop"/"LevelSelect"). ✓
- Layout: GameplayBoardLayout.RowsForColumnCount handles 2 / 6 / 7+ standard tubes and any
  added helpers (re-rows at >6, >10, >14). Warning logged past 18 columns; still added. ✓
- Added EditMode unit test HelperTube_Test.cs (4 tests) for ApplyExtraTube state mutations.
- KNOWN MINOR LIMITATION: app-pause snapshot persists helper columns in data but does not
  serialize _helperTubeCount; after resume the next extra-tube tap starts a new helper
  instead of growing the last. SaveSystem intentionally left untouched (constraint). Spec
  only requires reset on retry/win, which work correctly.
