// SPDX-License-Identifier: MIT
// Chroma Vale — OrbVisual: MeshRenderer + MaterialPropertyBlock wrapper for
// the GlassOrb shader.  Replaces per-orb SpriteRenderer + PNG sprite with a
// single shared quad mesh and a MaterialPropertyBlock that drives _BaseColor
// without creating material instances.

using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Renders a glass orb using the ChromaVale/GlassOrb shader.
    /// Attach to a GameObject that also has a MeshFilter + MeshRenderer.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteAlways]
    public class OrbVisual : MonoBehaviour
    {
        // Shader property IDs — cached once
        private static readonly int BaseColorId  = Shader.PropertyToID("_BaseColor");

        // Shared quad mesh — one for all OrbVisual instances
        private static Mesh _quadMesh;
        private static Material _glassOrbMaterial;

        // Per-instance property block
        private MaterialPropertyBlock _mpb;
        private Color _color = Color.white;
        private float _tier01;
        private bool _dirty = true;

        /// <summary>
        /// The shared quad mesh (1×1 plane centered at origin).
        /// </summary>
        public static Mesh QuadMesh
        {
            get
            {
                if (_quadMesh == null) CreateQuadMesh();
                return _quadMesh;
            }
        }

        /// <summary>
        /// The shared GlassOrb material (loaded once from Resources/Materials).
        /// </summary>
        public static Material GlassOrbMaterial
        {
            get
            {
                if (_glassOrbMaterial == null)
                {
                    // Load from Resources — path: Assets/_Project/Resources/Materials/GlassOrb.mat
                    _glassOrbMaterial = Resources.Load<Material>("Materials/GlassOrb");
#if UNITY_EDITOR
                    if (_glassOrbMaterial == null)
                    {
                        // Fallback: load directly from the project
                        _glassOrbMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                            "Assets/_Project/Materials/GlassOrb.mat");
                    }
#endif
                }
                return _glassOrbMaterial;
            }
        }

        /// <summary>
        /// Orb base color (mapped from OrbColor enum → ChromaVale palette).
        /// </summary>
        public Color BaseColor
        {
            get => _color;
            set { _color = value; _dirty = true; }
        }

        /// <summary>
        /// Tier as a 0..1 float (T1=0 → T5=1).
        /// </summary>
        public float Tier01
        {
            get => _tier01;
            set { _tier01 = value; _dirty = true; }
        }

        private MaterialPropertyBlock MPB
        {
            get
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                return _mpb;
            }
        }

        private void Awake()
        {
            EnsureComponents();
            ApplyIfDirty();
        }

        private void OnEnable()
        {
            EnsureComponents();
            ApplyIfDirty();
        }

        private void LateUpdate()
        {
            // The pulse animation lives in the shader (_Time.y), so we only
            // re-apply when the MPB is dirty (color/tier changed).  But we also
            // re-apply every frame in Editor mode so changes are visible.
            ApplyIfDirty();
        }

        private void OnValidate()
        {
            _dirty = true;
        }

        /// <summary>
        /// Set both color and tier in one call — avoids double-dirty.
        /// </summary>
        public void Configure(Color baseColor, float tier01)
        {
            _color = baseColor;
            _tier01 = tier01;
            _dirty = true;
            ApplyIfDirty();
        }

        /// <summary>
        /// Convenience: set color and tier from OrbColor + OrbTier enums.
        /// </summary>
        public void Configure(Color baseColor, int tier, int maxTier = 5)
        {
            float tier01 = maxTier > 1 ? (float)(tier - 1) / (maxTier - 1) : 0f;
            Configure(baseColor, tier01);
        }

        /// <summary>Set the orb's base color (updates MaterialPropertyBlock).</summary>
        public void SetColor(Color c)
        {
            _color = c;
            _dirty = true;
            ApplyIfDirty();
        }

        /// <summary>Get the orb's current base color.</summary>
        public Color GetColor() => _color;

        /// <summary>Set alpha multiplier on the base color (0=transparent, 1=opaque).</summary>
        public void SetAlpha(float a)
        {
            _color.a = a;
            _dirty = true;
            ApplyIfDirty();
        }

        private void EnsureComponents()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = gameObject.AddComponent<MeshFilter>();
            }
            if (mf.sharedMesh == null)
            {
                mf.sharedMesh = QuadMesh;
            }

            var mr = GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = gameObject.AddComponent<MeshRenderer>();
            }
            if (mr.sharedMaterial == null)
            {
                mr.sharedMaterial = GlassOrbMaterial;
            }
        }

        private void ApplyIfDirty()
        {
            if (!_dirty) return;
            var mr = GetComponent<MeshRenderer>();
            if (mr == null) return;
            MPB.SetColor(BaseColorId, _color);
            mr.SetPropertyBlock(MPB);
            _dirty = false;
        }

        // ── Shared quad mesh creation ──

        private static void CreateQuadMesh()
        {
            // Standard Unity quad (1×1, centered at origin, UVs 0..1)
            _quadMesh = new Mesh
            {
                name = "OrbVisualQuad",
                vertices = new Vector3[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3( 0.5f, -0.5f, 0),
                    new Vector3( 0.5f,  0.5f, 0),
                    new Vector3(-0.5f,  0.5f, 0),
                },
                uv = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1),
                },
                triangles = new int[] { 0, 1, 2, 0, 2, 3 },
                normals = new Vector3[]
                {
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward,
                },
            };
            _quadMesh.RecalculateBounds();
            _quadMesh.hideFlags = HideFlags.HideAndDontSave;
        }
    }
}
