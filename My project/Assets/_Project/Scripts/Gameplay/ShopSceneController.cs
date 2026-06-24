using System.Collections;
using System.Collections.Generic;
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
    /// Procedurally builds the standalone Shop scene: header, coin balance, a three-tab
    /// skin browser (Tubes / Backgrounds / Balls) and a 2-column scrollable grid with
    /// buy/equip flow. Skins persist via <see cref="SkinManager"/> and apply live in
    /// Gameplay. Returns to MainMenu via SceneTransitionManager.
    /// </summary>
    public class ShopSceneController : MonoBehaviour
    {
        private Font          _font;
        private GameObject    _canvas;
        private float         _safeTop, _safeBottom;
        private SkinCategory  _category = SkinCategory.Tubes;
        private RectTransform _gridContent;
        private Text          _coinsLabel;
        private GameObject    _popup;

        private readonly Dictionary<SkinCategory, Image> _tabImages = new();

        // Cell visual constants
        private static readonly Color CellBg        = new(0.13f, 0.13f, 0.25f, 0.98f);
        private static readonly Color CellEquipped  = new(0.15f, 0.45f, 0.25f, 0.98f);
        private static readonly Color BorderEquip   = new(1f, 0.84f, 0.20f, 1f);
        private static readonly Color CoinGold      = new(1f, 0.85f, 0.20f, 1f);

        private void Start()
        {
            EnsureAudioManager();
            EnsureSaveSystem();
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
            scaler.matchWidthOrHeight  = 1f;
            canvasGO.AddComponent<GraphicRaycaster>();

            float lpu     = 1280f / Screen.height;
            _safeTop      = (Screen.height - Screen.safeArea.yMax) * lpu;
            _safeBottom   = Screen.safeArea.yMin * lpu;

            // Background
            var bg    = new GameObject("Background");
            bg.transform.SetParent(canvasGO.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = BoltSortTheme.BackgroundDeep;
            GameAssets.Apply(bgImg, GameAssets.GameBackground);
            Stretch(bgImg.rectTransform);

            BuildHeader(canvasGO);
            BuildTabBar(canvasGO);
            BuildScroll(canvasGO);

            SkinManager.OnSkinChanged += UpdateCoins;
            SelectCategory(_category);
        }

        private void BuildHeader(GameObject canvasGO)
        {
            var header = new GameObject("Header");
            header.transform.SetParent(canvasGO.transform, false);
            header.AddComponent<Image>().color = BoltSortTheme.HUDBackground;
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -(110f + _safeTop)); hr.offsetMax = Vector2.zero;

            var title = MakeLabel(header, "Title", Tr("key_shop"), _font, 52,
                                  TextAnchor.MiddleCenter, bold: true, shadow: true);
            title.color = BoltSortTheme.HUDText;
            var tr = title.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = new Vector2(0f, -_safeTop);

            // Back / home button (top-left)
            var backBtn = new GameObject("BackButton");
            backBtn.transform.SetParent(header.transform, false);
            var backImg = backBtn.AddComponent<Image>();
            if (GameAssets.BtnHomeAction != null)      GameAssets.Apply(backImg, GameAssets.BtnHomeAction, true);
            else if (GameAssets.NavBack != null)       GameAssets.Apply(backImg, GameAssets.NavBack, true);
            else                                       backImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var bb = backBtn.AddComponent<Button>();
            bb.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            bb.onClick.AddListener(GoBack);
            var bbr = backBtn.GetComponent<RectTransform>();
            bbr.anchorMin = new Vector2(0f, 0f); bbr.anchorMax = new Vector2(0f, 1f);
            bbr.pivot     = new Vector2(0f, 0.5f);
            bbr.offsetMin = new Vector2(10f, 12f); bbr.offsetMax = new Vector2(98f, -(12f + _safeTop));

            // Coin display (top-right): right-anchored HLG → [CoinIcon][Number][+]
            // The group auto-sizes to fit its content and expands leftward from the right edge.
            var coinRow = new GameObject("CoinRow");
            coinRow.transform.SetParent(header.transform, false);
            var coinRowRt = coinRow.GetComponent<RectTransform>();
            if (coinRowRt == null) coinRowRt = coinRow.AddComponent<RectTransform>();
            coinRowRt.anchorMin = new Vector2(1f, 0f); coinRowRt.anchorMax = new Vector2(1f, 1f);
            coinRowRt.pivot     = new Vector2(1f, 0.5f);
            coinRowRt.anchoredPosition = new Vector2(-8f, -_safeTop * 0.5f);
            coinRowRt.sizeDelta = new Vector2(0f, 0f); // width auto-set by ContentSizeFitter

            var hlg = coinRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var rowFitter = coinRow.AddComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowFitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

            // CoinIcon
            var coinIconGO = new GameObject("CoinIcon");
            coinIconGO.transform.SetParent(coinRow.transform, false);
            var coinIconImg = coinIconGO.AddComponent<Image>();
            GameAssets.Apply(coinIconImg, GameAssets.ShopCoinIcon, preserveAspect: true);
            if (GameAssets.ShopCoinIcon == null) coinIconImg.color = CoinGold;
            coinIconImg.raycastTarget = false;
            var coinIconLe = coinIconGO.AddComponent<LayoutElement>();
            coinIconLe.preferredWidth = 18f; coinIconLe.preferredHeight = 18f; // 50% of 36px baseline

            // Coin number label — ContentSizeFitter so it grows with text
            var coinsGO = new GameObject("Coins");
            coinsGO.transform.SetParent(coinRow.transform, false);
            _coinsLabel = coinsGO.AddComponent<Text>();
            _coinsLabel.font = _font; _coinsLabel.fontSize = 32;
            _coinsLabel.fontStyle = FontStyle.Bold; _coinsLabel.alignment = TextAnchor.MiddleCenter;
            _coinsLabel.color = Color.white; _coinsLabel.supportRichText = false;
            _coinsLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _coinsLabel.verticalOverflow   = VerticalWrapMode.Overflow;
            var coinsSh = coinsGO.AddComponent<Shadow>();
            coinsSh.effectColor = new Color(0f, 0f, 0f, 0.8f);
            coinsSh.effectDistance = new Vector2(1f, -1f);
            var coinsLe = coinsGO.AddComponent<LayoutElement>();
            coinsLe.preferredHeight = 40f;
            var coinsCsf = coinsGO.AddComponent<ContentSizeFitter>();
            coinsCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            coinsCsf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

            // More coins button — fixed width, rightmost element
            var moreCoinsBtnGO = new GameObject("MoreCoinsButton");
            moreCoinsBtnGO.transform.SetParent(coinRow.transform, false);
            var moreCoinsImg = moreCoinsBtnGO.AddComponent<Image>();
            GameAssets.Apply(moreCoinsImg, GameAssets.ShopMoreCoins, preserveAspect: true);
            if (GameAssets.ShopMoreCoins == null) moreCoinsImg.color = new Color(0.20f, 0.65f, 0.30f, 1f);
            var moreCoinsBtn = moreCoinsBtnGO.AddComponent<Button>();
            moreCoinsBtn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            moreCoinsBtn.onClick.AddListener(ShowMoreCoinsPopup);
            var moreCoinsBtnLe = moreCoinsBtnGO.AddComponent<LayoutElement>();
            moreCoinsBtnLe.preferredWidth = 20f; moreCoinsBtnLe.preferredHeight = 20f; // 50% of 40px baseline

            UpdateCoins();
        }

        private void BuildTabBar(GameObject canvasGO)
        {
            var bar = new GameObject("TabBar", typeof(RectTransform));
            bar.transform.SetParent(canvasGO.transform, false);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 1f); barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            float top = 110f + _safeTop;
            barRt.offsetMin = new Vector2(0f, -(top + 96f));
            barRt.offsetMax = new Vector2(0f, -top);

            var grid = bar.AddComponent<HorizontalLayoutGroup>();
            grid.childControlWidth = true;  grid.childControlHeight = true;
            grid.childForceExpandWidth = true; grid.childForceExpandHeight = true;
            grid.spacing = 8f; grid.padding = new RectOffset(12, 12, 8, 8);

            MakeTab(bar, SkinCategory.Tubes,       "TUBES");
            MakeTab(bar, SkinCategory.Backgrounds, "BACKGROUNDS");
            MakeTab(bar, SkinCategory.Balls,       "BALLS");
        }

        private void MakeTab(GameObject parent, SkinCategory cat, string label)
        {
            var go  = new GameObject($"Tab_{cat}");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            _tabImages[cat] = img;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() => SelectCategory(cat));
        }

        private void BuildScroll(GameObject canvasGO)
        {
            var scrollGO = new GameObject("SkinScroll", typeof(RectTransform));
            scrollGO.transform.SetParent(canvasGO.transform, false);
            var scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, _safeBottom + 16f);
            scrollRt.offsetMax = new Vector2(0f, -(110f + _safeTop + 104f));

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f; scroll.inertia = true; scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 24f;

            // Viewport
            var vp = new GameObject("Viewport");
            vp.transform.SetParent(scrollGO.transform, false);
            var vpImg = vp.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.001f); // near-invisible, needed for raycast/mask
            vp.AddComponent<RectMask2D>();
            var vpRt = vp.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            vpRt.pivot = new Vector2(0.5f, 1f);
            scroll.viewport = vpRt;

            // Content
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            _gridContent = content.GetComponent<RectTransform>();
            _gridContent.anchorMin = new Vector2(0f, 1f); _gridContent.anchorMax = new Vector2(1f, 1f);
            _gridContent.pivot = new Vector2(0.5f, 1f);
            _gridContent.offsetMin = new Vector2(0f, 0f); _gridContent.offsetMax = new Vector2(0f, 0f);

            var glg = content.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(326f, 248f);
            glg.spacing  = new Vector2(18f, 18f);
            glg.padding  = new RectOffset(16, 16, 16, 16);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;
            glg.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = _gridContent;
        }

        // ── Category / grid ─────────────────────────────────────────────────────────

        private void SelectCategory(SkinCategory cat)
        {
            _category = cat;
            RefreshTabVisuals();
            RebuildGrid();
        }

        private void RefreshTabVisuals()
        {
            foreach (var kv in _tabImages)
            {
                bool active = kv.Key == _category;
                Sprite spr = GetTabSprite(kv.Key, active);
                if (spr != null) { kv.Value.sprite = spr; kv.Value.color = Color.white; }
                else kv.Value.color = active ? new Color(0.30f, 0.55f, 0.95f, 1f)
                                             : new Color(0.16f, 0.16f, 0.30f, 1f);
                var lbl = kv.Value.GetComponentInChildren<Text>();
                if (lbl != null) lbl.color = active ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            }
        }

        private static Sprite GetTabSprite(SkinCategory cat, bool selected) => cat switch
        {
            SkinCategory.Tubes       => selected ? GameAssets.ShopTabTubesSelected       : GameAssets.ShopTabTubesUnselected,
            SkinCategory.Backgrounds => selected ? GameAssets.ShopTabWallpapersSelected   : GameAssets.ShopTabWallpapersUnselected,
            SkinCategory.Balls       => selected ? GameAssets.ShopTabBallsSelected        : GameAssets.ShopTabBallsUnselected,
            _                        => null,
        };

        private void RebuildGrid()
        {
            if (_gridContent == null) return;
            for (int i = _gridContent.childCount - 1; i >= 0; i--)
                Destroy(_gridContent.GetChild(i).gameObject);

            var items = SkinCatalogue.For(_category);
            for (int i = 0; i < items.Count; i++)
                BuildCell(items[i], i);
        }

        private void BuildCell(SkinItem item, int index)
        {
            bool equipped = SkinManager.IsEquipped(item);
            bool unlocked = SkinManager.IsUnlocked(item);

            var cell = new GameObject($"Cell_{item.SkinId}");
            cell.transform.SetParent(_gridContent, false);
            var cellImg = cell.AddComponent<Image>();
            cellImg.color = equipped ? CellEquipped : CellBg;

            // Equipped border (gold outline)
            if (equipped)
            {
                var outline = cell.AddComponent<Outline>();
                outline.effectColor = BorderEquip;
                outline.effectDistance = new Vector2(4f, 4f);
            }

            // Thumbnail
            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(cell.transform, false);
            var thumbImg = thumbGO.AddComponent<Image>();
            var thumb = item.Thumbnail;
            if (thumb != null) { thumbImg.sprite = thumb; thumbImg.color = Color.white; thumbImg.preserveAspect = true; }
            else               { thumbImg.color = SwatchColor(item); }
            thumbImg.raycastTarget = false;
            var thr = thumbGO.GetComponent<RectTransform>();
            thr.anchorMin = new Vector2(0.5f, 1f); thr.anchorMax = new Vector2(0.5f, 1f);
            thr.pivot = new Vector2(0.5f, 1f);
            thr.sizeDelta = new Vector2(150f, 150f);
            thr.anchoredPosition = new Vector2(0f, -14f);

            // Locked overlay
            if (!unlocked)
            {
                var dim = new GameObject("Dim");
                dim.transform.SetParent(thumbGO.transform, false);
                var dimImg = dim.AddComponent<Image>();
                dimImg.color = new Color(0f, 0f, 0f, 0.45f);
                dimImg.raycastTarget = false;
                Stretch(dim.GetComponent<RectTransform>());
            }

            // Name
            var name = MakeLabel(cell, "Name", item.DisplayName, _font, 26,
                                 TextAnchor.MiddleCenter, bold: true, shadow: false);
            name.raycastTarget = false;
            var nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 0f);
            nrt.pivot = new Vector2(0.5f, 0f);
            nrt.sizeDelta = new Vector2(0f, 36f);
            nrt.anchoredPosition = new Vector2(0f, 52f);

            // Status
            string status; Color statusColor;
            if (equipped)       { status = "✓ " + Tr("key_equipped"); statusColor = Color.white; }
            else if (unlocked)  { status = Tr("key_equip");          statusColor = new Color(0.55f, 0.85f, 1f, 1f); }
            else                { status = TrF("key_cost_coins_fmt", item.UnlockCost); statusColor = CoinGold; }

            var st = MakeLabel(cell, "Status", status, _font, 26,
                               TextAnchor.MiddleCenter, bold: true, shadow: false);
            st.color = statusColor; st.raycastTarget = false;
            var srt = st.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.sizeDelta = new Vector2(0f, 40f);
            srt.anchoredPosition = new Vector2(0f, 10f);

            var btn = cell.AddComponent<Button>();
            btn.onClick.AddListener(() => OnCellTap(item, cell));

            // Entrance animation (staggered scale-up)
            var crt = cell.GetComponent<RectTransform>();
            crt.localScale = Vector3.zero;
            StartCoroutine(EntranceAnim(crt, 0.04f * index));

            // Equipped idle pulse
            if (equipped) StartCoroutine(EquippedPulse(crt));
        }

        private Color SwatchColor(SkinItem item) => item.Category switch
        {
            SkinCategory.Tubes => new Color(0.45f, 0.70f, 0.95f, 1f),
            SkinCategory.Balls => new Color(0.95f, 0.55f, 0.35f, 1f),
            _                  => new Color(0.35f, 0.30f, 0.55f, 1f),
        };

        // ── Interaction ──────────────────────────────────────────────────────────────

        private void OnCellTap(SkinItem item, GameObject cell)
        {
            AudioMgr.Instance?.PlaySFX("button_tap");
            if (SkinManager.IsEquipped(item)) return;

            if (SkinManager.IsUnlocked(item))
            {
                SkinManager.EquipSkin(item);
                RebuildGrid();
            }
            else
            {
                ShowPurchasePopup(item);
            }
        }

        private void ShowPurchasePopup(SkinItem item)
        {
            ClosePopup();

            _popup = new GameObject("PurchasePopup");
            _popup.transform.SetParent(_canvas.transform, false);
            var dim = _popup.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);
            Stretch(_popup.GetComponent<RectTransform>());
            var dimBtn = _popup.AddComponent<Button>();
            dimBtn.onClick.AddListener(ClosePopup);

            var card = new GameObject("Card");
            card.transform.SetParent(_popup.transform, false);
            var cardImg = card.AddComponent<Image>();
            GameAssets.Apply(cardImg, GameAssets.ShopPanel, false);
            if (GameAssets.ShopPanel == null) cardImg.color = new Color(0.10f, 0.10f, 0.18f, 0.99f);
            var cardBtn = card.AddComponent<Button>();
            cardBtn.onClick.AddListener(() => { });
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(520f, 560f);

            // Thumbnail
            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(card.transform, false);
            var thumbImg = thumbGO.AddComponent<Image>();
            var thumb = item.Thumbnail;
            if (thumb != null) { thumbImg.sprite = thumb; thumbImg.color = Color.white; thumbImg.preserveAspect = true; }
            else               thumbImg.color = SwatchColor(item);
            thumbImg.raycastTarget = false;
            var thr = thumbGO.GetComponent<RectTransform>();
            thr.anchorMin = thr.anchorMax = new Vector2(0.5f, 1f);
            thr.pivot = new Vector2(0.5f, 1f);
            thr.sizeDelta = new Vector2(220f, 220f);
            thr.anchoredPosition = new Vector2(0f, -48f);

            AddCardLabel(card, item.DisplayName, 38, new Vector2(0f, 0.40f), new Vector2(1f, 0.50f), Color.white);
            var price = AddCardLabel(card, TrF("key_cost_coins_fmt", item.UnlockCost), 40,
                                     new Vector2(0f, 0.30f), new Vector2(1f, 0.40f), CoinGold);
            price.color = CoinGold;

            // Confirm
            var confirm = CreateButton(card, "Confirm", Tr("key_buy"), 32,
                                       new Color(0.18f, 0.55f, 0.30f, 1f),
                                       () => ConfirmPurchase(item));
            var cfr = confirm.GetComponent<RectTransform>();
            cfr.anchorMin = new Vector2(0.5f, 0f); cfr.anchorMax = new Vector2(0.5f, 0f);
            cfr.pivot = new Vector2(0.5f, 0f);
            cfr.sizeDelta = new Vector2(300f, 64f);
            cfr.anchoredPosition = new Vector2(0f, 120f);

            // Cancel
            var cancel = CreateButton(card, "Cancel", Tr("key_cancel"), 30,
                                      new Color(0.40f, 0.20f, 0.24f, 1f), ClosePopup);
            var cnr = cancel.GetComponent<RectTransform>();
            cnr.anchorMin = new Vector2(0.5f, 0f); cnr.anchorMax = new Vector2(0.5f, 0f);
            cnr.pivot = new Vector2(0.5f, 0f);
            cnr.sizeDelta = new Vector2(300f, 60f);
            cnr.anchoredPosition = new Vector2(0f, 44f);

            cr.localScale = Vector3.zero;
            StartCoroutine(TweenUtility.LerpRectScale(cr, Vector3.one, 0.22f, TweenUtility.EaseOutBack));
        }

        private void ConfirmPurchase(SkinItem item)
        {
            var result = SkinManager.TryPurchase(item);
            switch (result)
            {
                case PurchaseResult.Success:
                    AudioMgr.Instance?.PlaySFX("extra_bonus");
                    SkinManager.EquipSkin(item);   // auto-equip on purchase
                    UpdateCoins();
                    ClosePopup();
                    RebuildGrid();
                    ShowToast(Tr("key_unlocked"));
                    break;
                case PurchaseResult.NotEnoughCoins:
                    ShowToast(Tr("key_not_enough_coins"));
                    if (_popup != null)
                        StartCoroutine(ShakeAnim(_popup.transform.Find("Card") as RectTransform));
                    break;
                default:
                    ClosePopup();
                    break;
            }
        }

        private void ClosePopup()
        {
            if (_popup != null) { Destroy(_popup); _popup = null; }
        }

        private void UpdateCoins()
        {
            if (_coinsLabel != null) _coinsLabel.text = FormatCoins(SkinManager.GetCoins());
        }

        /// <summary>Spanish-locale number format: dots as thousands separators (e.g. 1.234.567).</summary>
        private static string FormatCoins(int coins)
        {
            if (coins < 1000) return coins.ToString();
            // Build from right: groups of 3, separated by dots.
            var s = coins.ToString();
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

        private void ShowMoreCoinsPopup()
        {
            ClosePopup();

            _popup = new GameObject("MoreCoinsPopup");
            _popup.transform.SetParent(_canvas.transform, false);
            var dim = _popup.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);
            Stretch(_popup.GetComponent<RectTransform>());
            var dimBtn = _popup.AddComponent<Button>();
            dimBtn.onClick.AddListener(ClosePopup);

            var card = new GameObject("Card");
            card.transform.SetParent(_popup.transform, false);
            var cardImg = card.AddComponent<Image>();
            var popupSpr = GameAssets.MenuPopup;
            if (popupSpr != null) { cardImg.sprite = popupSpr; cardImg.color = Color.white; cardImg.type = Image.Type.Simple; }
            else cardImg.color = new Color(0.07f, 0.07f, 0.14f, 0.98f);
            var cardBtn = card.AddComponent<Button>();
            var cbc = cardBtn.colors;
            cbc.normalColor = cbc.highlightedColor = cbc.pressedColor = cbc.selectedColor = Color.white;
            cardBtn.colors = cbc;
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(520f, 620f);

            // Title centered within the top header area of the popup background
            AddCardLabel(card, Tr("key_buy_coins"), 38, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.94f), CoinGold);

            // Fix 5A: offer buttons — fixed pixel sizes, evenly stacked, well-contained in the popup
            // Card is 520×620. Use fixed-height rows centered in the lower portion.
            (int coins, string price)[] offers =
            {
                (500,    "0,99€"),
                (1200,   "1,99€"),
                (2500,   "3,99€"),
                (6000,   "7,99€"),
                (14000,  "14,99€"),
            };

            const float offerBtnW = 440f, offerBtnH = 68f, offerGap = 10f;
            // Stack from y=350 downward (center of card=310, title takes top portion)
            float offerStartY = 330f;
            for (int i = 0; i < offers.Length; i++)
            {
                var (offerCoins, offerPrice) = offers[i];
                float btnY = offerStartY - i * (offerBtnH + offerGap);

                var rowGO = new GameObject($"Offer_{i}");
                rowGO.transform.SetParent(card.transform, false);
                var rowImg = rowGO.AddComponent<Image>();
                // Sliced (9-slice) so the button keeps its border shape at the fixed
                // row dimensions instead of stretching/deforming the source sprite.
                if (GameAssets.MenuButton != null) { rowImg.sprite = GameAssets.MenuButton; rowImg.type = Image.Type.Sliced; rowImg.preserveAspect = false; rowImg.color = Color.white; }
                else rowImg.color = new Color(0.18f, 0.18f, 0.32f, 1f);
                var offerCaptured = offerCoins;
                var offerPriceCap = offerPrice;
                var rowBtn = rowGO.AddComponent<Button>();
                rowBtn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
                rowBtn.onClick.AddListener(() =>
                {
                    Debug.Log($"[Shop] Purchase selected: {offerCaptured} coins ({offerPriceCap})");
                    ClosePopup();
                });
                var rowRt = rowGO.GetComponent<RectTransform>();
                rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 1f);
                rowRt.pivot     = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -btnY);
                rowRt.sizeDelta        = new Vector2(offerBtnW, offerBtnH);

                var rowLabel = MakeLabel(rowGO, "Label",
                                         TrF("key_coins_pack_fmt", FormatCoins(offerCoins), offerPrice),
                                         _font, 26, TextAnchor.MiddleCenter, bold: true, shadow: true);
                rowLabel.color = Color.white; rowLabel.raycastTarget = false;
                var llr = rowLabel.rectTransform;
                llr.anchorMin = Vector2.zero; llr.anchorMax = Vector2.one;
                llr.offsetMin = llr.offsetMax = Vector2.zero;
            }

            // Close button
            var closeGO = new GameObject("CloseButton");
            closeGO.transform.SetParent(card.transform, false);
            var closeImg = closeGO.AddComponent<Image>();
            if (GameAssets.BtnExit != null) GameAssets.Apply(closeImg, GameAssets.BtnExit, true);
            else closeImg.color = new Color(0.7f, 0.2f, 0.2f, 1f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            closeBtn.onClick.AddListener(ClosePopup);
            var closeRt = closeGO.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-12f, -12f);
            closeRt.sizeDelta = new Vector2(60f, 60f);

            cr.localScale = Vector3.zero;
            StartCoroutine(TweenUtility.LerpRectScale(cr, Vector3.one, 0.22f, TweenUtility.EaseOutBack));
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
            SkinManager.OnSkinChanged -= UpdateCoins;
        }

        private void GoBack()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("MainMenu");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
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
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor    = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ── Localization helpers ────────────────────────────────────────────────────
        // The shop scene is rebuilt on entry, so static resolution against the active
        // table is sufficient (no in-scene language selector to refresh live).
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
