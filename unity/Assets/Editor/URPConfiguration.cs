using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChromaVale.Editor
{
    /// <summary>
    /// One-click URP setup for Chroma Vale.
    /// Run via Tools > Configure URP after opening the project.
    /// </summary>
    public static class URPConfiguration
    {
        private const string PipelineAssetPath = "Assets/Settings/URP_Pipeline.asset";
        private const string RendererAssetPath = "Assets/Settings/URP_Renderer.asset";

        [MenuItem("Tools/Configure URP", priority = 0)]
        public static void ConfigureURP()
        {
            // 1. Create Settings folder
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            // 2. Create URP Renderer Data
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "URP_Renderer";

            // Set some sensible mobile defaults
            rendererData.postProcessData = null; // no post-processing by default (save perf)

            AssetDatabase.CreateAsset(rendererData, RendererAssetPath);

            // 3. Create URP Pipeline Asset
            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            pipelineAsset.name = "URP_Pipeline";

            // Mobile-friendly defaults
            pipelineAsset.supportsHDR = false;
            pipelineAsset.msaaSampleCount = 2; // 2x MSAA — good quality/perf balance
            pipelineAsset.renderScale = 1.0f;
            pipelineAsset.shadowDistance = 25f;
            pipelineAsset.shadowCascadeCount = 1; // single cascade for mobile

            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);

            // 4. Assign to Graphics settings
            var graphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var rpProp = graphicsSettings.FindProperty("m_CustomRenderPipeline");
            rpProp.objectReferenceValue = pipelineAsset;
            graphicsSettings.ApplyModifiedProperties();

            // 5. Assign to all Quality levels
            var qualitySettings = new SerializedObject(QualitySettings.GetQualitySettings());
            var qualitiesProp = qualitySettings.FindProperty("m_QualitySettings");
            for (int i = 0; i < qualitiesProp.arraySize; i++)
            {
                var quality = qualitiesProp.GetArrayElementAtIndex(i);
                var rpField = quality.FindPropertyRelative("customRenderPipeline");
                rpField.objectReferenceValue = pipelineAsset;
            }
            var perPlatformProp = qualitySettings.FindProperty("m_PerPlatformDefaultQuality");
            perPlatformProp.boolValue = false; // use the same quality for all platforms
            qualitySettings.ApplyModifiedProperties();

            // 6. Save everything
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Chroma Vale] URP configured successfully! Pipeline and Renderer created in Assets/Settings/.");
            EditorUtility.DisplayDialog("URP Configured",
                "URP Pipeline and Renderer have been created in Assets/Settings/.\n\n" +
                "The Built-in RP deprecation warning should now be gone.\n\n" +
                "You can tweak settings in the URP_Pipeline asset.",
                "OK");
        }

        [MenuItem("Tools/Configure URP", validate = true)]
        public static bool ValidateConfigureURP()
        {
            // Only enable if URP isn't already configured
            return GraphicsSettings.currentRenderPipeline == null;
        }
    }
}
