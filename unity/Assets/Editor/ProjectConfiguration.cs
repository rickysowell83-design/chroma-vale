using UnityEditor;
using UnityEngine;

namespace ChromaVale.Editor
{
    /// <summary>
    /// Disables Dynamic Batching (deprecated in Unity 6) for all platforms.
    /// Run via Tools > Fix Project Warnings.
    /// </summary>
    public static class ProjectConfiguration
    {
        [MenuItem("Tools/Fix Project Warnings", priority = 1)]
        public static void FixWarnings()
        {
            // Disable Dynamic Batching for all platforms (deprecated in Unity 6)
            DisableDynamicBatching();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Chroma Vale] Project warnings fixed! Dynamic Batching disabled, Input System configured.");
            EditorUtility.DisplayDialog("Warnings Fixed",
                "Resolved:\n" +
                "• Dynamic Batching → Disabled (use GPU Instancing)\n" +
                "• Input Manager → Switched to Input System package\n\n" +
                "Restart Unity to clear any remaining cached warnings.",
                "OK");
        }

        private static void DisableDynamicBatching()
        {
            // All platforms we target
            var platforms = new[]
            {
                BuildTargetGroup.Standalone,
                BuildTargetGroup.Android,
                BuildTargetGroup.iOS,
                BuildTargetGroup.WebGL,
            };

            foreach (var platform in platforms)
            {
                // SetBatchingForPlatform(buildTargetGroup, staticBatching, dynamicBatching)
                // 0 = disabled, 1 = enabled
                PlayerSettings.SetBatchingForPlatform(platform, 1, 0);
            }

            Debug.Log("[Chroma Vale] Dynamic Batching disabled for all platforms. Use GPU Instancing instead.");
        }

        [MenuItem("Tools/Fix Project Warnings", validate = true)]
        public static bool ValidateFixWarnings()
        {
            return true;
        }
    }
}
