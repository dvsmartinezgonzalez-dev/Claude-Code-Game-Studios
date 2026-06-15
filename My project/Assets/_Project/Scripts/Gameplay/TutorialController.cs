using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using GSM = BoltSort.GameStateManager.GameStateManager;
using SM  = BoltSort.SortMechanic.SortMechanic;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Phase-2 mechanic tutorial TRIGGER stubs (TDD §6). On level load, if the level introduces a
    /// mechanic the player has not seen yet, pauses the board and shows a minimal placeholder panel
    /// (mechanic name + a one-line hint + OK). A PlayerPrefs flag <c>bs.tut.&lt;key&gt;</c> is set on
    /// dismiss so each tutorial shows exactly once.
    ///
    /// This is intentionally a STUB: the full animated-hand tutorial overlay is a later phase. The
    /// trigger logic, one-shot flags, pause gate (reuses <see cref="SM.SetGamePaused"/>), and
    /// per-mechanic copy are all final; only the visuals are placeholder.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        private GSM  _gsm;
        private SM   _sm;
        private Font _font;
        private GameObject _overlay;
        private string _pendingFlagKey;

        public void Initialize(GSM gsm, SM sm, Font font)
        {
            _gsm  = gsm;
            _sm   = sm;
            _font = font ?? GameAssets.MenuFont;
            if (_gsm != null) _gsm.OnLevelLoaded += OnLevelLoaded;
        }

        private void OnDestroy()
        {
            if (_gsm != null) _gsm.OnLevelLoaded -= OnLevelLoaded;
        }

        private void OnLevelLoaded(int levelId, int colorCount, int stackDepth,
                                   int tempSlotCount, int tempSlotDepth, long seqId)
        {
            if (_overlay != null) return; // already showing one

            var lds = BoltSort.LevelData.LevelDataSystem.Instance;
            if (lds == null || !lds.IsReady) return;

            BoltSort.LevelData.LevelRecord rec;
            try { rec = lds.GetLevel(levelId); }
            catch (BoltSort.LevelData.LevelDataException) { return; }

            // Show the first un-seen mechanic present on this level (one per load).
            if (HasFrozen(rec) && NotSeen("frozen"))
                Show("frozen", "FROZEN TUBE", "This tube is frozen for a few moves. Plan around it!");
            else if (HasMystery(rec) && NotSeen("mystery"))
                Show("mystery", "MYSTERY BALL", "This ball hides its color. Move the ball above it to reveal it!");
            else if (rec.HasMulticolor && NotSeen("multicolor"))
                Show("multicolor", "MULTICOLOR BALL", "This special ball matches any color. Use it wisely!");
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

        // ── Placeholder overlay ───────────────────────────────────────────────────

        private void Show(string flagKey, string title, string body)
        {
            _pendingFlagKey = flagKey;
            _sm?.SetGamePaused(true); // block board input until dismissed

            var canvasGO = new GameObject("TutorialOverlay");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150; // above board/HUD, below win overlay (200)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight  = 1f;
            canvasGO.AddComponent<GraphicRaycaster>();
            _overlay = canvasGO;

            // Dim backdrop (also swallows taps to the board behind it)
            var dim = new GameObject("Dim");
            dim.transform.SetParent(canvasGO.transform, false);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);
            Stretch(dim.GetComponent<RectTransform>());

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            panel.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.98f);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f); prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(560f, 420f);

            MakeLabel(panel, "Title", title, 48, new Vector2(0f, 110f), new Vector2(520f, 90f),
                      BoltSortTheme.WinGold);
            MakeLabel(panel, "Body", body, 30, new Vector2(0f, -10f), new Vector2(500f, 180f),
                      Color.white);

            // OK button
            var okGO = new GameObject("OK");
            okGO.transform.SetParent(panel.transform, false);
            okGO.AddComponent<Image>().color = new Color(0.20f, 0.55f, 0.30f, 1f);
            var okBtn = okGO.AddComponent<Button>();
            okBtn.onClick.AddListener(Dismiss);
            var okrt = okGO.GetComponent<RectTransform>();
            okrt.anchorMin = new Vector2(0.5f, 0f); okrt.anchorMax = new Vector2(0.5f, 0f);
            okrt.pivot = new Vector2(0.5f, 0f);
            okrt.sizeDelta = new Vector2(220f, 80f);
            okrt.anchoredPosition = new Vector2(0f, 30f);
            MakeLabel(okGO, "Label", "OK", 36, Vector2.zero, new Vector2(220f, 80f), Color.white);
        }

        private void Dismiss()
        {
            AudioMgr.Instance?.PlaySFX("button_tap");
            if (!string.IsNullOrEmpty(_pendingFlagKey))
            {
                PlayerPrefs.SetInt("bs.tut." + _pendingFlagKey, 1);
                PlayerPrefs.Save();
                _pendingFlagKey = null;
            }
            _sm?.SetGamePaused(false);
            if (_overlay != null) { Destroy(_overlay); _overlay = null; }
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        private void MakeLabel(GameObject parent, string name, string text, int size,
                               Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = _font; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = color; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
