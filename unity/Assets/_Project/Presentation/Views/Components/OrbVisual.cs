// SPDX-License-Identifier: MIT
// Chroma Vale — OrbVisual: SpriteRenderer-based orb renderer using artist's
// neon-glass PNG sprites. Loaded from Resources/Orbs/Art/<Color>/orb_<color>_t<n>.
// Falls back to a procedural circle sprite if art is missing.
// Public API is compatible with the previous version — MergeBoardView needs no
// structural changes, only the new Configure(OrbColor, OrbTier) overload call.

using System;
using System.Collections.Generic;
using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Renders an orb using the artist's tiered neon-glass sprite, driven by
    /// OrbColor + OrbTier. The PNG carries its own color/glow/facet detail,
    /// so the SpriteRenderer.color is NOT tinted (set to white). Falls back
    /// to a procedural circle if the artist sprite can't be loaded.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class OrbVisual : MonoBehaviour
    {
        // ── Sprite cache: (OrbColor, tier) → Sprite ──
        private static readonly Dictionary<(OrbColor, int), Sprite> _spriteCache
            = new();

        // ── Colourblind mode ──
        private static readonly Color32[] TierGlyphColors =
        {
            default,                                        // T0 (unused)
            new Color32(255, 255, 255, 255),                 // T1 white
            new Color32(100, 255, 100, 255),                 // T2 green
            new Color32(100, 200, 255, 255),                 // T3 blue
            new Color32(255, 200, 100, 255),                 // T4 orange
            new Color32(255, 100, 255, 255),                 // T5 purple
        };

        // ── Fallback procedural circle ──
        private static Sprite _circleSprite;
        private const int CircleTexSize = 128;

        // ── Per-instance state ──
        private SpriteRenderer _sr;
        private Color _color = Color.white;
        private OrbColor _orbColor = OrbColor.Cyan;
        private OrbTier _orbTier = OrbTier.T1;
        private float _tier01;
        private bool _colourblindMode;

        // ── Sprite cache accessors ──
        public static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite == null) CreateCircleSprite();
                return _circleSprite;
            }
        }

        /// <summary>The orb's current base color (read/write).</summary>
        public Color BaseColor
        {
            get => _color;
            set
            {
                _color = value;
                ApplyColor();
            }
        }

        /// <summary>Normalized tier value 0..1 (read-only).</summary>
        public float Tier01 => _tier01;

        /// <summary>The orb's OrbColor (set via Configure).</summary>
        public OrbColor OrbColorValue => _orbColor;

        /// <summary>The orb's OrbTier (set via Configure).</summary>
        public OrbTier OrbTierValue => _orbTier;

        // ── Lifecycle ──

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EnsureComponents();
        }

        // ── Public API (compatible with MergeBoardView) ──

        /// <summary>
        /// Configure the orb with a color and normalized tier (0..1).
        /// Uses the procedural circle fallback with color tinting.
        /// </summary>
        public void Configure(Color baseColor, float tier01)
        {
            _color = baseColor;
            _tier01 = tier01;
            // Fallback: procedural circle with tint
            _orbColor = OrbColor.Cyan; // default, won't be used for sprite lookup
            _orbTier = OrbTier.T1;
            ApplyColor();
        }

        /// <summary>
        /// Configure the orb with color and tier from enums.
        /// Loads the artist's PNG sprite if available; falls back to
        /// procedural circle with tint.
        /// </summary>
        public void Configure(Color baseColor, int tier, int maxTier = 5)
        {
            float tier01 = maxTier > 1 ? (float)(tier - 1) / (maxTier - 1) : 0f;
            Configure(baseColor, tier01);
        }

        /// <summary>
        /// Configure the orb with OrbColor + OrbTier enums.
        /// Loads the artist's neon-glass PNG sprite. Does NOT tint by color
        /// (the PNG carries its own color/glow). Falls back to procedural
        /// circle with tint if the sprite is missing.
        /// </summary>
        public void Configure(OrbColor color, OrbTier tier)
        {
            _orbColor = color;
            _orbTier = tier;
            _color = GetFallbackColor(color, tier);
            _tier01 = tier switch
            {
                OrbTier.T1 => 0f,
                OrbTier.T2 => 0.25f,
                OrbTier.T3 => 0.5f,
                OrbTier.T4 => 0.75f,
                OrbTier.T5 => 1f,
                _ => 0f,
            };

            // Try to load the artist sprite
            var sprite = LoadArtistSprite(color, (int)tier);
            if (sprite != null)
            {
                // PNG carries its own color — set color to white so the
                // sprite's native neon/glow shows through untouched.
                _sr.sprite = sprite;
                _sr.color = Color.white;
            }
            else
            {
                // Fallback: procedural circle with tint
                _sr.sprite = CircleSprite;
                _color = GetFallbackColor(color, tier);
                ApplyColor();
            }

            ApplyColourblindOverlay();
        }

        /// <summary>Set the orb's base color (fallback mode only).</summary>
        public void SetColor(Color c)
        {
            _color = c;
            ApplyColor();
        }

        /// <summary>Get the orb's current base color.</summary>
        public Color GetColor() => _color;

        /// <summary>Set alpha multiplier on the base color.</summary>
        public void SetAlpha(float a)
        {
            _color.a = a;
            ApplyColor();
        }

        /// <summary>
        /// Toggle colourblind mode: overlays a tier glyph (1–5) on the orb
        /// so tier is readable without relying on hue.
        /// </summary>
        public void SetColourblindMode(bool enabled)
        {
            if (_colourblindMode == enabled) return;
            _colourblindMode = enabled;
            ApplyColourblindOverlay();
        }

        // ── Internal ──

        private void EnsureComponents()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

            _sr.sortingOrder = 1;  // tiles are at -2, so orbs render on top
            if (_sr.sprite == null) _sr.sprite = CircleSprite;
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _sr.color = _color;
        }

        /// <summary>
        /// Load an artist orb sprite from Resources/Orbs/Art/<Color>/orb_<color>_t<n>.
        /// Cached in _spriteCache. Returns null if not found (caller uses fallback).
        /// </summary>
        private static Sprite LoadArtistSprite(OrbColor color, int tier)
        {
            var key = (color, tier);
            if (_spriteCache.TryGetValue(key, out var cached)) return cached;

            string colorName = color.ToString().ToLowerInvariant();
            string path = $"Orbs/Art/{color}/{colorName}_t{tier}";
            var sprite = Resources.Load<Sprite>(path);

            // Fallback: artist PNGs may be imported as raw textures (not Sprite assets),
            // or arrive as animation-strip slices. Wrap a Texture2D into a Sprite so the
            // neon-glass art still renders instead of dropping to the procedural circle.
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 256f);
                }
            }

            // Cache even if null (so we don't re-probe every frame)
            _spriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Colourblind overlay: a child SpriteRenderer with a tier-glyph PNG
        /// (Resources/UI/PipOverlays/tier_pips_T<n>) positioned at the orb's
        /// bottom-center. Created/destroyed as needed.
        /// </summary>
        private void ApplyColourblindOverlay()
        {
            // Remove existing overlay
            var existing = transform.Find("ColourblindPip");
            if (existing != null) Destroy(existing.gameObject);

            if (!_colourblindMode) return;

            int tierInt = (int)_orbTier;
            if (tierInt < 1 || tierInt > 5) return;

            string path = $"UI/PipOverlays/tier_pips_T{tierInt}";
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                // Try loading as texture and wrapping in a sprite
                var tex = Resources.Load<Texture2D>(path);
                if (tex == null || !tex.isReadable) return;
                sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 256f);
            }
            if (sprite == null) return;

            var pipGO = new GameObject("ColourblindPip");
            pipGO.transform.SetParent(transform, false);
            pipGO.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            pipGO.transform.localScale = Vector3.one;
            var pipSr = pipGO.AddComponent<SpriteRenderer>();
            pipSr.sprite = sprite;
            pipSr.sortingOrder = 5; // above orb
            pipSr.color = TierGlyphColors[tierInt];
        }

        /// <summary>Fallback color for the procedural circle when artist art is missing.</summary>
        private static Color GetFallbackColor(OrbColor color, OrbTier tier)
        {
            // Saturated version of the orb's hue, dimmed slightly per tier band.
            // The procedural fallback is only used if Resources/Orbs/Art is missing,
            // which should never happen in a built project.
            return color switch
            {
                OrbColor.Cyan =>    new Color(0f, 0.898f, 1f),
                OrbColor.Magenta => new Color(1f, 0f, 0.898f),
                OrbColor.Yellow =>  new Color(1f, 0.937f, 0f),
                OrbColor.Purple =>  new Color(0.541f, 0.169f, 0.886f),
                OrbColor.Green =>   new Color(0.098f, 0.8f, 0.098f),
                OrbColor.Orange =>  new Color(1f, 0.498f, 0f),
                OrbColor.Brown =>   new Color(0.4f, 0.2f, 0.05f),
                _ => Color.white,
            };
        }

        // ── Procedural circle sprite generation (fallback only) ──

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
                new Vector2(0.5f, 0.5f), size);
        }
    }
}