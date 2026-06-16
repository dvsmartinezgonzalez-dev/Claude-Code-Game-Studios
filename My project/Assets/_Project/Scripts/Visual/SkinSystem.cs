using System;
using System.Collections.Generic;
using UnityEngine;
using SS = BoltSort.SaveSystem.SaveSystem;

namespace BoltSort.Visual
{
    /// <summary>Skin category a <see cref="SkinItem"/> belongs to.</summary>
    public enum SkinCategory { Tubes, Backgrounds, Balls }

    /// <summary>Result of a <see cref="SkinManager.TryPurchase"/> call.</summary>
    public enum PurchaseResult { Success, AlreadyUnlocked, NotEnoughCoins, Invalid }

    /// <summary>
    /// A single cosmetic skin. Code-defined (no ScriptableObject) to match the project's
    /// static <see cref="GameAssets"/> registry pattern. Sprites resolve lazily via
    /// Resources.Load so behavior is identical in Editor and device builds.
    /// </summary>
    public sealed class SkinItem
    {
        public string SkinId;
        public string DisplayName;
        public SkinCategory Category;

        /// <summary>Resources path for the grid thumbnail (null → uses first asset / swatch).</summary>
        public string ThumbnailPath;

        /// <summary>
        /// Gameplay sprite resource paths. Backgrounds: one full-screen wallpaper.
        /// Tubes/Balls: optional explicit overrides (usually empty — see <see cref="AssetFolder"/>).
        /// </summary>
        public string[] AssetPaths = Array.Empty<string>();

        /// <summary>
        /// Sub-folder under Sprites/Tubes or Sprites/Balls (e.g. "neon/"). Empty = default art.
        /// Lets future tube/ball skins drop in without code changes.
        /// </summary>
        public string AssetFolder = "";

        public int UnlockCost;
        public int MinLevelRequired;   // TODO: enforce when level-gating is activated
        public bool IsDefault;

        /// <summary>Thumbnail sprite, or first asset, or null (caller draws a swatch).</summary>
        public Sprite Thumbnail
        {
            get
            {
                if (!string.IsNullOrEmpty(ThumbnailPath)) return GameAssets.LoadRes(ThumbnailPath);
                if (AssetPaths != null && AssetPaths.Length > 0) return GameAssets.LoadRes(AssetPaths[0]);
                return null;
            }
        }

        /// <summary>Primary gameplay sprite (backgrounds), or null.</summary>
        public Sprite PrimarySprite =>
            (AssetPaths != null && AssetPaths.Length > 0) ? GameAssets.LoadRes(AssetPaths[0]) : null;
    }

    /// <summary>
    /// Single source of truth for all skins, organised by category. Default item is always
    /// first in each list and free. Empty Tubes/Balls art folders are handled gracefully —
    /// only the Default item appears until skins are added here.
    /// </summary>
    public static class SkinCatalogue
    {
        private const string WallDir = "Sprites/Shop/Wallpapers/";

        private static readonly List<SkinItem> _tubes = new()
        {
            new SkinItem { SkinId = "tube_default", DisplayName = "Default",
                           Category = SkinCategory.Tubes, IsDefault = true, AssetFolder = "" },
            // Future: new SkinItem { SkinId = "tube_neon", AssetFolder = "neon/", UnlockCost = 1000 }
        };

        private static readonly List<SkinItem> _balls = new()
        {
            new SkinItem { SkinId = "ball_default", DisplayName = "Default",
                           Category = SkinCategory.Balls, IsDefault = true, AssetFolder = "" },
        };

        private static readonly List<SkinItem> _backgrounds = new()
        {
            new SkinItem { SkinId = "bg_default", DisplayName = "Default",
                           Category = SkinCategory.Backgrounds, IsDefault = true },
            Wall("bg_boreal",    "Boreal",    "boreal"),
            Wall("bg_planets",   "Planets",   "planets"),
            Wall("bg_nature",    "Nature",    "nature"),
            Wall("bg_lab",       "Lab",       "lab"),
            Wall("bg_viking",    "Viking",    "viking"),
            Wall("bg_icecream",  "Ice Cream", "icecream"),
            Wall("bg_halloween", "Halloween", "halloween"),
            Wall("bg_christmas", "Christmas", "christmas"),
            Wall("bg_easter",    "Easter",    "easter"),
        };

        private static SkinItem Wall(string id, string name, string file) => new SkinItem
        {
            SkinId = id, DisplayName = name, Category = SkinCategory.Backgrounds,
            ThumbnailPath = WallDir + file, AssetPaths = new[] { WallDir + file },
            UnlockCost = 1000, IsDefault = false,
        };

        public static IReadOnlyList<SkinItem> For(SkinCategory c) => c switch
        {
            SkinCategory.Tubes       => _tubes,
            SkinCategory.Balls       => _balls,
            _                        => _backgrounds,
        };

        public static SkinItem Find(string skinId)
        {
            foreach (var list in new[] { _tubes, _balls, _backgrounds })
                foreach (var s in list)
                    if (s.SkinId == skinId) return s;
            return null;
        }
    }

    /// <summary>
    /// Owns equipped + unlocked skin state, persistence, purchase and equip logic.
    /// Static (matches <see cref="GameAssets"/>); persists via PlayerPrefs and spends coins
    /// through the existing SaveSystem when ready. <see cref="OnSkinChanged"/> lets gameplay
    /// visuals refresh live when a skin is equipped.
    /// </summary>
    public static class SkinManager
    {
        private const string KeyTube   = "bs.equipped_tube";
        private const string KeyBg      = "bs.equipped_bg";
        private const string KeyBall    = "bs.equipped_ball";
        private const string KeyUnlocked = "bs.unlocked_skins";
        private const string CoinPrefKey = "bs.coin_balance";

        /// <summary>Fired after any equip/purchase changes equipped state.</summary>
        public static event Action OnSkinChanged;

        private static HashSet<string> _unlocked;

        private static void EnsureLoaded()
        {
            if (_unlocked != null) return;
            _unlocked = new HashSet<string>();
            string raw = PlayerPrefs.GetString(KeyUnlocked, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var id in raw.Split('|'))
                    if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);

            // Defaults are always unlocked.
            foreach (SkinCategory c in Enum.GetValues(typeof(SkinCategory)))
                foreach (var s in SkinCatalogue.For(c))
                    if (s.IsDefault) _unlocked.Add(s.SkinId);
        }

        private static void SaveUnlocked()
        {
            PlayerPrefs.SetString(KeyUnlocked, string.Join("|", _unlocked));
            PlayerPrefs.Save();
        }

        private static string KeyFor(SkinCategory c) =>
            c == SkinCategory.Tubes ? KeyTube : c == SkinCategory.Balls ? KeyBall : KeyBg;

        public static bool IsUnlocked(SkinItem item)
        {
            if (item == null) return false;
            if (item.IsDefault) return true;
            EnsureLoaded();
            return _unlocked.Contains(item.SkinId);
        }

        public static SkinItem GetEquippedSkin(SkinCategory c)
        {
            var list = SkinCatalogue.For(c);
            string id = PlayerPrefs.GetString(KeyFor(c), "");
            if (!string.IsNullOrEmpty(id))
            {
                var found = SkinCatalogue.Find(id);
                // Guard: equipped skin must still exist, be the right category, and be unlocked.
                if (found != null && found.Category == c && IsUnlocked(found)) return found;
            }
            return list.Count > 0 ? list[0] : null; // default
        }

        public static bool IsEquipped(SkinItem item) =>
            item != null && GetEquippedSkin(item.Category)?.SkinId == item.SkinId;

        public static void EquipSkin(SkinItem item)
        {
            if (item == null || !IsUnlocked(item)) return;
            PlayerPrefs.SetString(KeyFor(item.Category), item.SkinId);
            PlayerPrefs.Save();
            OnSkinChanged?.Invoke();
        }

        public static PurchaseResult TryPurchase(SkinItem item)
        {
            if (item == null) return PurchaseResult.Invalid;
            if (IsUnlocked(item)) return PurchaseResult.AlreadyUnlocked;
            // TODO: enforce minLevelRequired when level-gating is activated.

            int coins = GetCoins();
            if (coins < item.UnlockCost) return PurchaseResult.NotEnoughCoins;

            SetCoins(coins - item.UnlockCost);
            EnsureLoaded();
            _unlocked.Add(item.SkinId);
            SaveUnlocked();
            return PurchaseResult.Success;
        }

        // ── Coins (delegates to SaveSystem when ready, else PlayerPrefs) ────────────
        public static int GetCoins()
        {
            var ss = SS.Instance;
            if (ss != null && ss.IsReady) return ss.GetCoinBalance();
            return PlayerPrefs.GetInt(CoinPrefKey, 0);
        }

        private static void SetCoins(int value)
        {
            value = Mathf.Max(0, value);
            var ss = SS.Instance;
            if (ss != null && ss.IsReady) ss.SetCoinBalance(value);
            PlayerPrefs.SetInt(CoinPrefKey, value);
            PlayerPrefs.Save();
        }

        // ── Gameplay accessors ──────────────────────────────────────────────────────
        /// <summary>Equipped background wallpaper, or null when Default (use existing gradient).</summary>
        public static Sprite EquippedBackgroundSprite => GetEquippedSkin(SkinCategory.Backgrounds)?.PrimarySprite;

        /// <summary>Folder prefix for tube art under Sprites/Tubes/ ("" = default).</summary>
        public static string ActiveTubeFolder => GetEquippedSkin(SkinCategory.Tubes)?.AssetFolder ?? "";

        /// <summary>Folder prefix for ball art under Sprites/Balls/ ("" = default).</summary>
        public static string ActiveBallFolder => GetEquippedSkin(SkinCategory.Balls)?.AssetFolder ?? "";
    }
}
