using System.IO;
using UnityEditor;
using UnityEngine;
using BoltSort.Gameplay;

namespace BoltSort.EditorTools
{
    /// <summary>
    /// BoltSort ▸ Create Reward Config Asset — creates (or selects) the runtime-loaded
    /// <see cref="RewardConfig"/> at <c>Assets/Resources/RewardConfig.asset</c> so the values are
    /// editable in the Inspector. The game falls back to code defaults when the asset is absent,
    /// so running this is optional — it only exposes the knobs.
    /// </summary>
    public static class RewardConfigCreator
    {
        private const string ResourcesDir = "Assets/Resources";
        private const string AssetPath    = "Assets/Resources/RewardConfig.asset";

        [MenuItem("BoltSort/Create Reward Config Asset")]
        public static void CreateOrSelect()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RewardConfig>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[RewardConfigCreator] Asset already exists at {AssetPath}.");
                return;
            }

            if (!Directory.Exists(ResourcesDir))
                Directory.CreateDirectory(ResourcesDir);

            var asset = ScriptableObject.CreateInstance<RewardConfig>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[RewardConfigCreator] Created {AssetPath}.");
        }
    }
}
