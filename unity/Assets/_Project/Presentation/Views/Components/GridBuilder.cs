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
                    tv.SetIndicator(TileIndicator.ObstacleBlock, ChromaPalette.ObstacleCol);
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
                cam.transform.position = new Vector3(0, -0.8f, -dist);
                cam.transform.rotation = Quaternion.identity;
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
