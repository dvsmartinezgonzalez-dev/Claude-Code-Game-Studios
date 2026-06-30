using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Procedurally builds the standalone Shop scene (redesigned): full-screen background,
    /// banner header with an exit button, a live coins/gems currency bar, a four-tab
    /// sprite-swap category selector (Tubes / Balls / Backgrounds / Specials) and a
    /// 2-column scrollable card grid with buy / equip / stock flows. Skins persist via
    /// <see cref="SkinManager"/>; power-ups and currency via <see cref="EconomyManager"/>.
    /// Returns to the calling scene tracked by <see cref="SceneTransitionManager.PreviousScene"/>.
    /// </summary>
    public class ShopSceneController : MonoBehaviour
    {
        // Tab indices — order matches the four-icon row art (tab_tubes/balls/backgrounds/specials).
        private const int TabTubes = 0, TabBalls = 1, TabBackgrounds = 2, TabSpecials = 3;

        private Font          _font;
        private GameObject    _canvas;
        private float         _safeTop, _safeBottom;
        private int           _tab = TabTubes;
        private RectTransform _gridContent;
        private Text          _coinsLabel, _gemsLabel;
        private Image         _tabRow;
        private GameObject    _popup;

        // ── Purchase-popup state (premium animated buy flow) ──
        private RenderTexture _blurRt;
        private RectTransform _popupCard;
        private RectTransform _popupThumb;
        private RectTransform _popupPriceRow;
        private RectTransform _confettiHost;
        private RectTransform _buyRt;
        private Button        _buyBtn;
        private Text          _buyLabel;
        private Vector2       _thumbHome;
        private Coroutine     _thumbIdleCo;
        private static Sprite _radialSprite;
        private static Sprite _softCircle;

        // Card colours / fallbacks.
        private static readonly Color CardBgFallback = new(0.13f, 0.13f, 0.25f, 0.98f);
        private static readonly Color CoinGold       = new(1f, 0.85f, 0.20f, 1f);
        private static readonly Color GemBlue        = new(0.55f, 0.85f, 1f, 1f);
        private static readonly Color BtnGreenFb     = new(0.20f, 0.65f, 0.30f, 1f);
        private static readonly Color BtnLightBlueFb = new(0.35f, 0.70f, 0.95f, 1f);
        private static readonly Color BtnYellowFb    = new(0.95f, 0.78f, 0.20f, 1f);
        private static readonly Color BtnBlueFb      = new(0.25f, 0.45f, 0.90f, 1f);

        private void Start()
        {
            EnsureAudioManager();
            EnsureSaveSystem();
            EconomyManager.EnsureInstance();
            // DEBUG: opening the Shop must NEVER change balances — it only reads them. This logs the
            // balances at open so any unexpected change is caught against the next read.
            if (RewardConfig.Active.VerboseLogs)
                Debug.Log($"[Shop] Opened (read-only) — coins={SkinManager.GetCoins()}, " +
                          $"gems={(EconomyManager.Instance != null ? EconomyManager.Instance.Gems : 0)}");
            EnsureTransitionManager();
            EnsureEventSystem();
            ConfigureCamera();
            BuildUI();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_popup != null) { ClosePopup(); return; }
                GoBack();
            }
        }

        // ── UI construction ─────────────────────────────────────────────────────────

        private void BuildUI()
        {
            _font = GameAssets.MenuFont;

            var canvasGO = new GameObject("Canvas");
            _canvas      = canvasGO;
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            // Match WIDTH (0) not height: portrait phones are narrower than the 9:16
            // reference, so pinning logical width to 720 keeps fixed-width content (banner,
            // 2-col card grid, popups) from overflowing/clipping at the sides. No-op at 9:16.
            scaler.matchWidthOrHeight  = 0f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Uniform canvas scale = Screen.width/720, so 1 physical px = 720/Screen.width
            // canvas units (used for safe-area insets below).
            float lpu   = 720f / Mathf.Max(1, Screen.width);
            _safeTop    = (Screen.height - Screen.safeArea.yMax) * lpu;
            _safeBottom = Screen.safeArea.yMin * lpu;

            // ── Background (lowest layer) ──
            var bg    = new GameObject("Background");
            bg.transform.SetParent(canvasGO.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = BoltSortTheme.BackgroundDeep;
            GameAssets.Apply(bgImg, GameAssets.ShopBackground ?? GameAssets.GameBackground);
            bgImg.raycastTarget = false;
            Stretch(bgImg.rectTransform);

            // Vertical bands (canvas px from the top).
            float bannerH   = 150f;
            float cyTop      = _safeTop + bannerH + 6f;   // currency bar top
            float cyH        = 58f;
            float tabTop     = cyTop + cyH + 10f;          // tab row top
            float tabH       = 92f;
            float scrollTop  = tabTop + tabH + 12f;

            BuildHeader(canvasGO, bannerH);
            BuildCurrencyBar(canvasGO, cyTop, cyH);
            BuildTabBar(canvasGO, tabTop, tabH);
            BuildScroll(canvasGO, scrollTop);

            SkinManager.OnSkinChanged += OnBalance;
            if (EconomyManager.Instance != null) EconomyManager.Instance.OnBalanceChanged += OnBalance;

            SelectTab(_tab);
        }

        private void BuildHeader(GameObject canvasGO, float bannerH)
        {
            // Banner — centred at the top with a slight upward bleed; "Shop" text on it.
            var banner = new GameObject("Banner");
            banner.transform.SetParent(canvasGO.transform, false);
            var bannerImg = banner.AddComponent<Image>();
            var oldBannerSpr = GameAssets.ShopBanner;
            var newBannerSpr = GameAssets.ShopBanner2 ?? oldBannerSpr;
            GameAssets.Apply(bannerImg, newBannerSpr, preserveAspect: true);
            if (newBannerSpr == null) bannerImg.color = BoltSortTheme.HUDBackground;
            bannerImg.raycastTarget = false;
            var brt = banner.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot     = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(480f, bannerH);
            // Compensate vertical position when new banner sprite has a different height than old.
            float heightDeltaUnits = 0f;
            if (oldBannerSpr != null && newBannerSpr != null && !ReferenceEquals(oldBannerSpr, newBannerSpr))
                heightDeltaUnits = -(newBannerSpr.rect.height / newBannerSpr.pixelsPerUnit
                                   - oldBannerSpr.rect.height / oldBannerSpr.pixelsPerUnit) / 2f;
            brt.anchoredPosition = new Vector2(0f, -60f + heightDeltaUnits);

            var title = MakeLabel(banner, "Title", Tr("key_shop"), _font, 56,
                                  TextAnchor.MiddleCenter, bold: true, shadow: true);
            title.color = Color.white;
            var trt = title.rectTransform;
            trt.SetParent(brt, false); // title is a child of the banner, centred on the sprite
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, 27f);
            trt.sizeDelta = new Vector2(360f, 90f);

            // Exit button — top-right, inside the safe area; returns to the caller.
            var exit = new GameObject("ExitButton");
            exit.transform.SetParent(canvasGO.transform, false);
            var exitImg = exit.AddComponent<Image>();
            GameAssets.Apply(exitImg, GameAssets.ShopExitButton2 ?? GameAssets.ShopExitButton ?? GameAssets.BtnExit, preserveAspect: true);
            if (GameAssets.ShopExitButton2 == null && GameAssets.ShopExitButton == null && GameAssets.BtnExit == null)
                exitImg.color = new Color(0.75f, 0.22f, 0.22f, 1f);
            var exitBtn = exit.AddComponent<Button>();
            exitBtn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            exitBtn.onClick.AddListener(GoBack);
            var ert = exit.GetComponent<RectTransform>();
            ert.anchorMin = ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot     = new Vector2(1f, 1f);
            ert.sizeDelta = new Vector2(88f, 88f);
            ert.anchoredPosition = new Vector2(-16f, -(_safeTop + 14f));
        }

        private void BuildCurrencyBar(GameObject canvasGO, float top, float height)
        {
            float rawTop = top;
            top -= 40f; // raise the currency row 40px (shop tweak); anchoredPosition.y = -top
            Debug.Log($"[Shop] CurrencyBar anchoredPosition.y {-rawTop} → {-top}");
            _coinsLabel = BuildCurrencyChip(canvasGO, "CoinChip",
                GameAssets.ShopCoinShop ?? GameAssets.ShopCoin2 ?? GameAssets.ShopCoinIcon, CoinGold,
                top, height, leftAligned: true);
            _gemsLabel = BuildCurrencyChip(canvasGO, "GemChip",
                GameAssets.ShopDiamondShop ?? GameAssets.ShopDiamond2 ?? GameAssets.DiamondIcon, GemBlue,
                top, height, leftAligned: false);
            RefreshCurrency();
        }

        /// <summary>One corner-anchored [icon][amount] chip that auto-sizes to its text.</summary>
        private Text BuildCurrencyChip(GameObject parent, string name, Sprite icon,
                                       Color fallback, float top, float height, bool leftAligned)
        {
            var chip = new GameObject(name, typeof(RectTransform));
            chip.transform.SetParent(parent.transform, false);
            var rt = chip.GetComponent<RectTransform>();
            float x = leftAligned ? 0f : 1f;
            rt.anchorMin = rt.anchorMax = new Vector2(x, 1f);
            rt.pivot     = new Vector2(x, 1f);
            rt.anchoredPosition = new Vector2(leftAligned ? 24f : -24f, -top);

            var hlg = chip.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 6f;
            hlg.childControlWidth = false;  hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var csf = chip.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(chip.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            GameAssets.Apply(iconImg, icon, preserveAspect: true);
            iconImg.preserveAspect = true;
            if (icon == null) iconImg.color = fallback;
            iconImg.raycastTarget = false;
            // Fixed 70×70. The HLG has childControlWidth/Height = false, so it lays out using
            // the RectTransform sizeDelta directly (LayoutElement set too, for completeness).
            iconImg.rectTransform.sizeDelta = new Vector2(70f, 70f);
            var iconLe = iconGO.AddComponent<LayoutElement>();
            iconLe.preferredWidth = iconLe.preferredHeight = 70f;

            var label = new GameObject("Amount").AddComponent<Text>();
            label.transform.SetParent(chip.transform, false);
            label.font = _font; label.fontSize = 34; label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft; label.color = Color.white;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow   = VerticalWrapMode.Overflow;
            AddOutline(label.gameObject);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height - 8f;
            var lcsf = label.gameObject.AddComponent<ContentSizeFitter>();
            lcsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return label;
        }

        private void BuildTabBar(GameObject canvasGO, float top, float height)
        {
            var bar = new GameObject("TabBar", typeof(RectTransform));
            bar.transform.SetParent(canvasGO.transform, false);
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(16f, -(top + height));
            rt.offsetMax = new Vector2(-16f, -top);

            // Single Image whose sprite swaps per selected tab. Fills the rect (no
            // letterboxing) so the four equal click zones line up with the four icons.
            _tabRow = bar.AddComponent<Image>();
            _tabRow.preserveAspect = false;
            _tabRow.raycastTarget = false;

            // Four equal invisible click zones across the row.
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var zone = new GameObject($"Tab_{i}");
                zone.transform.SetParent(bar.transform, false);
                var zImg = zone.AddComponent<Image>();
                zImg.color = new Color(0f, 0f, 0f, 0.001f); // transparent but raycastable
                var zrt = zone.GetComponent<RectTransform>();
                zrt.anchorMin = new Vector2(i / 4f, 0f);
                zrt.anchorMax = new Vector2((i + 1) / 4f, 1f);
                zrt.offsetMin = zrt.offsetMax = Vector2.zero;
                var zbtn = zone.AddComponent<Button>();
                zbtn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
                zbtn.onClick.AddListener(() => SelectTab(idx));
            }
        }

        private void BuildScroll(GameObject canvasGO, float scrollTop)
        {
            var scrollGO = new GameObject("Scroll", typeof(RectTransform));
            scrollGO.transform.SetParent(canvasGO.transform, false);
            var scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, _safeBottom + 12f);
            scrollRt.offsetMax = new Vector2(0f, -scrollTop);

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f; scroll.inertia = true; scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 24f;

            var vp = new GameObject("Viewport");
            vp.transform.SetParent(scrollGO.transform, false);
            var vpImg = vp.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.001f);
            vp.AddComponent<RectMask2D>();
            var vpRt = vp.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            vpRt.pivot = new Vector2(0.5f, 1f);
            scroll.viewport = vpRt;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            _gridContent = content.GetComponent<RectTransform>();
            _gridContent.anchorMin = new Vector2(0f, 1f); _gridContent.anchorMax = new Vector2(1f, 1f);
            _gridContent.pivot = new Vector2(0.5f, 1f);
            _gridContent.offsetMin = _gridContent.offsetMax = Vector2.zero;

            var glg = content.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(326f, 300f);
            glg.spacing  = new Vector2(18f, 18f);
            glg.padding  = new RectOffset(16, 16, 16, 16);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;
            glg.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _gridContent;
        }

        // ── Tabs / grid ───────────────────────────────────────────────────────────

        private void SelectTab(int tab)
        {
            _tab = tab;
            if (_tabRow != null)
            {
                var spr = GameAssets.ShopTabRow(tab);
                if (spr != null) { _tabRow.sprite = spr; _tabRow.color = Color.white; }
                else _tabRow.color = new Color(0.16f, 0.16f, 0.30f, 1f);
            }
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            if (_gridContent == null) return;
            for (int i = _gridContent.childCount - 1; i >= 0; i--)
                Destroy(_gridContent.GetChild(i).gameObject);

            if (_tab == TabSpecials) { BuildSpecials(); return; }

            var category = _tab == TabBalls ? SkinCategory.Balls
                         : _tab == TabBackgrounds ? SkinCategory.Backgrounds
                         : SkinCategory.Tubes;
            var items = SkinCatalogue.For(category);
            for (int i = 0; i < items.Count; i++)
                BuildSkinCell(items[i], i);
        }

        // ── Skin cell ───────────────────────────────────────────────────────────────

        private void BuildSkinCell(SkinItem item, int index)
        {
            bool equipped = SkinManager.IsEquipped(item);
            bool unlocked = SkinManager.IsUnlocked(item);

            var cell = MakeCard(out var thumbArea);

            // Thumbnail (≈50% of card height).
            var thumb = item.Thumbnail;
            var thumbImg = thumbArea.AddComponent<Image>();
            if (thumb != null) { thumbImg.sprite = thumb; thumbImg.color = Color.white; thumbImg.preserveAspect = true; }
            else               thumbImg.color = SwatchColor(item);
            thumbImg.raycastTarget = false;
            // Locked items are shown at full brightness (the price row + Buy button already convey
            // locked state); no dark overlay is drawn over the thumbnail.

            // Name.
            AddCardName(cell, item.DisplayName);

            // Price row (only when locked).
            if (!unlocked) AddPriceRow(cell, item.UnlockCost, 0);

            // Action button + etiqueta.
            if (equipped)
                AddActionButton(cell, GameAssets.ShopBtnLightBlue, BtnLightBlueFb,
                                "✓ " + Tr("key_equipped"), interactable: false, onClick: null, showTag: false);
            else if (unlocked)
                AddActionButton(cell, GameAssets.ShopBtnGreen, BtnGreenFb,
                                Tr("key_equip"), interactable: true,
                                onClick: () => { SkinManager.EquipSkin(item); RebuildGrid(); }, showTag: false);
            else
                AddActionButton(cell, GameAssets.ShopBtnYellow, BtnYellowFb,
                                Tr("key_buy"), interactable: true,
                                onClick: () => ShowPurchasePopup(item), showTag: true);

            AnimateIn(cell, index, equipped);
        }

        // ── Specials tab ──────────────────────────────────────────────────────────

        private void BuildSpecials()
        {
            int cap = EconomyConfig.PowerUpMaxHeld;
            var em  = EconomyManager.Instance;

            // Extra Tube.
            BuildStockSpecial(0, GameAssets.BtnExtraTube, Tr("key_extra_tube"),
                em != null ? em.ExtraTubes : 0, cap, EconomyConfig.ExtraTubeGemPrice,
                () => em != null && em.BuyExtraTube());

            // Lightning Bolt.
            BuildStockSpecial(1, GameAssets.BtnUndoNew, Tr("key_lightning_bolt"),
                em != null ? em.LightningBolts : 0, cap, EconomyConfig.LightningBoltGemPrice,
                () => em != null && em.BuyLightningBolt());

            // Buy Coins (IAP placeholder) — uses the dedicated more_coins_2 art.
            BuildIapSpecial(2, GameAssets.ShopMoreCoins2 ?? GameAssets.ShopCoin2 ?? GameAssets.ShopCoinIcon, CoinGold,
                Tr("key_get_coins"), GameAssets.ShopBtnYellow, BtnYellowFb);

            // Buy Gems (IAP placeholder).
            BuildIapSpecial(3, GameAssets.ShopDiamond2 ?? GameAssets.DiamondIcon, GemBlue,
                Tr("key_get_gems"), GameAssets.ShopBtnBlue, BtnBlueFb);
        }

        private void BuildStockSpecial(int index, Sprite icon, string name,
                                       int held, int cap, int gemPrice, System.Func<bool> buy)
        {
            var cell = MakeCard(out var thumbArea);
            var thumbImg = thumbArea.AddComponent<Image>();
            if (icon != null) { thumbImg.sprite = icon; thumbImg.color = Color.white; thumbImg.preserveAspect = true; }
            else              thumbImg.color = new Color(0.45f, 0.70f, 0.95f, 1f);
            thumbImg.raycastTarget = false;

            AddCardName(cell, $"{name}  {held}/{cap}");

            bool full = held >= cap;
            if (full)
            {
                AddActionButton(cell, GameAssets.ShopBtnLightBlue, BtnLightBlueFb,
                                Tr("key_full"), interactable: false, onClick: null, showTag: false);
            }
            else
            {
                AddPriceRow(cell, 0, gemPrice);
                AddActionButton(cell, GameAssets.ShopBtnBlue, BtnBlueFb, Tr("key_buy"),
                    interactable: true, showTag: true, onClick: () =>
                    {
                        if (buy())
                        {
                            AudioMgr.Instance?.PlaySFX("extra_bonus");
                            ShowToast(Tr("key_unlocked"));
                            RebuildGrid();
                        }
                        else ShowToast(Tr("key_not_enough_coins"));
                    });
            }
            AnimateIn(cell, index, false);
        }

        private void BuildIapSpecial(int index, Sprite icon, Color iconFb, string label,
                                     Sprite btnSprite, Color btnFb)
        {
            var cell = MakeCard(out var thumbArea);
            var thumbImg = thumbArea.AddComponent<Image>();
            if (icon != null) { thumbImg.sprite = icon; thumbImg.color = Color.white; thumbImg.preserveAspect = true; }
            else              thumbImg.color = iconFb;
            thumbImg.raycastTarget = false;

            AddCardName(cell, label);
            AddActionButton(cell, btnSprite, btnFb, Tr("key_buy_euro"),
                interactable: true, showTag: false, onClick: ShowComingSoon);
            AnimateIn(cell, index, false);
        }

        // ── Card building blocks ────────────────────────────────────────────────────

        /// <summary>Creates a card (elements_background) and its centred thumbnail holder.</summary>
        private GameObject MakeCard(out GameObject thumbArea)
        {
            var cell = new GameObject("Card");
            cell.transform.SetParent(_gridContent, false);
            var img = cell.AddComponent<Image>();
            GameAssets.Apply(img, GameAssets.ShopCardBg, preserveAspect: false);
            if (GameAssets.ShopCardBg == null) img.color = CardBgFallback;

            thumbArea = new GameObject("Thumb", typeof(RectTransform));
            thumbArea.transform.SetParent(cell.transform, false);
            var thr = thumbArea.GetComponent<RectTransform>();
            thr.anchorMin = thr.anchorMax = new Vector2(0.5f, 1f);
            thr.pivot = new Vector2(0.5f, 1f);
            thr.sizeDelta = new Vector2(120f, 120f); // shrunk to make room for name + price row
            thr.anchoredPosition = new Vector2(0f, -12f);
            return cell;
        }

        private void AddCardName(GameObject cell, string text)
        {
            var name = MakeLabel(cell, "Name", text, _font, 26,
                                 TextAnchor.MiddleCenter, bold: true, shadow: false);
            name.raycastTarget = false;
            var rt = name.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-16f, 30f);
            rt.anchoredPosition = new Vector2(0f, 128f);
        }

        /// <summary>Centred [icon amount] (coins and/or gems) row above the action button.</summary>
        private void AddPriceRow(GameObject cell, int coins, int gems)
        {
            var row = new GameObject("PriceRow", typeof(RectTransform));
            row.transform.SetParent(cell.transform, false);
            var rrt = row.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 0f); rrt.anchorMax = new Vector2(0.5f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 78f);
            rrt.sizeDelta = new Vector2(300f, 44f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 6f;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var csf = row.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (coins > 0) AddPriceTerm(row, GameAssets.ShopCoinShop ?? GameAssets.ShopCoin2 ?? GameAssets.ShopCoinIcon,
                                        CoinGold, FormatCoins(coins), CoinGold);
            if (gems > 0)  AddPriceTerm(row, GameAssets.ShopDiamondShop ?? GameAssets.ShopDiamond2 ?? GameAssets.DiamondIcon,
                                        GemBlue, FormatCoins(gems), GemBlue);
        }

        private void AddPriceTerm(GameObject row, Sprite icon, Color iconFb, string amount, Color textColor)
        {
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(row.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            GameAssets.Apply(iconImg, icon, preserveAspect: true);
            iconImg.preserveAspect = true;
            if (icon == null) iconImg.color = iconFb;
            iconImg.raycastTarget = false;
            // Card price icon: 44×44 to fit the 300px card without overlapping the name/button.
            // (Top-bar currency chips stay 70×70 — see BuildCurrencyChip.)
            iconImg.rectTransform.sizeDelta = new Vector2(44f, 44f);
            var le = iconGO.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 44f;

            var t = new GameObject("Amount").AddComponent<Text>();
            t.transform.SetParent(row.transform, false);
            t.font = _font; t.fontSize = 28; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleLeft; t.color = textColor; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            AddOutline(t.gameObject);
            var tle = t.gameObject.AddComponent<LayoutElement>();
            tle.preferredHeight = 32f;
            var tcsf = t.gameObject.AddComponent<ContentSizeFitter>();
            tcsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>
        /// Bottom-centred action button. When <paramref name="showTag"/>, an etiqueta price-tag
        /// is placed so its right edge overlaps the button's left edge and its top sits at the
        /// button's vertical midpoint (per the design spec).
        /// </summary>
        private void AddActionButton(GameObject cell, Sprite sprite, Color fallback, string label,
                                     bool interactable, System.Action onClick, bool showTag)
        {
            const float btnW = 150f, btnH = 56f, btnY = 16f;

            if (showTag && GameAssets.ShopEtiqueta != null)
            {
                var tag = new GameObject("Etiqueta");
                tag.transform.SetParent(cell.transform, false);
                var tagImg = tag.AddComponent<Image>();
                tagImg.sprite = GameAssets.ShopEtiqueta; tagImg.color = Color.white;
                tagImg.preserveAspect = true; tagImg.raycastTarget = false;
                var tgrt = tag.GetComponent<RectTransform>();
                tgrt.anchorMin = tgrt.anchorMax = new Vector2(0.5f, 0f);
                tgrt.pivot = new Vector2(1f, 1f); // top-right corner is the anchor point
                tgrt.sizeDelta = new Vector2(62f, 62f);
                // right edge = button left edge (+6 overlap); top = button vertical midpoint
                tgrt.anchoredPosition = new Vector2(-btnW * 0.5f + 6f, btnY + btnH * 0.5f);
            }

            var go = new GameObject("Action");
            go.transform.SetParent(cell.transform, false);
            var img = go.AddComponent<Image>();
            if (sprite != null) { img.sprite = sprite; img.color = Color.white; img.type = Image.Type.Simple; }
            else img.color = fallback;
            var btn = go.AddComponent<Button>();
            btn.interactable = interactable;
            if (interactable && onClick != null)
            {
                btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
                btn.onClick.AddListener(() => onClick());
            }
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(btnW, btnH);
            rt.anchoredPosition = new Vector2(0f, btnY);

            var t = MakeLabel(go, "Label", label, _font, 24, TextAnchor.MiddleCenter, bold: true, shadow: true);
            t.color = Color.white; t.raycastTarget = false;
            var lr = t.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
        }

        private void AnimateIn(GameObject cell, int index, bool equipped)
        {
            var rt = cell.GetComponent<RectTransform>();
            rt.localScale = Vector3.zero;
            StartCoroutine(EntranceAnim(rt, 0.04f * index));
            if (equipped) StartCoroutine(EquippedPulse(rt));
        }

        private Color SwatchColor(SkinItem item) => item.Category switch
        {
            SkinCategory.Tubes => new Color(0.45f, 0.70f, 0.95f, 1f),
            SkinCategory.Balls => new Color(0.95f, 0.55f, 0.35f, 1f),
            _                  => new Color(0.35f, 0.30f, 0.55f, 1f),
        };

        // ── Purchase popup (skins) — premium animated buy flow ───────────────────────
        // Tap outside the card closes it (no cancel/X button). A blurred snapshot of the
        // shop sits behind a clean rounded card with a radial reward glow, a living
        // thumbnail, and a large animated Buy CTA. Confirming fires confetti + reward burst.

        private void ShowPurchasePopup(SkinItem item)
        {
            ClosePopup();

            // Root — full-screen, invisible but tap-to-close outside the card.
            _popup = new GameObject("Popup");
            _popup.transform.SetParent(_canvas.transform, false);
            var rootImg = _popup.AddComponent<Image>();
            rootImg.color = new Color(0f, 0f, 0f, 0.001f); // clear during blur capture, still raycastable
            Stretch(rootImg.rectTransform);
            _popup.AddComponent<Button>().onClick.AddListener(ClosePopup);

            // Blur layer — filled in after an end-of-frame screen capture.
            var blur = new GameObject("Blur").AddComponent<RawImage>();
            blur.transform.SetParent(_popup.transform, false);
            blur.color = new Color(1f, 1f, 1f, 0f);
            blur.raycastTarget = false;
            Stretch(blur.rectTransform);

            // Content — hidden until the snapshot is taken so it is not captured into the blur.
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(_popup.transform, false);
            Stretch(content.GetComponent<RectTransform>());
            content.SetActive(false);

            // Radial reward gradient (white core → pastel rim) behind the card.
            var glowGO = new GameObject("RadialGlow");
            glowGO.transform.SetParent(content.transform, false);
            var glowImg = glowGO.AddComponent<Image>();
            glowImg.sprite = RadialGradient();
            glowImg.color = new Color(1f, 1f, 1f, 0f);
            glowImg.raycastTarget = false;
            var glowRt = glowGO.GetComponent<RectTransform>();
            glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.pivot = new Vector2(0.5f, 0.5f);
            glowRt.anchoredPosition = new Vector2(0f, 40f);
            glowRt.sizeDelta = new Vector2(780f, 780f);

            // Clean rounded card — dedicated shop panel art (elements_background).
            var card = new GameObject("Card");
            card.transform.SetParent(content.transform, false);
            var cardImg = card.AddComponent<Image>();
            if (GameAssets.ShopElementsBg != null)
            { cardImg.sprite = GameAssets.ShopElementsBg; cardImg.type = Image.Type.Simple; cardImg.color = Color.white; }
            else cardImg.color = new Color(0.10f, 0.12f, 0.22f, 0.99f);
            card.AddComponent<Button>().onClick.AddListener(() => { }); // consume taps on the card
            _popupCard = card.GetComponent<RectTransform>();
            _popupCard.anchorMin = _popupCard.anchorMax = new Vector2(0.5f, 0.5f);
            _popupCard.pivot = new Vector2(0.5f, 0.5f);
            _popupCard.anchoredPosition = Vector2.zero;
            _popupCard.sizeDelta = new Vector2(620f, 740f);

            // Living thumbnail (square 1:1; float + pulse while open).
            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(card.transform, false);
            var thumbImg = thumbGO.AddComponent<Image>();
            var thumb = item.Thumbnail;
            if (thumb != null) { thumbImg.sprite = thumb; thumbImg.color = Color.white; thumbImg.preserveAspect = true; }
            else               thumbImg.color = SwatchColor(item);
            thumbImg.raycastTarget = false;
            _popupThumb = thumbGO.GetComponent<RectTransform>();
            _popupThumb.anchorMin = _popupThumb.anchorMax = new Vector2(0.5f, 0.5f);
            _popupThumb.pivot = new Vector2(0.5f, 0.5f);
            _popupThumb.sizeDelta = new Vector2(300f, 300f);
            _thumbHome = new Vector2(0f, 150f);
            _popupThumb.anchoredPosition = _thumbHome;

            // Name + price (≈35–40% larger than before).
            AddCardLabel(card, item.DisplayName, 50,
                         new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.56f), Color.white);
            BuildPopupPrice(card, item.UnlockCost);

            // Big animated Buy CTA.
            BuildBuyButton(card, Tr("key_buy"), () => ConfirmPurchase(item));

            // Confetti host (front of card).
            var confHost = new GameObject("ConfettiHost", typeof(RectTransform));
            confHost.transform.SetParent(content.transform, false);
            Stretch(confHost.GetComponent<RectTransform>());
            _confettiHost = confHost.GetComponent<RectTransform>();

            StartCoroutine(RevealPopup(blur, content, glowImg));
        }

        /// <summary>Centred [coin amount] price row, larger than the card rows for popup emphasis.</summary>
        private void BuildPopupPrice(GameObject card, int cost)
        {
            var row = new GameObject("PriceRow", typeof(RectTransform));
            row.transform.SetParent(card.transform, false);
            _popupPriceRow = row.GetComponent<RectTransform>();
            _popupPriceRow.anchorMin = _popupPriceRow.anchorMax = new Vector2(0.5f, 0.5f);
            _popupPriceRow.pivot = new Vector2(0.5f, 0.5f);
            _popupPriceRow.anchoredPosition = new Vector2(0f, -95f);
            _popupPriceRow.sizeDelta = new Vector2(360f, 70f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 8f;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var csf = row.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(row.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            var coin = GameAssets.ShopCoinShop ?? GameAssets.ShopCoin2 ?? GameAssets.ShopCoinIcon;
            if (coin != null) { iconImg.sprite = coin; iconImg.color = Color.white; iconImg.preserveAspect = true; }
            else iconImg.color = CoinGold;
            iconImg.raycastTarget = false;
            iconImg.rectTransform.sizeDelta = new Vector2(62f, 62f);
            var le = iconGO.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 62f;

            var t = new GameObject("Amount").AddComponent<Text>();
            t.transform.SetParent(row.transform, false);
            t.font = _font; t.fontSize = 52; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleLeft; t.color = CoinGold; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = FormatCoins(cost); t.raycastTarget = false;
            AddOutline(t.gameObject);
            var tle = t.gameObject.AddComponent<LayoutElement>();
            tle.preferredHeight = 56f;
            var tcsf = t.gameObject.AddComponent<ContentSizeFitter>();
            tcsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Large rounded green CTA with idle pulse, a sweeping shine, and press feedback.</summary>
        private void BuildBuyButton(GameObject card, string label, System.Action onClick)
        {
            var go = new GameObject("BuyButton");
            go.transform.SetParent(card.transform, false);
            var img = go.AddComponent<Image>();
            if (GameAssets.ShopBtnGreen != null)
            { img.sprite = GameAssets.ShopBtnGreen; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = BtnGreenFb;
            go.AddComponent<RectMask2D>(); // clip the moving shine to the button bounds
            _buyBtn = go.AddComponent<Button>();
            _buyBtn.transition = Selectable.Transition.None;
            _buyRt = go.GetComponent<RectTransform>();
            _buyRt.anchorMin = _buyRt.anchorMax = new Vector2(0.5f, 0.5f);
            _buyRt.pivot = new Vector2(0.5f, 0.5f);
            _buyRt.anchoredPosition = new Vector2(0f, -250f);
            _buyRt.sizeDelta = new Vector2(420f, 128f);

            var shineGO = new GameObject("Shine");
            shineGO.transform.SetParent(go.transform, false);
            var shine = shineGO.AddComponent<Image>();
            shine.sprite = SoftCircle(); shine.color = new Color(1f, 1f, 1f, 0f); shine.raycastTarget = false;
            var srt = shineGO.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(180f, 220f);

            _buyLabel = MakeLabel(go, "Label", label, _font, 46, TextAnchor.MiddleCenter, bold: true, shadow: true);
            _buyLabel.color = Color.white; _buyLabel.raycastTarget = false;
            var lr = _buyLabel.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;

            _buyBtn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            _buyBtn.onClick.AddListener(() => StartCoroutine(BuyPressFeedback(onClick)));

            // No idle pulse/bounce — the button stays static; only the sweeping shine lives.
            StartCoroutine(BuyShineSweep(srt, shine));
        }

        private void ConfirmPurchase(SkinItem item)
        {
            var result = SkinManager.TryPurchase(item);
            switch (result)
            {
                case PurchaseResult.Success:
                    AudioMgr.Instance?.PlaySFX("extra_bonus");
                    SkinManager.EquipSkin(item); // fires OnSkinChanged → currency refresh
                    StartCoroutine(CelebratePurchase());
                    break;
                case PurchaseResult.NotEnoughCoins:
                    ShowToast(Tr("key_not_enough_coins"));
                    if (_popupCard != null) StartCoroutine(ShakeAnim(_popupCard));
                    break;
                default:
                    ClosePopup();
                    break;
            }
        }

        // ── Purchase popup — animation & celebration ─────────────────────────────────

        private IEnumerator RevealPopup(RawImage blur, GameObject content, Image glow)
        {
            yield return BuildBlur(blur);
            if (content == null) yield break;
            content.SetActive(true);
            if (glow != null) StartCoroutine(GlowIdle(glow));
            if (_popupThumb != null) _thumbIdleCo = StartCoroutine(ThumbIdle(_popupThumb));
            if (_popupCard != null) PopIn(_popupCard);
        }

        /// <summary>Captures the screen, box-blurs it via successive downsample blits, shows it behind the card.</summary>
        private IEnumerator BuildBlur(RawImage target)
        {
            yield return new WaitForEndOfFrame();
            int w = Mathf.Max(2, Screen.width), h = Mathf.Max(2, Screen.height);

            RenderTexture full = null;
            try { full = RenderTexture.GetTemporary(w, h, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(full); }
            catch { if (full != null) { RenderTexture.ReleaseTemporary(full); full = null; } }
            if (full == null)
            {
                if (target != null) StartCoroutine(FadeRaw(target, new Color(0f, 0f, 0f, 0.72f), 0.18f));
                yield break;
            }

            int dw = Mathf.Max(2, w / 2), dh = Mathf.Max(2, h / 2);
            var a = RenderTexture.GetTemporary(dw, dh, 0); a.filterMode = FilterMode.Bilinear;
            Graphics.Blit(full, a);
            RenderTexture.ReleaseTemporary(full);
            for (int i = 0; i < 4; i++)
            {
                int nw = Mathf.Max(2, dw / 2), nh = Mathf.Max(2, dh / 2);
                var b = RenderTexture.GetTemporary(nw, nh, 0); b.filterMode = FilterMode.Bilinear;
                Graphics.Blit(a, b);
                RenderTexture.ReleaseTemporary(a); a = b; dw = nw; dh = nh;
            }

            _blurRt = new RenderTexture(Mathf.Max(2, w / 2), Mathf.Max(2, h / 2), 0) { filterMode = FilterMode.Bilinear };
            _blurRt.Create();
            Graphics.Blit(a, _blurRt);
            RenderTexture.ReleaseTemporary(a);

            if (target != null)
            {
                target.texture = _blurRt;
                target.uvRect = new Rect(0f, 1f, 1f, -1f); // capture is bottom-up; flip for display
                StartCoroutine(FadeRaw(target, new Color(0.72f, 0.72f, 0.78f, 1f), 0.18f));
            }
        }

        private IEnumerator FadeRaw(RawImage img, Color target, float dur)
        {
            var start = new Color(target.r, target.g, target.b, 0f);
            float t = 0f;
            while (t < dur && img != null)
            { t += Time.unscaledDeltaTime; img.color = Color.Lerp(start, target, t / dur); yield return null; }
            if (img != null) img.color = target;
        }

        private IEnumerator GlowIdle(Image img)
        {
            float t = 0f;
            while (img != null && _popup != null)
            {
                t += Time.unscaledDeltaTime;
                img.color = new Color(1f, 1f, 1f, 0.42f + 0.16f * Mathf.Sin(t * 1.6f));
                float s = 1f + 0.05f * Mathf.Sin(t * 1.2f);
                img.rectTransform.localScale = new Vector3(s, s, 1f);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 6f);
                yield return null;
            }
        }

        private IEnumerator ThumbIdle(RectTransform rt)
        {
            float t = 0f;
            while (rt != null && _popup != null)
            {
                t += Time.unscaledDeltaTime;
                rt.anchoredPosition = new Vector2(_thumbHome.x, _thumbHome.y + 9f * Mathf.Sin(t * 2.0f));
                float s = 1f + 0.045f * Mathf.Sin(t * 2.6f);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private IEnumerator BuyShineSweep(RectTransform srt, Image shine)
        {
            while (srt != null && _popup != null)
            {
                float t = 0f; const float dur = 1.1f;
                while (t < dur && srt != null)
                {
                    t += Time.unscaledDeltaTime; float p = t / dur;
                    srt.anchoredPosition = new Vector2(Mathf.Lerp(-230f, 230f, p), 0f);
                    if (shine != null) shine.color = new Color(1f, 1f, 1f, 0.3f * Mathf.Sin(p * Mathf.PI));
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.9f);
            }
        }

        private IEnumerator BuyPressFeedback(System.Action onClick)
        {
            if (_buyRt != null)
            {
                yield return TweenUtility.LerpRectScale(_buyRt, new Vector3(0.9f, 0.9f, 1f), 0.07f, TweenUtility.EaseOutQuad);
                yield return TweenUtility.LerpRectScale(_buyRt, Vector3.one, 0.09f, TweenUtility.EaseOutBack);
            }
            onClick?.Invoke();
        }

        private IEnumerator CelebratePurchase()
        {
            // Transform the CTA (now static apart from this one-shot).
            if (_buyBtn != null) _buyBtn.interactable = false;
            if (_buyRt != null) _buyRt.localScale = Vector3.one;
            if (_buyLabel != null) _buyLabel.text = "✓";

            // Stop idle float so the bounce reads cleanly.
            if (_thumbIdleCo != null) { StopCoroutine(_thumbIdleCo); _thumbIdleCo = null; }
            if (_popupThumb != null) _popupThumb.anchoredPosition = _thumbHome;

            ScreenFlashPopup();
            RadialBurst();
            StartCoroutine(BurstConfettiPopup());
            SpawnCoinFlourish();
            if (_popupCard != null)     StartCoroutine(PunchScale(_popupCard, 1.07f, 0.20f));
            if (_popupThumb != null)    StartCoroutine(PunchScale(_popupThumb, 1.28f, 0.32f));
            if (_popupPriceRow != null) StartCoroutine(PunchScale(_popupPriceRow, 1.22f, 0.30f));

            yield return new WaitForSecondsRealtime(1.0f);

            ClosePopup();
            RebuildGrid();
            ShowToast(Tr("key_unlocked"));
        }

        private IEnumerator PunchScale(RectTransform rt, float peak, float dur)
        {
            if (rt == null) yield break;
            float t = 0f;
            while (t < dur && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float s = 1f + (peak - 1f) * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private void ScreenFlashPopup()
        {
            if (_popup == null) return;
            var go = new GameObject("Flash");
            go.transform.SetParent(_popup.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = false;
            Stretch(img.rectTransform);
            StartCoroutine(FlashAnim(img));
        }

        private IEnumerator FlashAnim(Image img)
        {
            float t = 0f;
            while (t < 0.07f) { t += Time.unscaledDeltaTime; if (img != null) img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.5f, t / 0.07f)); yield return null; }
            t = 0f;
            while (t < 0.25f) { t += Time.unscaledDeltaTime; if (img != null) img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 0f, t / 0.25f)); yield return null; }
            if (img != null) Destroy(img.gameObject);
        }

        private void RadialBurst()
        {
            if (_popup == null) return;
            var go = new GameObject("Burst");
            go.transform.SetParent(_popup.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = SoftCircle(); img.color = new Color(1f, 1f, 1f, 0.7f); img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta = new Vector2(260f, 260f);
            StartCoroutine(BurstAnim(rt, img));
        }

        private IEnumerator BurstAnim(RectTransform rt, Image img)
        {
            float t = 0f; const float dur = 0.55f;
            while (t < dur && rt != null)
            {
                t += Time.unscaledDeltaTime; float p = t / dur;
                float s = Mathf.Lerp(0.4f, 4f, TweenUtility.EaseOutQuad(p));
                rt.localScale = new Vector3(s, s, 1f);
                if (img != null) img.color = new Color(1f, 1f, 1f, 0.7f * (1f - p));
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        private IEnumerator BurstConfettiPopup()
        {
            var sprites = GameAssets.ConfettiPieces;
            if (sprites == null || sprites.Length == 0 || _confettiHost == null) yield break;
            const int count = 26;
            for (int i = 0; i < count; i++)
            {
                if (_confettiHost == null) yield break;
                var go = new GameObject("Conf");
                go.transform.SetParent(_confettiHost, false);
                var img = go.AddComponent<Image>();
                img.sprite = sprites[UnityEngine.Random.Range(0, sprites.Length)];
                img.color = Color.white; img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(26f, 26f);
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(120f, 270f));
                StartCoroutine(FlyConfettiPopup(rt, img,
                    UnityEngine.Random.Range(220f, 460f),
                    UnityEngine.Random.Range(-300f, 300f),
                    UnityEngine.Random.Range(1.4f, 2.6f)));
                yield return new WaitForSecondsRealtime(0.03f);
            }
        }

        private void SpawnCoinFlourish()
        {
            var coin = GameAssets.ShopCoinShop ?? GameAssets.ShopCoin2 ?? GameAssets.ShopCoinIcon;
            var host = _confettiHost != null ? _confettiHost : (_popup != null ? _popup.transform as RectTransform : null);
            if (host == null) return;
            for (int i = 0; i < 7; i++)
            {
                var go = new GameObject("Coin");
                go.transform.SetParent(host, false);
                var img = go.AddComponent<Image>();
                if (coin != null) { img.sprite = coin; img.preserveAspect = true; img.color = Color.white; }
                else img.color = CoinGold;
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(54f, 54f);
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-50f, 50f), -60f);
                StartCoroutine(FlyConfettiPopup(rt, img,
                    UnityEngine.Random.Range(-300f, -180f), // negative speed → rises
                    UnityEngine.Random.Range(-60f, 60f),
                    UnityEngine.Random.Range(0.8f, 1.3f)));
            }
        }

        private IEnumerator FlyConfettiPopup(RectTransform rt, Image img, float speed, float rot, float life)
        {
            float t = 0f;
            while (t < life && rt != null)
            {
                t += Time.unscaledDeltaTime;
                var p = rt.anchoredPosition; p.y -= speed * Time.unscaledDeltaTime; rt.anchoredPosition = p;
                rt.Rotate(0f, 0f, rot * Time.unscaledDeltaTime);
                float a = 1f - Mathf.Clamp01((t - life * 0.6f) / (life * 0.4f));
                if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, a);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // ── Procedural sprites for the purchase popup ────────────────────────────────

        private static Sprite RadialGradient()
        {
            if (_radialSprite != null) return _radialSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f, maxR = size * 0.5f;
            Color inner = Color.white, outer = new Color(0.80f, 0.82f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxR);
                    var col = Color.Lerp(inner, outer, d);
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    px[y * size + x] = new Color(col.r, col.g, col.b, a);
                }
            tex.SetPixels(px); tex.Apply();
            _radialSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return _radialSprite;
        }

        private static Sprite SoftCircle()
        {
            if (_softCircle != null) return _softCircle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f, maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxR);
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            _softCircle = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return _softCircle;
        }

        private void ShowComingSoon()
        {
            ClosePopup();
            _popup = MakeDimmer();

            var card = new GameObject("Card");
            card.transform.SetParent(_popup.transform, false);
            var cardImg = card.AddComponent<Image>();
            if (GameAssets.MenuPopup != null) { cardImg.sprite = GameAssets.MenuPopup; cardImg.color = Color.white; }
            else cardImg.color = new Color(0.10f, 0.10f, 0.18f, 0.99f);
            card.AddComponent<Button>().onClick.AddListener(() => { });
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(500f, 320f);

            AddCardLabel(card, Tr("key_coming_soon"), 40, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.70f), Color.white);

            var ok = CreateButton(card, "OK", Tr("key_ok"), 32, BtnBlueFb, ClosePopup);
            var okr = ok.GetComponent<RectTransform>();
            okr.anchorMin = okr.anchorMax = new Vector2(0.5f, 0f);
            okr.pivot = new Vector2(0.5f, 0f);
            okr.sizeDelta = new Vector2(260f, 60f);
            okr.anchoredPosition = new Vector2(0f, 40f);

            PopIn(cr);
        }

        private GameObject MakeDimmer()
        {
            var dim = new GameObject("Popup");
            dim.transform.SetParent(_canvas.transform, false);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            Stretch(dim.GetComponent<RectTransform>());
            dim.AddComponent<Button>().onClick.AddListener(ClosePopup);
            return dim;
        }

        private void PopIn(RectTransform cr)
        {
            cr.localScale = Vector3.zero;
            StartCoroutine(TweenUtility.LerpRectScale(cr, Vector3.one, 0.22f, TweenUtility.EaseOutBack));
        }

        private void ClosePopup()
        {
            if (_popup != null) { Destroy(_popup); _popup = null; }
            if (_blurRt != null) { _blurRt.Release(); Destroy(_blurRt); _blurRt = null; }
            _popupCard = _popupThumb = _popupPriceRow = _confettiHost = _buyRt = null;
            _buyBtn = null; _buyLabel = null; _thumbIdleCo = null;
        }

        // ── Currency refresh ──────────────────────────────────────────────────────────

        private void OnBalance()
        {
            RefreshCurrency();
            if (_tab == TabSpecials) RebuildGrid(); // stock / Full state may have changed
        }

        private void RefreshCurrency()
        {
            if (_coinsLabel != null) _coinsLabel.text = FormatCoins(SkinManager.GetCoins());
            if (_gemsLabel != null)
                _gemsLabel.text = FormatCoins(EconomyManager.Instance != null ? EconomyManager.Instance.Gems : 0);
        }

        /// <summary>Spanish-locale number format: dots as thousands separators (e.g. 1.234.567).</summary>
        private static string FormatCoins(int value)
        {
            if (value < 1000) return value.ToString();
            var s = value.ToString();
            var result = new System.Text.StringBuilder();
            int mod = s.Length % 3;
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && (i - mod) % 3 == 0 && mod != 0) result.Append('.');
                else if (i > 0 && mod == 0 && i % 3 == 0)    result.Append('.');
                result.Append(s[i]);
            }
            return result.ToString();
        }

        // ── Feedback / animation ──────────────────────────────────────────────────────

        private void ShowToast(string message)
        {
            var go = new GameObject("Toast");
            go.transform.SetParent(_canvas.transform, false);
            var t = go.AddComponent<Text>();
            t.text = message; t.font = _font; t.fontSize = 38; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.8f); sh.effectDistance = new Vector2(2f, -2f);
            AddOutline(go);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.5f); rt.anchorMax = new Vector2(0.9f, 0.58f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            StartCoroutine(ToastAnim(go, t));
        }

        private IEnumerator ToastAnim(GameObject go, Text t)
        {
            float life = 1.1f, elapsed = 0f;
            while (elapsed < life)
            {
                if (go == null) yield break;
                elapsed += Time.deltaTime;
                float a = elapsed < 0.8f ? 1f : 1f - (elapsed - 0.8f) / 0.3f;
                t.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                go.transform.localPosition += Vector3.up * (30f * Time.deltaTime);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private IEnumerator EntranceAnim(RectTransform rt, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (rt != null)
                yield return TweenUtility.LerpRectScale(rt, Vector3.one, 0.25f, TweenUtility.EaseOutBack);
        }

        private IEnumerator EquippedPulse(RectTransform rt)
        {
            while (rt != null)
            {
                float s = 1f + Mathf.Sin(Time.time * 3f) * 0.02f;
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private IEnumerator ShakeAnim(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector2 origin = rt.anchoredPosition;
            float dur = 0.35f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float dx = Mathf.Sin(elapsed * 50f) * 14f * (1f - elapsed / dur);
                rt.anchoredPosition = origin + new Vector2(dx, 0f);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = origin;
        }

        private void OnDestroy()
        {
            SkinManager.OnSkinChanged -= OnBalance;
            if (EconomyManager.Instance != null) EconomyManager.Instance.OnBalanceChanged -= OnBalance;
            if (_blurRt != null) { _blurRt.Release(); Destroy(_blurRt); _blurRt = null; }
        }

        // ── Navigation ────────────────────────────────────────────────────────────────

        private void GoBack()
        {
            string prev = SceneTransitionManager.PreviousScene;
            if (string.IsNullOrEmpty(prev) || prev == "Shop") prev = "MainMenu";

            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo(prev);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(prev);
        }

        // ── Setup helpers ─────────────────────────────────────────────────────────

        private static void EnsureAudioManager()
        {
            if (AudioMgr.Instance == null)
                new GameObject("AudioManager").AddComponent<AudioMgr>();
        }

        private static void EnsureSaveSystem()
        {
            if (BoltSort.SaveSystem.SaveSystem.Instance == null)
                new GameObject("SaveSystem").AddComponent<BoltSort.SaveSystem.SaveSystem>();
        }

        private static void EnsureTransitionManager()
        {
            if (SceneTransitionManager.Instance == null)
                new GameObject("SceneTransitionManager").AddComponent<SceneTransitionManager>();
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private static void ConfigureCamera()
        {
            if (Camera.main != null)
                Camera.main.backgroundColor = BoltSortTheme.BackgroundDeep;
        }

        private Text AddCardLabel(GameObject parent, string text, int size,
                                  Vector2 aMin, Vector2 aMax, Color color)
        {
            var t = MakeLabel(parent, "Label", text, _font, size,
                              TextAnchor.MiddleCenter, bold: true, shadow: true);
            t.color = color; t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        private GameObject CreateButton(GameObject parent, string name, string label,
                                        int fontSize, Color bgColor, System.Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            if (GameAssets.MenuButton != null) GameAssets.Apply(img, GameAssets.MenuButton, false);
            else                               img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() => onClick?.Invoke());

            var t = MakeLabel(go, "Label", label, _font, fontSize,
                              TextAnchor.MiddleCenter, bold: true, shadow: true);
            t.color = Color.white; t.raycastTarget = false;
            var lr = t.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            return go;
        }

        private static Text MakeLabel(GameObject parent, string name, string text, Font font,
                                      int size, TextAnchor anchor, bool bold, bool shadow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = anchor; t.color = Color.white; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            // Auto-shrink to fit so no localized string overflows its container.
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 10;
            t.resizeTextMaxSize = size;
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor    = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            AddOutline(go); // black stroke on every Shop label for readability
            return t;
        }

        private static void AddOutline(GameObject go)
        {
            if (go.GetComponent<Outline>() != null) return; // never double up
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(1f, 1f);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ── Localization helpers ────────────────────────────────────────────────────
        private static string Tr(string key)
        {
            var m = LocalizationManager.Instance;
            return m != null ? m.Get(key) : key;
        }

        private static string TrF(string key, params object[] args)
        {
            var m = LocalizationManager.Instance;
            return m != null ? m.Format(key, args) : key;
        }
    }
}
