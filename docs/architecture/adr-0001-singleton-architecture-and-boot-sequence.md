# ADR-0001: Singleton Architecture and Boot Sequence

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | HIGH — Unity 6.x is post-LLM-cutoff |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md` |
| **Post-Cutoff APIs Used** | `Object.FindFirstObjectByType<T>()` — dev bootstrap only (not on runtime paths); replaces removed `FindObjectOfType<T>()` (Unity 6.0+) |
| **Verification Required** | Confirm Script Execution Order is respected when Managers scene is loaded additively at startup on an Android device; confirm `DontDestroyOnLoad` survives scene transitions correctly |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None — this is the root Foundation ADR |
| **Enables** | ADR-0002 (event architecture assumes singletons initialized in known order), ADR-0003 (save system assumes its boot slot at Order −90), ADR-0004 (level data system assumes `LDS.IsReady` can be safely queried) |
| **Blocks** | All implementation sprints — no MonoBehaviour may be implemented until this ADR is Accepted |
| **Ordering Note** | ADR-0003 (Save System Design) depends on the subscribe-then-check pattern defined here. ADR-0001 must be Accepted first. |

## Context

### Problem Statement
BoltSort has 6 manager-class MonoBehaviours (QualityTierSystem, SaveSystem, AudioSystem, GameStateManager, CoinEconomy, LevelProgression) that must initialize in strict order, persist across all scene loads, and be accessible from game-scene MonoBehaviours (SortMechanic, AnimationSystem, InGameHUD, LevelCompleteUI). Without a documented and enforced boot contract, initialization order bugs — such as CoinEconomy reading coin balance before SaveSystem fires `OnSaveReady` — will produce silent incorrect state.

### Constraints
- Unity 6.3 LTS: `FindObjectOfType<T>()` is removed; `FindFirstObjectByType<T>()` is the replacement, but is an O(n) scene scan every call — unacceptable on runtime hot paths
- `[SerializeField]` applied to a property or method signature is a **compile error** in Unity 6.3; all singleton `Instance` properties must use plain auto-properties with no attribute
- No third-party DI packages are in the allowed-library list (`CLAUDE.md`)
- Target platforms iOS + Android: startup time must be minimized; singleton access must be zero-cost after boot
- 60 fps target: no per-frame scene-graph scans

### Requirements
- QualityTierSystem must execute at SEO −100 (before any VFX, scene, or framerate-dependent system)
- SaveSystem must read `save.json` synchronously in `Awake()` at SEO −90; `IsReady = true` and `OnSaveReady?.Invoke()` must fire before any lower-SEO system's `Start()` runs
- All async-ready singletons must support the subscribe-then-check pattern (subscribe before checking `IsReady`, to handle race condition if `OnSaveReady` fires before subscription)
- Static `Instance` reference must be set in `Awake` — no runtime scene scans
- All managers must survive scene loads via `DontDestroyOnLoad`

## Decision

All 6 manager MonoBehaviours reside on GameObjects in a dedicated `Assets/Scenes/Managers.unity` scene. This scene is loaded once at app start via `SceneManager.LoadScene("Managers", LoadSceneMode.Single)`. Each manager calls `DontDestroyOnLoad(gameObject)` in `Awake`. Gameplay scenes are subsequently loaded additively or as replacements — managers are never unloaded for the duration of the session.

Boot order is enforced via Unity's **Script Execution Order** (Edit > Project Settings > Script Execution Order). Each manager's MonoBehaviour class is registered in Project Settings with the execution order defined below. This is the authoritative, locked boot contract:

| SEO Order | Singleton | Init work in Awake |
|-----------|-----------|---------------------|
| −100 | `QualityTierSystem` | Reads `SystemInfo`, sets `Application.targetFrameRate`, exposes `ActiveTier`, `DensityMultiplier` |
| −95 | `LevelDataSystem` | Starts async Addressables batch load (`LoadAssetsAsync`); exposes `IsReady`, `OnLevelDataReady`, `IsDegrade` |
| −90 | `SaveSystem` | Reads `save.json` synchronously in `Awake()`; `IsReady = true` set before any `Start()` runs; exposes `OnSaveReady` |
| −80 | `AudioSystem` | Reads `PlayerPrefs` for audio prefs; initializes `AudioMixer` routing |
| −50 | `GameStateManager` | Sets `Instance`; allocates board state arrays; initializes lifecycle FSM to UNLOADED |
| −40 | `CoinEconomy` | Subscribes to `SaveSystem.OnSaveReady` (subscribe-then-check); CE FSM → LOADING |
| −30 | `LevelProgression` | Subscribes to `SaveSystem.OnSaveReady` and `GSM.OnLevelComplete` (4-arg); subscribes to `LevelDataSystem.OnLevelDataReady`; dual-ready guard before `GSM.LoadLevel()` |
| 0 (default) | `SortMechanic`, `AnimationSystem`, `InGameHUD`, `LevelCompleteUI` | Board-level MonoBehaviours; all singletons guaranteed initialized before `Start()` |

Each singleton exposes a static `Instance` reference, set in `Awake` — never retrieved via scene scan:

```csharp
public class QualityTierSystem : MonoBehaviour
{
    public static QualityTierSystem Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // initialization work...
    }
}
```

**Subscribe-then-check pattern** — mandatory for all async-ready systems:

```csharp
// In CoinEconomy.Awake() [SEO −40, after SaveSystem at −90]
SaveSystem.Instance.OnSaveReady += HandleSaveReady;
if (SaveSystem.Instance.IsReady) HandleSaveReady();  // covers race: event already fired
```

**Editor-only dev bootstrap** — allows playing from any scene in-editor without `NullReferenceException`:

```csharp
#if UNITY_EDITOR
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void DevBootstrap()
{
    if (!SceneManager.GetSceneByName("Managers").isLoaded)
        SceneManager.LoadScene("Managers", LoadSceneMode.Additive);
}
#endif
```

### Architecture Diagram

```
app start
    │
    ▼
Managers.unity loaded
    │
    ├── QualityTierSystem.Awake() [−100] ── sets targetFrameRate, tier
    ├── LevelDataSystem.Awake()   [−95]  ── starts async Addressables load (LoadAssetsAsync)
    ├── SaveSystem.Awake()         [−90] ── reads save.json synchronously; IsReady = true; OnSaveReady fires
    ├── AudioSystem.Awake()        [−80] ── reads PlayerPrefs
    ├── GameStateManager.Awake()   [−50] ── allocates board state
    ├── CoinEconomy.Awake()        [−40] ── subscribe-then-check OnSaveReady → HandleSaveReady() immediately
    └── LevelProgression.Awake()   [−30] ── subscribe-then-check OnSaveReady → _saveReady = true
                                             subscribe OnLevelDataReady → _ldsReady = false (async still pending)
                                             TryLoadFirstLevel() no-ops (_ldsReady = false)

    (Frame 0 completes — all Awake() done, Start() runs for gameplay MonoBehaviours)

    (Frame N — Addressables load completes):
        LevelDataSystem fires OnLevelDataReady → _ldsReady = true
            → TryLoadFirstLevel(): _saveReady && _ldsReady && !IsDegrade
                 → GSM.LoadLevel(current_level_id)
                      → OnLevelLoaded event
                           → gameplay MonoBehaviours respond
```

### Key Interfaces

```csharp
// All managers: static Instance set in Awake, no [SerializeField] on properties
public class [ManagerName] : MonoBehaviour
{
    public static [ManagerName] Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}

// Async-ready singletons additionally expose:
public bool IsReady { get; private set; }
public event Action OnReady;  // per-system name: OnSaveReady, etc.

// Caller pattern (mandatory):
system.OnReady += HandleReady;
if (system.IsReady) HandleReady();
```

## Alternatives Considered

### Alternative A: Lazy `FindFirstObjectByType<T>()` Singleton
- **Description**: No manager scene; each singleton found at first access via `FindFirstObjectByType<T>()`. Boot order controlled implicitly by Awake call order.
- **Pros**: Simple to set up; no manager scene to maintain
- **Cons**: `FindFirstObjectByType` is O(n) scene scan; unacceptable on runtime hot paths at 60fps. Boot order is implicit and unreliable. Initialization race conditions remain unsolved.
- **Rejection Reason**: No boot order guarantee; runtime overhead; explicitly banned by `technical-preferences.md` ("Singleton `Instance` pattern via `FindObjectOfType` — register singletons in Script Execution Order via static reference")

### Alternative B: Dependency Injection Container (Zenject / VContainer)
- **Description**: All managers registered in a DI container at startup; systems receive dependencies via constructor injection or `[Inject]` attributes.
- **Pros**: Fully decoupled; excellent testability; explicit dependency graph enforced at bind time
- **Cons**: Requires a third-party package (not in allowed-library list); adds framework complexity; increases build size; overkill for 6 singletons with a well-defined, stable order
- **Rejection Reason**: Not in allowed-library list; scope exceeds project needs

## Consequences

### Positive
- Boot order is explicit, documented, and enforced by Unity's Project Settings — no runtime discovery cost
- Static `Instance` access is zero-cost after `Awake` completes
- Subscribe-then-check pattern eliminates the `OnSaveReady` race condition (OQ-03 from GDD reviews)
- Adding a new singleton requires updating this ADR and Project Settings — the change is visible and auditable

### Negative
- All singletons must be manually registered in Project Settings > Script Execution Order. Forgetting this for a new manager causes silent ordering bugs.
- The `DontDestroyOnLoad` scene is a hidden internal Unity scene — it does **not** appear in `SceneManager.sceneCount` or scene-iteration loops. Any code that needs to reach a manager must use the static `Instance` reference, not scene enumeration.
- `[SerializeField]` applied to any property or method in a manager class is a compile error in Unity 6.3. All inspector-exposed values in manager classes must use `[SerializeField] private T _field` backing fields or `[field: SerializeField]` on auto-properties. This restriction applies to every developer touching these files.
- The manager scene must be treated as a root bootstrap scene. Playing directly from a gameplay scene in the editor will skip it (mitigated by the dev bootstrap, but that is editor-only).
- `ProjectSettings/ProjectSettings.asset` must be committed to version control — the SEO registration lives there.

### Risks
- **Risk**: A developer adds a new singleton and forgets to register it in SEO → silent ordering bug. **Mitigation**: This ADR and the control manifest document the requirement; the control manifest will list "Every new singleton MUST be added to Script Execution Order with an explicit negative value" as a REQUIRED rule.
- **Risk**: No `SceneManager.LoadScene` may be called from within `Awake` of any manager class. If a manager triggers a scene load before `DontDestroyOnLoad` executes, the manager's GameObject will be destroyed with the outgoing scene before it escapes. This failure is silent and hard to diagnose. **Mitigation**: Scene transitions must always be initiated from gameplay code at SEO 0 or later, never from manager `Awake`. Enforced as a FORBIDDEN rule in the control manifest.
- **Risk**: `[SerializeField]` accidentally placed on a property in a manager class breaks the entire build compile. **Mitigation**: Code review checklist; CI full-compile gate on every push to main.
- **Risk**: In-editor "Play from current scene" skips Managers scene → `NullReferenceException` on `Instance`. **Mitigation**: Editor-only dev bootstrap (`#if UNITY_EDITOR` + `BeforeSceneLoad`) loads Managers additively when not already loaded. This is stripped from builds.
- **Risk**: iOS audio session interruption during scene transitions. `AudioSystem` survives as `DontDestroyOnLoad` but the iOS audio session may cut out during background/foreground transitions. **Mitigation**: `AudioSystem` must handle `OnApplicationFocus(true)` to re-activate the audio session on iOS. This is AudioSystem's responsibility, not a boot ordering bug.
- **Risk**: Android IL2CPP cold-start hitch on first launch (API 26–28). IL2CPP runtime init causes a single long frame before any `Awake` runs. SEO ordering is still honored; this is cosmetic only. **Mitigation**: Expected; not a correctness bug. QA should not flag a single-frame hitch on first install as a regression.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| quality-tier-system.md | TR-QTS-002: Apply `density_multiplier` + framerate before first scene | Enforces QualityTierSystem at SEO −100 — guaranteed to run before any other `Awake` |
| save-persistence.md | TR-SP-004: `IsReady` + `OnSaveReady`; subscribe-then-check pattern | Mandates subscribe-then-check as the project-wide pattern for all async-ready singletons |
| coin-economy.md | TR-CE-001: CE FSM LOADING/READY | Establishes CoinEconomy at SEO −40, after SaveSystem (−90), ensuring subscription precedes potential `OnSaveReady` fire |
| game-state-manager.md | TR-GSM-008: Level lifecycle FSM (UNLOADED/LOADING/ACTIVE/COMPLETE/TEARDOWN) | Establishes GSM at SEO −50 — board state available before feature-layer MonoBehaviours `Awake` |
| level-data-system.md | TR-LDS-003: System readiness query before load | LevelDataSystem registered at SEO −95; LevelProgression at SEO −30 subscribes to both `SaveSystem.OnSaveReady` and `LDS.OnLevelDataReady`; dual-ready guard before `GSM.LoadLevel()` |

## Performance Implications
- **CPU**: Zero-cost singleton access after boot (static field read). SEO adds ~0ms runtime overhead vs. default order.
- **Memory**: 6 manager GameObjects with `DontDestroyOnLoad` persist for the entire session — ~1–5KB total.
- **Load Time**: SaveSystem synchronous file read at SEO −90: <2ms for <22KB save file. LevelDataSystem async Addressables load at SEO −95: spreads across frames N to N+k; LevelProgression blocks on `OnLevelDataReady` before calling `GSM.LoadLevel()`. Target: <500ms on Galaxy A14.
- **Network**: N/A

## Migration Plan
No existing code to migrate — this ADR is written before implementation begins. All new singleton implementations must follow this pattern from the first commit.

## Validation Criteria
1. Unit test: CoinEconomy test harness fires `OnSaveReady` before CE subscribes → CE `HandleReady` still called (subscribe-then-check covers it)
2. Integration test: Cold-start on Android device; verify `Application.targetFrameRate` is set before any `FixedUpdate`/`Update` runs (via log timestamps)
3. CI: Full compile check confirms no `[SerializeField]` attribute on any property or method in manager classes
4. Manual: Play-from-current-scene in editor does not throw `NullReferenceException` for any singleton (dev bootstrap present)

## Related Decisions
- ADR-0002: Event and Signal Architecture — builds on singletons being initialized in known order
- ADR-0003: Save System Design — detailed save thread/async contract that this ADR's SEO order enables
- ADR-0004: Level Data Loading Strategy — LDS.IsReady pattern referenced in TR-LDS-003
- `docs/architecture/architecture.md` — Singleton Boot Sequence table (source that this ADR formalizes)
