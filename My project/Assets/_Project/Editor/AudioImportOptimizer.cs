using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// BoltSort > Optimize Audio Import Settings — applies mobile-optimal Vorbis
    /// settings to all clips in Assets/Resources/Audio/.
    /// SFX: DecompressOnLoad, 22050Hz, 70% quality.
    /// BGM: Streaming, 44100Hz, 60% quality.
    /// Run this after adding new audio clips to re-apply the settings.
    /// </summary>
    public static class AudioImportOptimizer
    {
        [MenuItem("BoltSort/Optimize Audio Import Settings")]
        public static void OptimizeAll()
        {
            int count = 0;
            string audioDir = "Assets/Resources/Audio";

            if (!Directory.Exists(audioDir))
            {
                Debug.LogWarning("[AudioImportOptimizer] Directory not found: " + audioDir);
                return;
            }

            foreach (string file in Directory.GetFiles(audioDir))
            {
                if (file.EndsWith(".meta")) continue;
                string ext = Path.GetExtension(file).ToLower();
                if (ext != ".wav" && ext != ".ogg" && ext != ".mp3" && ext != ".aiff") continue;

                string assetPath = "Assets/Resources/Audio/" + Path.GetFileName(file);
                var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                if (importer == null) continue;

                bool isBgm = Path.GetFileNameWithoutExtension(file).StartsWith("bgm");
                Apply(importer, isBgm);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AudioImportOptimizer] Applied settings to {count} clips in {audioDir}");
            EditorUtility.DisplayDialog("Audio Import Optimizer",
                $"Done. Applied optimized settings to {count} clip(s).", "OK");
        }

        private static void Apply(AudioImporter importer, bool isBgm)
        {
            importer.forceToMono = true;

            var settings = importer.defaultSampleSettings;
            settings.compressionFormat  = AudioCompressionFormat.Vorbis;
            settings.quality            = isBgm ? 0.60f : 0.70f;
            settings.loadType           = isBgm
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.DecompressOnLoad;
            settings.sampleRateSetting  = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = isBgm ? 44100u : 22050u;
            settings.preloadAudioData   = !isBgm;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground   = isBgm;

            importer.SaveAndReimport();
        }
    }
}
