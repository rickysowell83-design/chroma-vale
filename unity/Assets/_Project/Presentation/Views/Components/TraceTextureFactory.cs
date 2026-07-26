using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Generates procedural neon pipe textures — rounded tube shapes with glow,
    /// proper connection openings, and direction indicators.
    /// Replaces the old "child GameObject bars" approach.
    /// </summary>
    [System.Obsolete("Replaced by PipeMeshFactory3D — 3D overhaul")]
    public static class PipeTextureFactory
    {
        private const int TexSize = 64;
        private const float PipeWidth = 0.28f;  // relative to texture
        private const float GlowWidth = 0.12f;   // glow spread beyond pipe edge
        private const float CornerRadius = 0.15f;

        /// <summary>
        /// Create a sprite for the given pipe shape + rotation.
        /// Returns a Sprite that can be assigned to a SpriteRenderer.
        /// </summary>
        public static Sprite CreatePipeSprite(PieceShape shape, int rotationDeg, Color pipeColor, Color glowColor)
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[TexSize * TexSize];

            // Fill transparent
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            // Get the connection vectors based on shape
            var connections = GetConnections(shape, rotationDeg);

            // Draw pipe body for each connection arm
            foreach (var dir in connections)
            {
                DrawPipeArm(pixels, dir, pipeColor, glowColor);
            }

            // Draw center hub (all shapes except Straight get a center)
            if (shape != PieceShape.Straight || connections.Length > 0)
            {
                DrawCenterHub(pixels, pipeColor, glowColor);
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
        }

        private static Vector2[] GetConnections(PieceShape shape, int rotationDeg)
        {
            // Base connections (pointing OUT from center)
            Vector2[] baseConns = shape switch
            {
                PieceShape.Straight => new[] { new Vector2(1, 0), new Vector2(-1, 0) },
                PieceShape.Elbow => new[] { new Vector2(1, 0), new Vector2(0, 1) },
                PieceShape.TJunction => new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1) },
                PieceShape.Cross => new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) },
                PieceShape.Valve => new[] { new Vector2(1, 0), new Vector2(-1, 0) },
                PieceShape.Amplifier => new[] { new Vector2(1, 0), new Vector2(-1, 0) },
                PieceShape.Mixer => new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) },
                PieceShape.Blocker => new Vector2[0],
                _ => new[] { new Vector2(1, 0), new Vector2(-1, 0) }
            };

            // Apply rotation
            float rad = rotationDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            for (int i = 0; i < baseConns.Length; i++)
            {
                float x = baseConns[i].x * cos - baseConns[i].y * sin;
                float y = baseConns[i].x * sin + baseConns[i].y * cos;
                baseConns[i] = new Vector2(x, y);
            }
            return baseConns;
        }

        private static void DrawPipeArm(Color[] pixels, Vector2 dir, Color pipeColor, Color glowColor)
        {
            int cx = TexSize / 2, cy = TexSize / 2;

            for (int py = 0; py < TexSize; py++)
            {
                for (int px = 0; px < TexSize; px++)
                {
                    float fx = (px - cx) / (float)TexSize;
                    float fy = (py - cy) / (float)TexSize;

                    // Distance from the arm centerline
                    float proj = fx * dir.x + fy * dir.y; // projection along arm
                    float perp = Mathf.Abs(fx * dir.y - fy * dir.x); // perpendicular distance

                    // Only draw in the arm direction (positive projection)
                    if (proj < -CornerRadius || proj > 0.55f) continue;

                    float halfW = PipeWidth / 2f;
                    float glowHalfW = halfW + GlowWidth;

                    if (perp < halfW)
                    {
                        // Pipe body
                        int idx = py * TexSize + px;
                        float alpha = 1f;
                        // Soft edge
                        if (perp > halfW - 0.03f)
                            alpha = 1f - (perp - (halfW - 0.03f)) / 0.03f;
                        pixels[idx] = BlendAlpha(pixels[idx], new Color(pipeColor.r, pipeColor.g, pipeColor.b, alpha));
                    }
                    else if (perp < glowHalfW)
                    {
                        // Glow
                        int idx = py * TexSize + px;
                        float glowAlpha = (1f - (perp - halfW) / GlowWidth) * 0.5f;
                        pixels[idx] = BlendAlpha(pixels[idx], new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha));
                    }
                }
            }
        }

        private static void DrawCenterHub(Color[] pixels, Color pipeColor, Color glowColor)
        {
            int cx = TexSize / 2, cy = TexSize / 2;
            float radius = PipeWidth / 2f + 0.02f;
            float glowRadius = radius + GlowWidth;

            for (int py = 0; py < TexSize; py++)
            {
                for (int px = 0; px < TexSize; px++)
                {
                    float fx = (px - cx) / (float)TexSize;
                    float fy = (py - cy) / (float)TexSize;
                    float dist = Mathf.Sqrt(fx * fx + fy * fy);

                    int idx = py * TexSize + px;

                    if (dist < radius)
                    {
                        float alpha = 1f;
                        if (dist > radius - 0.03f)
                            alpha = 1f - (dist - (radius - 0.03f)) / 0.03f;
                        pixels[idx] = BlendAlpha(pixels[idx], new Color(pipeColor.r, pipeColor.g, pipeColor.b, alpha));
                    }
                    else if (dist < glowRadius)
                    {
                        float glowAlpha = (1f - (dist - radius) / GlowWidth) * 0.4f;
                        pixels[idx] = BlendAlpha(pixels[idx], new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha));
                    }
                }
            }
        }

        private static Color BlendAlpha(Color existing, Color incoming)
        {
            float a = incoming.a + existing.a * (1f - incoming.a);
            if (a < 0.001f) return Color.clear;
            return new Color(
                (incoming.r * incoming.a + existing.r * existing.a * (1f - incoming.a)) / a,
                (incoming.g * incoming.a + existing.g * existing.a * (1f - incoming.a)) / a,
                (incoming.b * incoming.a + existing.b * existing.a * (1f - incoming.a)) / a,
                a
            );
        }
    }
}
