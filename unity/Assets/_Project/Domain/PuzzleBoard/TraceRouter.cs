using ChromaVale.Core.GameLogic;
using System.Collections.Generic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class TraceRouter : ITraceRouter
    {
        private readonly GridBoard _board;
        private readonly Stack<(int x, int y)> _history = new();

        private static readonly (int dx, int dy, TraceDirection dir)[] Neighbors =
        {
            ( 0,  1, TraceDirection.Up),
            ( 0, -1, TraceDirection.Down),
            ( 1,  0, TraceDirection.Right),
            (-1,  0, TraceDirection.Left),
        };

        public TraceRouter(GridBoard board)
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
                && cell.Type != CellType.SignalGate
                && !cell.IsOccupied;
        }

        public void Place(int x, int y, int colorIndex)
        {
            if (!CanPlace(x, y, colorIndex)) return;
            _board.PlaceTrace(x, y);
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

                    // Can't route through obstacles
                    if (toCell.Type == CellType.Obstacle) continue;

                    // Must be able to traverse a trace or reach a target
                    bool toIsWalkable = toCell.Type == CellType.Trace
                                     || toCell.Type == CellType.Target
                                     || toCell.Type == CellType.SignalGate;

                    if (!toIsWalkable && toCell.Type != CellType.Target) continue;

                    // ── Directional flow constraint ──
                    // If the FROM cell is a signal gate, signal must exit in the gate's direction
                    if (fromCell.Type == CellType.SignalGate && fromCell.SignalDirection != dir)
                        continue;

                    // If the TO cell is a signal gate, signal must enter from the gate's input side
                    if (toCell.Type == CellType.SignalGate && toCell.SignalDirection != dir)
                        continue;

                    // If the TO cell is a trace, allow passage freely
                    if (toCell.Type == CellType.Trace || toCell.Type == CellType.Target || toCell.Type == CellType.SignalGate)
                        queue.Enqueue((nx, ny));
                }
            }
            return false;
        }
    }
}
