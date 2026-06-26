using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class TextureAndAudioOptimizer
{
    [MenuItem("Cleanup/Optimize Large Textures and Audio (Conservative)")]
    public static void RunOptimization()
    {
        var textures = AssetDatabase.FindAssets("t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).ToList();
        var report = new List<string>();
        foreach (var path in textures)
        {
            var fi = new FileInfo(path);
            if (!File.Exists(path)) continue;
            var sizeKb = new FileInfo(path).Length / 1024f;
            // conservative threshold
            if (sizeKb < 200f) continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            // skip editor-only or plugin folders
            if (path.Contains("/Editor/") || path.Contains("/Plugins/") || path.Contains("/StreamingAssets/")) continue;

            // read texture size via importer if available
            int originalMax = importer.maxTextureSize;

            // Heuristics by name
            var lower = Path.GetFileName(path).ToLower();
            int newSize = importer.maxTextureSize;
            if (lower.Contains("bg") || lower.Contains("background") || lower.Contains("fondo"))
                newSize = Mathf.Min(importer.maxTextureSize, 2048);
            else if (lower.Contains("rayo") || lower.Contains("ray") || lower.Contains("bolt") || lower.Contains("lightning"))
                newSize = Mathf.Min(importer.maxTextureSize, 512);
            else
                newSize = Mathf.Min(importer.maxTextureSize, 1024);

            if (newSize < importer.maxTextureSize)
            {
                importer.maxTextureSize = newSize;
            }
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 50;
            importer.SaveAndReimport();

            report.Add($"{path},oldMax={originalMax},newMax={importer.maxTextureSize},sizeKB~{sizeKb}");
        }

        var repDir = "Assets/Reports";
        Directory.CreateDirectory(repDir);
        File.WriteAllLines(Path.Combine(repDir, "texture_compression_report.csv"), report);
        AssetDatabase.Refresh();
        Debug.Log($"Texture optimization finished. Report: {repDir}/texture_compression_report.csv");
    }
}
