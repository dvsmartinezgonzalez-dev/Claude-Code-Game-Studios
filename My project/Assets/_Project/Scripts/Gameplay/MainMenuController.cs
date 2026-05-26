using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Procedurally builds the Main Menu UI. Attach to any root GameObject in MainMenu.unity.
    /// Creates PLAY, LEVELS, and Settings buttons; reads last saved level from SaveSystem.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private static readonly Color BgColor     = new Color(0.051f, 0.051f, 0.102f, 1f);
        private static readonly Color AccentColor = new Color(0.290f, 0.565f, 0.851f, 1f);
        private static readonly Color DimColor    = new Color(0.20f,  0.40f,  0.65f,  1f);
        private static readonly Color TitleColor  = new Color(0.98f,  0.85f,  0.30f,  1f);

        private SettingsPanel _settingsPanel;

        private void Start()
        {
            EnsureSaveSystem();
            EnsureEventSystem();
            ConfigureCamera();
            BuildUI();
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
            var bg = MakePanel(canvasGO, "Background", BgColor);
            Stretch(bg.GetComponent<RectTransform>());

            // Title
            var titleText = MakeLabel(canvasGO, "Title", "BOLT SORT", font,
                                      100, TextAnchor.MiddleCenter, bold: true, shadow: true);
            titleText.color = TitleColor;
            SetAnchors(titleText.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.76f));

            // PLAY button
            var playBtn = MakeButton(canvasGO, "PlayButton", "PLAY", font, 56, OnPlayClicked);
            playBtn.GetComponent<Image>().color = AccentColor;
            SetAnchors(playBtn.GetComponent<RectTransform>(),
                new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.54f));

            // LEVELS button
            var levelsBtn = MakeButton(canvasGO, "LevelsButton", "LEVELS", font, 48, OnLevelsClicked);
            levelsBtn.GetComponent<Image>().color = DimColor;
            SetAnchors(levelsBtn.GetComponent<RectTransform>(),
                new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.39f));

            // Settings button (top-left gear icon)
            var settingsBtn = MakeButton(canvasGO, "SettingsButton", "⚙", font, 44, OnSettingsClicked);
            settingsBtn.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var sr = settingsBtn.GetComponent<RectTransform>();
            sr.anchorMin        = new Vector2(0f, 1f);
            sr.anchorMax        = new Vector2(0f, 1f);
            sr.pivot            = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(16f, -16f);
            sr.sizeDelta        = new Vector2(80f, 80f);

            // Settings panel (hidden initially)
            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform);
        }

        private void OnPlayClicked()
        {
            int levelId = GetCurrentLevel();
            PlayerPrefs.SetInt("bs.next_level", levelId);
            SceneManager.LoadScene("Gameplay");
        }

        private void OnLevelsClicked() => SceneManager.LoadScene("LevelSelect");

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
