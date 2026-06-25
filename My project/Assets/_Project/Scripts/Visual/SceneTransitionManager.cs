using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Visual
{
    /// <summary>
    /// DDOL singleton that manages black-overlay scene transitions.
    /// Subscribe to OnTransitionOut to animate objects out before the scene changes.
    /// Call TransitionTo() in place of SceneManager.LoadScene().
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        /// <summary>Name of the scene that was active when the last transition started.
        /// Lets a destination scene (e.g. Shop) navigate back to its caller without the
        /// caller having to register itself. Null until the first transition.</summary>
        public static string PreviousScene { get; private set; }

        /// <summary>Fired at transition start so BoardView can animate columns out.</summary>
        public static event Action OnTransitionOut;

        private Image _overlay;
        private bool  _transitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();
        }

        private void BuildOverlay()
        {
            // Child of this (already-DDOL root) manager, so it persists across scenes
            // automatically. DontDestroyOnLoad must only be called on the root manager
            // GameObject (see Awake) — calling it on this child warns and is a no-op.
            var canvasGO = new GameObject("TransitionOverlayCanvas", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            canvasGO.AddComponent<GraphicRaycaster>();

            var overlayGO = new GameObject("BlackOverlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            _overlay       = overlayGO.AddComponent<Image>();
            _overlay.color = new Color(0f, 0f, 0f, 0f);
            // CRITICAL: this overlay lives on a sortingOrder 999 DontDestroyOnLoad
            // canvas above every other UI. With raycastTarget on it would silently
            // swallow ALL touches/clicks in every scene (dead buttons everywhere).
            // It is purely a visual fade — it must never intercept input.
            _overlay.raycastTarget = false;

            var rt        = overlayGO.GetComponent<RectTransform>();
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = rt.offsetMax = Vector2.zero;
        }

        public void TransitionTo(string sceneName, float outDuration = 0.30f, float inDuration = 0.40f)
        {
            if (_transitioning) return;
            StartCoroutine(DoTransition(sceneName, outDuration, inDuration));
        }

        private IEnumerator DoTransition(string sceneName, float outDuration, float inDuration)
        {
            _transitioning = true;

            // Notify subscribers (BoardView fly-out) then play whoosh and fade
            OnTransitionOut?.Invoke();
            AudioMgr.Instance?.PlaySFX("scene_whoosh");

            yield return StartCoroutine(FadeOverlay(0f, 1f, outDuration, fadeIn: false));

            // Record the caller before swapping scenes so destinations can navigate back.
            PreviousScene = SceneManager.GetActiveScene().name;

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;

            yield return StartCoroutine(FadeOverlay(1f, 0f, inDuration, fadeIn: true));

            _transitioning = false;
        }

        private IEnumerator FadeOverlay(float from, float to, float duration, bool fadeIn)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float alpha    = fadeIn
                    ? Mathf.Lerp(from, to, TweenUtility.EaseOutQuad(progress))
                    : Mathf.Lerp(from, to, TweenUtility.EaseInQuad(progress));
                _overlay.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }
            _overlay.color = new Color(0f, 0f, 0f, to);
        }
    }
}
