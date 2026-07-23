using ChromaVale.Core.GameLogic;
using System.Collections.Generic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class GridBoard : IBoardState
    {
        private GridCell[,] _cells;
        private readonly Stack<(int x, int y, GridCell previous)> _history = new();

        public int Width { get; }
        public int Height { get; }
        public bool IsComplete { get; private set; }

        public GridBoard(LevelData level)
        {
            Width = level.Width;
            Height = level.Height;
            _cells = new GridCell[Width, Height];

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                _cells[x, y] = GridCell.Empty;

            foreach (var s in level.Sources)
                _cells[s.X, s.Y] = GridCell.Source(s.ColorIndex);

            foreach (var t in level.Targets)
                _cells[t.X, t.Y] = GridCell.Target(t.ColorIndex);

            foreach (var o in level.Obstacles)
                _cells[o.X, o.Y] = GridCell.Obstacle;

            foreach (var fg in level.FlowGates)
                _cells[fg.X, fg.Y] = GridCell.FlowGate(fg.Direction);
        }

        public GridCell GetCell(int x, int y) => _cells[x, y];

        public void PlacePipe(int x, int y, int colorIndex)
        {
            if (!IsValidPosition(x, y)) return;
            var cell = _cells[x, y];
            // Allow placement on Empty cells only. colorIndex of -1 means "uncolored pipe."
            if (cell.Type != CellType.Empty) return;
            if (cell.IsOccupied) return;

            _history.Push((x, y, cell));
            _cells[x, y] = new GridCell
            {
                Type = CellType.Pipe,
                ColorIndex = colorIndex, // -1 = uncolored (color assigned by flow)
                IsOccupied = true,
                FlowDirection = PipeDirection.None
            };
        }

        public void UndoLast()
        {
            if (_history.Count == 0) return;
            var (x, y, prev) = _history.Pop();
            _cells[x, y] = prev;
        }

        public bool IsValidPosition(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height;
    }
}
