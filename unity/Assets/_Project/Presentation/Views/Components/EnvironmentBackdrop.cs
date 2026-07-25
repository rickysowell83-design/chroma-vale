using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class EnvironmentBackdrop : MonoBehaviour
    {
        private bool _built;

        // Shared building body materials (one per depth tier)
        private Material _bodyMatFar;
        private Material _bodyMatMid;
        private Material _bodyMatNear;
        private Material _bodyMatForeground;

        // Shared neon emissive materials (one per color)
        private Material _neonCyanMat;
        private Material _neonMagentaMat;
        private Material _neonPurpleMat;

        private static readonly int _emissionColorId = Shader.PropertyToID("_EmissionColor");

        // Window tracking for blinking effect
        private readonly List<GameObject> _litWindows = new List<GameObject>();
        private Coroutine _blinkCoroutine;

        // Depth-based emission multipliers
        private static readonly (float z, float emissionMul, string label)[] _rowConfig = new[]
        {
            (5f,  1.0f, "Foreground"),
            (9f,  0.85f, "Near"),
            (18f, 0.7f,  "Mid"),
            (30f, 0.4f,  "Far"),
        };

        /// <summary>
        /// Builds the 3D cyberpunk skyline behind the puzzle grid.
        /// Four parallax layers with depth-based emission falloff,
        /// blinking windows, skybridges, mega-structures, antenna spires,
        /// and a volumetric fog plane.
        /// Idempotent: calling twice destroys previous children first.
        /// </summary>
        public void Build()
        {
            if (_built)
            {
                StopBlinkCoroutine();
                ClearChildren();
                _litWindows.Clear();
            }

            var prevState = Random.state;
            Random.InitState(429);

            CreateSharedMaterials();

            // ── Four parallax building rows ────────────────────────────
            // Foreground: z=5,  tall dark silhouettes, minimal detail, heights 2-5
            BuildBuildingRow(z: 5,  count: 6,  minH: 2f,  maxH: 5f,  minW: 1.5f, maxW: 3f,  minD: 1f,   maxD: 2f,
                bodyMat: _bodyMatForeground, isForeground: true);
            // Near row:   z=9,  tallest detail, heights 3-8
            BuildBuildingRow(z: 9,  count: 10, minH: 3f,  maxH: 8f,  minW: 1.5f, maxW: 4f,  minD: 1.2f, maxD: 2f,
                bodyMat: _bodyMatNear);
            // Mid row:    z=18, medium scale, heights 5-14
            BuildBuildingRow(z: 18, count: 14, minH: 5f,  maxH: 14f, minW: 1.5f, maxW: 5f,  minD: 0.8f, maxD: 1.5f,
                bodyMat: _bodyMatMid);
            // Far row:    z=30, silhouettes, heights 10-25
            BuildBuildingRow(z: 30, count: 18, minH: 10f, maxH: 25f, minW: 2f,  maxW: 6f,  minD: 0.5f, maxD: 1f,
                bodyMat: _bodyMatFar);

            // ── Neon windows with depth-based emission falloff (~55% lit) ─
            BuildNeonWindows(z: 5,  rowCount: 6,  minWindows: 1, maxWindows: 3,
                emissionMul: _rowConfig[0].emissionMul);
            BuildNeonWindows(z: 9,  rowCount: 10, minWindows: 2, maxWindows: 5,
                emissionMul: _rowConfig[1].emissionMul);
            BuildNeonWindows(z: 18, rowCount: 14, minWindows: 1, maxWindows: 4,
                emissionMul: _rowConfig[2].emissionMul);
            BuildNeonWindows(z: 30, rowCount: 18, minWindows: 0, maxWindows: 3,
                emissionMul: _rowConfig[3].emissionMul);

            // ── Neon accents ───────────────────────────────────────────
            BuildAntennaSpires(z: 9,  rowCount: 10);
            BuildAntennaSpires(z: 18, rowCount: 14);
            BuildAntennaSpires(z: 30, rowCount: 18);
            BuildNeonSignFrame(z: 9, rowCount: 10);
            BuildSkybridges(z: 9, rowCount: 10);
            BuildHorizonStrip();
            BuildFogPlane();

            // ── Billboard ──────────────────────────────────────────────
            CreateBillboard(z: 9, rowCount: 10);

            // ── Start window blinking coroutine ────────────────────────
            if (Application.isPlaying && _litWindows.Count > 0)
                _blinkCoroutine = StartCoroutine(BlinkWindowsCoroutine());

            Random.state = prevState;
            _built = true;
        }

        // ── Cleanup ────────────────────────────────────────────────────────

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        private void StopBlinkCoroutine()
        {
            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }
        }

        // ── Material helpers ───────────────────────────────────────────────

        private static Shader FindLitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        private static Material CreateLitMaterial(Color color, float metallic, float smoothness)
        {
            var shader = FindLitShader();
            var mat = new Material(shader);
            mat.color = color;
            if (shader.name != "Sprites/Default")
            {
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
            }
            return mat;
        }

        private static Material CreateEmissiveMaterial(Color baseColor, Color emissionColor)
        {
            var shader = FindLitShader();
            var mat = new Material(shader);
            mat.color = baseColor;
            if (shader.name != "Sprites/Default")
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(_emissionColorId, emissionColor);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return mat;
        }

        private void CreateSharedMaterials()
        {
            // Building bodies: progressively darker/less saturated with distance
            // Foreground (z=5):  most saturated, lightest
            _bodyMatForeground = CreateLitMaterial(new Color(0.12f, 0.06f, 0.20f), 0.7f, 0.5f);
            // Near row (z=9):    rich purple
            _bodyMatNear       = CreateLitMaterial(new Color(0.08f, 0.04f, 0.14f), 0.6f, 0.4f);
            // Mid row (z=18):    medium saturation
            _bodyMatMid        = CreateLitMaterial(new Color(0.05f, 0.03f, 0.10f), 0.5f, 0.35f);
            // Far row (z=30):    darkest, least saturated
            _bodyMatFar        = CreateLitMaterial(new Color(0.02f, 0.015f, 0.05f), 0.4f, 0.3f);

            // Neon emissive (base: 3x palette values for bloom)
            _neonCyanMat = CreateEmissiveMaterial(
                new Color(0.02f, 0.02f, 0.04f),
                ChromaPalette.NeonCyan * 3f);
            _neonMagentaMat = CreateEmissiveMaterial(
                new Color(0.04f, 0.01f, 0.03f),
                ChromaPalette.NeonMagenta * 3f);
            _neonPurpleMat = CreateEmissiveMaterial(
                new Color(0.03f, 0.01f, 0.04f),
                ChromaPalette.NeonPurple * 3f);
        }

        // ── Building row construction ──────────────────────────────────────

        private void BuildBuildingRow(float z, int count, float minH, float maxH,
            float minW, float maxW, float minD, float maxD, Material bodyMat,
            bool isForeground = false)
        {
            float startX = -10f;
            float endX = 10f;
            float spacing = (endX - startX) / count;
            float baseY = -3f;

            for (int i = 0; i < count; i++)
            {
                float x = startX + spacing * i + Random.Range(-0.5f, 0.5f);
                float h = Random.Range(minH, maxH);
                float w = Random.Range(minW, maxW);

                // ── Mega-structure: 3-4x wider, taller, ~10% chance ──
                bool isMega = !isForeground && Random.value < 0.1f;
                if (isMega)
                {
                    w *= Random.Range(3f, 4f);
                    h = Mathf.Max(h, maxH * 0.85f);
                }

                float d = Random.Range(minD, maxD);

                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Bldg_Z" + z + "_" + i + (isMega ? "_MEGA" : "");
                b.transform.SetParent(transform);
                b.transform.localPosition = new Vector3(x, baseY + h / 2f, z);
                b.transform.localScale = new Vector3(w, h, d);
                Destroy(b.GetComponent<Collider>());
                b.GetComponent<MeshRenderer>().material = bodyMat;
            }
        }

        // ── Neon windows with depth-based emission falloff ─────────────────

        private void BuildNeonWindows(float z, int rowCount, int minWindows,
            int maxWindows, float emissionMul)
        {
            int totalWindows = 0;
            const int maxTotal = 200;

            for (int i = 0; i < rowCount && totalWindows < maxTotal; i++)
            {
                string prefix = "Bldg_Z" + z + "_" + i;
                var bldg = transform.Find(prefix);
                if (bldg == null) continue;

                // Mega-structures get bonus windows
                bool isMega = bldg.name.Contains("_MEGA");
                int baseWindowCount = Random.Range(minWindows, maxWindows + 1);
                if (isMega)
                    baseWindowCount += Random.Range(3, 8);

                int windows = Mathf.Min(baseWindowCount, maxTotal - totalWindows);

                float halfH = bldg.localScale.y / 2f;
                float halfW = bldg.localScale.x / 2f;
                float halfD = bldg.localScale.z / 2f;

                for (int w = 0; w < windows; w++)
                {
                    bool lit = Random.value < 0.55f;

                    float winX = Random.Range(-halfW + 0.15f, halfW - 0.15f);
                    float winY = Random.Range(-halfH + 0.2f, halfH - 0.2f);

                    var win = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    win.name = "Win_Z" + z + "_" + i + "_" + w;
                    win.transform.SetParent(bldg);
                    win.transform.localPosition = new Vector3(winX, winY, halfD + 0.01f);
                    win.transform.localScale = new Vector3(0.12f, 0.08f, 1f);
                    Destroy(win.GetComponent<Collider>());

                    var renderer = win.GetComponent<MeshRenderer>();

                    if (lit)
                    {
                        // Apply depth-based emission falloff
                        var mat = new Material(PickRandomNeonMat());
                        if (!Mathf.Approximately(emissionMul, 1f))
                        {
                            Color em = mat.GetColor(_emissionColorId);
                            mat.SetColor(_emissionColorId, em * emissionMul);
                        }
                        renderer.sharedMaterial = mat;
                        _litWindows.Add(win);
                    }
                    else
                    {
                        renderer.sharedMaterial = _bodyMatFar;
                    }

                    totalWindows++;
                }
            }
        }

        private Material PickRandomNeonMat()
        {
            float r = Random.value;
            if (r < 0.4f) return _neonCyanMat;
            if (r < 0.75f) return _neonMagentaMat;
            return _neonPurpleMat;
        }

        // ── Antenna spires ─────────────────────────────────────────────────

        private void BuildAntennaSpires(float z, int rowCount)
        {
            for (int i = 0; i < rowCount; i++)
            {
                string prefix = "Bldg_Z" + z + "_" + i;
                var bldg = transform.Find(prefix);
                if (bldg == null) continue;
                if (bldg.localScale.y < 5f) continue;
                if (Random.value > 0.3f) continue;

                float halfH = bldg.localScale.y / 2f;

                // Tall thin spire (cylinder)
                var spire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spire.name = "Spire_Z" + z + "_" + i;
                spire.transform.SetParent(bldg);
                spire.transform.localPosition = new Vector3(
                    Random.Range(-0.2f, 0.2f), halfH + 1.5f, Random.Range(-0.2f, 0.2f));
                spire.transform.localScale = new Vector3(0.05f, Random.Range(1.2f, 2.8f), 0.05f);
                Destroy(spire.GetComponent<Collider>());
                spire.GetComponent<MeshRenderer>().material = _bodyMatMid;

                // Beacon sphere on top
                var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                beacon.name = "Beacon_Z" + z + "_" + i;
                beacon.transform.SetParent(spire.transform);
                beacon.transform.localPosition = new Vector3(0f, 1f, 0f);
                beacon.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                Destroy(beacon.GetComponent<Collider>());

                Material beaconMat = (i % 2 == 0) ? _neonCyanMat : _neonMagentaMat;
                beacon.GetComponent<MeshRenderer>().material = beaconMat;
            }
        }

        // ── Skybridges ─────────────────────────────────────────────────────

        private void BuildSkybridges(float z, int rowCount)
        {
            for (int i = 0; i < rowCount - 1; i++)
            {
                if (Random.value > 0.2f) continue;

                var bldgA = transform.Find("Bldg_Z" + z + "_" + i);
                var bldgB = transform.Find("Bldg_Z" + z + "_" + (i + 1));
                if (bldgA == null || bldgB == null) continue;
                if (bldgA.localScale.y < 3f || bldgB.localScale.y < 3f) continue;

                float minHeight = Mathf.Min(bldgA.localScale.y, bldgB.localScale.y);
                float bridgeY = Random.Range(minHeight * 0.3f, minHeight * 0.7f);
                float ax = bldgA.localPosition.x;
                float bx = bldgB.localPosition.x;
                float midX = (ax + bx) / 2f;
                float span = Mathf.Abs(bx - ax);

                // Horizontal bridge as a thin, wide cube
                var bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bridge.name = "Skybridge_" + z + "_" + i;
                bridge.transform.SetParent(transform);
                bridge.transform.localPosition = new Vector3(
                    midX,
                    bldgA.localPosition.y - bldgA.localScale.y / 2f + bridgeY,
                    z);
                bridge.transform.localScale = new Vector3(span * 0.7f, 0.1f, 0.25f);
                Destroy(bridge.GetComponent<Collider>());
                bridge.GetComponent<MeshRenderer>().material = PickRandomNeonMat();
            }
        }

        // ── Neon sign frame ───────────────────────────────────────────────

        private void BuildNeonSignFrame(float z, int rowCount)
        {
            for (int i = 0; i < rowCount; i++)
            {
                string prefix = "Bldg_Z" + z + "_" + i;
                var bldg = transform.Find(prefix);
                if (bldg == null) continue;
                if (bldg.localScale.y < 5f) continue;

                float halfH = bldg.localScale.y / 2f;
                float halfW = bldg.localScale.x / 2f;
                float halfD = bldg.localScale.z / 2f;

                float signW = Mathf.Min(1.2f, halfW * 0.7f);
                float signH = Mathf.Min(1.8f, halfH * 0.5f);
                float signY = halfH * 0.1f;
                float borderThick = 0.04f;
                float faceZ = halfD + 0.015f;

                CreateBorderStrip("SignFrame_Top", bldg,
                    new Vector3(0f, signY + signH / 2f, faceZ),
                    new Vector3(signW + borderThick, borderThick, borderThick), _neonMagentaMat);
                CreateBorderStrip("SignFrame_Bot", bldg,
                    new Vector3(0f, signY - signH / 2f, faceZ),
                    new Vector3(signW + borderThick, borderThick, borderThick), _neonMagentaMat);
                CreateBorderStrip("SignFrame_Left", bldg,
                    new Vector3(-signW / 2f - borderThick / 2f, signY, faceZ),
                    new Vector3(borderThick, signH + borderThick, borderThick), _neonMagentaMat);
                CreateBorderStrip("SignFrame_Right", bldg,
                    new Vector3(signW / 2f + borderThick / 2f, signY, faceZ),
                    new Vector3(borderThick, signH + borderThick, borderThick), _neonMagentaMat);

                // Fill with dark emissive quad so the frame stands out
                var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
                fill.name = "SignFrame_Fill";
                fill.transform.SetParent(bldg);
                fill.transform.localPosition = new Vector3(0f, signY, faceZ - 0.005f);
                fill.transform.localScale = new Vector3(signW, signH, 1f);
                Destroy(fill.GetComponent<Collider>());
                fill.GetComponent<MeshRenderer>().material = _bodyMatFar;
                fill.GetComponent<MeshRenderer>().material.color = new Color(0.01f, 0.005f, 0.03f);

                break; // Only one sign frame
            }
        }

        private void CreateBorderStrip(string name, Transform parent, Vector3 localPos,
            Vector3 scale, Material mat)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent);
            strip.transform.localPosition = localPos;
            strip.transform.localScale = scale;
            Destroy(strip.GetComponent<Collider>());
            strip.GetComponent<MeshRenderer>().material = mat;
        }

        // ── Horizon glow strip ────────────────────────────────────────────

        private void BuildHorizonStrip()
        {
            var horizon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horizon.name = "HorizonStrip";
            horizon.transform.SetParent(transform);
            // Positioned past the far row for visible glow
            horizon.transform.localPosition = new Vector3(0f, -3f, 32f);
            // Wider (40 units) and slightly taller for more visible glow
            horizon.transform.localScale = new Vector3(40f, 0.08f, 1f);
            Destroy(horizon.GetComponent<Collider>());

            // 5x emission for dramatic bloom — up from 3x base
            var horizonMat = new Material(_neonCyanMat);
            Color em = horizonMat.GetColor(_emissionColorId);
            horizonMat.SetColor(_emissionColorId, em * (5f / 3f));
            horizon.GetComponent<MeshRenderer>().material = horizonMat;
        }

        // ── Volumetric fog plane ──────────────────────────────────────────

        private void BuildFogPlane()
        {
            var fog = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fog.name = "FogPlane";
            fog.transform.SetParent(transform);
            fog.transform.localPosition = new Vector3(0f, 4f, 35f);
            fog.transform.localScale = new Vector3(40f, 20f, 1f);
            Destroy(fog.GetComponent<Collider>());

            var fogMat = new Material(FindLitShader());
            fogMat.color = new Color(0.05f, 0.02f, 0.08f, 0.12f);

            if (fogMat.shader.name != "Sprites/Default")
            {
                fogMat.EnableKeyword("_EMISSION");
                fogMat.SetColor(_emissionColorId, new Color(0.12f, 0.04f, 0.22f) * 2f);
                fogMat.SetFloat("_Metallic", 0f);
                fogMat.SetFloat("_Smoothness", 0f);

                // Transparent rendering
                fogMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                fogMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                fogMat.SetInt("_ZWrite", 0);
                fogMat.renderQueue = 3000;
                fogMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                fogMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            fog.GetComponent<MeshRenderer>().material = fogMat;
        }

        // ── Billboard ─────────────────────────────────────────────────────

        private void CreateBillboard(float z, int rowCount)
        {
            Transform targetBldg = null;
            for (int i = 0; i < rowCount; i++)
            {
                string prefix = "Bldg_Z" + z + "_" + i;
                var t = transform.Find(prefix);
                if (t != null && t.localScale.y > 3f && t.localPosition.x > 2f)
                {
                    targetBldg = t;
                    break;
                }
            }
            if (targetBldg == null) return;

            float halfD = targetBldg.localScale.z / 2f;
            float halfH = targetBldg.localScale.y / 2f;
            float halfW = targetBldg.localScale.x / 2f;

            float bbW = Mathf.Min(2f, halfW * 0.8f);
            float bbH = Mathf.Min(3f, halfH * 0.6f);
            float billboardZ = halfD + 0.05f;
            float bbYoffset = halfH * 0.1f;

            // Background quad (load texture if available, else solid dark)
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "BillboardBG";
            bg.transform.SetParent(targetBldg);
            bg.transform.localPosition = new Vector3(0f, bbYoffset, billboardZ - 0.01f);
            bg.transform.localScale = new Vector3(bbW, bbH, 1f);
            Destroy(bg.GetComponent<Collider>());

            var bgMat = new Material(FindLitShader());
            var billboardTex = Resources.Load<Texture2D>("Sprites/billboard_tex");
            if (billboardTex != null)
            {
                bgMat.mainTexture = billboardTex;
                bgMat.color = new Color(1f, 1f, 1f, 0.85f);
            }
            else
            {
                bgMat.color = new Color(0.01f, 0.005f, 0.03f);
            }
            bg.GetComponent<MeshRenderer>().material = bgMat;

            // Neon border frame (thin cyan glow around billboard)
            float borderThick = 0.04f;
            CreateBorderStrip("BBTop", targetBldg,
                new Vector3(0f, bbYoffset + bbH / 2f, billboardZ),
                new Vector3(bbW + borderThick, borderThick, borderThick), _neonCyanMat);
            CreateBorderStrip("BBBot", targetBldg,
                new Vector3(0f, bbYoffset - bbH / 2f, billboardZ),
                new Vector3(bbW + borderThick, borderThick, borderThick), _neonCyanMat);
            CreateBorderStrip("BBLeft", targetBldg,
                new Vector3(-bbW / 2f, bbYoffset, billboardZ),
                new Vector3(borderThick, bbH + borderThick, borderThick), _neonCyanMat);
            CreateBorderStrip("BBRight", targetBldg,
                new Vector3(bbW / 2f, bbYoffset, billboardZ),
                new Vector3(borderThick, bbH + borderThick, borderThick), _neonCyanMat);

            // WorldSpace Canvas for scrolling TMP text
            var canvasGO = new GameObject("BillboardCanvas");
            canvasGO.transform.SetParent(targetBldg);
            canvasGO.transform.localPosition = new Vector3(0f, bbYoffset, billboardZ - 0.005f);
            canvasGO.transform.localRotation = Quaternion.identity;
            canvasGO.transform.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var textGO = new GameObject("BillboardText");
            textGO.transform.SetParent(canvasGO.transform);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "CHROMA VALE  ::  NEON DISTRICT  ::  FLOW THE PIPES  ::  ";
            tmp.fontSize = 48f;
            tmp.color = ChromaPalette.NeonCyan;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;

            var rt = textGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1200f, 200f);
            rt.anchoredPosition = Vector2.zero;

            StartCoroutine(ScrollBillboardText(tmp));
        }

        private IEnumerator ScrollBillboardText(TMP_Text text)
        {
            string msg = "CHROMA VALE  ::  NEON DISTRICT  ::  FLOW THE PIPES  ::  ";
            while (text != null)
            {
                for (int i = 0; i < msg.Length; i++)
                {
                    text.text = msg.Substring(i) + msg.Substring(0, i);
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        // ── Blinking windows coroutine ────────────────────────────────────

        /// <summary>
        /// Periodically flickers random lit windows off then back on,
        /// creating a lived-in, animated city feel.
        /// </summary>
        private IEnumerator BlinkWindowsCoroutine()
        {
            while (true)
            {
                // Wait 2-3 seconds between flicker batches
                yield return new WaitForSeconds(Random.Range(2f, 3f));

                if (_litWindows.Count == 0) continue;

                // Pick 2-5 random lit windows to flicker
                int batchSize = Mathf.Min(Random.Range(2, 6), _litWindows.Count);
                var toFlicker = new List<GameObject>(batchSize);

                for (int i = 0; i < batchSize; i++)
                {
                    int idx = Random.Range(0, _litWindows.Count);
                    var w = _litWindows[idx];
                    if (w != null && !toFlicker.Contains(w))
                        toFlicker.Add(w);
                }

                foreach (var w in toFlicker)
                {
                    if (w == null) continue;
                    var r = w.GetComponent<MeshRenderer>();
                    if (r == null) continue;

                    // Turn window off
                    r.sharedMaterial = _bodyMatFar;
                    _litWindows.Remove(w);

                    // Re-light after a short random delay (100-500ms)
                    StartCoroutine(RestoreWindow(w, Random.Range(0.1f, 0.5f)));
                }
            }
        }

        private IEnumerator RestoreWindow(GameObject w, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (w == null) yield break;

            var r = w.GetComponent<MeshRenderer>();
            if (r == null) yield break;

            // Restore a neon material (any random color)
            r.sharedMaterial = new Material(PickRandomNeonMat());
            _litWindows.Add(w);
        }
    }
}
