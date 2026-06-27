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
        private Image[]    _langRowImages;   // per-row pill backgrounds — sprite-swapped on selection
        private Text[]     _langRowLabels;   // per-row name labels — recoloured on selection

        // Native language names shown in the selector, aligned 1:1 with LangCodes.
        private static readonly string[] Languages =
            { "English", "Español", "Português", "Français", "Deutsch",
              "Italiano", "Русский", "中文", "日本語", "हिन्दी" };
        private static readonly string[] LangCodes =
            { "en", "es", "pt", "fr", "de", "it", "ru", "zh", "ja", "hi" };

        private static int CurrentLangIndex()
        {
            string cur = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.CurrentLanguage : LocalizationManager.DefaultLanguage;
            for (int i = 0; i < LangCodes.Length; i++)
                if (LangCodes[i] == cur) return i;
            return 0;
        }

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
            LocalizedText.Bind(title, "key_settings");
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
                LocalizedText.Bind(BuildWideButton(content, GameAssets.MenuButton, null, "Go to Shop", 58f, Color.white,
                                () => _onGoShop.Invoke()), "key_go_shop");
            if (_onGoLevels != null)
                LocalizedText.Bind(BuildWideButton(content, GameAssets.MenuButton, null, "Go to Levels", 58f, Color.white,
                                () => _onGoLevels.Invoke()), "key_go_levels");

            // No Ads row — no_ads icon + white_button
            BuildNoAdsRow(content, font);

            // Rate / Privacy — full-width buttons (rate_game_privacy_button.png)
            LocalizedText.Bind(BuildWideButton(content, GameAssets.SettingsWideButton, GameAssets.SettingsStar,
                            "Rate the game", 58f, Color.white, () =>
            {
#if UNITY_IOS
                Application.OpenURL("itms-apps://itunes.apple.com/app/id0000000000");
#else
                Application.OpenURL("https://play.google.com/store/apps/details?id=com.dvsstudio.boltsort");
#endif
            }), "key_rate");
            LocalizedText.Bind(BuildWideButton(content, GameAssets.SettingsWideButton, GameAssets.SettingsShield,
                            "Privacy Policy", 58f, Color.white,
                            () => Application.OpenURL("https://dvsstudio.github.io/boltsort/privacy")), "key_privacy");

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
            LocalizedText.Bind(lbl, "key_language");
            var lblLe = lbl.gameObject.AddComponent<LayoutElement>(); lblLe.flexibleWidth = 1f;

            int idx = CurrentLangIndex();
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
            LocalizedText.Bind(t, "key_no_ads");
            var lr = t.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        }

        // ── Generic full-width button (bg sprite + optional leading icon + centred text) ──

        private Text BuildWideButton(GameObject parent, Sprite bg, Sprite icon, string label,
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
            return t;
        }

        // ── Scrollable language list (hidden until Language is tapped) ────────────────

        // Row height for language list entries (flag + pill button) and selection colours.
        private const float LangRowH = 78f;
        private static readonly Color LangTextSelected   = Color.white;
        private static readonly Color LangTextUnselected = new Color(0.173f, 0.173f, 0.173f); // #2C2C2C

        private void BuildLanguageList(GameObject card, Font font)
        {
            const float panelW = 548f, panelH = 735f, bannerH = 132f, bgScale = 1.4f;

            // ── Panel frame (transparent container for banner, exit, and the scroll list) ─
            _langPanel = new GameObject("LanguagePanel", typeof(RectTransform));
            _langPanel.transform.SetParent(card.transform, false);
            var pr = _langPanel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = Vector2.zero;
            pr.sizeDelta = new Vector2(panelW, panelH);

            // Background art — scaled +40% (bgScale) so it visually covers the Settings card
            // behind the popup. Drawn first so it sits behind the banner/exit/scroll content.
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(_langPanel.transform, false);
            var panelImg = bgGO.AddComponent<Image>();
            var bgSpr = GameAssets.LangBackground;
            if (bgSpr != null) { panelImg.sprite = bgSpr; panelImg.color = Color.white; panelImg.type = Image.Type.Simple; }
            else               { panelImg.color = new Color(0.06f, 0.06f, 0.13f, 0.98f); }
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(panelW * bgScale, panelH * bgScale);

            // ── Top banner: world_background art + globe icon + title ───────────────────
            var bannerGO = new GameObject("TopBanner");
            bannerGO.transform.SetParent(_langPanel.transform, false);
            var bannerImg = bannerGO.AddComponent<Image>();
            bannerImg.raycastTarget = false;
            GameAssets.Apply(bannerImg, GameAssets.LangWorldBg, preserveAspect: true);
            var brt = bannerGO.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot     = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -2f);
            brt.sizeDelta        = new Vector2(panelW - 24f, bannerH);

            // Globe icon, left of the title.
            var worldGO = new GameObject("WorldIcon");
            worldGO.transform.SetParent(bannerGO.transform, false);
            var worldImg = worldGO.AddComponent<Image>();
            worldImg.raycastTarget = false;
            GameAssets.Apply(worldImg, GameAssets.LangWorldIcon, preserveAspect: true);
            var wrt = worldGO.GetComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0f, 0.5f); wrt.anchorMax = new Vector2(0f, 0.5f);
            wrt.pivot     = new Vector2(0f, 0.5f);
            wrt.anchoredPosition = new Vector2(28f, 0f);
            wrt.sizeDelta        = new Vector2(86f, 86f);

            // Title — centred on the banner, GummyPop white with a black outline.
            var titleGO = new GameObject("TitleText");
            titleGO.transform.SetParent(bannerGO.transform, false);
            var title = titleGO.AddComponent<Text>();
            title.font = font; title.fontSize = 40; title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter; title.color = Color.white;
            title.supportRichText = false; title.raycastTarget = false;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow   = VerticalWrapMode.Overflow;
            var ttrt = titleGO.GetComponent<RectTransform>();
            ttrt.anchorMin = Vector2.zero; ttrt.anchorMax = Vector2.one;
            ttrt.offsetMin = new Vector2(120f, 0f); ttrt.offsetMax = new Vector2(-40f, 0f);
            var titleOl = titleGO.AddComponent<Outline>();
            titleOl.effectColor    = new Color(0f, 0f, 0f, 0.9f);
            titleOl.effectDistance = new Vector2(2f, -2f);
            LocalizedText.Bind(title, "key_language");

            // ── Exit button (top-right of the panel) ────────────────────────────────────
            var closeBtnGO = new GameObject("ExitButton");
            closeBtnGO.transform.SetParent(_langPanel.transform, false);
            var closeBtnImg = closeBtnGO.AddComponent<Image>();
            if (GameAssets.BtnExit != null) GameAssets.Apply(closeBtnImg, GameAssets.BtnExit, preserveAspect: true);
            else closeBtnImg.color = new Color(0.7f, 0.2f, 0.2f, 1f);
            closeBtnGO.AddComponent<Button>().onClick.AddListener(() =>
            {
                AudioMgr.Instance?.PlaySFX("button_tap");
                _langPanel.SetActive(false);
            });
            var cbrt = closeBtnGO.GetComponent<RectTransform>();
            cbrt.anchorMin = cbrt.anchorMax = cbrt.pivot = new Vector2(1f, 1f);
            cbrt.anchoredPosition = new Vector2(-20f, -20f);
            cbrt.sizeDelta        = new Vector2(58f, 58f);

            // ── Scroll view (below the banner) ──────────────────────────────────────────
            var scrollGO = new GameObject("Scroll", typeof(RectTransform));
            scrollGO.transform.SetParent(_langPanel.transform, false);
            var srt = scrollGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 1f);
            // Top margin clears the banner; bottom/side insets keep rows off the art edges.
            srt.offsetMin = new Vector2(28f, 26f);
            srt.offsetMax = new Vector2(-28f, -(bannerH + 6f));

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f; scroll.inertia = true;
            scroll.decelerationRate = 0.135f; scroll.scrollSensitivity = 30f;

            // Viewport — RectMask2D avoids stencil/alpha issues that Mask has with
            // transparent backgrounds, and is the preferred approach for scroll views.
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGO.transform, false);
            viewport.AddComponent<RectMask2D>();
            // A near-invisible Image lets the scroll rect receive pointer events in the viewport.
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.001f);
            Stretch(viewport.GetComponent<RectTransform>());
            scroll.viewport = viewport.GetComponent<RectTransform>();

            // Content — top-anchored, grows downward via ContentSizeFitter.
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            scroll.content = crt;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(2, 2, 2, 6);
            vlg.childControlWidth = true;  vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            var fit = content.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Language rows: [flag] [pill button + native name] ───────────────────────
            int selected = CurrentLangIndex();
            _langRowImages = new Image[Languages.Length];
            _langRowLabels = new Text[Languages.Length];

            for (int i = 0; i < Languages.Length; i++)
            {
                int idx = i;
                bool isSel = (i == selected);

                // Row root — transparent tap catcher spanning the whole row (large tap target).
                var rowGo = new GameObject($"Lang_{i}");
                rowGo.transform.SetParent(content.transform, false);
                var rowCatcher = rowGo.AddComponent<Image>();
                rowCatcher.color = new Color(0f, 0f, 0f, 0.001f);
                rowGo.AddComponent<Button>().onClick.AddListener(() => SelectLanguage(idx));
                var rle = rowGo.AddComponent<LayoutElement>();
                rle.preferredHeight = LangRowH; rle.minHeight = LangRowH;
                var rowHlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                rowHlg.spacing = 14f; rowHlg.childAlignment = TextAnchor.MiddleLeft;
                rowHlg.padding = new RectOffset(6, 6, 4, 4);
                rowHlg.childControlWidth = true;  rowHlg.childForceExpandWidth = false;
                rowHlg.childControlHeight = true; rowHlg.childForceExpandHeight = true;

                // Flag (left, fixed width, aspect-preserved).
                var flagGo = new GameObject("FlagImage");
                flagGo.transform.SetParent(rowGo.transform, false);
                var flagImg = flagGo.AddComponent<Image>();
                flagImg.raycastTarget = false;
                GameAssets.Apply(flagImg, GameAssets.LangFlag(LangCodeFor(i)), preserveAspect: true);
                var flagLe = flagGo.AddComponent<LayoutElement>();
                flagLe.preferredWidth = 92f; flagLe.flexibleWidth = 0f;

                // Pill button background (fills the remaining width).
                var pillGo = new GameObject("ButtonBg");
                pillGo.transform.SetParent(rowGo.transform, false);
                var pillImg = pillGo.AddComponent<Image>();
                pillImg.raycastTarget = false; pillImg.type = Image.Type.Sliced;
                var pillSpr = isSel ? GameAssets.LangRowSelected : GameAssets.LangRowUnselected;
                if (pillSpr != null) { pillImg.sprite = pillSpr; pillImg.color = Color.white; }
                else pillImg.color = isSel ? new Color(0.20f, 0.55f, 0.30f) : new Color(0.85f, 0.85f, 0.88f);
                var pillLe = pillGo.AddComponent<LayoutElement>(); pillLe.flexibleWidth = 1f;
                _langRowImages[i] = pillImg;

                // Native-name label, centred on the pill.
                var lblGO = new GameObject("ButtonText");
                lblGO.transform.SetParent(pillGo.transform, false);
                var lbl = lblGO.AddComponent<Text>();
                _langRowLabels[i] = lbl;
                lbl.text = Languages[i]; lbl.font = font; lbl.fontSize = 30;
                lbl.fontStyle = FontStyle.Bold; lbl.alignment = TextAnchor.MiddleCenter;
                lbl.color = isSel ? LangTextSelected : LangTextUnselected;
                lbl.supportRichText = false; lbl.raycastTarget = false;
                lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                lbl.verticalOverflow   = VerticalWrapMode.Overflow;
                var lrt = lblGO.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(16f, 0f); lrt.offsetMax = new Vector2(-16f, 0f);

                // Outline only on the selected row (alpha toggled in SelectLanguage).
                var ol = lblGO.AddComponent<Outline>();
                ol.effectColor    = new Color(0f, 0f, 0f, isSel ? 0.85f : 0f);
                ol.effectDistance = new Vector2(1.5f, -1.5f);
            }

            _langPanel.SetActive(false);
        }

        /// <summary>Maps the language-list index to its localization code.</summary>
        private static string LangCodeFor(int idx) => LangCodes[Mathf.Clamp(idx, 0, LangCodes.Length - 1)];

        /// <summary>Applies a language selection live: persists, switches the active table,
        /// updates row visuals and checkmarks, updates the collapsed button label, closes the panel.</summary>
        private void SelectLanguage(int idx)
        {
            AudioMgr.Instance?.PlaySFX("button_tap");
            PlayerPrefs.SetInt(LangIdxKey, idx);
            PlayerPrefs.SetString(LangNameKey, Languages[idx]);
            PlayerPrefs.Save();
            LocalizationManager.Instance?.SetLanguage(LangCodeFor(idx));
            if (_langButtonLabel != null) _langButtonLabel.text = Languages[idx].ToUpper();

            var selSpr   = GameAssets.LangRowSelected;
            var unselSpr = GameAssets.LangRowUnselected;
            for (int i = 0; i < Languages.Length; i++)
            {
                bool isSel = (i == idx);

                if (_langRowImages != null && i < _langRowImages.Length && _langRowImages[i] != null)
                {
                    var spr = isSel ? selSpr : unselSpr;
                    if (spr != null) { _langRowImages[i].sprite = spr; _langRowImages[i].color = Color.white; }
                    else _langRowImages[i].color = isSel ? new Color(0.20f, 0.55f, 0.30f) : new Color(0.85f, 0.85f, 0.88f);
                }

                if (_langRowLabels != null && i < _langRowLabels.Length && _langRowLabels[i] != null)
                {
                    _langRowLabels[i].color = isSel ? LangTextSelected : LangTextUnselected;
                    var ol = _langRowLabels[i].GetComponent<Outline>();
                    if (ol != null) ol.effectColor = new Color(0f, 0f, 0f, isSel ? 0.85f : 0f);
                }
            }

            if (_langPanel != null) _langPanel.SetActive(false);
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
