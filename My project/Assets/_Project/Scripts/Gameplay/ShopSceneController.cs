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
    /// Procedurally builds the standalone Shop scene. Attach to a Bootstrap GameObject
    /// in Shop.unity. Currently a functional shell — header + back navigation — ready to
    /// be filled with the catalogue later. Returns to MainMenu via SceneTransitionManager.
    /// </summary>
    public class ShopSceneController : MonoBehaviour
    {
        private void Start()
        {
            EnsureAudioManager();
            EnsureTransitionManager();
            EnsureEventSystem();
            ConfigureCamera();
            BuildUI();
        }

        private void Update()
        {
            // Android back button → return to main menu.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                GoBack();
        }

        private void BuildUI()
        {
            Font font = GameAssets.MenuFont;

            var canvasGO = new GameObject("Canvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight  = 1f;
            canvasGO.AddComponent<GraphicRaycaster>();

            float lpu     = 1280f / Screen.height;
            float safeTop = (Screen.height - Screen.safeArea.yMax) * lpu;

            // Background
            var bg    = new GameObject("Background");
            bg.transform.SetParent(canvasGO.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = BoltSortTheme.BackgroundDeep;
            GameAssets.Apply(bgImg, GameAssets.GameBackground);
            Stretch(bgImg.rectTransform);

            // Header bar
            var header = new GameObject("Header");
            header.transform.SetParent(canvasGO.transform, false);
            header.AddComponent<Image>().color = BoltSortTheme.HUDBackground;
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -(110f + safeTop)); hr.offsetMax = Vector2.zero;

            // Title "SHOP"
            var title = MakeLabel(header, "Title", "SHOP", font, 52,
                                  TextAnchor.MiddleCenter, bold: true, shadow: true);
            title.color = BoltSortTheme.HUDText;
            var tr = title.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = new Vector2(0f, -safeTop);

            // Back / home button (top-left) — home_button.png
            var backBtn = new GameObject("BackButton");
            backBtn.transform.SetParent(header.transform, false);
            var backImg = backBtn.AddComponent<Image>();
            if (GameAssets.BtnHomeAction != null)
                GameAssets.Apply(backImg, GameAssets.BtnHomeAction, preserveAspect: true);
            else if (GameAssets.NavBack != null)
                GameAssets.Apply(backImg, GameAssets.NavBack, preserveAspect: true);
            else
                backImg.color = new Color(0.12f, 0.12f, 0.22f, 0.9f);
            var bb = backBtn.AddComponent<Button>();
            bb.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            bb.onClick.AddListener(GoBack);
            var bbr = backBtn.GetComponent<RectTransform>();
            bbr.anchorMin = new Vector2(0f, 0f); bbr.anchorMax = new Vector2(0f, 1f);
            bbr.pivot     = new Vector2(0f, 0.5f);
            bbr.offsetMin = new Vector2(10f, 12f); bbr.offsetMax = new Vector2(98f, -(12f + safeTop));

            // Placeholder body text (catalogue arrives later)
            var soon = MakeLabel(canvasGO, "ComingSoon", "Coming soon!", font, 36,
                                 TextAnchor.MiddleCenter, bold: true, shadow: true);
            soon.color = new Color(1f, 1f, 1f, 0.7f);
            var sr = soon.rectTransform;
            sr.anchorMin = new Vector2(0.1f, 0.45f); sr.anchorMax = new Vector2(0.9f, 0.55f);
            sr.offsetMin = sr.offsetMax = Vector2.zero;
        }

        private void GoBack()
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.TransitionTo("MainMenu");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // ── Setup helpers ─────────────────────────────────────────────────────────

        private static void EnsureAudioManager()
        {
            if (AudioMgr.Instance == null)
                new GameObject("AudioManager").AddComponent<AudioMgr>();
        }

        private static void EnsureTransitionManager()
        {
            if (SceneTransitionManager.Instance == null)
                new GameObject("SceneTransitionManager").AddComponent<SceneTransitionManager>();
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

        private static Text MakeLabel(GameObject parent, string name, string text, Font font,
                                      int size, TextAnchor anchor, bool bold, bool shadow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = anchor; t.color = Color.white; t.supportRichText = false;
            t.raycastTarget = false;
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor    = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
