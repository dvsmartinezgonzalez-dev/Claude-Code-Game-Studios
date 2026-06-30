using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using BoltSort.Visual;
using BoltSort.SortMechanic;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Bootstraps the Gameplay scene: creates all systems, loads the requested level,
    /// and wires progression (save completion, next-level flow, settings).
    /// Reads the target level from PlayerPrefs "bs.next_level" (set by MainMenu or LevelSelect).
    /// Falls back to SaveSystem.GetCurrentLevelId() if the key is absent.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private BoltSort.LevelData.LevelDataSystem         _lds;
        private BoltSort.GameStateManager.GameStateManager _gsm;
        private BoltSort.SortMechanic.SortMechanic         _sortMechanic;
        private HUDController                              _hud;
        private TutorialOverlaySystem                      _tutorialOverlay;

        private int _currentLevelId = 1;
        private int _maxLevelId = 1;
        private const string NextLevelKey = "bs.next_level";

        private void Awake()
        {
            EnsureSaveSystem();
            EconomyManager.EnsureInstance(); // after SaveSystem: seeds coins from it on first run
            AdService.EnsureInstance();      // rewarded-ad facade for the win-screen reward buttons
            EnsureAudioManager();
            EnsureTransitionManager();
            EnsureCamera();
            ConfigureCamera();
            CreateSystems();

            _lds.LoadCatalogueTextAsync = LoadLevelsFromResources;

#if UNITY_EDITOR
            UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
#endif

            _sortMechanic.OnMoveCommitted       += _gsm.HandleMoveCommitted;
            _sortMechanic.OnPuzzleSolved        += _ => _gsm.HandlePuzzleSolved();
            _sortMechanic.OnMoveExecutingExited += _gsm.HandleMoveExecutingExited;

            // bolt_pick and bolt_place are played by BoardView for correct timing.
            // bolt_invalid plays here (frame 1 of rejection, same as shake start).
            _sortMechanic.OnMoveRejected += (_, _, _, _) => AudioMgr.Instance?.PlaySFX("bolt_invalid");

            _gsm.OnLevelComplete += HandleLevelComplete;
            _gsm.OnLevelLoaded   += (_, _, _, _, _, _) => AudioMgr.Instance?.PlaySFX("start_level");

            // GP-01: wire pause event here — SortMechanic cannot reference BoltSort.Gameplay directly.
            SettingsPanel.OnGamePaused += _sortMechanic.SetGamePaused;
        }

        private void OnDestroy()
        {
            SettingsPanel.OnGamePaused -= _sortMechanic.SetGamePaused;
            if (EconomyManager.Instance != null)
                EconomyManager.Instance.OnPowerUpDropped -= ShowPowerUpDropPopup;
        }

        // Reward drop full-screen reveal (System 2). Uses the Magnet-style splash popup.
        private void ShowPowerUpDropPopup(PowerUpType type)
        {
            bool isExtraTube = type == PowerUpType.ExtraTube;
            string titleKey = isExtraTube ? "key_extra_tube_title" : "key_extra_bolt_title";
            string bodyKey  = isExtraTube ? "key_extra_tube_body"  : "key_extra_bolt_body";
            Sprite icon     = isExtraTube
                ? GameAssets.BtnExtraTube : (GameAssets.BtnUndoNew ?? GameAssets.BtnUndoAction);
            MagnetUnlockSplash.ShowPowerUp(GameAssets.MenuFont, icon, titleKey, bodyKey);
        }

        private IEnumerator Start()
        {
            var loadingOverlay = CreateLoadingOverlay();   // GP-04: show before LDS init
            _lds.InitializeAsync();
            yield return new WaitUntil(() => _lds.IsReady);
            Destroy(loadingOverlay);                        // GP-04: hide after LDS ready

            // Procedural generation extends progression past the curated catalogue to the
            // SaveSystem-bounded ceiling (9999); levels 201+ are generated on demand.
            _maxLevelId     = Mathf.Max(_lds.GetMaxLevelId(), BoltSort.LevelData.ProcLevelGenerator.ProcMaxLevel);
            _currentLevelId = ReadTargetLevel();

            // Reward drops (System 2): show a small toast when a power-up drops after a level.
            var economy = EconomyManager.Instance;
            if (economy != null) economy.OnPowerUpDropped += ShowPowerUpDropPopup;

            // Background controller (gradient + vignette + ambient particles)
            var bgGO = new GameObject("BackgroundController");
            bgGO.AddComponent<BackgroundController>();

            var boardGO = new GameObject("Board");
            var boardView = boardGO.AddComponent<BoardView>();
            boardView.Initialize(_gsm, _sortMechanic);

            var hudGO = new GameObject("HUD");
            _hud = hudGO.AddComponent<HUDController>();
            _hud.Initialize(_gsm, _sortMechanic,
                onReset:    ResetLevel,
                onNextLevel: LoadNextLevel,
                onUndo:     OnUndoClicked,
                onMenu:     OnMenuClicked,
                onReplay:   ResetLevel,
                onLevels:   OnLevelsClicked,
                onShop:     OnShopClicked,
                onExtraTube: OnExtraTubeRequested);

            // Phase-2 mechanic tutorial trigger stubs — must subscribe to OnLevelLoaded
            // before LoadLevel so a level that introduces a mechanic shows its prompt.
            var tutGO = new GameObject("TutorialController");
            tutGO.AddComponent<TutorialController>().Initialize(_gsm, _sortMechanic, GameAssets.MenuFont);

            // Animated tutorial overlay (new-user onboarding). Dormant if tutorial already complete.
            var tutOverlayGO = new GameObject("TutorialOverlaySystem");
            _tutorialOverlay = tutOverlayGO.AddComponent<TutorialOverlaySystem>();
            _tutorialOverlay.Initialize(_gsm, _sortMechanic, boardView, _hud);

            LoadLevelWithMagnetCheck(_currentLevelId);
            AudioMgr.Instance?.PlayMusic();
        }

        // ── Level management ──────────────────────────────────────────────────────

        public void ResetLevel()
        {
            LoadLevelWithMagnetCheck(_currentLevelId);
        }

        public void LoadNextLevel()
        {
            if (_currentLevelId >= _maxLevelId)
            {
                _hud?.ShowMoreLevelsSoon();
                return;
            }
            _currentLevelId++;
            LoadLevelWithMagnetCheck(_currentLevelId);
        }

        /// <summary>
        /// Tears down the current level and loads <paramref name="levelId"/>. The first time a
        /// Magnet-enabled level (≥ <see cref="GameStateManager.MagnetUnlockLevel"/>) is loaded, shows
        /// the one-shot unlock splash and defers the actual load until the player dismisses it
        /// (PlayerPref-gated, so it never reappears). Independent of the mechanic — if the splash
        /// fails to build it loads immediately.
        /// </summary>
        private void LoadLevelWithMagnetCheck(int levelId)
        {
            _gsm.ExitLevel();

            if (levelId >= BoltSort.GameStateManager.GameStateManager.MagnetUnlockLevel &&
                PlayerPrefs.GetInt(MagnetUnlockSplash.ShownPrefKey, 0) == 0)
            {
                PlayerPrefs.SetInt(MagnetUnlockSplash.ShownPrefKey, 1);
                PlayerPrefs.Save();
                MagnetUnlockSplash.Show(GameAssets.MenuFont, () => _gsm.LoadLevel(levelId));
                return;
            }

            _gsm.LoadLevel(levelId);
        }

        private void OnUndoClicked() => _gsm.UndoRequested();

        // Header Extra-Tube tap (budget already checked/decremented by the HUD). Adds or grows
        // a runtime helper tube; GSM fires OnBoardShapeChanged and BoardView relayouts.
        private void OnExtraTubeRequested() => _gsm.ApplyExtraTube();

        private void OnMenuClicked()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("MainMenu");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // GP-06: navigate to LevelSelect from the in-game Levels button
        private void OnLevelsClicked()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("LevelSelect");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("LevelSelect");
        }

        // Navigate to Shop from the settings popup "Go to Shop" option.
        private void OnShopClicked()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("Shop");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("Shop");
        }

        // ── Android back button (GP-02) ───────────────────────────────────────────

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

            // If settings panel is open, close it first.
            if (_hud != null && _hud.IsSettingsOpen)
            {
                _hud.CloseSettings();
                return;
            }

            // If bolt is held, SortMechanic handles cancellation (existing AC-12 logic).
            if (_sortMechanic != null && _sortMechanic.CurrentState == SortMechState.BoltSelected)
                return;

            // Win overlay showing or in gameplay — go to main menu.
            OnMenuClicked();
        }

        // ── Save wiring ───────────────────────────────────────────────────────────

        private async void HandleLevelComplete(int levelId, int moves, int par, long seqId)
        {
            // Clear any active tutorial prompt/animation before the Victory overlay is shown.
            // This runs before the HUD's OnLevelComplete handler (subscribed later) opens the
            // win screen, so no hand/hint/icon can survive onto it.
            _tutorialOverlay?.CleanupAll();

            int stars = moves <= par ? 3 : moves <= (int)(par * 1.5f) ? 2 : 1;

            // EconomyManager (System 3) is the currency authority: it awards first-time vs replay
            // coins, streak/milestone/achievement bonuses, rolls power-up drops (System 2) for
            // non-tutorial levels, and mirrors the coin balance back into SaveSystem.
            var economy = EconomyManager.Instance;
            int coinsEarned;
            int gemsEarned = 0;
            if (economy != null)
            {
                bool isTutorial = false;
                bool isSpecial  = false;
                try
                {
                    var rec    = _lds.GetLevel(levelId);
                    isTutorial = rec.IsTutorial;
                    // "Special" = any phase-2 mechanic present (mystery / multicolor / frozen tube).
                    isSpecial  = rec.MysteryBalls || rec.HasMulticolor ||
                                 (rec.FrozenTubes != null && rec.FrozenTubes.Length > 0);
                }
                catch { /* generated ⇒ not tutorial, not special */ }

                LevelRewardResult reward = economy.OnLevelComplete(levelId, stars, isTutorial, isSpecial);
                coinsEarned = reward.TotalCoins;
                gemsEarned  = reward.TotalGems;
            }
            else
            {
                coinsEarned = stars * 10;
            }

            // SetWinResult runs synchronously here (GameBootstrap subscribed in Awake, before
            // HUD subscribed in Start) so _winStarCount/_winCoins/_winGems are set before HUD's
            // handler calls ShowWinOverlay.
            _hud?.SetWinResult(stars, coinsEarned, gemsEarned, levelId);

            var ss = SaveSystem.SaveSystem.Instance;
            if (ss == null || !ss.IsReady) return;

            // Coins already written by EconomyManager; only mirror in the no-economy fallback path.
            if (economy == null)
                ss.SetCoinBalance(ss.GetCoinBalance() + coinsEarned);

            int savedCurrent = ss.GetCurrentLevelId();
            int newCurrent   = Math.Max(levelId + 1, savedCurrent);
            newCurrent       = Math.Min(newCurrent, _maxLevelId + 1);

            await ss.WriteCompletionAtomic(levelId, stars, Application.version, newCurrent);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        // GP-04: minimal full-screen loading overlay shown while LevelDataSystem initialises
        private static GameObject CreateLoadingOverlay()
        {
            var go = new GameObject("LoadingOverlay");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Bg");
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.08f, 1f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var lblGO = new GameObject("LoadingText");
            lblGO.transform.SetParent(go.transform, false);
            var lbl = lblGO.AddComponent<Text>();
            lbl.text      = "Loading…";
            lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                         ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            lbl.fontSize  = 42;
            lbl.fontStyle = FontStyle.Bold;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color     = new Color(0.7f, 0.7f, 0.9f, 1f);
            lbl.supportRichText = false;
            var lblRt = lblGO.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0.4f); lblRt.anchorMax = new Vector2(1f, 0.6f);
            lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;

            return go;
        }

        private int ReadTargetLevel()
        {
            if (PlayerPrefs.HasKey(NextLevelKey))
            {
                int id = PlayerPrefs.GetInt(NextLevelKey, 1);
                PlayerPrefs.DeleteKey(NextLevelKey);
                return Mathf.Clamp(id, 1, _maxLevelId);
            }
            var ss = SaveSystem.SaveSystem.Instance;
            return (ss != null && ss.IsReady)
                ? Mathf.Clamp(ss.GetCurrentLevelId(), 1, _maxLevelId)
                : 1;
        }

        private static void EnsureSaveSystem()
        {
            if (SaveSystem.SaveSystem.Instance == null)
                new GameObject("SaveSystem").AddComponent<SaveSystem.SaveSystem>();
        }

        private static void EnsureAudioManager()
        {
            if (AudioMgr.Instance == null)
                new GameObject("AudioManager").AddComponent<AudioMgr>();
        }

        private static void EnsureTransitionManager()
        {
            if (SceneTransitionManager.Instance == null)
                new GameObject("SceneTransitionManager")
                    .AddComponent<SceneTransitionManager>();
        }

        private void CreateSystems()
        {
            if (BoltSort.LevelData.LevelDataSystem.Instance == null)
                new GameObject("LevelDataSystem")
                    .AddComponent<BoltSort.LevelData.LevelDataSystem>();
            _lds = BoltSort.LevelData.LevelDataSystem.Instance;

            if (BoltSort.GameStateManager.GameStateManager.Instance == null)
                new GameObject("GameStateManager")
                    .AddComponent<BoltSort.GameStateManager.GameStateManager>();
            _gsm = BoltSort.GameStateManager.GameStateManager.Instance;

            _sortMechanic = new GameObject("SortMechanic")
                .AddComponent<BoltSort.SortMechanic.SortMechanic>();
        }

        private static System.Threading.Tasks.Task<(bool Succeeded, string Text)> LoadLevelsFromResources()
        {
            var asset = Resources.Load<TextAsset>("levels");
            return asset != null
                ? System.Threading.Tasks.Task.FromResult((Succeeded: true,  Text: asset.text))
                : System.Threading.Tasks.Task.FromResult((Succeeded: false, Text: (string)null));
        }

        private void EnsureCamera()
        {
            if (Camera.main != null) return;
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags   = CameraClearFlags.SolidColor;
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGO.AddComponent<AudioListener>();
        }

        private static void ConfigureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.backgroundColor  = BoltSortTheme.BackgroundDeep;
            cam.orthographicSize = 9.6f;
            cam.orthographic     = true;
        }
    }
}
