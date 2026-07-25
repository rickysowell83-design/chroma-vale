using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Type of indicator marker rendered on a tile.
    /// </summary>
    public enum TileIndicator
    {
        None,
        SourceDot,
        TargetRing,
        ObstacleBlock,
        FlowGateArrow
    }

    /// <summary>
    /// MonoBehaviour that renders one puzzle-grid cell as a 3D tile with URP Lit
    /// emissive materials.  Base slab + optional 3D pipe mesh + optional indicator
    /// marker — all color-driven via a single cached MaterialPropertyBlock so no
    /// per-frame material instancing occurs.
    /// </summary>
    public class TileVisual : MonoBehaviour
    {
        private static Material _baseMaterial;

        private MeshRenderer _baseRenderer;
        private BoxCollider _boxCollider;
        private MaterialPropertyBlock _mpb;
        private GameObject _pipeRoot;
        private GameObject _indicatorRoot;
        private float _tileSize = 1f;
        private Color _color;
        private float _emissionIntensity = 5.0f;

        /// <summary>
        /// Logical tile color.  Setting this drives both the HDR emission
        /// (_EmissionColor = color * EmissionIntensity) and a dimmed base
        /// (_BaseColor = color * 0.25f) on the base slab and all pipe children.
        /// </summary>
        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                ApplyColor();
            }
        }

        /// <summary>
        /// HDR emission multiplier applied when setting <see cref="Color"/>.
        /// Default: 2.5f.
        /// </summary>
        public float EmissionIntensity
        {
            get => _emissionIntensity;
            set => _emissionIntensity = value;
        }

        /// <summary>
        /// The root Transform of this tile (same as this.transform).
        /// </summary>
        public Transform Root => transform;

        // ── Shared base material ──────────────────────────────────────────

        /// <summary>
        /// Get or create the shared base-slab material (near-black, metallic, emission-enabled).
        /// One instance shared across all tiles; per-tile variation via MaterialPropertyBlock.
        /// </summary>
        private static Material BaseMaterial
        {
            get
            {
                if (_baseMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _baseMaterial = new Material(shader)
                    {
                        color = new Color(0.05f, 0.06f, 0.09f)
                    };
                    _baseMaterial.SetFloat("_Metallic", 0.8f);
                    _baseMaterial.SetFloat("_Smoothness", 0.75f);
                    _baseMaterial.EnableKeyword("_EMISSION");
                    _baseMaterial.SetColor("_EmissionColor", Color.black);
                    _baseMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _baseMaterial;
            }
        }

        // ── Factory ───────────────────────────────────────────────────────

        /// <summary>
        /// Create a new TileVisual GameObject with a base slab, MeshRenderer,
        /// BoxCollider, and the TileVisual component attached.
        /// </summary>
        /// <param name="parent">Parent transform for the tile.</param>
        /// <param name="position">World/local position of the tile.</param>
        /// <param name="tileSize">World-space size of each tile edge.</param>
        /// <param name="name">Name for the new GameObject.</param>
        /// <returns>The TileVisual component on the newly created GameObject.</returns>
        public static TileVisual Create(Transform parent, Vector3 position, float tileSize, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var tile = go.AddComponent<TileVisual>();
            tile._tileSize = tileSize;

            // Build the base slab: a flat cube
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            Object.DestroyImmediate(slab.GetComponent<Collider>());

            slab.transform.SetParent(go.transform, false);
            slab.transform.localPosition = Vector3.zero;
            slab.transform.localScale = new Vector3(tileSize, tileSize, 0.1f);

            tile._baseRenderer = slab.GetComponent<MeshRenderer>();
            tile._baseRenderer.sharedMaterial = BaseMaterial;

            // BoxCollider on the root GameObject (click handling via PhysicsRaycaster)
            tile._boxCollider = go.AddComponent<BoxCollider>();
            tile._boxCollider.size = new Vector3(tileSize, tileSize, 0.3f);

            // Initialize the MaterialPropertyBlock cache
            tile._mpb = new MaterialPropertyBlock();

            return tile;
        }

        // ── Shape management ──────────────────────────────────────────────

        /// <summary>
        /// Instantiate or replace the 3D pipe mesh child for the given shape,
        /// rotated about the Z axis by <paramref name="rotationDeg"/> degrees.
        /// </summary>
        /// <param name="shape">The pipe shape to display.</param>
        /// <param name="rotationDeg">Z-axis rotation in degrees.</param>
        public void SetShape(PieceShape shape, int rotationDeg)
        {
            ClearShape();

            _pipeRoot = PipeMeshFactory3D.BuildPipe(shape, rotationDeg, transform);
            _pipeRoot.transform.localScale = Vector3.one * _tileSize;

            // Re-apply the current color to the new pipe meshes
            ApplyColor();
        }

        /// <summary>
        /// Destroy the pipe mesh child, if any.
        /// </summary>
        public void ClearShape()
        {
            if (_pipeRoot != null)
            {
                Object.DestroyImmediate(_pipeRoot);
                _pipeRoot = null;
            }
        }

        // ── Indicator management ──────────────────────────────────────────

        /// <summary>
        /// Set or replace a 3D indicator marker on this tile.
        /// </summary>
        /// <param name="kind">The indicator type to display.</param>
        /// <param name="color">Emissive color for the indicator.</param>
        public void SetIndicator(TileIndicator kind, Color color)
        {
            ClearIndicator();

            if (kind == TileIndicator.None) return;

            _indicatorRoot = new GameObject("Indicator_" + kind);
            _indicatorRoot.transform.SetParent(transform, false);
            _indicatorRoot.transform.localPosition = Vector3.zero;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var indicatorMat = new Material(shader);
            indicatorMat.EnableKeyword("_EMISSION");
            indicatorMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            switch (kind)
            {
                case TileIndicator.SourceDot:
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dot.name = "SourceDot";
                    Object.DestroyImmediate(dot.GetComponent<Collider>());
                    dot.transform.SetParent(_indicatorRoot.transform, false);
                    dot.transform.localPosition = new Vector3(0f, 0f, -0.2f);
                    dot.transform.localScale = Vector3.one * (_tileSize * 0.15f);

                    var rend = dot.GetComponent<MeshRenderer>();
                    rend.sharedMaterial = indicatorMat;
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_EmissionColor", color * 6f);
                    mpb.SetColor("_BaseColor", color * 0.5f);
                    rend.SetPropertyBlock(mpb);
                    break;
                }

                case TileIndicator.TargetRing:
                {
                    // Build a ring approximation: a flattened sphere disk
                    var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ring.name = "TargetRing";
                    Object.DestroyImmediate(ring.GetComponent<Collider>());
                    ring.transform.SetParent(_indicatorRoot.transform, false);
                    ring.transform.localPosition = new Vector3(0f, 0f, -0.2f);
                    ring.transform.localScale = new Vector3(_tileSize * 0.35f, _tileSize * 0.35f, _tileSize * 0.05f);

                    var rend = ring.GetComponent<MeshRenderer>();
                    rend.sharedMaterial = indicatorMat;
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_EmissionColor", color * 4f);
                    mpb.SetColor("_BaseColor", color * 0.3f);
                    rend.SetPropertyBlock(mpb);
                    break;
                }

                case TileIndicator.ObstacleBlock:
                {
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.name = "ObstacleBlock";
                    Object.DestroyImmediate(block.GetComponent<Collider>());
                    block.transform.SetParent(_indicatorRoot.transform, false);
                    block.transform.localPosition = new Vector3(0f, 0f, -0.15f);
                    block.transform.localScale = new Vector3(_tileSize * 0.4f, _tileSize * 0.4f, _tileSize * 0.4f);

                    var rend = block.GetComponent<MeshRenderer>();
                    rend.sharedMaterial = indicatorMat;
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_EmissionColor", color * 2f);
                    mpb.SetColor("_BaseColor", color * 0.35f);
                    rend.SetPropertyBlock(mpb);
                    break;
                }

                case TileIndicator.FlowGateArrow:
                {
                    // Arrow shaft
                    var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    shaft.name = "ArrowShaft";
                    Object.DestroyImmediate(shaft.GetComponent<Collider>());
                    shaft.transform.SetParent(_indicatorRoot.transform, false);
                    shaft.transform.localPosition = new Vector3(_tileSize * 0.12f, 0f, -0.2f);
                    // Cylinder defaults along Y — rotate to align with +X (shaft direction)
                    shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    shaft.transform.localScale = new Vector3(_tileSize * 0.04f, _tileSize * 0.25f, _tileSize * 0.04f);

                    var shaftRend = shaft.GetComponent<MeshRenderer>();
                    shaftRend.sharedMaterial = indicatorMat;
                    var shaftMpb = new MaterialPropertyBlock();
                    shaftMpb.SetColor("_EmissionColor", color * 2f);
                    shaftMpb.SetColor("_BaseColor", color * 0.4f);
                    shaftRend.SetPropertyBlock(shaftMpb);

                    // Diamond tip: cube rotated 45° reads as an arrowhead
                    var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    head.name = "ArrowHead";
                    Object.DestroyImmediate(head.GetComponent<Collider>());
                    head.transform.SetParent(_indicatorRoot.transform, false);
                    head.transform.localPosition = new Vector3(_tileSize * 0.27f, 0f, -0.2f);
                    // Diamond tip: cube rotated 45° reads as an arrowhead
                    head.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    head.transform.localScale = new Vector3(_tileSize * 0.09f, _tileSize * 0.09f, _tileSize * 0.04f);

                    var headRend = head.GetComponent<MeshRenderer>();
                    headRend.sharedMaterial = indicatorMat;
                    var headMpb = new MaterialPropertyBlock();
                    headMpb.SetColor("_EmissionColor", color * 2f);
                    headMpb.SetColor("_BaseColor", color * 0.4f);
                    headRend.SetPropertyBlock(headMpb);
                    break;
                }
            }
        }

        /// <summary>
        /// Rotate the indicator about the Z axis (used for FlowGateArrow direction).
        /// </summary>
        /// <param name="zDeg">Z rotation in degrees.</param>
        public void SetIndicatorRotation(float zDeg)
        {
            if (_indicatorRoot != null)
            {
                _indicatorRoot.transform.localRotation = Quaternion.Euler(0f, 0f, zDeg);
            }
        }

        // ── Internal helpers ──────────────────────────────────────────────

        /// <summary>
        /// Apply the current color to the base renderer and all pipe-child renderers
        /// via the cached MaterialPropertyBlock.
        /// </summary>
        private void ApplyColor()
        {
            if (_mpb == null) return;

            _mpb.SetColor("_EmissionColor", _color * _emissionIntensity);
            _mpb.SetColor("_BaseColor", _color * 0.25f);

            // Base slab
            if (_baseRenderer != null)
            {
                _baseRenderer.SetPropertyBlock(_mpb);
            }

            // All pipe mesh children
            if (_pipeRoot != null)
            {
                ApplyMpbToTree(_pipeRoot.transform, _mpb);
            }
        }

        /// <summary>
        /// Recursively apply a MaterialPropertyBlock to all MeshRenderers under the given transform.
        /// </summary>
        private static void ApplyMpbToTree(Transform root, MaterialPropertyBlock mpb)
        {
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.SetPropertyBlock(mpb);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                ApplyMpbToTree(root.GetChild(i), mpb);
            }
        }

        /// <summary>
        /// Destroy the current indicator child, if any.
        /// </summary>
        private void ClearIndicator()
        {
            if (_indicatorRoot != null)
            {
                Object.DestroyImmediate(_indicatorRoot);
                _indicatorRoot = null;
            }
        }
    }
}
