using System;
using UnityEngine;
using UnityEngine.UI;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Modal settings overlay. Call Initialize() once after the parent Canvas is created,
    /// then Toggle() to show/hide. Stores music and SFX prefs in PlayerPrefs.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private const string MusicKey = "bs.music_on";
        private const string SfxKey   = "bs.sfx_on";

        private GameObject _overlay;

        public void Initialize(Font font, Transform canvasRoot)
        {
            BuildOverlay(font, canvasRoot);
            _overlay.SetActive(false);
        }

        public void Toggle()
        {
            if (_overlay != null)
                _overlay.SetActive(!_overlay.activeSelf);
        }

        private void BuildOverlay(Font font, Transform canvasRoot)
        {
            // Full-screen dim background
            _overlay = new GameObject("SettingsOverlay");
            _overlay.transform.SetParent(canvasRoot, false);

            var dimImg = _overlay.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.7f);
            Stretch(_overlay.GetComponent<RectTransform>());

            // Block raycasts so dim background is tappable (closes panel)
            var btn = _overlay.AddComponent<Button>();
            btn.onClick.AddListener(() => _overlay.SetActive(false));

            // Card
            var card = new GameObject("Card");
            card.transform.SetParent(_overlay.transform, false);
            card.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.14f, 0.98f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
            cardRt.pivot            = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = new Vector2(500f, 560f);

            // Prevent taps on card from propagating to dim-close button
            card.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible blocker

            float y = 200f;

            // Title
            AddLabel(card, "Title", "SETTINGS", font, 52, TextAnchor.MiddleCenter, Color.white, y);
            y -= 90f;

            // Music toggle
            AddToggleRow(card, font, "Music", MusicKey, y, newVal =>
            {
                PlayerPrefs.SetInt(MusicKey, newVal ? 1 : 0);
                PlayerPrefs.Save();
            });
            y -= 80f;

            // SFX toggle
            AddToggleRow(card, font, "SFX", SfxKey, y, newVal =>
            {
                PlayerPrefs.SetInt(SfxKey, newVal ? 1 : 0);
                PlayerPrefs.Save();
            });
            y -= 80f;

            // Rate button
            AddActionButton(card, font, "Rate the Game ★", y, () => { /* placeholder */ });
            y -= 70f;

            // Privacy Policy button
            AddActionButton(card, font, "Privacy Policy", y, () => { /* placeholder */ });
            y -= 70f;

            // Version
            AddLabel(card, "Version", $"v{Application.version}", font, 26,
                     TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.7f, 1f), y);
            y -= 50f;

            // Close button
            var closeBtn = CreateButton(card, "CloseBtn", "✕  Close", font, 36,
                                        new Color(0.290f, 0.565f, 0.851f, 1f),
                                        () => _overlay.SetActive(false));
            var cr = closeBtn.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot     = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(0f, y);
            cr.sizeDelta        = new Vector2(280f, 64f);
        }

        private static void AddLabel(GameObject parent, string name, string text, Font font,
                                     int size, TextAnchor anchor, Color color, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = anchor;
            t.color = color; t.supportRichText = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta        = new Vector2(0f, 50f);
        }

        private static void AddToggleRow(GameObject parent, Font font, string label,
                                         string prefKey, float y, Action<bool> onChange)
        {
            bool current = PlayerPrefs.GetInt(prefKey, 1) == 1;

            // Label
            var lgo = new GameObject($"{label}Label");
            lgo.transform.SetParent(parent.transform, false);
            var lt = lgo.AddComponent<Text>();
            lt.text = label; lt.font = font; lt.fontSize = 36;
            lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleLeft;
            lt.color = Color.white; lt.supportRichText = false;
            var lr = lgo.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 0.5f); lr.anchorMax = new Vector2(0.6f, 0.5f);
            lr.pivot     = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(24f, y);
            lr.sizeDelta        = new Vector2(0f, 50f);

            // Toggle button (ON/OFF)
            var tgo = new GameObject($"{label}Toggle");
            tgo.transform.SetParent(parent.transform, false);
            var tImg = tgo.AddComponent<Image>();
            tImg.color = current ? new Color(0.20f, 0.65f, 0.30f) : new Color(0.50f, 0.20f, 0.20f);
            var tr = tgo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(1f, 0.5f); tr.anchorMax = new Vector2(1f, 0.5f);
            tr.pivot     = new Vector2(1f, 0.5f);
            tr.anchoredPosition = new Vector2(-24f, y);
            tr.sizeDelta        = new Vector2(110f, 50f);

            var tLabel = new GameObject("Label");
            tLabel.transform.SetParent(tgo.transform, false);
            var tlt = tLabel.AddComponent<Text>();
            tlt.text = current ? "ON" : "OFF"; tlt.font = font; tlt.fontSize = 30;
            tlt.fontStyle = FontStyle.Bold; tlt.alignment = TextAnchor.MiddleCenter;
            tlt.color = Color.white; tlt.supportRichText = false;
            var tlr = tLabel.GetComponent<RectTransform>();
            tlr.anchorMin = Vector2.zero; tlr.anchorMax = Vector2.one;
            tlr.offsetMin = tlr.offsetMax = Vector2.zero;

            var tBtn = tgo.AddComponent<Button>();
            tBtn.onClick.AddListener(() =>
            {
                bool next = PlayerPrefs.GetInt(prefKey, 1) != 1;
                onChange(next);
                tImg.color = next ? new Color(0.20f, 0.65f, 0.30f) : new Color(0.50f, 0.20f, 0.20f);
                tlt.text   = next ? "ON" : "OFF";
            });
        }

        private static void AddActionButton(GameObject parent, Font font, string label,
                                            float y, Action onClick)
        {
            var btn = CreateButton(parent, label, label, font, 30,
                                   new Color(0.15f, 0.15f, 0.28f, 1f), onClick);
            var r = btn.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f); r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot     = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta        = new Vector2(360f, 54f);
        }

        private static GameObject CreateButton(GameObject parent, string name, string label,
                                               Font font, int size, Color bgColor, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = bgColor;
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
    }
}
