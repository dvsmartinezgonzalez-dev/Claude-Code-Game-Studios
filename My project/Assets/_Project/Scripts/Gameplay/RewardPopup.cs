using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Full-screen epic reward reveal shown when a power-up drops after level completion.
    /// Dims the screen, animates the reward in with elastic ease-out, plays confetti and
    /// a glow effect, then waits for a player tap before fading out.
    /// </summary>
    public class RewardPopup : MonoBehaviour
    {
        private static Sprite _circle;

        private Image          _dimOverlay;
        private RectTransform  _containerRt;
        private bool           _canDismiss;
        private bool           _dismissing;

        /// <summary>Creates and plays a full-screen reward reveal. Safe to call with a null font or sprite.</summary>
        public static void Show(Font font, string title, Color accent, Sprite icon = null)
        {
            try
            {
                var go = new GameObject("RewardPopup");
                go.AddComponent<RewardPopup>().Build(font, title, accent, icon);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RewardPopup] Build failed: {e.Message}");
            }
        }

        private void Build(Font font, string title, Color accent, Sprite icon)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight  = 1f;
            gameObject.AddComponent<GraphicRaycaster>();

            Font f = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ── Full-screen dim — blocks input, tap-to-dismiss ────────────────────
            var dimGO  = new GameObject("Dim");
            dimGO.transform.SetParent(transform, false);
            _dimOverlay = dimGO.AddComponent<Image>();
            _dimOverlay.color = Color.clear;
            var dimRt  = dimGO.GetComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;

            var dimBtn = dimGO.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(OnTap);

            // ── Confetti host (behind the card) ───────────────────────────────────
            var confHost = new GameObject("ConfettiHost");
            confHost.transform.SetParent(transform, false);
            var confHostImg = confHost.AddComponent<Image>();
            confHostImg.color = Color.clear;
            confHostImg.raycastTarget = false;
            var confHostRt = confHost.GetComponent<RectTransform>();
            confHostRt.anchorMin = Vector2.zero; confHostRt.anchorMax = Vector2.one;
            confHostRt.offsetMin = confHostRt.offsetMax = Vector2.zero;

            // ── Center container (glowing card) ───────────────────────────────────
            var containerGO = new GameObject("Container");
            containerGO.transform.SetParent(transform, false);
            var containerImg = containerGO.AddComponent<Image>();
            containerImg.raycastTarget = false;
            if (GameAssets.MenuButton != null)
            {
                containerImg.sprite        = GameAssets.MenuButton;
                containerImg.type          = Image.Type.Sliced;
                containerImg.preserveAspect = false;
            }
            containerImg.color = new Color(0.06f, 0.06f, 0.16f, 0.97f);
            _containerRt = containerGO.GetComponent<RectTransform>();
            _containerRt.anchorMin        = _containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            _containerRt.pivot            = new Vector2(0.5f, 0.5f);
            _containerRt.anchoredPosition = Vector2.zero;
            _containerRt.sizeDelta        = new Vector2(580f, 700f);
            _containerRt.localScale       = Vector3.zero;

            // Outer glow rim (looping accent pulse)
            var glowGO  = new GameObject("Glow");
            glowGO.transform.SetParent(containerGO.transform, false);
            var glowImg = glowGO.AddComponent<Image>();
            glowImg.sprite        = Circle();
            glowImg.color         = new Color(accent.r, accent.g, accent.b, 0.20f);
            glowImg.raycastTarget = false;
            var glowRt  = glowGO.GetComponent<RectTransform>();
            glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.pivot     = new Vector2(0.5f, 0.5f);
            glowRt.anchoredPosition = new Vector2(0f, 110f);
            glowRt.sizeDelta        = new Vector2(340f, 340f);

            // Power-up icon (large)
            var iconGO  = new GameObject("Icon");
            iconGO.transform.SetParent(containerGO.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            if (icon != null) { iconImg.sprite = icon; iconImg.color = Color.white; iconImg.preserveAspect = true; }
            else              { iconImg.sprite = Circle(); iconImg.color = accent; }
            iconImg.raycastTarget = false;
            var iconRt  = iconGO.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot     = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0f, 120f);
            iconRt.sizeDelta        = new Vector2(230f, 230f);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(containerGO.transform, false);
            var titleT  = titleGO.AddComponent<Text>();
            titleT.text            = title;
            titleT.font            = f;
            titleT.fontSize        = 58;
            titleT.fontStyle       = FontStyle.Bold;
            titleT.alignment       = TextAnchor.MiddleCenter;
            titleT.color           = Color.white;
            titleT.supportRichText = false;
            titleT.raycastTarget   = false;
            titleT.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleT.verticalOverflow   = VerticalWrapMode.Overflow;
            var titleOl = titleGO.AddComponent<Outline>();
            titleOl.effectColor    = new Color(0f, 0f, 0f, 0.9f);
            titleOl.effectDistance = new Vector2(3f, -3f);
            var titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.pivot     = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, -90f);
            titleRt.sizeDelta        = new Vector2(520f, 80f);

            // "Tap to continue" subtitle
            string tapLabel = LocalizationManager.Instance?.Get("key_tap_continue") ?? "";
            if (string.IsNullOrEmpty(tapLabel) || tapLabel == "key_tap_continue")
                tapLabel = "Tap to continue";
            var subGO  = new GameObject("Subtitle");
            subGO.transform.SetParent(containerGO.transform, false);
            var subT   = subGO.AddComponent<Text>();
            subT.text            = tapLabel;
            subT.font            = f;
            subT.fontSize        = 30;
            subT.alignment       = TextAnchor.MiddleCenter;
            subT.color           = new Color(1f, 1f, 1f, 0.65f);
            subT.supportRichText = false;
            subT.raycastTarget   = false;
            var subRt  = subGO.GetComponent<RectTransform>();
            subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 0.5f);
            subRt.pivot     = new Vector2(0.5f, 0.5f);
            subRt.anchoredPosition = new Vector2(0f, -230f);
            subRt.sizeDelta        = new Vector2(480f, 50f);

            // ── Kick off the animation ────────────────────────────────────────────
            StartCoroutine(AnimateIn(confHostRt, iconRt, glowImg, accent));
        }

        private void OnTap()
        {
            if (!_canDismiss || _dismissing) return;
            _dismissing = true;
            StartCoroutine(AnimateOut());
        }

        private IEnumerator AnimateIn(RectTransform confHost, RectTransform iconRt, Image glowImg, Color accent)
        {
            // Dim fade-in
            float elapsed = 0f;
            while (elapsed < 0.20f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_dimOverlay != null)
                    _dimOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.72f, elapsed / 0.20f));
                yield return null;
            }
            if (_dimOverlay != null) _dimOverlay.color = new Color(0f, 0f, 0f, 0.72f);

            // Brief screen flash
            StartCoroutine(ScreenFlash());
            AudioMgr.Instance?.PlaySFX("button_tap");

            // Container elastic scale-in
            elapsed = 0f;
            while (elapsed < 0.45f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / 0.45f));
                if (_containerRt != null) _containerRt.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, t);
                yield return null;
            }
            if (_containerRt != null) _containerRt.localScale = Vector3.one;

            // Confetti burst
            var sprites = GameAssets.ConfettiPieces;
            if (sprites != null && sprites.Length > 0)
                StartCoroutine(BurstConfetti(confHost, sprites));

            // Idle animations (run until dismissed)
            StartCoroutine(IconIdleAnim(iconRt));
            StartCoroutine(GlowIdleAnim(glowImg));

            // Minimum hold before player can dismiss (~2.5 s total including the 0.65s intro)
            yield return new WaitForSecondsRealtime(1.85f);
            _canDismiss = true;
        }

        private IEnumerator ScreenFlash()
        {
            var go  = new GameObject("Flash");
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = false;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            float t = 0f;
            while (t < 0.08f) { t += Time.unscaledDeltaTime; img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.55f, t / 0.08f)); yield return null; }
            t = 0f;
            while (t < 0.22f) { t += Time.unscaledDeltaTime; img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 0f, t / 0.22f)); yield return null; }
            Destroy(go);
        }

        private IEnumerator IconIdleAnim(RectTransform rt)
        {
            if (rt == null) yield break;
            float t = 0f;
            while (!_dismissing && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float s = 1f + 0.06f * Mathf.Sin(t * 3.0f);
                float r = 5f * Mathf.Sin(t * 2.1f);
                rt.localScale    = new Vector3(s, s, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f, r);
                yield return null;
            }
            if (rt != null) { rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity; }
        }

        private IEnumerator GlowIdleAnim(Image img)
        {
            if (img == null) yield break;
            float t = 0f;
            var   bc = img.color;
            while (!_dismissing && img != null)
            {
                t += Time.unscaledDeltaTime;
                float alpha = 0.12f + 0.14f * Mathf.Sin(t * 1.8f);
                img.color = new Color(bc.r, bc.g, bc.b, alpha);
                float s = 1f + 0.07f * Mathf.Sin(t * 1.4f);
                img.rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private IEnumerator BurstConfetti(RectTransform host, Sprite[] sprites)
        {
            const int count = 22;
            for (int i = 0; i < count; i++)
            {
                var go  = new GameObject($"Conf_{i}");
                go.transform.SetParent(host, false);
                var img = go.AddComponent<Image>();
                img.sprite        = sprites[UnityEngine.Random.Range(0, sprites.Length)];
                img.color         = Color.white;
                img.raycastTarget = false;
                var rt  = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = new Vector2(22f, 22f);
                rt.anchoredPosition = new Vector2(
                    UnityEngine.Random.Range(-120f, 120f),
                    UnityEngine.Random.Range(-60f, 80f));
                StartCoroutine(FlyConfetti(rt, img,
                    UnityEngine.Random.Range(200f, 420f),
                    UnityEngine.Random.Range(-300f, 300f),
                    UnityEngine.Random.Range(1.6f, 3.0f)));
                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        private IEnumerator FlyConfetti(RectTransform rt, Image img, float speed, float rotSpeed, float life)
        {
            float elapsed = 0f;
            while (elapsed < life && rt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var pos = rt.anchoredPosition;
                pos.y -= speed * Time.unscaledDeltaTime;
                rt.anchoredPosition = pos;
                rt.Rotate(0f, 0f, rotSpeed * Time.unscaledDeltaTime);
                float a = 1f - Mathf.Clamp01((elapsed - life * 0.65f) / (life * 0.35f));
                img.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        private IEnumerator AnimateOut()
        {
            Vector3 startScale = _containerRt != null ? _containerRt.localScale : Vector3.one;
            Color   dimStart   = _dimOverlay  != null ? _dimOverlay.color : Color.clear;
            float   t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / 0.22f);
                if (_containerRt != null) _containerRt.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
                if (_dimOverlay  != null) _dimOverlay.color       = Color.Lerp(dimStart, Color.clear, p);
                yield return null;
            }
            Destroy(gameObject);
        }

        private static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float c = size * 0.5f, r = size * 0.46f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    px[y * size + x] = new Color(1f, 1f, 1f,
                        Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy)));
                }
            tex.SetPixels(px); tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return _circle;
        }
    }
}
