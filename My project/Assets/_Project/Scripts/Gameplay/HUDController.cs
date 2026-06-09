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
        private Image       _confettiImg;
        private Image       _trophyImg;
        private SettingsPanel _settingsPanel;

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
                if (_confettiImg != null) { var c = _confettiImg.color; c.a = 0f; _confettiImg.color = c; }
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

        private static IEnumerator FadeImageIn(Image img, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (img == null) yield break;
                elapsed += Time.deltaTime;
                var c = img.color;
                c.a = Mathf.Clamp01(elapsed / dur);
                img.color = c;
                yield return null;
            }
            if (img != null) { var c = img.color; c.a = 1f; img.color = c; }
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

            // D.2-C: brief white flash before card arrives
            StartCoroutine(WinFlash());
            yield return new WaitForSeconds(0.06f);

            // Confetti burst — fade in + scale pop
            if (_confettiImg != null)
            {
                _confettiImg.color = new Color(1f, 1f, 1f, 0f);
                var crt = _confettiImg.GetComponent<RectTransform>();
                if (crt != null) crt.localScale = new Vector3(0.6f, 0.6f, 1f);
                StartCoroutine(FadeImageIn(_confettiImg, 0.35f));
                if (crt != null) StartCoroutine(TweenUtility.LerpRectScale(
                    crt, Vector3.one, 0.35f, TweenUtility.EaseOutBack));
            }

            // Dim all stars first
            if (_winStarImages != null)
                foreach (var si in _winStarImages)
                    if (si != null) si.color = new Color(1f, 1f, 1f, 0.18f);

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

            // Show coins earned (WIN-04)
            if (_winCoinsText != null)
                _winCoinsText.text = _winCoins > 0 ? $"+{_winCoins} coins" : "";

            // Light up only earned stars (WIN-01); leave unearned stars at dim alpha
            if (_winStarImages != null)
            {
                for (int i = 0; i < _winStarImages.Length; i++)
                {
                    var si = _winStarImages[i];
                    if (si == null) continue;
                    yield return new WaitForSeconds(0.22f);
                    if (i < _winStarCount)
                    {
                        si.color = Color.white;
                        var rt = si.GetComponent<RectTransform>();
                        if (rt != null) StartCoroutine(TweenUtility.LerpRectScale(
                            rt, new Vector3(1.3f, 1.3f, 1f), 0.08f, TweenUtility.EaseOutBack));
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
                esGO.AddComponent<InputSystemUIInputModule>();
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

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

            _levelText = MakeLabel(topBar, "LevelText", "Level —", font, 50,
                                   TextAnchor.MiddleCenter, bold: true, shadow: true);
            _levelText.color = BoltSortTheme.HUDText;
            SetAnchors(_levelText.rectTransform,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.55f, 1f),
                offsetMin: new Vector2(16f, 0f), offsetMax: new Vector2(0f, -safeTop));

            _movesText = MakeLabel(topBar, "MovesText", "Moves: 0", font, 50,
                                   TextAnchor.MiddleRight, bold: true, shadow: true);
            _movesText.color = BoltSortTheme.HUDText;
            SetAnchors(_movesText.rectTransform,
                anchorMin: new Vector2(0.55f, 0f), anchorMax: new Vector2(1f, 1f),
                offsetMin: new Vector2(0f, 0f), offsetMax: new Vector2(-16f, -safeTop));
            _movesRT = _movesText.GetComponent<RectTransform>();

            // ── Coin pill (top-right corner of top bar) — D.3-B ──────────────────
            var pillGO  = new GameObject("CoinPill");
            pillGO.transform.SetParent(topBar.transform, false);
            var pillImg = pillGO.AddComponent<Image>();
            pillImg.color = new Color(0f, 0f, 0f, 0.35f);
            var pillRt  = pillGO.GetComponent<RectTransform>();
            pillRt.anchorMin        = new Vector2(1f, 0.5f);
            pillRt.anchorMax        = new Vector2(1f, 0.5f);
            pillRt.pivot            = new Vector2(1f, 0.5f);
            pillRt.anchoredPosition = new Vector2(-8f, -safeTop * 0.5f);
            pillRt.sizeDelta        = new Vector2(110f, 38f);

            _coinPillText = MakeLabel(pillGO, "CoinText", "0", font, 26,
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

            // Retry / Reset button — btn_retry sprite
            var resetBtn  = MakeIconButton(bottomBar, "ResetButton", "Reset", font, 30,
                                           GameAssets.BtnRetry, _onReset);
            var resetRect = resetBtn.GetComponent<RectTransform>();
            resetRect.anchorMin = resetRect.anchorMax = new Vector2(0.04f, 0f);
            resetRect.pivot     = new Vector2(0f, 0f);
            resetRect.anchoredPosition = new Vector2(0f, btnY);
            resetRect.sizeDelta        = new Vector2(90f, 90f);

            // Undo button — no BtnUndo sprite; use "↩" text fallback (GP-03)
            var undoBtn  = MakeIconButton(bottomBar, "UndoButton", "↩", font, 42,
                                          null, _onUndo);
            var undoRect = undoBtn.GetComponent<RectTransform>();
            undoRect.anchorMin = undoRect.anchorMax = new Vector2(0.36f, 0f);
            undoRect.pivot     = new Vector2(0.5f, 0f);
            undoRect.anchoredPosition = new Vector2(0f, btnY);
            undoRect.sizeDelta        = new Vector2(90f, 90f);

            // Levels button — btn_levels sprite (GP-06)
            var levelsBtn  = MakeIconButton(bottomBar, "LevelsButton", "Lvls", font, 24,
                                            GameAssets.BtnLevels, _onLevels);
            var levelsRect = levelsBtn.GetComponent<RectTransform>();
            levelsRect.anchorMin = levelsRect.anchorMax = new Vector2(0.64f, 0f);
            levelsRect.pivot     = new Vector2(0.5f, 0f);
            levelsRect.anchoredPosition = new Vector2(0f, btnY);
            levelsRect.sizeDelta        = new Vector2(90f, 90f);

            // Menu / Home button — btn_home sprite
            var menuBtn  = MakeIconButton(bottomBar, "MenuButton", "Menu", font, 30,
                                          GameAssets.BtnHome, _onMenu);
            var menuRect = menuBtn.GetComponent<RectTransform>();
            menuRect.anchorMin = menuRect.anchorMax = new Vector2(0.96f, 0f);
            menuRect.pivot     = new Vector2(1f, 0f);
            menuRect.anchoredPosition = new Vector2(0f, btnY);
            menuRect.sizeDelta        = new Vector2(90f, 90f);

            // Settings button (top-left corner) — btn_settings_pause sprite (yellow gear)
            var settingsBtn = MakeIconButton(canvasGO, "SettingsButton", "",  font, 36,
                                             GameAssets.BtnSettingsPause, OnSettingsClicked);
            var settingsImg = settingsBtn.GetComponent<Image>();
            if (GameAssets.BtnSettingsPause == null)
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
            _winOverlay = MakePanel(canvasGO, "WinOverlay", new Color(0f, 0f, 0f, 0.78f));
            var winRect = _winOverlay.GetComponent<RectTransform>();
            winRect.anchorMin = Vector2.zero; winRect.anchorMax = Vector2.one;
            winRect.offsetMin = winRect.offsetMax = Vector2.zero;

            // Confetti fullscreen overlay (renders behind win card, above dim)
            var confettiGO = new GameObject("Confetti");
            confettiGO.transform.SetParent(_winOverlay.transform, false);
            _confettiImg = confettiGO.AddComponent<Image>();
            GameAssets.Apply(_confettiImg, GameAssets.Confetti, preserveAspect: true);
            if (GameAssets.Confetti == null) _confettiImg.color = new Color(0f, 0f, 0f, 0f);
            var confettiRt = confettiGO.GetComponent<RectTransform>();
            confettiRt.anchorMin = Vector2.zero; confettiRt.anchorMax = Vector2.one;
            confettiRt.offsetMin = confettiRt.offsetMax = Vector2.zero;
            _confettiImg.raycastTarget = false;

            // Win card — use victory_screen sprite as background if available
            var winCard  = MakePanel(_winOverlay, "WinCard", new Color(0.071f, 0.071f, 0.118f, 0.97f));
            _winCardRT   = winCard.GetComponent<RectTransform>();
            _winCardRT.anchorMin        = new Vector2(0.5f, 0.5f);
            _winCardRT.anchorMax        = new Vector2(0.5f, 0.5f);
            _winCardRT.pivot            = new Vector2(0.5f, 0.5f);
            _winCardRT.anchoredPosition = Vector2.zero;
            _winCardRT.sizeDelta        = new Vector2(520f, 680f);

            // WIN-03: apply VictoryBg sprite; dark fallback color from MakePanel remains if null
            var winCardImg = winCard.GetComponent<Image>();
            GameAssets.Apply(winCardImg, GameAssets.VictoryBg, preserveAspect: false);

            // Trophy image at top of win card
            var trophyGO = new GameObject("Trophy");
            trophyGO.transform.SetParent(winCard.transform, false);
            _trophyImg = trophyGO.AddComponent<Image>();
            GameAssets.Apply(_trophyImg, GameAssets.Trophy, preserveAspect: true);
            if (GameAssets.Trophy == null) _trophyImg.color = new Color(0f, 0f, 0f, 0f);
            var trt = trophyGO.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot     = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, 30f); // floats above card top
            trt.sizeDelta        = new Vector2(180f, 180f);
            _trophyImg.raycastTarget = false;

            // 3 star icons near the top of the win card
            _winStarImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var starGO  = new GameObject($"WinStar_{i + 1}");
                starGO.transform.SetParent(winCard.transform, false);
                var starImg = starGO.AddComponent<Image>();
                Sprite spr  = GameAssets.StarLarge;
                if (spr != null) { starImg.sprite = spr; starImg.color = new Color(1f, 1f, 1f, 0.25f); starImg.preserveAspect = true; }
                else              { starImg.color = new Color(0f, 0f, 0f, 0f); }
                var stRT = starGO.GetComponent<RectTransform>();
                stRT.anchorMin = stRT.anchorMax = new Vector2(0.5f, 1f);
                stRT.pivot     = new Vector2(0.5f, 1f);
                float starW = 110f;
                float offsetX = (i - 1) * 120f;
                float offsetY = i == 1 ? -28f : -50f; // center star higher
                stRT.anchoredPosition = new Vector2(offsetX, offsetY);
                stRT.sizeDelta        = new Vector2(starW, starW);
                _winStarImages[i]     = starImg;
            }

            var winTitle = MakeLabel(winCard, "WinTitle", "LEVEL COMPLETE!",
                                     font, 52, TextAnchor.MiddleCenter, bold: true, shadow: true);
            winTitle.color = BoltSortTheme.WinGold;
            var wtRect = winTitle.GetComponent<RectTransform>();
            wtRect.anchorMin = new Vector2(0f, 0.55f); wtRect.anchorMax = new Vector2(1f, 0.72f);
            wtRect.offsetMin = new Vector2(12f, 0f);   wtRect.offsetMax = new Vector2(-12f, 0f);

            _winMovesText = MakeLabel(winCard, "WinMoves", "Moves: 0",
                                      font, 38, TextAnchor.MiddleCenter, bold: false, shadow: false);
            _winMovesText.color = Color.white;
            var wmRect = _winMovesText.GetComponent<RectTransform>();
            wmRect.anchorMin = new Vector2(0f, 0.44f); wmRect.anchorMax = new Vector2(1f, 0.56f);
            wmRect.offsetMin = wmRect.offsetMax = Vector2.zero;

            // Coins earned label (WIN-04)
            _winCoinsText = MakeLabel(winCard, "WinCoins", "",
                                      font, 30, TextAnchor.MiddleCenter, bold: false, shadow: false);
            _winCoinsText.color = new Color(1f, 0.85f, 0.2f, 1f);
            var wcRect = _winCoinsText.GetComponent<RectTransform>();
            wcRect.anchorMin = new Vector2(0f, 0.34f); wcRect.anchorMax = new Vector2(1f, 0.44f);
            wcRect.offsetMin = wcRect.offsetMax = Vector2.zero;

            // Replay button (left) — btn_retry sprite; "↩" fallback label (WIN-06)
            var replayBtn = MakeIconButton(winCard, "ReplayButton", "↩", font, 28,
                                           GameAssets.BtnRetry, _onReplay ?? _onReset);
            if (GameAssets.BtnRetry == null)
                replayBtn.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.36f, 1f);
            var rpRect = replayBtn.GetComponent<RectTransform>();
            rpRect.anchorMin = rpRect.anchorMax = new Vector2(0.5f, 0f);
            rpRect.pivot     = new Vector2(0.5f, 0f);
            rpRect.anchoredPosition = new Vector2(-130f, 30f);
            rpRect.sizeDelta        = new Vector2(110f, 110f);

            // Next Level button (center) — btn_continue sprite
            var nextBtn = MakeIconButton(winCard, "NextLevelButton", "NEXT", font, 34,
                                         GameAssets.BtnContinue, _onNextLevel);
            if (GameAssets.BtnContinue == null)
                nextBtn.GetComponent<Image>().color = BoltSortTheme.HUDAccent;
            var nbRect = nextBtn.GetComponent<RectTransform>();
            nbRect.anchorMin = nbRect.anchorMax = new Vector2(0.5f, 0f);
            nbRect.pivot     = new Vector2(0.5f, 0f);
            nbRect.anchoredPosition = new Vector2(0f, 30f);
            nbRect.sizeDelta        = new Vector2(110f, 110f);

            // Home button (right) — btn_home sprite (WIN-02)
            var homeBtn = MakeIconButton(winCard, "HomeButton", "HOME", font, 28,
                                         GameAssets.BtnHome, _onMenu);
            if (GameAssets.BtnHome == null)
                homeBtn.GetComponent<Image>().color = new Color(0.22f, 0.36f, 0.22f, 1f);
            var hbRect = homeBtn.GetComponent<RectTransform>();
            hbRect.anchorMin = hbRect.anchorMax = new Vector2(0.5f, 0f);
            hbRect.pivot     = new Vector2(0.5f, 0f);
            hbRect.anchoredPosition = new Vector2(130f, 30f);
            hbRect.sizeDelta        = new Vector2(110f, 110f);

            _moreLevelsText = MakeLabel(winCard, "MoreLevels", "More levels coming soon!",
                                        font, 26, TextAnchor.MiddleCenter, bold: false, shadow: false);
            _moreLevelsText.color = new Color(0.8f, 0.8f, 0.5f, 1f);
            var mlRect = _moreLevelsText.GetComponent<RectTransform>();
            mlRect.anchorMin = new Vector2(0f, 0f); mlRect.anchorMax = new Vector2(1f, 0f);
            mlRect.pivot     = new Vector2(0.5f, 0f);
            mlRect.offsetMin = new Vector2(8f, 30f); mlRect.offsetMax = new Vector2(-8f, 76f);
            _moreLevelsText.gameObject.SetActive(false);

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
