# Control Manifest

> **Engine**: Unity 6.3 LTS
> **Last Updated**: 2026-05-12
> **Manifest Version**: 2026-05-12
> **ADRs Covered**: ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0008, ADR-0009, ADR-0010, ADR-0011, ADR-0012, ADR-0013
> **Status**: Active — regenerate with `/create-control-manifest` when ADRs change

`Manifest Version` is the date this manifest was generated. Story files embed this date when created. `/story-readiness` compares a story's embedded version to this field to detect stories written against stale rules. Always matches `Last Updated` — they are the same date, serving different consumers.

This manifest is a programmer's quick-reference extracted from all Accepted ADRs, technical preferences, and engine reference docs. For the reasoning behind each rule, see the referenced ADR.

---

## Foundation Layer Rules

*Applies to: scene management, singleton boot sequence, event architecture, save/load, level data loading, engine initialisation*

### Required Patterns

- **Managers scene**: All 6 manager MonoBehaviours live in `Assets/Scenes/Managers.unity`, loaded once at app start. Each calls `DontDestroyOnLoad(gameObject)` in `Awake`. — source: ADR-0001
- **Static Instance in Awake**: Every singleton sets `public static T Instance { get; private set; }` in `Awake`. Never retrieve via scene scan. — source: ADR-0001
- **Script Execution Order (SEO) — locked boot contract**: Every new singleton MUST be registered in Project Settings > Script Execution Order with an explicit negative value before implementation:

  | SEO | Singleton |
  |-----|-----------|
  | −100 | `QualityTierSystem` |
  | −95  | `LevelDataSystem` |
  | −90  | `SaveSystem` |
  | −80  | `AudioSystem` |
  | −50  | `GameStateManager` |
  | −40  | `CoinEconomy` |
  | −30  | `LevelProgression` |
  | 0    | `SortMechanic`, `AnimationSystem`, `InGameHUD`, `LevelCompleteUI` |

  — source: ADR-0001

- **Subscribe-then-check pattern**: Mandatory for all async-ready singletons (`OnSaveReady`, `OnLevelDataReady`). Subscribe first, then immediately check `IsReady` and call the handler if already fired:
  ```csharp
  system.OnReady += HandleReady;
  if (system.IsReady) HandleReady();
  ```
  — source: ADR-0001, ADR-0002

- **Scene transitions from SEO 0 only**: `SceneManager.LoadScene` may only be called from gameplay code at SEO 0 or later — never from a manager's `Awake`. — source: ADR-0001

- **Typed C# events**: All inter-system communication uses `event Action<T>` declared on MonoBehaviour classes. No `UnityEvent`, no ScriptableObject channels, no EventBus. — source: ADR-0002

- **Subscribe in Awake, not Start**: Event subscriptions must be in `Awake` to guarantee they are in place before the first `Update`. — source: ADR-0002

- **Unsubscribe in OnDestroy**: Scene-loaded MonoBehaviours must unsubscribe all events in `OnDestroy` with a null guard:
  ```csharp
  if (GameStateManager.Instance != null)
      GameStateManager.Instance.OnLevelLoaded -= HandleLevelLoaded;
  ```
  DDOL-to-DDOL subscriptions are exempt. — source: ADR-0002

- **`?.Invoke()` for all event invocations**: Use null-conditional invoke only — not `if (event != null) event(...)`:
  ```csharp
  OnLevelLoaded?.Invoke(levelId, colorCount);  // CORRECT
  ```
  — source: ADR-0002

- **Named method subscribers only**: Event subscriptions must use named instance methods — never lambdas or anonymous methods. — source: ADR-0002

- **Sequence ID guard**: Every consumer of an event carrying a `seqId` must validate it before acting:
  ```csharp
  if (seqId != _currentMoveExecutingSeqId) return;
  ```
  — source: ADR-0002

- **`OnMoveExecutingExited` on IDLE path only**: This event is emitted only on MOVE_EXECUTING → IDLE transition. Never on WIN, never on watchdog path. — source: ADR-0002

- **Presentation → Core layer communication**: Presentation (HUD, LevelCompleteUI) must never hold a direct reference to Core or Foundation instances. Call commands via exposed interfaces (`IGameStateManager`, `ICoinEconomy`) only. — source: ADR-0002

- **Atomic save write (write-then-swap)**:
  1. `FileStream` write + `Flush(flushToDisk: true)` to `.tmp` file
  2. `File.Replace(tmpPath, savePath, null)` if existing file present; `File.Move(tmpPath, savePath)` for first write

  — source: ADR-0003

- **W-1 background write**: Use `Awaitable.BackgroundThreadAsync()` + `SemaphoreSlim._writeLock.WaitAsync()`. Capture snapshot on main thread BEFORE `BackgroundThreadAsync()`. — source: ADR-0003

- **W-2 pause write**: `OnApplicationPause` save is synchronous — uses `_writeLock.Wait()`. No `await`. Must complete before iOS 5-second suspension deadline. — source: ADR-0003

- **IOException + UnauthorizedAccessException**: Catch BOTH in all I/O paths — they are sibling .NET types. `catch (IOException)` alone does NOT catch `UnauthorizedAccessException`. — source: ADR-0003

- **iOS cold-start retry**: Retry loop on `UnauthorizedAccessException` with 250ms sleep, 5-second deadline. Thread must be joined before `IsReady = true` fires. — source: ADR-0003

- **`PlayerPrefs.Save()` after every write**: Called after every `PlayerPrefs.Set*()` — `OnApplicationQuit` is not guaranteed to fire on Android OOM kill. — source: ADR-0003, ADR-0011

- **Addressables 2.x — 3-arg `LoadAssetsAsync`**: Use `Addressables.LoadAssetsAsync<TextAsset>(key, callback, true)` — 3rd argument `releaseDependenciesOnFailure` required in Addressables 2.x. — source: ADR-0004

- **Retain Addressables handle until load completes**: `AsyncOperationHandle` for `LoadAssetAsync<TextAsset>` must be kept alive until `.Status` is checked and content is deserialized. Release via `Addressables.Release(handle)` immediately after deserialization. — source: ADR-0004

- **Newtonsoft.Json for LevelRecord deserialization**: Use `JsonConvert.DeserializeObject<LevelCatalogue>(json)`. Apply `[JsonObject(MemberSerialization.OptIn)]` and `[JsonProperty("snake_case_name")]` on all fields. Nullable `int?` (`HintOverride`) and private setters are supported. — source: ADR-0004

- **`LevelRecord` fields must carry `[JsonProperty]` attribute**: `MemberSerialization.OptIn` means only attributed members are deserialized — unattributed fields silently return defaults and are caught by Stage 2 validation. — source: ADR-0004

- **Release Addressables handle after deserialization**: Call `Addressables.Release(handle)` after `JsonConvert.DeserializeObject` completes. Only retain the typed `_levelCache` dictionary. — source: ADR-0004

### Forbidden Approaches

- **Never `FindObjectsOfType<T>()` without sort mode** — use `FindObjectsByType<T>(FindObjectsSortMode.None)` — removed in Unity 6.0. Source: ADR-0001
- **Never `FindObjectOfType<T>()`** — use `FindFirstObjectByType<T>()` or `FindAnyObjectByType<T>()`. Source: ADR-0001
- **Never `[SerializeField]` on properties or methods** — compile error in Unity 6.3. Use `[SerializeField] private T _field;` backing fields or `[field: SerializeField]` on auto-properties. Source: ADR-0001, ADR-0002
- **Never `[SerializeField]` on event field declarations** — events are not inspector-serializable; compile error in Unity 6.3. Source: ADR-0002
- **Never `SceneManager.LoadScene` from manager `Awake`** — manager's GameObject will be destroyed with the outgoing scene before `DontDestroyOnLoad` executes. Source: ADR-0001
- **Never `async void Awake()`** — Unity does not await `Awake()`; `Start()` on other MonoBehaviours fires before `IsReady = true`, breaking initialization contract. Source: ADR-0003
- **Never `async void OnApplicationPause()`** — Unity returns control to OS at first `await`; write never completes under iOS suspension. Source: ADR-0003
- **Never `File.Move(source, dest, overwrite: true)` (3-arg overload)** — does not exist in .NET Standard 2.1 (Unity 6.3 BCL target), compile error. Source: ADR-0003
- **Never `UnityEvent` on hot paths** — allocates GC per invoke; unacceptable at 60fps. Source: ADR-0002
- **Never lambda or anonymous method event subscribers** — prevents `OnDestroy` unsubscription; causes `MissingReferenceException`. Source: ADR-0002
- **Never `Resources.Load` for level data** — no memory management, no remote delivery path. Source: ADR-0004
- **Never make `LDS.GetLevel()` async** — must remain synchronous per GSM contract. Source: ADR-0004
- **Never allow migrators to set `completion_version` on previously-empty records** — write-once contract. Source: ADR-0003
- **Never store `coin_balance` outside of CoinEconomy / SaveSystem** — CE is sole owner of economy state. Source: ADR-0006

### Performance Guardrails

- **SaveSystem cold-start read**: < 2ms for < 22KB save file (synchronous in Awake). Alert if read exceeds 10ms. — source: ADR-0003
- **LevelData load target**: < 500ms from app launch to `OnLevelDataReady` on Samsung Galaxy A14. Monitor at each content milestone. — source: ADR-0004

---

## Core Layer Rules

*Applies to: Game State Manager, board state, Sort Mechanic, CoinEconomy, input handling*

### Required Patterns

- **GSM is sole writer of board state**: No system other than `GameStateManager` may write to `_stackContents` or `_tempSlotContents`. All external access via `IReadOnlyList<int>[]` read-only interface. — source: ADR-0006

- **Monotonic sequence ID**: `CurrentSequenceId` only ever increments, never decrements. Monotonicity enforced by convention — only GSM calls `_currentSequenceId++`. — source: ADR-0006

- **Atomic 5-step board mutation**: Steps 1–5 must execute synchronously on the main thread within a single `HandleMoveCommitted()` callback. No `await` between steps:
  1. Remove top bolt from source
  2. Add bolt to destination
  3. Push `UndoEntry`
  4. `_currentSequenceId++`
  5. `_moveCount++`
  Then fire `OnBoardStateChanged`. — source: ADR-0006

- **Cancel watchdog in every MOVE_EXECUTING exit**: `CancelWatchdog()` must be called on IDLE exit, WIN exit, TEARDOWN state transition, and `OnDestroy`. Missing any exit path leaks a DDOL coroutine. — source: ADR-0006

- **`WaitForSecondsRealtime` for watchdog** (not `WaitForSeconds`): Fires even when `Time.timeScale = 0` (e.g., pause screen). — source: ADR-0006

- **CE idempotency guard**: `AddCoins(amount, levelId, source)` checks `_lastCreditedLevelId[source]` before crediting. Pass `levelId = -1` for manual grants (pity, starter) to bypass guard. — source: ADR-0006

- **CE coin floor = 0 (hard)**: `SpendCoins` uses `Math.Max(0, _coinBalance - amount)`. — source: ADR-0006

- **`StarRatingCalculator.Compute(moveCount, parMoves, threshold2Star)`**: Single shared static utility for star rating. Both `InGameHUD` and `LevelCompleteUI` call this — no formula duplication. — source: ADR-0012

- **Dual-ready guard in LevelProgression**: Must wait for BOTH `SaveSystem.IsReady` AND `LevelDataSystem.IsReady` before calling `GSM.LoadLevel()`. — source: ADR-0004

- **Check `LDS.IsDegrade` before `GSM.LoadLevel()`**: If `IsDegrade == true`, do not call `LoadLevel()` — surface error instead. — source: ADR-0004

- **Input System Package**: All touch via `EnhancedTouch.Touch.activeTouches`. Active Input Handling: "Input System Package (New)" — not "Both". — source: ADR-0007

- **`EnhancedTouchSupport.Enable()` in Awake**: Must be called before any `Update()`. Paired with `EnhancedTouchSupport.Disable()` in `OnDestroy`. — source: ADR-0007

- **`Physics2D.OverlapPoint` with layer mask**: Always pass `_boltStacksLayerMask` to restrict hits to BoltStacks layer only. Cache `LayerMask.GetMask("BoltStacks")` in `Awake`. — source: ADR-0007

- **Cache `Camera.main` in Awake**: Avoid per-frame `FindAnyObjectByType` calls. — source: ADR-0007

- **One-tap buffer during MOVE_EXECUTING**: Store `_pendingTap` + `_pendingTapStackIndex`. Process on `OnMoveExecutingExited` (IDLE path). Discard on WIN and watchdog (`OnBoardRefreshForced`). — source: ADR-0007

- **All EventSystems use `InputSystemUIInputModule`**: Verify on every scene. `StandaloneInputModule` causes silent UGUI button failures with Input System Package. — source: ADR-0007

- **Android back button → bolt cancellation**: `Keyboard.current.escapeKey.wasPressedThisFrame` in BOLT_SELECTED → `CancelHeldBolt()`. No-op in other states. — source: ADR-0007

### Forbidden Approaches

- **Never write board state from outside GSM** — silent data corruption with no exception. Source: ADR-0006
- **Never `StopAllCoroutines()` on AnimationSystem** — kills glow ramp, celebration, all pending sequences. Always target named `Coroutine` references. Source: ADR-0009
- **Never process deferred undo on WIN path** — `_pendingUndo` is only processed in `OnMoveExecutingExited` (IDLE transition). Never on WIN, never on watchdog. Source: ADR-0006
- **Never use legacy `Input` class** — deprecated in Unity 6.x; does not correctly support multi-touch. Source: ADR-0007
- **Never use `StandaloneInputModule`** — must be `InputSystemUIInputModule` when using Input System Package. Source: ADR-0007
- **Never set `coin_balance` directly from UI layer** — all economy calls go through `ICoinEconomy.AddCoins()` / `SpendCoins()` via LevelProgression event chain. Source: ADR-0012

### Performance Guardrails

- **Win condition check**: O(colorCount × stackDepth) ≤ 64 iterations. No allocation. Must complete within frame. — source: ADR-0006
- **Deadlock check**: O(N²) where N ≤ 11 stacks — ~120 comparisons. Triggered on every `OnMoveExecutingExited`. — source: ADR-0006

---

## Feature Layer Rules

*Applies to: AnimationSystem, LevelProgression, level authoring pipeline*

### Required Patterns

- **Per-bolt coroutine on `BoltVisual`**: Store reference as `_moveCoroutine` field. `StopCoroutine(_moveCoroutine)` for surgical cancellation — never `StopAllCoroutines`. — source: ADR-0009

- **`Time.unscaledDeltaTime` for animation timing**: All phases use unscaledDeltaTime accumulation inside `yield return null` loop — pause-safe, independent of `Time.timeScale`. — source: ADR-0009

- **`AbortAndSnap()` — null before position**: Stop coroutine, set `_moveCoroutine = null`, THEN set `transform.position`. Order matters to prevent stale handle. — source: ADR-0009

- **No `OnAnimationComplete` on abort**: When `OnBoardRefreshForced` fires, AnimationSystem calls `AbortAndSnap()` and does NOT emit `OnAnimationComplete`. SortMechanic exits MOVE_EXECUTING via the GSM event directly. — source: ADR-0009

- **Cancel celebration coroutine on `HandleLevelLoaded`**: Guard against rapid next-level tap firing `OnAnimationComplete` for the wrong level. — source: ADR-0009

- **Column cap authoring validation**: `color_count + temp_slot_count ≤ 8` on all level records. `LevelRecordValidator.Validate()` editor script must reject JSON before Addressables commit. — source: ADR-0013

- **Level Progression `HandleLevelComplete` signature**: Subscribes to `GSM.OnLevelComplete` 4-arg: `(int levelId, int moveCount, int parMoves, int sequenceId)`. LP computes stars directly from this payload — does NOT re-query LDS for `parMoves`. — source: ADR-0012

### Forbidden Approaches

- **Never DOTween or LeanTween** — not in allowed-library list. Source: ADR-0009
- **Never frame-count-based animation timing** — framerate-dependent; cannot meet 80ms/300ms precision requirements. Source: ADR-0009
- **Never `UIElements.ValueAnimation` for gameplay tweens** — scoped to UI Toolkit only. Source: ADR-0009
- **Never > 8 columns (color_count + temp_slot_count)** — violates ADR-0008 tap target mandate on iPhone SE. Source: ADR-0013
- **Never dynamic column width scaling below 44pt** — iOS HIG violation, non-negotiable. Source: ADR-0013

---

## Presentation Layer Rules

*Applies to: rendering pipeline, VFX, bloom, audio, UI hierarchy, HUD and LevelComplete business logic*

### Required Patterns

**Rendering:**
- **URP 2D Renderer**: Use 2D Renderer Data asset. Not Forward Renderer. — source: ADR-0005
- **HDR enabled on 2D Renderer Data asset**: Required for bloom to trigger from sprite emissive color > 1.0. — source: ADR-0005
- **On-Tile Post Processing enabled**: "Tile-Only Mode" on 2D Renderer Data asset. Most impactful mobile GPU bandwidth optimization. — source: ADR-0005
- **`vSyncCount = 0` before `targetFrameRate`**: Both must be set in `QualityTierSystem.Awake()`. If `vSyncCount ≠ 0`, `targetFrameRate` is silently ignored on mobile. — source: ADR-0005
- **Render Graph for custom render features**: Override `AddRenderPasses()` on feature + `RecordRenderGraph()` on pass. Never override `Execute()` or `SetupRenderPasses()`. — source: ADR-0005
- **Bloom intensity calibrated in Unity 6.3**: Bloom processes before tonemapping in URP 6.x. Any pre-6.3 bloom values are invalid. All settings must be calibrated in-engine. — source: ADR-0005

**VFX / Bloom:**
- **VFX Graph sorting layer**: Set `Sorting Layer = Effects, Order In Layer = 10` on all `VisualEffect` output contexts. — source: ADR-0010
- **All VFX Graph assets expose `"quality_density_multiplier"` property**: Float in Blackboard. Mandatory before build. — source: ADR-0010
- **Per-instance `VisualEffect.SetFloat()`**: Apply `"quality_density_multiplier"` on each active VFX instance in `HandleLevelLoaded()`. No global VFX API exists in Unity 6.x. — source: ADR-0010
- **GlowOverlay sprite layer for bloom**: Each BoltStack prefab has a `GlowOverlay` child SpriteRenderer with additive blend material. Set HDR color (`R/G/B > 1.0`) to trigger URP Bloom. — source: ADR-0010
- **`SupportsVFXGraph` gate at startup**: `QualityTierSystem.SupportsVFXGraph = SystemInfo.supportsComputeShaders`. ParticleSystem fallback when false. — source: ADR-0010
- **Runtime spike downgrade via `FrameTimingManager`**: Use `FrameTimingManager.CaptureFrameTimings()` + `GetLatestTimings()` for GPU spike detection — not `GpuTimingProbe` (does not exist). — source: ADR-0010

**Audio:**
- **AudioMixer bus groups**: Three groups: SFX (`"SFXVolume"`), Ambient (`"AmbientVolume"`), UI (`"UIVolume"`). — source: ADR-0011
- **All volume changes via `IAudioSystem`**: Never call `AudioMixer.SetFloat()` directly from game code. Route through `AudioSystem.SetSFXVolume()` / `SetAmbientVolume()` / `SetUIVolume()`. — source: ADR-0011
- **Linear→dB conversion**: `linear > 0.001f ? Mathf.Log10(linear) * 20f : -80f`. — source: ADR-0011
- **8-source SFX pool**: Round-robin via `NextPoolSource()`. `PlayOneShot()` — zero allocation. — source: ADR-0011
- **`PlayBoltSettle(bool isValid)` is the sole bolt SFX API**: AnimationSystem is the sole caller. No other system plays bolt SFX directly. — source: ADR-0011
- **iOS audio session resume**: `AudioSystem.OnApplicationFocus(true)` restarts `_ambientSource` if `!isPlaying`. — source: ADR-0011

**UI (Canvas / HUD):**
- **Screen Space-Overlay for all Canvases**: No Camera reference required; renders on top of all world-space content. — source: ADR-0008
- **Canvas sort order: HUD = 0, LevelCompleteUI = 1, toast = 2**: Higher order renders on top. Do not add Canvases without consulting this table. — source: ADR-0008
- **`SafeAreaPanel` on every gameplay Canvas**: All interactive UI elements must be children of `SafeAreaPanel`. Full-bleed background elements may be direct Canvas children. — source: ADR-0008
- **`Screen.safeArea` → RectTransform anchors**: Divide by `Screen.width`/`Screen.height` to get normalized anchor coordinates. — source: ADR-0008
- **Canvas Scaler: Scale with Screen Size, 1080×1920, Match 0.5**: Handles height variance between tall (21:9) and standard (16:9) devices. — source: ADR-0008
- **All text via `TextMeshProUGUI`**: All font assets must be `TMP_FontAsset`. — source: ADR-0008
- **All buttons RectTransform ≥ 132×132px at reference resolution**: Satisfies ≥ 44pt (iOS) / ≥ 48dp (Android) at 3x screen density. — source: ADR-0008

**HUD / LevelComplete Business Logic:**
- **`OnCoinRewardGranted` fires in `OnEnable`**: LevelCompleteUI fires this event synchronously in `OnEnable` — before any `Update()`, before navigation is physically possible. Guarantees coin delivery (AC-08). — source: ADR-0012
- **Ad watchdog via `Time.realtimeSinceStartup`**: Wall-clock time continues during iOS backgrounding. `WaitForSeconds` pauses during app suspension — causes permanent AD_PROCESSING soft-lock. — source: ADR-0012
- **HUD FSM owns pity grant counter**: `_pityAttempts` and `_activeLevelId` fields in `InGameHUD` — session-only, not persisted. — source: ADR-0012
- **Hint timeout via stored `Coroutine` reference**: Cancel with `StopCoroutine(_hintTimeoutCoroutine)` on any `hint_result`. `ExitHintProcessing()` on timeout — no coin deducted. — source: ADR-0012
- **LevelCompleteUI → Level Progression event chain**: `LevelCompleteUI` fires `OnCoinRewardGranted` / `OnCoinBonusGranted` events. LP subscribes → calls `CE.AddCoins()`. Never direct. — source: ADR-0012
- **All `OnDestroy` unsubscriptions use null guard**: `if (X.Instance != null) X.Instance.OnEvent -= Handler;` — protects against app quit / scene reload order. — source: ADR-0012

### Forbidden Approaches

- **Never `ScriptableRendererFeature.SetupRenderPasses()`** — removed in Unity 6.3; compile error. Source: ADR-0005
- **Never URP Compatibility Mode** — fully removed in Unity 6.3; compile error. Source: ADR-0005
- **Never `Execute(ScriptableRenderContext, ref RenderingData)` override for new render features** — use `RecordRenderGraph` instead. Source: ADR-0005
- **Never render scale below 1.0 for quality scaling** — use VFX density multiplier instead; sub-pixel shimmer on sprites. Source: ADR-0005
- **Never `MaterialPropertyBlock` on Sprite Renderers for glow** — breaks SRP Batcher for affected sprite (up to 11 batch splits). Use GlowOverlay sprite layer instead. Source: ADR-0010
- **Never `VFXManager.SetGlobalFloat()`** — API does not exist in Unity 6.x VFX Graph. Source: ADR-0010
- **Never `audioSource.volume` for bus control** — no path to Settings UI; no mixer snapshot support. Source: ADR-0011
- **Never `UI.Text`** — deprecated in Unity 6.x; inferior rendering on high-DPI screens. Source: ADR-0008
- **Never interactive UI elements as direct Canvas children** — must be inside `SafeAreaPanel`. Source: ADR-0008
- **Never `LevelCompleteUI` call `ICoinEconomy.AddCoins()` directly** — violation of CE ownership; routes through LP event. Source: ADR-0012
- **Never `WaitForSeconds` for ad watchdog** — pauses during iOS backgrounding; use `Time.realtimeSinceStartup` polling. Source: ADR-0012
- **Never emit `OnAnimationComplete` on abort (watchdog path)** — SortMechanic exits via `OnBoardRefreshForced`; double-exit breaks FSM. Source: ADR-0009

### Performance Guardrails

- **GlowOverlay draw calls**: ≤ 11 additional draws during full-board glow event. Within ≤ 100 batch budget. — source: ADR-0010
- **VFX Graph ring + sparks**: Target ≤ 3ms GPU per stack completion event (Medium/High tier). ParticleSystem fallback ≤ 0.2ms (Low tier). — source: ADR-0010
- **UI re-batch**: Move counter update (one text change per move) ≈ 0.1ms. Two-Canvas architecture prevents LevelCompleteUI elements from dirtying HUD batch. — source: ADR-0008

---

## Global Rules (All Layers)

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `SortMechanic`, `GameStateManager` |
| Public properties / fields | PascalCase | `MoveSpeed`, `StackDepth` |
| Private fields | `_camelCase` | `_currentBalance`, `_sequenceId` |
| Methods | PascalCase | `TakeDamage()`, `GetCoinBalance()` |
| Events | C# `event Action<T>`, PascalCase | `OnMoveCommitted`, `OnPuzzleSolved` |
| Files | PascalCase matching class | `SortMechanic.cs`, `GameStateManager.cs` |
| Scenes | PascalCase matching root purpose | `GameScene.unity`, `MainMenu.unity` |
| Prefabs | PascalCase matching component | `BoltStack.prefab`, `HUD.prefab` |
| Constants | PascalCase | `MaxStackDepth`, `WatchdogTimeoutMs` |

### Performance Budgets

| Target | Value | Notes |
|--------|-------|-------|
| Framerate | 60fps (Medium/High tier), 30fps (Low tier) | Set via QualityTierSystem at SEO −100 |
| Frame budget | 16.6ms | — |
| Draw calls | ≤ 100 batches | GPU Resident Drawer + sprite atlasing |
| Memory ceiling | 512MB | Mid-range Android target; profile on Samsung Galaxy A series |

### Approved Libraries / Addons

- **Unity Test Framework (NUnit)** — built-in, required for all unit and integration tests
- **Unity Addressables** — level data loading and asset memory management
- **Unity Localization package** — string management (all player-facing text uses localization keys)
- **AdMob (Google Mobile Ads Unity Plugin)** — rewarded ads; Beta milestone
- **Unity IAP** — in-app purchases; Launch milestone

### Forbidden APIs (Unity 6.3 LTS)

These APIs are deprecated, removed, or unverified for Unity 6.3 LTS. Do not use:

| Forbidden | Use Instead | Since |
|-----------|-------------|-------|
| `Object.FindObjectsOfType<T>()` | `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` | 6.0 |
| `Object.FindObjectOfType<T>()` | `Object.FindFirstObjectByType<T>()` or `FindAnyObjectByType<T>()` | 6.0 |
| `GraphicsFormat.DepthAuto` | Explicit depth format | 6.0 (compile error) |
| `GraphicsFormat.ShadowAuto` | Explicit shadow format | 6.0 (compile error) |
| `ScriptableRendererFeature.SetupRenderPasses()` | `AddRenderPasses()` + `RecordRenderGraph()` | 6.3 (compile error) |
| URP Compatibility Mode | Render Graph API | 6.3 (removed) |
| `RenderPipelineEditorUtility.FetchFirstCompatibleTypeUsingScriptableRenderPipelineExtension()` | `GetDerivedTypesSupportedOnCurrentPipeline()` | 6.0 |
| `CustomEditorForRenderPipelineAttribute` | `[CustomEditor] + [SupportedOnRenderPipeline]` | 6.0 |
| `VolumeComponentMenuForRenderPipelineAttribute` | `[VolumeComponentMenu] + [SupportedOnRenderPipeline]` | 6.0 |
| `ExecuteDefaultAction()` (UI Toolkit) | `HandleEventBubbleUp()` | 6.0 |
| `ExecuteDefaultActionAtTarget()` (UI Toolkit) | `HandleEventTrickleDown()` | 6.0 |
| `PreventDefault()` (UI Toolkit) | `StopPropagation()` | 6.0 |
| `AccessibilityNode.selected` | `AccessibilityNode.invoked` | 6.3 |
| `[SerializeField]` on properties or methods | `[SerializeField] private T _field;` backing fields | 6.3 (compile error) |
| Legacy ETC texture compressor | Default ETC compressor | 6.3 (auto-converted) |
| Bitwise operations on `AccessibilityRole` | Individual enum values | 6.3 |
| Round / legacy Android icons | Adaptive icons (foreground + background layers) | Android 16+ |
| `VFXManager.SetGlobalFloat()` | `VisualEffect.SetFloat()` per-instance | Unity 6.x (does not exist) |
| `GpuTimingProbe` | `FrameTimingManager.CaptureFrameTimings()` + `GetLatestTimings()` | Unity 6.x (does not exist) |
| `UI.Text` | `TextMeshProUGUI` | 6.x (deprecated) |
| Legacy `Input` class | `UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches` | 6.x (deprecated) |
| `StandaloneInputModule` on EventSystem | `InputSystemUIInputModule` | Required with Input System Package |

Source: `docs/engine-reference/unity/deprecated-apis.md`, ADRs 0001–0013

### Cross-Cutting Constraints

- **All visible player-facing strings must use localization keys** — no hardcoded UI text in `src/`. — source: technical-preferences.md
- **Singleton `Instance` pattern via `FindObjectOfType` is forbidden** — register singletons in Script Execution Order via static reference. — source: technical-preferences.md
- **Synchronous file I/O on main thread is forbidden** except for: SaveSystem cold-start read (intentional) and W-2 pause write (intentional). — source: technical-preferences.md
- **`ProjectSettings/ProjectSettings.asset` must be committed to version control** — the Script Execution Order lives there. — source: ADR-0001
- **Every new DDOL singleton must be added to Script Execution Order with explicit negative value** — omitting this causes silent ordering bugs. — source: ADR-0001
- **AudioMixer exposed parameter names (`SFXVolume`, `AmbientVolume`, `UIVolume`) must not be renamed post-launch** — stored in PlayerPrefs keys indirectly; renaming requires a coordinated migration. — source: ADR-0011
- **Android back button manifest flag**: Do NOT add `android:enableOnBackInvokedCallback` to the manifest without a corresponding input migration. If added, `escapeKey` back gesture stops working. — source: ADR-0007
