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
        public int  SpecialCoins;   // first-time special-level coin bonus (RewardConfig; 0 if none)
        public int  SpecialGems;    // first-time special-level gem bonus (RewardConfig; 0 if none)
        public int  RandomGems;     // random gem drop on a normal level (RewardConfig; 0 if none)

        /// <summary>Total coins earned this completion (base + streak + special).</summary>
        public int TotalCoins => LevelCoins + StreakBonus + SpecialCoins;
        /// <summary>Total gems earned this completion (milestone + achievement + special + random).</summary>
        public int TotalGems  => MilestoneGems + AchievementGems + SpecialGems + RandomGems;
    }

    /// <summary>
    /// Extra hard-reward inputs for a completion, supplied by the Unity layer so
    /// <see cref="EconomyData"/> stays free of UnityEngine / RNG / ScriptableObjects. Default value
    /// (all zero, <see cref="IsSpecial"/> false) grants no bonuses — preserving classic behaviour.
    /// </summary>
    public struct BonusRules
    {
        public bool  IsSpecial;    // level has mystery / multicolor / frozen mechanics
        public int   SpecialGems;  // gems to grant on FIRST special completion
        public int   SpecialCoins; // coins to grant on FIRST special completion
        public int   RandomGems;   // gems to grant if the random roll succeeds (normal levels)
        public float RandomProb;   // probability threshold for the random gem drop
        public float RandomRoll;   // caller-supplied roll in [0,1)
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
            => RegisterLevelComplete(levelId, stars, default);

        public LevelRewardResult RegisterLevelComplete(int levelId, int stars, BonusRules bonus)
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

            // First-time special-level bonus (mystery / multicolor / frozen). First completion is
            // inherently once-only (level enters completedLevels above), so no extra "claimed" set.
            if (first && bonus.IsSpecial)
            {
                r.SpecialGems  = bonus.SpecialGems;
                r.SpecialCoins = bonus.SpecialCoins;
            }
            // Random gem drop on normal (non-special) levels — any completion, gated by RNG roll.
            else if (!bonus.IsSpecial && bonus.RandomRoll < bonus.RandomProb)
            {
                r.RandomGems = bonus.RandomGems;
            }

            coins += r.LevelCoins + r.StreakBonus + r.SpecialCoins;
            gems  += r.MilestoneGems + r.AchievementGems + r.SpecialGems + r.RandomGems;
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
            // Auto-claim once-per-day / once-per-week rewards on launch (no dedicated screen) — but
            // ONLY for a profile that has already played. A brand-new profile must stay 0 coins / 0
            // gems until it clears its first level; this also stops "opening the Shop" (which lazily
            // creates this manager) from granting the daily/weekly chest. See OnLevelComplete.
            if (_data.completedLevels.Count > 0)
            {
                ClaimDailyReward();
                ClaimWeeklyChest();
            }
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
        /// Awards coins/gems for completing <paramref name="levelId"/> (base + streak + milestone +
        /// achievement, plus the RewardConfig first-time-special and random-gem bonuses for
        /// non-tutorial levels), rolls power-up drops, and — for non-tutorial levels — performs the
        /// deferred daily/weekly auto-claim (kept off the fresh-profile / shop-open path so a new
        /// profile stays 0/0 until the player actually plays). Returns the full reward breakdown.
        /// </summary>
        public LevelRewardResult OnLevelComplete(int levelId, int stars, bool isTutorial, bool isSpecial)
        {
            var cfg = RewardConfig.Active;

            BonusRules bonus = default;
            if (!isTutorial)
            {
                bonus.IsSpecial    = isSpecial;
                bonus.SpecialGems  = cfg.DiamondsFirstTimeSpecial;
                bonus.SpecialCoins = cfg.CoinsFirstTimeSpecial;
                bonus.RandomGems   = cfg.DiamondsForRandom;
                bonus.RandomProb   = cfg.ProbRandomDiamond;
                bonus.RandomRoll   = UnityEngine.Random.value;
            }

            LevelRewardResult r = _data.RegisterLevelComplete(levelId, stars, bonus);
            Save();
            OnBalanceChanged?.Invoke();

            if (cfg.VerboseLogs)
                Debug.Log($"[Reward] Level {levelId} (special={isSpecial}, tutorial={isTutorial}): " +
                          $"coins +{r.TotalCoins} [base {r.LevelCoins}, streak {r.StreakBonus}, special {r.SpecialCoins}], " +
                          $"gems +{r.TotalGems} [milestone {r.MilestoneGems}, achiev {r.AchievementGems}, " +
                          $"special {r.SpecialGems}, random {r.RandomGems}]  →  coins={_data.coins}, gems={_data.gems}");

            if (!isTutorial)
            {
                TryDropPowerUp();
                // Deferred daily/weekly: both are time-gated (idempotent), so calling per completion
                // is cheap and means a fresh profile only starts earning them once it engages.
                ClaimDailyReward();
                ClaimWeeklyChest();
            }
            return r;
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
        /// <summary>Central audited coin grant. <paramref name="context"/> tags the caller in the log.</summary>
        public void AddCoins(int amount, string context = null)
        {
            if (amount <= 0) return;
            _data.AddCoins(amount); Save(); OnBalanceChanged?.Invoke();
            if (RewardConfig.Active.VerboseLogs)
                Debug.Log($"[Reward] AddCoins +{amount} ({context ?? "unspecified"}) → coins={_data.coins}");
        }

        /// <summary>Central audited gem (diamond) grant. <paramref name="context"/> tags the caller in the log.</summary>
        public void AddGems(int amount, string context = null)
        {
            if (amount <= 0) return;
            _data.AddGems(amount);  Save(); OnBalanceChanged?.Invoke();
            if (RewardConfig.Active.VerboseLogs)
                Debug.Log($"[Reward] AddGems +{amount} ({context ?? "unspecified"}) → gems={_data.gems}");
        }

        /// <summary>
        /// Grants the rewarded-ad gem reward (RewardConfig.DiamondsPerAdWatch). Call this from the
        /// "watch ad for gems" button after <see cref="AdService.ShowRewardedAd"/> reports success.
        /// </summary>
        public void GrantAdGemReward() => AddGems(RewardConfig.Active.DiamondsPerAdWatch, "ad_watch");

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
