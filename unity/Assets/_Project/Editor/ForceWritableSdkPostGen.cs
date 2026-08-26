using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace ChromaVale.Editor
{
    /// <summary>
    /// Rewrites sdk.dir in the generated Gradle project to the writable SDK copy.
    /// Unity's batchmode Gradle generator keeps pointing local.properties at the
    /// read-only bundled SDK under Program Files, which fails on android-35 licenses.
    /// </summary>
    public class ForceWritableSdkPostGen : IPostGenerateGradleAndroidProject
    {
        public const string WritableSdk = "C:/Users/rsowe/AndroidSdk_Unity";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string line = "sdk.dir=" + WritableSdk.Replace("/", "\\\\") + "\n";
            // path = .../Gradle/unityLibrary — Gradle reads the ROOT project's local.properties
            string root = Directory.GetParent(path).FullName;
            foreach (string dir in new[] { path, root, Path.Combine(root, "launcher") })
            {
                string p = Path.Combine(dir, "local.properties");
                if (Directory.Exists(dir))
                {
                    File.WriteAllText(p, line);
                    Debug.Log($"[ForceWritableSdk] Overwrote {p}");
                }
            }
        }

        public int callbackOrder => 0;
    }
}
