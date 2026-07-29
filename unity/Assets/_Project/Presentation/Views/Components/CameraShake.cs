using DG.Tweening;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    public class CameraShake : MonoBehaviour
    {
        private Vector3 _basePosition;
        private Tweener _shakeTween;

        /// <summary>
        /// Trigger a camera shake using Perlin noise via DOTween.
        /// Recommended: 0.05s/0.04mag for placement snap, 0.2s/0.25mag for trace burst.
        /// </summary>
        public void Shake(float duration = 0.2f, float magnitude = 0.15f)
        {
            _shakeTween?.Kill();
            _basePosition = transform.localPosition;

            // Use DOShakePosition for a more natural shake effect
            _shakeTween = transform.DOShakePosition(duration, magnitude, 20, 90f, false, true)
                .OnComplete(() => transform.localPosition = _basePosition);
        }

        private void OnDestroy()
        {
            _shakeTween?.Kill();
        }
    }
}
