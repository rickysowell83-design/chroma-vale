using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Static factory that builds 3D pipe meshes from Unity primitive cylinders and spheres.
    /// All pipes use a shared dark-circuit-board material; per-tile color and emission is
    /// driven via MaterialPropertyBlock on the TileVisual that owns the pipe.
    /// </summary>
    public static class PipeMeshFactory3D
    {
        private static Material _pipeMaterial;

        /// <summary>
        /// Radius of pipe cylinders, relative to tile size = 1.0.
        /// </summary>
        private const float PipeRadius = 0.14f;

        /// <summary>
        /// Radius of joint spheres, slightly larger to cover cylinder seams.
        /// </summary>
        private const float JointRadius = 0.16f;

        /// <summary>
        /// Z-offset to sit pipes slightly in front of the tile slab.
        /// </summary>
        private const float PipeZ = -0.12f;

        /// <summary>
        /// Get or create the shared dark-circuit-board pipe material with cyberpunk aesthetic.
        /// </summary>
        public static Material PipeMaterial
        {
            get
            {
                if (_pipeMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _pipeMaterial = new Material(shader)
                    {
                        color = new Color(0.08f, 0.09f, 0.12f) // Darker circuit-board feel
                    };
                    _pipeMaterial.SetFloat("_Metallic", 0.9f);
                    _pipeMaterial.SetFloat("_Smoothness", 0.9f);
                    _pipeMaterial.EnableKeyword("_EMISSION");
                    // Subtle dark-cyan idle glow — TileVisual overrides this during flow animation
                    _pipeMaterial.SetColor("_EmissionColor", new Color(0.05f, 0.08f, 0.12f) * 2f);
                    _pipeMaterial.SetFloat("_CoatMask", 0.15f);
                    _pipeMaterial.EnableKeyword("_CLEARCOAT");
                    _pipeMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _pipeMaterial;
            }
        }

        /// <summary>
        /// Set the _EmissionColor on a MaterialPropertyBlock for per-tile pipe glow.
        /// Called by TileVisual during flow animation to make pipes glow neon cyan/magenta
        /// when flow passes through them.
        /// </summary>
        public static void SetPipeEmission(MaterialPropertyBlock mpb, Color emissionColor)
        {
            mpb.SetColor("_EmissionColor", emissionColor);
        }

        /// <summary>
        /// Build a 3D pipe mesh as a child of the given transform.
        /// </summary>
        /// <param name="shape">The pipe shape to build.</param>
        /// <param name="rotationDeg">Z-axis rotation in degrees.</param>
        /// <param name="parent">Parent transform for the pipe group.</param>
        /// <returns>The root GameObject of the pipe group (rotated by rotationDeg).</returns>
        public static GameObject BuildPipe(PieceShape shape, int rotationDeg, Transform parent)
        {
            var root = new GameObject("Pipe_" + shape);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);

            switch (shape)
            {
                case PieceShape.Straight:
                    BuildStraight(root.transform);
                    break;

                case PieceShape.Elbow:
                    BuildElbow(root.transform);
                    break;

                case PieceShape.TJunction:
                    BuildTJunction(root.transform);
                    break;

                case PieceShape.Cross:
                    BuildCross(root.transform);
                    break;

                case PieceShape.Valve:
                    BuildValve(root.transform);
                    break;

                default:
                    // Amplifier, Mixer, Blocker — render as straight pipe by default
                    BuildStraight(root.transform);
                    break;
            }

            return root;
        }

        /// <summary>
        /// Create a primitive cylinder configured as a pipe segment.
        /// Collider is destroyed immediately — collision lives on the tile BoxCollider.
        /// </summary>
        private static GameObject CreatePipeCylinder(float height, float radius, Vector3 localPos, float zRot, Transform parent)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "PipeSeg";
            Object.DestroyImmediate(cyl.GetComponent<Collider>());

            cyl.transform.SetParent(parent, false);
            cyl.transform.localPosition = new Vector3(localPos.x, localPos.y, PipeZ);
            cyl.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
            cyl.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);

            var renderer = cyl.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = PipeMaterial;

            return cyl;
        }

        /// <summary>
        /// Create a primitive sphere configured as a pipe joint.
        /// Collider is destroyed immediately.
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
            if (renderer != null) renderer.sharedMaterial = PipeMaterial;

            return sphere;
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
            if (renderer != null) renderer.sharedMaterial = PipeMaterial;
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
        /// Build a straight pipe: one cylinder along X axis, full tile length, with rivets.
        /// </summary>
        private static void BuildStraight(Transform parent)
        {
            // Cylinders default along Y — rotate 90° around Z to lie along X
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);
            AddSegmentRivets(Vector3.zero, 0.5f, true, parent);
        }

        /// <summary>
        /// Build an elbow: two half-length cylinders (X and Y) meeting at a joint sphere.
        /// </summary>
        private static void BuildElbow(Transform parent)
        {
            // Half-cylinder along +X from center
            CreatePipeCylinder(0.5f, PipeRadius, new Vector3(0.25f, 0f, 0f), 90f, parent);
            // Half-cylinder along +Y from center
            CreatePipeCylinder(0.5f, PipeRadius, new Vector3(0f, 0.25f, 0f), 0f, parent);
            // Joint sphere at center
            CreateJointSphere(Vector3.zero, parent);
            // Rivets at outer ends only (inner ends hidden by joint sphere)
            AddSegmentRivets(new Vector3(0.25f, 0f, 0f), 0.25f, true, parent);
            AddSegmentRivets(new Vector3(0f, 0.25f, 0f), 0.25f, false, parent);
        }

        /// <summary>
        /// Build a T-junction: full straight + half stub + joint sphere + rivets.
        /// </summary>
        private static void BuildTJunction(Transform parent)
        {
            // Full cylinder along X
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);
            // Half stub along +Y from center
            CreatePipeCylinder(0.5f, PipeRadius, new Vector3(0f, 0.25f, 0f), 0f, parent);
            // Joint sphere at center
            CreateJointSphere(Vector3.zero, parent);
            // Rivets
            AddSegmentRivets(Vector3.zero, 0.5f, true, parent);
            AddSegmentRivets(new Vector3(0f, 0.25f, 0f), 0.25f, false, parent);
        }

        /// <summary>
        /// Build a cross: two full cylinders crossing at center + joint sphere + rivets.
        /// </summary>
        private static void BuildCross(Transform parent)
        {
            // Full cylinder along X
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 90f, parent);
            // Full cylinder along Y
            CreatePipeCylinder(1.0f, PipeRadius, Vector3.zero, 0f, parent);
            // Joint sphere at center
            CreateJointSphere(Vector3.zero, parent);
            // Rivets
            AddSegmentRivets(Vector3.zero, 0.5f, true, parent);
            AddSegmentRivets(Vector3.zero, 0.5f, false, parent);
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
    }
}
