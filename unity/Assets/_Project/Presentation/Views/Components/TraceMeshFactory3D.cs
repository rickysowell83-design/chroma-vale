using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Static factory that builds 3D pipe meshes from Unity primitive cylinders and spheres.
    /// All pipes use a shared dark-circuit-board material; per-tile color and emission is
    /// driven via MaterialPropertyBlock on the TileVisual that owns the pipe.
    /// Joints now include glow-ring occlusion rings, and cross/elbow/T-junction have
    /// fillet-sphere chamfers for a smooth PCB trace appearance.
    /// </summary>
    public static class TraceMeshFactory3D
    {
        private static Material _traceMaterial;
        private static Material _occlusionRingMaterial;

        /// <summary>
        /// Radius of pipe cylinders — chunky copper like mockup, not thin PCB trace.
        /// </summary>
        private const float PipeRadius = 0.14f;

        /// <summary>
        /// Radius of joint spheres, slightly larger to cover cylinder seams.
        /// </summary>
        private const float JointRadius = 0.22f;

        /// <summary>
        /// Z-offset to sit pipes slightly in front of the tile slab.
        /// </summary>
        private const float PipeZ = -0.12f;

        /// <summary>
        /// Z-offset for the joint glow ring (slightly further forward than the pipe itself,
        /// creating a subtle ambient-occlusion / contact-shadow effect).
        /// </summary>
        private const float GlowRingZ = -0.13f;

        /// <summary>
        /// Radius of the glow ring that sits around each joint sphere.
        /// </summary>
        private const float GlowRingRadius = 0.18f;

        /// <summary>
        /// Very dark non-metallic material used for the glow/occlusion rings around joints.
        /// </summary>
        private static Material OcclusionRingMaterial
        {
            get
            {
                if (_occlusionRingMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _occlusionRingMaterial = new Material(shader)
                    {
                        color = new Color(0.02f, 0.025f, 0.035f) // Near-black for AO ring
                    };
                    _occlusionRingMaterial.SetFloat("_Metallic", 0f);
                    _occlusionRingMaterial.SetFloat("_Smoothness", 0f);
                }
                return _occlusionRingMaterial;
            }
        }

        /// <summary>
        /// Get or create the shared dark-cyberpunk pipe material with AAA URP Lit quality.
        /// Color: very dark brushed-metal core.
        /// Metallic 1.0 + Smoothness 0.85 gives a glossy glass/neon-tube surface.
        /// Emission is DEAD by default (black); TileVisual overrides via MaterialPropertyBlock.
        /// Clear-coat enabled for a subtle glass-like top layer.
        /// </summary>
        public static Material PipeMaterial
        {
            get
            {
                if (_traceMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _traceMaterial = new Material(shader)
                    {
                        color = new Color(0.06f, 0.07f, 0.10f) // Very dark brushed metal core
                    };
                    _traceMaterial.SetFloat("_Metallic", 1.0f);       // Fully metallic — AAA copper trace
                    _traceMaterial.SetFloat("_Smoothness", 0.85f);    // Glossy glass/neon tube surface
                    _traceMaterial.EnableKeyword("_EMISSION");
                    // DEFAULT DEAD — TileVisual's MaterialPropertyBlock overrides during flow animation
                    _traceMaterial.SetColor("_EmissionColor", Color.black);
                    _traceMaterial.SetFloat("_CoatMask", 0.25f);      // Clear-coat amount
                    _traceMaterial.EnableKeyword("_CLEARCOAT");        // Clear-coat layer enabled
                    _traceMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _traceMaterial;
            }
        }

        private static Material _copperPipeMaterial;

        /// <summary>
        /// Copper-toned pipe material for imported .glb models.
        /// Bright metallic copper core, emission dead by default.
        /// </summary>
        private static Material CopperPipeMaterial
        {
            get
            {
                if (_copperPipeMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _copperPipeMaterial = new Material(shader)
                    {
                        color = new Color(0.78f, 0.50f, 0.22f) // Brighter warm copper with specular highlights
                    };
                    _copperPipeMaterial.SetFloat("_Metallic", 1.0f);
                    _copperPipeMaterial.SetFloat("_Smoothness", 0.85f); // High gloss for specular highlights
                    _copperPipeMaterial.EnableKeyword("_EMISSION");
                    _copperPipeMaterial.SetColor("_EmissionColor", new Color(0.3f, 0.15f, 0.04f)); // Visible warm idle glow
                    _copperPipeMaterial.SetFloat("_CoatMask", 0.3f);
                    _copperPipeMaterial.EnableKeyword("_CLEARCOAT");
                    _copperPipeMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _copperPipeMaterial;
            }
        }

        private static Material _blockerBarrierMaterial;

        /// <summary>
        /// Dark red-black material for the Blocker barrier — communicates danger / do not pass.
        /// </summary>
        private static Material BlockerBarrierMaterial
        {
            get
            {
                if (_blockerBarrierMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _blockerBarrierMaterial = new Material(shader)
                    {
                        color = new Color(0.15f, 0.02f, 0.02f)
                    };
                    _blockerBarrierMaterial.SetFloat("_Metallic", 0.3f);
                    _blockerBarrierMaterial.SetFloat("_Smoothness", 0.2f);
                    _blockerBarrierMaterial.EnableKeyword("_EMISSION");
                    _blockerBarrierMaterial.SetColor("_EmissionColor", new Color(0.3f, 0.01f, 0.01f) * 2f);
                    _blockerBarrierMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _blockerBarrierMaterial;
            }
        }

        /// <summary>
        /// Set the _EmissionColor on a MaterialPropertyBlock for per-tile pipe glow.
        /// Intensity is multiplied by 5.0 for a punchy neon effect.
        /// Called by TileVisual during flow animation.
        /// </summary>
        public static void SetPipeEmission(MaterialPropertyBlock mpb, Color emissionColor)
        {
            mpb.SetColor("_EmissionColor", emissionColor * 5.0f);
        }

        /// <summary>
        /// Try loading a Sketchfab pipe prefab from Resources. Instantiate on success.
        /// Returns true if loaded, false to fall back to runtime cylinders.
        /// </summary>
        private static bool TryLoadPrefab(string resourcePath, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/SketchfabPipes/" + resourcePath);
            if (prefab == null) return false;

            var instance = Object.Instantiate(prefab, parent);
            instance.name = resourcePath;
            instance.transform.localPosition = new Vector3(0f, 0f, PipeZ);
            // Model is vertical (Y-up), rotate to horizontal (X-aligned) like our cylinders
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Convert glTF shader to URP/Lit and scale to fit tile
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var mr in instance.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat != null && mat.shader.name.Contains("glTF"))
                    {
                        mat.shader = urpShader;
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", Color.black);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                }
            }

            // Scale: native ~5 units tall → fit in 1.0 unit
            instance.transform.localScale = Vector3.one * 0.2f;

            foreach (var col in instance.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);

            return true;
        }

        /// <summary>
        /// Build a 3D pipe mesh as a child of the given transform.
        /// </summary>
        /// <param name="shape">The trace shape to build.</param>
        /// <param name="rotationDeg">Z-axis rotation in degrees.</param>
        /// <param name="parent">Parent transform for the pipe group.</param>
        /// <returns>The root GameObject of the pipe group (rotated by rotationDeg).</returns>
        public static GameObject BuildPipe(SegmentShape shape, int rotationDeg, Transform parent)
        {
            var root = new GameObject("Pipe_" + shape);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);

            switch (shape)
            {
                case SegmentShape.Straight:
                    BuildStraight(root.transform);
                    break;

                case SegmentShape.Corner:
                    BuildElbow(root.transform);
                    break;

                case SegmentShape.Splitter:
                    BuildTJunction(root.transform);
                    break;

                case SegmentShape.CrossJunction:
                    BuildCross(root.transform);
                    break;

                case SegmentShape.Diode:
                    BuildValve(root.transform);
                    break;

                case SegmentShape.Repeater:
                    BuildAmplifier(root.transform);
                    break;

                case SegmentShape.Combiner:
                    BuildMixer(root.transform);
                    break;

                case SegmentShape.Breaker:
                    BuildBlocker(root.transform);
                    break;

                default:
                    BuildStraight(root.transform);
                    break;
            }

            return root;
        }

        /// <summary>
        /// Create a primitive cylinder configured as a pipe segment.
        /// Collider is destroyed immediately — collision lives on the tile BoxCollider.
        /// Z localScale uses radius*2.2f for a subtly oval "trace" cross-section rather
        /// than a round tube — more PCB-like.
        /// </summary>
        private static GameObject CreatePipeCylinder(float height, float radius, Vector3 localPos, float zRot, Transform parent)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "PipeSeg";
            Object.DestroyImmediate(cyl.GetComponent<Collider>());

            cyl.transform.SetParent(parent, false);
            cyl.transform.localPosition = new Vector3(localPos.x, localPos.y, PipeZ);
            cyl.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
            // Flat wide cross-section like a PCB trace (wider than tall)
            cyl.transform.localScale = new Vector3(radius * 2.0f, height / 2f, radius * 1.5f);

            var renderer = cyl.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = CopperPipeMaterial;

            return cyl;
        }

        /// <summary>
        /// Create a primitive sphere configured as a pipe joint.
        /// Collider is destroyed immediately.
        /// Also creates a subtle glow/occlusion ring around the joint.
        /// </summary>
        private static GameObject CreateJointSphere(Vector3 localPos, Transform parent)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "PipeJoint";
            Object.DestroyImmediate(sphere.GetComponent<Collider>());

            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = new Vector3(localPos.x, localPos.y, PipeZ);
            sphere.transform.localScale = Vector3.one * (JointRadius * 2f);

            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = CopperPipeMaterial;

            // Add subtle ambient-occlusion / contact-shadow ring around this joint
            CreateJointGlowRing(localPos, parent);

            return sphere;
        }

        /// <summary>
        /// Create a tiny thin glow ring around a joint sphere for a subtle ambient-occlusion /
        /// contact-shadow effect — enhances the "glass tube sitting on PCB" look.
        /// </summary>
        private static void CreateJointGlowRing(Vector3 localPos, Transform parent)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PipeGlowRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(localPos.x, localPos.y, GlowRingZ);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Flat disc
            ring.transform.localScale = new Vector3(GlowRingRadius * 2f, 0.015f, GlowRingRadius * 2f);

            var renderer = ring.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = OcclusionRingMaterial;
        }

        /// <summary>
        /// Create a small fillet sphere for joint corner blending (smooth PCB chamfer look).
        /// </summary>
        private static void CreateFilletSphere(Vector3 localPos, float scale, Transform parent)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "PipeFillet";
            Object.DestroyImmediate(sphere.GetComponent<Collider>());

            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = new Vector3(localPos.x, localPos.y, PipeZ);
            sphere.transform.localScale = Vector3.one * scale;

            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = CopperPipeMaterial;
        }

        /// <summary>
        /// Create a single tiny rivet sphere for industrial cyberpunk detailing.
        /// </summary>
        private static void CreateRivet(Vector3 localPos, float scale, Transform parent)
        {
            var rivet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rivet.name = "PipeRivet";
            Object.DestroyImmediate(rivet.GetComponent<Collider>());
            rivet.transform.SetParent(parent, false);
            rivet.transform.localPosition = new Vector3(localPos.x, localPos.y, PipeZ);
            rivet.transform.localScale = Vector3.one * scale;
            var renderer = rivet.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = CopperPipeMaterial;
        }

        /// <summary>
        /// Add four rivet spheres (two at each end) flanking a pipe segment.
        /// </summary>
        /// <param name="center">Center position of the cylinder parent.</param>
        /// <param name="halfLength">Half the cylinder's length along its axis.</param>
        /// <param name="horizontal">True if the cylinder is oriented along X (zRot=90).</param>
        /// <param name="parent">Parent transform of the pipe group.</param>
        private static void AddSegmentRivets(Vector3 center, float halfLength, bool horizontal, Transform parent)
        {
            float off = PipeRadius * 0.7f;
            const float rivetScale = 0.03f;

            if (horizontal)
            {
                // Pipe along X — rivets above/below at each end
                CreateRivet(new Vector3(center.x - halfLength, center.y + off, 0f), rivetScale, parent);
                CreateRivet(new Vector3(center.x - halfLength, center.y - off, 0f), rivetScale, parent);
                CreateRivet(new Vector3(center.x + halfLength, center.y + off, 0f), rivetScale, parent);
                CreateRivet(new Vector3(center.x + halfLength, center.y - off, 0f), rivetScale, parent);
            }
            else
            {
                // Pipe along Y — rivets left/right at each end
                CreateRivet(new Vector3(center.x + off, center.y - halfLength, 0f), rivetScale, parent);
                CreateRivet(new Vector3(center.x - off, center.y - halfLength, 0f), rivetScale, parent);
                CreateRivet(new Vector3(center.x + off, center.y + halfLength, 0f), rivetScale, parent);
                CreateRivet(new Vector3(center.x - off, center.y + halfLength, 0f), rivetScale, parent);
            }
        }

        /// <summary>
        /// Build a straight pipe using the imported .glb model if available,
        /// falling back to a runtime cylinder.
        /// </summary>
        private static void BuildStraight(Transform parent)
        {
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);
        }

        /// <summary>
        /// Build an elbow: two half-length cylinders meeting at a joint sphere.
        /// </summary>
        private static void BuildElbow(Transform parent)
        {
            CreatePipeCylinder(0.5f, PipeRadius, new Vector3(0.25f, 0f, 0f), 90f, parent);
            CreatePipeCylinder(0.5f, PipeRadius, new Vector3(0f, 0.25f, 0f), 0f, parent);
            CreateJointSphere(Vector3.zero, parent);
        }

        /// <summary>
        /// Build a T-junction: full straight + half stub + joint sphere + rivets + fillets.
        /// Two fillet spheres smooth the inner corners where the stub meets the straight.
        /// </summary>
        private static void BuildTJunction(Transform parent)
        {
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);
            CreatePipeCylinder(0.5f, PipeRadius, new Vector3(0f, 0.25f, 0f), 0f, parent);
            CreateJointSphere(Vector3.zero, parent);
        }

        /// <summary>
        /// Build a cross: two full cylinders crossing at center + joint sphere + rivets + fillets.
        /// Four fillet spheres at the inner corners blend the intersection for a smooth fillet look.
        /// </summary>
        private static void BuildCross(Transform parent)
        {
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 0f, parent);
            CreateJointSphere(Vector3.zero, parent);
        }

        /// <summary>
        /// Build a valve: straight pipe + flattened cylinder ring + cylinder disc handle on top + rivets.
        /// </summary>
        private static void BuildValve(Transform parent)
        {
            // Straight pipe along X
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);
            AddSegmentRivets(Vector3.zero, 0.5f, true, parent);

            // Ring disk: flattened cylinder (torus-like disc, lies in pipe plane)
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ValveRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, 0f, PipeZ - 0.04f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(0.55f, 0.04f, 0.55f);
            var ringRenderer = ring.GetComponent<MeshRenderer>();
            if (ringRenderer != null) ringRenderer.sharedMaterial = PipeMaterial;

            // Handle disc: flat cylinder knob on top of the ring
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "ValveHandle";
            Object.DestroyImmediate(handle.GetComponent<Collider>());
            handle.transform.SetParent(parent, false);
            handle.transform.localPosition = new Vector3(0f, 0.28f, PipeZ - 0.04f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.22f, 0.04f, 0.22f);
            var handleRenderer = handle.GetComponent<MeshRenderer>();
            if (handleRenderer != null) handleRenderer.sharedMaterial = PipeMaterial;
        }

        /// <summary>
        /// Build an amplifier: triangle of 3 angled cylinders + apex joint on a straight trace.
        /// The triangle/chevron shape is unique — reads like an op-amp symbol.
        /// </summary>
        private static void BuildAmplifier(Transform parent)
        {
            // Main horizontal trace through the component
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);

            // Triangle body — 3 angled cylinders forming a right-pointing triangle
            CreatePipeCylinder(0.36f, PipeRadius, new Vector3(0.10f, 0f, 0f), 0f, parent);
            CreatePipeCylinder(0.28f, PipeRadius, new Vector3(0.20f, 0.09f, 0f), 45f, parent);
            CreatePipeCylinder(0.28f, PipeRadius, new Vector3(0.20f, -0.09f, 0f), -45f, parent);

            // Joint sphere at triangle apex (right point)
            CreateJointSphere(new Vector3(0.30f, 0f, 0f), parent);

            // Output trace stub to the right of the triangle
            CreatePipeCylinder(0.20f, PipeRadius, new Vector3(0.50f, 0f, 0f), 90f, parent);

            // Small input marker dot on the left side
            CreateFilletSphere(new Vector3(0.02f, 0f, 0f), 0.04f, parent);
        }

        /// <summary>
        /// Build a mixer: X-shaped diagonal crossing + oversized mixing chamber sphere.
        /// The enlarged center sphere immediately reads as "something happens here."
        /// </summary>
        private static void BuildMixer(Transform parent)
        {
            // Main horizontal trace
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);

            // X-shaped diagonals crossing at center
            CreatePipeCylinder(0.35f, PipeRadius, Vector3.zero, 45f, parent);
            CreatePipeCylinder(0.35f, PipeRadius, Vector3.zero, -45f, parent);

            // Enlarged mixing chamber sphere (2.5x normal joint diameter)
            var mixer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mixer.name = "MixerChamber";
            Object.DestroyImmediate(mixer.GetComponent<Collider>());
            mixer.transform.SetParent(parent, false);
            mixer.transform.localPosition = new Vector3(0f, 0f, PipeZ);
            mixer.transform.localScale = Vector3.one * (JointRadius * 5f);
            var mr = mixer.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = CopperPipeMaterial;

            // Larger glow ring around mixing chamber
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "MixerGlowRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, 0f, GlowRingZ);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(GlowRingRadius * 5f, 0.015f, GlowRingRadius * 5f);
            var rr = ring.GetComponent<MeshRenderer>();
            if (rr != null) rr.sharedMaterial = OcclusionRingMaterial;
        }

        /// <summary>
        /// Build a blocker: truncated stubs + dark red-black barrier block.
        /// The gap and red-black barrier communicate "flow stops here."
        /// </summary>
        private static void BuildBlocker(Transform parent)
        {
            // Truncated stubs — don't reach center, leaving a gap
            CreatePipeCylinder(0.30f, PipeRadius, new Vector3(-0.35f, 0f, 0f), 90f, parent);
            CreatePipeCylinder(0.30f, PipeRadius, new Vector3(0.35f, 0f, 0f), 90f, parent);

            // Solid barrier block across the gap
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "BlockerBarrier";
            Object.DestroyImmediate(barrier.GetComponent<Collider>());
            barrier.transform.SetParent(parent, false);
            barrier.transform.localPosition = new Vector3(0f, 0f, PipeZ - 0.02f);
            barrier.transform.localScale = new Vector3(0.12f, 0.55f, 0.30f);
            var br = barrier.GetComponent<MeshRenderer>();
            if (br != null) br.sharedMaterial = BlockerBarrierMaterial;

            // Warning X mark on the barrier face
            CreateRivet(new Vector3(0f, 0.06f, PipeZ - 0.03f), PipeRadius * 0.4f, parent);
            CreateRivet(new Vector3(0f, -0.06f, PipeZ - 0.03f), PipeRadius * 0.4f, parent);
        }
    }
}
