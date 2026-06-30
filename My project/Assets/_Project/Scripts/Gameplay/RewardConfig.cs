using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Inspector-tunable hard-reward parameters layered on top of the existing
    /// <see cref="EconomyConfig"/> economy (System 2/3). Holds ONLY the values introduced by the
    /// reward/diamond pass: first-time-special grants, the per-normal-level random gem drop, the
    /// rewarded-ad gem reward and the "double reward every X levels" ad mechanic.
    ///
    /// Loaded at runtime from <c>Resources/RewardConfig.asset</c> via <see cref="Active"/>; when the
    /// asset is absent a code-default instance (these field initialisers) is used, so gameplay and
    /// unit tests work head-less without the asset. Create/edit the asset from the Unity menu
    /// <c>BoltSort ▸ Create Reward Config Asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "BoltSort/Reward Config", fileName = "RewardConfig")]
    public class RewardConfig : ScriptableObject
    {
        [Header("First-time special level (mystery / multicolor / frozen)")]
        [Tooltip("Gems granted the first time a special level is completed.")]
        public int DiamondsFirstTimeSpecial = 3;
        [Tooltip("Coins granted the first time a special level is completed.")]
        public int CoinsFirstTimeSpecial = 1000;

        [Header("Random gem drop (normal levels only)")]
        [Range(0f, 1f)]
        [Tooltip("Probability of a gem drop on completing a normal (non-special, non-tutorial) level.")]
        public float ProbRandomDiamond = 0.01f;
        [Tooltip("Gems granted when the random drop fires.")]
        public int DiamondsForRandom = 1;

        [Header("Rewarded ad")]
        [Tooltip("Gems granted for watching a rewarded ad (the explicit 'watch ad for gems' button).")]
        public int DiamondsPerAdWatch = 2;

        [Header("Double reward (ad)")]
        [Tooltip("The 'double your reward' ad offer appears on every Nth completed level. 0 disables it.")]
        public int DoubleRewardEveryXLevels = 5;
        [Tooltip("Multiplier applied to the just-earned coins + gems when the double-reward ad is watched.")]
        public float MultiplierOnDouble = 2.0f;

        [Header("Debug")]
        [Tooltip("Dev-only verbose logging for every currency mutation and reward grant.")]
        public bool VerboseLogs = true;

        private static RewardConfig _active;

        /// <summary>
        /// The active config: the <c>Resources/RewardConfig</c> asset if present, otherwise a
        /// code-default instance. Never null. Cached after first access.
        /// </summary>
        public static RewardConfig Active
        {
            get
            {
                if (_active != null) return _active;
                _active = Resources.Load<RewardConfig>("RewardConfig");
                if (_active == null)
                {
                    _active = CreateInstance<RewardConfig>();
                    _active.name = "RewardConfig (defaults)";
                }
                return _active;
            }
        }

        /// <summary>Test/Editor hook: forces a specific config (or null to reset to lazy-load).</summary>
        public static void OverrideActive(RewardConfig config) => _active = config;
    }
}
