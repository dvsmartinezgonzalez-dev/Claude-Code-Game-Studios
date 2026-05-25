using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Drop on any empty GameObject and press Play.
    /// Programmatically creates LevelDataSystem, GameStateManager, and SortMechanic,
    /// loads levels.json from Resources/, and starts the sorting puzzle at level 1.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private BoltSort.LevelData.LevelDataSystem         _lds;
        private BoltSort.GameStateManager.GameStateManager _gsm;
        private BoltSort.SortMechanic.SortMechanic         _sortMechanic;

        private int _currentLevelId = 1;
        private const int MaxLevelId = 3;

        private void Awake()
        {
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
        }

        private IEnumerator Start()
        {
            _lds.InitializeAsync();
            yield return new WaitUntil(() => _lds.IsReady);

            new GameObject("Board").AddComponent<BoardView>()
                .Initialize(_gsm, _sortMechanic);

            new GameObject("HUD").AddComponent<HUDController>()
                .Initialize(_gsm, _sortMechanic, ResetLevel, LoadNextLevel);

            _gsm.LoadLevel(_currentLevelId);
        }

        public void ResetLevel()
        {
            _gsm.ExitLevel();
            _gsm.LoadLevel(_currentLevelId);
        }

        public void LoadNextLevel()
        {
            _currentLevelId = _currentLevelId < MaxLevelId ? _currentLevelId + 1 : 1;
            _gsm.ExitLevel();
            _gsm.LoadLevel(_currentLevelId);
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

        private static Task<(bool Succeeded, string Text)> LoadLevelsFromResources()
        {
            var asset = Resources.Load<TextAsset>("levels");
            return asset != null
                ? Task.FromResult((Succeeded: true,  Text: asset.text))
                : Task.FromResult((Succeeded: false, Text: (string)null));
        }

        private void EnsureCamera()
        {
            if (Camera.main != null) return;
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGO.AddComponent<AudioListener>();
        }

        // Applies portrait-first camera settings at runtime regardless of scene defaults.
        private void ConfigureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.backgroundColor  = new Color(0.051f, 0.051f, 0.102f, 1f); // #0D0D1A
            cam.orthographicSize = 9.6f;  // 19.2 world-unit tall view at 100 ppu (1080×1920 ref)
            cam.orthographic     = true;
        }
    }
}
