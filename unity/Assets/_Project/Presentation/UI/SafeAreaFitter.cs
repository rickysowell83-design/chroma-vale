// SPDX-License-Identifier: MIT
// Chroma Vale — SafeAreaFitter: keeps a RectTransform inside the device safe
// area (notch/cutout aware). Attach to a stretch-anchored container directly
// under a Canvas; children then lay out inside the safe region. Refreshes on
// screen size / orientation changes.

using UnityEngine;

namespace ChromaVale.Presentation.UI
{
    /// <summary>
    /// Resizes the attached RectTransform to <see cref="Screen.safeArea"/>
    /// (normalized anchors, zero offsets), so UI children never sit under a
    /// notch, cutout, or rounded corner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private ScreenOrientation _lastOrientation = ScreenOrientation.Unknown;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            Rect safeArea = Screen.safeArea;
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            ScreenOrientation orientation = Screen.orientation;

            if (safeArea == _lastSafeArea && size == _lastScreenSize && orientation == _lastOrientation)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastScreenSize = size;
            _lastOrientation = orientation;

            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= width;
            anchorMin.y /= height;
            anchorMax.x /= width;
            anchorMax.y /= height;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
