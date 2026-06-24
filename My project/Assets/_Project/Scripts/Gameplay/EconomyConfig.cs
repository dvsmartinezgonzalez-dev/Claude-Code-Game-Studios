using System.Collections.Generic;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Single source of truth for every tunable currency value, reward table, drop rate, cap and
    /// store price across the dual-currency economy (System 3), the reward system (System 2) and
    /// power-up purchases. Pure static data — no logic — so balance can be retuned without touching
    /// <see cref="EconomyManager"/> / <see cref="EconomyData"/>. All amounts are per the task spec.
    /// </summary>
    public static class EconomyConfig
    {
        // ── PlayerPrefs key ─────────────────────────────────────────────────────────
        public const string SaveKey = "bs.economy";

        // ── Coins: level completion ─────────────────────────────────────────────────
        /// <summary>First-time clear coin reward by star count (index 1..3).</summary>
        public static int FirstTimeCoins(int stars) => stars switch { >= 3 => 55, 2 => 35, _ => 20 };
        /// <summary>Replay clear coin reward by star count (index 1..3).</summary>
        public static int ReplayCoins(int stars)    => stars switch { >= 3 => 12, 2 => 8,  _ => 5  };

        // ── Coins: win streak (consecutive first-time clears, no replays) ────────────
        public const int Streak5Bonus  = 100;
        public const int Streak10Bonus = 250;

        // ── Daily login (7-day cycle; coins + gems awarded together) ─────────────────
        public static readonly int[] DailyCoins = { 50, 75, 100, 150, 200, 300, 500 };
        public static readonly int[] DailyGems  = {  1,  2,   3,   5,   5,  10,  15 };

        // ── Gems: milestones (level-id multiples of 25, first time only, up to 200) ──
        public const int MilestoneGems     = 10;
        public const int MilestoneInterval = 25;
        public const int MilestoneMaxLevel = 200;

        // ── Gems: achievements by total distinct levels completed (first time only) ──
        public static readonly IReadOnlyDictionary<int, int> AchievementGems = new Dictionary<int, int>
        {
            { 50, 20 }, { 100, 30 }, { 500, 50 }, { 1000, 100 },
        };

        // ── Gems: weekly chest (random in range, once per 7 days) ────────────────────
        public const int WeeklyChestMin = 25;
        public const int WeeklyChestMax = 50;

        // ── Rewarded ad (max 3/day; player chooses coins OR gems) ────────────────────
        public const int RewardedAdMaxPerDay = 3;
        public const int RewardedAdCoins     = 30;
        public const int RewardedAdGems      = 2;

        // ── Power-up drops (System 2) ────────────────────────────────────────────────
        /// <summary>Independent per-type drop chance after every level completion.</summary>
        public const float PowerUpDropChance = 0.08f;
        /// <summary>Maximum held at any time per power-up type. Drop chance is 0 when at cap.</summary>
        public const int PowerUpMaxHeld = 3;

        // ── Store prices ─────────────────────────────────────────────────────────────
        // Power-ups (hard currency only — never purchasable with coins).
        public const int ExtraTubeGemPrice     = 150;
        public const int LightningBoltGemPrice = 80;

        // Skins — coins (soft currency).
        public static readonly int[] CommonSkinCoinPrices = { 200, 500, 800, 1000 };
        public static readonly int[] RareSkinCoinPrices   = { 2000, 3500, 5000 };

        // Skins — gems (hard currency).
        public static readonly int[] EpicSkinGemPrices      = { 50, 75, 100, 150 };
        public static readonly int[] LegendarySkinGemPrices = { 250, 350, 500 };
        public const int MythicSkinGemPrice = 1000; // max 2 mythic skins in the game
    }
}
