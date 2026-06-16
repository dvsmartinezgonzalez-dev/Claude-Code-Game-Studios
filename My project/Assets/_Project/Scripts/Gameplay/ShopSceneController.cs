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

            var title = MakeLabel(header, "Title", "SHOP", _font, 52,
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

            // Coin balance (top-right)
            _coinsLabel = MakeLabel(header, "Coins", "", _font, 34,
                                    TextAnchor.MiddleRight, bold: true, shadow: true);
            _coinsLabel.color = CoinGold;
            var crt = _coinsLabel.rectTransform;
            crt.anchorMin = new Vector2(1f, 0f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot     = new Vector2(1f, 0.5f);
            crt.sizeDelta = new Vector2(240f, 0f);
            crt.anchoredPosition = new Vector2(-20f, -_safeTop * 0.5f);
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
            img.type = Image.Type.Sliced;
            _tabImages[cat] = img;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() => SelectCategory(cat));

            var t = MakeLabel(go, "Label", label, _font, 30,
                              TextAnchor.MiddleCenter, bold: true, shadow: true);
            t.color = Color.white;
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
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
                var spr = active ? GameAssets.ShopTabSelected : GameAssets.ShopTabUnselected;
                if (spr != null) { kv.Value.sprite = spr; kv.Value.color = Color.white; }
                else kv.Value.color = active ? new Color(0.30f, 0.55f, 0.95f, 1f)
                                             : new Color(0.16f, 0.16f, 0.30f, 1f);
                var lbl = kv.Value.GetComponentInChildren<Text>();
                if (lbl != null) lbl.color = active ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            }
        }

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
            if (equipped)       { status = "✓ Equipped"; statusColor = Color.white; }
            else if (unlocked)  { status = "Equip";          statusColor = new Color(0.55f, 0.85f, 1f, 1f); }
            else                { status = $"{item.UnlockCost} coins"; statusColor = CoinGold; }

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
            var price = AddCardLabel(card, $"{item.UnlockCost} coins", 40,
                                     new Vector2(0f, 0.30f), new Vector2(1f, 0.40f), CoinGold);
            price.color = CoinGold;

            // Confirm
            var confirm = CreateButton(card, "Confirm", "BUY", 32,
                                       new Color(0.18f, 0.55f, 0.30f, 1f),
                                       () => ConfirmPurchase(item));
            var cfr = confirm.GetComponent<RectTransform>();
            cfr.anchorMin = new Vector2(0.5f, 0f); cfr.anchorMax = new Vector2(0.5f, 0f);
            cfr.pivot = new Vector2(0.5f, 0f);
            cfr.sizeDelta = new Vector2(300f, 64f);
            cfr.anchoredPosition = new Vector2(0f, 120f);

            // Cancel
            var cancel = CreateButton(card, "Cancel", "CANCEL", 30,
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
                    AudioMgr.Instance?.PlaySFX("button_tap");
                    SkinManager.EquipSkin(item);   // auto-equip on purchase
                    UpdateCoins();
                    ClosePopup();
                    RebuildGrid();
                    ShowToast("Unlocked!");
                    break;
                case PurchaseResult.NotEnoughCoins:
                    ShowToast("Not enough coins");
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
            if (_coinsLabel != null) _coinsLabel.text = $"Coins: {SkinManager.GetCoins()}";
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
    }
}
