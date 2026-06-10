using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// BoltSort > Import Sprite Settings — ensures every PNG in Resources/Sprites/
    /// is imported as Sprite (2D and UI) with alpha transparency enabled.
    /// Run after adding new PNGs to the folder.
    /// Also fires automatically via AssetPostprocessor on new imports.
    /// </summary>
    public class SpriteImportOptimizer : AssetPostprocessor
    {
        private const string SpritesFolder = "Assets/Resources/Sprites";

        // Auto-applies on every new texture import inside SpritesFolder
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpritesFolder)) return;
            var ti = (TextureImporter)assetImporter;
            if (ti.textureType != TextureImporterType.Sprite)
                ApplySettings(ti);
        }

        [MenuItem("BoltSort/Import Sprite Settings")]
        public static void OptimizeAll()
        {
            int count = 0;
            if (!Directory.Exists(SpritesFolder))
            {
                Debug.LogWarning("[SpriteImportOptimizer] Folder not found: " + SpritesFolder);
                return;
            }

            foreach (string file in Directory.GetFiles(SpritesFolder, "*.png", SearchOption.AllDirectories))
            {
                string ap = file.Replace("\\", "/");
                // Normalize to project-relative path
                int idx = ap.IndexOf("Assets/");
                if (idx < 0) continue;
                ap = ap[idx..];

                var ti = AssetImporter.GetAtPath(ap) as TextureImporter;
                if (ti == null) continue;
                ApplySettings(ti);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SpriteImportOptimizer] Applied Sprite settings to {count} PNG(s).");
            EditorUtility.DisplayDialog("Sprite Import Optimizer",
                $"Done. {count} sprite(s) configured.", "OK");
        }

        private static void ApplySettings(TextureImporter ti)
        {
            ti.textureType         = TextureImporterType.Sprite;
            ti.spriteImportMode    = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.alphaSource         = TextureImporterAlphaSource.FromInput;
            ti.sRGBTexture         = true;
            ti.mipmapEnabled       = false;
            ti.filterMode          = FilterMode.Bilinear;
            ti.maxTextureSize      = 2048;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);

            var platform = ti.GetDefaultPlatformTextureSettings();
            platform.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SetPlatformTextureSettings(platform);

            ti.SaveAndReimport();
        }
    }
}
