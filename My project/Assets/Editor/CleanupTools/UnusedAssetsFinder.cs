using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class UnusedAssetsFinder
{
    [MenuItem("Cleanup/Generate Unused Asset Report")]
    public static void GenerateReport()
    {
        var allAssetPaths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !p.StartsWith("Assets/Editor/") && !p.EndsWith(".meta"))
            .ToArray();

        // Scenes in build
        var scenePaths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var used = new HashSet<string>();
        foreach (var scene in scenePaths)
        {
            var deps = AssetDatabase.GetDependencies(scene, true);
            foreach (var d in deps) used.Add(d);
        }

        // Also include dependencies of all prefabs and scriptable objects
        var prefabs = AssetDatabase.FindAssets("t:Prefab").Select(g => AssetDatabase.GUIDToAssetPath(g));
        foreach (var p in prefabs)
        {
            var deps = AssetDatabase.GetDependencies(p, true);
            foreach (var d in deps) used.Add(d);
        }

        // Find Resources.Load and Addressables usage (simple heuristic)
        var csFiles = AssetDatabase.FindAssets("t:TextAsset", new[] {"Assets"}).Select(AssetDatabase.GUIDToAssetPath);
        foreach (var cs in csFiles)
        {
            if (!cs.EndsWith(".cs")) continue;
            var text = File.ReadAllText(cs);
            // basic regex-free search
            foreach (var candidate in allAssetPaths)
            {
                var filename = Path.GetFileNameWithoutExtension(candidate);
                if (!string.IsNullOrEmpty(filename) && text.Contains("Resources.Load") && text.Contains(filename))
                    used.Add(candidate);
            }
        }

        var unused = new List<string>();
        foreach (var a in allAssetPaths)
        {
            if (!used.Contains(a) && !a.StartsWith("Assets/Editor/") && !a.Contains("/Plugins/") && !a.Contains("/StreamingAssets/"))
                unused.Add(a);
        }

        var reportDir = "Assets/Reports";
        Directory.CreateDirectory(reportDir);
        File.WriteAllLines(Path.Combine(reportDir, "unused_candidates_before.csv"), unused);
        AssetDatabase.Refresh();
        Debug.Log($"Unused report generated: {reportDir}/unused_candidates_before.csv (count: {unused.Count})");
    }
}
