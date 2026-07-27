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
        /// <param name="level">Level data with sources, targets, obstacles, signal gates.</param>
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

            // ── Create source/target indicators ──
            foreach (var src in level.Sources)
            {
                var tv = _renderers[src.X, src.Y];
                if (tv != null)
                {
                    var srcColor = GetPipeColor(src.ColorIndex);
                    tv.SetIndicator(TileIndicator.SourceDot, srcColor);
                }
            }

            foreach (var tgt in level.Targets)
            {
                var tv = _renderers[tgt.X, tgt.Y];
                if (tv != null)
                {
                    var tgtColor = GetPipeColor(tgt.ColorIndex);
                    tv.SetIndicator(TileIndicator.TargetRing, tgtColor);
                }
            }

            // ── Render ghost traces (pre-existing copper on the board) ──
            if (level.GhostTraces != null)
            {
                foreach (var gt in level.GhostTraces)
                {
                    var tv = _renderers[gt.X, gt.Y];
                    if (tv != null)
                    {
                        tv.SetShape(gt.Shape, gt.Rotation);
                        tv.Color = ChromaPalette.CopperOxidized; // v3 oxidized copper #5C3A1E
                        tv.EmissionIntensity = 0.08f; // Very subtle, barely visible ghost
                    }
                }
            }

            // ── Add corner mounting holes with copper rings ──
            AddMountingHoles(board, tileSize, off);

                        // ── Add silkscreen labels at corners ──
            AddSilkscreenLabels(board, tileSize);

            // ── Add ghost component outlines on random empty tiles ──
            AddGhostComponents(board, tileSize);
            SetupCamera();
            AddPcbWings(board, tileSize);
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
                // ── Flat top-down orthographic PCB view ──
                // Board is in the XY plane at z=0. Camera looks straight down.
                cam.orthographic = true;
                cam.orthographicSize = ComputeOrthoSize();
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 100f;
                cam.backgroundColor = ChromaPalette.DarkBG;
                cam.clearFlags = CameraClearFlags.SolidColor;
                if (cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                    cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
                DisableBackdrop();
            }
        }

        private float ComputeOrthoSize()
        {
            float boardWidth = _board.Width * _tileSize;
            float boardHeight = _board.Height * _tileSize;
            float pad = _tileSize * 0.8f;

            // orthographicSize = half the vertical world units visible
            float heightSize = (boardHeight / 2f) + pad;
            float widthSize = ((boardWidth / 2f) + pad) / (Screen.width > 0 ? (float)Screen.width / Screen.height : 1.7778f);

            // Use whichever is larger so the board fits fully in both dimensions
            return Mathf.Max(heightSize, widthSize);
        }

        private void DisableBackdrop()
        {
            var backdrop = GameObject.Find("CyberpunkBackdrop");
            if (backdrop != null) backdrop.SetActive(false);
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


        private void AddSilkscreenLabels(GridBoard board, float tileSize)
        {
            // Board-level labels along edges (larger text)
            float halfW = board.Width * tileSize / 2f;
            float halfH = board.Height * tileSize / 2f;

            // "CHROMA-VALE REV 3" along bottom edge
            {
                var go = new GameObject("Silkscreen_BoardLabel_Bottom");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, -halfH - tileSize * 0.35f, -0.04f);
                var tmp = go.AddComponent<TMPro.TextMeshPro>();
                tmp.text = "CHROMA-VALE REV 3";
                tmp.fontSize = 3.5f;
                tmp.color = ChromaPalette.SilkscreenLabel;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontStyle = TMPro.FontStyles.Normal;
            }

            // "LEVEL 1" along top edge
            {
                var go = new GameObject("Silkscreen_BoardLabel_Top");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, halfH + tileSize * 0.35f, -0.04f);
                var tmp = go.AddComponent<TMPro.TextMeshPro>();
                tmp.text = "LEVEL 1";
                tmp.fontSize = 3.5f;
                tmp.color = ChromaPalette.SilkscreenLabel;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontStyle = TMPro.FontStyles.Normal;
            }

            // Reference designators at all four corners
            string[] labels = { "R14", "U3", "C7", "J1" };
            Vector3[] labelPositions = {
                new(-halfW - tileSize * 0.1f, -halfH - tileSize * 0.1f, -0.04f),
                new( halfW + tileSize * 0.1f, -halfH - tileSize * 0.1f, -0.04f),
                new(-halfW - tileSize * 0.1f,  halfH + tileSize * 0.1f, -0.04f),
                new( halfW + tileSize * 0.1f,  halfH + tileSize * 0.1f, -0.04f)
            };

            for (int i = 0; i < labels.Length; i++)
            {
                var go = new GameObject("Silkscreen_" + labels[i]);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = labelPositions[i];

                var tmp = go.AddComponent<TMPro.TextMeshPro>();
                tmp.text = labels[i];
                tmp.fontSize = 2.5f;
                tmp.color = ChromaPalette.SilkscreenLabel;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontStyle = TMPro.FontStyles.Normal;
            }
        }

        private void AddGhostComponents(GridBoard board, float tileSize)
        {
            // Add 2-3 faint ghost IC outlines on random empty tiles
            // Use Sprites/Default on thin Quads at 8-12% opacity
            Shader quadShader = Shader.Find("Sprites/Default");
            if (quadShader == null) return;

            // Collect ALL empty cells (not just interior)
            var candidates = new System.Collections.Generic.List<(int x, int y)>();
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    var cell = board.GetCell(x, y);
                    if (cell.Type == CellType.Empty)
                        candidates.Add((x, y));
                }

            if (candidates.Count == 0)
            {
                Debug.Log("[GridBuilder] AddGhostComponents: no empty cells found, skipping ghost ICs.");
                return;
            }

            // Use deterministic seed for reproducibility
            var rng = new System.Random(42 + board.Width * 100 + board.Height);

            // Shuffle candidates
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }

            int ghostCount = Mathf.Min(rng.Next(2, 4), candidates.Count); // 2-3 ghosts
            int placed = 0;

            foreach (var (cx, cy) in candidates)
            {
                if (placed >= ghostCount) break;

                var tile = _renderers[cx, cy];
                if (tile == null) continue;

                // Vary ghost size slightly for variety
                float gw = tileSize * (0.25f + (float)rng.NextDouble() * 0.15f);
                float gh = tileSize * (0.20f + (float)rng.NextDouble() * 0.10f);

                // Ghost IC body outline (thin rectangle)
                var ghost = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ghost.name = "GhostIC_" + cx + "_" + cy;
                DestroyImmediate(ghost.GetComponent<MeshCollider>());
                ghost.transform.SetParent(tile.transform, false);
                ghost.transform.localPosition = new Vector3(0f, 0f, -0.08f);
                ghost.transform.localScale = new Vector3(gw, gh, 1f);
                var rend = ghost.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    var mat = new Material(quadShader);
                    mat.color = ChromaPalette.GhostComponent;
                    rend.sharedMaterial = mat;
                    rend.sortingOrder = -5;
                }

                // Small pin pads on ghost IC (4 tiny dots at corners)
                Vector2[] pinPositions = {
                    new(-gw * 0.3f, -gh * 0.3f),
                    new( gw * 0.3f, -gh * 0.3f),
                    new(-gw * 0.3f,  gh * 0.3f),
                    new( gw * 0.3f,  gh * 0.3f)
                };
                foreach (var pp in pinPositions)
                {
                    var pin = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    pin.name = "GhostPin";
                    DestroyImmediate(pin.GetComponent<MeshCollider>());
                    pin.transform.SetParent(ghost.transform, false);
                    pin.transform.localPosition = new Vector3(pp.x, pp.y, -0.01f);
                    pin.transform.localScale = new Vector3(tileSize * 0.04f, tileSize * 0.04f, 1f);
                    var pinRend = pin.GetComponent<MeshRenderer>();
                    if (pinRend != null)
                    {
                        var pinMat = new Material(quadShader);
                        pinMat.color = new Color(0.7f, 0.7f, 0.7f, 0.08f);
                        pinRend.sharedMaterial = pinMat;
                        pinRend.sortingOrder = -5;
                    }
                }

                placed++;
            }

            Debug.Log($"[GridBuilder] AddGhostComponents: placed {placed} ghost ICs out of {candidates.Count} empty cells.");
        }



        private void AddMountingHoles(GridBoard board, float tileSize, Vector3 off)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // Corner positions (slightly outside the grid)
            float padX = board.Width * tileSize / 2f + tileSize * 0.4f;
            float padY = board.Height * tileSize / 2f + tileSize * 0.4f;
            Vector3[] corners = {
                new(-padX, -padY, -0.06f),
                new( padX, -padY, -0.06f),
                new(-padX,  padY, -0.06f),
                new( padX,  padY, -0.06f)
            };

            // Dark mounting hole color from spec (0.02, 0.02, 0.02)
            Color holeColor = new(0.02f, 0.02f, 0.02f);

            foreach (var pos in corners)
            {
                // Thin dark ring — no emission, no gold, just a dark circle
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "MountingHole";
                DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = pos;
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                float ringR = tileSize * 0.15f;
                ring.transform.localScale = new Vector3(ringR * 2f, 0.015f, ringR * 2f);
                var rend = ring.GetComponent<MeshRenderer>();
                if (rend != null && shader != null)
                {
                    var mat = new Material(shader) { color = holeColor };
                    mat.SetFloat("_Metallic", 0.05f);
                    mat.SetFloat("_Smoothness", 0.1f);
                    rend.sharedMaterial = mat;
                }

                // Small inner void (even darker center)
                var voidGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                voidGo.name = "MountingHoleVoid";
                DestroyImmediate(voidGo.GetComponent<Collider>());
                voidGo.transform.SetParent(transform, false);
                voidGo.transform.localPosition = pos - new Vector3(0f, 0f, 0.003f);
                voidGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                float voidR = tileSize * 0.06f;
                voidGo.transform.localScale = new Vector3(voidR * 2f, 0.018f, voidR * 2f);
                var voidRend = voidGo.GetComponent<MeshRenderer>();
                if (voidRend != null && shader != null)
                {
                    var mat = new Material(shader) { color = new Color(0.01f, 0.01f, 0.01f) };
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", 0f);
                    voidRend.sharedMaterial = mat;
                }
            }
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

        private void AddPcbWings(GridBoard board, float tileSize)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var wingMat = new Material(shader) { color = new Color(0.04f, 0.06f, 0.10f) };
            wingMat.SetFloat("_Metallic", 0.3f);
            wingMat.SetFloat("_Smoothness", 0.4f);

            float halfW = board.Width * tileSize / 2f;
            float halfH = board.Height * tileSize / 2f;
            float wingW = halfW * 0.8f;
            float wingH = halfH + tileSize * 0.6f;
            float wingZ = -0.01f;

            // Left wing
            var leftWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWing.name = "PcbWing_Left";
            DestroyImmediate(leftWing.GetComponent<Collider>());
            leftWing.transform.SetParent(transform, false);
            leftWing.transform.localPosition = new Vector3(-halfW - wingW / 2f, 0f, wingZ);
            leftWing.transform.localScale = new Vector3(wingW, wingH * 2f, 0.03f);
            leftWing.GetComponent<MeshRenderer>().sharedMaterial = wingMat;

            // Right wing
            var rightWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWing.name = "PcbWing_Right";
            DestroyImmediate(rightWing.GetComponent<Collider>());
            rightWing.transform.SetParent(transform, false);
            rightWing.transform.localPosition = new Vector3(halfW + wingW / 2f, 0f, wingZ);
            rightWing.transform.localScale = new Vector3(wingW, wingH * 2f, 0.03f);
            rightWing.GetComponent<MeshRenderer>().sharedMaterial = wingMat;
        }
    }
}
