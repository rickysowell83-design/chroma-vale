using ChromaVale.Core.GameLogic;
using System.Collections;
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
    /// marker + PCB via details — all color-driven via a single cached MaterialPropertyBlock
    /// so no per-frame material instancing occurs.
    /// </summary>
    public class TileVisual : MonoBehaviour
    {
        private static Material _baseMaterial;
        private static Material _viaMaterial;

        private MeshRenderer _baseRenderer;
        private BoxCollider _boxCollider;
        private MaterialPropertyBlock _mpb;
        private GameObject _pipeRoot;
        private GameObject _indicatorRoot;
        private GameObject _previewRoot;
        private float _tileSize = 1f;
        private Color _color;
        private Color _darkColor = new Color(0.04f, 0.05f, 0.06f);
        private float _emissionIntensity = 0.6f; // Subtle idle glow — pipes visible when placed

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
        /// Default: 0.0f (unpowered pipes are completely dead/dark).
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

        /// <summary>
        /// Set a custom material on the slab (per-tile PCB texture with UV offset).
        /// </summary>
        public void SetSlabMaterial(Material mat)
        {
            if (_baseRenderer != null)
                _baseRenderer.sharedMaterial = mat;
        }

        private MaterialPropertyBlock _slabMpb; // Per-tile UV offset for PCB texture

        /// <summary>
        /// Set a MaterialPropertyBlock for the slab (used for per-tile UV offsets).
        /// </summary>
        public void SetSlabPropertyBlock(MaterialPropertyBlock mpb)
        {
            _slabMpb = mpb;
            if (_baseRenderer != null)
                _baseRenderer.SetPropertyBlock(mpb);
        }

        // ── Shared base material ──────────────────────────────────────────

        /// <summary>
        /// Get or create the shared base-slab material (pitch-black PCB, metallic,
        /// emission-enabled).  One instance shared across all tiles; per-tile
        /// variation via MaterialPropertyBlock.
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
                        color = new Color(0.03f, 0.06f, 0.03f) // Dark green PCB
                    };
                    _baseMaterial.SetFloat("_Metallic", 0.15f);
                    _baseMaterial.SetFloat("_Smoothness", 0.5f);
                    _baseMaterial.EnableKeyword("_EMISSION");
                    _baseMaterial.SetColor("_EmissionColor", Color.black);
                    _baseMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _baseMaterial;
            }
        }

        /// <summary>
        /// Get or create the shared via / contact-pad material (copper/gold,
        /// highly metallic, no emission).  Used for all PCB detail elements.
        /// </summary>
        private static Material ViaMaterial
        {
            get
            {
                if (_viaMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _viaMaterial = new Material(shader)
                    {
                        color = new Color(0.8f, 0.55f, 0.2f) // Brighter gold/copper
                    };
                    _viaMaterial.SetFloat("_Metallic", 0.95f);
                    _viaMaterial.SetFloat("_Smoothness", 0.7f);
                    _viaMaterial.EnableKeyword("_EMISSION");
                    _viaMaterial.SetColor("_EmissionColor", new Color(0.4f, 0.25f, 0.08f) * 0.5f); // Subtle glow visible in dark
                    _viaMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _viaMaterial;
            }
        }

        // ── Factory ───────────────────────────────────────────────────────

        /// <summary>
        /// Create a new TileVisual GameObject with a base slab, MeshRenderer,
        /// BoxCollider, PCB via / contact-pad details, and the TileVisual
        /// component attached.
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

            // Build the base slab with per-tile PCB texture
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            Object.DestroyImmediate(slab.GetComponent<Collider>());

            slab.transform.SetParent(go.transform, false);
            slab.transform.localPosition = Vector3.zero;
            slab.transform.localScale = new Vector3(tileSize, tileSize, 0.1f);

            tile._baseRenderer = slab.GetComponent<MeshRenderer>();
            tile._baseRenderer.sharedMaterial = BaseMaterial;

            // ── No 3D vias — PCB texture handles pads ──

            // BoxCollider on the root GameObject (click handling via PhysicsRaycaster)
            tile._boxCollider = go.AddComponent<BoxCollider>();
            tile._boxCollider.size = new Vector3(tileSize, tileSize, 1.0f);

            // Initialize the MaterialPropertyBlock cache
            tile._mpb = new MaterialPropertyBlock();

            return tile;
        }

        // ── Flow emission control ────────────────────────────────────────

        /// <summary>
        /// Activate flow on this tile.  Sets the pipe color and smoothly ramps
        /// emission intensity from 0 to 5 over 0.15 seconds using a coroutine
        /// with SmoothStep interpolation.
        /// </summary>
        /// <param name="flowColor">The emissive flow colour to apply.</param>
        public void SetFlowActive(Color flowColor)
        {
            _color = flowColor;
            StopAllCoroutines();
            StartCoroutine(FlowLerpCoroutine());
        }

        /// <summary>
        /// Deactivate flow on this tile.  Resets emission intensity to 0 and
        /// restores the tile's idle dark colour.
        /// </summary>
        public void SetFlowIdle()
        {
            _emissionIntensity = 0.0f;
            _color = _darkColor;
            ApplyColor();
        }

        private IEnumerator FlowLerpCoroutine()
        {
            float duration = 0.25f; // Slightly longer for the surge effect
            float elapsed = 0f;
            float surgeDuration = 0.06f; // First 60ms: bright white electricity surge

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (elapsed <= surgeDuration)
                {
                    // Electricity surge: white-hot flash
                    float surgeT = elapsed / surgeDuration;
                    _emissionIntensity = Mathf.Lerp(12.0f, 8.0f, surgeT); // Start at 12, drop to 8
                    ApplyColorSurge(Color.white, _color); // White flash blending to target color
                }
                else
                {
                    // Settle to target color at intensity 5.0
                    float settleT = (elapsed - surgeDuration) / (duration - surgeDuration);
                    _emissionIntensity = Mathf.SmoothStep(7.0f, 5.0f, settleT);
                    ApplyColor();
                }
                yield return null;
            }

            _emissionIntensity = 5.0f;
            ApplyColor();
        }

        private void ApplyColorSurge(Color surgeColor, Color targetColor)
        {
            if (_mpb == null) return;
            float blend = Mathf.Clamp01(_emissionIntensity / 12.0f);
            Color blended = Color.Lerp(targetColor, surgeColor, blend);
            _mpb.SetColor("_EmissionColor", blended * _emissionIntensity);
            // Keep copper base — only emission surges
            _mpb.SetColor("_BaseColor", new Color(0.65f, 0.38f, 0.15f)); // Copper always

            if (_pipeRoot != null)
                ApplyMpbToTree(_pipeRoot.transform, _mpb);
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
            HidePlacementPreview();
            if (_pipeRoot != null)
            {
                Object.DestroyImmediate(_pipeRoot);
                _pipeRoot = null;
            }
        }

        // ── Placement Preview Ghost ────────────────────────────────────────

        /// <summary>
        /// Show a translucent placement preview ghost at this tile.
        /// Builds a pipe mesh using PipeMeshFactory3D.BuildPipe with a
        /// transparent URP/Lit material (alpha ~0.35, no emission).
        /// </summary>
        /// <param name="shape">The pipe shape to preview.</param>
        /// <param name="rotationDeg">Z-axis rotation in degrees.</param>
        public void ShowPlacementPreview(PieceShape shape, int rotationDeg)
        {
            HidePlacementPreview();

            _previewRoot = PipeMeshFactory3D.BuildPipe(shape, rotationDeg, transform);
            _previewRoot.transform.localScale = Vector3.one * _tileSize;

            // Create translucent preview material (URP/Lit, transparent, alpha ~0.35, no emission)
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var previewMat = new Material(shader);
            previewMat.color = new Color(0.2f, 0.9f, 0.95f, 0.35f); // NeonCyan with alpha ~0.35
            previewMat.SetFloat("_Metallic", 1.0f);
            previewMat.SetFloat("_Smoothness", 0.85f);

            // Configure transparent surface mode
            previewMat.SetFloat("_Surface", 1f);
            previewMat.SetFloat("_Blend", 0f);
            previewMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            previewMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            previewMat.SetFloat("_ZWrite", 0f);
            previewMat.renderQueue = 3000;
            previewMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // No emission on preview ghost
            previewMat.DisableKeyword("_EMISSION");
            previewMat.SetColor("_EmissionColor", Color.black);

            // Apply the translucent material to all pipe child meshes
            ApplyPreviewMatToTree(_previewRoot.transform, previewMat);
        }

        /// <summary>
        /// Hide and destroy the placement preview ghost on this tile.
        /// </summary>
        public void HidePlacementPreview()
        {
            if (_previewRoot != null)
            {
                Object.DestroyImmediate(_previewRoot);
                _previewRoot = null;
            }
        }

        /// <summary>
        /// Recursively replace sharedMaterial on all MeshRenderers under the given
        /// transform with the specified preview material.
        /// </summary>
        private static void ApplyPreviewMatToTree(Transform root, Material mat)
        {
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                ApplyPreviewMatToTree(root.GetChild(i), mat);
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
                    dot.transform.localScale = Vector3.one * (_tileSize * 0.35f); // Larger beacon

                    var rend = dot.GetComponent<MeshRenderer>();
                    rend.sharedMaterial = indicatorMat;
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_EmissionColor", color * 15f); // Intense electric glow
                    mpb.SetColor("_BaseColor", color * 0.7f);
                    rend.SetPropertyBlock(mpb);

                    // Add a larger outer glow halo
                    var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    halo.name = "SourceHalo";
                    Object.DestroyImmediate(halo.GetComponent<Collider>());
                    halo.transform.SetParent(_indicatorRoot.transform, false);
                    halo.transform.localPosition = new Vector3(0f, 0f, -0.25f);
                    halo.transform.localScale = Vector3.one * (_tileSize * 0.55f);
                    var haloRend = halo.GetComponent<MeshRenderer>();
                    haloRend.sharedMaterial = indicatorMat;
                    var haloMpb = new MaterialPropertyBlock();
                    haloMpb.SetColor("_EmissionColor", color * 4f);
                    haloMpb.SetColor("_BaseColor", color * 0.0f); // Emission-only, transparent base
                    haloRend.SetPropertyBlock(haloMpb);
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
                    mpb.SetColor("_EmissionColor", color * 10f); // Bright target beacon
                    mpb.SetColor("_BaseColor", color * 0.4f);
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

            // Pipe KEEPS copper base — only EMISSION changes for flow glow
            _mpb.SetColor("_EmissionColor", _color * _emissionIntensity);
            _mpb.SetColor("_BaseColor", new Color(0.65f, 0.38f, 0.15f)); // Copper always

            // Base slab: dark green PCB, no emission
            if (_baseRenderer != null)
            {
                var slabMpb = new MaterialPropertyBlock();
                slabMpb.SetColor("_EmissionColor", Color.black);
                _baseRenderer.SetPropertyBlock(slabMpb);
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
