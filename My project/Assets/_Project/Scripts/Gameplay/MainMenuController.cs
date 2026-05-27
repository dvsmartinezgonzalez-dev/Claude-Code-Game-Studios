using System;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private void Start()
        {
            EnsureSaveSystem();
            EnsureAudioManager();
            EnsureTransitionManager();
            EnsureEventSystem();
            ConfigureCamera();
            BuildUI();
            AudioMgr.Instance?.PlayMusic();
        }

        private void BuildUI()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

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

            // Title
            var titleText = MakeLabel(canvasGO, "Title", "BOLT SORT", font,
                                      100, TextAnchor.MiddleCenter, bold: true, shadow: true);
            titleText.color = BoltSortTheme.WinGold;
            SetAnchors(titleText.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.76f));

            // PLAY button
            var playBtn = MakeAnimatedButton(canvasGO, "PlayButton", "PLAY", font, 56, OnPlayClicked);
            playBtn.GetComponent<Image>().color = BoltSortTheme.HUDAccent;
            SetAnchors(playBtn.GetComponent<RectTransform>(),
                new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.54f));

            // LEVELS button
            var levelsBtn = MakeAnimatedButton(canvasGO, "LevelsButton", "LEVELS", font, 48, OnLevelsClicked);
            levelsBtn.GetComponent<Image>().color = new Color(0.20f, 0.40f, 0.65f, 1f);
            SetAnchors(levelsBtn.GetComponent<RectTransform>(),
                new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.39f));

            // Settings button
            var settingsBtn = MakeAnimatedButton(canvasGO, "SettingsButton", "⚙", font, 44, OnSettingsClicked);
            settingsBtn.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var sr = settingsBtn.GetComponent<RectTransform>();
            sr.anchorMin        = new Vector2(0f, 1f);
            sr.anchorMax        = new Vector2(0f, 1f);
            sr.pivot            = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(16f, -16f);
            sr.sizeDelta        = new Vector2(80f, 80f);

            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform);
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
