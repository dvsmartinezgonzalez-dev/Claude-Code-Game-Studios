using System;
using System.IO;
using System.Linq;
using BoltSort.LevelData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// BoltSort &gt; QA Level Loader — DEV/QA ONLY (Editor assembly, never shipped).
    ///
    /// Jumps straight into any authored level for manual verification, bypassing
    /// normal progression, and prints that level's known OPTIMAL solution path to
    /// the Console so a tester can step through it by hand to validate the level
    /// and its special mechanics (multicolor / frozen / asymmetric / mystery).
    ///
    /// It writes the same PlayerPrefs key GameBootstrap reads ("bs.next_level"),
    /// opens the Gameplay scene, and enters Play mode. No gameplay code is touched
    /// and nothing here compiles into a player build.
    ///
    /// The solution path is read from the deterministic Python export at
    /// tools/levels/solutions.json (produced by export_solutions.py). If that file
    /// is absent the loader still works — it just reports the trace as unavailable.
    /// </summary>
    public class QaLevelLoaderWindow : EditorWindow
    {
        private const string NextLevelKey  = "bs.next_level";
        private const string GameplayScene = "Assets/_Project/Scenes/Gameplay.unity";

        private int    _levelId = 1;
        private string _status;
        private string _trace;

        [MenuItem("BoltSort/QA Level Loader")]
        private static void Open() => GetWindow<QaLevelLoaderWindow>("QA Level Loader");

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Dev-only level jump + solution trace", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Loads any level directly and logs its known optimal solution for manual QA. " +
                "Editor-only — not exposed to players.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            _levelId = EditorGUILayout.IntField("Level ID", _levelId);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play This Level", GUILayout.Height(28))) PlayLevel();
            if (GUILayout.Button("Show Solution", GUILayout.Height(28)))   ShowSolution();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
            if (!string.IsNullOrEmpty(_trace))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Optimal solution (Tn = color tube, Bm = buffer):",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(_trace,
                    EditorStyles.textArea, GUILayout.MinHeight(80));
            }
        }

        private void PlayLevel()
        {
            int max = MaxLevelId();
            if (max > 0) _levelId = Mathf.Clamp(_levelId, 1, max);

            PlayerPrefs.SetInt(NextLevelKey, _levelId);
            PlayerPrefs.Save();
            ShowSolution();

            if (File.Exists(GameplayScene))
            {
                EditorSceneManager.OpenScene(GameplayScene);
                EditorApplication.isPlaying = true;
                _status = $"Loaded level {_levelId} in Gameplay scene (Play mode).";
            }
            else
            {
                _status = $"Set next_level={_levelId}. Open the Gameplay scene and press Play " +
                          $"(scene not found at {GameplayScene}).";
            }
        }

        private void ShowSolution()
        {
            string path = SolutionsPath();
            if (!File.Exists(path))
            {
                _trace  = null;
                _status = $"No solution trace found. Generate it with:\n" +
                          $"  cd tools/levels && python export_solutions.py";
                return;
            }

            try
            {
                var root  = JObject.Parse(File.ReadAllText(path));
                var entry = root["levels"]?.FirstOrDefault(l => (int)l["level_id"] == _levelId);
                if (entry == null)
                {
                    _trace  = null;
                    _status = $"Level {_levelId} not present in solutions.json.";
                    return;
                }

                bool solvable = (bool)(entry["solvable"] ?? false);
                int  opt      = entry["optimal_moves"]?.Type == JTokenType.Integer
                              ? (int)entry["optimal_moves"] : -1;
                var  moves    = entry["moves"]?.Select(m => (string)m).ToArray() ?? Array.Empty<string>();
                var  mechs    = entry["mechanics"]?.Select(m => (string)m).ToArray() ?? Array.Empty<string>();

                string mechStr = mechs.Length > 0 ? $"  mechanics: {string.Join(", ", mechs)}" : "";
                _trace = string.Join("   ", moves);
                _status = $"L{_levelId}: solvable={solvable}, optimal={opt}, {moves.Length} moves.{mechStr}";
                Debug.Log($"[QA] L{_levelId} optimal solution ({moves.Length} moves){mechStr}:\n  " +
                          string.Join("  ", moves));
            }
            catch (Exception ex)
            {
                _trace  = null;
                _status = $"Failed to read solutions.json: {ex.Message}";
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────────
        private static string SolutionsPath()
            // Application.dataPath = <repo>/My project/Assets → up two to repo root.
            => Path.GetFullPath(Path.Combine(Application.dataPath, "../../tools/levels/solutions.json"));

        private static int MaxLevelId()
        {
            try
            {
                string json = File.ReadAllText(Application.dataPath + "/Resources/levels.json");
                var cat = JsonConvert.DeserializeObject<LevelCatalogue>(json);
                return cat?.Levels == null || cat.Levels.Length == 0
                    ? 0 : cat.Levels.Max(l => l.LevelId);
            }
            catch { return 0; }
        }
    }
}
