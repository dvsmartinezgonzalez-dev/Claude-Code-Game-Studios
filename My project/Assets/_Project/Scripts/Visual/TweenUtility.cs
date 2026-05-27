using System;
using System.Collections;
using UnityEngine;

namespace BoltSort.Visual
{
    public static class TweenUtility
    {
        public static IEnumerator LerpPosition(Transform t, Vector3 target, float duration,
                                               Func<float, float> easing)
        {
            Vector3 start   = t.localPosition;
            float   elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                t.localPosition = Vector3.LerpUnclamped(start, target,
                    easing(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            if (t != null) t.localPosition = target;
        }

        public static IEnumerator LerpScale(Transform t, Vector3 target, float duration,
                                            Func<float, float> easing)
        {
            Vector3 start   = t.localScale;
            float   elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                t.localScale = Vector3.LerpUnclamped(start, target,
                    easing(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            if (t != null) t.localScale = target;
        }

        public static IEnumerator LerpColor(SpriteRenderer sr, Color target, float duration,
                                            Func<float, float> easing)
        {
            Color start   = sr.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (sr == null) yield break;
                elapsed += Time.deltaTime;
                sr.color = Color.LerpUnclamped(start, target,
                    easing(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            if (sr != null) sr.color = target;
        }

        public static IEnumerator LerpImageColor(UnityEngine.UI.Image img, Color target,
                                                  float duration, Func<float, float> easing)
        {
            Color start   = img.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (img == null) yield break;
                elapsed += Time.deltaTime;
                img.color = Color.LerpUnclamped(start, target,
                    easing(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            if (img != null) img.color = target;
        }

        public static IEnumerator LerpRectScale(RectTransform rt, Vector3 target, float duration,
                                                 Func<float, float> easing)
        {
            Vector3 start   = rt.localScale;
            float   elapsed = 0f;
            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                rt.localScale = Vector3.LerpUnclamped(start, target,
                    easing(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            if (rt != null) rt.localScale = target;
        }

        // ── Easing functions ─────────────────────────────────────────────────────

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float EaseInOutQuad(float t)
            => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        public static float EaseOutQuad(float t)  => 1f - (1f - t) * (1f - t);
        public static float EaseInQuad(float t)   => t * t;

        public static float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1)         return n1 * t * t;
            if (t < 2f / d1)         { t -= 1.5f / d1;   return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1)       { t -= 2.25f / d1;  return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;        return n1 * t * t + 0.984375f;
        }

        public static float EaseInBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        public static float Linear(float t) => t;
    }
}
