using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Procedurally builds the Level Select screen. 5-column scrollable grid of 30 levels.
    /// Reads unlock/completion state from SaveSystem. Includes entrance animation, pulsing
    /// current-level border, and gold glow for completed levels.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        private const int TotalLevels = 30;
        private const int Columns     = 5;

        private int _currentLevelId = 1;
        private SettingsPanel _settingsPanel;

        // Cells that need per-frame pulsing
        private readonly List<(Image border, bool isCurrent)> _pulseCells =
            new List<(Image, bool)>();
        private readonly List<RectTransform> _entranceCells = new List<RectTransform>();

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

        private void Update()
        {
            // Pulse the current-level border
            float alpha = 0.5f + Mathf.Sin(Time.time * Mathf.PI * 2f * 1f) * 0.4f;
            foreach (var (border, isCurrent) in _pulseCells)
            {
                if (border == null) continue;
                if (isCurrent)
                {
                    var c = border.color;
                    c.a = alpha;
                    border.color = c;
                }
            }
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
            var bg = MakePanel(canvasGO, "Background", BoltSortTheme.BackgroundDeep);
            Stretch(bg.GetComponent<RectTransform>());

            // Header bar
            var header = MakePanel(canvasGO, "Header", BoltSortTheme.HUDBackground);
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -100f); hr.offsetMax = Vector2.zero;

            var titleText = MakeLabel(header, "Title", "LEVELS", font,
                                      52, TextAnchor.MiddleCenter, bold: true, shadow: true);
            titleText.color = BoltSortTheme.HUDText;
            Stretch(titleText.GetComponent<RectTransform>());

            var backBtn = MakeAnimatedButton(header, "BackButton", "< Back", font, 36,
                                             OnBackClicked);
            backBtn.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var bbr = backBtn.GetComponent<RectTransform>();
            bbr.anchorMin = new Vector2(0f, 0f); bbr.anchorMax = new Vector2(0f, 1f);
            bbr.pivot     = new Vector2(0f, 0.5f);
            bbr.offsetMin = new Vector2(8f, 8f); bbr.offsetMax = new Vector2(180f, -8f);

            // Scroll area
            var scrollGO  = new GameObject("ScrollView");
            scrollGO.transform.SetParent(canvasGO.transform, false);
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.decelerationRate  = 0.135f; // momentum scroll feel
            scrollRect.elasticity        = 0.1f;

            var scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, 0f); scrollRt.offsetMax = new Vector2(0f, -100f);

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpImg = viewportGO.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewportGO.GetComponent<RectTransform>());
            scrollRect.viewport = viewportGO.GetComponent<RectTransform>();

            // Content
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRt = contentGO.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f); contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;
            scrollRect.content  = contentRt;

            // Grid
            const float cellSize = 120f;
            const float cellGap  = 16f;
            const float padX     = 20f;
            const float padTop   = 20f;
            int   rows   = Mathf.CeilToInt((float)TotalLevels / Columns);
            float totalH = rows * (cellSize + cellGap) + padTop;
            contentRt.sizeDelta = new Vector2(0f, totalH);

            _pulseCells.Clear();
            _entranceCells.Clear();

            for (int i = 0; i < TotalLevels; i++)
            {
                int levelId     = i + 1;
                int row         = i / Columns;
                int col         = i % Columns;

                bool isCompleted = ss != null && ss.GetCompletionRecord(levelId) != null;
                bool isCurrent   = levelId == _currentLevelId;
                bool isLocked    = levelId > _currentLevelId;

                float x = padX + col * (cellSize + cellGap) + cellSize * 0.5f;
                float y = -(padTop + row * (cellSize + cellGap) + cellSize * 0.5f);

                // Cell background color
                Color cellColor = isLocked
                    ? new Color(0.20f, 0.20f, 0.24f, 0.6f)
                    : isCompleted
                        ? BoltSortTheme.HexColor("1A4A22") // dark green for completed
                        : isCurrent
                            ? BoltSortTheme.HexColor("1A2E4A")
                            : BoltSortTheme.HexColor("1A2E4A");

                var cell = new GameObject($"Level_{levelId}");
                cell.transform.SetParent(contentGO.transform, false);
                var cellImg = cell.AddComponent<Image>();
                cellImg.color = cellColor;
                var cr = cell.GetComponent<RectTransform>();
                cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(0f, 1f);
                cr.pivot     = new Vector2(0.5f, 0.5f);
                cr.anchoredPosition = new Vector2(x, y);
                cr.sizeDelta = new Vector2(cellSize, cellSize);

                // Current-level pulsing border (separate Image)
                if (!isLocked)
                {
                    var borderGO  = new GameObject("Border");
                    borderGO.transform.SetParent(cell.transform, false);
                    var borderImg = borderGO.AddComponent<Image>();
                    var borderRt  = borderGO.GetComponent<RectTransform>();
                    borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
                    borderRt.offsetMin = new Vector2(-3f, -3f);
                    borderRt.offsetMax = new Vector2(3f, 3f);

                    if (isCurrent)
                    {
                        borderImg.color = new Color(
                            BoltSortTheme.HUDAccent.r, BoltSortTheme.HUDAccent.g,
                            BoltSortTheme.HUDAccent.b, 0.9f);
                        _pulseCells.Add((borderImg, true));
                    }
                    else if (isCompleted)
                    {
                        borderImg.color = new Color(
                            BoltSortTheme.WinGold.r, BoltSortTheme.WinGold.g,
                            BoltSortTheme.WinGold.b, 0.5f);
                        _pulseCells.Add((borderImg, false));
                    }
                    else
                    {
                        borderImg.color = new Color(1f, 1f, 1f, 0.08f);
                        _pulseCells.Add((borderImg, false));
                    }
                }

                // Completed glow overlay
                if (isCompleted && !isLocked)
                {
                    var glowGO  = new GameObject("CompletedGlow");
                    glowGO.transform.SetParent(cell.transform, false);
                    var glowImg = glowGO.AddComponent<Image>();
                    glowImg.color = new Color(
                        BoltSortTheme.WinGold.r, BoltSortTheme.WinGold.g,
                        BoltSortTheme.WinGold.b, 0.08f);
                    Stretch(glowGO.GetComponent<RectTransform>());
                }

                // Lock icon or level number
                var numLabel = MakeLabel(cell, "Num", isLocked ? "🔒" : levelId.ToString(),
                                         font, isLocked ? 32 : 36,
                                         TextAnchor.MiddleCenter, bold: true, shadow: false);
                numLabel.color = isLocked ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : BoltSortTheme.HUDText;
                Stretch(numLabel.GetComponent<RectTransform>());

                // Stars for completed
                if (isCompleted && !isLocked)
                {
                    int stars = ss!.GetCompletionRecord(levelId)!.Value.best_stars;
                    string starStr = stars >= 3 ? "★★★" : stars >= 2 ? "★★☆" : "★☆☆";
                    var starLabel = MakeLabel(cell, "Stars", starStr,
                                             font, 20, TextAnchor.LowerCenter, bold: false, shadow: false);
                    starLabel.color = BoltSortTheme.WinGold;
                    var slr = starLabel.GetComponent<RectTransform>();
                    slr.anchorMin = new Vector2(0f, 0f); slr.anchorMax = new Vector2(1f, 0.4f);
                    slr.offsetMin = slr.offsetMax = Vector2.zero;
                }

                // Desaturate locked cells
                if (isLocked)
                {
                    var lockOverlay = new GameObject("LockOverlay");
                    lockOverlay.transform.SetParent(cell.transform, false);
                    var lo = lockOverlay.AddComponent<Image>();
                    lo.color = new Color(0f, 0f, 0f, 0.35f);
                    Stretch(lockOverlay.GetComponent<RectTransform>());
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
                    btn.onClick.AddListener(() =>
                    {
                        StartCoroutine(TapBounce(cr));
                        LoadLevel(captured);
                    });
                }

                _entranceCells.Add(cr);
            }

            // Settings panel
            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform);

            // Stagger entrance animation
            StartCoroutine(EntranceAnimation());
        }

        private IEnumerator TapBounce(RectTransform rt)
        {
            if (rt == null) yield break;
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                rt, new Vector3(1.15f, 1.15f, 1f), 0.09f, TweenUtility.EaseOutBack));
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                rt, Vector3.one, 0.09f, TweenUtility.EaseInOutQuad));
        }

        private IEnumerator EntranceAnimation()
        {
            // Sort cells from center outward (simple: just stagger in order)
            foreach (var rt in _entranceCells)
            {
                if (rt == null) continue;
                rt.localScale = Vector3.zero;
            }
            yield return null; // wait one frame for layout

            foreach (var rt in _entranceCells)
            {
                if (rt == null) continue;
                StartCoroutine(ScaleInCell(rt));
                yield return new WaitForSeconds(0.03f);
            }
        }

        private IEnumerator ScaleInCell(RectTransform rt)
        {
            float dur = 0.20f, elapsed = 0f;
            while (elapsed < dur)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / dur));
                rt.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, t);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private static void LoadLevel(int levelId)
        {
            PlayerPrefs.SetInt("bs.next_level", levelId);
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("Gameplay");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
        }

        private static void OnBackClicked()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("MainMenu");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // ── Setup helpers ─────────────────────────────────────────────────────────

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
                Camera.main.backgroundColor = BoltSortTheme.BackgroundDeep;
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

        private GameObject MakeAnimatedButton(GameObject parent, string name, string label,
                                              Font font, int size, Action onClick)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() =>
            {
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) StartCoroutine(TweenUtility.LerpRectScale(
                    rt, new Vector3(0.92f, 0.92f, 1f), 0.06f, TweenUtility.EaseInQuad));
                onClick?.Invoke();
            });

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
