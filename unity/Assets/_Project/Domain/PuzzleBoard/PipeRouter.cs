using ChromaVale.Core.GameLogic;
using System.Collections.Generic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class PipeRouter : IPipeRouter
    {
        private readonly GridBoard _board;
        private readonly Stack<(int x, int y)> _history = new();

        private static readonly (int dx, int dy, PipeDirection dir)[] Neighbors =
        {
            ( 0,  1, PipeDirection.Up),
            ( 0, -1, PipeDirection.Down),
            ( 1,  0, PipeDirection.Right),
            (-1,  0, PipeDirection.Left),
        };

        public PipeRouter(GridBoard board)
        {
            _board = board;
        }

        public bool CanPlace(int x, int y, int colorIndex)
        {
            if (!_board.IsValidPosition(x, y)) return false;
            var cell = _board.GetCell(x, y);
            return cell.Type != CellType.Source
                && cell.Type != CellType.Target
                && cell.Type != CellType.Obstacle
                && cell.Type != CellType.FlowGate
                && !cell.IsOccupied;
        }

        public void Place(int x, int y, int colorIndex)
        {
            if (!CanPlace(x, y, colorIndex)) return;
            _board.PlacePipe(x, y, colorIndex);
            _history.Push((x, y));
        }

        public void Undo()
        {
            if (_history.Count == 0) return;
            _history.Pop();
            _board.UndoLast();
        }

        public bool IsPathConnected(int sourceX, int sourceY, int targetX, int targetY)
        {
            var visited = new HashSet<(int, int)>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue((sourceX, sourceY));

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                if (cx == targetX && cy == targetY) return true;
                if (!visited.Add((cx, cy))) continue;

                foreach (var (dx, dy, dir) in Neighbors)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!_board.IsValidPosition(nx, ny)) continue;

                    var fromCell = _board.GetCell(cx, cy);
                    var toCell = _board.GetCell(nx, ny);

                    // Can't walk through obstacles
                    if (toCell.Type == CellType.Obstacle) continue;

                    // Must be able to traverse a pipe or reach a target
                    bool toIsWalkable = toCell.Type == CellType.Pipe
                                     || toCell.Type == CellType.Target
                                     || toCell.Type == CellType.FlowGate;

                    if (!toIsWalkable && toCell.Type != CellType.Target) continue;

                    // ── Directional flow constraint ──
                    // If the FROM cell is a flow gate, flow must exit in the gate's direction
                    if (fromCell.Type == CellType.FlowGate && fromCell.FlowDirection != dir)
                        continue;

                    // If the TO cell is a flow gate, flow must enter from the gate's input side
                    if (toCell.Type == CellType.FlowGate && toCell.FlowDirection != dir)
                        continue;

                    // If the TO cell is a pipe, allow passage freely
                    if (toCell.Type == CellType.Pipe || toCell.Type == CellType.Target || toCell.Type == CellType.FlowGate)
                        queue.Enqueue((nx, ny));
                }
            }
            return false;
        }
    }
}
