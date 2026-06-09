using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    public static class BuildScript
    {
        private const string ApkOutputPath = "Builds/Android/BoltSort.apk";

        [MenuItem("BoltSort/Build Android APK")]
        public static void BuildAndroidMenu() => BuildAndroid();

        /// <summary>Called from command line: -executeMethod BoltSort.Editor.BuildScript.BuildAndroid</summary>
        public static void BuildAndroid()
        {
            string outputPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../", ApkOutputPath));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var options = new BuildPlayerOptions
            {
                scenes     = GetEnabledScenes(),
                locationPathName = outputPath,
                target     = BuildTarget.Android,
                options    = BuildOptions.Development | BuildOptions.AllowDebugging,
            };

            Debug.Log($"[BuildScript] Building APK → {outputPath}");
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log($"[BuildScript] Build succeeded — {report.summary.totalSize / 1024 / 1024} MB");
            else
                throw new Exception($"[BuildScript] Build FAILED: {report.summary.result}");
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);
            return scenes.ToArray();
        }
    }
}
