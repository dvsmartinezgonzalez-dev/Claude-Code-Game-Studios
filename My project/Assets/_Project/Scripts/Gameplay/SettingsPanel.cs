using System;
using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Modal settings overlay. Call Initialize() once after the parent Canvas is created,
    /// then Toggle() to show/hide. Stores music and SFX prefs in PlayerPrefs.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private const string MusicKey = "bs.music_on";
        private const string SfxKey   = "bs.sfx_on";

        private GameObject _overlay;
        private GameObject _langPanel;
        private NoAdsPopup _noAdsPopup;
        private Font       _font;
        private Action     _onGoShop;
        private Action     _onGoLevels;

        private static readonly string[] Languages =
            { "English", "Español", "Français", "Deutsch", "Italiano", "Português" };

        /// <summary>
        /// Fired before the overlay's active state changes.
        /// True = panel is opening; false = panel is closing.
        /// GP-01: SortMechanic subscribes to block input while settings is open.
        /// </summary>
        public static event Action<bool> OnGamePaused;

        /// <summary>True when the settings overlay is currently visible.</summary>
        public bool IsOpen => _overlay != null && _overlay.activeSelf;

        public void Initialize(Font font, Transform canvasRoot,
                               Action onGoShop = null, Action onGoLevels = null)
        {
            _onGoShop   = onGoShop;
            _onGoLevels = onGoLevels;
            BuildOverlay(font, canvasRoot);
            _overlay.SetActive(false);
        }

        /// <summary>
        /// Toggles the settings overlay. Fires <see cref="OnGamePaused"/> before changing
        /// visibility so subscribers can react before the panel animates open or closed.
        /// </summary>
        public void Toggle()
        {
            if (_overlay == null) return;
            bool opening = !_overlay.activeSelf;
            OnGamePaused?.Invoke(opening);
            _overlay.SetActive(opening);
        }

        private void BuildOverlay(Font font, Transform canvasRoot)
        {
            _font = font;

            // Full-screen dim background
            _overlay = new GameObject("SettingsOverlay");
            _overlay.transform.SetParent(canvasRoot, false);

            var dimImg = _overlay.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.7f);
            Stretch(_overlay.GetComponent<RectTransform>());

            // Block raycasts so dim background is tappable (closes panel)
            var btn = _overlay.AddComponent<Button>();
            btn.onClick.AddListener(() => Toggle());

            // Card
            var card = new GameObject("Card");
            card.transform.SetParent(_overlay.transform, false);
            var cardBg = card.AddComponent<Image>();
            var popupSpr = GameAssets.MenuPopup;
            if (popupSpr != null) { cardBg.sprite = popupSpr; cardBg.color = Color.white; cardBg.type = Image.Type.Simple; }
            else cardBg.color = new Color(0.07f, 0.07f, 0.14f, 0.98f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRt.pivot            = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = new Vector2(520f, 820f);

            // Consume taps on the card so they don't fall through to the dim closer.
            var cardBtn = card.AddComponent<Button>();
            var cbc = cardBtn.colors;
            cbc.normalColor = cbc.highlightedColor = cbc.pressedColor = cbc.selectedColor = Color.white;
            cardBtn.colors = cbc;

            float y = 300f;

            // Title
            AddLabel(card, "Title", "Settings", font, 50, TextAnchor.MiddleCenter, Color.white, y);

            // Close button — exit_button.png, top-right corner of card.
            var closeBtn = CreateButton(card, "CloseBtn", "", font, 36,
                                        new Color(0.290f, 0.565f, 0.851f, 1f),
                                        () => Toggle());
            GameAssets.Apply(closeBtn.GetComponent<Image>(), GameAssets.BtnExit, preserveAspect: true);
            var cr = closeBtn.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(1f, 1f); cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot     = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-12f, -12f);
            cr.sizeDelta        = new Vector2(68f, 68f);

            y -= 90f;

            // Music toggle
            AddToggleRow(card, font, "Music", MusicKey, y, newVal =>
            {
                PlayerPrefs.SetInt(MusicKey, newVal ? 1 : 0);
                PlayerPrefs.Save();
                AudioMgr.Instance?.SetMusicEnabled(newVal);
            });
            y -= 78f;

            // SFX toggle
            AddToggleRow(card, font, "SFX", SfxKey, y, newVal =>
            {
                PlayerPrefs.SetInt(SfxKey, newVal ? 1 : 0);
                PlayerPrefs.Save();
                AudioMgr.Instance?.SetSFXEnabled(newVal);
            });
            y -= 84f;

            // Language button — general_button.png; opens a scrollable language list.
            AddImageButton(card, "Language", GameAssets.MenuButton, null, y, 380f, 64f,
                           () => { if (_langPanel != null) _langPanel.SetActive(!_langPanel.activeSelf); });
            y -= 80f;

            // Go to Shop / Go to Levels — in-game navigation (header redesign). Only shown
            // when wired (gameplay HUD); the main-menu settings popup leaves these unset.
            if (_onGoShop != null)
            {
                AddImageButton(card, "Go to Shop", GameAssets.MenuButton, null, y, 380f, 64f,
                               () => _onGoShop.Invoke());
                y -= 72f;
            }
            if (_onGoLevels != null)
            {
                AddImageButton(card, "Go to Levels", GameAssets.MenuButton, null, y, 380f, 64f,
                               () => _onGoLevels.Invoke());
                y -= 80f;
            }

            // No Ads button — general_button.png + no_ads icon + label; opens the popup.
            AddImageButton(card, "No Ads", GameAssets.MenuButton, GameAssets.MenuNoAds, y, 380f, 70f,
                           () => _noAdsPopup?.Open());
            y -= 80f;

            // Rate button — SET-01: open store page instead of no-op
            AddActionButton(card, font, "Rate the Game ★", y, () =>
            {
#if UNITY_IOS
                Application.OpenURL("itms-apps://itunes.apple.com/app/id0000000000");
#else
                Application.OpenURL("https://play.google.com/store/apps/details?id=com.dvsstudio.boltsort");
#endif
            });
            y -= 64f;

            // Privacy Policy button
            AddActionButton(card, font, "Privacy Policy", y,
                () => Application.OpenURL("https://dvsstudio.github.io/boltsort/privacy"));
            y -= 56f;

            // Version
            AddLabel(card, "Version", $"v{Application.version}", font, 24,
                     TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.7f, 1f), y);

            // Scrollable language list (hidden until Language is tapped)
            BuildLanguageList(card, font);

            // No Ads popup instance for this scene
            var noAdsHost = new GameObject("SettingsNoAdsHost");
            noAdsHost.transform.SetParent(canvasRoot, false);
            _noAdsPopup = noAdsHost.AddComponent<NoAdsPopup>();
            _noAdsPopup.Initialize(font, canvasRoot);
        }

        // Scrollable list of selectable language placeholders. Selection is persisted
        // to PlayerPrefs for future localization but does not change strings yet.
        private void BuildLanguageList(GameObject card, Font font)
        {
            _langPanel = new GameObject("LanguagePanel");
            _langPanel.transform.SetParent(card.transform, false);
            _langPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.09f, 0.99f);
            // Tapping the panel background (outside the rows) closes it.
            _langPanel.AddComponent<Button>().onClick.AddListener(() => _langPanel.SetActive(false));
            var pr = _langPanel.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = new Vector2(0f, 0f);
            pr.sizeDelta = new Vector2(420f, 420f);

            var hdr = AddLabel(_langPanel, "LangTitle", "Language", font, 36,
                               TextAnchor.MiddleCenter, Color.white, 175f);
            hdr.raycastTarget = false;

            // ScrollRect + viewport + content
            var scrollGO = new GameObject("Scroll");
            scrollGO.transform.SetParent(_langPanel.transform, false);
            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 24f;
            var srt = scrollGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(16f, 16f); srt.offsetMax = new Vector2(-16f, -56f);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGO.transform, false);
            var vpImg = viewport.AddComponent<Image>(); vpImg.color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport.GetComponent<RectTransform>());
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f); crt.offsetMin = crt.offsetMax = Vector2.zero;
            scroll.content = crt;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            var fit = content.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            int selected = PlayerPrefs.GetInt("bs.language", 0);
            for (int i = 0; i < Languages.Length; i++)
            {
                int idx = i;
                var rowGo = new GameObject($"Lang_{i}");
                rowGo.transform.SetParent(content.transform, false);
                var rowImg = rowGo.AddComponent<Image>();
                rowImg.color = i == selected ? new Color(0.20f, 0.55f, 0.30f, 1f)
                                             : new Color(0.15f, 0.15f, 0.28f, 1f);
                var le = rowGo.AddComponent<LayoutElement>(); le.minHeight = 56f;
                var rowBtn = rowGo.AddComponent<Button>();
                rowBtn.onClick.AddListener(() =>
                {
                    AudioMgr.Instance?.PlaySFX("button_tap");
                    PlayerPrefs.SetInt("bs.language", idx); PlayerPrefs.Save();
                    _langPanel.SetActive(false);
                });
                var lbl = AddLabel(rowGo, "Label", Languages[i], font, 30,
                                   TextAnchor.MiddleCenter, Color.white, 0f);
                lbl.raycastTarget = false;
                var lrt = lbl.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.anchoredPosition = Vector2.zero; lrt.sizeDelta = Vector2.zero;
            }

            _langPanel.SetActive(false);
        }

        // general_button.png-backed button with optional leading icon + text label.
        private void AddImageButton(GameObject parent, string label, Sprite bg, Sprite icon,
                                    float y, float w, float h, Action onClick)
        {
            var go = new GameObject($"{label}Button");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            if (bg != null) GameAssets.Apply(img, bg, preserveAspect: true);
            else img.color = new Color(0.20f, 0.40f, 0.65f, 1f);
            var b = go.AddComponent<Button>();
            b.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            b.onClick.AddListener(() => onClick?.Invoke());
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(w, h);

            if (icon != null)
            {
                var icGo = new GameObject("Icon");
                icGo.transform.SetParent(go.transform, false);
                var icImg = icGo.AddComponent<Image>();
                GameAssets.Apply(icImg, icon, preserveAspect: true);
                icImg.raycastTarget = false;
                var icrt = icGo.GetComponent<RectTransform>();
                icrt.anchorMin = new Vector2(0f, 0.5f); icrt.anchorMax = new Vector2(0f, 0.5f);
                icrt.pivot = new Vector2(0f, 0.5f);
                icrt.anchoredPosition = new Vector2(18f, 0f);
                icrt.sizeDelta = new Vector2(h * 0.7f, h * 0.7f);
            }

            var t = AddLabel(go, "Label", label, _font, 32, TextAnchor.MiddleCenter, Color.white, 0f);
            t.raycastTarget = false;
            var lr = t.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.anchoredPosition = Vector2.zero; lr.sizeDelta = Vector2.zero;
        }

        private static Text AddLabel(GameObject parent, string name, string text, Font font,
                                     int size, TextAnchor anchor, Color color, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = anchor;
            t.color = color; t.supportRichText = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta        = new Vector2(0f, 50f);
            return t;
        }

        private static void AddToggleRow(GameObject parent, Font font, string label,
                                         string prefKey, float y, Action<bool> onChange)
        {
            bool current = PlayerPrefs.GetInt(prefKey, 1) == 1;

            // Label
            var lgo = new GameObject($"{label}Label");
            lgo.transform.SetParent(parent.transform, false);
            var lt = lgo.AddComponent<Text>();
            lt.text = label; lt.font = font; lt.fontSize = 36;
            lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleLeft;
            lt.color = Color.white; lt.supportRichText = false;
            var lr = lgo.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 0.5f); lr.anchorMax = new Vector2(0.6f, 0.5f);
            lr.pivot     = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(24f, y);
            lr.sizeDelta        = new Vector2(0f, 50f);

            // Toggle button (ON/OFF)
            var tgo = new GameObject($"{label}Toggle");
            tgo.transform.SetParent(parent.transform, false);
            var tImg = tgo.AddComponent<Image>();
            tImg.color = current ? new Color(0.20f, 0.65f, 0.30f) : new Color(0.50f, 0.20f, 0.20f);
            var tr = tgo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(1f, 0.5f); tr.anchorMax = new Vector2(1f, 0.5f);
            tr.pivot     = new Vector2(1f, 0.5f);
            tr.anchoredPosition = new Vector2(-24f, y);
            tr.sizeDelta        = new Vector2(110f, 50f);

            var tLabel = new GameObject("Label");
            tLabel.transform.SetParent(tgo.transform, false);
            var tlt = tLabel.AddComponent<Text>();
            tlt.text = current ? "ON" : "OFF"; tlt.font = font; tlt.fontSize = 30;
            tlt.fontStyle = FontStyle.Bold; tlt.alignment = TextAnchor.MiddleCenter;
            tlt.color = Color.white; tlt.supportRichText = false;
            var tlr = tLabel.GetComponent<RectTransform>();
            tlr.anchorMin = Vector2.zero; tlr.anchorMax = Vector2.one;
            tlr.offsetMin = tlr.offsetMax = Vector2.zero;

            var tBtn = tgo.AddComponent<Button>();
            tBtn.onClick.AddListener(() =>
            {
                bool next = PlayerPrefs.GetInt(prefKey, 1) != 1;
                onChange(next);
                tImg.color = next ? new Color(0.20f, 0.65f, 0.30f) : new Color(0.50f, 0.20f, 0.20f);
                tlt.text   = next ? "ON" : "OFF";
            });
        }

        private static void AddActionButton(GameObject parent, Font font, string label,
                                            float y, Action onClick)
        {
            var btn = CreateButton(parent, label, label, font, 30,
                                   new Color(0.15f, 0.15f, 0.28f, 1f), onClick);
            var r = btn.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f); r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot     = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta        = new Vector2(360f, 54f);
        }

        private static GameObject CreateButton(GameObject parent, string name, string label,
                                               Font font, int size, Color bgColor, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() => onClick?.Invoke());

            var lgo = new GameObject("Label");
            lgo.transform.SetParent(go.transform, false);
            var t = lgo.AddComponent<Text>();
            t.text = label; t.font = font; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.supportRichText = false;
            var lr = lgo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
