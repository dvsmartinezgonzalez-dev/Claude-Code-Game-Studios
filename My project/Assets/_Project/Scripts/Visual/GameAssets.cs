using System.Collections.Generic;
using UnityEngine;

namespace BoltSort.Visual
{
    /// <summary>
    /// Runtime sprite registry. Every sprite loads via Resources.Load from
    /// Resources/Sprites/, so behavior is identical in the Editor and on device.
    /// Drop a PNG matching the resource name into Resources/Sprites/ and run
    /// BoltSort &gt; Import Sprite Settings to wire up new art.
    /// </summary>
    public static class GameAssets
    {
        private static readonly Dictionary<string, Sprite> _cache = new();

        // ── Individual sprites — load from Resources/Sprites/ (works in builds) ──

        public static Sprite Trophy          => LoadRes("Sprites/Trofeo");
        public static Sprite Confetti        => LoadRes("Sprites/Confetti");
        public static Sprite LockIcon        => LoadRes("Sprites/Candado");
        public static Sprite BtnLevels       => LoadRes("Sprites/Levels");
        public static Sprite Superestrella   => LoadRes("Sprites/Superestrella");
        public static Sprite DiamondIcon     => LoadRes("Sprites/Diamante");
        public static Sprite GameBackground  => LoadRes("Sprites/game_background");
        public static Sprite VictoryBg       => LoadRes("Sprites/victory_screen");
        public static Sprite ShopPanel       => LoadRes("Sprites/shop_frame");
        public static Sprite TileLevelLocked   => LoadRes("Sprites/level_locked");
        public static Sprite TileLevelUnlocked => LoadRes("Sprites/level_unlocked");

        // Victory screen action buttons (assets_admin/Victory → Resources/Sprites/)
        public static Sprite VictoryRetry => LoadRes("Sprites/retry");
        public static Sprite VictoryNext  => LoadRes("Sprites/next_button");

        // ── Main menu art (assets_admin/Main_menu → Resources/Sprites/Menu/) ──
        public static Sprite MenuSettings   => LoadRes("Sprites/Menu/settings");
        public static Sprite MenuNoAds      => LoadRes("Sprites/Menu/no_ads");
        public static Sprite MenuButton     => LoadRes("Sprites/Menu/general_button");
        public static Sprite MenuVolume     => LoadRes("Sprites/Menu/volume");
        public static Sprite TitleBolt      => LoadRes("Sprites/Menu/title_bolt");
        public static Sprite TitleSort      => LoadRes("Sprites/Menu/title_sort");
        public static Sprite TitleSpark     => LoadRes("Sprites/Menu/spark");

        /// <summary>Decorative shop diamonds (1-5). Returns null if missing.</summary>
        public static Sprite MenuDiamond(int index) => LoadRes($"Sprites/Menu/diamond_{index}");

        // Navigation buttons (assets_admin/Buttons → Resources/Sprites/Menu/)
        public static Sprite NavBack        => LoadRes("Sprites/Menu/back_button");
        public static Sprite NavSettings    => LoadRes("Sprites/Menu/settings_button");

        // Action buttons (assets_admin/Buttons → Resources/Sprites/Buttons/, single sprites)
        public static Sprite BtnRetryAction => LoadRes("Sprites/Buttons/retry_button");
        public static Sprite BtnUndoAction  => LoadRes("Sprites/Buttons/undo_button");
        public static Sprite BtnHomeAction  => LoadRes("Sprites/Buttons/home_button");
        public static Sprite BtnExit        => LoadRes("Sprites/Buttons/exit_button");

        // Level select tiles (assets_admin/Levels → Resources/Sprites/Levels/)
        public static Sprite LevelBackground => LoadRes("Sprites/Levels/level_background");
        public static Sprite LevelLock       => LoadRes("Sprites/Levels/lock");

        // ── Fonts ───────────────────────────────────────────────────────────────────
        private static Font _menuFont;

        /// <summary>
        /// The Gummy display font (Resources/Fonts/GummyPop). Falls back to the
        /// built-in legacy runtime font when the asset is missing.
        /// </summary>
        public static Font MenuFont
        {
            get
            {
                if (_menuFont != null) return _menuFont;
                _menuFont = Resources.Load<Font>("Fonts/GummyPop");
                if (_menuFont == null)
                    _menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                             ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _menuFont;
            }
        }

        // Legacy aliases kept for call-site compatibility
        public static Sprite StarLarge       => Superestrella;
        public static Sprite VictoryScreen   => VictoryBg;

        // ── Buttons — sliced sub-sprites of sheets in Resources/Sprites/Buttons/ ──
        // Sheets are imported in Multiple mode (see GameAssetImporter) so each
        // named sub-sprite ships as part of the texture asset and loads via
        // Resources.LoadAll on both Editor and device builds.

        private const string SheetBtns1 = "Sprites/Buttons/ui_buttons_set1";
        private const string SheetBtns2 = "Sprites/Buttons/ui_buttons_set2";

        public static Sprite BtnPlay          => LoadFromSheet(SheetBtns1, "btn_play");
        public static Sprite BtnSettings      => LoadFromSheet(SheetBtns1, "btn_settings");
        public static Sprite BtnPause         => LoadFromSheet(SheetBtns1, "btn_pause");
        public static Sprite BtnRetry         => LoadFromSheet(SheetBtns1, "btn_retry");
        public static Sprite BtnSound         => LoadFromSheet(SheetBtns1, "btn_sound");
        public static Sprite BtnSettingsPause => LoadFromSheet(SheetBtns1, "btn_settings_pause");

        public static Sprite BtnHome          => LoadFromSheet(SheetBtns2, "btn_home");
        public static Sprite BtnBack          => LoadFromSheet(SheetBtns2, "btn_back");
        public static Sprite BtnContinue      => LoadFromSheet(SheetBtns2, "btn_continue");
        public static Sprite BtnClose         => LoadFromSheet(SheetBtns2, "btn_close");
        public static Sprite BtnSoundOff      => LoadFromSheet(SheetBtns2, "btn_sound_off");

        // ── Loaders ───────────────────────────────────────────────────────────────

        /// <summary>Resources.Load path — works in Editor AND device builds.</summary>
        public static Sprite LoadRes(string resourcePath)
        {
            if (_cache.TryGetValue(resourcePath, out var cached)) return cached;
            var spr = Resources.Load<Sprite>(resourcePath);
            if (spr != null) _cache[resourcePath] = spr;
#if UNITY_EDITOR
            else Debug.LogWarning($"[GameAssets] Resources.Load<Sprite>(\"{resourcePath}\") returned null. " +
                                  "Run BoltSort > Import Sprite Settings to configure import.");
#endif
            return spr;
        }

        /// <summary>
        /// Loads a named sub-sprite from a Multiple-mode sliced sheet via Resources.LoadAll —
        /// works in Editor AND device builds (unlike AssetDatabase sub-asset lookups).
        /// </summary>
        public static Sprite LoadFromSheet(string sheetResourcePath, string spriteName)
        {
            string key = sheetResourcePath + "/" + spriteName;
            if (_cache.TryGetValue(key, out var cached)) return cached;

            foreach (var sub in Resources.LoadAll<Sprite>(sheetResourcePath))
            {
                _cache[sheetResourcePath + "/" + sub.name] = sub;
                if (sub.name == spriteName) return sub;
            }

#if UNITY_EDITOR
            Debug.LogWarning($"[GameAssets] LoadFromSheet(\"{sheetResourcePath}\", \"{spriteName}\") found no match. " +
                             "Place the sliced sheet under Resources/ and run BoltSort > Import Game Assets.");
#endif
            return null;
        }

        /// <summary>
        /// Applies sprite + white color to an Image. No-ops gracefully when sprite is null.
        /// </summary>
        public static void Apply(UnityEngine.UI.Image img, Sprite sprite,
                                 bool preserveAspect = false)
        {
            if (img == null || sprite == null) return;
            img.sprite         = sprite;
            img.color          = Color.white;
            img.preserveAspect = preserveAspect;
        }

        /// <summary>Clears the runtime sprite cache (call when switching scenes if needed).</summary>
        public static void ClearCache() => _cache.Clear();
    }
}
