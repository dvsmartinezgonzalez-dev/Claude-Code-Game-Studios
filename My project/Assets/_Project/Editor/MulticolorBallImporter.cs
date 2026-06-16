using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// BoltSort > Import Multicolor Ball Sheet — configures ball_multicolor_sheet.png
    /// as a Multiple-mode sprite sheet sliced into 128×128 cells for the animated
    /// multicolor wildcard ball. Run once after the PNG is placed in Resources/Sprites/Balls/.
    /// </summary>
    public static class MulticolorBallImporter
    {
        private const string SheetPath = "Assets/Resources/Sprites/Balls/ball_multicolor_sheet.png";
        private const int    CellSize  = 128;

        [MenuItem("BoltSort/Import Multicolor Ball Sheet")]
        public static void Import()
        {
            if (!File.Exists(Path.GetFullPath(SheetPath)))
            {
                Debug.LogError($"[MulticolorBallImporter] Sheet not found at {SheetPath}. " +
                               "Copy ball_multicolor_sprite_Sheet.png to Resources/Sprites/Balls/ball_multicolor_sheet.png.");
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
                Debug.LogError("[MulticolorBallImporter] Could not get TextureImporter for sheet.");
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

            // Auto-slice into CellSize × CellSize cells
            ti.spritesheet = GenerateSpriteMetaData(SheetPath, CellSize);
            ti.SaveAndReimport();

            AssetDatabase.Refresh();
            int count = ti.spritesheet.Length;
            Debug.Log($"[MulticolorBallImporter] Sliced {count} frame(s) at {CellSize}px from {SheetPath}.");
            EditorUtility.DisplayDialog("Multicolor Ball Importer",
                $"Done. {count} frame(s) sliced.", "OK");
        }

        private static SpriteMetaData[] GenerateSpriteMetaData(string assetPath, int cellSize)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return System.Array.Empty<SpriteMetaData>();

            int cols   = tex.width  / cellSize;
            int rows   = tex.height / cellSize;
            int total  = cols * rows;
            var metas  = new SpriteMetaData[total];
            int idx    = 0;

            for (int row = rows - 1; row >= 0; row--)   // top-to-bottom in display order
            {
                for (int col = 0; col < cols; col++)
                {
                    metas[idx] = new SpriteMetaData
                    {
                        name   = $"ball_multicolor_{idx:D2}",
                        rect   = new Rect(col * cellSize, row * cellSize, cellSize, cellSize),
                        pivot  = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center,
                    };
                    idx++;
                }
            }
            return metas;
        }
    }
}
