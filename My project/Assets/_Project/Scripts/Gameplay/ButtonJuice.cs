using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using BoltSort.Visual;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Reusable tactile press feedback for UI buttons. On pointer-down the target scales to
    /// 88% (fast ease-out) and fires a light haptic; on pointer-up it springs back to its
    /// resting scale with a slight overshoot. The resting scale is captured once on Awake and
    /// every animation runs relative to it, so repeated or interrupted presses never leave a
    /// residual offset. Attach only to buttons that do NOT run their own scale animation (e.g.
    /// an idle attention pulse) — two writers on one RectTransform would fight.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float PressScale      = 0.88f;
        private const float Overshoot       = 1.05f;
        private const float PressDuration   = 0.06f;
        private const float ReleaseDuration = 0.10f;

        private Vector3   _baseScale = Vector3.one;
        private bool      _captured;
        private Coroutine _anim;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _captured  = true;
        }

        private void OnDisable()
        {
            // Cancel mid-press so a button disabled while held never sticks at a shrunken scale.
            if (_anim != null) { StopCoroutine(_anim); _anim = null; }
            if (_captured) transform.localScale = _baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            HapticManager.Light();
            Play(AnimateTo(_baseScale * PressScale, PressDuration, TweenUtility.EaseOutQuad));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Play(Release());
        }

        private void Play(IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(routine);
        }

        private IEnumerator Release()
        {
            yield return AnimateTo(_baseScale * Overshoot, ReleaseDuration * 0.6f, TweenUtility.EaseOutQuad);
            yield return AnimateTo(_baseScale, ReleaseDuration * 0.4f, TweenUtility.EaseInOutQuad);
            _anim = null;
        }

        private IEnumerator AnimateTo(Vector3 target, float dur, Func<float, float> ease)
        {
            Vector3 start   = transform.localScale;
            float   elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime; // animate even while the game is paused
                transform.localScale = Vector3.LerpUnclamped(start, target, ease(Mathf.Clamp01(elapsed / dur)));
                yield return null;
            }
            transform.localScale = target;
        }
    }
}
