using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Lightweight JSON-backed localization singleton. Reads flat
    /// <c>{ "key": "value" }</c> tables from <c>Resources/Languages/{code}.json</c>,
    /// persists the chosen language in PlayerPrefs, and notifies listeners on change
    /// so the UI swaps languages live without reloading the scene. Default = English.
    /// </summary>
    public class LocalizationManager : MonoBehaviour
    {
        public const string PlayerPrefsKey = "current_language";
        public const string DefaultLanguage = "en";

        /// <summary>Supported language codes — each must have a matching JSON in Resources/Languages.</summary>
        public static readonly string[] SupportedLanguages =
            { "en", "es", "pt", "fr", "de", "it", "ru", "zh", "ja", "hi" };

        public static LocalizationManager Instance { get; private set; }

        /// <summary>Raised after the active language table changes. Listeners should re-read translations.</summary>
        public static event Action LanguageChanged;

        public string CurrentLanguage { get; private set; } = DefaultLanguage;

        private readonly Dictionary<string, string> _table = new Dictionary<string, string>();

        /// <summary>Creates the manager before the first scene loads so translations are
        /// available everywhere without per-scene bootstrap code.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("LocalizationManager");
            go.AddComponent<LocalizationManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            LoadTable(ResolveInitialLanguage());
        }

        /// <summary>Saved preference if valid; otherwise the default language (English).</summary>
        private static string ResolveInitialLanguage()
        {
            string saved = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            return (!string.IsNullOrEmpty(saved) && IsSupported(saved)) ? saved : DefaultLanguage;
        }

        public static bool IsSupported(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            foreach (var c in SupportedLanguages)
                if (c == code) return true;
            return false;
        }

        /// <summary>Languages whose script (Cyrillic / CJK / Devanagari) is not covered by the
        /// GummyPop display font — these must render with a Unicode fallback font instead.</summary>
        private static readonly HashSet<string> NonLatinScripts = new HashSet<string> { "ru", "zh", "ja", "hi" };

        /// <summary>True when <paramref name="code"/> needs the Unicode fallback font (non-Latin script).</summary>
        public static bool NeedsFallbackFont(string code) => code != null && NonLatinScripts.Contains(code);

        /// <summary>Switches the active language: loads its table, persists the choice,
        /// fires <see cref="LanguageChanged"/>, and refreshes every <see cref="LocalizedText"/>.</summary>
        public void SetLanguage(string code)
        {
            LoadTable(code);
            PlayerPrefs.SetString(PlayerPrefsKey, CurrentLanguage);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
            RefreshAllLocalizedTexts();
        }

        /// <summary>Loads the JSON table for <paramref name="code"/> (falling back to English).</summary>
        private void LoadTable(string code)
        {
            if (!IsSupported(code)) code = DefaultLanguage;

            var asset = Resources.Load<TextAsset>($"Languages/{code}");
            if (asset == null && code != DefaultLanguage)
            {
                code  = DefaultLanguage;
                asset = Resources.Load<TextAsset>($"Languages/{code}");
            }

            _table.Clear();
            if (asset != null) ParseFlatJson(asset.text, _table);
            else Debug.LogWarning($"[Localization] Missing language file: Languages/{code}.json");

            CurrentLanguage = code;
        }

        /// <summary>Returns the translation for <paramref name="key"/>, or the key itself if missing.</summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return _table.TryGetValue(key, out var value) ? value : key;
        }

        /// <summary><see cref="Get"/> formatted with <paramref name="args"/> (for keys containing {0} …).</summary>
        public string Format(string key, params object[] args)
        {
            string fmt = Get(key);
            try { return string.Format(fmt, args); }
            catch (FormatException) { return fmt; }
        }

        /// <summary>Refreshes every <see cref="LocalizedText"/> in all loaded scenes, including inactive ones.</summary>
        public static void RefreshAllLocalizedTexts()
        {
            var all = Resources.FindObjectsOfTypeAll<LocalizedText>();
            foreach (var lt in all)
            {
                // Skip prefab assets / non-scene objects; only refresh instantiated scene objects.
                if (lt == null || lt.gameObject.scene.rootCount == 0) continue;
                lt.Refresh();
            }
        }

        // ── Minimal flat-object JSON parser ───────────────────────────────────────
        // Handles a single-level { "key": "value", ... } object with standard string
        // escapes. Avoids a heavyweight dependency for the small, controlled language files.
        private static void ParseFlatJson(string json, Dictionary<string, string> into)
        {
            int i = 0, n = json.Length;
            while (i < n)
            {
                if (json[i] != '"') { i++; continue; }
                string key = ReadString(json, ref i);
                while (i < n && json[i] != ':') i++;
                i++; // skip ':'
                while (i < n && json[i] != '"' && json[i] != '}') i++;
                if (i >= n || json[i] != '"') break;
                string value = ReadString(json, ref i);
                into[key] = value;
            }
        }

        /// <summary>Reads a JSON string starting at the opening quote (json[i] == '"').</summary>
        private static string ReadString(string json, ref int i)
        {
            var sb = new System.Text.StringBuilder();
            i++; // skip opening quote
            int n = json.Length;
            while (i < n)
            {
                char c = json[i++];
                if (c == '"') break;
                if (c == '\\' && i < n)
                {
                    char e = json[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '"': sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/');  break;
                        case 'u':
                            if (i + 4 <= n &&
                                ushort.TryParse(json.Substring(i, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out var code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
