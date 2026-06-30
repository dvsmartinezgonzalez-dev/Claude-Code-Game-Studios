using System;
using NUnit.Framework;
using BoltSort.Gameplay;

namespace BoltSort.Tests.Unit.Economy
{
    /// <summary>
    /// Unit tests for <see cref="EconomyData"/> (System 3 rules + System 2 caps): coin/gem earning,
    /// first-time vs replay, streaks, milestones, achievements, daily/weekly cycles, spending and
    /// power-up caps. Pure logic — no Unity, no PlayerPrefs; clock/random are injected.
    /// </summary>
    public class EconomyData_Test
    {
        // ── Coins: first-time vs replay ──────────────────────────────────────────────
        [Test]
        public void test_first_time_completion_awards_first_time_coins()
        {
            var d = new EconomyData();
            var r = d.RegisterLevelComplete(1, 3);
            Assert.IsTrue(r.FirstTime);
            Assert.AreEqual(55, r.LevelCoins);
            Assert.AreEqual(55, d.coins);
        }

        [Test]
        public void test_replay_completion_awards_replay_coins()
        {
            var d = new EconomyData();
            d.RegisterLevelComplete(1, 3);            // first time: 55
            var r = d.RegisterLevelComplete(1, 3);    // replay: 12
            Assert.IsFalse(r.FirstTime);
            Assert.AreEqual(12, r.LevelCoins);
            Assert.AreEqual(67, d.coins);
        }

        [Test]
        public void test_coin_rewards_scale_with_stars()
        {
            var d = new EconomyData();
            Assert.AreEqual(20, d.RegisterLevelComplete(1, 1).LevelCoins);
            Assert.AreEqual(35, d.RegisterLevelComplete(2, 2).LevelCoins);
            Assert.AreEqual(55, d.RegisterLevelComplete(3, 3).LevelCoins);
        }

        // ── Streak ───────────────────────────────────────────────────────────────────
        [Test]
        public void test_streak_bonus_triggers_at_5_and_10()
        {
            var d = new EconomyData();
            for (int i = 1; i <= 4; i++) Assert.AreEqual(0, d.RegisterLevelComplete(i, 1).StreakBonus);
            Assert.AreEqual(100, d.RegisterLevelComplete(5, 1).StreakBonus);
            for (int i = 6; i <= 9; i++) Assert.AreEqual(0, d.RegisterLevelComplete(i, 1).StreakBonus);
            Assert.AreEqual(250, d.RegisterLevelComplete(10, 1).StreakBonus);
        }

        [Test]
        public void test_replay_resets_streak()
        {
            var d = new EconomyData();
            d.RegisterLevelComplete(1, 1);
            d.RegisterLevelComplete(2, 1);
            Assert.AreEqual(2, d.currentStreak);
            d.RegisterLevelComplete(1, 1); // replay
            Assert.AreEqual(0, d.currentStreak);
        }

        // ── Reward pass: first-time-special bonus + random gem drop (BonusRules) ─────
        [Test]
        public void test_bonus_first_time_special_awards_special_coins_and_gems()
        {
            // Arrange
            var d = new EconomyData();
            var bonus = new BonusRules { IsSpecial = true, SpecialGems = 3, SpecialCoins = 1000 };

            // Act
            var r = d.RegisterLevelComplete(7, 3, bonus);

            // Assert
            Assert.AreEqual(3, r.SpecialGems);
            Assert.AreEqual(1000, r.SpecialCoins);
            Assert.AreEqual(3, d.gems);
            Assert.AreEqual(55 + 1000, d.coins); // first-time 3★ base + special coins
        }

        [Test]
        public void test_bonus_special_replay_awards_no_special_bonus()
        {
            // Arrange — first clear pays the special bonus
            var d = new EconomyData();
            var bonus = new BonusRules { IsSpecial = true, SpecialGems = 3, SpecialCoins = 1000 };
            d.RegisterLevelComplete(7, 3, bonus);

            // Act — replay
            var r = d.RegisterLevelComplete(7, 3, bonus);

            // Assert
            Assert.AreEqual(0, r.SpecialGems);
            Assert.AreEqual(0, r.SpecialCoins);
            Assert.AreEqual(3, d.gems); // unchanged from the first clear
        }

        [Test]
        public void test_bonus_random_drop_below_threshold_awards_gem()
        {
            // Arrange — roll strictly below the probability ⇒ drop fires
            var d = new EconomyData();
            var bonus = new BonusRules { IsSpecial = false, RandomGems = 1, RandomProb = 0.01f, RandomRoll = 0f };

            // Act
            var r = d.RegisterLevelComplete(2, 1, bonus);

            // Assert
            Assert.AreEqual(1, r.RandomGems);
            Assert.AreEqual(1, d.gems);
        }

        [Test]
        public void test_bonus_random_drop_at_or_above_threshold_awards_nothing()
        {
            // Arrange — roll above the probability ⇒ no drop
            var d = new EconomyData();
            var bonus = new BonusRules { IsSpecial = false, RandomGems = 1, RandomProb = 0.01f, RandomRoll = 0.5f };

            // Act
            var r = d.RegisterLevelComplete(2, 1, bonus);

            // Assert
            Assert.AreEqual(0, r.RandomGems);
            Assert.AreEqual(0, d.gems);
        }

        [Test]
        public void test_bonus_special_level_does_not_roll_random_drop()
        {
            // Arrange — special level with a roll that WOULD pass the random gate
            var d = new EconomyData();
            var bonus = new BonusRules
            {
                IsSpecial = true, SpecialGems = 3, SpecialCoins = 1000,
                RandomGems = 1, RandomProb = 0.01f, RandomRoll = 0f,
            };

            // Act
            var r = d.RegisterLevelComplete(7, 3, bonus);

            // Assert — only the special bonus pays out; the random drop never applies on specials
            Assert.AreEqual(0, r.RandomGems);
            Assert.AreEqual(3, r.SpecialGems);
        }

        [Test]
        public void test_bonus_default_rules_preserve_legacy_behaviour()
        {
            // Arrange / Act — the 2-arg overload (legacy callers) must behave exactly as before
            var d = new EconomyData();
            var r = d.RegisterLevelComplete(25, 1); // a milestone level

            // Assert — milestone gems only; no special/random additions
            Assert.AreEqual(0, r.SpecialGems);
            Assert.AreEqual(0, r.RandomGems);
            Assert.AreEqual(10, r.MilestoneGems);
            Assert.AreEqual(10, d.gems);
        }

        // ── Gems: milestones + achievements ──────────────────────────────────────────
        [Test]
        public void test_milestone_gems_awarded_once()
        {
            var d = new EconomyData();
            for (int i = 1; i < 25; i++) d.RegisterLevelComplete(i, 1);
            Assert.AreEqual(10, d.RegisterLevelComplete(25, 1).MilestoneGems);
            Assert.AreEqual(0, d.RegisterLevelComplete(25, 1).MilestoneGems); // replay: no repeat
        }

        [Test]
        public void test_achievement_gems_awarded_on_count_threshold()
        {
            var d = new EconomyData();
            for (int i = 1; i <= 50; i++) d.RegisterLevelComplete(i, 1);
            CollectionAssert.Contains(d.achievementsClaimed, 50);
            // gems = milestone(25) + milestone(50) + achievement(50) = 10 + 10 + 20
            Assert.AreEqual(40, d.gems);
        }

        // ── Daily login ──────────────────────────────────────────────────────────────
        [Test]
        public void test_daily_login_increments_and_wraps_after_day_7()
        {
            var d = new EconomyData();
            var t0 = new DateTime(2026, 1, 1);
            for (int day = 1; day <= 7; day++)
            {
                var r = d.ClaimDaily(t0.AddDays(day - 1));
                Assert.IsTrue(r.Claimed);
                Assert.AreEqual(day, r.Day);
                Assert.AreEqual(EconomyConfig.DailyCoins[day - 1], r.Coins);
                Assert.AreEqual(EconomyConfig.DailyGems[day - 1], r.Gems);
            }
            var wrap = d.ClaimDaily(t0.AddDays(7));
            Assert.AreEqual(1, wrap.Day); // resets after day 7
        }

        [Test]
        public void test_daily_login_same_day_does_not_double_claim()
        {
            var d = new EconomyData();
            var t = new DateTime(2026, 1, 1, 8, 0, 0);
            Assert.IsTrue(d.ClaimDaily(t).Claimed);
            Assert.IsFalse(d.ClaimDaily(t.AddHours(6)).Claimed);
        }

        // ── Weekly chest ─────────────────────────────────────────────────────────────
        [Test]
        public void test_weekly_chest_respects_7_day_cooldown()
        {
            var d = new EconomyData();
            var t = new DateTime(2026, 1, 1);
            Assert.AreEqual(30, d.ClaimWeekly(t, 30));
            Assert.AreEqual(0, d.ClaimWeekly(t.AddDays(3), 40));  // still on cooldown
            Assert.AreEqual(45, d.ClaimWeekly(t.AddDays(7), 45)); // cooldown elapsed
        }

        // ── Rewarded ad ──────────────────────────────────────────────────────────────
        [Test]
        public void test_rewarded_ad_capped_at_3_per_day_and_resets()
        {
            var d = new EconomyData();
            var t = new DateTime(2026, 1, 1);
            Assert.IsTrue(d.TryRewardedAd(t, false));
            Assert.IsTrue(d.TryRewardedAd(t, true));
            Assert.IsTrue(d.TryRewardedAd(t, false));
            Assert.IsFalse(d.TryRewardedAd(t, false));        // 4th today blocked
            Assert.IsTrue(d.TryRewardedAd(t.AddDays(1), false)); // new day resets
        }

        // ── Spending + power-up caps/prices ──────────────────────────────────────────
        [Test]
        public void test_spend_gems_insufficient_returns_false()
        {
            var d = new EconomyData { gems = 50 };
            Assert.IsFalse(d.SpendGems(80));
            Assert.AreEqual(50, d.gems);
            Assert.IsTrue(d.SpendGems(40));
            Assert.AreEqual(10, d.gems);
        }

        [Test]
        public void test_buy_extra_tube_charges_gems_and_respects_cap()
        {
            var d = new EconomyData { gems = 500 };
            Assert.IsTrue(d.BuyPowerUp(PowerUpType.ExtraTube));  // -150 → 350
            Assert.IsTrue(d.BuyPowerUp(PowerUpType.ExtraTube));  // -150 → 200
            Assert.IsTrue(d.BuyPowerUp(PowerUpType.ExtraTube));  // -150 → 50
            Assert.IsFalse(d.BuyPowerUp(PowerUpType.ExtraTube)); // at cap 3 → no charge
            Assert.AreEqual(3, d.extraTubes);
            Assert.AreEqual(50, d.gems);
        }

        [Test]
        public void test_grant_power_up_respects_cap_of_3()
        {
            var d = new EconomyData();
            Assert.IsTrue(d.GrantPowerUp(PowerUpType.LightningBolt));
            Assert.IsTrue(d.GrantPowerUp(PowerUpType.LightningBolt));
            Assert.IsTrue(d.GrantPowerUp(PowerUpType.LightningBolt));
            Assert.IsFalse(d.GrantPowerUp(PowerUpType.LightningBolt)); // cap
            Assert.AreEqual(3, d.lightningBolts);
        }
    }
}
