using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Procedurally builds the Level Select screen. 5-column scrollable grid of 30 levels.
    /// Reads unlock/completion state from SaveSystem.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        private const int TotalLevels = 30;
        private const int Columns     = 5;

        private static readonly Color BgColor       = new Color(0.051f, 0.051f, 0.102f, 1f);
        private static readonly Color LockedColor   = new Color(0.25f,  0.25f,  0.30f,  1f);
        private static readonly Color CompletedColor = new Color(0.20f, 0.55f,  0.25f,  1f);
        private static readonly Color CurrentColor  = new Color(0.290f, 0.565f, 0.851f, 1f);
        private static readonly Color PanelColor    = new Color(0.09f,  0.09f,  0.16f,  1f);

        private int _currentLevelId  = 1;
        private SettingsPanel _settingsPanel;

        private void Start()
        {
            EnsureSaveSystem();
            EnsureEventSystem();
            ConfigureCamera();

            var ss = SaveSystem.SaveSystem.Instance;
            if (ss != null && ss.IsReady)
                BuildUI(ss);
            else if (ss != null)
            {
                ss.OnSaveReady += () => BuildUI(ss);
                if (ss.IsReady) BuildUI(ss);
            }
            else
                BuildUI(null);
        }

        private void BuildUI(SaveSystem.ISaveSystem ss)
        {
            _currentLevelId = ss != null ? ss.GetCurrentLevelId() : 1;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Root Canvas
            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight  = 1f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Background
            var bg = MakePanel(canvasGO, "Background", BgColor);
            Stretch(bg.GetComponent<RectTransform>());

            // Header bar
            var header = MakePanel(canvasGO, "Header", PanelColor);
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -100f); hr.offsetMax = Vector2.zero;

            var titleText = MakeLabel(header, "Title", "LEVELS", font,
                                      52, TextAnchor.MiddleCenter, bold: true, shadow: true);
            Stretch(titleText.GetComponent<RectTransform>());

            // Back button (top-left)
            var backBtn = MakeButton(header, "BackButton", "< Back", font, 36, OnBackClicked);
            backBtn.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var bbr = backBtn.GetComponent<RectTransform>();
            bbr.anchorMin = new Vector2(0f, 0f); bbr.anchorMax = new Vector2(0f, 1f);
            bbr.pivot = new Vector2(0f, 0.5f);
            bbr.offsetMin = new Vector2(8f, 8f); bbr.offsetMax = new Vector2(180f, -8f);

            // Scroll area (below header, above bottom safe area)
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(canvasGO.transform, false);
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            var scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f); scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(0f, 0f); scrollRt.offsetMax = new Vector2(0f, -100f);

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpImg = viewportGO.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewportGO.GetComponent<RectTransform>());
            scrollRect.viewport = viewportGO.GetComponent<RectTransform>();

            // Content container
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRt = contentGO.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f); contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero; contentRt.offsetMax = Vector2.zero;
            scrollRect.content = contentRt;

            // Build the grid
            const float cellSize  = 120f;
            const float cellGap   = 16f;
            const float padX      = 20f;
            const float padTop    = 20f;
            int rows = Mathf.CeilToInt((float)TotalLevels / Columns);
            float totalH = rows * (cellSize + cellGap) + padTop;
            contentRt.sizeDelta = new Vector2(0f, totalH);

            for (int i = 0; i < TotalLevels; i++)
            {
                int levelId = i + 1;
                int row     = i / Columns;
                int col     = i % Columns;

                bool isCompleted = ss != null && ss.GetCompletionRecord(levelId) != null;
                bool isCurrent   = levelId == _currentLevelId;
                bool isLocked    = levelId > _currentLevelId;

                Color cellColor = isLocked   ? LockedColor :
                                  isCurrent  ? CurrentColor :
                                  isCompleted ? CompletedColor :
                                  CurrentColor;

                float x = padX + col * (cellSize + cellGap) + cellSize * 0.5f;
                float y = -(padTop + row * (cellSize + cellGap) + cellSize * 0.5f);

                var cell = MakePanel(contentGO, $"Level_{levelId}", cellColor);
                var cr   = cell.GetComponent<RectTransform>();
                cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(0f, 1f);
                cr.pivot     = new Vector2(0.5f, 0.5f);
                cr.anchoredPosition = new Vector2(x, y);
                cr.sizeDelta = new Vector2(cellSize, cellSize);

                // Level number
                var numLabel = MakeLabel(cell, "Num", isLocked ? "🔒" : levelId.ToString(),
                                         font, 36, TextAnchor.MiddleCenter, bold: true, shadow: false);
                Stretch(numLabel.GetComponent<RectTransform>());

                // Star indicator for completed
                if (isCompleted && !isLocked)
                {
                    int stars = ss!.GetCompletionRecord(levelId)!.Value.best_stars;
                    string starStr = stars >= 3 ? "★★★" : stars >= 2 ? "★★☆" : "★☆☆";
                    var starLabel = MakeLabel(cell, "Stars", starStr,
                                             font, 20, TextAnchor.LowerCenter, bold: false, shadow: false);
                    starLabel.color = new Color(1f, 0.85f, 0.2f, 1f);
                    var slr = starLabel.GetComponent<RectTransform>();
                    slr.anchorMin = new Vector2(0f, 0f); slr.anchorMax = new Vector2(1f, 0.4f);
                    slr.offsetMin = slr.offsetMax = Vector2.zero;
                }

                if (!isLocked)
                {
                    int captured = levelId;
                    var btn = cell.AddComponent<Button>();
                    var cs  = btn.colors;
                    cs.highlightedColor = new Color(cellColor.r + 0.1f, cellColor.g + 0.1f, cellColor.b + 0.1f);
                    cs.pressedColor     = new Color(cellColor.r - 0.1f, cellColor.g - 0.1f, cellColor.b - 0.1f);
                    btn.colors = cs;
                    btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
                    btn.onClick.AddListener(() => LoadLevel(captured));
                }
            }

            // Settings panel
            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform);
        }

        private static void LoadLevel(int levelId)
        {
            PlayerPrefs.SetInt("bs.next_level", levelId);
            SceneManager.LoadScene("Gameplay");
        }

        private static void OnBackClicked() => SceneManager.LoadScene("MainMenu");

        private static void EnsureSaveSystem()
        {
            if (SaveSystem.SaveSystem.Instance == null)
                new GameObject("SaveSystem").AddComponent<SaveSystem.SaveSystem>();
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
        }

        private static void ConfigureCamera()
        {
            if (Camera.main != null)
                Camera.main.backgroundColor = new Color(0.051f, 0.051f, 0.102f, 1f);
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        private static GameObject MakePanel(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static Text MakeLabel(GameObject parent, string name, string text, Font font,
                                      int size, TextAnchor anchor, bool bold, bool shadow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = anchor; t.color = Color.white; t.supportRichText = false;
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor    = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private static GameObject MakeButton(GameObject parent, string name, string label,
                                             Font font, int size, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() => onClick?.Invoke());

            var lgo = new GameObject("Label");
            lgo.transform.SetParent(go.transform, false);
            var t = lgo.AddComponent<Text>();
            t.text = label; t.font = font; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.supportRichText = false;
            var lr = lgo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
