using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Drives a UI text element from a localization key. Updates on <see cref="OnEnable"/>,
    /// on <see cref="MonoBehaviour.Start"/>, and whenever the language changes, so text swaps
    /// live without reloading the scene.
    ///
    /// Targets a legacy <see cref="Text"/> if present; otherwise binds to a TextMeshPro
    /// component via reflection (no hard dependency on the TMP package). Enables best-fit
    /// auto-sizing by default so longer translations (German, Russian, French …) shrink to
    /// fit their container instead of clipping or overflowing the button.
    /// </summary>
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _translationKey;
        [SerializeField] private bool   _autoSize = true;

        /// <summary>Localization key resolved into the target text's content.</summary>
        public string translationKey
        {
            get => _translationKey;
            set { _translationKey = value; Refresh(); }
        }

        /// <summary>When true (default), enables font best-fit so long translations never clip.</summary>
        public bool AutoSize
        {
            get => _autoSize;
            set { _autoSize = value; _autoSizeApplied = false; Refresh(); }
        }

        /// <summary>Optional provider of format arguments for keys containing {0} placeholders.
        /// When set, the translation is treated as a format string.</summary>
        public Func<object[]> ArgsProvider { get; set; }

        private Text          _text;        // legacy UI.Text (preferred path)
        private Component     _tmp;          // TMP_Text instance, if no legacy Text
        private PropertyInfo  _tmpTextProp;
        private bool          _resolved;
        private bool          _autoSizeApplied;
        private Font          _originalFont;   // font assigned at bind time (kept for Latin scripts)
        private static Font   _fallbackFont;   // shared Unicode fallback for non-Latin scripts

        /// <summary>Built-in dynamic font with broad Unicode / OS fallback coverage (Cyrillic, CJK, Devanagari).</summary>
        private static Font FallbackFont =>
            _fallbackFont != null ? _fallbackFont
            : (_fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (_resolved) return;
            _text = GetComponent<Text>();
            if (_text != null) _originalFont = _text.font;
            else ResolveTmp();
            _resolved = true;
        }

        /// <summary>Locates a TextMeshPro component (TMP_Text base) without referencing the package.</summary>
        private void ResolveTmp()
        {
            foreach (var c in GetComponents<Component>())
            {
                if (c == null) continue;
                for (var t = c.GetType(); t != null; t = t.BaseType)
                {
                    if (t.Name != "TMP_Text") continue;
                    _tmp = c;
                    _tmpTextProp = t.GetProperty("text");
                    return;
                }
            }
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => LocalizationManager.LanguageChanged -= Refresh;

        private void Start() => Refresh();

        /// <summary>Re-applies the current translation (and best-fit settings) to the target text.</summary>
        public void Refresh()
        {
            Resolve();
            var mgr = LocalizationManager.Instance;
            if (mgr == null || string.IsNullOrEmpty(_translationKey)) return;

            string value = ArgsProvider != null
                ? mgr.Format(_translationKey, ArgsProvider())
                : mgr.Get(_translationKey);

            ApplyAutoSize();

            if (_text != null)
            {
                ApplyScriptFont(mgr.CurrentLanguage);
                _text.text = value;
            }
            else _tmpTextProp?.SetValue(_tmp, value);
        }

        /// <summary>Enables best-fit (legacy) / auto-sizing (TMP) once, capping max at the original size.</summary>
        private void ApplyAutoSize()
        {
            // AutoSize disabled: actively clear best-fit/auto-sizing. A prior Refresh (e.g. the
            // translationKey setter during Bind) may have enabled it before AutoSize was set
            // false; without clearing it here, AutoSize=false is a no-op and fontSize is ignored.
            if (!_autoSize)
            {
                if (_text != null) _text.resizeTextForBestFit = false;
                else _tmp?.GetType().GetProperty("enableAutoSizing")?.SetValue(_tmp, false);
                _autoSizeApplied = true;
                return;
            }
            if (_autoSizeApplied) return;
            _autoSizeApplied = true;

            if (_text != null)
            {
                int max = _text.resizeTextForBestFit ? _text.resizeTextMaxSize : _text.fontSize;
                if (max <= 0) max = _text.fontSize;
                _text.resizeTextForBestFit = true;
                _text.resizeTextMaxSize    = max;
                _text.resizeTextMinSize    = Mathf.Max(8, Mathf.RoundToInt(max * 0.45f));
                return;
            }

            if (_tmp != null)
            {
                var tt = _tmp.GetType();
                tt.GetProperty("enableAutoSizing")?.SetValue(_tmp, true);
            }
        }

        /// <summary>Swaps to the Unicode fallback font for non-Latin scripts (Cyrillic, CJK,
        /// Devanagari) which the GummyPop display font does not cover; restores the original
        /// font for Latin-script languages so the menu style is preserved.</summary>
        private void ApplyScriptFont(string language)
        {
            if (_text == null) return;
            var desired = LocalizationManager.NeedsFallbackFont(language) ? FallbackFont : _originalFont;
            if (desired != null && _text.font != desired) _text.font = desired;
        }

        // ── Helpers for procedurally-built UI ─────────────────────────────────────

        /// <summary>Attaches a <see cref="LocalizedText"/> to <paramref name="text"/> bound to a static key.</summary>
        public static LocalizedText Bind(Text text, string key)
        {
            if (text == null) return null;
            var lt = text.gameObject.GetComponent<LocalizedText>()
                     ?? text.gameObject.AddComponent<LocalizedText>();
            lt.translationKey = key;
            return lt;
        }

        /// <summary>Attaches a <see cref="LocalizedText"/> bound to a format key with live arguments.</summary>
        public static LocalizedText BindFormat(Text text, string key, Func<object[]> argsProvider)
        {
            if (text == null) return null;
            var lt = text.gameObject.GetComponent<LocalizedText>()
                     ?? text.gameObject.AddComponent<LocalizedText>();
            lt.ArgsProvider = argsProvider;
            lt.translationKey = key;
            return lt;
        }
    }
}
