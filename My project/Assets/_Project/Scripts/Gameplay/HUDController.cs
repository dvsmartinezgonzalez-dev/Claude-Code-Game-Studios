using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Canvas-based HUD for portrait mobile. All sizes reference 720×1280 logical pixels
    /// (ScaleWithScreenSize, match-height). Font sizes are large enough to meet 48dp minimums
    /// on a 1080-pixel-tall screen.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ── Colour palette ────────────────────────────────────────────────────────
        private static readonly Color PanelColor   = new Color(0.051f, 0.051f, 0.102f, 0.95f); // #0D0D1A 95%
        private static readonly Color ButtonColor  = new Color(0.290f, 0.565f, 0.851f, 1.00f); // #4A90D9
        private static readonly Color WinBgColor   = new Color(0.000f, 0.000f, 0.000f, 0.75f); // rgba(0,0,0,0.75)
        private static readonly Color WinCardColor = new Color(0.071f, 0.071f, 0.118f, 0.97f); // #121230 97%
        private static readonly Color DeadlockRed  = new Color(0.95f,  0.24f,  0.24f,  1.00f);

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
        private Text       _levelText;
        private Text       _movesText;
        private Text       _deadlockText;
        private GameObject _winOverlay;
        private Text       _winMovesText;
        private Text       _moreLevelsText;
        private SettingsPanel _settingsPanel;

        // ── Stored event handlers (for clean unsubscription in OnDestroy) ─────────
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
                if (_levelText != null) _levelText.text = $"Level {id}";
                _levelComplete = false;
                _deadlock      = false;
                RefreshDeadlock();
                if (_winOverlay != null) _winOverlay.SetActive(false);
            };
            gsm.OnLevelLoaded += _onLevelLoadedHandler;

            _onLevelCompleteHandler = (id, moves, par, seqId) =>
            {
                _levelComplete = true;
                if (_winMovesText != null) _winMovesText.text = $"Moves: {moves}";
                if (_winOverlay   != null) _winOverlay.SetActive(true);
            };
            gsm.OnLevelComplete += _onLevelCompleteHandler;

            sm.OnDeadlockDetected += () => { _deadlock = true; RefreshDeadlock(); };

            BuildUI();
        }

        private void Update()
        {
            if (_gsm != null && _movesText != null)
                _movesText.text = $"Moves: {_gsm.MoveCount}";
        }

        private void RefreshDeadlock()
        {
            if (_deadlockText != null)
                _deadlockText.gameObject.SetActive(_deadlock && !_levelComplete);
        }

        /// <summary>Shows "More levels coming soon" in the win overlay (last level reached).</summary>
        public void ShowMoreLevelsSoon()
        {
            if (_moreLevelsText != null) _moreLevelsText.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_gsm != null)
            {
                _gsm.OnLevelLoaded  -= _onLevelLoadedHandler;
                _gsm.OnLevelComplete -= _onLevelCompleteHandler;
            }
        }

        private void OnSettingsClicked() => _settingsPanel?.Toggle();

        // ── UI construction ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            // EventSystem — skip if one already exists in the scene.
            if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Safe area in logical pixels at 720×1280 reference (match-height scale = 1280/Screen.height).
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

            // ── Top bar (Level + Moves) ──────────────────────────────────────────
            const float topBarH = 110f;
            var topBar     = MakePanel(canvasGO, "TopBar", PanelColor);
            var topRect    = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot     = new Vector2(0.5f, 1f);
            topRect.offsetMin = new Vector2(0f, -(topBarH + safeTop));
            topRect.offsetMax = new Vector2(0f, 0f);

            // Level label — left 55%, centre-aligned (reads as centred on small screens)
            _levelText = MakeLabel(topBar, "LevelText", "Level —", font, 50,
                                   TextAnchor.MiddleCenter, bold: true, shadow: true);
            SetAnchors(_levelText.rectTransform,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.55f, 1f),
                offsetMin: new Vector2(16f, 0f), offsetMax: new Vector2(0f, -safeTop));

            // Moves label — right 45%, right-aligned
            _movesText = MakeLabel(topBar, "MovesText", "Moves: 0", font, 50,
                                   TextAnchor.MiddleRight, bold: true, shadow: true);
            SetAnchors(_movesText.rectTransform,
                anchorMin: new Vector2(0.55f, 0f), anchorMax: new Vector2(1f, 1f),
                offsetMin: new Vector2(0f, 0f), offsetMax: new Vector2(-16f, -safeTop));

            // ── Deadlock banner ──────────────────────────────────────────────────
            _deadlockText = MakeLabel(canvasGO, "DeadlockBanner",
                                      "DEADLOCK — Reset!", font, 34,
                                      TextAnchor.MiddleCenter, bold: false, shadow: false);
            _deadlockText.color = DeadlockRed;
            var dlRect = _deadlockText.GetComponent<RectTransform>();
            dlRect.anchorMin = new Vector2(0.1f, 1f);
            dlRect.anchorMax = new Vector2(0.9f, 1f);
            dlRect.pivot     = new Vector2(0.5f, 1f);
            dlRect.offsetMin = new Vector2(0f, -(topBarH + safeTop + 54f));
            dlRect.offsetMax = new Vector2(0f, -(topBarH + safeTop));
            _deadlockText.gameObject.SetActive(false);

            // ── Bottom bar (Reset / Undo / Menu buttons) ─────────────────────────
            const float botBarH = 140f;
            var bottomBar  = MakePanel(canvasGO, "BottomBar", PanelColor);
            var botRect    = bottomBar.GetComponent<RectTransform>();
            botRect.anchorMin = new Vector2(0f, 0f);
            botRect.anchorMax = new Vector2(1f, 0f);
            botRect.pivot     = new Vector2(0.5f, 0f);
            botRect.offsetMin = new Vector2(0f, 0f);
            botRect.offsetMax = new Vector2(0f, botBarH + safeBottom);

            float btnY = safeBottom + 22f;

            // Reset button — left third
            var resetBtn  = MakeButton(bottomBar, "ResetButton", "Reset", font, 30, _onReset);
            var resetRect = resetBtn.GetComponent<RectTransform>();
            resetRect.anchorMin        = new Vector2(0.04f, 0f);
            resetRect.anchorMax        = new Vector2(0.04f, 0f);
            resetRect.pivot            = new Vector2(0f, 0f);
            resetRect.anchoredPosition = new Vector2(0f, btnY);
            resetRect.sizeDelta        = new Vector2(196f, 72f);

            // Undo button — centre
            var undoBtn  = MakeButton(bottomBar, "UndoButton", "Undo", font, 30, _onUndo);
            var undoRect = undoBtn.GetComponent<RectTransform>();
            undoRect.anchorMin        = new Vector2(0.5f, 0f);
            undoRect.anchorMax        = new Vector2(0.5f, 0f);
            undoRect.pivot            = new Vector2(0.5f, 0f);
            undoRect.anchoredPosition = new Vector2(0f, btnY);
            undoRect.sizeDelta        = new Vector2(196f, 72f);

            // Menu button — right third
            var menuBtn  = MakeButton(bottomBar, "MenuButton", "Menu", font, 30, _onMenu);
            var menuRect = menuBtn.GetComponent<RectTransform>();
            menuRect.anchorMin        = new Vector2(0.96f, 0f);
            menuRect.anchorMax        = new Vector2(0.96f, 0f);
            menuRect.pivot            = new Vector2(1f, 0f);
            menuRect.anchoredPosition = new Vector2(0f, btnY);
            menuRect.sizeDelta        = new Vector2(196f, 72f);

            // Settings button (top-left gear)
            var settingsBtn  = MakeButton(canvasGO, "SettingsButton", "⚙", font, 36, OnSettingsClicked);
            settingsBtn.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.85f);
            var sgr = settingsBtn.GetComponent<RectTransform>();
            sgr.anchorMin        = new Vector2(0f, 1f);
            sgr.anchorMax        = new Vector2(0f, 1f);
            sgr.pivot            = new Vector2(0f, 1f);
            sgr.anchoredPosition = new Vector2(12f, -(safeTop + 12f));
            sgr.sizeDelta        = new Vector2(72f, 72f);

            // Settings panel (hidden initially)
            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform);

            // ── Win overlay ──────────────────────────────────────────────────────
            _winOverlay = MakePanel(canvasGO, "WinOverlay", WinBgColor);
            var winRect = _winOverlay.GetComponent<RectTransform>();
            winRect.anchorMin = Vector2.zero;
            winRect.anchorMax = Vector2.one;
            winRect.offsetMin = Vector2.zero;
            winRect.offsetMax = Vector2.zero;

            // Card
            var winCard  = MakePanel(_winOverlay, "WinCard", WinCardColor);
            var cardRect = winCard.GetComponent<RectTransform>();
            cardRect.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRect.pivot            = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta        = new Vector2(460f, 340f);

            // Star + title
            var winTitle = MakeLabel(winCard, "WinTitle", "★ Level Complete! ★",
                                     font, 56, TextAnchor.MiddleCenter, bold: true, shadow: true);
            winTitle.color = Color.yellow;
            var wtRect = winTitle.GetComponent<RectTransform>();
            wtRect.anchorMin = new Vector2(0f, 1f);
            wtRect.anchorMax = new Vector2(1f, 1f);
            wtRect.pivot     = new Vector2(0.5f, 1f);
            wtRect.offsetMin = new Vector2(12f, -108f);
            wtRect.offsetMax = new Vector2(-12f, -16f);

            // Move count inside card
            _winMovesText = MakeLabel(winCard, "WinMoves", "Moves: 0",
                                      font, 40, TextAnchor.MiddleCenter, bold: false, shadow: false);
            var wmRect = _winMovesText.GetComponent<RectTransform>();
            wmRect.anchorMin = new Vector2(0f, 0.5f);
            wmRect.anchorMax = new Vector2(1f, 0.5f);
            wmRect.pivot     = new Vector2(0.5f, 0.5f);
            wmRect.offsetMin = new Vector2(0f, -24f);
            wmRect.offsetMax = new Vector2(0f,  24f);

            // Next Level button
            var nextBtn  = MakeButton(winCard, "NextLevelButton", "Next Level", font, 36, _onNextLevel);
            nextBtn.GetComponent<Image>().color = ButtonColor;
            var nbRect   = nextBtn.GetComponent<RectTransform>();
            nbRect.anchorMin        = new Vector2(0.5f, 0f);
            nbRect.anchorMax        = new Vector2(0.5f, 0f);
            nbRect.pivot            = new Vector2(0.5f, 0f);
            nbRect.anchoredPosition = new Vector2(-80f, 22f);
            nbRect.sizeDelta        = new Vector2(220f, 68f);

            // Replay button
            var replayBtn  = MakeButton(winCard, "ReplayButton", "Replay", font, 28, _onReplay ?? _onReset);
            replayBtn.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.36f, 1f);
            var rpRect   = replayBtn.GetComponent<RectTransform>();
            rpRect.anchorMin        = new Vector2(0.5f, 0f);
            rpRect.anchorMax        = new Vector2(0.5f, 0f);
            rpRect.pivot            = new Vector2(0f, 0f);
            rpRect.anchoredPosition = new Vector2(16f, 22f);
            rpRect.sizeDelta        = new Vector2(180f, 68f);

            // "More levels coming soon" text (hidden by default)
            _moreLevelsText = MakeLabel(winCard, "MoreLevels", "More levels coming soon!",
                                        font, 28, TextAnchor.MiddleCenter, bold: false, shadow: false);
            _moreLevelsText.color = new Color(0.8f, 0.8f, 0.5f, 1f);
            var mlRect = _moreLevelsText.GetComponent<RectTransform>();
            mlRect.anchorMin = new Vector2(0f, 0f);
            mlRect.anchorMax = new Vector2(1f, 0f);
            mlRect.pivot     = new Vector2(0.5f, 0f);
            mlRect.offsetMin = new Vector2(8f, 22f);
            mlRect.offsetMax = new Vector2(-8f, 76f);
            _moreLevelsText.gameObject.SetActive(false);

            _winOverlay.SetActive(false);
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        private static GameObject MakePanel(GameObject parent, string name, Color color)
        {
            var go  = new GameObject(name);
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
            t.text            = text;
            t.font            = font;
            t.fontSize        = fontSize;
            t.fontStyle       = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment       = anchor;
            t.color           = Color.white;
            t.supportRichText = false;

            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor    = new Color(0f, 0f, 0f, 0.80f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private static GameObject MakeButton(GameObject parent, string name, string label,
                                             Font font, int fontSize, Action onClick)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = ButtonColor;

            var btn = go.AddComponent<Button>();
            var cs  = btn.colors;
            cs.highlightedColor = new Color(0.40f, 0.65f, 0.95f);
            cs.pressedColor     = new Color(0.18f, 0.38f, 0.68f);
            btn.colors = cs;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var t = labelGO.AddComponent<Text>();
            t.text            = label;
            t.font            = font;
            t.fontSize        = fontSize;
            t.fontStyle       = FontStyle.Bold;
            t.alignment       = TextAnchor.MiddleCenter;
            t.color           = Color.white;
            t.supportRichText = false;

            var lr    = labelGO.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;

            return go;
        }

        private static void SetAnchors(RectTransform rt,
                                       Vector2 anchorMin, Vector2 anchorMax,
                                       Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
