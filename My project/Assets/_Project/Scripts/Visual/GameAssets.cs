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
        public static Sprite ShopTabTubesSelected        => LoadRes("Sprites/Shop/selected_tubes");
        public static Sprite ShopTabTubesUnselected      => LoadRes("Sprites/Shop/unselected_tubes");
        public static Sprite ShopTabWallpapersSelected   => LoadRes("Sprites/Shop/selected_wallpapers");
        public static Sprite ShopTabWallpapersUnselected => LoadRes("Sprites/Shop/unselected_wallpapers");
        public static Sprite ShopTabBallsSelected        => LoadRes("Sprites/Shop/selected_balls");
        public static Sprite ShopTabBallsUnselected      => LoadRes("Sprites/Shop/unselected_balls");
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
        public static Sprite MenuPopup      => LoadRes("Sprites/Menu/popup");

        // ── Settings popup art (assets_admin/Settings → Resources/Sprites/Settings/) ──
        public static Sprite SettingsBackground => LoadRes("Sprites/Settings/background_large");
        public static Sprite SettingsSoundOn    => LoadRes("Sprites/Settings/sound");
        public static Sprite SettingsSoundOff   => LoadRes("Sprites/Settings/no_sound");
        public static Sprite SettingsSfxOn      => LoadRes("Sprites/Settings/volume");
        public static Sprite SettingsSfxOff     => LoadRes("Sprites/Settings/no_volume");
        public static Sprite SettingsMinus      => LoadRes("Sprites/Settings/volume_minus");
        public static Sprite SettingsPlus       => LoadRes("Sprites/Settings/volume_plus");
        public static Sprite SettingsBarOn      => LoadRes("Sprites/Settings/sound_bar_on");
        public static Sprite SettingsBarOff     => LoadRes("Sprites/Settings/sound_bar_off");
        public static Sprite SettingsLanguageBtn=> LoadRes("Sprites/Settings/language_button");
        public static Sprite SettingsStar       => LoadRes("Sprites/Settings/star");
        public static Sprite SettingsShield     => LoadRes("Sprites/Settings/shield");
        public static Sprite SettingsWideButton => LoadRes("Sprites/Settings/rate_game_privacy_button");
        public static Sprite WhiteButton        => LoadRes("Sprites/Buttons/white_button");

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

        // Gameplay header replacements (assets_admin/Levels → Resources/Sprites/Buttons/)
        public static Sprite BtnMenuSettings  => LoadRes("Sprites/Buttons/menu_settings");
        public static Sprite BtnRestartLevel  => LoadRes("Sprites/Buttons/restart_level");
        public static Sprite BtnUndoNew       => LoadRes("Sprites/Buttons/undo_new");
        public static Sprite BtnExtraTube     => LoadRes("Sprites/Buttons/plus1tube");

        // Level select navigation (assets_admin/Levels → Resources/Sprites/Levels/)
        public static Sprite LevelArrowLeft   => LoadRes("Sprites/Levels/left_arrow");
        public static Sprite LevelArrowRight  => LoadRes("Sprites/Levels/right_arrow");
        public static Sprite LevelGoButton    => LoadRes("Sprites/Levels/go_button");

        // Shop coin UI (assets_admin/Shop/Shop_elements → Resources/Sprites/Shop/)
        public static Sprite ShopCoinIcon     => LoadRes("Sprites/Shop/coin");
        public static Sprite ShopMoreCoins    => LoadRes("Sprites/Shop/more_coins");

        // Level select tiles (assets_admin/Levels → Resources/Sprites/Levels/)
        public static Sprite LevelBackground => LoadRes("Sprites/Levels/level_background");
        public static Sprite LevelLock       => LoadRes("Sprites/Levels/lock");

        // ── Gameplay sprites — balls & tubes (assets_admin/Sprites_objets/New → Resources/Sprites/) ──

        /// <summary>1-based color_id → sprite name. Wraps if color_id exceeds 11 (not expected
        /// for the current 50-level catalogue, whose color_count never exceeds 6).</summary>
        private static readonly string[] BallSpriteNames =
        {
            "ball_red", "ball_green", "ball_blue", "ball_orange", "ball_purple", "ball_pink",
            "ball_yellow", "ball_light_blue", "ball_brown", "ball_grey", "ball_black",
        };

        /// <summary>Pre-coloured ball sprite for a 1-based color_id. Returns null for colorId &lt;= 0.</summary>
        public static Sprite BallSprite(int colorId)
        {
            if (colorId <= 0) return null;
            string name   = BallSpriteNames[(colorId - 1) % BallSpriteNames.Length];
            string folder = SkinManager.ActiveBallFolder;
            // Skinned art falls back to default if the skin file is missing.
            if (!string.IsNullOrEmpty(folder))
            {
                var skinned = LoadRes($"Sprites/Balls/{folder}{name}");
                if (skinned != null) return skinned;
            }
            return LoadRes($"Sprites/Balls/{name}");
        }

        /// <summary>Mystery-ball sprite (hidden color). Phase-2 mechanic (negative color id).</summary>
        public static Sprite BallMystery    => LoadRes("Sprites/Balls/ball_mystery");

        /// <summary>Multicolor wildcard ball sprite (matches any color). Phase-2 mechanic (color id 0).</summary>
        public static Sprite BallMulticolor
        {
            get
            {
                string folder = SkinManager.ActiveBallFolder;
                if (!string.IsNullOrEmpty(folder))
                {
                    var skinned = LoadRes($"Sprites/Balls/{folder}ball_multicolor");
                    if (skinned != null) return skinned;
                }
                return LoadRes("Sprites/Balls/ball_multicolor");
            }
        }

        private static Sprite[] _ballMulticolorFrames;

        /// <summary>Animation frames for the multicolor ball, loaded from ball_multicolor_sheet sprite sheet.
        /// Falls back to single-element array of BallMulticolor when the sheet is missing or unsliced.</summary>
        public static Sprite[] BallMulticolorFrames
        {
            get
            {
                string folder = SkinManager.ActiveBallFolder;
                if (!string.IsNullOrEmpty(folder))
                {
                    var skinned = LoadRes($"Sprites/Balls/{folder}ball_multicolor");
                    if (skinned != null) return new[] { skinned };
                }
                if (_ballMulticolorFrames != null) return _ballMulticolorFrames;
                var frames = Resources.LoadAll<Sprite>("Sprites/Balls/ball_multicolor_sheet");
                if (frames != null && frames.Length > 1)
                {
                    // Sort by sprite name so frames play in authoring order
                    System.Array.Sort(frames, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                    _ballMulticolorFrames = frames;
                }
                else
                {
                    var single = BallMulticolor;
                    _ballMulticolorFrames = single != null ? new[] { single } : System.Array.Empty<Sprite>();
                }
                return _ballMulticolorFrames;
            }
        }

        /// <summary>
        /// Board ball sprite for a raw token: 0 → multicolor wildcard, &lt;0 → mystery (hidden),
        /// &gt;0 → the pre-coloured ball for that color id. Central mapping used by BoardView.
        /// </summary>
        public static Sprite BallSpriteForToken(int token)
        {
            if (token == 0) return BallMulticolor;
            if (token < 0)  return BallMystery;
            return BallSprite(token);
        }

        /// <summary>
        /// Tube body sprite for the given slot capacity (3 → short, 4 → normal, 5 → large,
        /// 6 → extra large, 7+ → XXL), in its selected or unselected state.
        /// </summary>
        public static Sprite TubeSprite(int capacity, bool selected)
        {
            string size = capacity <= 3 ? "_short"
                         : capacity == 4 ? ""
                         : capacity == 5 ? "_large"
                         : capacity == 6 ? "_extra_large"
                         : "_XXL";
            string state  = selected ? "Tube_selected" : "Tube_unselected";
            string folder = SkinManager.ActiveTubeFolder;
            if (!string.IsNullOrEmpty(folder))
            {
                var skinned = LoadRes($"Sprites/Tubes/{folder}{state}{size}");
                if (skinned != null) return skinned;
            }
            return LoadRes($"Sprites/Tubes/{state}{size}");
        }

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

        // ── Confetti — sliced sub-sprites of confetti_sheet in Resources/Sprites/Victory/ ──
        private const string SheetConfetti = "Sprites/Victory/confetti_sheet";
        private static Sprite[] _confettiPieces;

        /// <summary>All confetti piece sprites sliced from confetti_sheet.png, loaded once
        /// via Resources.LoadAll (works in Editor and device builds). Empty array if the
        /// sheet is missing or not yet sliced.</summary>
        public static Sprite[] ConfettiPieces
        {
            get
            {
                if (_confettiPieces != null) return _confettiPieces;
                _confettiPieces = Resources.LoadAll<Sprite>(SheetConfetti) ?? System.Array.Empty<Sprite>();
#if UNITY_EDITOR
                if (_confettiPieces.Length == 0)
                    Debug.LogWarning($"[GameAssets] Resources.LoadAll<Sprite>(\"{SheetConfetti}\") returned none. " +
                                     "Run BoltSort > Import Game Assets to slice the confetti sheet.");
#endif
                return _confettiPieces;
            }
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
