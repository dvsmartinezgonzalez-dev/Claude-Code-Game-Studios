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
    /// Paged Level Select screen. A 5×10 grid (50 levels per page) with
    /// ‹‹ Prev | Page X of Y | Next ›› navigation, a "Go to level" jump box, and
    /// per-cell completion stars + mechanic indicator icons. Scales to thousands of
    /// levels: only the current page's 50 cells are built, and the page is persisted
    /// in PlayerPrefs ("bs.ls_page") so the player returns to where they were.
    /// Number display abbreviates large ids (1.2K, 25K) — see <see cref="FormatLevelNumber"/>.
    /// Phase-2 (TDD §5; supersedes the old single-scroll 3-column build).
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        private const int    Columns       = 5;
        private const int    Rows          = 10;
        private const int    LevelsPerPage = Columns * Rows; // 50
        private const string PagePrefKey   = "bs.ls_page";

        private int _currentLevelId = 1;
        private int _totalLevels    = 1;
        private int _pageCount       = 1;
        private int _currentPage     = 0; // 0-based

        private SettingsPanel _settingsPanel;
        private SaveSystem.ISaveSystem _ss;
        private Font _font;

        // Chrome refs (built once; grid is rebuilt per page)
        private RectTransform _contentRt;
        private float         _cellSize;
        private Text          _pageLabel;
        private Button        _prevBtn;
        private Button        _nextBtn;
        private InputField    _gotoInput;

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
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_settingsPanel != null && _settingsPanel.IsOpen)
                    _settingsPanel.Toggle();
                else
                    OnBackClicked();
            }
        }

        // ── UI construction (static chrome; grid filled by BuildPage) ─────────────

        private void BuildUI(SaveSystem.ISaveSystem ss)
        {
            _ss             = ss;
            _currentLevelId = ss != null ? ss.GetCurrentLevelId() : 1;
            _totalLevels    = Mathf.Max(1, GetAvailableLevelCount());
            _pageCount      = Mathf.Max(1, (_totalLevels + LevelsPerPage - 1) / LevelsPerPage);

            // Restore the saved page, defaulting to the page holding the current level.
            int defaultPage = (_currentLevelId - 1) / LevelsPerPage;
            _currentPage = Mathf.Clamp(PlayerPrefs.GetInt(PagePrefKey, defaultPage), 0, _pageCount - 1);

            _font = GameAssets.MenuFont;

            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight  = 1f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var bg = MakePanel(canvasGO, "Background", BoltSortTheme.BackgroundDeep);
            GameAssets.Apply(bg.GetComponent<Image>(), GameAssets.GameBackground);
            Stretch(bg.GetComponent<RectTransform>());

            var safeAreaGO = new GameObject("SafeArea", typeof(RectTransform));
            safeAreaGO.transform.SetParent(canvasGO.transform, false);
            ApplySafeArea(safeAreaGO.GetComponent<RectTransform>());

            // Header bar (title + back + settings)
            var header = MakePanel(safeAreaGO, "Header", BoltSortTheme.HUDBackground);
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -100f); hr.offsetMax = Vector2.zero;

            var titleText = MakeLabel(header, "Title", "LEVELS", _font, 52,
                                      TextAnchor.MiddleCenter, bold: true, shadow: true);
            titleText.color = BoltSortTheme.HUDText;
            Stretch(titleText.GetComponent<RectTransform>());

            var backBtn = MakeAnimatedButton(header, "BackButton", "", _font, 36, OnBackClicked);
            var backImg = backBtn.GetComponent<Image>();
            if (GameAssets.NavBack != null) GameAssets.Apply(backImg, GameAssets.NavBack, true);
            else backImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var bbr = backBtn.GetComponent<RectTransform>();
            bbr.anchorMin = new Vector2(0f, 0f); bbr.anchorMax = new Vector2(0f, 1f);
            bbr.pivot = new Vector2(0f, 0.5f);
            bbr.offsetMin = new Vector2(8f, 8f); bbr.offsetMax = new Vector2(100f, -8f);

            var settingsBtn = MakeAnimatedButton(header, "SettingsButton", "", _font, 36,
                                                 () => _settingsPanel?.Toggle());
            var settingsImg = settingsBtn.GetComponent<Image>();
            if (GameAssets.NavSettings != null) GameAssets.Apply(settingsImg, GameAssets.NavSettings, true);
            else settingsImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var sbr = settingsBtn.GetComponent<RectTransform>();
            sbr.anchorMin = new Vector2(1f, 0f); sbr.anchorMax = new Vector2(1f, 1f);
            sbr.pivot = new Vector2(1f, 0.5f);
            sbr.offsetMin = new Vector2(-100f, 8f); sbr.offsetMax = new Vector2(-8f, -8f);

            // Scroll area between header (top 100) and nav bar (bottom 150).
            var scrollGO  = new GameObject("ScrollView");
            scrollGO.transform.SetParent(safeAreaGO.transform, false);
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f; scrollRect.decelerationRate = 0.135f;
            scrollRect.elasticity = 0.1f;
            var scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, 150f); scrollRt.offsetMax = new Vector2(0f, -100f);

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpImg = viewportGO.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f);
            viewportGO.AddComponent<RectMask2D>();
            Stretch(viewportGO.GetComponent<RectTransform>());
            scrollRect.viewport = viewportGO.GetComponent<RectTransform>();

            const float cellGap = 12f, padX = 24f, padTop = 20f;
            float canvasWidth = 1280f * Screen.width / Mathf.Max(1f, Screen.height);
            _cellSize = (canvasWidth - padX * 2f - cellGap * (Columns - 1)) / Columns;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            _contentRt = contentGO.GetComponent<RectTransform>();
            _contentRt.anchorMin = new Vector2(0f, 1f); _contentRt.anchorMax = new Vector2(1f, 1f);
            _contentRt.pivot = new Vector2(0.5f, 1f);
            _contentRt.offsetMin = _contentRt.offsetMax = Vector2.zero;
            scrollRect.content = _contentRt;

            var grid = contentGO.AddComponent<GridLayoutGroup>();
            grid.cellSize        = new Vector2(_cellSize, _cellSize);
            grid.spacing         = new Vector2(cellGap, cellGap);
            grid.padding         = new RectOffset((int)padX, (int)padX, (int)padTop, (int)padTop);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.childAlignment  = TextAnchor.UpperCenter;
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildNavBar(safeAreaGO);

            // Settings panel
            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(_font, canvasGO.transform);

            BuildPage(_currentPage);
        }

        // ── Bottom navigation bar: ‹‹ Prev | Page X of Y | Next ›› + Go-to ────────

        private void BuildNavBar(GameObject parent)
        {
            var nav = MakePanel(parent, "NavBar", BoltSortTheme.HUDBackground);
            var nr = nav.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 0f);
            nr.pivot = new Vector2(0.5f, 0f);
            nr.offsetMin = new Vector2(8f, 8f); nr.offsetMax = new Vector2(-8f, 150f);

            // Prev
            _prevBtn = MakeTextButton(nav, "Prev", "‹‹", () => GoToPage(_currentPage - 1));
            var pr = _prevBtn.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0f, 0.5f); pr.anchorMax = new Vector2(0f, 0.5f);
            pr.pivot = new Vector2(0f, 0.5f); pr.sizeDelta = new Vector2(120f, 80f);
            pr.anchoredPosition = new Vector2(8f, 35f);

            // Page label
            _pageLabel = MakeLabel(nav, "PageLabel", "", _font, 30, TextAnchor.MiddleCenter, true, true);
            _pageLabel.color = BoltSortTheme.HUDText;
            var plr = _pageLabel.GetComponent<RectTransform>();
            plr.anchorMin = new Vector2(0.5f, 0.5f); plr.anchorMax = new Vector2(0.5f, 0.5f);
            plr.pivot = new Vector2(0.5f, 0.5f); plr.sizeDelta = new Vector2(240f, 80f);
            plr.anchoredPosition = new Vector2(0f, 35f);

            // Next
            _nextBtn = MakeTextButton(nav, "Next", "››", () => GoToPage(_currentPage + 1));
            var ntr = _nextBtn.GetComponent<RectTransform>();
            ntr.anchorMin = new Vector2(1f, 0.5f); ntr.anchorMax = new Vector2(1f, 0.5f);
            ntr.pivot = new Vector2(1f, 0.5f); ntr.sizeDelta = new Vector2(120f, 80f);
            ntr.anchoredPosition = new Vector2(-8f, 35f);

            // Go-to input + GO button (bottom row of the nav bar)
            _gotoInput = MakeIntInput(nav, "GotoInput", "Go to…");
            var gir = _gotoInput.GetComponent<RectTransform>();
            gir.anchorMin = new Vector2(0.5f, 0f); gir.anchorMax = new Vector2(0.5f, 0f);
            gir.pivot = new Vector2(1f, 0f); gir.sizeDelta = new Vector2(200f, 56f);
            gir.anchoredPosition = new Vector2(60f, 6f);

            var goBtn = MakeTextButton(nav, "Go", "GO", OnGotoSubmit);
            var gbr = goBtn.GetComponent<RectTransform>();
            gbr.anchorMin = new Vector2(0.5f, 0f); gbr.anchorMax = new Vector2(0.5f, 0f);
            gbr.pivot = new Vector2(0f, 0f); gbr.sizeDelta = new Vector2(96f, 56f);
            gbr.anchoredPosition = new Vector2(72f, 6f);
        }

        private void OnGotoSubmit()
        {
            AudioMgr.Instance?.PlaySFX("button_tap");
            if (_gotoInput == null) return;
            if (!int.TryParse(_gotoInput.text, out int target)) return;
            target = Mathf.Clamp(target, 1, _totalLevels);
            int page = (target - 1) / LevelsPerPage;
            GoToPage(page, highlightLevel: target);
        }

        private void GoToPage(int page, int highlightLevel = -1)
        {
            page = Mathf.Clamp(page, 0, _pageCount - 1);
            _currentPage = page;
            PlayerPrefs.SetInt(PagePrefKey, page);
            PlayerPrefs.Save();
            AudioMgr.Instance?.PlaySFX("button_tap");
            BuildPage(page, highlightLevel);
        }

        // ── Page grid build (50 cells max for the page) ───────────────────────────

        private void BuildPage(int page, int highlightLevel = -1)
        {
            if (_contentRt == null) return;
            foreach (Transform child in _contentRt) Destroy(child.gameObject);
            _entranceCells.Clear();

            int firstLevel = page * LevelsPerPage + 1;
            int lastLevel  = Mathf.Min(_totalLevels, firstLevel + LevelsPerPage - 1);

            RectTransform nextLevelCell = null, adjacentLockRt = null, highlightRt = null;

            for (int levelId = firstLevel; levelId <= lastLevel; levelId++)
            {
                bool isCompleted = _ss != null && _ss.GetCompletionRecord(levelId) != null;
                bool isCurrent   = levelId == _currentLevelId;
                bool isUnlocked  = levelId <= _currentLevelId;

                var cell = new GameObject($"Level_{levelId}");
                cell.transform.SetParent(_contentRt, false);
                var cellImg = cell.AddComponent<Image>();
                Sprite tileSprite = GameAssets.LevelBackground;
                if (tileSprite != null)
                {
                    cellImg.sprite = tileSprite; cellImg.type = Image.Type.Simple; cellImg.preserveAspect = false;
                    cellImg.color = isCompleted ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
                }
                else
                {
                    cellImg.color = isCompleted ? BoltSortTheme.HexColor("1A4A22")
                                                : new Color(0.20f, 0.20f, 0.24f, 1f);
                }
                var cr = cell.GetComponent<RectTransform>();

                // Level number — explicit per-magnitude font sizing + abbreviation (TDD §5).
                var (label, ratio) = FormatLevelNumber(levelId);
                var numLabel = MakeLabel(cell, "Num", label, _font,
                                         Mathf.RoundToInt(_cellSize * ratio),
                                         TextAnchor.MiddleCenter, bold: true, shadow: true);
                numLabel.color = isCompleted ? BoltSortTheme.WinGold : new Color(1f, 1f, 1f, 0.95f);
                numLabel.raycastTarget = false;
                // Overflow (not Truncate): at the largest ratio (1-9), GummyPop's line
                // height can exceed the anchored box, and legacy Text's default
                // VerticalWrapMode.Truncate drops the whole line rather than clipping it
                // — the digit silently disappears. Overflow always renders, still centered.
                numLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                numLabel.verticalOverflow   = VerticalWrapMode.Overflow;
                var nlr = numLabel.GetComponent<RectTransform>();
                nlr.anchorMin = new Vector2(0.08f, 0.18f); nlr.anchorMax = new Vector2(0.92f, 0.92f);
                nlr.offsetMin = nlr.offsetMax = Vector2.zero;

                // Lock overlay for incomplete levels
                RectTransform lockRt = null;
                if (!isCompleted)
                {
                    var lockGO = new GameObject("LockIcon");
                    lockGO.transform.SetParent(cell.transform, false);
                    var lockImg = lockGO.AddComponent<Image>();
                    Sprite lockSpr = GameAssets.LevelLock;
                    if (lockSpr != null)
                    {
                        lockImg.sprite = lockSpr;
                        lockImg.color = isUnlocked ? new Color(1f, 1f, 1f, 0.85f) : Color.white;
                        lockImg.preserveAspect = true;
                    }
                    else lockImg.color = new Color(0f, 0f, 0f, 0f);
                    lockRt = lockGO.GetComponent<RectTransform>();
                    lockRt.anchorMin = lockRt.anchorMax = new Vector2(0.5f, 0.5f);
                    lockRt.pivot = new Vector2(0.5f, 0.5f);
                    lockRt.anchoredPosition = Vector2.zero;
                    float lockSize = _cellSize * 0.6f * 1.25f;
                    lockRt.sizeDelta = new Vector2(lockSize, lockSize);
                    lockImg.raycastTarget = false;
                }

                if (isCurrent && !isCompleted) { nextLevelCell = cr; if (lockRt != null) adjacentLockRt = lockRt; }
                if (levelId == highlightLevel) highlightRt = cr;

                if (isCompleted)
                {
                    int earnedStars = _ss!.GetCompletionRecord(levelId)!.Value.best_stars;
                    AddStarRow(cell, earnedStars, _cellSize);
                }

                AddMechanicIcons(cell, levelId, _cellSize);

                int captured = levelId;
                var btn = cell.AddComponent<Button>();
                var cs = btn.colors;
                cs.normalColor = Color.white;
                cs.highlightedColor = isUnlocked ? new Color(0.92f, 0.92f, 0.92f, 1f) : Color.white;
                cs.pressedColor = isUnlocked ? new Color(0.75f, 0.75f, 0.75f, 1f) : Color.white;
                btn.colors = cs;
                if (isUnlocked)
                {
                    btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
                    btn.onClick.AddListener(() => { StartCoroutine(TapBounce(cr)); LoadLevel(captured); });
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

            // Page label + nav button interactability
            if (_pageLabel != null) _pageLabel.text = $"Page {_currentPage + 1} of {_pageCount}";
            if (_prevBtn != null) _prevBtn.interactable = _currentPage > 0;
            if (_nextBtn != null) _nextBtn.interactable = _currentPage < _pageCount - 1;

            if (nextLevelCell != null)
                StartCoroutine(PulseNextLevel(nextLevelCell, _entranceCells.IndexOf(nextLevelCell)));
            if (adjacentLockRt != null)
                StartCoroutine(ShakeLockLoop(adjacentLockRt));
            if (highlightRt != null)
                StartCoroutine(TapBounce(highlightRt));

            StartCoroutine(EntranceAnimation());
        }

        /// <summary>
        /// Adds small mechanic indicator icons (top-left) so the player knows what a level
        /// contains before entering: mystery ball, multicolor ball, frozen tube. Reads the
        /// tooling flags off the LevelRecord; classic levels show none.
        /// </summary>
        private void AddMechanicIcons(GameObject cell, int levelId, float cellSize)
        {
            var lds = LevelData.LevelDataSystem.Instance;
            if (lds == null || !lds.IsReady) return;

            bool mystery = false, multicolor = false, frozen = false;
            try
            {
                var rec = lds.GetLevel(levelId);
                mystery    = rec.MysteryBalls;
                multicolor = rec.HasMulticolor;
                frozen     = rec.FrozenTubes != null && rec.FrozenTubes.Length > 0;
            }
            catch (LevelData.LevelDataException) { return; }

            float size = cellSize * 0.22f;
            int idx = 0;
            void AddIcon(Sprite spr, Color fallback)
            {
                var go = new GameObject("Mech");
                go.transform.SetParent(cell.transform, false);
                var img = go.AddComponent<Image>();
                if (spr != null) { img.sprite = spr; img.preserveAspect = true; img.color = Color.white; }
                else img.color = fallback;
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(4f + idx * (size + 2f), -4f);
                idx++;
            }

            if (mystery)    AddIcon(GameAssets.BallMystery,    new Color(0.4f, 0.3f, 0.6f, 0.9f));
            if (multicolor) AddIcon(GameAssets.BallMulticolor,  new Color(0.9f, 0.6f, 0.2f, 0.9f));
            if (frozen)     AddIcon(null,                       new Color(0.5f, 0.72f, 1f, 0.95f));
        }

        // ── Level number formatting (TDD §5) ──────────────────────────────────────

        /// <summary>
        /// Returns the display string and a font-size ratio (fraction of the cell size) for a
        /// level number: 1–9 largest, 10–99, 100–999, then K-abbreviation (1.2K for 1000–9999,
        /// integer K at 10000+).
        /// </summary>
        private static (string label, float ratio) FormatLevelNumber(int n)
        {
            if (n < 10)   return (n.ToString(), 0.55f);
            if (n < 100)  return (n.ToString(), 0.42f);
            if (n < 1000) return (n.ToString(), 0.34f);
            if (n < 10000)
            {
                string s = (n / 1000f).ToString("0.0");
                if (s.EndsWith(".0")) s = s.Substring(0, s.Length - 2);
                return (s + "K", 0.30f);
            }
            return (Mathf.RoundToInt(n / 1000f) + "K", 0.26f);
        }

        // ── Animations (unchanged from the single-scroll build) ───────────────────

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
            foreach (var rt in _entranceCells) { if (rt != null) rt.localScale = Vector3.zero; }
            yield return null;
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
            float starSize = cellSize * 0.22f;
            float totalW   = total * starSize + (total - 1) * 2f;
            float startX   = -totalW * 0.5f + starSize * 0.5f;
            Sprite starSprite = GameAssets.StarLarge;

            for (int i = 0; i < total; i++)
            {
                var starGO = new GameObject($"Star_{i + 1}");
                starGO.transform.SetParent(cell.transform, false);
                var starImg = starGO.AddComponent<Image>();
                if (starSprite != null)
                {
                    starImg.sprite = starSprite; starImg.preserveAspect = true;
                    starImg.color = i < earned ? Color.white : new Color(1f, 1f, 1f, 0.22f);
                }
                else starImg.color = new Color(0f, 0f, 0f, 0f);
                var rt = starGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(startX + i * (starSize + 2f), 4f);
                rt.sizeDelta = new Vector2(starSize, starSize);
            }
        }

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

        private IEnumerator ShakeLocked(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector2 orig = rt.anchoredPosition;
            float px = 8f;
            (float dx, float dur)[] frames =
            {
                ( px, 0.04f), (-px, 0.04f), ( px * 0.5f, 0.04f), ( 0f, 0.03f),
            };
            foreach (var (dx, dur) in frames)
            {
                float elapsed = 0f, startX = rt.anchoredPosition.x, targetX = orig.x + dx;
                while (elapsed < dur)
                {
                    if (rt == null) yield break;
                    elapsed += Time.deltaTime;
                    rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, elapsed / dur), orig.y);
                    yield return null;
                }
            }
            if (rt != null) rt.anchoredPosition = orig;
        }

        // ── Data / navigation helpers ─────────────────────────────────────────────

        /// <summary>Total available levels — real catalogue count (no longer capped at 50).</summary>
        private static int GetAvailableLevelCount()
        {
            try
            {
                var asset = Resources.Load<TextAsset>("levels");
                if (asset == null) return 1;
                var root = JObject.Parse(asset.text);
                var arr  = (root["levels"] ?? root["Levels"]) as JArray;
                return arr != null ? Mathf.Max(1, arr.Count) : 1;
            }
            catch { return 1; }
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
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private static void ConfigureCamera()
        {
            if (Camera.main != null)
                Camera.main.backgroundColor = BoltSortTheme.BackgroundDeep;
        }

        // ── UI element factories ──────────────────────────────────────────────────

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
                sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private GameObject MakeAnimatedButton(GameObject parent, string name, string label,
                                              Font font, int size, Action onClick)
        {
            var go = new GameObject(name);
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

        /// <summary>A simple framed text button used by the nav bar.</summary>
        private Button MakeTextButton(GameObject parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.16f, 0.28f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) StartCoroutine(TweenUtility.LerpRectScale(
                    rt, new Vector3(0.92f, 0.92f, 1f), 0.06f, TweenUtility.EaseInQuad));
                onClick?.Invoke();
            });
            var t = MakeLabel(go, "Label", label, _font, 34, TextAnchor.MiddleCenter, true, true);
            Stretch(t.GetComponent<RectTransform>());
            return btn;
        }

        /// <summary>An integer-only InputField with placeholder, for the "Go to level" box.</summary>
        private InputField MakeIntInput(GameObject parent, string name, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.92f);
            var input = go.AddComponent<InputField>();
            input.contentType = InputField.ContentType.IntegerNumber;

            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(go.transform, false);
            var ph = phGO.AddComponent<Text>();
            ph.text = placeholder; ph.font = _font; ph.fontSize = 26;
            ph.fontStyle = FontStyle.Italic; ph.alignment = TextAnchor.MiddleCenter;
            ph.color = new Color(0.3f, 0.3f, 0.3f, 0.8f); ph.supportRichText = false;
            Stretch(ph.GetComponent<RectTransform>());

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txt = txtGO.AddComponent<Text>();
            txt.font = _font; txt.fontSize = 28; txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.05f, 0.05f, 0.1f, 1f); txt.supportRichText = false;
            Stretch(txt.GetComponent<RectTransform>());

            input.textComponent = txt;
            input.placeholder   = ph;
            return input;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

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
