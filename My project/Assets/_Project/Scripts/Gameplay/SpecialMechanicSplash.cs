using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// One-shot full-screen splash announcing a special mechanic (frozen tube, multicolor ball,
    /// mystery ball). Identical structure to <see cref="MagnetUnlockSplash"/>: same background,
    /// bounce-in animation, pulsing icon, and LET'S GO! button. Parameterized so all three
    /// mechanics share a single implementation.
    /// </summary>
    public class SpecialMechanicSplash : MonoBehaviour
    {
        private Action _onDismiss;
        private RectTransform _cardRt;
        private RectTransform _iconRt;
        private bool _dismissed;
        private static Sprite _circleSprite;

        /// <summary>Creates and shows the splash. <paramref name="onDismiss"/> runs when LET'S GO!
        /// is tapped, or immediately if the overlay fails to build.</summary>
        public static SpecialMechanicSplash Show(Font font, Sprite icon,
                                                  string titleKey, string bodyKey,
                                                  Action onDismiss)
        {
            try
            {
                var go     = new GameObject("SpecialMechanicSplash");
                var splash = go.AddComponent<SpecialMechanicSplash>();
                splash.Build(font, icon, titleKey, bodyKey, onDismiss);
                return splash;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SpecialMechanicSplash] Build failed, dismissing directly: {e.Message}");
                onDismiss?.Invoke();
                return null;
            }
        }

        private void Build(Font font, Sprite icon, string titleKey, string bodyKey, Action onDismiss)
        {
            _onDismiss = onDismiss;

            // Canvas — above gameplay HUD, same order as MagnetUnlockSplash.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Full-screen dim.
            var dim = new GameObject("Dim");
            dim.transform.SetParent(transform, false);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.78f);
            Stretch(dim.GetComponent<RectTransform>());

            // Card — same background as MagnetUnlockSplash.
            var card    = new GameObject("Card");
            card.transform.SetParent(transform, false);
            var cardImg = card.AddComponent<Image>();
            var bgSpr   = GameAssets.SettingsBackground ?? GameAssets.MenuPopup;
            if (bgSpr != null) { cardImg.sprite = bgSpr; cardImg.color = Color.white; }
            else cardImg.color = new Color(0.10f, 0.10f, 0.20f, 0.98f);
            _cardRt = card.GetComponent<RectTransform>();
            _cardRt.anchorMin = _cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            _cardRt.pivot     = new Vector2(0.5f, 0.5f);
            _cardRt.anchoredPosition = Vector2.zero;
            _cardRt.sizeDelta        = new Vector2(820f, 980f);

            // Icon — mechanic-specific sprite, pulsed by PulseIcon coroutine.
            var iconGO  = new GameObject("Icon");
            iconGO.transform.SetParent(card.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            if (icon != null) GameAssets.Apply(iconImg, icon, preserveAspect: true);
            else { iconImg.sprite = CircleSprite(); iconImg.color = new Color(0.55f, 0.75f, 1f, 1f); }
            iconImg.raycastTarget = false;
            _iconRt = iconGO.GetComponent<RectTransform>();
            _iconRt.anchorMin = _iconRt.anchorMax = new Vector2(0.5f, 0.74f);
            _iconRt.pivot     = new Vector2(0.5f, 0.5f);
            _iconRt.anchoredPosition = Vector2.zero;
            _iconRt.sizeDelta        = new Vector2(260f, 260f);

            // Title.
            LocalizedText.Bind(
                AddLabel(card, font, "Title", Tr(titleKey), 66,
                         new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.64f),
                         BoltSortTheme.WinGold, outline: true),
                titleKey);

            // Body.
            LocalizedText.Bind(
                AddLabel(card, font, "Body", Tr(bodyKey), 40,
                         new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.48f),
                         Color.black, outline: false),
                bodyKey);

            // LET'S GO! button — identical to MagnetUnlockSplash.
            var btnGO  = new GameObject("LetsGoButton");
            btnGO.transform.SetParent(card.transform, false);
            var btnImg = btnGO.AddComponent<Image>();
            var btnSpr = GameAssets.MenuButton;
            if (btnSpr != null) GameAssets.Apply(btnImg, btnSpr, preserveAspect: true);
            else btnImg.color = new Color(0.20f, 0.65f, 0.30f, 1f);
            var btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(Dismiss);
            var btnRt = btnGO.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot     = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 80f);
            btnRt.sizeDelta        = new Vector2(440f, 140f);
            LocalizedText.Bind(
                AddLabel(btnGO, font, "Label", Tr("key_lets_go"), 48,
                         Vector2.zero, Vector2.one, Color.white, outline: false),
                "key_lets_go");

            StartCoroutine(ScaleIn());
            StartCoroutine(PulseIcon());
        }

        private void Dismiss()
        {
            if (_dismissed) return;
            _dismissed = true;
            AudioMgr.Instance?.PlaySFX("button_tap");
            var cb = _onDismiss; _onDismiss = null;
            cb?.Invoke();
            Destroy(gameObject);
        }

        // Card scales 0 → 1 with a bounce ease, identical to MagnetUnlockSplash.
        private IEnumerator ScaleIn()
        {
            if (_cardRt == null) yield break;
            _cardRt.localScale = Vector3.zero;
            float dur = 0.32f, elapsed = 0f;
            while (elapsed < dur)
            {
                if (_cardRt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / dur));
                _cardRt.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, t);
                yield return null;
            }
            if (_cardRt != null) _cardRt.localScale = Vector3.one;
        }

        // Idle pulse on the icon (1.0 ↔ 1.08, ~1 s loop).
        private IEnumerator PulseIcon()
        {
            float elapsed = 0f;
            while (_iconRt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float s = 1f + Mathf.Sin(elapsed * Mathf.PI * 2f) * 0.04f;
                _iconRt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private static Text AddLabel(GameObject parent, Font font, string name, string text,
                                     int size, Vector2 anchorMin, Vector2 anchorMax,
                                     Color color, bool outline)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = color; t.supportRichText = false; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (outline)
            {
                var ol = go.AddComponent<Outline>();
                ol.effectColor    = new Color(0f, 0f, 0f, 0.9f);
                ol.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float c = size * 0.5f, r = size * 0.46f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return _circleSprite;
        }

        private static string Tr(string key)
            => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key) : key;
    }
}
