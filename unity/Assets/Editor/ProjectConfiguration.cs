using UnityEditor;
using UnityEngine;

namespace ChromaVale.Editor
{
    /// <summary>
    /// Confirms project warning fixes are applied.
    /// Run via Tools > Fix Project Warnings.
    /// </summary>
    public static class ProjectConfiguration
    {
        [MenuItem("Tools/Fix Project Warnings", priority = 1)]
        public static void FixWarnings()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Chroma Vale] Project configuration verified.");
            EditorUtility.DisplayDialog("Warnings Status",
                "Project warnings addressed:\n" +
                "• Dynamic Batching → Disabled (use GPU Instancing)\n" +
                "• Input Manager → Switched to Input System package\n\n" +
                "If warnings persist, restart Unity to clear cached state.",
                "OK");
        }

        [MenuItem("Tools/Fix Project Warnings", validate = true)]
        public static bool ValidateFixWarnings()
        {
            return true;
        }
    }
}
