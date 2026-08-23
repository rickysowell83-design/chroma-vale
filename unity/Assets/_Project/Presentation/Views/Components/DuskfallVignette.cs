// SPDX-License-Identifier: MIT
// Chroma Vale — DuskfallVignette (Presentation)
//
// Spec: l8_duskfall_blackout_spec.md §3/§8 — the vignette IS the meter.
// Full-screen screen-space-overlay darkness driven by DuskfallSystem.CounterRatio:
//   ratio 0 → clear, 1 → fully dark; slow ominous pulse while counter <= 2.
// No new art assets, no new HUD elements, no new GameObjects beyond the one
// overlay this component builds (screen-edge darkness only — board stays visible).

using ChromaVale.Core.GameLogic;
using ChromaVale.Infrastructure.Audio;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Screen-edge darkness overlay for the Duskfall mechanic. Attach to any
    /// active GameObject; it lazily creates its overlay canvas child on first use.
    /// </summary>
    public sealed class DuskfallVignette : MonoBehaviour
    {
        private const float PulseSpeed = 2.2f;
        private const float PulseAmplitude = 0.12f;
        private const float StepTweenSeconds = 0.25f;
        private const float FailFlashSeconds = 0.5f;
        private const float ReleaseFlashAlpha = 0.85f;

        private CanvasGroup _canvasGroup;
        private Image _vignetteImage;
        private DuskfallSystem _duskfall;
        private int _lastCounter = int.MinValue;
        private bool _pulsing;
        private float _pulseTimer;
        private float _baseOpacity;

        /// <summary>Binds the vignette to a level's dusk system and snaps fully clear.</summary>
        public void Bind(DuskfallSystem duskfall)
        {
            _duskfall = duskfall;
            EnsureOverlay();
            _lastCounter = int.MinValue;
            _pulsing = false;
            SetOpacityImmediate(0f);

            if (_duskfall != null)
            {
                _duskfall.OnBrownCleared -= HandleRelease;
                _duskfall.OnBrownCleared += HandleRelease;
                _duskfall.OnDuskfall -= HandleFail;
                _duskfall.OnDuskfall += HandleFail;
            }
        }

        /// <summary>Unbinds and clears the overlay (level unload).</summary>
        public void Unbind()
        {
            if (_duskfall != null)
            {
                _duskfall.OnBrownCleared -= HandleRelease;
                _duskfall.OnDuskfall -= HandleFail;
                _duskfall = null;
            }
            if (_canvasGroup != null) SetOpacityImmediate(0f);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            if (_duskfall == null || !_duskfall.Enabled || !_duskfall.Armed) return;

            // Per-beat step tween: counter changed since last frame → re-tween.
            if (_duskfall.Counter != _lastCounter)
            {
                _lastCounter = _duskfall.Counter;
                float target = 1f - ((float)_lastCounter / Mathf.Max(1, _duskfall.DuskBeats));
                target = Mathf.Clamp01(target * 0.75f); // never fully black before the fail beat
                _baseOpacity = target;

                _canvasGroup.DOKill();
                _canvasGroup.DOFade(target, StepTweenSeconds).SetEase(Ease.InOutSine)
                    .SetLink(gameObject).OnComplete(() => _canvasGroup.alpha = target);

                // Ominous pulse at counter <= 2 (spec §3).
                _pulsing = _lastCounter <= 2 && _lastCounter > 0;
                if (!_pulsing) _pulseTimer = 0f;

                // Blind-accessible audio: rhythmic tick each beat; pulse tone
                // doubles as the "almost there" warning at counter <= 2.
                if (_lastCounter > 0 && AudioServiceInstaller.Instance != null)
                    AudioServiceInstaller.Instance.PlaySound(_lastCounter <= 2 ? "lock_flash" : "button_tap");
            }

            if (_pulsing && _canvasGroup != null)
            {
                _pulseTimer += Time.deltaTime * PulseSpeed;
                float pulse = (Mathf.Sin(_pulseTimer * Mathf.PI) * 0.5f + 0.5f) * PulseAmplitude;
                _canvasGroup.alpha = Mathf.Clamp01(_baseOpacity + pulse);
            }
        }

        private void HandleRelease()
        {
            // Last Brown cleared — snap bright + white release flash (spec §3).
            if (AudioServiceInstaller.Instance != null)
                AudioServiceInstaller.Instance.PlaySound("merge"); // release sting (placeholder)
            if (_canvasGroup == null) return;
            _canvasGroup.DOKill();
            _pulsing = false;
            Sequence seq = DOTween.Sequence().SetLink(gameObject);
            seq.Append(_canvasGroup.DOFade(ReleaseFlashAlpha, 0.08f));
            seq.Append(_canvasGroup.DOFade(0f, 0.35f).SetEase(Ease.OutQuad));
        }

        private void HandleFail()
        {
            // Duskfall — snap inward to full black over ~0.5s (soft fail, retry).
            if (AudioServiceInstaller.Instance != null)
                AudioServiceInstaller.Instance.PlaySound("spawn"); // low fail tone (placeholder)
            if (_canvasGroup == null) return;
            _canvasGroup.DOKill();
            _pulsing = false;
            _canvasGroup.DOFade(1f, FailFlashSeconds).SetEase(Ease.InQuad).SetLink(gameObject);
        }

        private void SetOpacityImmediate(float alpha)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
                _canvasGroup.alpha = alpha;
            }
        }

        private void EnsureOverlay()
        {
            if (_canvasGroup != null) return;

            var go = new GameObject("DuskfallVignette", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above board/HUD, below modal popups
            _canvasGroup = go.GetComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false; // pure feedback — never intercept input
            _canvasGroup.alpha = 0f;

            _vignetteImage = go.AddComponent<Image>();
            _vignetteImage.raycastTarget = false;
            _vignetteImage.sprite = BuildVignetteSprite();
            _vignetteImage.color = new Color(0.02f, 0.01f, 0.05f); // near-black violet tint
        }

        /// <summary>
        /// Runtime-generated radial vignette texture (transparent center → opaque
        /// edges). 256×256, no imported art asset required (spec hard constraint).
        /// </summary>
        private static Sprite BuildVignetteSprite()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    // Radial distance from center, normalized so corners ≈ 1.
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    // Clear until ~55% radius, ramp to opaque by ~100% (screen edge).
                    float edge = Mathf.InverseLerp(0.55f, 1.05f, d);
                    byte a = (byte)(edge * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true); // read-only upload once
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
