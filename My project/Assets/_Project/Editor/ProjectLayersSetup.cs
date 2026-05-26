using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    [InitializeOnLoad]
    public static class ProjectLayersSetup
    {
        static ProjectLayersSetup()
        {
            EnsureLayer("BoltStacks");
        }

        private static void EnsureLayer(string layerName)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");

            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                    return;
            }

            // User layers start at index 8
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var element = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[ProjectLayersSetup] Registered layer '{layerName}' at index {i}.");
                    return;
                }
            }

            Debug.LogWarning($"[ProjectLayersSetup] No empty layer slot available for '{layerName}'.");
        }
    }
}
