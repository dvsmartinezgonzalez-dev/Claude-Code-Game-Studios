using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// One-shot menu command that repairs all three game scenes and updates Build Settings.
    /// Run via BoltSort ▶ Setup Scenes once after opening the project.
    /// </summary>
    public static class SceneSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/MainMenu.unity",
            "Assets/_Project/Scenes/LevelSelect.unity",
            "Assets/_Project/Scenes/Gameplay.unity",
        };

        private static readonly string[] ControllerTypeNames =
        {
            "BoltSort.Gameplay.MainMenuController",
            "BoltSort.Gameplay.LevelSelectController",
            "BoltSort.Gameplay.GameBootstrap",
        };

        [MenuItem("BoltSort/Setup Scenes")]
        static void SetupAll()
        {
            // Save current scene so we can restore it
            string currentScenePath = EditorSceneManager.GetActiveScene().path;

            for (int i = 0; i < ScenePaths.Length; i++)
                EnsureScene(ScenePaths[i], ControllerTypeNames[i]);

            UpdateBuildSettings();

            // Restore original scene
            if (!string.IsNullOrEmpty(currentScenePath) &&
                File.Exists(currentScenePath))
                EditorSceneManager.OpenScene(currentScenePath);

            Debug.Log("[SceneSetup] All scenes configured. Build Settings updated. ✓");
            EditorUtility.DisplayDialog("BoltSort Scene Setup",
                "All scenes are in Build Settings.\n\n" +
                "Build order:\n" +
                "  0 — MainMenu\n" +
                "  1 — LevelSelect\n" +
                "  2 — Gameplay\n\n" +
                "Press Play from MainMenu.unity to test.",
                "OK");
        }

        // ── Scene ensure ──────────────────────────────────────────────────────────

        static void EnsureScene(string scenePath, string controllerTypeName)
        {
            bool sceneExists = File.Exists(scenePath);

            UnityEngine.SceneManagement.Scene scene;
            if (sceneExists)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                                                    NewSceneMode.Single);
                Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);
            }

            EnsureController(scene, controllerTypeName);

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SceneSetup] {Path.GetFileName(scenePath)} ✓");
        }

        static void EnsureController(UnityEngine.SceneManagement.Scene scene,
                                     string fullTypeName)
        {
            System.Type type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogWarning($"[SceneSetup] Type not found: {fullTypeName}. " +
                                 "Script may not be compiled yet.");
                return;
            }

            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent(type) != null)
                    return; // already present

            var controllerGO = new GameObject(type.Name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                controllerGO, scene);
            controllerGO.AddComponent(type);
            Debug.Log($"[SceneSetup] Added {type.Name} to {scene.name}");
        }

        static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        // ── Build Settings ────────────────────────────────────────────────────────

        static void UpdateBuildSettings()
        {
            var existing = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            foreach (string path in ScenePaths)
            {
                bool found = false;
                foreach (var s in existing)
                    if (s.path == path) { found = true; break; }

                if (!found)
                    existing.Add(new EditorBuildSettingsScene(path, true));
                else
                {
                    // Ensure enabled
                    for (int i = 0; i < existing.Count; i++)
                        if (existing[i].path == path)
                            existing[i] = new EditorBuildSettingsScene(path, true);
                }
            }

            // Re-order so our scenes are first: MainMenu(0), LevelSelect(1), Gameplay(2)
            var ordered = new List<EditorBuildSettingsScene>();
            foreach (string path in ScenePaths)
                foreach (var s in existing)
                    if (s.path == path) { ordered.Add(s); break; }

            foreach (var s in existing)
            {
                bool inOrder = false;
                foreach (string path in ScenePaths)
                    if (s.path == path) { inOrder = true; break; }
                if (!inOrder) ordered.Add(s);
            }

            EditorBuildSettings.scenes = ordered.ToArray();
        }
    }
}
