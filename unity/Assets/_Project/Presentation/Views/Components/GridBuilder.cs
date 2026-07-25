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

            for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board.GetCell(x, y);
                var worldPos = new Vector3(x * tileSize + off.x, y * tileSize + off.y, 0);
                var tv = TileVisual.Create(transform, worldPos, tileSize, "Tile_" + x + "_" + y);

                _renderers[x, y] = tv;

                tv.Color = cell.Type switch
                {
                    CellType.Source when cell.ColorIndex == 0 => ChromaPalette.CyanHint,
                    CellType.Source when cell.ColorIndex == 1 => ChromaPalette.MagentaHint,
                    CellType.Source when cell.ColorIndex == 2 => ChromaPalette.YellowHint,
                    CellType.Target when cell.ColorIndex == 0 => ChromaPalette.CyanHint,
                    CellType.Target when cell.ColorIndex == 1 => ChromaPalette.MagentaHint,
                    CellType.Target when cell.ColorIndex == 2 => ChromaPalette.YellowHint,
                    CellType.Target when cell.ColorIndex == 6 => ChromaPalette.PurpleHint,
                    CellType.Target when cell.ColorIndex == 7 => new Color(0.05f, 0.15f, 0.05f),
                    CellType.Obstacle => ChromaPalette.ObstacleCol,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Up => ChromaPalette.FlowGateUp,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Down => ChromaPalette.FlowGateDown,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Right => ChromaPalette.FlowGateRight,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Left => ChromaPalette.FlowGateLeft,
                    _ => ChromaPalette.DarkTile
                };

                // Indicators
                if (cell.Type == CellType.Source)
                    tv.SetIndicator(TileIndicator.SourceDot, GetPipeColor(cell.ColorIndex));
                if (cell.Type == CellType.Target)
                    tv.SetIndicator(TileIndicator.TargetRing, GetPipeColor(cell.ColorIndex));
                if (cell.Type == CellType.Obstacle)
                {
                    // Build a burnt-out microchip mesh instead of an indicator block
                    var chipRoot = new GameObject("BurntMicrochip");
                    chipRoot.transform.SetParent(tv.transform, false);
                    chipRoot.transform.localPosition = Vector3.zero;

                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");

                    // Dark burnt-red URP/Lit material for the chip body
                    var chipMat = new Material(shader);
                    chipMat.color = new Color(0.08f, 0.04f, 0.04f);
                    chipMat.SetFloat("_Metallic", 0.5f);
                    chipMat.SetFloat("_Smoothness", 0.3f);

                    // Silver material for pins
                    var pinMat = new Material(shader);
                    pinMat.color = new Color(0.75f, 0.75f, 0.78f);
                    pinMat.SetFloat("_Metallic", 0.9f);
                    pinMat.SetFloat("_Smoothness", 0.8f);

                    // 1. Flattened cube (microchip body)
                    var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    body.name = "ChipBody";
                    DestroyImmediate(body.GetComponent<Collider>());
                    body.transform.SetParent(chipRoot.transform, false);
                    body.transform.localPosition = new Vector3(0f, 0f, -0.15f);
                    body.transform.localScale = new Vector3(_tileSize * 0.45f, _tileSize * 0.45f, 0.08f);
                    var bodyRend = body.GetComponent<MeshRenderer>();
                    bodyRend.sharedMaterial = chipMat;

                    // 2. Four silver pins at the corners pointing DOWN (Z- direction)
                    float pinOffset = _tileSize * 0.12f;
                    float pinRadius = _tileSize * 0.015f;
                    float pinLength = _tileSize * 0.08f;
                    Vector3[] pinPositions = new[]
                    {
                        new Vector3(-pinOffset, -pinOffset, -0.15f - pinLength * 0.5f),
                        new Vector3( pinOffset, -pinOffset, -0.15f - pinLength * 0.5f),
                        new Vector3(-pinOffset,  pinOffset, -0.15f - pinLength * 0.5f),
                        new Vector3( pinOffset,  pinOffset, -0.15f - pinLength * 0.5f),
                    };

                    for (int pi = 0; pi < pinPositions.Length; pi++)
                    {
                        var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        pin.name = "Pin_" + pi;
                        DestroyImmediate(pin.GetComponent<Collider>());
                        pin.transform.SetParent(chipRoot.transform, false);
                        pin.transform.localPosition = pinPositions[pi];
                        // Rotate cylinder (default Y-axis) to point along Z
                        pin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        pin.transform.localScale = new Vector3(pinRadius, pinLength * 0.5f, pinRadius);
                        var pinRend = pin.GetComponent<MeshRenderer>();
                        pinRend.sharedMaterial = pinMat;
                    }

                    // 3. Dark scorch mark: glowing red sphere at center
                    var scorch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    scorch.name = "ScorchMark";
                    DestroyImmediate(scorch.GetComponent<Collider>());
                    scorch.transform.SetParent(chipRoot.transform, false);
                    scorch.transform.localPosition = new Vector3(_tileSize * 0.04f, _tileSize * 0.04f, -0.18f);
                    scorch.transform.localScale = Vector3.one * (_tileSize * 0.12f);
                    var scorchRend = scorch.GetComponent<MeshRenderer>();
                    var scorchMat = new Material(shader);
                    scorchMat.color = new Color(0.15f, 0.02f, 0.02f);
                    scorchMat.SetFloat("_Metallic", 0.1f);
                    scorchMat.SetFloat("_Smoothness", 0.1f);
                    scorchMat.EnableKeyword("_EMISSION");
                    scorchMat.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.05f) * 0.8f); // Bright red glow
                    scorchMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    scorchRend.sharedMaterial = scorchMat;
                }
                if (cell.Type == CellType.FlowGate)
                {
                    tv.SetIndicator(TileIndicator.FlowGateArrow, Color.white);
                    float angle = cell.FlowDirection switch
                    {
                        PipeDirection.Up => 0f,
                        PipeDirection.Right => 270f,
                        PipeDirection.Down => 180f,
                        PipeDirection.Left => 90f,
                        _ => 0f
                    };
                    tv.SetIndicatorRotation(angle);
                }

                // Click handler (BoxCollider already added by TileVisual.Create)
                tv.gameObject.AddComponent<TileClickHandler>().Init(x, y, view);
            }

            SetupCamera();
            return _renderers;
        }

        public void Clear()
        {
            // Destroy all child tiles
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
    }
}
