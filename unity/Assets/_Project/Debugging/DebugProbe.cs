#if UNITY_EDITOR || CHROMA_DEBUG
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChromaVale.Debugging
{
    /// <summary>
    /// Base class for all debug probes. Auto-registers with DebugProbeRegistry on enable.
    /// Override RunProbe() to emit diagnostic data. All probe code is stripped from
    /// production builds — the assembly is editor-only (includePlatforms: Editor).
    /// </summary>
    [ExecuteAlways]
    public abstract class DebugProbe : MonoBehaviour
    {
        public abstract string ProbeType { get; }
        public abstract void RunProbe();

        protected virtual void OnEnable()
        {
            DebugProbeRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            DebugProbeRegistry.Unregister(this);
        }

        protected void Log(string message)
        {
            Debug.Log($"[Probe:{ProbeType}] {message}");
        }
    }

    /// <summary>
    /// Central registry for all active debug probes. Use GetAuditReport() to list
    /// all active probes. This is the tracking mechanism — call it before deploy
    /// to verify no probes are active (though the editor-only asmdef already strips them).
    /// </summary>
    public static class DebugProbeRegistry
    {
        private static readonly List<DebugProbe> _probes = new();

        public static void Register(DebugProbe probe)
        {
            if (!_probes.Contains(probe))
                _probes.Add(probe);
        }

        public static void Unregister(DebugProbe probe)
        {
            _probes.Remove(probe);
        }

        public static IReadOnlyList<DebugProbe> Active => _probes;
        public static int Count => _probes.Count;

        public static string GetAuditReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Debug Probe Audit: {_probes.Count} active ===");
            for (int i = 0; i < _probes.Count; i++)
            {
                var p = _probes[i];
                sb.AppendLine($"  [{i}] {p.ProbeType} on '{p.gameObject.name}' (active: {p.gameObject.activeInHierarchy})");
            }
            return sb.ToString();
        }
    }
}
#endif
