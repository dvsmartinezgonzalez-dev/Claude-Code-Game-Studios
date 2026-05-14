# Technical Preferences

## Engine & Language

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP) — 2D Renderer
- **Physics**: Physics 2D only (no 3D physics)

## Input & Platform

- **Target Platforms**: Mobile (iOS & Android)
- **Input Methods**: Touch (tap only — no drag, no gamepad)
- **Primary Input**: Touch
- **Gamepad Support**: None
- **Touch Support**: Full
- **Platform Notes**: All tap targets ≥ 44pt (iOS HIG) / 48dp (Android). Safe area handling required for notch and home indicator on all Canvas overlays. Android 16+ requires adaptive icons.

## Naming Conventions

- **Classes**: PascalCase (e.g., `SortMechanic`, `GameStateManager`)
- **Public properties/fields**: PascalCase (e.g., `MoveSpeed`, `StackDepth`)
- **Private fields**: `_camelCase` (e.g., `_currentBalance`, `_sequenceId`)
- **Methods**: PascalCase (e.g., `TakeDamage()`, `GetCoinBalance()`)
- **Signals/Events**: C# events with PascalCase name, `EventArgs` or typed delegates (e.g., `OnMoveCommitted`, `OnPuzzleSolved`)
- **Files**: PascalCase matching class (e.g., `SortMechanic.cs`, `GameStateManager.cs`)
- **Scenes**: PascalCase matching root scene purpose (e.g., `GameScene.unity`, `MainMenu.unity`)
- **Prefabs**: PascalCase matching component (e.g., `BoltStack.prefab`, `HUD.prefab`)
- **Constants**: PascalCase (e.g., `MaxStackDepth`, `WatchdogTimeoutMs`)

## Performance Budgets

- **Target Framerate**: 60fps
- **Frame Budget**: 16.6ms
- **Draw Calls**: ≤ 100 batches (use GPU Resident Drawer + sprite atlasing to stay under budget)
- **Memory Ceiling**: 512MB (mid-range Android target; profile on Samsung Galaxy A series)

## Testing

- **Framework**: NUnit (Unity Test Framework, built-in)
- **Minimum Coverage**: Logic and state machine tests BLOCKING before story Done
- **Required Tests**: Sort Mechanic state machine, GSM board mutations, win condition formula, CE earn/spend rules, SP atomic write
- **Test locations**: `tests/unit/[system]/`, `tests/integration/[system-pair]/`

## Forbidden Patterns

- `Object.FindObjectsOfType()` without sort mode — use `FindObjectsByType()` (removed in Unity 6.0)
- `[SerializeField]` on properties or methods — compile error in Unity 6.3; use backing fields or `[field: SerializeField]`
- URP `SetupRenderPasses` / Compatibility Mode — removed in Unity 6.3; all custom render features must use Render Graph
- Synchronous file I/O on main thread — use background thread or async/await for all save operations
- Hardcoded UI string text — all visible strings must use localization keys (`LocalizationTable` or `tr()` equivalent)
- Singleton `Instance` pattern via `FindObjectOfType` — register singletons in Script Execution Order via static reference

## Allowed Libraries / Addons

- Unity Test Framework (NUnit) — built-in, required
- Unity Addressables — asset loading and memory management
- Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) — JSON deserialization for LevelRecord/LevelCatalogue; required for nullable int?, private setters, and [JsonProperty] attribute mapping (ADR-0004)
- Unity Localization package — string management for future i18n
- AdMob (Google Mobile Ads Unity Plugin) — rewarded ads (Beta milestone)
- Unity IAP — in-app purchases (Launch milestone)

## Architecture Decisions Log

- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, URP/2D materials)
- **UI Specialist**: unity-ui-specialist (UGUI Canvas, UI Toolkit UXML/USS)
- **Additional Specialists**: unity-addressables-specialist (Addressables asset loading)
- **Routing Notes**: Invoke primary for architecture decisions, ADR validation, and C# code review. Invoke shader specialist for URP 2D materials, Shader Graph, bloom/glow effects. Invoke UI specialist for Canvas hierarchy, safe area handling, and UI animation. Invoke Addressables specialist for level data loading and asset memory management.

### File Extension Routing

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Asset loading / Addressables config | unity-addressables-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |
