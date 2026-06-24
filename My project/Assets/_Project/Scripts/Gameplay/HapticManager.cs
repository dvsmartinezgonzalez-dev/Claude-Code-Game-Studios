using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Lightweight cross-platform haptic feedback. Static — no scene object required.
    /// Three intensities map to platform vibration:
    ///   <see cref="Light"/>  → ball select, button press (~10ms)
    ///   <see cref="Medium"/> → ball land, tube complete  (~30ms)
    ///   <see cref="Heavy"/>  → win condition             (~60ms)
    /// Android drives the system Vibrator (graded one-shot durations, default amplitude on
    /// API 26+). iOS falls back to <c>Handheld.Vibrate()</c> — the only built-in API; true
    /// taptic intensity needs a native plugin, which this project deliberately does not add.
    /// Editor and unsupported platforms are no-ops. Globally gated by <see cref="Enabled"/>,
    /// persisted to PlayerPrefs ("haptics_enabled", default on).
    /// </summary>
    public static class HapticManager
    {
        private const string EnabledKey = "haptics_enabled";

        private static bool _loaded;
        private static bool _enabled = true;

        /// <summary>Global on/off, persisted across sessions. Every Light/Medium/Heavy checks this.</summary>
        public static bool Enabled
        {
            get
            {
                if (!_loaded)
                {
                    _enabled = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
                    _loaded  = true;
                }
                return _enabled;
            }
            set
            {
                _enabled = value;
                _loaded  = true;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void Light()  => Fire(10);
        public static void Medium() => Fire(30);
        public static void Heavy()  => Fire(60);

        private static void Fire(long milliseconds)
        {
            if (!Enabled) return;
#if UNITY_EDITOR
            // Editor: no-op (avoids spurious vibration during play-in-editor).
#elif UNITY_ANDROID
            AndroidVibrate(milliseconds);
#elif UNITY_IOS
            Handheld.Vibrate();
#endif
        }

#if !UNITY_EDITOR && UNITY_ANDROID
        private static AndroidJavaObject _vibrator;
        private static int _apiLevel = -1;
        private static bool _initFailed;

        private static void AndroidVibrate(long ms)
        {
            try
            {
                if (_vibrator == null && !_initFailed)
                {
                    using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using var version = new AndroidJavaClass("android.os.Build$VERSION");
                    _apiLevel = version.GetStatic<int>("SDK_INT");
                }
                if (_vibrator == null) { _initFailed = true; return; }

                if (_apiLevel >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    const int defaultAmplitude = -1; // VibrationEffect.DEFAULT_AMPLITUDE
                    using var effect = effectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", ms, defaultAmplitude);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", ms);
                }
            }
            catch
            {
                // No vibrator hardware / permission denied — degrade silently, never retry init.
                _initFailed = true;
            }
        }
#endif
    }
}
