using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Canvas-based HUD for portrait mobile. All sizes reference 720×1280 logical pixels
    /// (ScaleWithScreenSize, match-height). Includes animated move counter, button
    /// press feedback, and a slide-in win overlay.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────────
        private BoltSort.GameStateManager.GameStateManager _gsm;
        private bool   _levelComplete;
        private bool   _deadlock;
        private Action _onReset;
        private Action _onNextLevel;
        private Action _onUndo;
        private Action _onMenu;
        private Action _onReplay;
        private Action _onLevels;

        // ── Live UI refs ──────────────────────────────────────────────────────────
        private Text        _levelText;
        private Text        _movesText;
        private RectTransform _movesRT;
        private Text        _coinPillText;
        private Text        _deadlockText;
        private GameObject  _winOverlay;
        private RectTransform _winCardRT;
        private Text        _winMovesText;
        private Text        _moreLevelsText;
        private Image[]     _winStarImages;
        private Text        _winCoinsText;     // WIN-04
        private int         _winStarCount = 3; // WIN-01: set by GameBootstrap before HUD handler fires
        private int         _winCoins     = 0; // WIN-04
        private RectTransform[] _confettiPieces;
        private Image[]     _confettiImages;
        private float[]     _confettiSpeeds;
        private float[]     _confettiRotSpeeds;
        private float[]     _confettiBaseAlphas;
        private Sprite[]    _confettiSprites;
        private Coroutine   _confettiLoop;
        private Image       _trophyImg;
        private RectTransform _trophyRT;
        private RectTransform _winTitleRT;
        private RectTransform[] _winButtonRects;
        private Coroutine   _winIdleAnim;
        private SettingsPanel _settingsPanel;

        // Confetti rain layout, in the 720×1280 logical-pixel space (see class doc).
        private const int   ConfettiPieceCount = 30;
        private const float ConfettiHalfWidth   = 360f;  // 720 / 2
        private const float ConfettiHalfHeight  = 640f;  // 1280 / 2
        private const float ConfettiMargin      = 80f;   // spawn/despawn buffer beyond visible area
        private const float ConfettiFadeBand    = 140f;  // distance over which pieces fade in/out
        private const float ConfettiTopSpawnY    =  ConfettiHalfHeight + ConfettiMargin;
        private const float ConfettiBottomDespawnY = -ConfettiHalfHeight - ConfettiMargin;

        // ── Move counter animation ────────────────────────────────────────────────
        private int       _lastMoveCount = -1;
        private Coroutine _movesAnimCoroutine;

        // ── Stored event handlers ─────────────────────────────────────────────────
        private Action<int, int, int, int, int, long> _onLevelLoadedHandler;
        private Action<int, int, int, long>           _onLevelCompleteHandler;

        // ─────────────────────────────────────────────────────────────────────────

        public void Initialize(
            BoltSort.GameStateManager.GameStateManager gsm,
            BoltSort.SortMechanic.SortMechanic sm,
            Action onReset,
            Action onNextLevel,
            Action onUndo   = null,
            Action onMenu   = null,
            Action onReplay = null,
            Action onLevels = null)
        {
            _gsm         = gsm;
            _onReset     = onReset;
            _onNextLevel = onNextLevel;
            _onUndo      = onUndo;
            _onMenu      = onMenu;
            _onReplay    = onReplay;
            _onLevels    = onLevels;

            _onLevelLoadedHandler = (id, cc, sd, tsc, tsd, seqId) =>
            {
                if (_levelText  != null) _levelText.text = $"Level {id}";
                _levelComplete = false;
                _deadlock      = false;
                _lastMoveCount = 0;
                RefreshDeadlock();
                if (_winOverlay != null) _winOverlay.SetActive(false);
                if (_confettiLoop != null) { StopCoroutine(_confettiLoop); _confettiLoop = null; }
                if (_winIdleAnim != null) { StopCoroutine(_winIdleAnim); _winIdleAnim = null; }
            };
            gsm.OnLevelLoaded += _onLevelLoadedHandler;

            _onLevelCompleteHandler = (id, moves, par, seqId) =>
            {
                _levelComplete = true;
                if (_winMovesText != null) _winMovesText.text = $"Moves: {moves}";
                if (_winOverlay   != null) StartCoroutine(ShowWinOverlay(moves));
                // D.3-B: refresh coin pill balance after level complete
                var pillSS = BoltSort.SaveSystem.SaveSystem.Instance;
                if (_coinPillText != null && pillSS != null && pillSS.IsReady)
                    _coinPillText.text = pillSS.GetCoinBalance().ToString();
            };
            gsm.OnLevelComplete += _onLevelCompleteHandler;

            sm.OnDeadlockDetected += () => { _deadlock = true; RefreshDeadlock(); };

            BuildUI();
        }

        private void Update()
        {
            if (_gsm == null || _movesText == null) return;
            int cur = _gsm.MoveCount;
            if (cur != _lastMoveCount)
            {
                _movesText.text = $"Moves: {cur}";
                if (_lastMoveCount >= 0 && _movesRT != null)
                {
                    if (_movesAnimCoroutine != null) StopCoroutine(_movesAnimCoroutine);
                    _movesAnimCoroutine = StartCoroutine(AnimateMoveCounter());
                }
                _lastMoveCount = cur;
            }
        }

        private IEnumerator AnimateMoveCounter()
        {
            if (_movesText != null) _movesText.color = BoltSortTheme.HUDAccent;
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                _movesRT, new Vector3(1.30f, 1.30f, 1f), 0.08f, TweenUtility.EaseOutBack));
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                _movesRT, Vector3.one, 0.10f, TweenUtility.EaseInOutQuad));
            if (_movesText != null)
                StartCoroutine(FadeTextColor(_movesText, BoltSortTheme.HUDAccent, Color.white, 0.20f));
        }

        private IEnumerator FadeTextColor(Text t, Color from, Color to, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                t.color = Color.Lerp(from, to, elapsed / dur);
                yield return null;
            }
            if (t != null) t.color = to;
        }

        private static void AddOutline(Text t, Color color, float distance)
        {
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor    = color;
            ol.effectDistance = new Vector2(distance, -distance);
        }

        /// <summary>Drives the confetti rain: each piece falls and spins independently
        /// at its own speed, fading in as it enters from above and fading out as it
        /// nears the bottom edge, then recycling back above the top once fully faded.
        /// A continuous, gap-free stream of individual pieces rather than a looping
        /// background image.</summary>
        private IEnumerator ConfettiLoop()
        {
            while (_winOverlay != null && _winOverlay.activeSelf)
            {
                for (int i = 0; i < _confettiPieces.Length; i++)
                {
                    var rt = _confettiPieces[i];
                    if (rt == null) continue;

                    var pos = rt.anchoredPosition;
                    pos.y -= _confettiSpeeds[i] * Time.deltaTime;

                    if (pos.y < ConfettiBottomDespawnY)
                    {
                        ResetConfettiPiece(i, spreadAcrossScreen: false);
                        continue;
                    }

                    rt.anchoredPosition = pos;
                    rt.Rotate(0f, 0f, _confettiRotSpeeds[i] * Time.deltaTime);

                    float fadeIn  = Mathf.Clamp01((ConfettiTopSpawnY - pos.y) / ConfettiFadeBand);
                    float fadeOut = Mathf.Clamp01((pos.y - ConfettiBottomDespawnY) / ConfettiFadeBand);
                    var c = _confettiImages[i].color;
                    c.a = Mathf.Min(fadeIn, fadeOut) * _confettiBaseAlphas[i];
                    _confettiImages[i].color = c;
                }
                yield return null;
            }
        }

        /// <summary>Randomizes one confetti piece's position, fall speed, spin and
        /// color. With <paramref name="spreadAcrossScreen"/> true (initial setup),
        /// the piece is placed at a random height across the whole fall range so the
        /// rain starts staggered instead of as one synchronized wave. Otherwise
        /// (recycling after falling off the bottom) it is placed just above the
        /// top edge to fall back in.</summary>
        private void ResetConfettiPiece(int i, bool spreadAcrossScreen)
        {
            var rt  = _confettiPieces[i];
            var img = _confettiImages[i];
            if (rt == null || img == null) return;

            // Pick a random real confetti shape from the sliced sheet. Size the piece to
            // the sprite's native aspect, normalized so its longest side is ~26 logical px,
            // then apply the random scale below. Keep the image tint white so the sprite's
            // own colors show through (no procedural recoloring).
            var sprites = _confettiSprites;
            if (sprites != null && sprites.Length > 0)
            {
                var spr = sprites[UnityEngine.Random.Range(0, sprites.Length)];
                img.sprite = spr;
                var nat = spr.rect.size;
                float longest = Mathf.Max(nat.x, nat.y, 1f);
                float k = 26f / longest;
                rt.sizeDelta = new Vector2(nat.x * k, nat.y * k);
            }

            float scale = UnityEngine.Random.Range(0.5f, 1.2f);
            rt.localScale    = new Vector3(scale, scale, 1f);
            rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            _confettiSpeeds[i]     = UnityEngine.Random.Range(150f, 320f);
            _confettiRotSpeeds[i]  = UnityEngine.Random.Range(-220f, 220f);
            _confettiBaseAlphas[i] = UnityEngine.Random.Range(0.85f, 1f);

            img.color = new Color(1f, 1f, 1f, 0f);

            float posY = spreadAcrossScreen
                ? UnityEngine.Random.Range(ConfettiBottomDespawnY, ConfettiTopSpawnY)
                : ConfettiTopSpawnY;
            rt.anchoredPosition = new Vector2(
                UnityEngine.Random.Range(-ConfettiHalfWidth, ConfettiHalfWidth), posY);
        }

        /// <summary>Slow continuous scale breathing for the win title while the overlay is shown.</summary>
        private IEnumerator IdlePulse(RectTransform rt, float minScale, float maxScale, float period)
        {
            while (_winOverlay != null && _winOverlay.activeSelf && rt != null)
            {
                float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / period)) + 1f) * 0.5f;
                float s = Mathf.Lerp(minScale, maxScale, t);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        /// <summary>Subtle continuous brightness shimmer for an earned star.</summary>
        private IEnumerator StarTwinkle(Image star)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.5f));
            while (_winOverlay != null && _winOverlay.activeSelf && star != null)
            {
                float t = (Mathf.Sin(Time.time * 2.2f) + 1f) * 0.5f;
                var c = star.color;
                c.a = Mathf.Lerp(0.75f, 1f, t);
                star.color = c;
                yield return null;
            }
        }

        private IEnumerator ButtonEntrance(RectTransform rt, float delay)
        {
            if (rt == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                rt, Vector3.one, 0.22f, TweenUtility.EaseOutBack));
        }

        /// <summary>Brief white flash at win moment. D.2-C.</summary>
        private IEnumerator WinFlash()
        {
            var flashGO  = new GameObject("WinFlash");
            flashGO.transform.SetParent(_winOverlay.transform, false);
            var flashImg = flashGO.AddComponent<UnityEngine.UI.Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            var flashRt  = flashGO.GetComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero; flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;
            flashImg.raycastTarget = false;

            // Ramp up to 65% white in 80ms
            float dur = 0.08f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                flashImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.65f, elapsed / dur));
                yield return null;
            }
            // Fade out to transparent in 220ms
            elapsed = 0f; dur = 0.22f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                flashImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.65f, 0f, elapsed / dur));
                yield return null;
            }
            Destroy(flashGO);
        }

        private IEnumerator ShowWinOverlay(int moves)
        {
            _winOverlay.SetActive(true);

            // Reset entrance state for trophy/title/buttons — the card itself is
            // still off-screen (slides in below) so this causes no visible pop.
            if (_trophyRT != null) _trophyRT.localScale = new Vector3(0.4f, 0.4f, 1f);
            if (_winTitleRT != null) _winTitleRT.localScale = Vector3.zero;
            if (_winButtonRects != null)
                foreach (var brt in _winButtonRects)
                    if (brt != null) brt.localScale = Vector3.zero;

            // D.2-C: brief white flash before card arrives
            StartCoroutine(WinFlash());

            // Continuous looping confetti rain — many independently falling pieces.
            if (_confettiPieces != null)
            {
                if (_confettiLoop != null) StopCoroutine(_confettiLoop);
                _confettiLoop = StartCoroutine(ConfettiLoop());
            }

            yield return new WaitForSeconds(0.06f);

            // Dim all stars first
            if (_winStarImages != null)
                foreach (var si in _winStarImages)
                    if (si != null) si.color = new Color(1f, 1f, 1f, 0.18f);

            // Trophy punch-scale entrance
            if (_trophyRT != null)
                StartCoroutine(TweenUtility.LerpRectScale(_trophyRT, Vector3.one, 0.40f, TweenUtility.EaseOutBack));

            // Title pop-in
            if (_winTitleRT != null)
                StartCoroutine(TweenUtility.LerpRectScale(_winTitleRT, Vector3.one, 0.30f, TweenUtility.EaseOutBack));

            // Slide card in from bottom
            if (_winCardRT != null)
            {
                Vector2 hiddenPos = new Vector2(0f, -1300f);
                Vector2 shownPos  = Vector2.zero;
                _winCardRT.anchoredPosition = hiddenPos;

                float dur = 0.30f, elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / dur));
                    _winCardRT.anchoredPosition = Vector2.LerpUnclamped(hiddenPos, shownPos, t);
                    yield return null;
                }
                _winCardRT.anchoredPosition = shownPos;
            }

            // Title idle pulse begins once the card has settled
            if (_winTitleRT != null)
                _winIdleAnim = StartCoroutine(IdlePulse(_winTitleRT, 1f, 1.05f, 1.6f));

            // Button entrance: staggered scale-in
            if (_winButtonRects != null)
                for (int i = 0; i < _winButtonRects.Length; i++)
                    StartCoroutine(ButtonEntrance(_winButtonRects[i], 0.05f * i));

            // Count up moves text
            if (_winMovesText != null)
            {
                float dur = 0.20f, elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    int displayed = Mathf.RoundToInt(Mathf.Lerp(0f, moves, elapsed / dur));
                    _winMovesText.text = $"Moves: {displayed}";
                    yield return null;
                }
                _winMovesText.text = $"Moves: {moves}";
            }

            // Coin count-up + bounce (WIN-04)
            if (_winCoinsText != null && _winCoins > 0)
            {
                float dur = 0.35f, elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    int displayed = Mathf.RoundToInt(Mathf.Lerp(0f, _winCoins, elapsed / dur));
                    _winCoinsText.text = $"+{displayed} coins";
                    yield return null;
                }
                _winCoinsText.text = $"+{_winCoins} coins";

                var coinRT = _winCoinsText.GetComponent<RectTransform>();
                yield return StartCoroutine(TweenUtility.LerpRectScale(
                    coinRT, new Vector3(1.18f, 1.18f, 1f), 0.10f, TweenUtility.EaseOutBack));
                yield return StartCoroutine(TweenUtility.LerpRectScale(
                    coinRT, Vector3.one, 0.12f, TweenUtility.EaseInOutQuad));
            }
            else if (_winCoinsText != null)
            {
                _winCoinsText.text = "";
            }

            // Light up only earned stars (WIN-01) and twinkle them; leave unearned stars dim
            if (_winStarImages != null)
            {
                for (int i = 0; i < _winStarImages.Length; i++)
                {
                    var si = _winStarImages[i];
                    if (si == null) continue;
                    yield return new WaitForSeconds(0.18f);
                    if (i < _winStarCount)
                    {
                        si.color = Color.white;
                        var rt = si.GetComponent<RectTransform>();
                        if (rt != null) StartCoroutine(TweenUtility.LerpRectScale(
                            rt, new Vector3(1.3f, 1.3f, 1f), 0.08f, TweenUtility.EaseOutBack));
                        StartCoroutine(StarTwinkle(si));
                    }
                }
            }
        }

        private void RefreshDeadlock()
        {
            if (_deadlockText != null)
                _deadlockText.gameObject.SetActive(_deadlock && !_levelComplete);
        }

        public void ShowMoreLevelsSoon()
        {
            if (_moreLevelsText != null) _moreLevelsText.gameObject.SetActive(true);
        }

        // Called by GameBootstrap.HandleLevelComplete (subscribed in Awake) before this
        // component's own OnLevelComplete handler (subscribed in Initialize/Start) fires.
        public void SetWinResult(int stars, int coins)
        {
            _winStarCount = Mathf.Clamp(stars, 0, 3);
            _winCoins     = Mathf.Max(0, coins);
        }

        private void OnDestroy()
        {
            if (_gsm != null)
            {
                _gsm.OnLevelLoaded   -= _onLevelLoadedHandler;
                _gsm.OnLevelComplete -= _onLevelCompleteHandler;
            }
        }

        private void OnSettingsClicked() => _settingsPanel?.Toggle();

        /// <summary>True when the settings overlay is currently visible. GP-02: queried by GameBootstrap.</summary>
        public bool IsSettingsOpen => _settingsPanel != null && _settingsPanel.IsOpen;

        /// <summary>Closes the settings panel if it is open. GP-02: called by GameBootstrap on back gesture.</summary>
        public void CloseSettings() => _settingsPanel?.Toggle();

        // ── UI construction ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                // Defensive: module self-assigns default UI actions in OnEnable too.
                esGO.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Font font = GameAssets.MenuFont; // Gummy display font (falls back to built-in)

            float lpu        = 1280f / Screen.height;
            float safeTop    = (Screen.height - Screen.safeArea.yMax) * lpu;
            float safeBottom = Screen.safeArea.yMin * lpu;

            // ── Root Canvas ──────────────────────────────────────────────────────
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight  = 1f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Top bar ──────────────────────────────────────────────────────────
            const float topBarH = 110f;
            var topBar  = MakePanel(canvasGO, "TopBar", BoltSortTheme.HUDBackground);
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot     = new Vector2(0.5f, 1f);
            topRect.offsetMin = new Vector2(0f, -(topBarH + safeTop));
            topRect.offsetMax = new Vector2(0f, 0f);

            // Thin border line at bottom of top bar
            var barLine = new GameObject("BarBorder");
            barLine.transform.SetParent(topBar.transform, false);
            var blImg = barLine.AddComponent<Image>();
            blImg.color = BoltSortTheme.TubeRim;
            var blRT = barLine.GetComponent<RectTransform>();
            blRT.anchorMin = new Vector2(0f, 0f); blRT.anchorMax = new Vector2(1f, 0f);
            blRT.pivot     = new Vector2(0.5f, 1f);
            blRT.sizeDelta = new Vector2(0f, 2f);

            // GP-06: clean header layout — Level centered & small, Moves small top-right,
            // score/coin pill stacked directly below Moves. Nothing overlaps.
            // Level title — centered horizontally at top, small GummyPop label.
            _levelText = MakeLabel(topBar, "LevelText", "Level —", font, 34,
                                   TextAnchor.MiddleCenter, bold: true, shadow: true);
            _levelText.color = BoltSortTheme.HUDText;
            SetAnchors(_levelText.rectTransform,
                anchorMin: new Vector2(0.25f, 0f), anchorMax: new Vector2(0.75f, 1f),
                offsetMin: new Vector2(0f, 0f), offsetMax: new Vector2(0f, -safeTop));

            // Moves — small, top-right, upper half of the bar.
            _movesText = MakeLabel(topBar, "MovesText", "Moves: 0", font, 26,
                                   TextAnchor.UpperRight, bold: true, shadow: true);
            _movesText.color = BoltSortTheme.HUDText;
            SetAnchors(_movesText.rectTransform,
                anchorMin: new Vector2(0.6f, 0.5f), anchorMax: new Vector2(1f, 1f),
                offsetMin: new Vector2(0f, 0f), offsetMax: new Vector2(-14f, -(safeTop + 8f)));
            _movesRT = _movesText.GetComponent<RectTransform>();

            // ── Coin/score pill — directly BELOW Moves (lower half, right) ───────
            var pillGO  = new GameObject("CoinPill");
            pillGO.transform.SetParent(topBar.transform, false);
            var pillImg = pillGO.AddComponent<Image>();
            pillImg.color = new Color(0f, 0f, 0f, 0.35f);
            var pillRt  = pillGO.GetComponent<RectTransform>();
            pillRt.anchorMin        = new Vector2(1f, 0f);
            pillRt.anchorMax        = new Vector2(1f, 0f);
            pillRt.pivot            = new Vector2(1f, 0f);
            pillRt.anchoredPosition = new Vector2(-14f, 14f);
            pillRt.sizeDelta        = new Vector2(96f, 32f);

            _coinPillText = MakeLabel(pillGO, "CoinText", "0", font, 22,
                TextAnchor.MiddleCenter, bold: true, shadow: false);
            _coinPillText.color = BoltSortTheme.WinGold;
            var pillTextRt = _coinPillText.rectTransform;
            pillTextRt.anchorMin = Vector2.zero; pillTextRt.anchorMax = Vector2.one;
            pillTextRt.offsetMin = new Vector2(6f, 0f); pillTextRt.offsetMax = new Vector2(-6f, 0f);

            // Seed coin display
            var coinSS = BoltSort.SaveSystem.SaveSystem.Instance;
            if (coinSS != null && coinSS.IsReady)
                _coinPillText.text = coinSS.GetCoinBalance().ToString();

            // ── Deadlock banner ──────────────────────────────────────────────────
            _deadlockText = MakeLabel(canvasGO, "DeadlockBanner",
                                      "No more moves! Tap Reset", font, 34,
                                      TextAnchor.MiddleCenter, bold: false, shadow: false);
            _deadlockText.color = new Color(0.95f, 0.24f, 0.24f, 1f);
            var dlRect = _deadlockText.GetComponent<RectTransform>();
            dlRect.anchorMin = new Vector2(0.1f, 1f); dlRect.anchorMax = new Vector2(0.9f, 1f);
            dlRect.pivot     = new Vector2(0.5f, 1f);
            dlRect.offsetMin = new Vector2(0f, -(topBarH + safeTop + 54f));
            dlRect.offsetMax = new Vector2(0f, -(topBarH + safeTop));
            _deadlockText.gameObject.SetActive(false);

            // ── Bottom bar ───────────────────────────────────────────────────────
            const float botBarH = 140f;
            var bottomBar  = MakePanel(canvasGO, "BottomBar", BoltSortTheme.HUDBackground);
            var botRect    = bottomBar.GetComponent<RectTransform>();
            botRect.anchorMin = new Vector2(0f, 0f); botRect.anchorMax = new Vector2(1f, 0f);
            botRect.pivot     = new Vector2(0.5f, 0f);
            botRect.offsetMin = new Vector2(0f, 0f);
            botRect.offsetMax = new Vector2(0f, botBarH + safeBottom);

            float btnY = safeBottom + 22f;

            // Retry / Reset button (far bottom-left) — retry_button.png
            var resetBtn  = MakeIconButton(bottomBar, "ResetButton", "Reset", font, 30,
                                           GameAssets.BtnRetryAction, _onReset);
            var resetRect = resetBtn.GetComponent<RectTransform>();
            resetRect.anchorMin = resetRect.anchorMax = new Vector2(0.06f, 0f);
            resetRect.pivot     = new Vector2(0f, 0f);
            resetRect.anchoredPosition = new Vector2(0f, btnY);
            resetRect.sizeDelta        = new Vector2(90f, 90f);

            // Undo button (center) — undo_button.png
            var undoBtn  = MakeIconButton(bottomBar, "UndoButton", "↩", font, 42,
                                          GameAssets.BtnUndoAction, _onUndo);
            var undoRect = undoBtn.GetComponent<RectTransform>();
            undoRect.anchorMin = undoRect.anchorMax = new Vector2(0.5f, 0f);
            undoRect.pivot     = new Vector2(0.5f, 0f);
            undoRect.anchoredPosition = new Vector2(0f, btnY);
            undoRect.sizeDelta        = new Vector2(90f, 90f);

            // Menu / Home button (far bottom-right) — home_button.png
            var menuBtn  = MakeIconButton(bottomBar, "MenuButton", "Menu", font, 30,
                                          GameAssets.BtnHomeAction, _onMenu);
            var menuRect = menuBtn.GetComponent<RectTransform>();
            menuRect.anchorMin = menuRect.anchorMax = new Vector2(0.94f, 0f);
            menuRect.pivot     = new Vector2(1f, 0f);
            menuRect.anchoredPosition = new Vector2(0f, btnY);
            menuRect.sizeDelta        = new Vector2(90f, 90f);

            // Settings button (top-left corner) — settings_button.png
            var settingsBtn = MakeIconButton(canvasGO, "SettingsButton", "",  font, 36,
                                             GameAssets.NavSettings, OnSettingsClicked);
            var settingsImg = settingsBtn.GetComponent<Image>();
            if (GameAssets.NavSettings == null)
                settingsImg.color = new Color(0.12f, 0.12f, 0.22f, 0.85f);
            var sgr = settingsBtn.GetComponent<RectTransform>();
            sgr.anchorMin = sgr.anchorMax = new Vector2(0f, 1f);
            sgr.pivot     = new Vector2(0f, 1f);
            sgr.anchoredPosition = new Vector2(12f, -(safeTop + 12f));
            sgr.sizeDelta        = new Vector2(88f, 88f);

            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform);

            // ── Win overlay ──────────────────────────────────────────────────────
            // Layering (back→front): dim background (WinOverlay's own Image, full
            // screen) → looping confetti rain → card content (trophy/stars/text,
            // background fully transparent so confetti shows through) → buttons.
            _winOverlay = MakePanel(canvasGO, "WinOverlay", new Color(0f, 0f, 0f, 0.78f));
            var winRect = _winOverlay.GetComponent<RectTransform>();
            winRect.anchorMin = Vector2.zero; winRect.anchorMax = Vector2.one;
            winRect.offsetMin = winRect.offsetMax = Vector2.zero;

            // Confetti rain — many small individually-falling pieces. Each piece is an
            // Image showing a random real confetti shape sliced from confetti_sheet.png
            // (white tint so the sprite's own colors show) that falls, spins and fades at
            // its own randomized rate, and recycles back above the top once it fades out at
            // the bottom — a continuous, pooled rain with no per-piece allocation after setup.
            _confettiPieces     = new RectTransform[ConfettiPieceCount];
            _confettiImages     = new Image[ConfettiPieceCount];
            _confettiSpeeds     = new float[ConfettiPieceCount];
            _confettiRotSpeeds  = new float[ConfettiPieceCount];
            _confettiBaseAlphas = new float[ConfettiPieceCount];
            _confettiSprites    = GameAssets.ConfettiPieces;

            var confettiContainer = MakePanel(_winOverlay, "ConfettiContainer", new Color(0f, 0f, 0f, 0f));
            confettiContainer.GetComponent<Image>().raycastTarget = false;
            var confettiContainerRt = confettiContainer.GetComponent<RectTransform>();
            confettiContainerRt.anchorMin = Vector2.zero; confettiContainerRt.anchorMax = Vector2.one;
            confettiContainerRt.offsetMin = confettiContainerRt.offsetMax = Vector2.zero;

            for (int i = 0; i < ConfettiPieceCount; i++)
            {
                var pieceGO = new GameObject($"ConfettiPiece_{i}");
                pieceGO.transform.SetParent(confettiContainer.transform, false);
                var pieceImg = pieceGO.AddComponent<Image>();
                pieceImg.raycastTarget = false;
                var pieceRt = pieceGO.GetComponent<RectTransform>();
                pieceRt.anchorMin = pieceRt.anchorMax = pieceRt.pivot = new Vector2(0.5f, 0.5f);

                _confettiPieces[i] = pieceRt;
                _confettiImages[i] = pieceImg;
                ResetConfettiPiece(i, spreadAcrossScreen: true);
            }

            // Win card — transparent layout container only; no background image,
            // so the confetti rain stays visible behind the trophy and text.
            var winCard  = MakePanel(_winOverlay, "WinCard", new Color(0f, 0f, 0f, 0f));
            winCard.GetComponent<Image>().raycastTarget = false;
            _winCardRT   = winCard.GetComponent<RectTransform>();
            _winCardRT.anchorMin        = new Vector2(0.5f, 0.5f);
            _winCardRT.anchorMax        = new Vector2(0.5f, 0.5f);
            _winCardRT.pivot            = new Vector2(0.5f, 0.5f);
            _winCardRT.anchoredPosition = Vector2.zero;
            _winCardRT.sizeDelta        = new Vector2(620f, 880f);

            // Title — top of the card
            var winTitle = MakeLabel(winCard, "WinTitle", "LEVEL COMPLETE!",
                                     font, 52, TextAnchor.MiddleCenter, bold: true, shadow: true);
            winTitle.color = BoltSortTheme.WinGold;
            AddOutline(winTitle, new Color(0f, 0f, 0f, 0.9f), 3f);
            _winTitleRT = winTitle.GetComponent<RectTransform>();
            _winTitleRT.anchorMin = _winTitleRT.anchorMax = new Vector2(0.5f, 1f);
            _winTitleRT.pivot     = new Vector2(0.5f, 1f);
            _winTitleRT.anchoredPosition = new Vector2(0f, -20f);
            _winTitleRT.sizeDelta        = new Vector2(560f, 80f);

            // 3 star icons below the title, above the trophy (no overlap)
            _winStarImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var starGO  = new GameObject($"WinStar_{i + 1}");
                starGO.transform.SetParent(winCard.transform, false);
                var starImg = starGO.AddComponent<Image>();
                Sprite spr  = GameAssets.StarLarge;
                if (spr != null) { starImg.sprite = spr; starImg.color = new Color(1f, 1f, 1f, 0.25f); starImg.preserveAspect = true; }
                else              { starImg.color = new Color(0f, 0f, 0f, 0f); }
                starImg.raycastTarget = false;
                var stRT = starGO.GetComponent<RectTransform>();
                stRT.anchorMin = stRT.anchorMax = new Vector2(0.5f, 1f);
                stRT.pivot     = new Vector2(0.5f, 1f);
                float offsetX = (i - 1) * 120f;
                stRT.anchoredPosition = new Vector2(offsetX, -110f);
                stRT.sizeDelta        = new Vector2(90f, 90f);
                _winStarImages[i]     = starImg;
            }

            // Trophy — centered in the card, below the stars (no overlap)
            var trophyGO = new GameObject("Trophy");
            trophyGO.transform.SetParent(winCard.transform, false);
            _trophyImg = trophyGO.AddComponent<Image>();
            GameAssets.Apply(_trophyImg, GameAssets.Trophy, preserveAspect: true);
            if (GameAssets.Trophy == null) _trophyImg.color = new Color(0f, 0f, 0f, 0f);
            _trophyRT = trophyGO.GetComponent<RectTransform>();
            _trophyRT.anchorMin = _trophyRT.anchorMax = new Vector2(0.5f, 0.5f);
            _trophyRT.pivot     = new Vector2(0.5f, 0.5f);
            _trophyRT.anchoredPosition = new Vector2(0f, 100f);
            _trophyRT.sizeDelta        = new Vector2(260f, 260f);
            _trophyImg.raycastTarget = false;

            // Moves text — below the trophy
            _winMovesText = MakeLabel(winCard, "WinMoves", "Moves: 0",
                                      font, 38, TextAnchor.MiddleCenter, bold: false, shadow: true);
            _winMovesText.color = Color.white;
            var wmRect = _winMovesText.GetComponent<RectTransform>();
            wmRect.anchorMin = wmRect.anchorMax = new Vector2(0.5f, 1f);
            wmRect.pivot     = new Vector2(0.5f, 1f);
            wmRect.anchoredPosition = new Vector2(0f, -490f);
            wmRect.sizeDelta        = new Vector2(560f, 50f);

            // Coins earned label (WIN-04) — below moves, with outline for legibility
            // over the confetti rain
            _winCoinsText = MakeLabel(winCard, "WinCoins", "",
                                      font, 36, TextAnchor.MiddleCenter, bold: true, shadow: true);
            _winCoinsText.color = new Color(1f, 0.85f, 0.2f, 1f);
            AddOutline(_winCoinsText, new Color(0.25f, 0.12f, 0f, 0.9f), 2.5f);
            var wcRect = _winCoinsText.GetComponent<RectTransform>();
            wcRect.anchorMin = wcRect.anchorMax = new Vector2(0.5f, 1f);
            wcRect.pivot     = new Vector2(0.5f, 1f);
            wcRect.anchoredPosition = new Vector2(0f, -550f);
            wcRect.sizeDelta        = new Vector2(320f, 70f);

            // More-levels fallback — between the coins label and the button row
            _moreLevelsText = MakeLabel(winCard, "MoreLevels", "More levels coming soon!",
                                        font, 26, TextAnchor.MiddleCenter, bold: false, shadow: true);
            _moreLevelsText.color = new Color(1f, 0.95f, 0.7f, 1f);
            var mlRect = _moreLevelsText.GetComponent<RectTransform>();
            mlRect.anchorMin = mlRect.anchorMax = new Vector2(0.5f, 1f);
            mlRect.pivot     = new Vector2(0.5f, 1f);
            mlRect.anchoredPosition = new Vector2(0f, -640f);
            mlRect.sizeDelta        = new Vector2(560f, 70f);
            _moreLevelsText.gameObject.SetActive(false);

            // Buttons — bottom row of the card
            _winButtonRects = new RectTransform[3];

            // Replay button (left) — retry.png sprite; "↩" fallback label (WIN-06)
            var replayBtn = MakeIconButton(winCard, "ReplayButton", "↩", font, 28,
                                           GameAssets.VictoryRetry, _onReplay ?? _onReset);
            if (GameAssets.VictoryRetry == null)
                replayBtn.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.36f, 1f);
            var rpRect = replayBtn.GetComponent<RectTransform>();
            rpRect.anchorMin = rpRect.anchorMax = new Vector2(0.5f, 0f);
            rpRect.pivot     = new Vector2(0.5f, 0f);
            rpRect.anchoredPosition = new Vector2(-130f, 20f);
            rpRect.sizeDelta        = new Vector2(110f, 110f);
            _winButtonRects[0] = rpRect;

            // Next Level button (center) — next_button.png sprite
            var nextBtn = MakeIconButton(winCard, "NextLevelButton", "NEXT", font, 34,
                                         GameAssets.VictoryNext, _onNextLevel);
            if (GameAssets.VictoryNext == null)
                nextBtn.GetComponent<Image>().color = BoltSortTheme.HUDAccent;
            var nbRect = nextBtn.GetComponent<RectTransform>();
            nbRect.anchorMin = nbRect.anchorMax = new Vector2(0.5f, 0f);
            nbRect.pivot     = new Vector2(0.5f, 0f);
            nbRect.anchoredPosition = new Vector2(0f, 20f);
            nbRect.sizeDelta        = new Vector2(110f, 110f);
            _winButtonRects[1] = nbRect;

            // Home button (right) — home_button.png sprite (WIN-02)
            var homeBtn = MakeIconButton(winCard, "HomeButton", "HOME", font, 28,
                                         GameAssets.BtnHomeAction, _onMenu);
            if (GameAssets.BtnHomeAction == null)
                homeBtn.GetComponent<Image>().color = new Color(0.22f, 0.36f, 0.22f, 1f);
            var hbRect = homeBtn.GetComponent<RectTransform>();
            hbRect.anchorMin = hbRect.anchorMax = new Vector2(0.5f, 0f);
            hbRect.pivot     = new Vector2(0.5f, 0f);
            hbRect.anchoredPosition = new Vector2(130f, 20f);
            hbRect.sizeDelta        = new Vector2(110f, 110f);
            _winButtonRects[2] = hbRect;

            _winOverlay.SetActive(false);
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        private static GameObject MakePanel(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static Text MakeLabel(GameObject parent, string name, string text,
                                      Font font, int fontSize, TextAnchor anchor,
                                      bool bold, bool shadow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = fontSize;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = anchor; t.color = Color.white; t.supportRichText = false;
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.80f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private GameObject MakeAnimatedButton(GameObject parent, string name, string label,
                                              Font font, int fontSize, Action onClick)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = BoltSortTheme.HUDAccent;

            var btn = go.AddComponent<Button>();
            var cs  = btn.colors;
            cs.highlightedColor = BoltSortTheme.BrightnessMult(BoltSortTheme.HUDAccent, 1.25f);
            cs.pressedColor     = BoltSortTheme.BrightnessMult(BoltSortTheme.HUDAccent, 0.75f);
            btn.colors = cs;

            var rt = go.GetComponent<RectTransform>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() =>
            {
                StartCoroutine(BounceButton(rt));
                onClick?.Invoke();
            });

            var lgo = new GameObject("Label");
            lgo.transform.SetParent(go.transform, false);
            var t = lgo.AddComponent<Text>();
            t.text = label; t.font = font; t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.supportRichText = false;
            var lr = lgo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;

            return go;
        }

        private IEnumerator BounceButton(RectTransform rt)
        {
            if (rt == null) yield break;
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                rt, new Vector3(0.90f, 0.90f, 1f), 0.07f, TweenUtility.EaseInQuad));
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                rt, Vector3.one, 0.10f, TweenUtility.EaseOutBack));
        }

        // Icon-first button: uses sprite if available, falls back to labeled colored rect.
        private GameObject MakeIconButton(GameObject parent, string name, string fallbackLabel,
                                          Font font, int fontSize,
                                          Sprite icon, Action onClick)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();

            if (icon != null)
            {
                img.sprite         = icon;
                img.color          = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = BoltSortTheme.HUDAccent;
                if (!string.IsNullOrEmpty(fallbackLabel))
                {
                    var lgo = new GameObject("Label");
                    lgo.transform.SetParent(go.transform, false);
                    var t = lgo.AddComponent<Text>();
                    t.text = fallbackLabel; t.font = font; t.fontSize = fontSize;
                    t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
                    t.color = Color.white; t.supportRichText = false;
                    var lr = lgo.GetComponent<RectTransform>();
                    lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                    lr.offsetMin = lr.offsetMax = Vector2.zero;
                }
            }

            var btn = go.AddComponent<Button>();
            var cs  = btn.colors;
            cs.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cs.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = cs;

            var rt = go.GetComponent<RectTransform>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() =>
            {
                StartCoroutine(BounceButton(rt));
                onClick?.Invoke();
            });
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt,
                                       Vector2 anchorMin, Vector2 anchorMax,
                                       Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }
    }
}
