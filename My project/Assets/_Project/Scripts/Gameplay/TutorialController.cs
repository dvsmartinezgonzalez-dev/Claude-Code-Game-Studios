using UnityEngine;
using BoltSort.Visual;
using GSM = BoltSort.GameStateManager.GameStateManager;
using SM  = BoltSort.SortMechanic.SortMechanic;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Detects the first time a level introduces a phase-2 mechanic (frozen tube, mystery ball,
    /// multicolor ball) and shows a one-shot <see cref="SpecialMechanicSplash"/> — the same
    /// full-screen popup UI used by <see cref="MagnetUnlockSplash"/>. Each mechanic is shown
    /// exactly once, gated by the <c>bs.tut.&lt;key&gt;</c> PlayerPrefs flag set on dismiss.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        private GSM _gsm;
        private SM  _sm;

        // Non-null while a splash is visible; prevents a second splash on rapid level changes.
        private SpecialMechanicSplash _activeSplash;

        public void Initialize(GSM gsm, SM sm, UnityEngine.Font font)
        {
            _gsm = gsm;
            _sm  = sm;
            // font kept for signature compatibility; SpecialMechanicSplash resolves its own font.
            if (_gsm != null) _gsm.OnLevelLoaded += OnLevelLoaded;
        }

        private void OnDestroy()
        {
            if (_gsm != null) _gsm.OnLevelLoaded -= OnLevelLoaded;
        }

        private void OnLevelLoaded(int levelId, int colorCount, int stackDepth,
                                   int tempSlotCount, int tempSlotDepth, long seqId)
        {
            if (_activeSplash != null) return; // already showing one

            var lds = BoltSort.LevelData.LevelDataSystem.Instance;
            if (lds == null || !lds.IsReady) return;

            BoltSort.LevelData.LevelRecord rec;
            try { rec = lds.GetLevel(levelId); }
            catch (BoltSort.LevelData.LevelDataException) { return; }

            // Show the first un-seen mechanic present on this level (one per load).
            if (HasFrozen(rec) && NotSeen("frozen"))
                ShowSplash("frozen",     "key_tut_frozen_title",     "key_tut_frozen_body",
                           GameAssets.SnowFlakeIcon ?? GameAssets.SnowflakeBadge);
            else if (HasMystery(rec) && NotSeen("mystery"))
                ShowSplash("mystery",    "key_tut_mystery_title",    "key_tut_mystery_body",
                           GameAssets.BallMystery);
            else if (rec.HasMulticolor && NotSeen("multicolor"))
                ShowSplash("multicolor", "key_tut_multicolor_title", "key_tut_multicolor_body",
                           GameAssets.BallMulticolor);
        }

        private static bool HasFrozen(BoltSort.LevelData.LevelRecord rec)
            => rec.FrozenTubes != null && rec.FrozenTubes.Length > 0;

        private static bool HasMystery(BoltSort.LevelData.LevelRecord rec)
        {
            if (rec.MysteryBalls) return true;
            if (rec.ColorStacks != null)
                foreach (var stack in rec.ColorStacks)
                    if (stack != null)
                        foreach (int c in stack)
                            if (c < 0) return true;
            return false;
        }

        private static bool NotSeen(string key) => PlayerPrefs.GetInt("bs.tut." + key, 0) == 0;

        private void ShowSplash(string flagKey, string titleKey, string bodyKey, Sprite icon)
        {
            _sm?.SetGamePaused(true);
            _activeSplash = SpecialMechanicSplash.Show(
                GameAssets.MenuFont,
                icon,
                titleKey,
                bodyKey,
                onDismiss: () =>
                {
                    PlayerPrefs.SetInt("bs.tut." + flagKey, 1);
                    PlayerPrefs.Save();
                    _activeSplash = null;
                    _sm?.SetGamePaused(false);
                });
        }
    }
}
