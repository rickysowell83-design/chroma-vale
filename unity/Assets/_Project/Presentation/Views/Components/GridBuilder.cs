using System;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    public class GridBuilder : MonoBehaviour
    {
        private LevelData _level;
        private GridBoard _board;
        private float _tileSize;
        private TileVisual[,] _renderers;

        /// <summary>
        /// Build the grid and return the array of TileVisuals.
        /// </summary>
        /// <param name="level">Level data with sources, targets, obstacles, flow gates.</param>
        /// <param name="board">Grid board with cell state.</param>
        /// <param name="tileSize">World-space size of each tile.</param>
        /// <param name="view">The puzzle board view for tile click wiring.</param>
        /// <returns>2D array of TileVisuals for each cell.</returns>
        public TileVisual[,] Build(LevelData level, GridBoard board, float tileSize, PuzzleBoardView view)
        {
            _level = level;
            _board = board;
            _tileSize = tileSize;

            _renderers = new TileVisual[board.Width, board.Height];
            var off = new Vector3(-board.Width * tileSize / 2f, -board.Height * tileSize / 2f, 0);

            // Load PCB texture material once, create per-tile copies with correct UV offsets
            var pcbMaster = Resources.Load<Material>("Materials/PCB_Board");

            for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board.GetCell(x, y);
                var worldPos = new Vector3(x * tileSize + off.x, y * tileSize + off.y, 0);
                var tv = TileVisual.Create(transform, worldPos, tileSize, "Tile_" + x + "_" + y);

                _renderers[x, y] = tv;

                // Per-tile PCB material copy with correct UV offset
                if (pcbMaster != null)
                {
                    var tileMat = new Material(pcbMaster);
                    float uScale = 1f / board.Width;
                    float vScale = 1f / board.Height;
                    // V is inverted: row 0 = top of texture, row H-1 = bottom
                    float vOff = (board.Height - 1 - y) * vScale;
                    tileMat.SetTextureScale("_BaseMap", new Vector2(uScale, vScale));
                    tileMat.SetTextureOffset("_BaseMap", new Vector2(x * uScale, vOff));
                    tv.SetSlabMaterial(tileMat);
                }

                tv.Color = ChromaPalette.DarkTile;

                // ── Burnt-out microchip at obstacle cells ──
                if (cell.Type == CellType.Obstacle)
                    BuildBurntMicrochip(tv.transform, tileSize);

                // Click handler
                tv.gameObject.AddComponent<TileClickHandler>().Init(x, y, view);
            }

            SetupCamera();
            return _renderers;
        }

        public void Clear()
        {
            // Destroy all child tiles AND the background plane
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (child != null) DestroyImmediate(child);
            }
            _renderers = null;
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = false;
                cam.fieldOfView = 32f;
                float halfSpan = Mathf.Max(_board.Width, _board.Height) * _tileSize / 2f + 1.5f;
                float dist = halfSpan / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                dist *= 1.06f;
                float tiltDeg = 38f;
                float tiltRad = tiltDeg * Mathf.Deg2Rad;
                Vector3 lookTarget = new Vector3(0f, -0.8f, 0f);
                Vector3 camPos = lookTarget + new Vector3(0f, Mathf.Sin(tiltRad) * dist, -Mathf.Cos(tiltRad) * dist);
                cam.transform.position = camPos;
                cam.transform.rotation = Quaternion.LookRotation(lookTarget - camPos, Vector3.up);
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 100f;
                cam.backgroundColor = ChromaPalette.DarkBG;
                cam.clearFlags = CameraClearFlags.SolidColor;
                if (cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                    cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            FitBackdrop();
            }
        }

        private void FitBackdrop()
        {
            var backdrop = GameObject.Find("CyberpunkBackdrop");
            if (backdrop == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            // Find world-space Y bounds of near-zone buildings (z <= 15)
            float worldMinY = float.MaxValue;
            float worldMaxY = float.MinValue;
            foreach (Transform child in backdrop.transform)
            {
                if (child.position.z <= 15f)
                {
                    float halfH = child.lossyScale.y * 0.5f;
                    float top = child.position.y + halfH;
                    float bottom = child.position.y - halfH;
                    worldMinY = Mathf.Min(worldMinY, bottom);
                    worldMaxY = Mathf.Max(worldMaxY, top);
                }
            }
            if (worldMinY > worldMaxY) return; // no near-zone buildings

            // Compute the visible world-Y span at z=10 for the skyline region
            float nearZ = 10f;
            float distToNear = (nearZ - cam.transform.position.z) / cam.transform.forward.z;
            float vpTop = 0.50f;
            float vpBottom = 0.02f;
            float targetTop = cam.ViewportToWorldPoint(new Vector3(0.5f, vpTop, distToNear)).y;
            float targetBottom = cam.ViewportToWorldPoint(new Vector3(0.5f, vpBottom, distToNear)).y;

            float currentSpan = worldMaxY - worldMinY;
            float currentCenter = (worldMaxY + worldMinY) * 0.5f;
            float targetSpan = targetTop - targetBottom;
            float targetCenter = (targetTop + targetBottom) * 0.5f;

            float scaleFactor = Mathf.Min(targetSpan / currentSpan, 1.0f);

            // Capture old backdrop state
            float oldScaleY = backdrop.transform.localScale.y;
            float oldPosY = backdrop.transform.position.y;

            // Compute local-space center (invariant under our scale change)
            float localCenterY = (currentCenter - oldPosY) / oldScaleY;

            // Apply Y-only scale to backdrop
            var s = backdrop.transform.localScale;
            float newScaleY = oldScaleY * scaleFactor;
            backdrop.transform.localScale = new Vector3(s.x, newScaleY, s.z);

            // Reposition so the scaled building center aligns with target center
            float newPosY = targetCenter - localCenterY * newScaleY;
            backdrop.transform.position = new Vector3(backdrop.transform.position.x, newPosY, backdrop.transform.position.z);

            Debug.Log($"[GridBuilder] FitBackdrop: scaleY={scaleFactor:F3} newY={newPosY:F2} " +
                      $"(span={currentSpan:F2}->{targetSpan:F2}, center={currentCenter:F2}->{targetCenter:F2})");
        }


        private Color GetPipeColor(int ci) => ci switch
        {
            0 => ChromaPalette.NeonCyan,
            1 => ChromaPalette.NeonMagenta,
            2 => ChromaPalette.NeonYellow,
            6 => ChromaPalette.NeonPurple,
            7 => ChromaPalette.NeonGreen,
            8 => ChromaPalette.NeonOrange,
            9 => new Color(0.4f, 0.25f, 0.1f), // Brown
            _ => ChromaPalette.NeonCyan
        };

        private void BuildBurntMicrochip(Transform parent, float ts)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // Chip body
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ChipBody";
            DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, 0f, -0.15f);
            body.transform.localScale = new Vector3(ts * 0.45f, ts * 0.45f, 0.08f);
            var chipMat = new Material(shader) { color = new Color(0.08f, 0.04f, 0.04f) };
            chipMat.SetFloat("_Metallic", 0.5f);
            chipMat.SetFloat("_Smoothness", 0.3f);
            body.GetComponent<MeshRenderer>().sharedMaterial = chipMat;

            // Silver pins
            var pinMat = new Material(shader) { color = new Color(0.75f, 0.75f, 0.78f) };
            pinMat.SetFloat("_Metallic", 0.9f);
            pinMat.SetFloat("_Smoothness", 0.8f);
            float po = ts * 0.12f;
            float pr = ts * 0.015f;
            float pl = ts * 0.08f;
            Vector3[] pins = { new(-po,-po), new(po,-po), new(-po,po), new(po,po) };
            foreach (var p in pins)
            {
                var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pin.name = "Pin";
                DestroyImmediate(pin.GetComponent<Collider>());
                pin.transform.SetParent(parent, false);
                pin.transform.localPosition = new Vector3(p.x, p.y, -0.15f - pl * 0.5f);
                pin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pin.transform.localScale = new Vector3(pr, pl * 0.5f, pr);
                pin.GetComponent<MeshRenderer>().sharedMaterial = pinMat;
            }

            // Glowing scorch mark
            var scorch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            scorch.name = "ScorchMark";
            DestroyImmediate(scorch.GetComponent<Collider>());
            scorch.transform.SetParent(parent, false);
            scorch.transform.localPosition = new Vector3(ts * 0.04f, ts * 0.04f, -0.18f);
            scorch.transform.localScale = Vector3.one * (ts * 0.12f);
            var scorchMat = new Material(shader) { color = new Color(0.15f, 0.02f, 0.02f) };
            scorchMat.SetFloat("_Metallic", 0.1f);
            scorchMat.SetFloat("_Smoothness", 0.1f);
            scorchMat.EnableKeyword("_EMISSION");
            scorchMat.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.05f) * 0.8f);
            scorchMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            scorch.GetComponent<MeshRenderer>().sharedMaterial = scorchMat;
        }
    }
}
