using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using BoltSort.Visual;
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

        private int _currentLevelId = 1;
        private const int MaxLevelId = 30;
        private const string NextLevelKey = "bs.next_level";

        private void Awake()
        {
            EnsureSaveSystem();
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
        }

        private IEnumerator Start()
        {
            _lds.InitializeAsync();
            yield return new WaitUntil(() => _lds.IsReady);

            _currentLevelId = ReadTargetLevel();

            // Background controller (gradient + vignette + ambient particles)
            var bgGO = new GameObject("BackgroundController");
            bgGO.AddComponent<BackgroundController>();

            var boardGO = new GameObject("Board");
            boardGO.AddComponent<BoardView>().Initialize(_gsm, _sortMechanic);

            var hudGO = new GameObject("HUD");
            _hud = hudGO.AddComponent<HUDController>();
            _hud.Initialize(_gsm, _sortMechanic,
                onReset:    ResetLevel,
                onNextLevel: LoadNextLevel,
                onUndo:     OnUndoClicked,
                onMenu:     OnMenuClicked,
                onReplay:   ResetLevel);

            _gsm.ExitLevel();
            _gsm.LoadLevel(_currentLevelId);
            AudioMgr.Instance?.PlayMusic();
        }

        // ── Level management ──────────────────────────────────────────────────────

        public void ResetLevel()
        {
            _gsm.ExitLevel();
            _gsm.LoadLevel(_currentLevelId);
        }

        public void LoadNextLevel()
        {
            if (_currentLevelId >= MaxLevelId)
            {
                _hud?.ShowMoreLevelsSoon();
                return;
            }
            _currentLevelId++;
            _gsm.ExitLevel();
            _gsm.LoadLevel(_currentLevelId);
        }

        private void OnUndoClicked() => _gsm.UndoRequested();

        private void OnMenuClicked()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("MainMenu");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // ── Save wiring ───────────────────────────────────────────────────────────

        private async void HandleLevelComplete(int levelId, int moves, int par, long seqId)
        {
            int stars     = moves <= par ? 3 : moves <= (int)(par * 1.5f) ? 2 : 1;
            var ss        = SaveSystem.SaveSystem.Instance;
            if (ss == null || !ss.IsReady) return;

            int savedCurrent = ss.GetCurrentLevelId();
            int newCurrent   = Math.Max(levelId + 1, savedCurrent);
            newCurrent       = Math.Min(newCurrent, MaxLevelId + 1);

            await ss.WriteCompletionAtomic(levelId, stars, Application.version, newCurrent);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private int ReadTargetLevel()
        {
            if (PlayerPrefs.HasKey(NextLevelKey))
            {
                int id = PlayerPrefs.GetInt(NextLevelKey, 1);
                PlayerPrefs.DeleteKey(NextLevelKey);
                return Mathf.Clamp(id, 1, MaxLevelId);
            }
            var ss = SaveSystem.SaveSystem.Instance;
            return (ss != null && ss.IsReady)
                ? Mathf.Clamp(ss.GetCurrentLevelId(), 1, MaxLevelId)
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
