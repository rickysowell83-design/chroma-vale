using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Static factory that builds flat copper-foil trace meshes from Unity primitive Cubes.
    /// All traces use a shared copper material; per-tile color and emission is
    /// driven via MaterialPropertyBlock on the TileVisual that owns the trace.
    /// v3: Flat copper foil — NOT pipes/tubes/cylinders. Thin Cubes flushed to the PCB surface.
    /// </summary>
    public static class TraceMeshFactory3D
    {
        private static Material _traceMaterial;
        private static Material _traceOxidizedMaterial;
        private static Material _copperViaMaterial;

        /// <summary>
        /// Width of the flat copper trace — wide like a real PCB trace.
        /// </summary>
        private const float TraceWidth = 0.30f;

        /// <summary>
        /// Thickness (Z-depth) of the flat trace — almost flush with the tile slab.
        /// </summary>
        private const float TraceThickness = 0.025f;

        /// <summary>
        /// Z-offset to sit traces slightly in front of the tile slab surface.
        /// </summary>
        private const float TraceZ = -0.08f;

        /// <summary>
        /// Z-offset for via pad rings (slightly above traces).
        /// </summary>
        private const float PadZ = -0.09f;

        /// <summary>
        /// Radius of the center via hole / ENIG pad ring.
        /// </summary>
        private const float ViaOuterRadius = 0.32f;
        private const float ViaInnerRadius = 0.05f;
        private const float PadThickness = 0.06f;

        // ── Materials ──────────────────────────────────────────────────

        /// <summary>
        /// Active/lit copper trace material — copper base #B87333, metallic, with
        /// emission slot for TileVisual's MaterialPropertyBlock override.
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
                        color = ChromaPalette.CopperOxidized // #5C3A1E — overridden by MPB; player traces get PlayerTraceCopper
                    };
                    _traceMaterial.SetFloat("_Metallic", 0.85f);
                    _traceMaterial.SetFloat("_Smoothness", 0.55f);     // Bright specular copper — overridden by MPB for ghost state
                    _traceMaterial.EnableKeyword("_EMISSION");
                    _traceMaterial.SetColor("_EmissionColor", Color.black); // Dead until flow
                    _traceMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _traceMaterial;
            }
        }

        /// <summary>
        /// Oxidized copper material for unlit/ghost traces — dark matte.
        /// </summary>
        public static Material OxidizedTraceMaterial
        {
            get
            {
                if (_traceOxidizedMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _traceOxidizedMaterial = new Material(shader)
                    {
                        color = ChromaPalette.GhostTraceCopper  // Dark oxidized #3A2A1A — reads as "part of the board"
                    };
                    _traceOxidizedMaterial.SetFloat("_Metallic", 0.4f);   // Matte, non-reflective
                    _traceOxidizedMaterial.SetFloat("_Smoothness", 0.1f); // Dull, recessed
                    _traceOxidizedMaterial.EnableKeyword("_EMISSION");
                    _traceOxidizedMaterial.SetColor("_EmissionColor", Color.black); // Zero emission — ghost traces are dead
                    _traceOxidizedMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _traceOxidizedMaterial;
            }
        }

        /// <summary>
        /// ENIG gold via pad material for source/target contact pads.
        /// </summary>
        public static Material ViaPadMaterial
        {
            get
            {
                if (_copperViaMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");

                    _copperViaMaterial = new Material(shader)
                    {
                        color = ChromaPalette.ENIG_Gold
                    };
                    _copperViaMaterial.SetFloat("_Metallic", 0.95f);
                    _copperViaMaterial.SetFloat("_Smoothness", 0.6f);
                    _copperViaMaterial.EnableKeyword("_EMISSION");
                    _copperViaMaterial.SetColor("_EmissionColor", new Color(0.30f, 0.20f, 0.04f));
                    _copperViaMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return _copperViaMaterial;
            }
        }

        /// <summary>
        /// Set the _EmissionColor on a MaterialPropertyBlock for per-tile trace glow.
        /// Called by TileVisual during flow animation.
        /// </summary>
        public static void SetPipeEmission(MaterialPropertyBlock mpb, Color emissionColor)
        {
            mpb.SetColor("_EmissionColor", emissionColor * 5.0f);
            mpb.SetColor("_BaseColor", ChromaPalette.CopperActive);
        }

        // ── Factory ────────────────────────────────────────────────────

        /// <summary>
        /// Build a flat copper-foil trace as a child of the given transform.
        /// </summary>
        public static GameObject BuildPipe(SegmentShape shape, int rotationDeg, Transform parent)
        {
            var root = new GameObject("Trace_" + shape);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);

            switch (shape)
            {
                case SegmentShape.Straight:
                    BuildFlatStraight(root.transform);
                    break;

                case SegmentShape.Corner:
                    BuildFlatElbow(root.transform);
                    break;

                case SegmentShape.Splitter:
                    BuildFlatTJunction(root.transform);
                    break;

                case SegmentShape.CrossJunction:
                    BuildFlatCross(root.transform);
                    break;

                case SegmentShape.Diode:
                    BuildFlatValve(root.transform);
                    break;

                case SegmentShape.Repeater:
                    BuildFlatAmplifier(root.transform);
                    break;

                case SegmentShape.Combiner:
                    BuildFlatMixer(root.transform);
                    break;

                case SegmentShape.Breaker:
                    BuildFlatBlocker(root.transform);
                    break;

                default:
                    BuildFlatStraight(root.transform);
                    break;
            }

            return root;
        }

        // ── Flat Trace Primitives ──────────────────────────────────────

        /// <summary>
        /// Create a flat copper foil strip (thin Cube) for a trace segment.
        /// Width = TraceWidth, thickness = TraceThickness, length = height.
        /// Positioned flat on the PCB surface.
        /// </summary>
        private static GameObject CreateFlatTrace(float length, Vector3 localPos, bool horizontal, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "FlatTrace";
            Object.DestroyImmediate(cube.GetComponent<Collider>());

            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = new Vector3(localPos.x, localPos.y, TraceZ);
            // Horizontal traces run along X; vertical run along Y
            if (horizontal)
                cube.transform.localScale = new Vector3(length, TraceWidth, TraceThickness);
            else
                cube.transform.localScale = new Vector3(TraceWidth, length, TraceThickness);

            var renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = PipeMaterial;

            return cube;
        }

        /// <summary>
        /// Create a flat square pad at a joint/via location.
        /// </summary>
        private static GameObject CreateJointPad(Vector3 localPos, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "JointPad";
            Object.DestroyImmediate(cube.GetComponent<Collider>());

            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = new Vector3(localPos.x, localPos.y, TraceZ);
            // Slightly larger square where traces meet
            cube.transform.localScale = new Vector3(TraceWidth * 1.1f, TraceWidth * 1.1f, TraceThickness);

            var renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = PipeMaterial;

            return cube;
        }

        /// <summary>
        /// Create an octagonal via pad ring (approximated with a thin flattened cylinder).
        /// </summary>
/// <summary>
        /// Create an octagonal ENIG gold via pad ring using a custom 8-sided mesh.
        /// </summary>
        public static GameObject CreateViaPad(Vector3 worldPos, Transform parent, float scale = 1.0f)
        {
            var ring = new GameObject("ViaPad");
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, 0f, PadZ);

            float outerR = ViaOuterRadius * scale;
            float innerR = ViaInnerRadius * scale;
            int sides = 8;

            var mesh = new Mesh { name = "OctPadRing" };
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var norms = new System.Collections.Generic.List<Vector3>();

            // Build an octagonal ring: outer ring + inner ring, extruded
            for (int i = 0; i < sides; i++)
            {
                float angle = (i / (float)sides) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Top face vertices
                verts.Add(new Vector3(cos * outerR, sin * outerR, PadThickness * 0.5f));  // outer top
                verts.Add(new Vector3(cos * innerR, sin * innerR, PadThickness * 0.5f));  // inner top
                // Bottom face vertices
                verts.Add(new Vector3(cos * outerR, sin * outerR, -PadThickness * 0.5f)); // outer bottom
                verts.Add(new Vector3(cos * innerR, sin * innerR, -PadThickness * 0.5f)); // inner bottom
            }

            // Build triangles for each side segment
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int o0 = i * 4, i0 = i * 4 + 1, o1 = next * 4, i1 = next * 4 + 1;
                int ob0 = i * 4 + 2, ib0 = i * 4 + 3, ob1 = next * 4 + 2, ib1 = next * 4 + 3;

                // Top face (inner ring outward to outer ring)
                tris.AddRange(new[] { i0, o0, o1, i0, o1, i1 });
                // Bottom face
                tris.AddRange(new[] { ob0, ib0, ib1, ob0, ib1, ob1 });
                // Outer wall
                tris.AddRange(new[] { o0, ob0, ob1, o0, ob1, o1 });
                // Inner wall
                tris.AddRange(new[] { ib0, i0, i1, ib0, i1, ib1 });
            }

            // Normals (all point up for top faces, down for bottom; walls get per-face normals in shader)
            for (int i = 0; i < verts.Count; i++)
                norms.Add(new Vector3(0f, 0f, 1f));

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(norms);
            mesh.RecalculateBounds();

            var mf = ring.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ViaPadMaterial;

            return ring;
        }

        /// <summary>
        /// Create a dimple/hole in a via pad (dark center circle).
        /// </summary>
/// <summary>
        /// Create an octagonal dimple/hole in a via pad (dark center, 8-sided).
        /// </summary>
        public static GameObject CreateViaCenter(Vector3 worldPos, Transform parent, Color viaColor, float scale = 1.0f)
        {
            var center = new GameObject("ViaCenter");
            center.transform.SetParent(parent, false);
            center.transform.localPosition = new Vector3(0f, 0f, PadZ - 0.005f);

            float innerR = ViaInnerRadius * scale;
            int sides = 8;
            float thickness = PadThickness + 0.01f;

            var mesh = new Mesh { name = "OctCenter" };
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            // Top disc: center + ring vertices
            verts.Add(new Vector3(0f, 0f, thickness * 0.5f)); // index 0: center top
            // Bottom disc: center + ring vertices
            verts.Add(new Vector3(0f, 0f, -thickness * 0.5f)); // index 1: center bottom

            int vTopStart = 2;
            int vBotStart = 2 + sides;

            for (int i = 0; i < sides; i++)
            {
                float angle = (i / (float)sides) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts.Add(new Vector3(cos * innerR, sin * innerR, thickness * 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                float angle = (i / (float)sides) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts.Add(new Vector3(cos * innerR, sin * innerR, -thickness * 0.5f));
            }

            // Top face triangles (fan from center)
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris.AddRange(new[] { 0, vTopStart + i, vTopStart + next });
            }
            // Bottom face triangles (fan from center, reversed winding)
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris.AddRange(new[] { 1, vBotStart + next, vBotStart + i });
            }
            // Side wall triangles
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int t0 = vTopStart + i, t1 = vTopStart + next;
                int b0 = vBotStart + i, b1 = vBotStart + next;
                tris.AddRange(new[] { t0, b0, b1, t0, b1, t1 });
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = center.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var renderer = center.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader) { color = viaColor };
            mat.SetFloat("_Metallic", 0.1f);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", viaColor * 0.5f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            renderer.sharedMaterial = mat;

            return center;
        }

        // ── Shape Builders ─────────────────────────────────────────────

        private static void BuildFlatStraight(Transform parent)
        {
            CreateFlatTrace(1.0f, Vector3.zero, true, parent);
        }

        private static void BuildFlatElbow(Transform parent)
        {
            // Two half-length flat traces meeting at center
            CreateFlatTrace(0.5f, new Vector3(0.25f, 0f, 0f), true, parent);   // Right arm
            CreateFlatTrace(0.5f, new Vector3(0f, 0.25f, 0f), false, parent);  // Up arm
            CreateJointPad(Vector3.zero, parent);
        }

        private static void BuildFlatTJunction(Transform parent)
        {
            CreateFlatTrace(1.0f, Vector3.zero, true, parent);                  // Horizontal through
            CreateFlatTrace(0.5f, new Vector3(0f, 0.25f, 0f), false, parent);  // Vertical stub up
            CreateJointPad(Vector3.zero, parent);
        }

        private static void BuildFlatCross(Transform parent)
        {
            CreateFlatTrace(1.0f, Vector3.zero, true, parent);   // Horizontal
            CreateFlatTrace(1.0f, Vector3.zero, false, parent);  // Vertical
            CreateJointPad(Vector3.zero, parent);
        }

        private static void BuildFlatValve(Transform parent)
        {
            // Straight trace with diode symbol marking
            CreateFlatTrace(1.0f, Vector3.zero, true, parent);

            // Triangle arrow marker (using a small rotated cube as diamond)
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "ValveArrow";
            Object.DestroyImmediate(arrow.GetComponent<Collider>());
            arrow.transform.SetParent(parent, false);
            arrow.transform.localPosition = new Vector3(0.1f, 0f, TraceZ - 0.005f);
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            arrow.transform.localScale = new Vector3(0.12f, 0.12f, TraceThickness + 0.005f);
            var arrowRend = arrow.GetComponent<MeshRenderer>();
            if (arrowRend != null) arrowRend.sharedMaterial = PipeMaterial;

            // Vertical bar (diode cathode line)
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "ValveBar";
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0.2f, 0f, TraceZ - 0.005f);
            bar.transform.localScale = new Vector3(0.03f, TraceWidth * 0.7f, TraceThickness + 0.005f);
            var barRend = bar.GetComponent<MeshRenderer>();
            if (barRend != null) barRend.sharedMaterial = PipeMaterial;
        }

        private static void BuildFlatAmplifier(Transform parent)
        {
            // Horizontal trace through
            CreateFlatTrace(1.0f, Vector3.zero, true, parent);

            // Triangle chevron (op-amp symbol)
            var chevronRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chevronRight.name = "AmpChevronR";
            Object.DestroyImmediate(chevronRight.GetComponent<Collider>());
            chevronRight.transform.SetParent(parent, false);
            chevronRight.transform.localPosition = new Vector3(0.12f, 0.08f, TraceZ - 0.005f);
            chevronRight.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            chevronRight.transform.localScale = new Vector3(0.1f, 0.20f, TraceThickness + 0.005f);
            var rRend = chevronRight.GetComponent<MeshRenderer>();
            if (rRend != null) rRend.sharedMaterial = PipeMaterial;

            var chevronLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chevronLeft.name = "AmpChevronL";
            Object.DestroyImmediate(chevronLeft.GetComponent<Collider>());
            chevronLeft.transform.SetParent(parent, false);
            chevronLeft.transform.localPosition = new Vector3(0.12f, -0.08f, TraceZ - 0.005f);
            chevronLeft.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            chevronLeft.transform.localScale = new Vector3(0.1f, 0.20f, TraceThickness + 0.005f);
            var lRend = chevronLeft.GetComponent<MeshRenderer>();
            if (lRend != null) lRend.sharedMaterial = PipeMaterial;
        }

        private static void BuildFlatMixer(Transform parent)
        {
            // X-shaped diagonal crossing
            var diag1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diag1.name = "MixerDiag1";
            Object.DestroyImmediate(diag1.GetComponent<Collider>());
            diag1.transform.SetParent(parent, false);
            diag1.transform.localPosition = new Vector3(0f, 0f, TraceZ);
            diag1.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            diag1.transform.localScale = new Vector3(TraceWidth, 1.0f, TraceThickness);
            var d1Rend = diag1.GetComponent<MeshRenderer>();
            if (d1Rend != null) d1Rend.sharedMaterial = PipeMaterial;

            var diag2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diag2.name = "MixerDiag2";
            Object.DestroyImmediate(diag2.GetComponent<Collider>());
            diag2.transform.SetParent(parent, false);
            diag2.transform.localPosition = new Vector3(0f, 0f, TraceZ);
            diag2.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            diag2.transform.localScale = new Vector3(TraceWidth, 1.0f, TraceThickness);
            var d2Rend = diag2.GetComponent<MeshRenderer>();
            if (d2Rend != null) d2Rend.sharedMaterial = PipeMaterial;

            // Center mixing pad
            var center = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            center.name = "MixerCenter";
            Object.DestroyImmediate(center.GetComponent<Collider>());
            center.transform.SetParent(parent, false);
            center.transform.localPosition = new Vector3(0f, 0f, TraceZ - 0.003f);
            center.transform.localScale = new Vector3(TraceWidth * 1.6f, TraceThickness + 0.006f, TraceWidth * 1.6f);
            center.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var cRend = center.GetComponent<MeshRenderer>();
            if (cRend != null) cRend.sharedMaterial = PipeMaterial;
        }

        private static void BuildFlatBlocker(Transform parent)
        {
            // Dark barrier across the trace — blocks signal
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "BlockerBar";
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0f, 0f, TraceZ - 0.01f);
            bar.transform.localScale = new Vector3(TraceWidth + 0.06f, TraceWidth + 0.06f, TraceThickness + 0.015f);

            var renderer = bar.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                var mat = new Material(shader) { color = new Color(0.15f, 0.02f, 0.02f) };
                mat.SetFloat("_Metallic", 0.3f);
                mat.SetFloat("_Smoothness", 0.2f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.5f, 0.02f, 0.02f));
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                renderer.sharedMaterial = mat;
            }
        }
    }
}
