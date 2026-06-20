using System;
using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Modal settings overlay. Call Initialize() once after the parent Canvas is created,
    /// then Toggle() to show/hide. Music and SFX are 0–8 volume steps persisted to
    /// PlayerPrefs ("MusicVolume" / "SFXVolume"); language to "Language" + index "bs.language".
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private const string MusicVolKey = "MusicVolume";
        private const string SfxVolKey   = "SFXVolume";
        private const string LangIdxKey  = "bs.language";
        private const string LangNameKey = "Language";
        private const int    MaxStep     = 8;
        private const int    BarCount    = 8;

        private GameObject _overlay;
        private GameObject _langPanel;
        private NoAdsPopup _noAdsPopup;
        private Font       _font;
        private Action     _onGoShop;
        private Action     _onGoLevels;
        private Text       _langButtonLabel;

        private static readonly string[] Languages =
            { "English", "Spanish", "French", "German", "Italian",
              "Portuguese", "Japanese", "Korean", "Chinese" };

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
            // Never let a Settings build failure interrupt the host screen's UI
            // construction (MainMenu / LevelSelect / Gameplay), and always leave the
            // overlay hidden so it can never appear automatically.
            try
            {
                BuildOverlay(font, canvasRoot);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsPanel] Build failed; overlay disabled. {e}");
            }
            finally
            {
                if (_overlay != null) _overlay.SetActive(false);
            }
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

            // Tap dim background to close
            _overlay.AddComponent<Button>().onClick.AddListener(Toggle);

            // Card — background_large.png (aspect 600x805)
            var card = new GameObject("Card");
            card.transform.SetParent(_overlay.transform, false);
            var cardBg = card.AddComponent<Image>();
            var bgSpr = GameAssets.SettingsBackground;
            if (bgSpr != null) { cardBg.sprite = bgSpr; cardBg.color = Color.white; cardBg.type = Image.Type.Simple; }
            else cardBg.color = new Color(0.07f, 0.07f, 0.14f, 0.98f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = new Vector2(560f, 751f);

            // Consume taps on the card so they don't fall through to the dim closer.
            var cardBtn = card.AddComponent<Button>();
            var cbc = cardBtn.colors;
            cbc.normalColor = cbc.highlightedColor = cbc.pressedColor = cbc.selectedColor = Color.white;
            cardBtn.colors = cbc;

            // Title "SETTINGS" on the blue banner (top-centre)
            var title = AddLabel(card, "Title", "SETTINGS", font, 34, TextAnchor.MiddleCenter, Color.white, 0f);
            var trt = title.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 1f); trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -14f);
            trt.sizeDelta = new Vector2(220f, 56f);

            // Close button — exit_button.png, top-right corner of card.
            var closeBtn = CreateButton(card, "CloseBtn", "", font, 36,
                                        new Color(0.290f, 0.565f, 0.851f, 1f), Toggle);
            GameAssets.Apply(closeBtn.GetComponent<Image>(), GameAssets.BtnExit, preserveAspect: true);
            var cr = closeBtn.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-10f, -10f);
            cr.sizeDelta        = new Vector2(64f, 64f);

            // ── Content area (vertical layout, inset to the cream region) ──
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(card.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.12f, 0.06f);
            crt.anchorMax = new Vector2(0.88f, 0.82f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(0, 0, 4, 4);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;  vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            // Music + SFX volume rows
            BuildVolumeRow(content, GameAssets.SettingsSoundOn, GameAssets.SettingsSoundOff,
                           MusicVolKey, step => AudioMgr.Instance?.SetMusicVolume(step));
            BuildVolumeRow(content, GameAssets.SettingsSfxOn, GameAssets.SettingsSfxOff,
                           SfxVolKey, step => AudioMgr.Instance?.SetSFXVolume(step));

            // Language row
            BuildLanguageRow(content, font);

            // In-game navigation (gameplay HUD only)
            if (_onGoShop != null)
                BuildWideButton(content, GameAssets.MenuButton, null, "Go to Shop", 58f, Color.white,
                                () => _onGoShop.Invoke());
            if (_onGoLevels != null)
                BuildWideButton(content, GameAssets.MenuButton, null, "Go to Levels", 58f, Color.white,
                                () => _onGoLevels.Invoke());

            // No Ads row — no_ads icon + white_button
            BuildNoAdsRow(content, font);

            // Rate / Privacy — full-width buttons (rate_game_privacy_button.png)
            BuildWideButton(content, GameAssets.SettingsWideButton, GameAssets.SettingsStar,
                            "Rate the game", 58f, Color.white, () =>
            {
#if UNITY_IOS
                Application.OpenURL("itms-apps://itunes.apple.com/app/id0000000000");
#else
                Application.OpenURL("https://play.google.com/store/apps/details?id=com.dvsstudio.boltsort");
#endif
            });
            BuildWideButton(content, GameAssets.SettingsWideButton, GameAssets.SettingsShield,
                            "Privacy Policy", 58f, Color.white,
                            () => Application.OpenURL("https://dvsstudio.github.io/boltsort/privacy"));

            // Scrollable language list (hidden until Language is tapped)
            BuildLanguageList(card, font);

            // No Ads popup instance for this scene
            var noAdsHost = new GameObject("SettingsNoAdsHost");
            noAdsHost.transform.SetParent(canvasRoot, false);
            _noAdsPopup = noAdsHost.AddComponent<NoAdsPopup>();
            _noAdsPopup.Initialize(font, canvasRoot);
        }

        // ── Volume row: [icon] [-] [8 bars] [+] ──────────────────────────────────────

        private sealed class VolumeRow
        {
            public Image   Icon;
            public Sprite  IconOn, IconOff;
            public Image[] Bars;
            public int     Step;

            public void Refresh()
            {
                for (int i = 0; i < Bars.Length; i++)
                    GameAssets.Apply(Bars[i], i < Step ? GameAssets.SettingsBarOn : GameAssets.SettingsBarOff);
                GameAssets.Apply(Icon, Step > 0 ? IconOn : IconOff, preserveAspect: true);
            }
        }

        private void BuildVolumeRow(GameObject parent, Sprite iconOn, Sprite iconOff,
                                    string prefKey, Action<int> apply)
        {
            var state = new VolumeRow { IconOn = iconOn, IconOff = iconOff, Bars = new Image[BarCount] };
            state.Step = Mathf.Clamp(PlayerPrefs.GetInt(prefKey, MaxStep), 0, MaxStep);

            var row = new GameObject("VolumeRow", typeof(RectTransform));
            row.transform.SetParent(parent.transform, false);
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 66f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = true;

            // Icon
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            state.Icon = iconGo.AddComponent<Image>();
            state.Icon.raycastTarget = false;
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 56f; iconLe.flexibleWidth = 0f;

            // Minus button
            CreateRowButton(row, "Minus", GameAssets.SettingsMinus, 52f, () =>
            {
                if (state.Step <= 0) return;
                state.Step--; PersistAndApply(prefKey, state.Step, apply); state.Refresh();
            });

            // Bars container (takes remaining width)
            var barsGo = new GameObject("Bars", typeof(RectTransform));
            barsGo.transform.SetParent(row.transform, false);
            var barsLe = barsGo.AddComponent<LayoutElement>(); barsLe.flexibleWidth = 1f;
            var barsRt = barsGo.GetComponent<RectTransform>();
            for (int i = 0; i < BarCount; i++)
            {
                var bar = new GameObject($"Bar_{i}");
                bar.transform.SetParent(barsRt, false);
                var img = bar.AddComponent<Image>();
                img.raycastTarget = false; img.type = Image.Type.Simple; img.preserveAspect = false;
                state.Bars[i] = img;
                float heightFrac = 0.4f + 0.6f * (i / (float)(BarCount - 1));
                float xc = (i + 0.5f) / BarCount;
                var brt = bar.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(xc, 0f);
                brt.anchorMax = new Vector2(xc, heightFrac);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.sizeDelta = new Vector2(14f, 0f);
                brt.anchoredPosition = Vector2.zero;
            }

            // Plus button
            CreateRowButton(row, "Plus", GameAssets.SettingsPlus, 52f, () =>
            {
                if (state.Step >= MaxStep) return;
                state.Step++; PersistAndApply(prefKey, state.Step, apply); state.Refresh();
            });

            state.Refresh();
        }

        private static void PersistAndApply(string prefKey, int step, Action<int> apply)
        {
            PlayerPrefs.SetInt(prefKey, step);
            PlayerPrefs.Save();
            AudioMgr.Instance?.PlaySFX("button_tap");
            apply?.Invoke(step);
        }

        private static void CreateRowButton(GameObject row, string name, Sprite sprite,
                                            float width, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(row.transform, false);
            var img = go.AddComponent<Image>();
            if (sprite != null) GameAssets.Apply(img, sprite, preserveAspect: true);
            else img.color = new Color(0.20f, 0.40f, 0.65f, 1f);
            go.AddComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.flexibleWidth = 0f;
        }

        // ── Language row ─────────────────────────────────────────────────────────────

        private void BuildLanguageRow(GameObject parent, Font font)
        {
            var row = new GameObject("LanguageRow", typeof(RectTransform));
            row.transform.SetParent(parent.transform, false);
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 66f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = true;

            var lbl = AddLabel(row, "Label", "Language", font, 30, TextAnchor.MiddleLeft, new Color(0.30f, 0.25f, 0.10f), 0f);
            lbl.raycastTarget = false;
            var lblLe = lbl.gameObject.AddComponent<LayoutElement>(); lblLe.flexibleWidth = 1f;

            int idx = Mathf.Clamp(PlayerPrefs.GetInt(LangIdxKey, 0), 0, Languages.Length - 1);
            var btnGo = new GameObject("LanguageButton");
            btnGo.transform.SetParent(row.transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            if (GameAssets.SettingsLanguageBtn != null) GameAssets.Apply(btnImg, GameAssets.SettingsLanguageBtn, preserveAspect: true);
            else btnImg.color = new Color(0.20f, 0.40f, 0.65f, 1f);
            btnGo.AddComponent<Button>().onClick.AddListener(() =>
            {
                AudioMgr.Instance?.PlaySFX("button_tap");
                if (_langPanel != null) _langPanel.SetActive(!_langPanel.activeSelf);
            });
            var btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.preferredWidth = 180f; btnLe.preferredHeight = 60f; btnLe.flexibleWidth = 0f;

            _langButtonLabel = AddLabel(btnGo, "Label", Languages[idx].ToUpper(), font, 26,
                                        TextAnchor.MiddleCenter, Color.white, 0f);
            _langButtonLabel.raycastTarget = false;
            var llr = _langButtonLabel.GetComponent<RectTransform>();
            llr.anchorMin = Vector2.zero; llr.anchorMax = Vector2.one; llr.offsetMin = llr.offsetMax = Vector2.zero;
        }

        // ── No Ads row ───────────────────────────────────────────────────────────────

        private void BuildNoAdsRow(GameObject parent, Font font)
        {
            var row = new GameObject("NoAdsRow", typeof(RectTransform));
            row.transform.SetParent(parent.transform, false);
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 72f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = true;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            GameAssets.Apply(icon, GameAssets.MenuNoAds, preserveAspect: true);
            var iconLe = iconGo.AddComponent<LayoutElement>(); iconLe.preferredWidth = 72f; iconLe.flexibleWidth = 0f;

            var btnGo = new GameObject("NoAdsButton");
            btnGo.transform.SetParent(row.transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            if (GameAssets.WhiteButton != null) { btnImg.sprite = GameAssets.WhiteButton; btnImg.color = Color.white; }
            else btnImg.color = Color.white;
            btnGo.AddComponent<Button>().onClick.AddListener(() =>
            {
                AudioMgr.Instance?.PlaySFX("button_tap");
                _noAdsPopup?.Open();
            });
            var btnLe = btnGo.AddComponent<LayoutElement>(); btnLe.flexibleWidth = 1f;

            var t = AddLabel(btnGo, "Label", "No Ads", font, 30, TextAnchor.MiddleCenter, new Color(0.20f, 0.18f, 0.10f), 0f);
            t.raycastTarget = false;
            var lr = t.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        }

        // ── Generic full-width button (bg sprite + optional leading icon + centred text) ──

        private void BuildWideButton(GameObject parent, Sprite bg, Sprite icon, string label,
                                     float height, Color textColor, Action onClick)
        {
            var go = new GameObject($"{label}Button");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            if (bg != null) { img.sprite = bg; img.color = Color.white; img.type = Image.Type.Simple; }
            else img.color = new Color(0.20f, 0.40f, 0.65f, 1f);
            var b = go.AddComponent<Button>();
            b.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            b.onClick.AddListener(() => onClick?.Invoke());
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = height;

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
                icrt.anchoredPosition = new Vector2(28f, 0f);
                icrt.sizeDelta = new Vector2(height * 0.6f, height * 0.6f);
            }

            var t = AddLabel(go, "Label", label, _font, 30, TextAnchor.MiddleCenter, textColor, 0f);
            t.raycastTarget = false;
            var lr = t.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        }

        // ── Scrollable language list (hidden until Language is tapped) ────────────────

        private void BuildLanguageList(GameObject card, Font font)
        {
            _langPanel = new GameObject("LanguagePanel");
            _langPanel.transform.SetParent(card.transform, false);
            _langPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.09f, 0.99f);
            _langPanel.AddComponent<Button>().onClick.AddListener(() => _langPanel.SetActive(false));
            var pr = _langPanel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = Vector2.zero;
            pr.sizeDelta = new Vector2(420f, 460f);

            var hdr = AddLabel(_langPanel, "LangTitle", "Language", font, 36,
                               TextAnchor.MiddleCenter, Color.white, 195f);
            hdr.raycastTarget = false;

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

            int selected = PlayerPrefs.GetInt(LangIdxKey, 0);
            for (int i = 0; i < Languages.Length; i++)
            {
                int idx = i;
                var rowGo = new GameObject($"Lang_{i}");
                rowGo.transform.SetParent(content.transform, false);
                var rowImg = rowGo.AddComponent<Image>();
                rowImg.color = i == selected ? new Color(0.20f, 0.55f, 0.30f, 1f)
                                             : new Color(0.15f, 0.15f, 0.28f, 1f);
                var le = rowGo.AddComponent<LayoutElement>(); le.minHeight = 56f;
                rowGo.AddComponent<Button>().onClick.AddListener(() =>
                {
                    AudioMgr.Instance?.PlaySFX("button_tap");
                    PlayerPrefs.SetInt(LangIdxKey, idx);
                    PlayerPrefs.SetString(LangNameKey, Languages[idx]);
                    PlayerPrefs.Save();
                    if (_langButtonLabel != null) _langButtonLabel.text = Languages[idx].ToUpper();
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

        // ── Shared helpers ───────────────────────────────────────────────────────────

        private static Text AddLabel(GameObject parent, string name, string text, Font font,
                                     int size, TextAnchor anchor, Color color, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = anchor;
            t.color = color; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta        = new Vector2(0f, 50f);
            return t;
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
