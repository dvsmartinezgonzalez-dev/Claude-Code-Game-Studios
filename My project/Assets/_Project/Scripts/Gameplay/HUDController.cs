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

        // ── Live UI refs ──────────────────────────────────────────────────────────
        private Text        _levelText;
        private Text        _movesText;
        private RectTransform _movesRT;
        private Text        _deadlockText;
        private GameObject  _winOverlay;
        private RectTransform _winCardRT;
        private Text        _winMovesText;
        private Text        _moreLevelsText;
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
            Action onUndo  = null,
            Action onMenu  = null,
            Action onReplay = null)
        {
            _gsm         = gsm;
            _onReset     = onReset;
            _onNextLevel = onNextLevel;
            _onUndo      = onUndo;
            _onMenu      = onMenu;
            _onReplay    = onReplay;

            _onLevelLoadedHandler = (id, cc, sd, tsc, tsd, seqId) =>
            {
                if (_levelText  != null) _levelText.text = $"Level {id}";
                _levelComplete = false;
                _deadlock      = false;
                _lastMoveCount = 0;
                RefreshDeadlock();
                if (_winOverlay != null) _winOverlay.SetActive(false);
            };
            gsm.OnLevelLoaded += _onLevelLoadedHandler;

            _onLevelCompleteHandler = (id, moves, par, seqId) =>
            {
                _levelComplete = true;
                if (_winMovesText != null) _winMovesText.text = $"Moves: {moves}";
                if (_winOverlay   != null) StartCoroutine(ShowWinOverlay(moves));
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

        private IEnumerator ShowWinOverlay(int moves)
        {
            _winOverlay.SetActive(true);

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

        private void OnDestroy()
        {
            if (_gsm != null)
            {
                _gsm.OnLevelLoaded   -= _onLevelLoadedHandler;
                _gsm.OnLevelComplete -= _onLevelCompleteHandler;
            }
        }

        private void OnSettingsClicked() => _settingsPanel?.Toggle();

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

            // ── Deadlock banner ──────────────────────────────────────────────────
            _deadlockText = MakeLabel(canvasGO, "DeadlockBanner",
                                      "DEADLOCK — Reset!", font, 34,
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

            var resetBtn  = MakeAnimatedButton(bottomBar, "ResetButton", "Reset", font, 30, _onReset);
            var resetRect = resetBtn.GetComponent<RectTransform>();
            resetRect.anchorMin = resetRect.anchorMax = new Vector2(0.04f, 0f);
            resetRect.pivot     = new Vector2(0f, 0f);
            resetRect.anchoredPosition = new Vector2(0f, btnY);
            resetRect.sizeDelta        = new Vector2(196f, 72f);

            var undoBtn  = MakeAnimatedButton(bottomBar, "UndoButton", "Undo", font, 30, _onUndo);
            var undoRect = undoBtn.GetComponent<RectTransform>();
            undoRect.anchorMin = undoRect.anchorMax = new Vector2(0.5f, 0f);
            undoRect.pivot     = new Vector2(0.5f, 0f);
            undoRect.anchoredPosition = new Vector2(0f, btnY);
            undoRect.sizeDelta        = new Vector2(196f, 72f);

            var menuBtn  = MakeAnimatedButton(bottomBar, "MenuButton", "Menu", font, 30, _onMenu);
            var menuRect = menuBtn.GetComponent<RectTransform>();
            menuRect.anchorMin = menuRect.anchorMax = new Vector2(0.96f, 0f);
            menuRect.pivot     = new Vector2(1f, 0f);
            menuRect.anchoredPosition = new Vector2(0f, btnY);
            menuRect.sizeDelta        = new Vector2(196f, 72f);

            var settingsBtn = MakeAnimatedButton(canvasGO, "SettingsButton", "⚙", font, 36, OnSettingsClicked);
            settingsBtn.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.85f);
            var sgr = settingsBtn.GetComponent<RectTransform>();
            sgr.anchorMin = sgr.anchorMax = new Vector2(0f, 1f);
            sgr.pivot     = new Vector2(0f, 1f);
            sgr.anchoredPosition = new Vector2(12f, -(safeTop + 12f));
            sgr.sizeDelta        = new Vector2(72f, 72f);

            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform);

            // ── Win overlay ──────────────────────────────────────────────────────
            _winOverlay = MakePanel(canvasGO, "WinOverlay", new Color(0f, 0f, 0f, 0.75f));
            var winRect = _winOverlay.GetComponent<RectTransform>();
            winRect.anchorMin = Vector2.zero; winRect.anchorMax = Vector2.one;
            winRect.offsetMin = winRect.offsetMax = Vector2.zero;

            var winCard  = MakePanel(_winOverlay, "WinCard", new Color(0.071f, 0.071f, 0.118f, 0.97f));
            _winCardRT   = winCard.GetComponent<RectTransform>();
            _winCardRT.anchorMin        = new Vector2(0.5f, 0.5f);
            _winCardRT.anchorMax        = new Vector2(0.5f, 0.5f);
            _winCardRT.pivot            = new Vector2(0.5f, 0.5f);
            _winCardRT.anchoredPosition = Vector2.zero;
            _winCardRT.sizeDelta        = new Vector2(460f, 340f);

            var winTitle = MakeLabel(winCard, "WinTitle", "★ Level Complete! ★",
                                     font, 56, TextAnchor.MiddleCenter, bold: true, shadow: true);
            winTitle.color = BoltSortTheme.WinGold;
            var wtRect = winTitle.GetComponent<RectTransform>();
            wtRect.anchorMin = new Vector2(0f, 1f); wtRect.anchorMax = new Vector2(1f, 1f);
            wtRect.pivot     = new Vector2(0.5f, 1f);
            wtRect.offsetMin = new Vector2(12f, -108f); wtRect.offsetMax = new Vector2(-12f, -16f);

            _winMovesText = MakeLabel(winCard, "WinMoves", "Moves: 0",
                                      font, 40, TextAnchor.MiddleCenter, bold: false, shadow: false);
            _winMovesText.color = BoltSortTheme.HUDText;
            var wmRect = _winMovesText.GetComponent<RectTransform>();
            wmRect.anchorMin = new Vector2(0f, 0.5f); wmRect.anchorMax = new Vector2(1f, 0.5f);
            wmRect.pivot     = new Vector2(0.5f, 0.5f);
            wmRect.offsetMin = new Vector2(0f, -24f); wmRect.offsetMax = new Vector2(0f, 24f);

            var nextBtn  = MakeAnimatedButton(winCard, "NextLevelButton", "Next Level", font, 36, _onNextLevel);
            nextBtn.GetComponent<Image>().color = BoltSortTheme.HUDAccent;
            var nbRect   = nextBtn.GetComponent<RectTransform>();
            nbRect.anchorMin = nbRect.anchorMax = new Vector2(0.5f, 0f);
            nbRect.pivot     = new Vector2(0.5f, 0f);
            nbRect.anchoredPosition = new Vector2(-80f, 22f);
            nbRect.sizeDelta        = new Vector2(220f, 68f);

            var replayBtn  = MakeAnimatedButton(winCard, "ReplayButton", "Replay", font, 28, _onReplay ?? _onReset);
            replayBtn.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.36f, 1f);
            var rpRect   = replayBtn.GetComponent<RectTransform>();
            rpRect.anchorMin = rpRect.anchorMax = new Vector2(0.5f, 0f);
            rpRect.pivot     = new Vector2(0f, 0f);
            rpRect.anchoredPosition = new Vector2(16f, 22f);
            rpRect.sizeDelta        = new Vector2(180f, 68f);

            _moreLevelsText = MakeLabel(winCard, "MoreLevels", "More levels coming soon!",
                                        font, 28, TextAnchor.MiddleCenter, bold: false, shadow: false);
            _moreLevelsText.color = new Color(0.8f, 0.8f, 0.5f, 1f);
            var mlRect = _moreLevelsText.GetComponent<RectTransform>();
            mlRect.anchorMin = new Vector2(0f, 0f); mlRect.anchorMax = new Vector2(1f, 0f);
            mlRect.pivot     = new Vector2(0.5f, 0f);
            mlRect.offsetMin = new Vector2(8f, 22f); mlRect.offsetMax = new Vector2(-8f, 76f);
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

        private static void SetAnchors(RectTransform rt,
                                       Vector2 anchorMin, Vector2 anchorMax,
                                       Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }
    }
}
