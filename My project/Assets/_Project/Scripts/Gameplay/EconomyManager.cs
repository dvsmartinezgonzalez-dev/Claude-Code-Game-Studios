using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>The two power-up types tracked as owned inventory (System 2).</summary>
    public enum PowerUpType { ExtraTube, LightningBolt }

    /// <summary>Outcome of a single level completion, used to drive win-screen + popups.</summary>
    public struct LevelRewardResult
    {
        public bool FirstTime;
        public int  LevelCoins;     // base coins (first-time or replay) — shown on the win card
        public int  StreakBonus;    // extra coins from a 5/10 streak milestone (0 if none)
        public int  MilestoneGems;  // gems from a 25-level milestone (0 if none)
        public int  AchievementGems;// gems from a completion-count achievement (0 if none)
    }

    /// <summary>Outcome of a daily-login claim.</summary>
    public struct DailyRewardResult
    {
        public bool Claimed;        // false when already claimed today
        public int  Day;            // 1..7
        public int  Coins;
        public int  Gems;
    }

    /// <summary>
    /// Pure, engine-independent economy state + rules (System 3). Holds coins, gems, owned power-ups,
    /// completion history, streak, daily/weekly/ad timers, and the earn/spend logic. Kept free of
    /// UnityEngine and of clocks/RNG (callers pass <c>now</c> / random rolls) so it is fully unit
    /// testable. <see cref="EconomyManager"/> wraps it with persistence, events and Unity glue.
    /// [Serializable] with public fields so Unity's JsonUtility can round-trip it to PlayerPrefs.
    /// </summary>
    [Serializable]
    public class EconomyData
    {
        public int coins;
        public int gems;
        public int extraTubes;
        public int lightningBolts;

        public List<int> completedLevels    = new List<int>(); // distinct level ids ever completed
        public List<int> milestonesClaimed  = new List<int>(); // level ids whose milestone gems were paid
        public List<int> achievementsClaimed = new List<int>();// achievement thresholds already paid

        public int  currentStreak;
        public int  lastStreakLevel;

        public int  dailyLoginDay;   // 0 = never claimed; otherwise 1..7
        public long lastLoginTicks;  // DateTime.Ticks (date) of the last daily claim
        public long lastWeeklyTicks; // DateTime.Ticks of the last weekly chest
        public int  rewardedAdsToday;
        public long lastAdTicks;

        public bool HasCompleted(int levelId) => completedLevels.Contains(levelId);

        public int Held(PowerUpType type) => type == PowerUpType.ExtraTube ? extraTubes : lightningBolts;

        // ── Level completion: coins, streak, milestone + achievement gems ────────────
        public LevelRewardResult RegisterLevelComplete(int levelId, int stars)
        {
            var r = new LevelRewardResult();
            bool first = !completedLevels.Contains(levelId);
            r.FirstTime = first;

            if (first)
            {
                r.LevelCoins = EconomyConfig.FirstTimeCoins(stars);
                completedLevels.Add(levelId);

                currentStreak++;
                lastStreakLevel = levelId;
                if (currentStreak == 5)  r.StreakBonus = EconomyConfig.Streak5Bonus;
                else if (currentStreak == 10) r.StreakBonus = EconomyConfig.Streak10Bonus;

                if (levelId % EconomyConfig.MilestoneInterval == 0 &&
                    levelId <= EconomyConfig.MilestoneMaxLevel &&
                    !milestonesClaimed.Contains(levelId))
                {
                    milestonesClaimed.Add(levelId);
                    r.MilestoneGems = EconomyConfig.MilestoneGems;
                }

                int count = completedLevels.Count;
                if (EconomyConfig.AchievementGems.TryGetValue(count, out int ag) &&
                    !achievementsClaimed.Contains(count))
                {
                    achievementsClaimed.Add(count);
                    r.AchievementGems = ag;
                }
            }
            else
            {
                r.LevelCoins  = EconomyConfig.ReplayCoins(stars);
                currentStreak = 0; // replaying an old level breaks the streak
            }

            coins += r.LevelCoins + r.StreakBonus;
            gems  += r.MilestoneGems + r.AchievementGems;
            return r;
        }

        // ── Daily login (7-day cycle, wraps 7→1) ─────────────────────────────────────
        public DailyRewardResult ClaimDaily(DateTime now)
        {
            var r = new DailyRewardResult();
            DateTime today = now.Date;
            if (lastLoginTicks != 0 && new DateTime(lastLoginTicks).Date == today)
                return r; // already claimed today

            dailyLoginDay = (dailyLoginDay >= 7 || dailyLoginDay < 1) ? 1 : dailyLoginDay + 1;
            lastLoginTicks = today.Ticks;

            r.Claimed = true;
            r.Day     = dailyLoginDay;
            r.Coins   = EconomyConfig.DailyCoins[dailyLoginDay - 1];
            r.Gems    = EconomyConfig.DailyGems[dailyLoginDay - 1];
            coins += r.Coins;
            gems  += r.Gems;
            return r;
        }

        // ── Weekly chest (once per 7 days; caller supplies the random amount) ─────────
        public int ClaimWeekly(DateTime now, int randomGems)
        {
            if (lastWeeklyTicks != 0 && (now - new DateTime(lastWeeklyTicks)).TotalDays < 7)
                return 0; // still on cooldown
            lastWeeklyTicks = now.Ticks;
            gems += randomGems;
            return randomGems;
        }

        // ── Rewarded ad (max 3/day; resets on date change) ───────────────────────────
        public bool TryRewardedAd(DateTime now, bool chooseGems)
        {
            DateTime today = now.Date;
            if (lastAdTicks != 0 && new DateTime(lastAdTicks).Date != today) rewardedAdsToday = 0;
            if (rewardedAdsToday >= EconomyConfig.RewardedAdMaxPerDay) return false;
            rewardedAdsToday++;
            lastAdTicks = today.Ticks;
            if (chooseGems) gems += EconomyConfig.RewardedAdGems;
            else            coins += EconomyConfig.RewardedAdCoins;
            return true;
        }

        // ── Currency primitives ──────────────────────────────────────────────────────
        public bool SpendCoins(int amount) { if (amount < 0 || coins < amount) return false; coins -= amount; return true; }
        public bool SpendGems(int amount)  { if (amount < 0 || gems  < amount) return false; gems  -= amount; return true; }
        public void AddCoins(int amount)   { if (amount > 0) coins += amount; }
        public void AddGems(int amount)    { if (amount > 0) gems  += amount; }

        /// <summary>Grants a power-up if below the cap. Returns false (no change) when already at cap.</summary>
        public bool GrantPowerUp(PowerUpType type)
        {
            if (Held(type) >= EconomyConfig.PowerUpMaxHeld) return false;
            if (type == PowerUpType.ExtraTube) extraTubes++; else lightningBolts++;
            return true;
        }

        /// <summary>Buys a power-up with gems, respecting the held cap. Cap is checked before spending.</summary>
        public bool BuyPowerUp(PowerUpType type)
        {
            if (Held(type) >= EconomyConfig.PowerUpMaxHeld) return false;
            int price = type == PowerUpType.ExtraTube
                ? EconomyConfig.ExtraTubeGemPrice : EconomyConfig.LightningBoltGemPrice;
            if (!SpendGems(price)) return false;
            if (type == PowerUpType.ExtraTube) extraTubes++; else lightningBolts++;
            return true;
        }
    }

    /// <summary>
    /// Singleton facade over <see cref="EconomyData"/>: owns persistence (PlayerPrefs JSON blob),
    /// mirrors the coin balance into the existing <c>SaveSystem</c> (so the Shop and win screen stay
    /// correct), raises change/drop/reward events for the HUD and popups, and auto-claims the daily
    /// login + weekly chest on launch. The authority for all currency and power-up state.
    /// </summary>
    [DefaultExecutionOrder(-80)] // after SaveSystem (-90); coins seed from it on first run
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager Instance { get; private set; }

        private EconomyData _data = new EconomyData();

        /// <summary>Fires whenever any balance (coins/gems/power-ups) changes — drives the HUD.</summary>
        public event Action OnBalanceChanged;
        /// <summary>Fires when a power-up is dropped after a level — drives the reward popup.</summary>
        public event Action<PowerUpType> OnPowerUpDropped;
        /// <summary>Fires after a daily-login claim (day, coins, gems).</summary>
        public event Action<int, int, int> OnDailyClaimed;
        /// <summary>Fires after a weekly-chest claim (gems).</summary>
        public event Action<int> OnWeeklyClaimed;

        public int Coins          => _data.coins;
        public int Gems           => _data.gems;
        public int ExtraTubes     => _data.extraTubes;
        public int LightningBolts => _data.lightningBolts;

        /// <summary>Ensures the singleton exists (called from GameBootstrap before HUD build).</summary>
        public static EconomyManager EnsureInstance()
        {
            if (Instance == null)
                new GameObject("EconomyManager").AddComponent<EconomyManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Auto-claim once-per-day / once-per-week rewards on launch (no dedicated screen).
            ClaimDailyReward();
            ClaimWeeklyChest();
        }

        // ── Persistence ──────────────────────────────────────────────────────────────
        private void Load()
        {
            string json = PlayerPrefs.GetString(EconomyConfig.SaveKey, "");
            if (string.IsNullOrEmpty(json))
            {
                _data = new EconomyData();
                // Migrate any pre-existing coin balance owned by SaveSystem so coins aren't lost.
                var ss = SaveSystem.SaveSystem.Instance;
                if (ss != null && ss.IsReady) _data.coins = ss.GetCoinBalance();
                Save();
            }
            else
            {
                _data = JsonUtility.FromJson<EconomyData>(json) ?? new EconomyData();
                _data.completedLevels     ??= new List<int>();
                _data.milestonesClaimed   ??= new List<int>();
                _data.achievementsClaimed ??= new List<int>();
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(EconomyConfig.SaveKey, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
            MirrorCoinsToSaveSystem();
        }

        // Keep SaveSystem's coin_balance in sync so the Shop and win overlay read the right value.
        private void MirrorCoinsToSaveSystem()
        {
            var ss = SaveSystem.SaveSystem.Instance;
            if (ss != null && ss.IsReady) ss.SetCoinBalance(_data.coins);
        }

        // ── Level completion + drops (System 2 + 3) ──────────────────────────────────

        /// <summary>
        /// Awards coins/gems for completing <paramref name="levelId"/> and (for non-tutorial levels)
        /// rolls power-up drops. Returns the base coin reward to display on the win card.
        /// </summary>
        public int OnLevelComplete(int levelId, int stars, bool isTutorial)
        {
            LevelRewardResult r = _data.RegisterLevelComplete(levelId, stars);
            Save();
            OnBalanceChanged?.Invoke();
            if (!isTutorial) TryDropPowerUp();
            return r.LevelCoins;
        }

        /// <summary>Independent 8% rolls per power-up type after a level; blocked at the held cap of 3.</summary>
        public void TryDropPowerUp()
        {
            bool any = false;
            if (_data.extraTubes < EconomyConfig.PowerUpMaxHeld &&
                UnityEngine.Random.value < EconomyConfig.PowerUpDropChance)
            { _data.extraTubes++; any = true; OnPowerUpDropped?.Invoke(PowerUpType.ExtraTube); }

            if (_data.lightningBolts < EconomyConfig.PowerUpMaxHeld &&
                UnityEngine.Random.value < EconomyConfig.PowerUpDropChance)
            { _data.lightningBolts++; any = true; OnPowerUpDropped?.Invoke(PowerUpType.LightningBolt); }

            if (any) { Save(); OnBalanceChanged?.Invoke(); }
        }

        // ── Daily / weekly (System 3) ────────────────────────────────────────────────
        public DailyRewardResult ClaimDailyReward()
        {
            DailyRewardResult r = _data.ClaimDaily(DateTime.Now);
            if (r.Claimed)
            {
                Save();
                OnBalanceChanged?.Invoke();
                OnDailyClaimed?.Invoke(r.Day, r.Coins, r.Gems);
            }
            return r;
        }

        public int ClaimWeeklyChest()
        {
            int gems = _data.ClaimWeekly(DateTime.Now,
                UnityEngine.Random.Range(EconomyConfig.WeeklyChestMin, EconomyConfig.WeeklyChestMax + 1));
            if (gems > 0)
            {
                Save();
                OnBalanceChanged?.Invoke();
                OnWeeklyClaimed?.Invoke(gems);
            }
            return gems;
        }

        // ── Rewarded ad / spend / buy ────────────────────────────────────────────────
        public bool WatchRewardedAd(bool chooseGems)
        {
            bool ok = _data.TryRewardedAd(DateTime.Now, chooseGems);
            if (ok) { Save(); OnBalanceChanged?.Invoke(); }
            return ok;
        }

        public bool SpendCoins(int amount) { bool ok = _data.SpendCoins(amount); if (ok) { Save(); OnBalanceChanged?.Invoke(); } return ok; }
        public bool SpendGems(int amount)  { bool ok = _data.SpendGems(amount);  if (ok) { Save(); OnBalanceChanged?.Invoke(); } return ok; }
        public void AddCoins(int amount)   { _data.AddCoins(amount); Save(); OnBalanceChanged?.Invoke(); }
        public void AddGems(int amount)    { _data.AddGems(amount);  Save(); OnBalanceChanged?.Invoke(); }

        public bool BuyExtraTube()     { return BuyPowerUp(PowerUpType.ExtraTube); }
        public bool BuyLightningBolt() { return BuyPowerUp(PowerUpType.LightningBolt); }

        private bool BuyPowerUp(PowerUpType type)
        {
            bool ok = _data.BuyPowerUp(type);
            if (ok) { Save(); OnBalanceChanged?.Invoke(); }
            return ok;
        }

        /// <summary>Consumes one owned power-up of the given type. Returns false when none are held.</summary>
        public bool ConsumePowerUp(PowerUpType type)
        {
            if (_data.Held(type) <= 0) return false;
            if (type == PowerUpType.ExtraTube) _data.extraTubes--; else _data.lightningBolts--;
            Save();
            OnBalanceChanged?.Invoke();
            return true;
        }
    }
}
