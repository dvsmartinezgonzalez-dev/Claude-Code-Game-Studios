using System;
using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// First-run mandatory language selector. Shown on the Main Menu the very first time the
    /// game is launched (before any language has ever been persisted). Renders a full-screen
    /// modal whose opaque, raycast-blocking background intercepts every tap, so the player
    /// cannot reach PLAY / the tutorial until they pick a language. On selection it persists
    /// the choice, forces <see cref="LocalizationManager"/> to reload its table (which live-
    /// refreshes all <see cref="LocalizedText"/>), then invokes the supplied callback.
    ///
    /// Gated by <see cref="LocalizationManager.PlayerPrefsKey"/> ("current_language"): that key
    /// is absent until the player explicitly chooses a language for the first time, so returning
    /// players (and anyone who picked a language in Settings) never see this popup again.
    /// </summary>
    public class FirstRunLanguagePopup : MonoBehaviour
    {
        // Keep in sync with SettingsPanel.LangCodes / Languages.
        private const string LangIdxKey  = "bs.language";
        private const string LangNameKey = "Language";

        private static readonly string[] Languages =
            { "English", "Español", "Português", "Français", "Deutsch",
              "Italiano", "Русский", "中文", "日本語", "हिन्दी" };
        private static readonly string[] LangCodes =
            { "en", "es", "pt", "fr", "de", "it", "ru", "zh", "ja", "hi" };

        /// <summary>True while a first-run popup is on screen and blocking input. The Main Menu's
        /// Android back-button handler checks this so Esc cannot bypass the mandatory choice.</summary>
        public static bool IsBlocking { get; private set; }

        private Action _onChosen;

        /// <summary>True the first time the game runs — no language has ever been persisted.</summary>
        public static bool ShouldShow => !PlayerPrefs.HasKey(LocalizationManager.PlayerPrefsKey);

        /// <summary>Builds and shows the modal under <paramref name="canvasRoot"/>. <paramref name="onChosen"/>
        /// fires once the player picks a language and the popup has closed.</summary>
        public void Show(Font font, Transform canvasRoot, Action onChosen)
        {
            _onChosen = onChosen;
            IsBlocking = true;
            DevLog("shown (first run, no language persisted)");
            try { BuildOverlay(font, canvasRoot); }
            catch (Exception e)
            {
                // Never hard-block the menu if the popup fails to build — fall back to default.
                Debug.LogError($"[FirstRunLanguagePopup] Build failed; defaulting language. {e}");
                CommitDefaultAndClose();
            }
        }

        private void BuildOverlay(Font font, Transform canvasRoot)
        {
            // Opaque, raycast-blocking dim that swallows every tap behind the card.
            var dimImg = gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.82f);
            transform.SetParent(canvasRoot, false);
            transform.SetAsLastSibling();
            Stretch(GetComponent<RectTransform>());
            gameObject.AddComponent<Button>(); // consume taps on the dim (no listener = no-op)

            // Card
            var card = new GameObject("Card");
            card.transform.SetParent(transform, false);
            var cardBg = card.AddComponent<Image>();
            var bgSpr = GameAssets.LangBackground ?? GameAssets.SettingsBackground;
            if (bgSpr != null) { cardBg.sprite = bgSpr; cardBg.color = Color.white; cardBg.type = Image.Type.Simple; }
            else cardBg.color = new Color(0.07f, 0.07f, 0.14f, 0.99f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(600f, 920f);

            // Title banner (globe + localized "Language")
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(card.transform, false);
            var title = titleGO.AddComponent<Text>();
            title.font = font; title.fontSize = 46; title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter; title.color = Color.white;
            title.supportRichText = false; title.raycastTarget = false;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            var titleOl = titleGO.AddComponent<Outline>();
            titleOl.effectColor = new Color(0f, 0f, 0f, 0.9f);
            titleOl.effectDistance = new Vector2(2f, -2f);
            LocalizedText.Bind(title, "key_language");
            var trt = titleGO.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -36f);
            trt.sizeDelta = new Vector2(0f, 80f);

            // Scrollable list of languages
            var scrollGO = new GameObject("Scroll", typeof(RectTransform));
            scrollGO.transform.SetParent(card.transform, false);
            var srt = scrollGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.08f, 0.05f); srt.anchorMax = new Vector2(0.92f, 1f);
            srt.offsetMin = new Vector2(0f, 24f); srt.offsetMax = new Vector2(0f, -132f);

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGO.transform, false);
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.001f);
            Stretch(viewport.GetComponent<RectTransform>());
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            scroll.content = crt;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f; vlg.padding = new RectOffset(2, 2, 2, 6);
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            var fit = content.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < Languages.Length; i++)
            {
                int idx = i;
                var rowGo = new GameObject($"Lang_{i}");
                rowGo.transform.SetParent(content.transform, false);
                var rowCatcher = rowGo.AddComponent<Image>();
                var rowSpr = GameAssets.LangRowUnselected;
                if (rowSpr != null) { rowCatcher.sprite = rowSpr; rowCatcher.color = Color.white; rowCatcher.type = Image.Type.Sliced; }
                else rowCatcher.color = new Color(0.85f, 0.85f, 0.88f);
                rowGo.AddComponent<Button>().onClick.AddListener(() => SelectLanguage(idx));
                var rle = rowGo.AddComponent<LayoutElement>();
                rle.preferredHeight = 78f; rle.minHeight = 78f;
                var rowHlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                rowHlg.spacing = 14f; rowHlg.childAlignment = TextAnchor.MiddleLeft;
                rowHlg.padding = new RectOffset(12, 12, 4, 4);
                rowHlg.childControlWidth = true; rowHlg.childForceExpandWidth = false;
                rowHlg.childControlHeight = true; rowHlg.childForceExpandHeight = true;

                var flagGo = new GameObject("Flag");
                flagGo.transform.SetParent(rowGo.transform, false);
                var flagImg = flagGo.AddComponent<Image>();
                flagImg.raycastTarget = false;
                GameAssets.Apply(flagImg, GameAssets.LangFlag(LangCodes[i]), preserveAspect: true);
                var flagLe = flagGo.AddComponent<LayoutElement>();
                flagLe.preferredWidth = 92f; flagLe.flexibleWidth = 0f;

                var lblGO = new GameObject("Name");
                lblGO.transform.SetParent(rowGo.transform, false);
                var lbl = lblGO.AddComponent<Text>();
                lbl.text = Languages[i]; lbl.font = font; lbl.fontSize = 32;
                lbl.fontStyle = FontStyle.Bold; lbl.alignment = TextAnchor.MiddleLeft;
                lbl.color = new Color(0.173f, 0.173f, 0.173f);
                lbl.supportRichText = false; lbl.raycastTarget = false;
                lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                lbl.verticalOverflow = VerticalWrapMode.Overflow;
                var lblLe = lblGO.AddComponent<LayoutElement>(); lblLe.flexibleWidth = 1f;
            }
        }

        private void SelectLanguage(int idx)
        {
            AudioMgr.Instance?.PlaySFX("button_tap");
            idx = Mathf.Clamp(idx, 0, LangCodes.Length - 1);

            // Persist (mirrors SettingsPanel keys) and force-reload the localization table.
            PlayerPrefs.SetInt(LangIdxKey, idx);
            PlayerPrefs.SetString(LangNameKey, Languages[idx]);
            PlayerPrefs.Save();
            // SetLanguage loads the table, persists current_language, and live-refreshes all LocalizedText.
            LocalizationManager.Instance?.SetLanguage(LangCodes[idx]);
            DevLog($"language selected = {LangCodes[idx]}; localization reloaded");

            Close();
        }

        private void CommitDefaultAndClose()
        {
            LocalizationManager.Instance?.SetLanguage(LocalizationManager.DefaultLanguage);
            Close();
        }

        private void Close()
        {
            IsBlocking = false;
            var cb = _onChosen; _onChosen = null;
            Destroy(gameObject);
            cb?.Invoke();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DevLog(string msg) => Debug.Log($"[FirstRunLanguagePopup] {msg}");
    }
}
