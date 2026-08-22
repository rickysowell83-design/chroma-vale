#if UNITY_EDITOR || CHROMA_DEBUG
using UnityEditor;
using UnityEngine;

namespace ChromaVale.Debugging
{
    /// <summary>
    /// Editor menu for running debug probes and auditing active probes.
    /// All code stripped from production builds (editor-only asmdef + #if guards).
    /// </summary>
    internal static class DebugProbeMenu
    {
        [MenuItem("Debug/Probes/Audit Active Probes")]
        static void AuditProbes()
        {
            Debug.Log(DebugProbeRegistry.GetAuditReport());
        }

        [MenuItem("Debug/Probes/Run Rendering Probe")]
        static void RunRenderingProbe()
        {
            var probes = Object.FindObjectsByType<RenderingDebugProbe>(FindObjectsSortMode.None);
            if (probes.Length == 0)
            {
                Debug.Log("[Probe:Rendering] No RenderingDebugProbe in scene. Creating temporary...");
                var go = new GameObject("__TempRenderingProbe");
                var probe = go.AddComponent<RenderingDebugProbe>();
                probe.RunProbe();
                Object.DestroyImmediate(go);
            }
            else
            {
                foreach (var probe in probes)
                    probe.RunProbe();
            }
        }

        [MenuItem("Debug/Probes/Run All Active Probes")]
        static void RunAllProbes()
        {
            if (DebugProbeRegistry.Count == 0)
            {
                Debug.Log("[Probe] No active probes registered.");
                return;
            }

            foreach (var probe in DebugProbeRegistry.Active)
            {
                Debug.Log($"Running probe: {probe.ProbeType} on '{probe.gameObject.name}'");
                probe.RunProbe();
            }
        }
    }
}
#endif
