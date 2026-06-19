using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// BoltSort &gt; Import Thunder Sheet — configures thunder_sheet.png as a Multiple-mode
    /// sprite sheet sliced into 192×512 cells (8 columns × 2 rows = 16 frames) for the
    /// Undo thunder effect (<see cref="BoltSort.Gameplay.ThunderUndoEffect"/>). Run once after
    /// the PNG is placed in Resources/Sprites/Effects/.
    ///
    /// Frames are named thunder_00 … thunder_15 in top-to-bottom, left-to-right display order so
    /// the runtime (which sorts sliced sprites by name) plays them in authoring order.
    /// </summary>
    public static class ThunderSheetImporter
    {
        private const string SheetPath = "Assets/Resources/Sprites/Effects/thunder_sheet.png";
        private const int    CellW     = 192;   // 1536 / 8 columns
        private const int    CellH     = 512;   // 1024 / 2 rows

        [MenuItem("BoltSort/Import Thunder Sheet")]
        public static void Import()
        {
            if (!File.Exists(Path.GetFullPath(SheetPath)))
            {
                Debug.LogError($"[ThunderSheetImporter] Sheet not found at {SheetPath}. " +
                               "Copy thunder.png to Resources/Sprites/Effects/thunder_sheet.png.");
                return;
            }

            var ti = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (ti == null)
            {
                AssetDatabase.ImportAsset(SheetPath);
                AssetDatabase.Refresh();
                ti = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            }
            if (ti == null)
            {
                Debug.LogError("[ThunderSheetImporter] Could not get TextureImporter for sheet.");
                return;
            }

            ti.textureType         = TextureImporterType.Sprite;
            ti.spriteImportMode    = SpriteImportMode.Multiple;
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

            ti.spritesheet = GenerateSpriteMetaData(SheetPath, CellW, CellH);
            ti.SaveAndReimport();

            AssetDatabase.Refresh();
            int count = ti.spritesheet.Length;
            Debug.Log($"[ThunderSheetImporter] Sliced {count} frame(s) at {CellW}×{CellH}px from {SheetPath}.");
            EditorUtility.DisplayDialog("Thunder Sheet Importer",
                $"Done. {count} frame(s) sliced.", "OK");
        }

        private static SpriteMetaData[] GenerateSpriteMetaData(string assetPath, int cellW, int cellH)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return System.Array.Empty<SpriteMetaData>();

            int cols  = tex.width  / cellW;
            int rows  = tex.height / cellH;
            int total = cols * rows;
            var metas = new SpriteMetaData[total];
            int idx   = 0;

            for (int row = rows - 1; row >= 0; row--)   // top-to-bottom in display order
            {
                for (int col = 0; col < cols; col++)
                {
                    metas[idx] = new SpriteMetaData
                    {
                        name      = $"thunder_{idx:D2}",
                        rect      = new Rect(col * cellW, row * cellH, cellW, cellH),
                        pivot     = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center,
                    };
                    idx++;
                }
            }
            return metas;
        }
    }
}
