using ChromaVale.Core.GameLogic;
using System.Collections.Generic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class PipeRouter : IPipeRouter
    {
        private readonly GridBoard _board;
        private readonly Stack<(int x, int y)> _history = new();

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
            // Simple BFS to check if a pipe path connects source to target
            var visited = new HashSet<(int, int)>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue((sourceX, sourceY));

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                if (cx == targetX && cy == targetY) return true;
                if (!visited.Add((cx, cy))) continue;

                foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!_board.IsValidPosition(nx, ny)) continue;
                    var cell = _board.GetCell(nx, ny);
                    if (cell.Type == CellType.Pipe || cell.Type == CellType.Target)
                        queue.Enqueue((nx, ny));
                }
            }
            return false;
        }
    }
}