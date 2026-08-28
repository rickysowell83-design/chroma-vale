using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;
using UnityEngine;

namespace ChromaVale.Editor
{
    /// <summary>
    /// Configures Gradle to sign the AAB with the upload keystore.
    /// This runs during Gradle project generation, before the AAB is built.
    /// </summary>
    public class GradleSigningConfig : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string keystorePath = "C:/Users/rsowe/upload.keystore";
            if (!File.Exists(keystorePath))
            {
                Debug.LogWarning($"[GradleSigningConfig] Upload keystore not found at {keystorePath}");
                return;
            }

            // Signing must go in launcher/build.gradle, NOT unityLibrary
            string buildGradlePath = Path.Combine(path, "launcher", "build.gradle");
            if (!File.Exists(buildGradlePath))
            {
                Debug.LogWarning($"[GradleSigningConfig] Could not find launcher/build.gradle at {path}");
                return;
            }

            string content = File.ReadAllText(buildGradlePath);
            
            // Add signing config if not present (insert before android { block)
            string signingConfigBlock = @"
    signingConfigs {
        release {
            storeFile file('C:/Users/rsowe/upload.keystore')
            storePassword 'YOUR_SECURE_PASSWORD'
            keyAlias 'upload'
            keyPassword 'YOUR_SECURE_PASSWORD'
        }
    }
";

            if (!content.Contains("signingConfigs"))
            {
                int androidBlockStart = content.IndexOf("android {");
                if (androidBlockStart >= 0)
                {
                    content = content.Insert(androidBlockStart, signingConfigBlock);
                    Debug.Log($"[GradleSigningConfig] Added signingConfigs to {buildGradlePath}");
                }
            }

            // Force release build type to use release signing config
            // (Unity defaults release to signingConfigs.debug — overwrite that)
            if (content.Contains("signingConfig signingConfigs.debug"))
            {
                content = content.Replace(
                    "signingConfig signingConfigs.debug",
                    "signingConfig signingConfigs.release");
                Debug.Log($"[GradleSigningConfig] Overrode debug signing in release build type: {buildGradlePath}");
            }
            else if (!content.Contains("signingConfig signingConfigs.release"))
            {
                // Find "release {" inside buildTypes block
                int buildTypesIdx = content.IndexOf("buildTypes");
                int releaseIdx = content.IndexOf("release {", buildTypesIdx >= 0 ? buildTypesIdx : 0);
                if (releaseIdx >= 0)
                {
                    int openBrace = content.IndexOf("{", releaseIdx) + 1;
                    content = content.Insert(openBrace, "\n        signingConfig signingConfigs.release\n");
                    Debug.Log($"[GradleSigningConfig] Added signingConfig to release build type in {buildGradlePath}");
                }
            }

            File.WriteAllText(buildGradlePath, content);
        }
    }

    /// <summary>
    /// CLI-driven playtest build entry points.
    /// Usage (Unity batchmode):
    ///   Unity.exe -batchmode -quit -projectPath <proj> \
    ///     -executeMethod ChromaVale.Editor.BuildScript.BuildPlaytestAndroid \
    ///     -logFile <log>
    ///   Unity.exe -batchmode -quit -projectPath <proj> \
    ///     -executeMethod ChromaVale.Editor.BuildScript.BuildPlaytestIOS \
    ///     -logFile <log>
    ///
    /// Android produces an .apk (Development, IL2CPP, ARM64, debug symbols).
    /// iOS on Windows produces an Xcode project (archiving to IPA requires macOS).
    /// </summary>
    public static class BuildScript
    {
        public const string PlaytestBundleId = "com.manahunter4.chromavale.playtest";

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/App.unity"
        };

        // Outputs land in <repo-root>/builds/<platform>/
        private static string OutputRoot
        {
            get
            {
                // Application.dataPath = <repo>/unity/Assets → ../../ = <repo>
                string repoRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", ".."));
                return Path.Combine(repoRoot, "builds");
            }
        }

        [MenuItem("ChromaVale/Build/Playtest Android APK")]
        public static void BuildPlaytestAndroidMenu()
        {
            BuildPlaytest(BuildTarget.Android);
        }

        [MenuItem("ChromaVale/Build/Playtest iOS Xcode Project")]
        public static void BuildPlaytestIOSMenu()
        {
            BuildPlaytest(BuildTarget.iOS);
        }

        /// <summary>CLI entry: Android APK playtest build.</summary>
        public static void BuildPlaytestAndroid()
        {
            BuildPlaytest(BuildTarget.Android);
        }

        /// <summary>CLI entry: iOS Xcode project playtest build.</summary>
        public static void BuildPlaytestIOS()
        {
            BuildPlaytest(BuildTarget.iOS);
        }

        /// <summary>
        /// Shared playtest build: bundle id, IL2CPP, ARM64, Development + debug symbols.
        /// </summary>
        public static void BuildPlaytest(BuildTarget target)
        {
            BuildTargetGroup group = target == BuildTarget.Android
                ? BuildTargetGroup.Android
                : BuildTargetGroup.iOS;

            // ── PlayerSettings ──────────────────────────────────────────────
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(group), PlaytestBundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(group), ScriptingImplementation.IL2CPP);
            PlayerSettings.companyName = "Manahunter4";
            PlayerSettings.productName = "Chroma Vale";

            if (target == BuildTarget.Android)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.bundleVersionCode = 10005;
                // Point Unity at writable SDK copy (Unity-bundled SDK is read-only in Program Files)
                UnityEditor.EditorPrefs.SetString("AndroidSdkRoot", "C:/Users/rsowe/AndroidSdk_Unity");
                // Development builds emit debug symbols automatically (IL2CPP).
                EditorUserBuildSettings.buildAppBundle = true; // AAB (Google Play preferred)
                // Target API 35 (required by Play Console for new submissions)
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

                // ── Signing: use upload keystore for Play Console ───────────────
                string keystorePath = "C:/Users/rsowe/upload.keystore";
                if (File.Exists(keystorePath))
                {
                    PlayerSettings.Android.keystoreName = keystorePath;
                    PlayerSettings.Android.keystorePass = "YOUR_SECURE_PASSWORD";
                    PlayerSettings.Android.keyaliasName = "upload";
                    PlayerSettings.Android.keyaliasPass = "YOUR_SECURE_PASSWORD";
                    Debug.Log($"[BuildScript] Signing with upload keystore: {keystorePath}");
                }
                else
                {
                    Debug.LogWarning($"[BuildScript] Upload keystore not found at {keystorePath} — building unsigned (debug).");
                }
            }
            else
            {
                PlayerSettings.iOS.targetOSVersionString = "15.0";
                // Signing is configured later in Xcode on macOS; do not set team here.
            }

            // ── Output path ─────────────────────────────────────────────────
            string outDir = Path.Combine(OutputRoot,
                target == BuildTarget.Android ? "android" : "ios");
            Directory.CreateDirectory(outDir);

            string locationPath = target == BuildTarget.Android
                ? Path.Combine(outDir, "ChromaVale_playtest.aab")
                : Path.Combine(outDir, "ChromaVale_iOS"); // Xcode project folder

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = locationPath,
                target = target,
                targetGroup = group,
                options = BuildOptions.None  // Release build (no Development/AllowDebugging)
            };

            Debug.Log($"[BuildScript] Playtest build start: {target} → {locationPath}");
            Debug.Log($"[BuildScript] Scenes: {string.Join(", ", Scenes)}");
            Debug.Log($"[BuildScript] Bundle ID: {PlaytestBundleId}, IL2CPP, Development+symbols");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] PLAYTEST BUILD SUCCEEDED ({target}) in {summary.totalTime} — {summary.totalSize} bytes → {locationPath}");
            }
            else
            {
                Debug.LogError($"[BuildScript] PLAYTEST BUILD FAILED ({target}) — {summary.totalErrors} errors, {summary.totalWarnings} warnings");
                report.SummarizeErrors();
                EditorApplication.Exit(1);
            }
        }
    }
}
