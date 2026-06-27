using System;
using System.Collections;
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
    /// Procedurally builds the Main Menu UI. Attach to any root GameObject in MainMenu.unity.
    /// Creates PLAY, LEVELS, and Settings buttons; reads last saved level from SaveSystem.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private SettingsPanel _settingsPanel;
        private NoAdsPopup    _noAdsPopup;

        private void Start()
        {
            EnsureSaveSystem();
            EnsureAudioManager();
            EnsureTransitionManager();
            EnsureEventSystem();
            ConfigureCamera();
            new GameObject("BackgroundController").AddComponent<BoltSort.Visual.BackgroundController>();
            SpawnWorldBackground();
            BuildUI();
            AudioMgr.Instance?.PlayMusic();
        }

        /// <summary>Adds game_background.png as a world-space sprite so particles in front of it
        /// are visible through the transparent canvas background.</summary>
        private static void SpawnWorldBackground()
        {
            Sprite spr = GameAssets.GameBackground;
            if (spr == null) return;
            Camera cam = Camera.main;
            float camH = cam != null ? cam.orthographicSize * 2f : 19.2f;
            float camW = cam != null ? camH * cam.aspect       : camH * (9f / 16f);
            float sw   = spr.bounds.size.x, sh = spr.bounds.size.y;
            float s    = Mathf.Max(camW / Mathf.Max(0.0001f, sw), camH / Mathf.Max(0.0001f, sh));
            var go = new GameObject("GameBackground_World");
            go.transform.localScale = new Vector3(s, s, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = spr;
            sr.sortingOrder = -98; // above gradient (-100), behind particles (5)
        }

        // ── Android back button (GP-02) ───────────────────────────────────────────

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

            // If settings panel is open, close it first.
            if (_settingsPanel != null && _settingsPanel.IsOpen)
            {
                _settingsPanel.Toggle();
                return;
            }

            // Main menu: back = quit application.
            Application.Quit();
        }

        private void BuildUI()
        {
            Font font = GameAssets.MenuFont; // Gummy display font (falls back to built-in)

            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            // Match WIDTH (0) not height: portrait phones are narrower than the 9:16
            // reference, so pinning logical width to 720 preserves the designed horizontal
            // proportions and stops the Settings/Language popups clipping. No-op at 9:16.
            scaler.matchWidthOrHeight  = 0f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // MM-04: safe area — push top-corner buttons below notch/Dynamic Island.
            // Uniform canvas scale = Screen.width/720, so 1 physical px = 720/Screen.width units.
            float lpu     = 720f / Mathf.Max(1, Screen.width);
            float safeTop = (Screen.height - Screen.safeArea.yMax) * lpu;

            // Background is now a world-space SpriteRenderer (SpawnWorldBackground),
            // so the canvas background is transparent to let particles show through.

            // Title — two stacked image words ("BOLT" / "SORT") + decorative spark,
            // replacing the old "BOLT SORT" text label.
            var boltImg  = MakeImage(canvasGO, "Title_Bolt",  GameAssets.TitleBolt);
            SetAnchors(boltImg.rectTransform,
                new Vector2(0.14f, 0.62f), new Vector2(0.86f, 0.74f));
            var sortImg  = MakeImage(canvasGO, "Title_Sort",  GameAssets.TitleSort);
            SetAnchors(sortImg.rectTransform,
                new Vector2(0.14f, 0.50f), new Vector2(0.86f, 0.62f));
            var sparkImg = MakeImage(canvasGO, "Title_Spark", GameAssets.TitleSpark);
            SetAnchors(sparkImg.rectTransform,
                new Vector2(0.66f, 0.59f), new Vector2(0.86f, 0.69f));

            // PLAY button — shared general_button.png base + independent text label
            var playBtn = MakeAnimatedButton(canvasGO, "PlayButton", "PLAY", font, 56, OnPlayClicked);
            var playImg = playBtn.GetComponent<Image>();
            if (GameAssets.MenuButton != null)
                GameAssets.Apply(playImg, GameAssets.MenuButton, preserveAspect: true);
            else
                playImg.color = BoltSortTheme.HUDAccent;
            SetAnchors(playBtn.GetComponent<RectTransform>(),
                new Vector2(0.02f, 0.29f), new Vector2(0.98f, 0.44f));
            // PLAY label occupies the upper half of the button
            var playLabel = playBtn.transform.Find("Label")?.GetComponent<Text>();
            if (playLabel != null)
            {
                // The 56pt label is taller than its anchored band; legacy Text's default
                // Truncate vertical-overflow drops the whole line, making "PLAY" invisible.
                // Overflow always renders it. Centered in the upper half of the button.
                playLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                playLabel.verticalOverflow   = VerticalWrapMode.Overflow;
                var playLabelRt = playLabel.rectTransform;
                playLabelRt.anchorMin = new Vector2(0f, 0.50f);
                playLabelRt.anchorMax = new Vector2(1f, 1.00f);
                playLabelRt.offsetMin = Vector2.zero;
                playLabelRt.offsetMax = new Vector2(0f, 60f);
                LocalizedText.Bind(playLabel, "key_play");
            }

            // MM-03: "Level X" subtitle — sits directly below PLAY in the lower half
            var mm03ss      = BoltSort.SaveSystem.SaveSystem.Instance;
            int nextLevelId = (mm03ss != null && mm03ss.IsReady)
                ? mm03ss.GetCurrentLevelId()
                : PlayerPrefs.GetInt("bs.current_level_id", 1);
            var lvlLabelGO = new GameObject("LevelLabel");
            lvlLabelGO.transform.SetParent(playBtn.transform, false);
            var lvlLabelT = lvlLabelGO.AddComponent<Text>();
            lvlLabelT.text             = $"Level {nextLevelId}";
            LocalizedText.BindFormat(lvlLabelT, "key_level_fmt", () => new object[] { nextLevelId }).AutoSize = false;
            lvlLabelT.font             = font;
            lvlLabelT.fontSize         = 40;
            lvlLabelT.fontStyle        = FontStyle.Bold;
            lvlLabelT.alignment        = TextAnchor.MiddleCenter;
            lvlLabelT.color            = new Color(1f, 1f, 1f, 0.75f);
            lvlLabelT.supportRichText  = false;
            lvlLabelT.raycastTarget    = false;
            var lvlLabelRT = lvlLabelGO.GetComponent<RectTransform>();
            // Sits just below PLAY (band tops out at 0.50) with a small ~8px gap.
            lvlLabelRT.anchorMin = new Vector2(0f, 0.18f);
            lvlLabelRT.anchorMax = new Vector2(1f, 0.46f);
            lvlLabelRT.offsetMin = lvlLabelRT.offsetMax = Vector2.zero;

            // LEVELS button — shared general_button.png base + independent text label
            var levelsBtn = MakeAnimatedButton(canvasGO, "LevelsButton", "LEVELS", font, 40, OnLevelsClicked);
            var levelsImg = levelsBtn.GetComponent<Image>();
            if (GameAssets.MenuButton != null)
                GameAssets.Apply(levelsImg, GameAssets.MenuButton, preserveAspect: true);
            else
                levelsImg.color = new Color(0.20f, 0.40f, 0.65f, 1f);
            SetAnchors(levelsBtn.GetComponent<RectTransform>(),
                new Vector2(0.04f, 0.11f), new Vector2(0.47f, 0.25f));
            { var _lvlsLt = LocalizedText.Bind(levelsBtn.transform.Find("Label")?.GetComponent<Text>(), "key_levels"); if (_lvlsLt != null) _lvlsLt.AutoSize = false; }

            // Tutorial gate — lock LEVELS and SHOP until the onboarding tutorial is complete.
            bool tutorialDone = PlayerPrefs.GetInt("bs.tutorial_complete", 0) == 1;
            if (!tutorialDone)
                ApplyTutorialLock(levelsBtn, font);

            // Settings button (top-left corner) — btn_settings sprite (red gear circle)
            var settingsBtn = MakeAnimatedButton(canvasGO, "SettingsButton", "", font, 44, OnSettingsClicked);
            var settingsImg = settingsBtn.GetComponent<Image>();
            if (GameAssets.MenuSettings != null)
                GameAssets.Apply(settingsImg, GameAssets.MenuSettings, preserveAspect: true);
            else
                settingsImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var sr = settingsBtn.GetComponent<RectTransform>();
            sr.anchorMin        = new Vector2(0f, 1f);
            sr.anchorMax        = new Vector2(0f, 1f);
            sr.pivot            = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(16f, -(16f + safeTop));
            sr.sizeDelta        = new Vector2(93.5f, 93.5f);

            // No Ads button (top-right corner) — no_ads.png (sound config now lives in Settings)
            var noAdsBtn = MakeAnimatedButton(canvasGO, "NoAdsButton", "", font, 32, OnNoAdsClicked);
            var noAdsImg = noAdsBtn.GetComponent<Image>();
            if (GameAssets.MenuNoAds != null)
                GameAssets.Apply(noAdsImg, GameAssets.MenuNoAds, preserveAspect: true);
            else
                noAdsImg.color = new Color(0.9f, 0.3f, 0.3f, 1f);
            var noAdsRT = noAdsBtn.GetComponent<RectTransform>();
            noAdsRT.anchorMin        = new Vector2(1f, 1f);
            noAdsRT.anchorMax        = new Vector2(1f, 1f);
            noAdsRT.pivot            = new Vector2(1f, 1f);
            noAdsRT.anchoredPosition = new Vector2(-16f, -(16f + safeTop));
            noAdsRT.sizeDelta        = new Vector2(93.5f, 93.5f);

            // SHOP button — shared general_button.png base + independent text label
            var shopBtn = MakeAnimatedButton(canvasGO, "ShopButton", "SHOP", font, 40, OnShopClicked);
            var shopImg = shopBtn.GetComponent<Image>();
            if (GameAssets.MenuButton != null)
                GameAssets.Apply(shopImg, GameAssets.MenuButton, preserveAspect: true);
            else
                shopImg.color = new Color(0.55f, 0.25f, 0.75f, 1f); // purple
            SetAnchors(shopBtn.GetComponent<RectTransform>(),
                new Vector2(0.53f, 0.11f), new Vector2(0.96f, 0.25f));
            LocalizedText.Bind(shopBtn.transform.Find("Label")?.GetComponent<Text>(), "key_shop");

            if (!tutorialDone)
                ApplyTutorialLock(shopBtn, font);

            // MM-05: diamond decorations — ~18% larger with slight overflow beyond button edges
            MakeDiamond(shopBtn, "ShopDiamond_L", GameAssets.MenuDiamond(1),
                new Vector2(0f, 0.10f), new Vector2(0f, 0.90f),
                new Vector2(-25f, 0f), new Vector2(55f, 0f));
            MakeDiamond(shopBtn, "ShopDiamond_R1", GameAssets.MenuDiamond(2),
                new Vector2(1f, 0.38f), new Vector2(1f, 1.02f),
                new Vector2(-91f, 0f), new Vector2(-9f, -40f));
            MakeDiamond(shopBtn, "ShopDiamond_R2", GameAssets.MenuDiamond(3),
                new Vector2(1f, 0.00f), new Vector2(1f, 0.60f),
                new Vector2(-58f, 0f), new Vector2(8f, -20f));

            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform);

            var noAdsHost = new GameObject("NoAdsPopupHost");
            noAdsHost.transform.SetParent(canvasGO.transform, false);
            _noAdsPopup = noAdsHost.AddComponent<NoAdsPopup>();
            _noAdsPopup.Initialize(font, canvasGO.transform);

            // D.3-C: stagger entrance + D.2-A: idle float on PLAY button after entrance
            var playRt   = playBtn.GetComponent<RectTransform>();
            var levelsRt = levelsBtn.GetComponent<RectTransform>();
            var shopRt   = shopBtn.GetComponent<RectTransform>();
            StartCoroutine(StaggerEntrance(
                (boltImg.rectTransform,   0f),
                (sortImg.rectTransform,   0.06f),
                (playRt,                  0.10f),
                (levelsRt,                0.16f),
                (shopRt,                  0.22f)));

            // Title float (BOLT + SORT, offset phase), spark scale-pulse, SHOP breathe.
            StartCoroutine(DelayedStart(0.40f, () =>
            {
                StartCoroutine(IdleFloatButton(boltImg.rectTransform, 6f, 2.4f, 0f));
                StartCoroutine(IdleFloatButton(sortImg.rectTransform, 6f, 2.4f, 0.5f));
                StartCoroutine(ScalePulse(sparkImg.rectTransform, 0.85f, 1.15f, 1.2f));
                StartCoroutine(ScalePulse(shopRt,                  0.97f, 1.04f, 1.6f));
            }));
        }

        // ── Entrance & idle animations ────────────────────────────────────────────

        /// <summary>Slides UI elements in from below with stagger delay. D.3-C.</summary>
        private IEnumerator StaggerEntrance(params (RectTransform rt, float delay)[] items)
        {
            AudioMgr.Instance?.PlaySFX("boltsort_title");
            foreach (var (rt, delay) in items)
            {
                if (delay > 0f) yield return new WaitForSeconds(delay);
                if (rt == null) continue;
                Vector2 finalPos = rt.anchoredPosition;
                rt.anchoredPosition = new Vector2(finalPos.x, finalPos.y - 80f);
                float dur = 0.25f, elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / dur));
                    rt.anchoredPosition = Vector2.LerpUnclamped(
                        new Vector2(finalPos.x, finalPos.y - 80f), finalPos, t);
                    yield return null;
                }
                rt.anchoredPosition = finalPos;
            }
        }

        /// <summary>Gently floats an element up and down in a loop. D.2-A.
        /// <paramref name="phase"/> (0..1) offsets the sine cycle for staggered motion.</summary>
        private IEnumerator IdleFloatButton(RectTransform rt, float amplitude, float period, float phase = 0f)
        {
            Vector2 origin  = rt.anchoredPosition;
            float   elapsed = phase * period;
            while (rt != null)
            {
                elapsed += Time.deltaTime;
                float t       = elapsed / period;
                float offsetY = Mathf.Sin(t * Mathf.PI * 2f) * amplitude;
                rt.anchoredPosition = new Vector2(origin.x, origin.y + offsetY);
                yield return null;
            }
        }

        /// <summary>Loops a sinusoidal scale pulse (breathe effect) on a RectTransform.</summary>
        private IEnumerator ScalePulse(RectTransform rt, float minScale, float maxScale, float period)
        {
            float elapsed = 0f;
            while (rt != null)
            {
                elapsed += Time.deltaTime;
                float t = (Mathf.Sin(elapsed / period * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
                float s = Mathf.Lerp(minScale, maxScale, t);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        /// <summary>Waits <paramref name="delay"/> seconds then invokes <paramref name="action"/>.</summary>
        private IEnumerator DelayedStart(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        private void OnPlayClicked()
        {
            int levelId = GetCurrentLevel();
            PlayerPrefs.SetInt("bs.next_level", levelId);
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("Gameplay");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
        }

        private void OnLevelsClicked()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("LevelSelect");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("LevelSelect");
        }

        private void OnSettingsClicked() => _settingsPanel?.Toggle();
        private void OnNoAdsClicked()    => _noAdsPopup?.Open();

        private void OnShopClicked()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("Shop");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("Shop");
        }

        private static int GetCurrentLevel()
        {
            var ss = SaveSystem.SaveSystem.Instance;
            return (ss != null && ss.IsReady) ? ss.GetCurrentLevelId() : 1;
        }

        private static void EnsureSaveSystem()
        {
            if (SaveSystem.SaveSystem.Instance == null)
                new GameObject("SaveSystem").AddComponent<SaveSystem.SaveSystem>();
        }

        private static void EnsureAudioManager()
        {
            if (AudioMgr.Instance == null)
                new GameObject("AudioManager").AddComponent<AudioMgr>();
        }

        private static void EnsureTransitionManager()
        {
            if (SceneTransitionManager.Instance == null)
                new GameObject("SceneTransitionManager")
                    .AddComponent<SceneTransitionManager>();
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                // Explicit default-action assignment for clarity. (The module also
                // self-assigns these in OnEnable when none are present, so this is
                // defensive, not load-bearing.)
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

        private static GameObject MakeAnimatedButton(GameObject parent, string name, string label,
                                                     Font font, int size, Action onClick)
        {
            var go  = new GameObject(name);
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

        /// <summary>Creates a non-interactive Image GameObject (raycast off, aspect preserved).</summary>
        private static Image MakeImage(GameObject parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            if (sprite != null) { img.sprite = sprite; img.preserveAspect = true; }
            else                  img.color  = new Color(1f, 1f, 1f, 0f); // invisible if missing
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Places a decorative diamond sprite inside the SHOP button.</summary>
        private static void MakeDiamond(GameObject parent, string name, Sprite sprite,
                                        Vector2 anchorMin, Vector2 anchorMax,
                                        Vector2 offsetMin, Vector2 offsetMax)
        {
            var img = MakeImage(parent, name, sprite);
            if (sprite == null) img.color = new Color(0.8f, 0.5f, 1f, 0.9f);
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        /// <summary>
        /// Disables the button interaction and overlays a dimmed lock icon to signal the
        /// feature is gated behind tutorial completion.
        /// </summary>
        private static void ApplyTutorialLock(GameObject btn, Font font)
        {
            // Disable the button so taps do nothing.
            var b = btn.GetComponent<Button>();
            if (b != null) b.interactable = false;

            // Semi-transparent dim so it reads as "inactive".
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = new Color(0.4f, 0.4f, 0.4f, 0.75f);

            // Hide the text label so the lock replaces it visually.
            var lbl = btn.transform.Find("Label")?.GetComponent<Text>();
            if (lbl != null) lbl.enabled = false;

            // Lock icon centered inside the button.
            var lockGO  = new GameObject("LockIcon");
            lockGO.transform.SetParent(btn.transform, false);
            var lockImg = lockGO.AddComponent<Image>();
            lockImg.raycastTarget = false;
            lockImg.preserveAspect = true;
            var lockSpr = GameAssets.LevelLock;
            if (lockSpr != null) { lockImg.sprite = lockSpr; lockImg.color = Color.white; }
            else                   lockImg.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            var lockRT = lockImg.rectTransform;
            lockRT.anchorMin = new Vector2(0.5f, 0.5f);
            lockRT.anchorMax = new Vector2(0.5f, 0.5f);
            lockRT.pivot     = new Vector2(0.5f, 0.5f);
            lockRT.sizeDelta = new Vector2(145.6f, 145.6f); // +180% of the original 52px for clear visibility
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
