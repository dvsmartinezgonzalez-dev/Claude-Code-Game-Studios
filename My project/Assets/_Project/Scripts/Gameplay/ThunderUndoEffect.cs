using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Cinematic thunder flourish played when the player taps Undo. It plays a brief
    /// lightning animation + sound, and triggers the <b>existing</b> undo action at the
    /// lightning's impact frame — then fades out and re-enables input.
    ///
    /// <para>Purely additive. This component never touches board / undo / move / level logic.
    /// It only wraps a caller-supplied <c>onImpact</c> action (the real undo) with visuals.</para>
    ///
    /// <para><b>Layering:</b> its own ScreenSpaceOverlay Canvas at <see cref="SortingOrder"/> = 150 —
    /// above the gameplay board (world sprites) and the HUD (sortingOrder 100), and below the
    /// settings / pause popup (sortingOrder 200).</para>
    ///
    /// <para><b>Failsafes (undo always matters more than visuals):</b>
    /// <list type="bullet">
    ///   <item>No sliced thunder frames → invoke the undo immediately, no overlay.</item>
    ///   <item>Audio missing or disabled → silent; effect still plays; undo still fires.</item>
    ///   <item><c>onImpact</c> is invoked EXACTLY once per <see cref="Play"/> → never a duplicate undo.</item>
    ///   <item>Any exception during playback still runs the impact + completion callbacks.</item>
    /// </list></para>
    ///
    /// Total wall-clock budget ≈ 0.6s (within the 0.4–0.8s responsiveness target); the undo itself
    /// fires at ≈ 0.3s so the board reacts quickly.
    /// </summary>
    public sealed class ThunderUndoEffect : MonoBehaviour
    {
        // ── Tuning ────────────────────────────────────────────────────────────────
        private const float OverlayFadeInSec  = 0.10f;       // dark overlay fade-in
        private const float OverlayHoldAlpha  = 0.45f;       // 45% dark over the scene
        private const float FrameSec          = 1f / 24f;    // 24 fps, one PNG frame at a time
        private const float ImpactFraction    = 0.69f;       // strike frame ≈ frame 12 of 16 (SFX + flash)
        private const float FlashSec          = 0.07f;       // brief white flash at the strike
        private const float FlashPeakAlpha    = 0.55f;
        private const int   SortingOrder      = 150;         // above HUD(100), below popups(200)

        /// <summary>Folder under Resources holding the 16 individual full-screen thunder PNGs
        /// (named 1..16). Imported as single sprites, NOT a sprite sheet.</summary>
        private const string ThunderFramesDir = "Sprites/Effects/Thunder";
        private const string ThunderSfxName   = "thunder";

        private static readonly int[] FrameOrder = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        private Canvas   _canvas;
        private Image    _overlay;   // dark dim
        private Image    _thunder;   // animated lightning frame
        private Image    _flash;     // white flash
        private Sprite[] _frames;
        private bool     _playing;

        /// <summary>True while the effect is mid-playback. The HUD reads this to block re-presses.</summary>
        public bool IsPlaying => _playing;

        /// <summary>
        /// Factory: creates the effect GameObject as a child of <paramref name="parent"/> and builds
        /// its overlay/canvas. Returns null only if construction throws (caller falls back to plain undo).
        /// </summary>
        public static ThunderUndoEffect Create(Transform parent)
        {
            try
            {
                var go = new GameObject("ThunderUndoEffect");
                go.transform.SetParent(parent, false);
                var fx = go.AddComponent<ThunderUndoEffect>();
                fx.Build();
                return fx;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ThunderUndoEffect] Create failed, undo will run without the effect: {e.Message}");
                return null;
            }
        }

        private void Build()
        {
            // Load the 16 individual full-screen PNGs in sequential order (1..16).
            // Each is its own single sprite under Resources.
            var frames = new System.Collections.Generic.List<Sprite>(FrameOrder.Length);
            foreach (int n in FrameOrder)
            {
                var spr = Resources.Load<Sprite>($"{ThunderFramesDir}/{n}");
                if (spr != null) frames.Add(spr);
            }
            _frames = frames.ToArray();
#if UNITY_EDITOR
            if (_frames == null || _frames.Length < FrameOrder.Length)
                Debug.LogWarning($"[ThunderUndoEffect] Loaded {_frames?.Length ?? 0}/{FrameOrder.Length} thunder " +
                                 $"frame(s) from Resources/{ThunderFramesDir}/. Until all are present, Undo falls " +
                                 "back to no-effect (still works).");
#endif

            // Own overlay canvas, above the HUD and below the settings popup.
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight  = 1f;

            // Raycaster present so the dark overlay swallows UI taps during the brief effect
            // (prevents stray Undo/Settings presses). Disabled with the canvas when idle.
            gameObject.AddComponent<GraphicRaycaster>();

            _overlay = MakeFullScreenImage("Overlay", new Color(0f, 0f, 0f, 0f), raycast: true);
            _thunder = MakeThunderImage();
            _flash   = MakeFullScreenImage("Flash", new Color(1f, 1f, 1f, 0f), raycast: false);

            SetVisible(false);
        }

        /// <summary>
        /// Plays the thunder effect, then runs <paramref name="onImpact"/> (the existing undo) at the
        /// impact frame and <paramref name="onComplete"/> when fully done. Both run exactly once.
        /// If the effect cannot play (no frames), <paramref name="onImpact"/> + <paramref name="onComplete"/>
        /// run immediately so Undo is never blocked.
        /// </summary>
        public void Play(Action onImpact, Action onComplete)
        {
            if (_playing)
            {
                // Defensive: should never happen (HUD guards re-entry). Don't drop the undo.
                SafeInvoke(onImpact);
                SafeInvoke(onComplete);
                return;
            }

            if (_frames == null || _frames.Length == 0 || _canvas == null)
            {
                // FAILSAFE: asset not ready → behave exactly like the original Undo.
                SafeInvoke(onImpact);
                SafeInvoke(onComplete);
                return;
            }

            StartCoroutine(PlayRoutine(onImpact, onComplete));
        }

        private IEnumerator PlayRoutine(Action onImpact, Action onComplete)
        {
            _playing = true;
            bool impactFired = false;

            void FireImpactOnce()
            {
                if (impactFired) return;
                impactFired = true;
                SafeInvoke(onImpact);   // the EXISTING undo
            }

            try
            {
                SetVisible(true);
                _thunder.sprite = _frames[0];
                SetAlpha(_overlay, 0f);
                SetAlpha(_flash, 0f);

                // Dim the scene behind the (transparent-background) frames.
                yield return FadeOverlay(0f, OverlayHoldAlpha, OverlayFadeInSec);

                // Play every frame at 24 fps. The thunder SFX + flash fire on the strike
                // frame (unchanged); the real undo runs AFTER the last frame.
                int impactIdx = Mathf.Clamp(Mathf.RoundToInt(_frames.Length * ImpactFraction),
                                            0, _frames.Length - 1);
                for (int i = 0; i < _frames.Length; i++)
                {
                    _thunder.sprite = _frames[i];
                    if (i == impactIdx)
                    {
                        PlayThunderSfx();
                        StartCoroutine(FlashRoutine());
                    }
                    yield return new WaitForSecondsRealtime(FrameSec);
                }

                // After the last frame: hide the image, THEN execute the undo.
                SetVisible(false);
                FireImpactOnce();
            }
            finally
            {
                FireImpactOnce(); // last-resort guarantee (e.g. coroutine interrupted before the end)
                SetVisible(false);
                _playing = false;
                SafeInvoke(onComplete);
            }
        }

        // ── Sub-effects ───────────────────────────────────────────────────────────
        private void PlayThunderSfx()
        {
            try { AudioMgr.Instance?.PlaySFX(ThunderSfxName); }
            catch (Exception e) { Debug.LogWarning($"[ThunderUndoEffect] thunder SFX failed: {e.Message}"); }
        }

        private IEnumerator FlashRoutine()
        {
            float half = FlashSec * 0.5f;
            yield return FadeImage(_flash, 0f, FlashPeakAlpha, half);
            yield return FadeImage(_flash, FlashPeakAlpha, 0f, half);
        }

        private IEnumerator FadeOverlay(float from, float to, float dur) => FadeImage(_overlay, from, to, dur);

        private IEnumerator FadeImage(Image img, float from, float to, float dur)
        {
            if (img == null) yield break;
            if (dur <= 0f) { SetAlpha(img, to); yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(img, Mathf.Lerp(from, to, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            SetAlpha(img, to);
        }

        // ── Build helpers ─────────────────────────────────────────────────────────
        private Image MakeFullScreenImage(string name, Color color, bool raycast)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = raycast;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        private Image MakeThunderImage()
        {
            var go = new GameObject("Thunder");
            go.transform.SetParent(_canvas.transform, false);
            var img = go.AddComponent<Image>();
            img.color          = Color.white;
            img.type           = Image.Type.Simple;  // one frame at a time — never Tiled
            img.preserveAspect = false;               // fill the whole screen (no letterbox)
            img.raycastTarget  = false;               // must not block input
            // Stretch to fill the entire screen on any resolution: anchors 0,0 → 1,1, zero offsets.
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
            if (!visible)
            {
                SetAlpha(_overlay, 0f);
                SetAlpha(_flash, 0f);
            }
        }

        private static void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            var c = img.color; c.a = a; img.color = c;
        }

        private static void SafeInvoke(Action a)
        {
            if (a == null) return;
            try { a(); }
            catch (Exception e) { Debug.LogError($"[ThunderUndoEffect] callback threw: {e}"); }
        }
    }
}
