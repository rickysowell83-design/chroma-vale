using System.Collections;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    public class CameraShake : MonoBehaviour
    {
        private Vector3 _basePosition;
        private float _remainingDuration;
        private float _magnitude;
        private float _seed;
        private Coroutine _shakeRoutine;

        private void Awake()
        {
            _seed = Random.Range(0f, 1000f);
        }

        /// <summary>
        /// Trigger a Perlin-noise camera shake.
        /// Recommended: 0.05s/0.04mag for placement snap, 0.2s/0.25mag for pipe burst.
        /// </summary>
        public void Shake(float duration = 0.2f, float magnitude = 0.15f)
        {
            // Re-entrant: restart the timer without stacking offsets
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
            }

            // Cache base local position at shake start (not per frame)
            _basePosition = transform.localPosition;
            _remainingDuration = duration;
            _magnitude = magnitude;
            _shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;
            while (elapsed < _remainingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _remainingDuration;
                float mag = _magnitude * (1f - t); // linear decay to zero

                float offsetX = (Mathf.PerlinNoise(_seed, Time.unscaledTime * 25f) - 0.5f) * 2f * mag;
                float offsetY = (Mathf.PerlinNoise(_seed + 100f, Time.unscaledTime * 25f) - 0.5f) * 2f * mag;

                transform.localPosition = _basePosition + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }

            // Exact restore of base position
            transform.localPosition = _basePosition;
            _shakeRoutine = null;
        }
    }
}
