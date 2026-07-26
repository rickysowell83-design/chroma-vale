using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChromaVale.Editor
{
    /// <summary>
    /// Unity Editor build pipeline scripts for mobile builds.
    /// Exposed via MenuItem so MCP can drive builds via execute_menu_item.
    /// </summary>
    public static class BuildTools
    {
        private const string MenuRoot = "ChromaVale/Build/";

        // ──────────────────────────────────────────────
        // 1. SetBuildTarget
        // ──────────────────────────────────────────────
        [MenuItem(MenuRoot + "Set Target/Android", priority = 10)]
        public static void SetBuildTargetAndroid() => SetBuildTarget("Android");

        [MenuItem(MenuRoot + "Set Target/iOS", priority = 11)]
        public static void SetBuildTargetIOS() => SetBuildTarget("iOS");

        /// <summary>
        /// Switches the active build platform.
        /// Valid <paramref name="platform"/> values: "Android", "iOS".
        /// </summary>
        public static void SetBuildTarget(string platform)
        {
            BuildTarget target;
            BuildTargetGroup targetGroup;

            switch (platform.ToLowerInvariant())
            {
                case "android":
                    target = BuildTarget.Android;
                    targetGroup = BuildTargetGroup.Android;
                    break;
                case "ios":
                    target = BuildTarget.iOS;
                    targetGroup = BuildTargetGroup.iOS;
                    break;
                default:
                    Debug.LogError($"[BuildTools] Unknown platform: '{platform}'. Valid values: Android, iOS.");
                    return;
            }

            if (EditorUserBuildSettings.activeBuildTarget == target)
            {
                Debug.Log($"[BuildTools] Already on {platform}. No switch needed.");
                return;
            }

            Debug.Log($"[BuildTools] Switching build target to {platform}...");
            bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target);
            if (success)
            {
                Debug.Log($"[BuildTools] Successfully switched to {platform}.");
            }
            else
            {
                Debug.LogError($"[BuildTools] Failed to switch build target to {platform}.");
            }
        }

        // ──────────────────────────────────────────────
        // 2. IncrementBundleVersion
        // ──────────────────────────────────────────────
        [MenuItem(MenuRoot + "Increment Version", priority = 20)]
        public static void IncrementBundleVersion()
        {
            string currentVersion = PlayerSettings.bundleVersion;
            Debug.Log($"[BuildTools] Current bundleVersion: {currentVersion}");

            string newVersion = IncrementPatch(currentVersion);
            if (newVersion == currentVersion)
            {
                Debug.LogWarning($"[BuildTools] Could not parse version '{currentVersion}' as X.Y.Z. No increment performed.");
                return;
            }

            PlayerSettings.bundleVersion = newVersion;

            // Set platform-specific version codes
            int versionCode = ParseToVersionCode(newVersion);
            PlayerSettings.Android.bundleVersionCode = versionCode;
            PlayerSettings.iOS.buildNumber = newVersion;

            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildTools] bundleVersion: {currentVersion} -> {newVersion} (versionCode: {versionCode})");
        }

        /// <summary>
        /// Increments the patch number of a "X.Y.Z" semantic version string.
        /// Returns the original string if parsing fails.
        /// </summary>
/// <summary>
        /// Increments the patch number of a version string.
        /// Handles both "X.Y" (treated as X.Y.0) and "X.Y.Z" formats.
        /// Returns the original string if parsing fails.
        /// </summary>
        private static string IncrementPatch(string version)
        {
            // Try 3-part format first: X.Y.Z
            var match = Regex.Match(version, @"^(\d+)\.(\d+)\.(\d+)$");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                int patch = int.Parse(match.Groups[3].Value) + 1;
                return $"{major}.{minor}.{patch}";
            }

            // Try 2-part format: X.Y -> treat as X.Y.0 -> X.Y.1
            match = Regex.Match(version, @"^(\d+)\.(\d+)$");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                return $"{major}.{minor}.1";
            }

            return version;
        }

        /// <summary>
        /// Converts a "X.Y.Z" version to a single integer version code
        /// suitable for Android (max 2100000000).
        /// Uses formula: major * 10000 + minor * 100 + patch.
        /// </summary>
/// <summary>
        /// Converts a version string to a single integer version code
        /// suitable for Android (max 2100000000).
        /// Handles both "X.Y" and "X.Y.Z" formats.
        /// Uses formula: major * 10000 + minor * 100 + patch.
        /// </summary>
        private static int ParseToVersionCode(string version)
        {
            var match = Regex.Match(version, @"^(\d+)\.(\d+)\.(\d+)$");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                int patch = int.Parse(match.Groups[3].Value);
                return major * 10000 + minor * 100 + patch;
            }

            match = Regex.Match(version, @"^(\d+)\.(\d+)$");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                return major * 10000 + minor * 100;
            }

            return 1;
        }

        // ──────────────────────────────────────────────
        // 3. ExecuteBuild
        // ──────────────────────────────────────────────
        [MenuItem(MenuRoot + "Execute Build", priority = 30)]
        public static void ExecuteBuildMenu()
        {
            string outputPath = EditorUtility.SaveFolderPanel("Choose Build Output", "builds", "");
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.Log("[BuildTools] Build cancelled — no output path selected.");
                return;
            }

            ExecuteBuild(outputPath);
        }

        /// <summary>
        /// Runs BuildPipeline.BuildPlayer() and returns true on success.
        /// Logs error count on failure.
        /// </summary>
        /// <param name="outputPath">Directory to write the build to.</param>
        /// <returns>true if build succeeded.</returns>
        public static bool ExecuteBuild(string outputPath)
        {
            // Gather scenes from Build Settings, fall back to current scene
            var scenes = EditorBuildSettings.scenes;
            string[] scenePaths;

            if (scenes != null && scenes.Length > 0)
            {
                scenePaths = new string[scenes.Length];
                for (int i = 0; i < scenes.Length; i++)
                {
                    scenePaths[i] = scenes[i].path;
                }
            }
            else
            {
                // Fall back to the active scene + any loaded scenes
                var loadedScenes = new System.Collections.Generic.List<string>();
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (!string.IsNullOrEmpty(scene.path))
                        loadedScenes.Add(scene.path);
                }
                scenePaths = loadedScenes.ToArray();

                if (scenePaths.Length == 0)
                {
                    Debug.LogError("[BuildTools] No scenes in Build Settings and no loaded scenes. Cannot build.");
                    return false;
                }
            }

            // Determine target from current platform
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            // Construct output filename
            string buildName = PlayerSettings.productName.Replace(" ", "_");
            string extension = GetBuildExtension(target);
            string locationPath = System.IO.Path.Combine(outputPath, $"{buildName}{extension}");

            Debug.Log($"[BuildTools] Starting build: {locationPath} for {target} ({targetGroup})");
            Debug.Log($"[BuildTools] Scenes: {string.Join(", ", scenePaths)}");

            var options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = locationPath,
                target = target,
                targetGroup = targetGroup,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildTools] Build SUCCEEDED in {summary.totalTime} — {summary.totalSize} bytes");
                return true;
            }
            else
            {
                Debug.LogError($"[BuildTools] Build FAILED ({summary.result}) — {summary.totalErrors} error(s), {summary.totalWarnings} warning(s)");
                report.SummarizeErrors();
                return false;
            }
        }

        private static string GetBuildExtension(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk";
                case BuildTarget.iOS:
                    return ""; // iOS builds to a folder, no extension needed
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return ".exe";
                case BuildTarget.StandaloneOSX:
                    return ".app";
                case BuildTarget.StandaloneLinux64:
                    return ".x86_64";
                default:
                    return "";
            }
        }
    }
}
