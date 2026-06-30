using System;
using System.Collections;
using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Minimal rewarded-ad facade. This is a STUB that auto-completes successfully so the
    /// reward flows (watch-ad-for-gems, double-reward) are fully wired and testable before the
    /// real ad SDK lands. Replace the bodies of <see cref="IsRewardedAdReady"/> and
    /// <see cref="ShowRewardedAd"/> with the Google Mobile Ads (AdMob) rewarded-ad calls at the
    /// Beta milestone — keep the same signatures so callers (Victory screen, Shop) are unchanged.
    /// </summary>
    [DefaultExecutionOrder(-85)]
    public class AdService : MonoBehaviour
    {
        public static AdService Instance { get; private set; }

        /// <summary>Ensures the singleton exists. Safe to call from any scene's setup.</summary>
        public static AdService EnsureInstance()
        {
            if (Instance == null)
                new GameObject("AdService").AddComponent<AdService>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// True when a rewarded ad is loaded and can be shown. STUB: always ready.
        /// Real impl: return <c>_rewardedAd != null &amp;&amp; _rewardedAd.CanShowAd()</c>.
        /// </summary>
        public bool IsRewardedAdReady() => true;

        /// <summary>
        /// Shows a rewarded ad and invokes <paramref name="onComplete"/> with the result
        /// (true = user earned the reward, false = dismissed/failed). STUB: completes true next frame.
        /// Real impl: subscribe to the SDK's OnUserEarnedReward / OnAdFullScreenContentClosed and
        /// forward the outcome here; ALWAYS invoke the callback exactly once.
        /// </summary>
        public void ShowRewardedAd(Action<bool> onComplete)
        {
            if (RewardConfig.Active.VerboseLogs)
                Debug.Log("[AdService] ShowRewardedAd (stub) — auto-completing success next frame.");
            StartCoroutine(CompleteNextFrame(onComplete));
        }

        private static IEnumerator CompleteNextFrame(Action<bool> onComplete)
        {
            yield return null; // simulate the ad round-trip without blocking the click handler
            onComplete?.Invoke(true);
        }
    }
}
