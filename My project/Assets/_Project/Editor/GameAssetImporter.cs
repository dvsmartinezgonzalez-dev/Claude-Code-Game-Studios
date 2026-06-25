using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// One-shot menu command that configures TextureImporter settings and
    /// SpriteMetaData slices for game_assets PNGs and the button sheets.
    /// Run via BoltSort ▶ Import Game Assets after placing PNGs in the project.
    /// Pixel rects use Unity's bottom-left origin (y = imageH − screenY_bottom).
    ///
    /// Button sheets live under Assets/Resources/Sprites/Buttons/ (not game_assets/)
    /// so their sliced sub-sprites ship in device builds — GameAssets loads them via
    /// Resources.LoadAll, which only sees assets inside a Resources folder.
    /// </summary>
    public static class GameAssetImporter
    {
        [MenuItem("BoltSort/Import Game Assets")]
        public static void ImportAll()
        {
            ImportButtonsSet1();
            ImportButtonsSet2();
            ImportStarsProgressCoins();
            ImportScrewsColorful();
            ImportScrewsRow1();
            ImportNutsMetallic();
            ImportNutsBasic();
            ImportTubeContainers();
            ImportLevelButtonsGrid();
            ImportBallsAndTubes();
            ImportHelperTubes();
            ImportConfettiSheet();
            ImportShopTabs();
            ImportShopWallpapers();
            ImportShopRedesign();
            ImportParticlesSprite();

            // Single-sprite images — name differs from filename → use named single entry
            ImportNamed("Assets/game_assets/level_selector/level_unlocked_1.png",
                        "tile_level_unlocked", 1024, 1024);
            ImportNamed("Assets/game_assets/level_selector/level_locked.png",
                        "tile_level_locked",   1024, 1024);
            ImportNamed("Assets/game_assets/screens/ad_screen.png",
                        "ad_frame",            2048, 1152);
            ImportNamed("Assets/game_assets/shop/shop_frame.png",
                        "shop_panel",          1152, 2048);

            // Single-sprite images — name matches filename, use Single mode
            ImportSingle("Assets/game_assets/screens/game_background.png");
            ImportSingle("Assets/game_assets/screens/victory_screen.png");

            AssetDatabase.Refresh();
            Debug.Log("[GameAssetImporter] All game_assets imported successfully.");
        }

        // ── Sprite sheet importers ────────────────────────────────────────────────

        static void ImportButtonsSet1()
        {
            // 1376×768  bg = checkerboard (painted, not transparent)
            // Row 1 (screen y 73–353, h=280): PLAY wide button + red settings circle
            // Row 2 (screen y 432–705, h=273): blue pause | orange retry | yellow settings | teal sound
            // Lives in Resources/ so GameAssets.LoadFromSheet can read its sliced
            // sub-sprites via Resources.LoadAll on device builds.
            const int H = 768;
            ImportSheet("Assets/Resources/Sprites/Buttons/ui_buttons_set1.png", new List<SpriteMetaData>
            {
                Spr("btn_play",            58, H-353,  652, 280),
                Spr("btn_settings",      1037, H-353,  272, 280),
                Spr("btn_pause",           66, H-705,  271, 273),
                Spr("btn_retry",          389, H-705,  273, 273),
                Spr("btn_settings_pause", 713, H-705,  272, 273),
                Spr("btn_sound",         1038, H-705,  271, 273),
            });
        }

        static void ImportButtonsSet2()
        {
            // 1376×768
            // Row 1 (screen y 66–291, h=225): yellow home | blue back | green continue | red close
            // Row 2 (screen y 425–668, h=243): red close_alt | purple settings_sq | cyan sound_on | grey sound_off
            // Lives in Resources/ — see ImportButtonsSet1 note.
            const int H = 768;
            ImportSheet("Assets/Resources/Sprites/Buttons/ui_buttons_set2.png", new List<SpriteMetaData>
            {
                Spr("btn_home",            72, H-291, 223, 225),
                Spr("btn_back",           400, H-291, 221, 225),
                Spr("btn_continue",       752, H-291, 221, 225),
                Spr("btn_close",         1079, H-291, 223, 225),
                Spr("btn_close_alt",       57, H-668, 240, 243),
                Spr("btn_settings_square",385, H-668, 243, 243),
                Spr("btn_sound_on",       728, H-668, 244, 243),
                Spr("btn_sound_off",     1079, H-668, 223, 243),
            });
        }

        static void ImportStarsProgressCoins()
        {
            // 1376×768 — irregular layout (items at different vertical positions)
            // big star (top-left) | progress bar (top-center) | coin (top-right)
            // medium star (bot-left) | small star (bot-center-left) | progress circle (bot-center)
            const int H = 768;
            ImportSheet("Assets/game_assets/decorations/stars_progress_coins.png", new List<SpriteMetaData>
            {
                Spr("star_large",        60, H-425,  360, 355),
                Spr("progress_bar",     490, H-265,  500, 195),
                Spr("coin_icon",       1025, H-375,  300, 305),
                Spr("star_medium",       60, H-690,  250, 280),
                Spr("star_small",       265, H-690,  180, 220),
                Spr("progress_circle",  490, H-715,  465, 330),
            });
        }

        static void ImportScrewsColorful()
        {
            // 2928×352 — 8 Phillips-head screws, single row (detected y 32–329, h=297)
            // Colors L→R: Red Blue Green Yellow Orange Purple Pink Cyan
            const int H = 352; int y = H - 329; int h = 297;
            ImportSheet("Assets/game_assets/game_elements/screws_individual_colorful.png", new List<SpriteMetaData>
            {
                Spr("screw_red",      98,  y, 177, h),
                Spr("screw_blue",    461,  y, 177, h),
                Spr("screw_green",   823,  y, 177, h),
                Spr("screw_yellow", 1183,  y, 177, h),
                Spr("screw_orange", 1561,  y, 177, h),
                Spr("screw_purple", 1922,  y, 176, h),
                Spr("screw_pink",   2285,  y, 178, h),
                Spr("screw_cyan",   2646,  y, 177, h),
            });
        }

        static void ImportScrewsRow1()
        {
            // 2064×512 — 10 hex-head bolts, single row (detected y 87–451, h=364)
            // Colors L→R: Red Blue Green Yellow Orange Purple Pink Cyan Gold Silver
            const int H = 512; int y = H - 451; int h = 364;
            ImportSheet("Assets/game_assets/game_elements/screws_row1.png", new List<SpriteMetaData>
            {
                Spr("screw_hex_red",       47, y, 171, h),
                Spr("screw_hex_blue",     247, y, 170, h),
                Spr("screw_hex_green",    446, y, 171, h),
                Spr("screw_hex_yellow",   646, y, 171, h),
                Spr("screw_hex_orange",   846, y, 171, h),
                Spr("screw_hex_purple",  1048, y, 169, h),
                Spr("screw_hex_pink",    1247, y, 171, h),
                Spr("screw_hex_cyan",    1448, y, 170, h),
                Spr("screw_hex_gold",    1646, y, 172, h),
                Spr("screw_hex_silver",  1858, y, 154, h),
            });
        }

        static void ImportNutsMetallic()
        {
            // 1152×928 — 5 metallic hex nuts, single row (detected y 330–600, h=270)
            // Colors L→R: Silver Gold Copper DarkSteel RoseGold
            // Note: DarkSteel (index 3) was not auto-detected (low saturation) — position estimated
            const int H = 928; int y = H - 600; int h = 270;
            ImportSheet("Assets/game_assets/game_elements/nuts_metallic.png", new List<SpriteMetaData>
            {
                Spr("nut_silver",  25,  y, 204, h),
                Spr("nut_gold",   243,  y, 219, h),
                Spr("nut_copper", 467,  y, 217, h),
                Spr("nut_steel",  690,  y, 220, h),
                Spr("nut_rose",   913,  y, 217, h),
            });
        }

        static void ImportNutsBasic()
        {
            // 2064×512 — 4 large basic hex nuts, single row (detected y 54–444, h=390)
            // Colors L→R: Silver Gold Copper DarkSteel
            // Note: DarkSteel (index 3) was not auto-detected (low saturation) — position estimated
            const int H = 512; int y = H - 444; int h = 390;
            ImportSheet("Assets/game_assets/game_elements/nuts_basic.png", new List<SpriteMetaData>
            {
                Spr("nut_basic_silver",   79, y, 414, h),
                Spr("nut_basic_gold",    567, y, 421, h),
                Spr("nut_basic_copper", 1060, y, 421, h),
                Spr("nut_basic_steel",  1548, y, 421, h),
            });
        }

        static void ImportTubeContainers()
        {
            // 1376×768 — 3 identical glass dome containers (detected y 61–718, h=657)
            // All three are visually identical; only the leftmost is registered as tube_empty
            const int H = 768; int y = H - 718; int h = 657;
            ImportSheet("Assets/game_assets/game_elements/tube_containers.png", new List<SpriteMetaData>
            {
                Spr("tube_empty", 175, y, 290, h),
            });
        }

        static void ImportLevelButtonsGrid()
        {
            // 1024×1024 — 3×3 grid of level number tiles
            // Reading order (L→R, T→B): 1, 2, 5 | 10, 25, 50 | 75, 99, 100
            // Col X ranges: (19,365), (367,660), (675,969)
            // Row Y ranges (screen): (36,365), (363,652), (662,952)
            const int H = 1024;
            ImportSheet("Assets/game_assets/level_selector/level_buttons_grid.png", new List<SpriteMetaData>
            {
                Spr("tile_level_1",    19, H-365, 346, 329),
                Spr("tile_level_2",   367, H-365, 293, 329),
                Spr("tile_level_5",   675, H-365, 294, 329),
                Spr("tile_level_10",   19, H-652, 346, 289),
                Spr("tile_level_25",  367, H-652, 293, 289),
                Spr("tile_level_50",  675, H-652, 294, 289),
                Spr("tile_level_75",   19, H-952, 346, 290),
                Spr("tile_level_99",  367, H-952, 293, 290),
                Spr("tile_level_100", 675, H-952, 294, 290),
            });
        }

        static void ImportBallsAndTubes()
        {
            // Resources/Sprites/Balls/ — 11 ball PNGs, cropped to their opaque bbox.
            // Pivot = Center (0.5, 0.5) per Part 3. PPU = bbox width so each ball
            // renders at 1 world unit at scale 1 (cropped rects are near-square).
            const string ballsDir = "Assets/Resources/Sprites/Balls/";
            ImportCropped(ballsDir + "ball_red.png",        5, 7, 395, 394, BallPivot);
            ImportCropped(ballsDir + "ball_green.png",      7, 8, 392, 393, BallPivot);
            ImportCropped(ballsDir + "ball_blue.png",       7, 8, 394, 392, BallPivot);
            ImportCropped(ballsDir + "ball_orange.png",     6, 8, 393, 393, BallPivot);
            ImportCropped(ballsDir + "ball_purple.png",     6, 7, 395, 395, BallPivot);
            ImportCropped(ballsDir + "ball_pink.png",       6, 9, 392, 392, BallPivot);
            ImportCropped(ballsDir + "ball_yellow.png",     6, 7, 395, 395, BallPivot);
            ImportCropped(ballsDir + "ball_light_blue.png", 6, 7, 394, 395, BallPivot);
            ImportCropped(ballsDir + "ball_brown.png",      8, 8, 391, 392, BallPivot);
            ImportCropped(ballsDir + "ball_grey.png",       8, 8, 393, 393, BallPivot);
            ImportCropped(ballsDir + "ball_black.png",      7, 7, 394, 394, BallPivot);
            // Phase-2 mechanic balls (407x407 source; imported full-image, centered).
            ImportCropped(ballsDir + "ball_mystery.png",    0, 0, 407, 407, BallPivot);
            ImportCropped(ballsDir + "ball_multicolor.png", 0, 0, 407, 407, BallPivot);

            // Resources/Sprites/Tubes/ — 10 tube PNGs, cropped to their opaque bbox.
            // Pivot = Bottom-Center (0.5, 0) per Part 3 so tubes anchor on the board floor.
            const string tubesDir = "Assets/Resources/Sprites/Tubes/";
            ImportCropped(tubesDir + "Tube_unselected_short.png",        55, 257, 480, 1171, TubePivot);
            ImportCropped(tubesDir + "Tube_unselected.png",              51, 153, 480, 1365, TubePivot);
            ImportCropped(tubesDir + "Tube_unselected_large.png",        52,  57, 481, 1564, TubePivot);
            ImportCropped(tubesDir + "Tube_unselected_extra_large.png",  52, 135, 481, 1897, TubePivot);
            ImportCropped(tubesDir + "Tube_unselected_XXL.png",          51,  82, 482, 2471, TubePivot);
            ImportCropped(tubesDir + "Tube_selected_short.png",          12, 216, 568, 1256, TubePivot);
            ImportCropped(tubesDir + "Tube_selected.png",                14, 115, 555, 1442, TubePivot);
            ImportCropped(tubesDir + "Tube_selected_large.png",          10,  16, 563, 1647, TubePivot);
            ImportCropped(tubesDir + "Tube_selected_extra_large.png",    10,  97, 563, 1973, TubePivot);
            ImportCropped(tubesDir + "Tube_selected_XXL.png",            11,  41, 563, 2558, TubePivot);
        }

        static void ImportHelperTubes()
        {
            // Resources/Sprites/Tubes/ — runtime helper (extra) tube art (plus_one/two/three,
            // capacity 1/2/3, each with a _selected variant). Cropped to their opaque content
            // (581×1682 canvases carry large transparent padding) with a Bottom-Center pivot, so
            // the visible tube anchors on the board floor exactly like the standard tubes and
            // balls sit inside the real glass. Rects are Unity bottom-left-origin (measured bbox).
            const string dir = "Assets/Resources/Sprites/Tubes/";
            ImportCropped(dir + "plus_one_tube.png",            55, 257, 480,  553, TubePivot);
            ImportCropped(dir + "plus_two_tube.png",            55, 257, 480,  915, TubePivot);
            ImportCropped(dir + "plus_three_tube.png",          55, 257, 480, 1275, TubePivot);
            ImportCropped(dir + "plus_one_tube_selected.png",   11, 215, 570,  637, TubePivot);
            ImportCropped(dir + "plus_two_tube_selected.png",   11, 215, 570, 1006, TubePivot);
            ImportCropped(dir + "plus_three_tube_selected.png", 11, 215, 570, 1358, TubePivot);
        }

        static void ImportConfettiSheet()
        {
            // 1080×1920 — scattered confetti pieces (top ~45% of image), transparent bg.
            // 34 individual pieces detected via connected-component analysis; each is
            // sliced as its own sub-sprite so the Victory rain can pick random real
            // confetti shapes instead of procedural colored squares.
            // Lives in Resources/Sprites/Victory/ so GameAssets.ConfettiPieces can read
            // the sub-sprites via Resources.LoadAll on device builds.
            const int H = 1920;
            ImportSheet("Assets/Resources/Sprites/Victory/confetti_sheet.png", new List<SpriteMetaData>
            {
                Spr("confetti_0", 306, H-82, 33, 48),
                Spr("confetti_1", 628, H-122, 64, 48),
                Spr("confetti_2", 118, H-154, 53, 79),
                Spr("confetti_3", 853, H-115, 22, 17),
                Spr("confetti_4", 813, H-353, 50, 56),
                Spr("confetti_5", 441, H-322, 15, 17),
                Spr("confetti_6", 246, H-339, 27, 32),
                Spr("confetti_7", 439, H-380, 30, 34),
                Spr("confetti_8", 477, H-382, 17, 31),
                Spr("confetti_9", 85, H-405, 40, 45),
                Spr("confetti_10", 367, H-397, 30, 34),
                Spr("confetti_11", 718, H-471, 94, 96),
                Spr("confetti_12", 622, H-506, 64, 81),
                Spr("confetti_13", 816, H-484, 27, 29),
                Spr("confetti_14", 90, H-524, 53, 62),
                Spr("confetti_15", 176, H-507, 18, 28),
                Spr("confetti_16", 729, H-509, 24, 24),
                Spr("confetti_17", 287, H-502, 19, 16),
                Spr("confetti_18", 324, H-520, 20, 18),
                Spr("confetti_19", 763, H-526, 19, 13),
                Spr("confetti_20", 282, H-540, 26, 21),
                Spr("confetti_21", 894, H-587, 63, 37),
                Spr("confetti_22", 330, H-608, 45, 55),
                Spr("confetti_23", 140, H-619, 44, 56),
                Spr("confetti_24", 804, H-627, 24, 25),
                Spr("confetti_25", 220, H-661, 44, 52),
                Spr("confetti_26", 93, H-659, 18, 21),
                Spr("confetti_27", 999, H-675, 27, 26),
                Spr("confetti_28", 141, H-706, 51, 50),
                Spr("confetti_29", 604, H-720, 18, 14),
                Spr("confetti_30", 727, H-758, 20, 17),
                Spr("confetti_31", 808, H-785, 15, 18),
                Spr("confetti_32", 518, H-854, 33, 45),
                Spr("confetti_33", 678, H-852, 39, 36),
            });
        }

        // ── Shop skin assets ──────────────────────────────────────────────────────

        // Selected_buttons.png (1080×357): top half = selected tab state, bottom = unselected.
        // Sliced into Sprites/Shop/Selected_buttons so GameAssets.LoadFromSheet can read it.
        static void ImportShopTabs()
        {
            const string path = "Assets/Resources/Sprites/Shop/Selected_buttons.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning($"[GameAssetImporter] Asset not found: {path}"); return; }
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int halfTop = h - h / 2;       // top half height (Unity y origin = bottom-left)
            int halfBot = h / 2;
            ImportSheet(path, new List<SpriteMetaData>
            {
                Spr("tab_selected",   0, h - halfTop, w, halfTop),
                Spr("tab_unselected", 0, 0,           w, halfBot),
            });
        }

        // Wallpapers (.jpg) → single sprites under Resources/Sprites/Shop/Wallpapers/.
        static void ImportParticlesSprite()
        {
            // 1080×1920 — scattered white spherical particle shapes (lower half of image).
            // Imported as a single sprite so GameAssets.AmbientParticleSprite can load it
            // for use in BackgroundController's ambient particle system.
            ImportSingle("Assets/Resources/Sprites/Levels/particles.png");
        }

        // Redesigned Shop scene art (assets_admin/Shop/Shop_elements + Main_menu).
        // Each PNG/JPG is a self-contained single sprite whose filename already matches the
        // resource name GameAssets requests, so a plain Single import is all that's needed.
        static readonly string[] ShopRedesignSprites =
        {
            "shop_background.jpg", "banner_shop.png", "exit_button.png", "coin_2.png",
            "diamond_2.png", "card_bg.png", "etiqueta.png", "more_coins_2.png",
            "btn_green.png", "btn_light_blue.png", "btn_yellow.png", "btn_blue.png",
            "tab_tubes.png", "tab_balls.png", "tab_backgrounds.png", "tab_specials.png",
        };

        static void ImportShopRedesign()
        {
            const string dir = "Assets/Resources/Sprites/Shop/";
            foreach (var file in ShopRedesignSprites)
            {
                string path = dir + file;
                if (AssetImporter.GetAtPath(path) != null) ImportSingle(path);
            }
        }

        static void ImportShopWallpapers()
        {
            const string dir = "Assets/Resources/Sprites/Shop/Wallpapers";
            if (!System.IO.Directory.Exists(dir)) return;
            foreach (var file in System.IO.Directory.GetFiles(dir))
            {
                if (file.EndsWith(".meta")) continue;
                string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") continue;
                ImportWallpaper(file.Replace('\\', '/'));
            }
        }

        static void ImportWallpaper(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning($"[GameAssetImporter] Asset not found: {path}"); return; }
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode          = FilterMode.Bilinear;
            importer.maxTextureSize      = 1024;
            importer.textureCompression  = TextureImporterCompression.Compressed;
            importer.mipmapEnabled       = false;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[GameAssetImporter] {path} → wallpaper sprite");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        static readonly Vector2 BallPivot = new Vector2(0.5f, 0.5f);
        static readonly Vector2 TubePivot = new Vector2(0.5f, 0f);

        /// <summary>
        /// Imports a single-sprite PNG cropped to (x, y, w, h) — Unity bottom-left-origin
        /// rect — with the given normalized pivot and spritePixelsPerUnit = w (so the
        /// cropped sprite is exactly 1 world unit wide at scale 1).
        /// </summary>
        static void ImportCropped(string path, int x, int y, int w, int h, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[GameAssetImporter] Asset not found: {path}");
                return;
            }
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = w;
            importer.filterMode          = FilterMode.Bilinear;
            importer.maxTextureSize      = 4096;
            importer.textureCompression  = TextureImporterCompression.CompressedHQ;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled       = false;
            importer.spritesheet = new[]
            {
                new SpriteMetaData
                {
                    name      = System.IO.Path.GetFileNameWithoutExtension(path),
                    rect      = new Rect(x, y, w, h),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot     = pivot,
                },
            };
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[GameAssetImporter] {path} → cropped sprite ({w}x{h}, pivot {pivot})");
        }

        static SpriteMetaData Spr(string name, int x, int y, int w, int h) =>
            new SpriteMetaData
            {
                name      = name,
                rect      = new Rect(x, y, w, h),
                alignment = (int)SpriteAlignment.Center,
                pivot     = new Vector2(0.5f, 0.5f),
            };

        static void ImportSheet(string path, List<SpriteMetaData> sprites)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[GameAssetImporter] Asset not found: {path}");
                return;
            }
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode          = FilterMode.Bilinear;
            importer.maxTextureSize      = 4096;
            importer.textureCompression  = TextureImporterCompression.CompressedHQ;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled       = false;
            importer.spritesheet         = sprites.ToArray();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[GameAssetImporter] {path} → {sprites.Count} sprites");
        }

        // Uses Multiple mode with one full-image entry so the sprite gets the exact name requested.
        static void ImportNamed(string path, string spriteName, int imageW, int imageH)
        {
            ImportSheet(path, new List<SpriteMetaData>
            {
                Spr(spriteName, 0, 0, imageW, imageH),
            });
        }

        // For images whose filename already matches the desired sprite name.
        static void ImportSingle(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[GameAssetImporter] Asset not found: {path}");
                return;
            }
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode          = FilterMode.Bilinear;
            importer.maxTextureSize      = 4096;
            importer.textureCompression  = TextureImporterCompression.CompressedHQ;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled       = false;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[GameAssetImporter] {path} → single sprite");
        }
    }

    /// <summary>
    /// Automatically slices game_assets sprites on Editor load if they haven't been imported yet.
    /// Checks for the btn_play sub-sprite as a proxy for whether ImportAll has been run.
    /// </summary>
    [InitializeOnLoad]
    static class GameAssetAutoImport
    {
        static GameAssetAutoImport()
        {
            EditorApplication.delayCall += AutoImportIfNeeded;
        }

        static void AutoImportIfNeeded()
        {
            // Re-run the importer if EITHER the button sheets aren't sliced yet OR the
            // tube sprites still carry their default (center) pivot. The tube check is
            // essential: buttons may already be sliced from an earlier run, which would
            // otherwise make us skip importing the newer ball/tube art entirely.
            if (ButtonsSliced() && TubesBottomPivot() && HelperTubesConfigured() &&
                ConfettiSliced() && ShopTabsSliced() && ParticlesImported() &&
                ShopRedesignImported()) return;

            Debug.Log("[GameAssets] Game assets not fully configured — auto-importing...");
            GameAssetImporter.ImportAll();
        }

        static bool ButtonsSliced()
        {
            const string probeAsset = "Assets/Resources/Sprites/Buttons/ui_buttons_set1.png";
            if (AssetImporter.GetAtPath(probeAsset) == null) return true; // PNG absent → nothing to do
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(probeAsset))
                if (obj is Sprite s && s.name == "btn_play") return true;
            return false;
        }

        // True when the shop tab sheet has been sliced into tab_selected/tab_unselected.
        static bool ShopTabsSliced()
        {
            const string probeAsset = "Assets/Resources/Sprites/Shop/Selected_buttons.png";
            if (AssetImporter.GetAtPath(probeAsset) == null) return true; // PNG absent → nothing to do
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(probeAsset))
                if (obj is Sprite s && s.name == "tab_selected") return true;
            return false;
        }

        // True when the confetti sheet has been sliced into its sub-sprites.
        static bool ConfettiSliced()
        {
            const string probeAsset = "Assets/Resources/Sprites/Victory/confetti_sheet.png";
            if (AssetImporter.GetAtPath(probeAsset) == null) return true; // PNG absent → nothing to do
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(probeAsset))
                if (obj is Sprite s && s.name == "confetti_0") return true;
            return false;
        }

        // True when the helper (plus) tube art is present, CROPPED to content, and bottom-pivoted,
        // i.e. our ImportHelperTubes has run. The crop check (rect shorter than the full texture)
        // also forces a re-import of any earlier full-rect version. Absent PNG → nothing to do.
        static bool HelperTubesConfigured()
        {
            const string probeAsset = "Assets/Resources/Sprites/Tubes/plus_one_tube.png";
            if (AssetImporter.GetAtPath(probeAsset) == null) return true; // PNG absent → nothing to do
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(probeAsset))
            {
                if (obj is Sprite s && s.rect.height > 0f && s.texture != null)
                    return (s.pivot.y / s.rect.height) < 0.10f
                        && s.rect.height < s.texture.height - 1; // cropped, not full-rect
            }
            return false; // no sprite sub-asset yet → needs import
        }

        static bool ParticlesImported()
        {
            const string probeAsset = "Assets/Resources/Sprites/Levels/particles.png";
            if (AssetImporter.GetAtPath(probeAsset) == null) return true; // PNG absent → nothing to do
            var imp = AssetImporter.GetAtPath(probeAsset) as TextureImporter;
            return imp != null && imp.textureType == TextureImporterType.Sprite;
        }

        // True once the redesigned shop art has been imported as sprites (probe = banner_shop).
        static bool ShopRedesignImported()
        {
            const string probeAsset = "Assets/Resources/Sprites/Shop/banner_shop.png";
            if (AssetImporter.GetAtPath(probeAsset) == null) return true; // absent → nothing to do
            var imp = AssetImporter.GetAtPath(probeAsset) as TextureImporter;
            return imp != null && imp.textureType == TextureImporterType.Sprite;
        }

        // True when the tube sprite is configured with a bottom (y≈0) pivot, i.e. our
        // ImportBallsAndTubes has run. A default-imported PNG has a centered pivot.
        static bool TubesBottomPivot()
        {
            const string probeAsset = "Assets/Resources/Sprites/Tubes/Tube_unselected.png";
            if (AssetImporter.GetAtPath(probeAsset) == null) return true; // PNG absent → nothing to do
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(probeAsset))
            {
                if (obj is Sprite s && s.rect.height > 0f)
                    return (s.pivot.y / s.rect.height) < 0.10f;
            }
            return false; // no sprite sub-asset yet → needs import
        }
    }
}
