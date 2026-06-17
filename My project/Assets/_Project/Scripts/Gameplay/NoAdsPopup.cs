using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Reusable "Remove Ads" modal. Call Initialize() once after the parent Canvas exists,
    /// then Toggle()/Open() to show. Opened from both the MainMenu top-right icon and the
    /// Settings "No Ads" button. Closes via the exit button or by tapping outside the card.
    /// Purchase is a placeholder until Unity IAP is wired (Launch milestone).
    /// </summary>
    public class NoAdsPopup : MonoBehaviour
    {
        private GameObject    _overlay;
        private Font          _font;
        private RectTransform _iconRt;

        public bool IsOpen => _overlay != null && _overlay.activeSelf;

        public void Initialize(Font font, Transform canvasRoot)
        {
            _font = font;
            BuildOverlay(canvasRoot);
            _overlay.SetActive(false);
            StartCoroutine(PulseIcon());
        }

        public void Open()
        {
            if (_overlay != null) _overlay.SetActive(true);
        }

        public void Toggle()
        {
            if (_overlay != null) _overlay.SetActive(!_overlay.activeSelf);
        }

        private void BuildOverlay(Transform canvasRoot)
        {
            _overlay = new GameObject("NoAdsOverlay");
            _overlay.transform.SetParent(canvasRoot, false);
            _overlay.transform.SetAsLastSibling();

            // Full-screen dim — tapping outside the card closes the popup.
            var dimImg = _overlay.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            Stretch(_overlay.GetComponent<RectTransform>());
            _overlay.AddComponent<Button>()
                    .onClick.AddListener(() => _overlay.SetActive(false));

            // Card
            var card = new GameObject("Card");
            card.transform.SetParent(_overlay.transform, false);
            var cardBg = card.AddComponent<Image>();
            var popupSpr = GameAssets.MenuPopup;
            if (popupSpr != null) { cardBg.sprite = popupSpr; cardBg.color = Color.white; cardBg.type = Image.Type.Simple; }
            else cardBg.color = new Color(0.07f, 0.07f, 0.14f, 0.98f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot     = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = new Vector2(560f, 620f);

            // Consume taps on the card so they don't fall through to the dim closer.
            var cardBtn = card.AddComponent<Button>();
            var cbc = cardBtn.colors;
            cbc.normalColor = cbc.highlightedColor = cbc.pressedColor = cbc.selectedColor = Color.white;
            cardBtn.colors = cbc;

            // Title
            var title = AddLabel(card, "Title", "Eliminar anuncios", 44,
                                 new Vector2(0f, 0.82f), new Vector2(1f, 0.96f));
            title.color = BoltSortTheme.WinGold;

            // Animated no_ads icon
            var iconGO = new GameObject("NoAdsIcon");
            iconGO.transform.SetParent(card.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            if (GameAssets.MenuNoAds != null)
                GameAssets.Apply(iconImg, GameAssets.MenuNoAds, preserveAspect: true);
            else
                iconImg.color = new Color(0.9f, 0.3f, 0.3f, 1f);
            iconImg.raycastTarget = false;
            _iconRt = iconGO.GetComponent<RectTransform>();
            _iconRt.anchorMin = _iconRt.anchorMax = new Vector2(0.5f, 0.66f);
            _iconRt.pivot     = new Vector2(0.5f, 0.5f);
            _iconRt.anchoredPosition = Vector2.zero;
            _iconRt.sizeDelta        = new Vector2(150f, 150f);

            // Description
            var desc = AddLabel(card, "Desc",
                "¡Di adiós a los anuncios emergentes y banners forzados para siempre!",
                26, new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.5f));
            desc.color = new Color(1f, 1f, 1f, 0.9f);

            // Buy button — general_button.png + "4,99€"
            var buyGo = new GameObject("BuyButton");
            buyGo.transform.SetParent(card.transform, false);
            var buyImg = buyGo.AddComponent<Image>();
            if (GameAssets.MenuButton != null)
                GameAssets.Apply(buyImg, GameAssets.MenuButton, preserveAspect: true);
            else
                buyImg.color = new Color(0.20f, 0.65f, 0.30f, 1f);
            var buyBtn = buyGo.AddComponent<Button>();
            buyBtn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            buyBtn.onClick.AddListener(OnPurchase);
            var buyRt = buyGo.GetComponent<RectTransform>();
            buyRt.anchorMin = buyRt.anchorMax = new Vector2(0.5f, 0f);
            buyRt.pivot     = new Vector2(0.5f, 0f);
            buyRt.anchoredPosition = new Vector2(0f, 40f);
            buyRt.sizeDelta        = new Vector2(300f, 96f);
            var buyLabel = AddLabel(buyGo, "Label", "4,99€", 40, Vector2.zero, Vector2.one);
            buyLabel.color = Color.white;

            // Close button — exit_button.png, top-right corner of card.
            var closeGo = new GameObject("CloseButton");
            closeGo.transform.SetParent(card.transform, false);
            var closeImg = closeGo.AddComponent<Image>();
            if (GameAssets.BtnExit != null)
                GameAssets.Apply(closeImg, GameAssets.BtnExit, preserveAspect: true);
            else
                closeImg.color = new Color(0.7f, 0.2f, 0.2f, 1f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            closeBtn.onClick.AddListener(() => _overlay.SetActive(false));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot     = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-12f, -12f);
            closeRt.sizeDelta        = new Vector2(72f, 72f);
        }

        private void OnPurchase()
        {
            // Placeholder — real entitlement flow arrives with Unity IAP (Launch milestone).
            Debug.Log("[NoAdsPopup] Purchase tapped (IAP not yet wired).");
        }

        // Subtle looping scale pulse on the icon so it is not static.
        private IEnumerator PulseIcon()
        {
            float elapsed = 0f;
            while (_iconRt != null)
            {
                elapsed += Time.deltaTime;
                float s = 1f + Mathf.Sin(elapsed * Mathf.PI * 1.2f) * 0.07f;
                _iconRt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private Text AddLabel(GameObject parent, string name, string text, int size,
                              Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = _font; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.supportRichText = false; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
