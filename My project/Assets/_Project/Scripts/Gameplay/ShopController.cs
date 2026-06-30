using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Modal shop overlay. Attach to any GameObject, call Initialize() after the parent
    /// Canvas is created, then Toggle() to show/hide. Persists selected skin to PlayerPrefs.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        private const string SelectedSkinKey = "bs.selected_skin";

        private static readonly List<SkinDef> Skins = new()
        {
            new SkinDef("Classic",   "#3A7BD5", null),
            new SkinDef("Gold",      "#F5A623", null),
            new SkinDef("Midnight",  "#1A1A3A", null),
            new SkinDef("Neon",      "#00FF88", null),
            new SkinDef("Diamond",   "#B0E0FF", "Sprites/Diamante"),
            new SkinDef("Champion",  "#FFD700", "Sprites/Trofeo"),
        };

        private readonly struct SkinDef
        {
            public readonly string Name;
            public readonly string HexColor;
            public readonly string SpritePath;
            public SkinDef(string name, string hex, string sprite)
            { Name = name; HexColor = hex; SpritePath = sprite; }
        }

        private GameObject _overlay;
        private Font       _font;
        private readonly List<GameObject> _skinCells = new();

        public void Initialize(Font font, Transform canvasRoot)
        {
            _font = font;
            BuildOverlay(canvasRoot);
            _overlay.SetActive(false);
        }

        public void Toggle()
        {
            if (_overlay != null)
                _overlay.SetActive(!_overlay.activeSelf);
        }

        private void BuildOverlay(Transform canvasRoot)
        {
            _overlay = new GameObject("ShopOverlay");
            _overlay.transform.SetParent(canvasRoot, false);

            // Full-screen dim
            var dimImg = _overlay.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            Stretch(_overlay.GetComponent<RectTransform>());

            var dimBtn = _overlay.AddComponent<Button>();
            dimBtn.onClick.AddListener(() => _overlay.SetActive(false));

            // Card
            var card = new GameObject("ShopCard");
            card.transform.SetParent(_overlay.transform, false);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.07f, 0.07f, 0.14f, 0.98f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRt.pivot            = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = new Vector2(560f, 740f);

            // Block clicks on card from closing overlay
            var cardBtn = card.AddComponent<Button>();
            cardBtn.onClick.AddListener(() => { }); // consume tap
            var cs = cardBtn.colors;
            cs.normalColor = Color.white; cs.highlightedColor = Color.white;
            cs.pressedColor = Color.white; cs.selectedColor = Color.white;
            cardBtn.colors = cs;

            // Title
            AddLabel(card, "Title", "SHOP", 60, new Vector2(0f, 0.88f), new Vector2(1f, 1f));

            // Diamond icon + "Coming Soon" subtitle
            var iconGO = new GameObject("DiamondIcon");
            iconGO.transform.SetParent(card.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            GameAssets.Apply(iconImg, GameAssets.DiamondIcon, preserveAspect: true);
            if (GameAssets.DiamondIcon == null) iconImg.color = new Color(0.5f, 0.8f, 1f, 0.8f);
            var irt = iconGO.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.82f);
            irt.pivot     = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(-80f, 0f);
            irt.sizeDelta        = new Vector2(50f, 50f);
            iconImg.raycastTarget = false;

            AddLabel(card, "Sub", "Select a skin:", 30, new Vector2(0f, 0.76f), new Vector2(1f, 0.82f));

            // SH-03: coin balance display
            var ss    = BoltSort.SaveSystem.SaveSystem.Instance;
            int coins = (ss != null && ss.IsReady) ? ss.GetCoinBalance() : PlayerPrefs.GetInt("bs.coin_balance", 0);
            var coinsLabel = AddLabel(card, "CoinsBalance", $"Coins: {coins}", 28,
                new Vector2(0f, 0.70f), new Vector2(1f, 0.76f));
            coinsLabel.color = new Color(1f, 0.85f, 0.2f, 1f);

            // Skin grid — 2 columns x 3 rows
            BuildSkinGrid(card);

            // Close button
            var closeGo = CreateButton(card, "Close", "CLOSE", 34,
                                       new Color(0.22f, 0.22f, 0.42f, 1f),
                                       () => _overlay.SetActive(false));
            var cr = closeGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.5f, 0f); cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot     = new Vector2(0.5f, 0f);
            cr.anchoredPosition = new Vector2(0f, 24f);
            cr.sizeDelta        = new Vector2(260f, 60f);
        }

        private void BuildSkinGrid(GameObject card)
        {
            int selected = PlayerPrefs.GetInt(SelectedSkinKey, 0);
            const int cols = 2;
            const float cellW = 220f, cellH = 80f, gapX = 12f, gapY = 12f;
            float startY = -130f;

            for (int i = 0; i < Skins.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float x = (col == 0 ? -(cellW * 0.5f + gapX * 0.5f) : cellW * 0.5f + gapX * 0.5f);
                float y = startY - row * (cellH + gapY);

                var skin = Skins[i];
                bool isSelected = i == selected;

                var cellGO = new GameObject($"Skin_{i}");
                cellGO.transform.SetParent(card.transform, false);
                var cellImg = cellGO.AddComponent<Image>();
                cellImg.color = isSelected
                    ? new Color(0.20f, 0.65f, 0.30f, 1f)
                    : new Color(0.15f, 0.15f, 0.28f, 1f);
                var cellRt = cellGO.GetComponent<RectTransform>();
                cellRt.anchorMin = cellRt.anchorMax = new Vector2(0.5f, 1f);
                cellRt.pivot     = new Vector2(0.5f, 1f);
                cellRt.anchoredPosition = new Vector2(x, y);
                cellRt.sizeDelta        = new Vector2(cellW, cellH);

                // Skin icon (sprite if available)
                if (!string.IsNullOrEmpty(skin.SpritePath))
                {
                    var sIconGO = new GameObject("Icon");
                    sIconGO.transform.SetParent(cellGO.transform, false);
                    var sIconImg = sIconGO.AddComponent<Image>();
                    var spr = Resources.Load<Sprite>(skin.SpritePath);
                    if (spr != null) { sIconImg.sprite = spr; sIconImg.color = Color.white; sIconImg.preserveAspect = true; }
                    else sIconImg.color = BoltSortTheme.HexColor(skin.HexColor.TrimStart('#'));
                    var sirt = sIconGO.GetComponent<RectTransform>();
                    sirt.anchorMin = new Vector2(0f, 0f); sirt.anchorMax = new Vector2(0f, 1f);
                    sirt.pivot     = new Vector2(0f, 0.5f);
                    sirt.offsetMin = new Vector2(8f, 8f); sirt.offsetMax = new Vector2(70f, -8f);
                    sIconImg.raycastTarget = false;
                }
                else
                {
                    // Color swatch
                    var swatchGO = new GameObject("Swatch");
                    swatchGO.transform.SetParent(cellGO.transform, false);
                    var swatchImg = swatchGO.AddComponent<Image>();
                    swatchImg.color = BoltSortTheme.HexColor(skin.HexColor.TrimStart('#'));
                    var swrt = swatchGO.GetComponent<RectTransform>();
                    swrt.anchorMin = new Vector2(0f, 0f); swrt.anchorMax = new Vector2(0f, 1f);
                    swrt.pivot     = new Vector2(0f, 0.5f);
                    swrt.offsetMin = new Vector2(10f, 10f); swrt.offsetMax = new Vector2(50f, -10f);
                    swatchImg.raycastTarget = false;
                }

                // Skin name label
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(cellGO.transform, false);
                var lt = labelGO.AddComponent<Text>();
                lt.text = skin.Name; lt.font = _font; lt.fontSize = 28;
                lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleLeft;
                lt.color = Color.white; lt.supportRichText = false;
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(72f, 0f); lrt.offsetMax = new Vector2(-8f, 0f);

                // Selected checkmark
                if (isSelected)
                {
                    var chkGO = new GameObject("Check");
                    chkGO.transform.SetParent(cellGO.transform, false);
                    var chkT = chkGO.AddComponent<Text>();
                    chkT.text = "✓"; chkT.font = _font; chkT.fontSize = 30;
                    chkT.fontStyle = FontStyle.Bold; chkT.alignment = TextAnchor.MiddleRight;
                    chkT.color = Color.white; chkT.supportRichText = false;
                    var chkRt = chkGO.GetComponent<RectTransform>();
                    chkRt.anchorMin = Vector2.zero; chkRt.anchorMax = Vector2.one;
                    chkRt.offsetMin = new Vector2(0f, 0f); chkRt.offsetMax = new Vector2(-8f, 0f);
                    chkT.raycastTarget = false;
                }

                int captured = i;
                var btn = cellGO.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    AudioMgr.Instance?.PlaySFX("button_tap");
                    SelectSkin(captured);
                });
                _skinCells.Add(cellGO);
            }
        }

        private void SelectSkin(int index)
        {
            PlayerPrefs.SetInt(SelectedSkinKey, index);
            PlayerPrefs.Save();
            RefreshCells(index);
        }

        private void RefreshCells(int selected)
        {
            for (int i = 0; i < _skinCells.Count; i++)
            {
                if (_skinCells[i] == null) continue;
                var img = _skinCells[i].GetComponent<Image>();
                if (img != null)
                    img.color = i == selected
                        ? new Color(0.20f, 0.65f, 0.30f, 1f)
                        : new Color(0.15f, 0.15f, 0.28f, 1f);
            }
        }

        private Text AddLabel(GameObject parent, string name, string text, int fontSize,
                              Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = _font; t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.supportRichText = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        private GameObject CreateButton(GameObject parent, string name, string label,
                                        int fontSize, Color bgColor, System.Action onClick)
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
            t.text = label; t.font = _font; t.fontSize = fontSize;
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
