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
        private GameObject _pipeTilePrefab;
        private GameObject _sourceTilePrefab;
        private GameObject _targetTilePrefab;
        private GameObject _obstacleTilePrefab;
        private SpriteRenderer[,] _renderers;

        /// <summary>
        /// Build the grid and return the array of SpriteRenderers.
        /// </summary>
        /// <param name="level">Level data with sources, targets, obstacles, flow gates.</param>
        /// <param name="board">Grid board with cell state.</param>
        /// <param name="tileSize">World-space size of each tile.</param>
        /// <param name="pipeTilePrefab">Prefab for empty/pipe tiles.</param>
        /// <param name="sourceTilePrefab">Prefab for source tiles.</param>
        /// <param name="targetTilePrefab">Prefab for target tiles.</param>
        /// <param name="obstacleTilePrefab">Prefab for obstacle tiles (falls back to pipeTilePrefab).</param>
        /// <param name="view">The puzzle board view for tile click wiring.</param>
        /// <returns>2D array of SpriteRenderers for each cell.</returns>
        public SpriteRenderer[,] Build(LevelData level, GridBoard board, float tileSize,
            GameObject pipeTilePrefab, GameObject sourceTilePrefab, GameObject targetTilePrefab,
            GameObject obstacleTilePrefab, PuzzleBoardView view)
        {
            _level = level;
            _board = board;
            _tileSize = tileSize;
            _pipeTilePrefab = pipeTilePrefab;
            _sourceTilePrefab = sourceTilePrefab;
            _targetTilePrefab = targetTilePrefab;
            _obstacleTilePrefab = obstacleTilePrefab;

            _renderers = new SpriteRenderer[board.Width, board.Height];
            var off = new Vector3(-board.Width * tileSize / 2f, -board.Height * tileSize / 2f, 0);

            for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board.GetCell(x, y);
                GameObject prefab = cell.Type switch
                {
                    CellType.Source => sourceTilePrefab,
                    CellType.Target => targetTilePrefab,
                    CellType.Obstacle => obstacleTilePrefab ?? pipeTilePrefab,
                    CellType.FlowGate => pipeTilePrefab,
                    _ => pipeTilePrefab
                };
                if (prefab == null) continue;

                var tile = Instantiate(prefab,
                    new Vector3(x * tileSize + off.x, y * tileSize + off.y, 0),
                    Quaternion.identity, transform);
                tile.name = "Tile_" + x + "_" + y;

                var sr = tile.GetComponent<SpriteRenderer>();
                if (sr == null) sr = tile.AddComponent<SpriteRenderer>();
                _renderers[x, y] = sr;

                sr.color = cell.Type switch
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

                // Source/Target/FlowGate indicators
                if (cell.Type == CellType.Source) AddIndicator(tile, "SrcDot", GetPipeColor(cell.ColorIndex) * 2f, 0.3f, 1);
                if (cell.Type == CellType.Target) AddIndicator(tile, "TgtRing", new Color(1f, 1f, 1f, 0.5f), 1.4f, 1);
                if (cell.Type == CellType.FlowGate) AddFlowGateArrow(tile, cell.FlowDirection);

                // Click handler
                var col = tile.AddComponent<BoxCollider>();
                col.size = new Vector3(1, 1, 0.1f) * tileSize;
                tile.AddComponent<TileClickHandler>().Init(x, y, view);
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

        private void AddIndicator(GameObject parent, string name, Color color, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            var parentSr = parent.GetComponent<SpriteRenderer>();
            if (sr != null && parentSr != null)
            {
                sr.sprite = parentSr.sprite;
                sr.color = color;
                sr.sortingOrder = order;
            }
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void AddFlowGateArrow(GameObject parent, PipeDirection dir)
        {
            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(parent.transform, false);
            arrow.transform.localPosition = Vector3.zero;
            var sr = arrow.AddComponent<SpriteRenderer>();
            var parentSr = parent.GetComponent<SpriteRenderer>();
            if (sr != null && parentSr != null)
            {
                sr.sprite = parentSr.sprite;
                sr.color = Color.white;
                sr.sortingOrder = 1;
            }
            arrow.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            float angle = dir switch
            {
                PipeDirection.Up => 0f,
                PipeDirection.Right => 270f,
                PipeDirection.Down => 180f,
                PipeDirection.Left => 90f,
                _ => 0f
            };
            arrow.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private Sprite CreatePixelSprite()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, -0.8f, -10);
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(_board.Width, _board.Height) * _tileSize / 2f + 2f;
                cam.backgroundColor = ChromaPalette.DarkBG;
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
