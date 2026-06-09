using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        private const int MaxLevels = 30;
        private const int Columns   = 5;

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

            // GP-02: Android back button
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_settingsPanel != null && _settingsPanel.IsOpen)
                    _settingsPanel.Toggle();
                else
                    OnBackClicked();
            }
        }

        private void BuildUI(SaveSystem.ISaveSystem ss)
        {
            _currentLevelId = ss != null ? ss.GetCurrentLevelId() : 1;
            int totalLevels = GetAvailableLevelCount();

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

            // LS-04: safe area — push header and scroll view down past notch/Dynamic Island
            float lpu     = 1280f / Screen.height;
            float safeTop = (Screen.height - Screen.safeArea.yMax) * lpu;

            // Background — game_background sprite if available, else solid color
            var bg    = MakePanel(canvasGO, "Background", BoltSortTheme.BackgroundDeep);
            GameAssets.Apply(bg.GetComponent<Image>(), GameAssets.GameBackground);
            Stretch(bg.GetComponent<RectTransform>());

            // Header bar
            var header = MakePanel(canvasGO, "Header", BoltSortTheme.HUDBackground);
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -(100f + safeTop)); hr.offsetMax = Vector2.zero;

            var titleText = MakeLabel(header, "Title", "LEVELS", font,
                                      52, TextAnchor.MiddleCenter, bold: true, shadow: true);
            titleText.color = BoltSortTheme.HUDText;
            Stretch(titleText.GetComponent<RectTransform>());

            var backBtn = MakeAnimatedButton(header, "BackButton", "", font, 36,
                                             OnBackClicked);
            var backImg = backBtn.GetComponent<Image>();
            if (GameAssets.BtnBack != null)
                GameAssets.Apply(backImg, GameAssets.BtnBack, preserveAspect: true);
            else
                backImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var bbr = backBtn.GetComponent<RectTransform>();
            bbr.anchorMin = new Vector2(0f, 0f); bbr.anchorMax = new Vector2(0f, 1f);
            bbr.pivot     = new Vector2(0f, 0.5f);
            bbr.offsetMin = new Vector2(8f, 8f); bbr.offsetMax = new Vector2(100f, -8f);

            // Settings button (top-right of header) — LS-01
            var settingsBtn = MakeAnimatedButton(header, "SettingsButton", "", font, 36,
                                                 () => _settingsPanel?.Toggle());
            var settingsImg = settingsBtn.GetComponent<Image>();
            if (GameAssets.BtnSettings != null)
                GameAssets.Apply(settingsImg, GameAssets.BtnSettings, preserveAspect: true);
            else
                settingsImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var sbr = settingsBtn.GetComponent<RectTransform>();
            sbr.anchorMin = new Vector2(1f, 0f); sbr.anchorMax = new Vector2(1f, 1f);
            sbr.pivot     = new Vector2(1f, 0.5f);
            sbr.offsetMin = new Vector2(-100f, 8f); sbr.offsetMax = new Vector2(-8f, -8f);

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
            scrollRt.offsetMin = new Vector2(0f, 0f); scrollRt.offsetMax = new Vector2(0f, -(100f + safeTop));

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
            int   rows   = Mathf.CeilToInt((float)totalLevels / Columns);
            float totalH = rows * (cellSize + cellGap) + padTop;
            contentRt.sizeDelta = new Vector2(0f, totalH);

            _pulseCells.Clear();
            _entranceCells.Clear();

            for (int i = 0; i < totalLevels; i++)
            {
                int levelId     = i + 1;
                int row         = i / Columns;
                int col         = i % Columns;

                bool isCompleted = ss != null && ss.GetCompletionRecord(levelId) != null;
                bool isCurrent   = levelId == _currentLevelId;
                bool isLocked    = levelId > _currentLevelId;

                float x = padX + col * (cellSize + cellGap) + cellSize * 0.5f;
                float y = -(padTop + row * (cellSize + cellGap) + cellSize * 0.5f);

                // Cell — tile_level_unlocked or tile_level_locked sprite
                var cell = new GameObject($"Level_{levelId}");
                cell.transform.SetParent(contentGO.transform, false);
                var cellImg = cell.AddComponent<Image>();
                Sprite tileSprite = isLocked ? GameAssets.TileLevelLocked
                                              : GameAssets.TileLevelUnlocked;
                if (tileSprite != null)
                {
                    cellImg.sprite  = tileSprite;
                    cellImg.color   = Color.white;
                    cellImg.type    = Image.Type.Simple;
                    cellImg.preserveAspect = false;
                    // Tint for completed (golden) vs current (bright) vs locked (dark)
                    if (isCompleted && !isLocked)
                        cellImg.color = new Color(1.0f, 0.95f, 0.7f, 1f); // warm gold tint
                    else if (isLocked)
                        cellImg.color = new Color(0.55f, 0.55f, 0.65f, 1f); // greyed out
                }
                else
                {
                    // Fallback — solid color if sprites not yet imported
                    Color cellColor = isLocked
                        ? new Color(0.20f, 0.20f, 0.24f, 0.6f)
                        : isCompleted ? BoltSortTheme.HexColor("1A4A22")
                                      : BoltSortTheme.HexColor("1A2E4A");
                    cellImg.color = cellColor;
                }

                var cr = cell.GetComponent<RectTransform>();
                cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(0f, 1f);
                cr.pivot     = new Vector2(0.5f, 0.5f);
                cr.anchoredPosition = new Vector2(x, y);
                cr.sizeDelta = new Vector2(cellSize, cellSize);

                // Pulsing glow border for current level
                if (isCurrent)
                {
                    var borderGO  = new GameObject("Border");
                    borderGO.transform.SetParent(cell.transform, false);
                    var borderImg = borderGO.AddComponent<Image>();
                    var borderRt  = borderGO.GetComponent<RectTransform>();
                    borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
                    borderRt.offsetMin = new Vector2(-4f, -4f);
                    borderRt.offsetMax = new Vector2(4f, 4f);
                    borderImg.color = new Color(
                        BoltSortTheme.WinGold.r, BoltSortTheme.WinGold.g,
                        BoltSortTheme.WinGold.b, 0.9f);
                    _pulseCells.Add((borderImg, true));
                }

                // Level number label (centered, above stars row)
                if (!isLocked)
                {
                    var numLabel = MakeLabel(cell, "Num", levelId.ToString(),
                                             font, 34, TextAnchor.MiddleCenter,
                                             bold: true, shadow: true);
                    numLabel.color = isCompleted
                        ? BoltSortTheme.WinGold
                        : Color.white;
                    var nlr = numLabel.GetComponent<RectTransform>();
                    nlr.anchorMin = new Vector2(0f, 0.35f); nlr.anchorMax = new Vector2(1f, 1f);
                    nlr.offsetMin = nlr.offsetMax = Vector2.zero;
                }

                // Stars row — real Image sprites for completed levels
                if (isCompleted && !isLocked)
                {
                    int earnedStars = ss!.GetCompletionRecord(levelId)!.Value.best_stars;
                    AddStarRow(cell, earnedStars, cellSize);
                }

                // Lock icon overlay for locked cells
                if (isLocked)
                {
                    var lockGO  = new GameObject("LockIcon");
                    lockGO.transform.SetParent(cell.transform, false);
                    var lockImg = lockGO.AddComponent<Image>();
                    Sprite lockSpr = GameAssets.LockIcon;
                    if (lockSpr != null)
                    {
                        lockImg.sprite         = lockSpr;
                        lockImg.color          = Color.white;
                        lockImg.preserveAspect = true;
                    }
                    else
                        lockImg.color = new Color(0f, 0f, 0f, 0f);
                    var lrt = lockGO.GetComponent<RectTransform>();
                    lrt.anchorMin = new Vector2(0.5f, 0.5f); lrt.anchorMax = new Vector2(0.5f, 0.5f);
                    lrt.pivot     = new Vector2(0.5f, 0.5f);
                    lrt.anchoredPosition = Vector2.zero;
                    lrt.sizeDelta        = new Vector2(64f, 64f);
                    lockImg.raycastTarget = false;

                    // LS-02: tap locked cell → invalid SFX + shake feedback
                    var lockBtn = cell.AddComponent<Button>();
                    var lockCs  = lockBtn.colors;
                    lockCs.normalColor = Color.white; lockCs.highlightedColor = Color.white;
                    lockCs.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                    lockBtn.colors = lockCs;
                    lockBtn.onClick.AddListener(() =>
                    {
                        AudioMgr.Instance?.PlaySFX("bolt_invalid");
                        StartCoroutine(ShakeLocked(cr));
                    });
                }

                if (!isLocked)
                {
                    int captured = levelId;
                    var btn = cell.AddComponent<Button>();
                    var cs  = btn.colors;
                    cs.normalColor      = Color.white;
                    cs.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                    cs.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
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

        private static void AddStarRow(GameObject cell, int earned, float cellSize)
        {
            const int total = 3;
            float starSize  = cellSize * 0.22f;
            float totalW    = total * starSize + (total - 1) * 2f;
            float startX    = -totalW * 0.5f + starSize * 0.5f;

            Sprite starSprite = GameAssets.StarLarge;

            for (int i = 0; i < total; i++)
            {
                var starGO = new GameObject($"Star_{i + 1}");
                starGO.transform.SetParent(cell.transform, false);
                var starImg = starGO.AddComponent<Image>();
                if (starSprite != null)
                {
                    starImg.sprite         = starSprite;
                    starImg.preserveAspect = true;
                    starImg.color = i < earned
                        ? Color.white                          // filled star
                        : new Color(1f, 1f, 1f, 0.22f);       // dim unfilled star
                }
                else
                {
                    // Fallback text star
                    starImg.color = new Color(0f, 0f, 0f, 0f);
                }
                var rt = starGO.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0.5f, 0f);
                rt.anchorMax        = new Vector2(0.5f, 0f);
                rt.pivot            = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(startX + i * (starSize + 2f), 4f);
                rt.sizeDelta        = new Vector2(starSize, starSize);
            }
        }

        // LS-02: horizontal shake for locked cell taps
        private IEnumerator ShakeLocked(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector2 orig = rt.anchoredPosition;
            float px = 8f;
            (float dx, float dur)[] frames =
            {
                ( px,      0.04f),
                (-px,      0.04f),
                ( px * 0.5f, 0.04f),
                ( 0f,      0.03f),
            };
            foreach (var (dx, dur) in frames)
            {
                float elapsed = 0f, startX = rt.anchoredPosition.x, targetX = orig.x + dx;
                while (elapsed < dur)
                {
                    if (rt == null) yield break;
                    elapsed += Time.deltaTime;
                    rt.anchoredPosition = new Vector2(
                        Mathf.Lerp(startX, targetX, elapsed / dur), orig.y);
                    yield return null;
                }
            }
            if (rt != null) rt.anchoredPosition = orig;
        }

        // LS-03: derive level count from loaded data; never exceed MaxLevels
        private static int GetAvailableLevelCount()
        {
            var lds = LevelData.LevelDataSystem.Instance;
            if (lds != null && lds.IsReady)
                return lds.GetRange(1, MaxLevels).Length;
            try
            {
                var asset = Resources.Load<TextAsset>("levels");
                if (asset == null) return MaxLevels;
                var root = JObject.Parse(asset.text);
                var arr  = (root["levels"] ?? root["Levels"]) as JArray;
                return arr != null ? Mathf.Clamp(arr.Count, 1, MaxLevels) : MaxLevels;
            }
            catch { return MaxLevels; }
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
