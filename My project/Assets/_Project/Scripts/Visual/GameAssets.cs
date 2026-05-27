using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoltSort.Visual
{
    /// <summary>
    /// Editor-play-mode sprite accessor. Uses AssetDatabase (Editor only) so every
    /// property works in Play mode without moving PNGs into a Resources folder.
    /// In runtime builds all properties return null — handle gracefully with fallback colors.
    /// </summary>
    public static class GameAssets
    {
        // ── Asset paths ────────────────────────────────────────────────────────────
        private const string P_Btns1   = "Assets/game_assets/buttons/ui_buttons_set1.png";
        private const string P_Btns2   = "Assets/game_assets/buttons/ui_buttons_set2.png";
        private const string P_Stars   = "Assets/game_assets/decorations/stars_progress_coins.png";
        private const string P_ScrewsC = "Assets/game_assets/game_elements/screws_individual_colorful.png";
        private const string P_ScrewsH = "Assets/game_assets/game_elements/screws_row1.png";
        private const string P_NutsM   = "Assets/game_assets/game_elements/nuts_metallic.png";
        private const string P_NutsB   = "Assets/game_assets/game_elements/nuts_basic.png";
        private const string P_Tubes   = "Assets/game_assets/game_elements/tube_containers.png";
        private const string P_Grid    = "Assets/game_assets/level_selector/level_buttons_grid.png";
        private const string P_Unlocked = "Assets/game_assets/level_selector/level_unlocked_1.png";
        private const string P_Locked   = "Assets/game_assets/level_selector/level_locked.png";
        private const string P_BgScreen = "Assets/game_assets/screens/game_background.png";
        private const string P_Victory  = "Assets/game_assets/screens/victory_screen.png";
        private const string P_AdFrame  = "Assets/game_assets/screens/ad_screen.png";
        private const string P_Shop     = "Assets/game_assets/shop/shop_frame.png";

        // ── Buttons set 1 ──────────────────────────────────────────────────────────
        public static Sprite BtnPlay          => Load(P_Btns1, "btn_play");
        public static Sprite BtnSettings      => Load(P_Btns1, "btn_settings");
        public static Sprite BtnPause         => Load(P_Btns1, "btn_pause");
        public static Sprite BtnRetry         => Load(P_Btns1, "btn_retry");
        public static Sprite BtnSettingsPause => Load(P_Btns1, "btn_settings_pause");
        public static Sprite BtnSound         => Load(P_Btns1, "btn_sound");

        // ── Buttons set 2 ──────────────────────────────────────────────────────────
        public static Sprite BtnHome         => Load(P_Btns2, "btn_home");
        public static Sprite BtnBack         => Load(P_Btns2, "btn_back");
        public static Sprite BtnContinue     => Load(P_Btns2, "btn_continue");
        public static Sprite BtnClose        => Load(P_Btns2, "btn_close");
        public static Sprite BtnCloseAlt     => Load(P_Btns2, "btn_close_alt");
        public static Sprite BtnSettingsSq   => Load(P_Btns2, "btn_settings_square");
        public static Sprite BtnSoundOn      => Load(P_Btns2, "btn_sound_on");
        public static Sprite BtnSoundOff     => Load(P_Btns2, "btn_sound_off");

        // ── Decorations ────────────────────────────────────────────────────────────
        public static Sprite StarLarge      => Load(P_Stars, "star_large");
        public static Sprite StarMedium     => Load(P_Stars, "star_medium");
        public static Sprite StarSmall      => Load(P_Stars, "star_small");
        public static Sprite CoinIcon       => Load(P_Stars, "coin_icon");
        public static Sprite ProgressBar    => Load(P_Stars, "progress_bar");
        public static Sprite ProgressCircle => Load(P_Stars, "progress_circle");

        // ── Level selector ─────────────────────────────────────────────────────────
        public static Sprite TileLevelUnlocked => Load(P_Unlocked, "tile_level_unlocked");
        public static Sprite TileLevelLocked   => Load(P_Locked,   "tile_level_locked");

        // ── Screens ────────────────────────────────────────────────────────────────
        public static Sprite GameBackground => Load(P_BgScreen, "game_background");
        public static Sprite VictoryScreen  => Load(P_Victory,  "victory_screen");
        public static Sprite AdFrame        => Load(P_AdFrame,  "ad_frame");
        public static Sprite ShopPanel      => Load(P_Shop,     "shop_panel");

        // ── Game elements ──────────────────────────────────────────────────────────
        public static Sprite TubeEmpty => Load(P_Tubes, "tube_empty");

        public static Sprite ScrewColorful(int colorId)
        {
            string n = colorId switch
            {
                0 => "screw_red",    1 => "screw_blue",   2 => "screw_green",
                3 => "screw_yellow", 4 => "screw_orange", 5 => "screw_purple",
                6 => "screw_pink",   7 => "screw_cyan",
                _ => null
            };
            return n != null ? Load(P_ScrewsC, n) : null;
        }

        public static Sprite ScrewHex(int colorId)
        {
            string n = colorId switch
            {
                0 => "screw_hex_red",    1 => "screw_hex_blue",   2 => "screw_hex_green",
                3 => "screw_hex_yellow", 4 => "screw_hex_orange", 5 => "screw_hex_purple",
                6 => "screw_hex_pink",   7 => "screw_hex_cyan",   8 => "screw_hex_gold",
                9 => "screw_hex_silver",
                _ => null
            };
            return n != null ? Load(P_ScrewsH, n) : null;
        }

        // ── Core loader ────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a sprite sub-asset by name. If spriteName is null, loads the first sprite.
        /// Only functional in the Unity Editor (Play mode). Returns null in builds.
        /// </summary>
        public static Sprite Load(string assetPath, string spriteName = null)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(spriteName))
                return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var obj in all)
                if (obj is Sprite s && s.name == spriteName)
                    return s;

            Debug.LogWarning($"[GameAssets] Sprite '{spriteName}' not found in {assetPath}. " +
                             "Run BoltSort ▶ Import Game Assets first.");
#endif
            return null;
        }

        /// <summary>
        /// Applies sprite + white color to an existing Image. No-ops if sprite is null
        /// (Image keeps its fallback solid color).
        /// </summary>
        public static void Apply(UnityEngine.UI.Image img, Sprite sprite,
                                 bool preserveAspect = false)
        {
            if (img == null || sprite == null) return;
            img.sprite          = sprite;
            img.color           = Color.white;
            img.preserveAspect  = preserveAspect;
        }
    }
}
