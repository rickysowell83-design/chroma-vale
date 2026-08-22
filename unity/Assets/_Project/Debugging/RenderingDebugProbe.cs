#if UNITY_EDITOR || CHROMA_DEBUG
using System.Text;
using UnityEngine;

namespace ChromaVale.Debugging
{
    /// <summary>
    /// Logs all Renderer sorting info in the scene. This probe would have caught
    /// the URP sorting conflict (MeshRenderer orbs sorted behind SpriteRenderer tiles
    /// at the same z) that took many iterations to diagnose.
    ///
    /// Usage: Menu > Debug > Probes > Run Rendering Probe
    /// Or: attach as component to any GameObject and use context menu "Run Probe".
    /// </summary>
    [AddComponentMenu("Debug/Rendering Debug Probe")]
    public class RenderingDebugProbe : DebugProbe
    {
        public override string ProbeType => "Rendering";

        [ContextMenu("Run Probe")]
        public override void RunProbe()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var sb = new StringBuilder();
            sb.AppendLine($"=== Rendering Probe: {renderers.Length} renderers ===");

            foreach (var r in renderers)
            {
                var pos = r.transform.position;
                sb.AppendLine($"  [{r.gameObject.name}]");
                sb.AppendLine($"    type={r.GetType().Name}");
                sb.AppendLine($"    sortingLayer='{r.sortingLayerName}' sortingOrder={r.sortingOrder}");
                sb.AppendLine($"    z={pos.z:F3}");
                sb.AppendLine($"    visible={r.isVisible}");

                if (r is SpriteRenderer sr)
                {
                    sb.AppendLine($"    sprite={(sr.sprite != null ? sr.sprite.name : "null")}");
                    sb.AppendLine($"    color={sr.color}");
                }

                if (r.material != null)
                {
                    sb.AppendLine($"    shader='{r.material.shader?.name}'");
                    sb.AppendLine($"    renderQueue={r.material.renderQueue}");
                }
            }

            Log(sb.ToString());
        }
    }
}
#endif
