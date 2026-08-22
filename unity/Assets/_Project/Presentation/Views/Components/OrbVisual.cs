// SPDX-License-Identifier: MIT
// Chroma Vale — OrbVisual: SpriteRenderer-based orb renderer.
// Replaces the MeshRenderer + custom GlassOrb shader approach (which was
// invisible due to URP Transparent-queue sorting conflicts with
// SpriteRenderer tiles at the same z).  Uses a procedurally generated
// white circle sprite + SpriteRenderer.color for tinting.
// Public API is identical to the previous version — MergeBoardView needs
// no changes.

using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Renders a glass orb using a SpriteRenderer + a procedurally
    /// generated circle sprite.  The orb color is driven by
    /// SpriteRenderer.color, which sorts predictably via sortingOrder.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class OrbVisual : MonoBehaviour
    {
        // ── Shared circle sprite (generated once) ──
        private static Sprite _circleSprite;
        private const int CircleTexSize = 128;

        // ── Per-instance state ──
        private SpriteRenderer _sr;
        private Color _color = Color.white;
        private float _tier01;

        /// <summary>
        /// The shared circle sprite (64×64 white circle with soft edge).
        /// </summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite == null) CreateCircleSprite();
                return _circleSprite;
            }
        }

        /// <summary>The orb's base color (read/write).</summary>
        public Color BaseColor
        {
            get => _color;
            set
            {
                _color = value;
                ApplyColor();
            }
        }

        /// <summary>Normalized tier value 0..1 (read-only after Configure).</summary>
        public float Tier01 => _tier01;

        // ── Lifecycle ──

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EnsureComponents();
        }

        // ── Public API (identical to previous version) ──

        /// <summary>Set color and normalized tier (0..1).</summary>
        public void Configure(Color baseColor, float tier01)
        {
            _color = baseColor;
            _tier01 = tier01;
            ApplyColor();
        }

        /// <summary>
        /// Convenience: set color and tier from OrbColor + OrbTier enums.
        /// maxTier defaults to 5 (T1..T5 → 0..1).
        /// </summary>
        public void Configure(Color baseColor, int tier, int maxTier = 5)
        {
            float tier01 = maxTier > 1 ? (float)(tier - 1) / (maxTier - 1) : 0f;
            Configure(baseColor, tier01);
        }

        /// <summary>Set the orb's base color.</summary>
        public void SetColor(Color c)
        {
            _color = c;
            ApplyColor();
        }

        /// <summary>Get the orb's current base color.</summary>
        public Color GetColor() => _color;

        /// <summary>Set alpha multiplier on the base color (0=transparent, 1=opaque).</summary>
        public void SetAlpha(float a)
        {
            _color.a = a;
            ApplyColor();
        }

        // ── Internal ──

        private void EnsureComponents()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

            _sr.sprite = CircleSprite;
            _sr.sortingOrder = 1;  // tiles are at -2, so orbs render on top
            _sr.color = _color;
        }

        private void ApplyColor()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _sr.color = _color;
        }

        // ── Procedural circle sprite generation ──

        private static void CreateCircleSprite()
        {
            int size = CircleTexSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float radius = size * 0.45f;
            float aaWidth = 1.5f;

            // Specular highlight parameters (upper-left quadrant).
            float hlCx = cx - 0.3f * radius;
            float hlCy = cy + 0.3f * radius;
            float hlRadius = 0.25f * radius;

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Base alpha: soft anti-aliased circular mask (shape).
                    float alpha = Mathf.Clamp01((radius - dist) / aaWidth);

                    // Radial gradient in luminance (sphere shading):
                    // 255 at center down to ~200 (0.78x) at the rim.
                    float edgeFactor = Mathf.Clamp01(dist / radius);
                    float lum = Mathf.Lerp(255f, 200f, edgeFactor);

                    // Specular highlight: bright Gaussian spot in the
                    // upper-left quadrant, added to luminance (capped at 255).
                    float hdx = x + 0.5f - hlCx;
                    float hdy = y + 0.5f - hlCy;
                    float hdist2 = hdx * hdx + hdy * hdy;
                    float gauss = Mathf.Exp(-hdist2 / (2f * hlRadius * hlRadius));
                    lum = Mathf.Min(255f, lum + gauss * 255f);

                    byte rgb = (byte)Mathf.RoundToInt(lum);
                    pixels[y * size + x] = new Color32(rgb, rgb, rgb, (byte)(alpha * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), pixelsPerUnit: size);
        }
    }
}
