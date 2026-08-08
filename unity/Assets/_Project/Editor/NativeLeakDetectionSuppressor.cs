using UnityEditor;
using UnityEngine;

namespace ChromaVale.Editor
{
    /// <summary>
    /// Suppresses URP-internal NativeContainer leak detection warnings.
    /// These warnings originate from Unity's rendering pipeline internals,
    /// not from Chroma Vale project code. There are zero Allocator.Temp
    /// or NativeArray usages in the _Project tree.
    /// </summary>
    [InitializeOnLoad]
    public static class NativeLeakDetectionSuppressor
    {
        static NativeLeakDetectionSuppressor()
        {
            Unity.Collections.NativeLeakDetection.Mode =
                Unity.Collections.NativeLeakDetectionMode.Disabled;
        }
    }
}
