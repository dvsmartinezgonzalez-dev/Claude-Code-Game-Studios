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
    /// Procedurally builds the Level Select screen. 3-column scrollable grid of all levels.
    /// Reads unlock/completion state from SaveSystem. Includes entrance animation, a
    /// scale pulse on the next level to play, and a lock-shake on the next locked level.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        private const int MaxLevels = 50;
        private const int Columns   = 3;

        private int _currentLevelId = 1;
        private SettingsPanel _settingsPanel;

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

            Font font = GameAssets.MenuFont; // Gummy display font (falls back to built-in)

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

            // Background — game_background sprite if available, else solid color
            var bg    = MakePanel(canvasGO, "Background", BoltSortTheme.BackgroundDeep);
            GameAssets.Apply(bg.GetComponent<Image>(), GameAssets.GameBackground);
            Stretch(bg.GetComponent<RectTransform>());

            // LS-04: safe area — wrap header and scroll view so they stay within the
            // device's safe bounds (notch / Dynamic Island / rounded corners) on all sides.
            var safeAreaGO = new GameObject("SafeArea", typeof(RectTransform));
            safeAreaGO.transform.SetParent(canvasGO.transform, false);
            ApplySafeArea(safeAreaGO.GetComponent<RectTransform>());

            // Header bar
            var header = MakePanel(safeAreaGO, "Header", BoltSortTheme.HUDBackground);
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -100f); hr.offsetMax = Vector2.zero;

            var titleText = MakeLabel(header, "Title", "LEVELS", font,
                                      52, TextAnchor.MiddleCenter, bold: true, shadow: true);
            titleText.color = BoltSortTheme.HUDText;
            Stretch(titleText.GetComponent<RectTransform>());

            // Left header slot → "back to main menu" → back_button.png (logical match)
            var backBtn = MakeAnimatedButton(header, "BackButton", "", font, 36,
                                             OnBackClicked);
            var backImg = backBtn.GetComponent<Image>();
            if (GameAssets.NavBack != null)
                GameAssets.Apply(backImg, GameAssets.NavBack, preserveAspect: true);
            else
                backImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var bbr = backBtn.GetComponent<RectTransform>();
            bbr.anchorMin = new Vector2(0f, 0f); bbr.anchorMax = new Vector2(0f, 1f);
            bbr.pivot     = new Vector2(0f, 0.5f);
            bbr.offsetMin = new Vector2(8f, 8f); bbr.offsetMax = new Vector2(100f, -8f);

            // Right header slot → opens settings panel → settings_button.png (logical match)
            var settingsBtn = MakeAnimatedButton(header, "SettingsButton", "", font, 36,
                                                 () => _settingsPanel?.Toggle());
            var settingsImg = settingsBtn.GetComponent<Image>();
            if (GameAssets.NavSettings != null)
                GameAssets.Apply(settingsImg, GameAssets.NavSettings, preserveAspect: true);
            else
                settingsImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var sbr = settingsBtn.GetComponent<RectTransform>();
            sbr.anchorMin = new Vector2(1f, 0f); sbr.anchorMax = new Vector2(1f, 1f);
            sbr.pivot     = new Vector2(1f, 0.5f);
            sbr.offsetMin = new Vector2(-100f, 8f); sbr.offsetMax = new Vector2(-8f, -8f);

            // Scroll area
            var scrollGO  = new GameObject("ScrollView");
            scrollGO.transform.SetParent(safeAreaGO.transform, false);
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
            vpImg.color = new Color(0f, 0f, 0f, 0f); // invisible; only provides scroll raycast area
            // RectMask2D clips by rectangle and does NOT depend on the graphic's alpha.
            // A plain Mask here writes its stencil from the graphic's alpha-clip, so the
            // transparent (alpha 0) viewport image produced an empty stencil that clipped
            // away every cell — full-size and clickable, but rendering nothing.
            viewportGO.AddComponent<RectMask2D>();
            Stretch(viewportGO.GetComponent<RectTransform>());
            scrollRect.viewport = viewportGO.GetComponent<RectTransform>();

            // Content — organised automatically by a GridLayoutGroup; a
            // ContentSizeFitter drives the scrollable height to fit all rows.
            // Cell size is derived from the actual canvas width so 3 columns +
            // padding + spacing always fit without clipping on any aspect ratio.
            const float cellGap  = 12f;
            const float padX     = 24f;
            const float padTop   = 20f;
            float canvasWidth = 1280f * Screen.width / Mathf.Max(1f, Screen.height);
            float cellSize    = (canvasWidth - padX * 2f - cellGap * (Columns - 1)) / Columns;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRt = contentGO.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f); contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;
            scrollRect.content  = contentRt;

            var grid = contentGO.AddComponent<GridLayoutGroup>();
            grid.cellSize        = new Vector2(cellSize, cellSize);
            grid.spacing         = new Vector2(cellGap, cellGap);
            grid.padding         = new RectOffset((int)padX, (int)padX, (int)padTop, (int)padTop);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.childAlignment  = TextAnchor.UpperCenter;

            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _entranceCells.Clear();
            RectTransform nextLevelCell  = null;
            RectTransform adjacentLockRt = null;

            for (int i = 0; i < totalLevels; i++)
            {
                int levelId = i + 1;

                // Visual state is driven by COMPLETION, not unlock:
                //  completed     → full-color tile, no lock
                //  not completed → darkened tile (×0.4) + lock overlay
                // Interactivity is driven by UNLOCK: an unlocked-but-incomplete
                // level still shows the lock yet remains tappable.
                bool isCompleted = ss != null && ss.GetCompletionRecord(levelId) != null;
                bool isCurrent   = levelId == _currentLevelId;
                bool isUnlocked  = levelId <= _currentLevelId;

                // Cell — level_background.png. Always visible (sprite or fallback color).
                var cell = new GameObject($"Level_{levelId}");
                cell.transform.SetParent(contentGO.transform, false);
                var cellImg = cell.AddComponent<Image>();
                Sprite tileSprite = GameAssets.LevelBackground;
                if (tileSprite != null)
                {
                    cellImg.sprite         = tileSprite;
                    cellImg.type           = Image.Type.Simple;
                    cellImg.preserveAspect = false;
                    cellImg.color = isCompleted ? Color.white
                                                : new Color(0.4f, 0.4f, 0.4f, 1f); // darken incomplete
                }
                else
                {
                    // Fallback — solid color if sprite not yet imported
                    cellImg.color = isCompleted
                        ? BoltSortTheme.HexColor("1A4A22")
                        : new Color(0.20f, 0.20f, 0.24f, 1f);
                }

                // Position & size are driven by the GridLayoutGroup; we keep the
                // RectTransform reference only for scale/shake animations.
                var cr = cell.GetComponent<RectTransform>();

                // Level number label — ALWAYS shown, GummyPop, centered, ~80% of the cell.
                var numLabel = MakeLabel(cell, "Num", levelId.ToString(),
                                         font, 36, TextAnchor.MiddleCenter,
                                         bold: true, shadow: true);
                numLabel.color = isCompleted ? BoltSortTheme.WinGold
                                             : new Color(1f, 1f, 1f, 0.95f);
                numLabel.raycastTarget = false;
                numLabel.resizeTextForBestFit = true;
                numLabel.resizeTextMinSize    = 24;
                numLabel.resizeTextMaxSize    = 72;
                var nlr = numLabel.GetComponent<RectTransform>();
                nlr.anchorMin = new Vector2(0.1f, 0.1f); nlr.anchorMax = new Vector2(0.9f, 0.9f);
                nlr.offsetMin = nlr.offsetMax = Vector2.zero;

                // Lock overlay for not-yet-completed levels — centered, ~60% of the
                // cell, drawn after the number so it can sit on top of it.
                RectTransform lockRt = null;
                if (!isCompleted)
                {
                    var lockGO  = new GameObject("LockIcon");
                    lockGO.transform.SetParent(cell.transform, false);
                    var lockImg = lockGO.AddComponent<Image>();
                    Sprite lockSpr = GameAssets.LevelLock;
                    if (lockSpr != null)
                    {
                        lockImg.sprite         = lockSpr;
                        lockImg.color          = isUnlocked ? new Color(1f, 1f, 1f, 0.85f) : Color.white;
                        lockImg.preserveAspect = true;
                    }
                    else
                        lockImg.color = new Color(0f, 0f, 0f, 0f);
                    lockRt = lockGO.GetComponent<RectTransform>();
                    lockRt.anchorMin = lockRt.anchorMax = new Vector2(0.5f, 0.5f);
                    lockRt.pivot     = new Vector2(0.5f, 0.5f);
                    lockRt.anchoredPosition = Vector2.zero;
                    float lockSize = cellSize * 0.6f * 1.25f;
                    lockRt.sizeDelta = new Vector2(lockSize, lockSize);
                    lockImg.raycastTarget = false;
                }

                // Track the cell to pulse (next level to play) and the next locked
                // cell whose lock should shake — only one of each, never all cells.
                if (isCurrent && !isCompleted)
                    nextLevelCell = cr;
                if (isCurrent && !isCompleted && lockRt != null)
                    adjacentLockRt = lockRt;

                // Stars row — real Image sprites for completed levels
                if (isCompleted)
                {
                    int earnedStars = ss!.GetCompletionRecord(levelId)!.Value.best_stars;
                    AddStarRow(cell, earnedStars, cellSize);
                }

                // Interactivity: unlocked → load level; locked → invalid SFX + shake.
                int captured = levelId;
                var btn = cell.AddComponent<Button>();
                var cs  = btn.colors;
                cs.normalColor      = Color.white;
                cs.highlightedColor = isUnlocked ? new Color(0.92f, 0.92f, 0.92f, 1f) : Color.white;
                cs.pressedColor     = isUnlocked ? new Color(0.75f, 0.75f, 0.75f, 1f) : Color.white;
                btn.colors = cs;
                if (isUnlocked)
                {
                    btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
                    btn.onClick.AddListener(() =>
                    {
                        StartCoroutine(TapBounce(cr));
                        LoadLevel(captured);
                    });
                }
                else
                {
                    btn.onClick.AddListener(() =>
                    {
                        AudioMgr.Instance?.PlaySFX("bolt_invalid");
                        StartCoroutine(ShakeLocked(cr));
                    });
                }

                _entranceCells.Add(cr);
            }

            // Continuous pulse on the next level to play (after the entrance settles)
            if (nextLevelCell != null)
            {
                int idx = _entranceCells.IndexOf(nextLevelCell);
                StartCoroutine(PulseNextLevel(nextLevelCell, idx));
            }

            // Periodic shake on the next locked level's lock icon
            if (adjacentLockRt != null)
                StartCoroutine(ShakeLockLoop(adjacentLockRt));

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

        // LS-05: continuous gentle scale pulse for the next level to play.
        // 1.0 -> 1.08 -> 1.0 over 1.2s, eased via cosine (smooth in/out), looping.
        private IEnumerator PulseNextLevel(RectTransform rt, int entranceIndex)
        {
            yield return new WaitForSeconds(0.03f * entranceIndex + 0.25f);
            float startTime = Time.time;
            while (rt != null)
            {
                float phase = (Time.time - startTime) % 1.2f / 1.2f;
                float scale = 1f + 0.04f * (1f - Mathf.Cos(phase * Mathf.PI * 2f));
                rt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
        }

        // LS-06: periodic wiggle on a locked level's lock icon, with a randomized
        // delay between shakes so adjacent locks don't shake in sync.
        private IEnumerator ShakeLockLoop(RectTransform lockRt)
        {
            float[] angles = { 12f, -12f, 8f, -8f, 0f };
            const float totalDur = 0.5f;
            float segDur = totalDur / angles.Length;

            while (lockRt != null)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 4f));
                float prevAngle = 0f;
                foreach (float target in angles)
                {
                    float elapsed = 0f;
                    while (elapsed < segDur)
                    {
                        if (lockRt == null) yield break;
                        elapsed += Time.deltaTime;
                        float angle = Mathf.LerpAngle(prevAngle, target, elapsed / segDur);
                        lockRt.localRotation = Quaternion.Euler(0f, 0f, angle);
                        yield return null;
                    }
                    prevAngle = target;
                }
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
                // Defensive: module self-assigns default UI actions in OnEnable too.
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
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

        // LS-04: constrain a full-screen RectTransform to the device safe area
        // (notch / Dynamic Island / rounded corners) on every side.
        private static void ApplySafeArea(RectTransform rt)
        {
            Rect safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;  anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;  anchorMax.y /= Screen.height;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
