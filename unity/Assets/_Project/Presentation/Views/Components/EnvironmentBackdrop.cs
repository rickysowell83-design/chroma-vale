using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaVale.Presentation.Views.Components
{
    public class EnvironmentBackdrop : MonoBehaviour
    {
        public void Build()
        {
            var mat = new Material(Shader.Find("Sprites/Default"));

            // Dark gradient background
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "CyberpunkBG";
            bg.transform.SetParent(transform);
            bg.transform.localPosition = new Vector3(0f, 0f, 1f);
            bg.transform.localScale = new Vector3(20f, 12f, 1f);
            Destroy(bg.GetComponent<MeshCollider>());
            bg.GetComponent<MeshRenderer>().material = mat;
            bg.GetComponent<MeshRenderer>().material.color = new Color(0.02f, 0.01f, 0.06f);
            bg.GetComponent<MeshRenderer>().sortingOrder = -20;

            // City skyline
            CreateBuildingLayer(-18, 8, 5, new Color(0.08f, 0.04f, 0.18f), mat);
            CreateBuildingLayer(-17, 15, 3.5f, new Color(0.12f, 0.05f, 0.22f), mat);
            CreateBuildingLayer(-16, 22, 2.5f, new Color(0.18f, 0.06f, 0.28f), mat);

            // Cyberpunk billboard sign
            CreateBillboard(mat);

            // Neon horizon line
            var horizon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            horizon.name = "Horizon";
            horizon.transform.SetParent(transform);
            horizon.transform.localPosition = new Vector3(0f, -2.5f, 1f);
            horizon.transform.localScale = new Vector3(20f, 0.04f, 1f);
            Destroy(horizon.GetComponent<MeshCollider>());
            horizon.GetComponent<MeshRenderer>().material = mat;
            horizon.GetComponent<MeshRenderer>().material.color = ChromaPalette.NeonCyan * 0.3f;
            horizon.GetComponent<MeshRenderer>().sortingOrder = -15;
        }

        private void CreateBuildingLayer(int order, int count, float maxHeight, Color color, Material mat)
        {
            float startX = -9f;
            float endX = 9f;
            float spacing = (endX - startX) / count;
            float baseY = -2.5f;

            for (int i = 0; i < count; i++)
            {
                float x = startX + spacing * i + Random.Range(-0.3f, 0.3f);
                float h = Random.Range(1f, maxHeight);
                float w = Random.Range(0.4f, 1.2f);

                var b = GameObject.CreatePrimitive(PrimitiveType.Quad);
                b.name = "Bldg_" + order + "_" + i;
                b.transform.SetParent(transform);
                b.transform.localPosition = new Vector3(x, baseY + h / 2f, 1f);
                b.transform.localScale = new Vector3(w, h, 1f);
                Destroy(b.GetComponent<MeshCollider>());
                b.GetComponent<MeshRenderer>().material = mat;
                b.GetComponent<MeshRenderer>().material.color = color;
                b.GetComponent<MeshRenderer>().sortingOrder = order;

                // Neon window dots
                int windows = Random.Range(1, 5);
                for (int ww = 0; ww < windows; ww++)
                {
                    var win = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    win.name = "Win_" + order + "_" + i + "_" + ww;
                    win.transform.SetParent(b.transform);
                    win.transform.localPosition = new Vector3(
                        Random.Range(-0.3f, 0.3f),
                        Random.Range(-h / 2f + 0.2f, h / 2f - 0.2f), 0f);
                    win.transform.localScale = new Vector3(0.08f, 0.06f, 1f);
                    Destroy(win.GetComponent<MeshCollider>());
                    win.GetComponent<MeshRenderer>().material = mat;
                    Color[] neonColors = {
                        ChromaPalette.NeonCyan * 0.7f,
                        ChromaPalette.NeonMagenta * 0.7f,
                        ChromaPalette.NeonYellow * 0.5f,
                        ChromaPalette.NeonPurple * 0.6f
                    };
                    win.GetComponent<MeshRenderer>().material.color = neonColors[Random.Range(0, neonColors.Length)];
                    win.GetComponent<MeshRenderer>().sortingOrder = order + 1;
                }
            }
        }

        private void CreateBillboard(Material mat)
        {
            // Load the billboard texture from Resources
            var billboardTex = Resources.Load<Texture2D>("Sprites/billboard_tex");

            // Billboard frame with the generated texture
            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "Billboard";
            frame.transform.SetParent(transform);
            frame.transform.localPosition = new Vector3(5.5f, 1.2f, 0.5f);
            frame.transform.localScale = new Vector3(2.0f, 3.5f, 1f);
            Destroy(frame.GetComponent<MeshCollider>());
            var frameRenderer = frame.GetComponent<MeshRenderer>();
            frameRenderer.material = new Material(Shader.Find("Sprites/Default"));
            frameRenderer.sortingOrder = -14;
            if (billboardTex != null)
            {
                frameRenderer.material.mainTexture = billboardTex;
                frameRenderer.material.color = new Color(1f, 1f, 1f, 0.85f);
            }
            else
            {
                frameRenderer.material.color = new Color(0.01f, 0.005f, 0.03f);
            }

            // Neon border — cyan glow around the billboard
            var border = GameObject.CreatePrimitive(PrimitiveType.Quad);
            border.name = "BillboardBorder";
            border.transform.SetParent(frame.transform);
            border.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            border.transform.localScale = new Vector3(1.08f, 1.06f, 1f);
            Destroy(border.GetComponent<MeshCollider>());
            border.GetComponent<MeshRenderer>().material = mat;
            border.GetComponent<MeshRenderer>().material.color = ChromaPalette.NeonCyan * 0.6f;
            border.GetComponent<MeshRenderer>().sortingOrder = -14;

            // Pulse the border
            StartCoroutine(PulseBorder(border));
        }

        private IEnumerator PulseBorder(GameObject border)
        {
            var renderer = border?.GetComponent<MeshRenderer>();
            while (renderer != null)
            {
                float t = 0f;
                while (t < 2f && renderer != null)
                {
                    t += Time.deltaTime;
                    float pulse = 0.4f + Mathf.Sin(t * 2.5f) * 0.4f;
                    renderer.material.color = ChromaPalette.NeonCyan * pulse;
                    yield return null;
                }
            }
        }
    }
}
