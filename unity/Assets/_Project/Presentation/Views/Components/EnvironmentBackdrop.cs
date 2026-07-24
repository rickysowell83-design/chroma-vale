using System.Collections;
using UnityEngine;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class EnvironmentBackdrop : MonoBehaviour
    {
        private TextMeshProUGUI _billboardText;

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
            // Billboard frame
            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "Billboard";
            frame.transform.SetParent(transform);
            frame.transform.localPosition = new Vector3(6f, -0.5f, 1f);
            frame.transform.localScale = new Vector3(1.8f, 2.5f, 1f);
            Destroy(frame.GetComponent<MeshCollider>());
            frame.GetComponent<MeshRenderer>().material = mat;
            frame.GetComponent<MeshRenderer>().material.color = new Color(0.02f, 0.01f, 0.04f);
            frame.GetComponent<MeshRenderer>().sortingOrder = -14;

            // Neon border
            var border = GameObject.CreatePrimitive(PrimitiveType.Quad);
            border.name = "BillboardBorder";
            border.transform.SetParent(frame.transform);
            border.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            border.transform.localScale = new Vector3(1.05f, 1.05f, 1f);
            Destroy(border.GetComponent<MeshCollider>());
            border.GetComponent<MeshRenderer>().material = mat;
            border.GetComponent<MeshRenderer>().material.color = ChromaPalette.NeonCyan * 0.6f;
            border.GetComponent<MeshRenderer>().sortingOrder = -14;

            // Scrolling text
            var textCanvas = new GameObject("BillboardText");
            textCanvas.transform.SetParent(frame.transform);
            textCanvas.transform.localPosition = Vector3.zero;
            textCanvas.transform.localScale = new Vector3(0.01f, 0.01f, 1f);
            var canvas = textCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -13;
            var crt = textCanvas.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(160f, 220f);

            var textGo = new GameObject("ScrollingLabel");
            textGo.transform.SetParent(textCanvas.transform);
            _billboardText = textGo.AddComponent<TextMeshProUGUI>();
            _billboardText.text = "CHROMA VALE /// PIPELINE ONLINE /// NEON FLOW ACTIVE ///";
            _billboardText.fontSize = 8;
            _billboardText.alignment = TextAlignmentOptions.Center;
            _billboardText.color = ChromaPalette.NeonCyan;
            _billboardText.rectTransform.sizeDelta = new Vector2(160f, 220f);

            // Start scrolling
            StartCoroutine(ScrollBillboard());
        }

        private IEnumerator ScrollBillboard()
        {
            string baseText = "CHROMA VALE /// PIPELINE ONLINE /// NEON FLOW ACTIVE /// SYSTEM NOMINAL /// ";
            int offset = 0;
            while (_billboardText != null)
            {
                _billboardText.text = baseText.Substring(offset) + baseText.Substring(0, offset);
                offset = (offset + 1) % baseText.Length;
                yield return new WaitForSeconds(0.15f);
            }
        }
    }
}
