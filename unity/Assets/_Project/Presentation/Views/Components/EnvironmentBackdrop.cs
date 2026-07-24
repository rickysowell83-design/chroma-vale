using System.Collections;
using UnityEngine;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class EnvironmentBackdrop : MonoBehaviour
    {
        private bool _built;

        // Shared building body materials (3 tiers)
        private Material _bodyMat1;
        private Material _bodyMat2;
        private Material _bodyMat3;

        // Shared neon emissive materials (one per color)
        private Material _neonCyanMat;
        private Material _neonMagentaMat;
        private Material _neonPurpleMat;

        private static readonly int _emissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Builds the 3D cyberpunk skyline behind the puzzle grid.
        /// Idempotent: calling twice destroys previous children first.
        /// </summary>
        public void Build()
        {
            if (_built)
                ClearChildren();

            var prevState = Random.state;
            Random.InitState(429);

            CreateSharedMaterials();

            // Three building rows at progressively deeper Z
            // Near row:  z=8,  tallest detail, heights 3-8
            BuildBuildingRow(8,  10, 3f,  8f,  1.5f, 4f,  1.2f, 2f,  _bodyMat3);
            // Mid row:   z=15, medium scale, heights 5-14
            BuildBuildingRow(15, 14, 5f,  14f, 1.5f, 5f,  0.8f, 1.5f, _bodyMat2);
            // Far row:   z=25, silhouettes, heights 10-25
            BuildBuildingRow(25, 18, 10f, 25f, 2f,  6f,  0.5f, 1f,  _bodyMat1);

            // Neon windows (~40% lit, capped at ~150)
            BuildNeonWindows(8,  10, 2, 5);
            BuildNeonWindows(15, 14, 1, 4);
            BuildNeonWindows(25, 18, 0, 3);

            // Neon accents
            BuildAntennaMasts(8, 10);
            BuildNeonSignFrame(8, 10);
            BuildHorizonStrip();

            // Billboard with scrolling TMP text on a near-row building
            CreateBillboard(8, 10);

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
            // Building bodies: near-black desaturated purples
            _bodyMat1 = CreateLitMaterial(new Color(0.02f, 0.015f, 0.05f), 0.4f, 0.3f);
            _bodyMat2 = CreateLitMaterial(new Color(0.05f, 0.03f, 0.10f), 0.5f, 0.35f);
            _bodyMat3 = CreateLitMaterial(new Color(0.08f, 0.04f, 0.14f), 0.6f, 0.4f);

            // Neon emissive (HDR: 3x palette values for bloom)
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

        // ── Building rows ─────────────────────────────────────────────────

        private void BuildBuildingRow(float z, int count, float minH, float maxH,
            float minW, float maxW, float minD, float maxD, Material bodyMat)
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
                float d = Random.Range(minD, maxD);

                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Bldg_Z" + z + "_" + i;
                b.transform.SetParent(transform);
                b.transform.localPosition = new Vector3(x, baseY + h / 2f, z);
                b.transform.localScale = new Vector3(w, h, d);
                Destroy(b.GetComponent<Collider>());
                b.GetComponent<MeshRenderer>().material = bodyMat;
            }
        }

        // ── Neon windows ──────────────────────────────────────────────────

        private void BuildNeonWindows(float rowZ, int rowCount, int minWindows, int maxWindows)
        {
            int totalWindows = 0;
            const int maxTotal = 150;

            for (int i = 0; i < rowCount && totalWindows < maxTotal; i++)
            {
                string prefix = "Bldg_Z" + rowZ + "_" + i;
                var bldg = transform.Find(prefix);
                if (bldg == null) continue;

                float halfH = bldg.localScale.y / 2f;
                float halfW = bldg.localScale.x / 2f;
                float halfD = bldg.localScale.z / 2f;

                int windows = Mathf.Min(Random.Range(minWindows, maxWindows + 1), maxTotal - totalWindows);
                for (int w = 0; w < windows; w++)
                {
                    bool lit = Random.value < 0.4f;
                    Material neonMat = lit ? PickRandomNeonMat() : null;

                    float winX = Random.Range(-halfW + 0.15f, halfW - 0.15f);
                    float winY = Random.Range(-halfH + 0.2f, halfH - 0.2f);

                    var win = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    win.name = "Win_Z" + rowZ + "_" + i + "_" + w;
                    win.transform.SetParent(bldg);
                    win.transform.localPosition = new Vector3(winX, winY, halfD + 0.01f);
                    win.transform.localScale = new Vector3(0.12f, 0.08f, 1f);
                    Destroy(win.GetComponent<Collider>());

                    var renderer = win.GetComponent<MeshRenderer>();
                    if (lit)
                        renderer.material = neonMat;
                    else
                        renderer.material = _bodyMat1;

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

        // ── Antenna masts ─────────────────────────────────────────────────

        private void BuildAntennaMasts(float rowZ, int rowCount)
        {
            int mastsPlaced = 0;
            for (int i = 0; i < rowCount && mastsPlaced < 3; i++)
            {
                string prefix = "Bldg_Z" + rowZ + "_" + i;
                var bldg = transform.Find(prefix);
                if (bldg == null) continue;
                if (bldg.localScale.y < 5f) continue;
                if (Random.value > 0.3f) continue;

                float halfH = bldg.localScale.y / 2f;

                // Mast: thin stretched cylinder
                var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mast.name = "Antenna_Z" + rowZ + "_" + i;
                mast.transform.SetParent(bldg);
                mast.transform.localPosition = new Vector3(
                    Random.Range(-0.3f, 0.3f), halfH + 1f, Random.Range(-0.3f, 0.3f));
                mast.transform.localScale = new Vector3(0.04f, Random.Range(0.8f, 1.5f), 0.04f);
                Destroy(mast.GetComponent<Collider>());
                mast.GetComponent<MeshRenderer>().material = _bodyMat1;

                // Beacon sphere on top
                var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                beacon.name = "Beacon_Z" + rowZ + "_" + i;
                beacon.transform.SetParent(mast.transform);
                beacon.transform.localPosition = new Vector3(0f, 1f, 0f);
                beacon.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                Destroy(beacon.GetComponent<Collider>());

                // Pick neon color that alternates for variety
                Material beaconMat = (mastsPlaced % 2 == 0) ? _neonCyanMat : _neonMagentaMat;
                beacon.GetComponent<MeshRenderer>().material = beaconMat;

                mastsPlaced++;
            }
        }

        // ── Neon sign frame ───────────────────────────────────────────────

        private void BuildNeonSignFrame(float rowZ, int rowCount)
        {
            for (int i = 0; i < rowCount; i++)
            {
                string prefix = "Bldg_Z" + rowZ + "_" + i;
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
                fill.GetComponent<MeshRenderer>().material = _bodyMat1;
                fill.GetComponent<MeshRenderer>().material.color = new Color(0.01f, 0.005f, 0.03f);

                break; // Only one sign frame
            }
        }

        private void CreateBorderStrip(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
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
            horizon.transform.localPosition = new Vector3(0f, -3f, 28f);
            horizon.transform.localScale = new Vector3(30f, 0.05f, 1f);
            Destroy(horizon.GetComponent<Collider>());
            horizon.GetComponent<MeshRenderer>().material = _neonCyanMat;
        }

        // ── Billboard ─────────────────────────────────────────────────────

        private void CreateBillboard(float rowZ, int rowCount)
        {
            Transform targetBldg = null;
            for (int i = 0; i < rowCount; i++)
            {
                string prefix = "Bldg_Z" + rowZ + "_" + i;
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
    }
}
