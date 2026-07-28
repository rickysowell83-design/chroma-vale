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
        SignalGateArrow
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
        private GameObject _traceRoot;
        private GameObject _indicatorRoot;
        private GameObject _previewRoot;
        private Color _indicatorColor; // Stored for source pulse coroutine
        private float _tileSize = 1f;
        private Color _color;
        private Color _darkColor = new Color(0.04f, 0.05f, 0.06f);
        private float _emissionIntensity = 0.4f;

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
                        color = ChromaPalette.PCB_Substrate // v3 dark green #0A160A
                    };
                    _baseMaterial.SetFloat("_Metallic", 0.15f);
                    _baseMaterial.SetFloat("_Smoothness", 0.35f);
                    _baseMaterial.EnableKeyword("_EMISSION");
                    _baseMaterial.SetColor("_EmissionColor", new Color(0.08f, 0.15f, 0.08f));
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
                        color = ChromaPalette.ENIG_Gold // v3 ENIG gold pad (#D4A843)
                    };
                    _viaMaterial.SetFloat("_Metallic", 0.95f);
                    _viaMaterial.SetFloat("_Smoothness", 0.6f);
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
            // v3: CopperActive (#B87333) base stays — only emission surges
            _mpb.SetColor("_BaseColor", ChromaPalette.CopperActive);

            if (_traceRoot != null)
                ApplyMpbToTree(_traceRoot.transform, _mpb);
        }

        // ── Shape management ──────────────────────────────────────────────

        /// <summary>
        /// Instantiate or replace the 3D pipe mesh child for the given shape,
        /// rotated about the Z axis by <paramref name="rotationDeg"/> degrees.
        /// </summary>
        /// <param name="shape">The trace shape to display.</param>
        /// <param name="rotationDeg">Z-axis rotation in degrees.</param>
        public void SetShape(SegmentShape shape, int rotationDeg)
        {
            ClearShape();

            _traceRoot = TraceMeshFactory3D.BuildPipe(shape, rotationDeg, transform);
            _traceRoot.transform.localScale = Vector3.one * _tileSize;

            // Re-apply the current color to the new pipe meshes
            ApplyColor();
        }

        /// <summary>
        /// Destroy the pipe mesh child, if any.
        /// </summary>
        public void ClearShape()
        {
            HidePlacementPreview();
            if (_traceRoot != null)
            {
                Object.DestroyImmediate(_traceRoot);
                _traceRoot = null;
            }
        }

        // ── Placement Preview Ghost ────────────────────────────────────────

        /// <summary>
        /// Show a translucent placement preview ghost at this tile.
        /// Builds a pipe mesh using TraceMeshFactory3D.BuildPipe with a
        /// transparent URP/Lit material (alpha ~0.35, no emission).
        /// </summary>
        /// <param name="shape">The trace shape to preview.</param>
        /// <param name="rotationDeg">Z-axis rotation in degrees.</param>
        public void ShowPlacementPreview(SegmentShape shape, int rotationDeg)
        {
            HidePlacementPreview();

            _previewRoot = TraceMeshFactory3D.BuildPipe(shape, rotationDeg, transform);
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
            _indicatorColor = color;

            if (kind == TileIndicator.None) return;

            _indicatorRoot = new GameObject("Indicator_" + kind);
            _indicatorRoot.transform.SetParent(transform, false);
            _indicatorRoot.transform.localPosition = Vector3.zero;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var indicatorMat = new Material(shader);
            indicatorMat.color = color * 0.8f;
            indicatorMat.SetFloat("_Metallic", 0f);
            indicatorMat.SetFloat("_Smoothness", 0.5f);
            indicatorMat.EnableKeyword("_EMISSION");
            indicatorMat.SetColor("_EmissionColor", color * 2f);
            indicatorMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            switch (kind)
            {
                case TileIndicator.SourceDot:
                {
                    // ── v3: ENIG gold octagonal pad with cyan via center ──

                    // Gold outer pad ring
                    var goldPad = TraceMeshFactory3D.CreateViaPad(Vector3.zero, _indicatorRoot.transform);
                    goldPad.name = "SourceENIGPad";
                    goldPad.transform.SetParent(_indicatorRoot.transform, false);

                    // Cyan via center (pulsing)
                    var viaCenter = TraceMeshFactory3D.CreateViaCenter(Vector3.zero, _indicatorRoot.transform,
                        ChromaPalette.ViaCyan);
                    viaCenter.name = "SourceViaCenter";

                    // Bloom halo disc (thin, wide, cyan)
                    var halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    halo.name = "SourceHalo";
                    Object.DestroyImmediate(halo.GetComponent<Collider>());
                    halo.transform.SetParent(_indicatorRoot.transform, false);
                    halo.transform.localPosition = new Vector3(0f, 0f, -0.06f); // Behind pad ring
                    halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float haloRadius = _tileSize * 0.40f;
                    halo.transform.localScale = new Vector3(haloRadius * 2f, 0.02f, haloRadius * 2f);
                    var haloRend = halo.GetComponent<MeshRenderer>();
                    if (haloRend != null)
                    {
                        var haloMat = new Material(indicatorMat.shader) { color = Color.clear };
                        haloMat.SetFloat("_Metallic", 0f);
                        haloMat.SetFloat("_Smoothness", 0f);
                        haloMat.EnableKeyword("_EMISSION");
                        haloMat.SetColor("_EmissionColor", ChromaPalette.ViaCyan * 3f);
                        haloMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        haloRend.sharedMaterial = haloMat;
                    }

                    // Bright center pin-point (small glowing sphere at via center)
                    var centerPin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    centerPin.name = "SourcePin";
                    Object.DestroyImmediate(centerPin.GetComponent<Collider>());
                    centerPin.transform.SetParent(_indicatorRoot.transform, false);
                    centerPin.transform.localPosition = new Vector3(0f, 0f, -0.11f);
                    centerPin.transform.localScale = Vector3.one * (_tileSize * 0.03f);
                    var pinRend = centerPin.GetComponent<MeshRenderer>();
                    if (pinRend != null)
                    {
                        var pinMat = new Material(indicatorMat.shader) { color = ChromaPalette.ViaCyan };
                        pinMat.SetFloat("_Metallic", 0f);
                        pinMat.SetFloat("_Smoothness", 0.5f);
                        pinMat.EnableKeyword("_EMISSION");
                        pinMat.SetColor("_EmissionColor", ChromaPalette.ViaCyan * 15f);
                        pinMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        pinRend.sharedMaterial = pinMat;
                    }
                    break;
                }

                case TileIndicator.TargetRing:
                {
                    // ── v3: ENIG gold octagonal pad with dark void via center ──

                    // Gold outer pad ring (same geometry as source)
                    var goldPad = TraceMeshFactory3D.CreateViaPad(Vector3.zero, _indicatorRoot.transform);
                    goldPad.name = "TargetENIGPad";
                    goldPad.transform.SetParent(_indicatorRoot.transform, false);

                    // Dark void via center (no emission until restoration)
                    var viaCenter = TraceMeshFactory3D.CreateViaCenter(Vector3.zero, _indicatorRoot.transform,
                        ChromaPalette.DestVoid);
                    viaCenter.name = "TargetVoid";

                    // Subtle dormant halo (barely visible, dark)
                    var halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    halo.name = "TargetDormantHalo";
                    Object.DestroyImmediate(halo.GetComponent<Collider>());
                    halo.transform.SetParent(_indicatorRoot.transform, false);
                    halo.transform.localPosition = new Vector3(0f, 0f, -0.06f); // Behind pad ring
                    halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float haloRadius = _tileSize * 0.38f;
                    halo.transform.localScale = new Vector3(haloRadius * 2f, 0.02f, haloRadius * 2f);
                    var haloRend = halo.GetComponent<MeshRenderer>();
                    if (haloRend != null)
                    {
                        var haloMat = new Material(indicatorMat.shader) { color = Color.clear };
                        haloMat.SetFloat("_Metallic", 0f);
                        haloMat.SetFloat("_Smoothness", 0f);
                        haloMat.EnableKeyword("_EMISSION");
                        haloMat.SetColor("_EmissionColor", ChromaPalette.DestVoid * 0.3f); // Barely visible
                        haloMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        haloRend.sharedMaterial = haloMat;
                    }
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

                case TileIndicator.SignalGateArrow:
                {
                    var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    shaft.name = "ArrowShaft";
                    Object.DestroyImmediate(shaft.GetComponent<Collider>());
                    shaft.transform.SetParent(_indicatorRoot.transform, false);
                    shaft.transform.localPosition = new Vector3(_tileSize * 0.12f, 0f, -0.2f);
                    shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    shaft.transform.localScale = new Vector3(_tileSize * 0.04f, _tileSize * 0.25f, _tileSize * 0.04f);
                    var shaftRend = shaft.GetComponent<MeshRenderer>();
                    shaftRend.sharedMaterial = indicatorMat;
                    var shaftMpb = new MaterialPropertyBlock();
                    shaftMpb.SetColor("_EmissionColor", color * 2f);
                    shaftMpb.SetColor("_BaseColor", color * 0.4f);
                    shaftRend.SetPropertyBlock(shaftMpb);

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
        /// Rotate the indicator about the Z axis (used for SignalGateArrow direction).
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

            // v3: Flat copper foil — CopperActive base with cyan emission overlay when lit
            // When idle (no flow active), emit oxidized copper glow
            Color emissionColor = _color * _emissionIntensity;
            if (emissionColor.r < 0.01f && emissionColor.g < 0.01f && emissionColor.b < 0.01f)
            {
                // Idle: oxidized copper subtle glow
                emissionColor = ChromaPalette.CopperOxidized * Mathf.Max(_emissionIntensity, 0.15f);
                _mpb.SetColor("_BaseColor", ChromaPalette.CopperOxidized);
            }
            else
            {
                // Active: CopperActive base + cyan emission overlay
                _mpb.SetColor("_BaseColor", ChromaPalette.CopperActive);
                // Blend cyan emission on top of copper
                emissionColor = Color.Lerp(ChromaPalette.CopperActive * _emissionIntensity,
                    ChromaPalette.TraceCyanEmission * _emissionIntensity, 0.4f);
            }
            _mpb.SetColor("_EmissionColor", emissionColor);

            if (_baseRenderer != null)
            {
                var slabMpb = new MaterialPropertyBlock();
                slabMpb.SetColor("_EmissionColor", Color.black);
                _baseRenderer.SetPropertyBlock(slabMpb);
            }

            if (_traceRoot != null)
            {
                ApplyMpbToTree(_traceRoot.transform, _mpb);
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
        /// Start a continuous emission pulse on the source indicator (dot + halo).
        /// Only call on tiles that have a SourceDot indicator.
        /// Stops automatically when a new indicator is set or the tile is destroyed.
        /// </summary>
        public void StartSourcePulse()
        {
            if (_indicatorRoot == null) return;
            StartCoroutine(SourcePulseCoroutine());
        }

        private IEnumerator SourcePulseCoroutine()
        {
            // v3: Find new ENIG pad geometry by name
            var goldPad = _indicatorRoot.transform.Find("SourceENIGPad");
            var viaCenter = _indicatorRoot.transform.Find("SourceViaCenter");
            var halo = _indicatorRoot.transform.Find("SourceHalo");
            var centerPin = _indicatorRoot.transform.Find("SourcePin");
            if (goldPad == null || viaCenter == null) yield break;

            var viaRend = viaCenter.GetComponent<MeshRenderer>();
            if (viaRend == null) yield break;

            Color pulseColor = ChromaPalette.ViaCyan;
            // 1.0→1.4 sine wave, 1.5s cycle per spec
            float pulsePeriod = 1.5f;

            while (_indicatorRoot != null && viaCenter != null)
            {
                // Sine wave 0→1→0 with 1.5s cycle
                float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / pulsePeriod)) + 1f) * 0.5f;

                // Via center pulses: emission intensity 1.0→1.4 (spec)
                float viaIntensity = Mathf.Lerp(1.0f, 1.4f, t);

                if (viaRend != null && viaRend.sharedMaterial != null)
                {
                    viaRend.sharedMaterial.SetColor("_EmissionColor", pulseColor * viaIntensity);
                }

                // Pulse halo bloom
                if (halo != null)
                {
                    var haloRend = halo.GetComponent<MeshRenderer>();
                    if (haloRend != null && haloRend.sharedMaterial != null)
                    {
                        float haloIntensity = Mathf.Lerp(2.0f, 4.0f, t);
                        haloRend.sharedMaterial.SetColor("_EmissionColor", pulseColor * haloIntensity);
                    }
                }

                // Center pin breathes
                if (centerPin != null)
                {
                    var pinRend = centerPin.GetComponent<MeshRenderer>();
                    if (pinRend != null && pinRend.sharedMaterial != null)
                    {
                        float pinIntensity = Mathf.Lerp(10f, 18f, t);
                        pinRend.sharedMaterial.SetColor("_EmissionColor", pulseColor * pinIntensity);
                    }
                }

                yield return null;
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
