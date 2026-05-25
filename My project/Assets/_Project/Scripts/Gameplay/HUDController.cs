using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Canvas-based HUD: level name, move counter, Reset button,
    /// deadlock banner, and a Level-Complete overlay with a Next Level button.
    /// Built entirely in code — no prefab or scene object required.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        private BoltSort.GameStateManager.GameStateManager _gsm;
        private bool   _levelComplete;
        private bool   _deadlock;
        private Action _onReset;
        private Action _onNextLevel;

        private Text       _levelText;
        private Text       _movesText;
        private Text       _deadlockText;
        private GameObject _winOverlay;
        private Text       _winMovesText;

        public void Initialize(
            BoltSort.GameStateManager.GameStateManager gsm,
            BoltSort.SortMechanic.SortMechanic sm,
            Action onReset,
            Action onNextLevel)
        {
            _gsm         = gsm;
            _onReset     = onReset;
            _onNextLevel = onNextLevel;

            gsm.OnLevelLoaded += (id, cc, sd, tsc, tsd, seqId) =>
            {
                if (_levelText != null) _levelText.text = $"Level {id}";
                _levelComplete = false;
                _deadlock      = false;
                RefreshDeadlock();
                if (_winOverlay != null) _winOverlay.SetActive(false);
            };

            gsm.OnLevelComplete += (id, moves, par, seqId) =>
            {
                _levelComplete = true;
                if (_winMovesText != null) _winMovesText.text = $"Moves: {moves}";
                if (_winOverlay   != null) _winOverlay.SetActive(true);
            };

            sm.OnDeadlockDetected += () =>
            {
                _deadlock = true;
                RefreshDeadlock();
            };

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

        private void BuildUI()
        {
            if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Safe area converted to canvas logical pixels (reference height 1280)
            float scale      = Screen.height / 1280f;
            float safeTop    = (Screen.height - Screen.safeArea.yMax) / scale;
            float safeBottom = Screen.safeArea.yMin / scale;

            // Root Canvas
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

            // Top bar — extends behind notch, content offset by safeTop
            var topBar     = MakePanel(canvasGO, "TopBar", new Color(0.10f, 0.11f, 0.15f, 0.92f));
            var topRect    = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot     = new Vector2(0.5f, 1f);
            topRect.offsetMin = new Vector2(0f, -(80f + safeTop));
            topRect.offsetMax = new Vector2(0f, 0f);

            _levelText = MakeLabel(topBar, "LevelText", "Level —", font, 28, TextAnchor.MiddleLeft);
            var lvlRect = _levelText.GetComponent<RectTransform>();
            lvlRect.anchorMin = new Vector2(0f, 0f);
            lvlRect.anchorMax = new Vector2(0.5f, 1f);
            lvlRect.offsetMin = new Vector2(20f, 0f);
            lvlRect.offsetMax = new Vector2(0f, -safeTop);

            _movesText = MakeLabel(topBar, "MovesText", "Moves: 0", font, 28, TextAnchor.MiddleRight);
            var mvRect = _movesText.GetComponent<RectTransform>();
            mvRect.anchorMin = new Vector2(0.5f, 0f);
            mvRect.anchorMax = new Vector2(1f, 1f);
            mvRect.offsetMin = new Vector2(0f, 0f);
            mvRect.offsetMax = new Vector2(-20f, -safeTop);

            // Deadlock banner just below top bar
            _deadlockText = MakeLabel(canvasGO, "DeadlockBanner", "DEADLOCK — Reset!", font, 24, TextAnchor.MiddleCenter);
            _deadlockText.color = new Color(1f, 0.28f, 0.28f);
            var dlRect = _deadlockText.GetComponent<RectTransform>();
            dlRect.anchorMin = new Vector2(0.1f, 1f);
            dlRect.anchorMax = new Vector2(0.9f, 1f);
            dlRect.pivot     = new Vector2(0.5f, 1f);
            dlRect.offsetMin = new Vector2(0f, -(80f + safeTop + 50f));
            dlRect.offsetMax = new Vector2(0f, -(80f + safeTop));
            _deadlockText.gameObject.SetActive(false);

            // Bottom bar — extends behind home indicator
            var bottomBar  = MakePanel(canvasGO, "BottomBar", new Color(0.10f, 0.11f, 0.15f, 0.92f));
            var botRect    = bottomBar.GetComponent<RectTransform>();
            botRect.anchorMin = new Vector2(0f, 0f);
            botRect.anchorMax = new Vector2(1f, 0f);
            botRect.pivot     = new Vector2(0.5f, 0f);
            botRect.offsetMin = new Vector2(0f, 0f);
            botRect.offsetMax = new Vector2(0f, 96f + safeBottom);

            var resetBtn  = MakeButton(bottomBar, "ResetButton", "Reset", font, 22, _onReset);
            var resetRect = resetBtn.GetComponent<RectTransform>();
            resetRect.anchorMin        = new Vector2(0f, 0f);
            resetRect.anchorMax        = new Vector2(0f, 0f);
            resetRect.pivot            = new Vector2(0f, 0f);
            resetRect.anchoredPosition = new Vector2(24f, safeBottom + 14f);
            resetRect.sizeDelta        = new Vector2(160f, 60f);

            // Win overlay (full screen, semi-transparent)
            _winOverlay = MakePanel(canvasGO, "WinOverlay", new Color(0f, 0f, 0f, 0.80f));
            var winOverRect = _winOverlay.GetComponent<RectTransform>();
            winOverRect.anchorMin = Vector2.zero;
            winOverRect.anchorMax = Vector2.one;
            winOverRect.offsetMin = Vector2.zero;
            winOverRect.offsetMax = Vector2.zero;

            // Win card centered on overlay
            var winCard  = MakePanel(_winOverlay, "WinCard", new Color(0.12f, 0.14f, 0.20f, 0.97f));
            var cardRect = winCard.GetComponent<RectTransform>();
            cardRect.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRect.pivot            = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta        = new Vector2(420f, 300f);

            var winTitle = MakeLabel(winCard, "WinTitle", "Level Complete!", font, 36, TextAnchor.MiddleCenter);
            winTitle.color = Color.yellow;
            var wtRect = winTitle.GetComponent<RectTransform>();
            wtRect.anchorMin = new Vector2(0f, 1f);
            wtRect.anchorMax = new Vector2(1f, 1f);
            wtRect.pivot     = new Vector2(0.5f, 1f);
            wtRect.offsetMin = new Vector2(16f, -96f);
            wtRect.offsetMax = new Vector2(-16f, -16f);

            _winMovesText = MakeLabel(winCard, "WinMoves", "Moves: 0", font, 26, TextAnchor.MiddleCenter);
            var wmRect = _winMovesText.GetComponent<RectTransform>();
            wmRect.anchorMin = new Vector2(0f, 0.5f);
            wmRect.anchorMax = new Vector2(1f, 0.5f);
            wmRect.pivot     = new Vector2(0.5f, 0.5f);
            wmRect.offsetMin = new Vector2(0f, -22f);
            wmRect.offsetMax = new Vector2(0f, 22f);

            var nextBtn  = MakeButton(winCard, "NextLevelButton", "Next Level", font, 24, _onNextLevel);
            nextBtn.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.90f);
            var nbRect   = nextBtn.GetComponent<RectTransform>();
            nbRect.anchorMin        = new Vector2(0.5f, 0f);
            nbRect.anchorMax        = new Vector2(0.5f, 0f);
            nbRect.pivot            = new Vector2(0.5f, 0f);
            nbRect.anchoredPosition = new Vector2(0f, 20f);
            nbRect.sizeDelta        = new Vector2(220f, 64f);

            _winOverlay.SetActive(false);
        }

        private static GameObject MakePanel(GameObject parent, string name, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static Text MakeLabel(GameObject parent, string name, string text,
                                      Font font, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text            = text;
            t.font            = font;
            t.fontSize        = fontSize;
            t.alignment       = anchor;
            t.color           = Color.white;
            t.supportRichText = false;
            return t;
        }

        private static GameObject MakeButton(GameObject parent, string name, string label,
                                             Font font, int fontSize, Action onClick)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = new Color(0.22f, 0.26f, 0.36f);

            var btn = go.AddComponent<Button>();
            var cs  = btn.colors;
            cs.highlightedColor = new Color(0.30f, 0.36f, 0.50f);
            cs.pressedColor     = new Color(0.12f, 0.16f, 0.26f);
            btn.colors = cs;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var t = labelGO.AddComponent<Text>();
            t.text            = label;
            t.font            = font;
            t.fontSize        = fontSize;
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
    }
}
