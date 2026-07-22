namespace ChromaVale.Core.GameLogic
{
    public enum PipeDirection { Up, Down, Left, Right }

    public enum CellType { Empty, Source, Target, Obstacle, Pipe }

    public struct GridCell
    {
        public CellType Type;
        public int ColorIndex;   // 0-5 for our 6 colors
        public bool IsOccupied;

        public static GridCell Empty => new GridCell { Type = CellType.Empty, ColorIndex = -1 };
        public static GridCell Source(int color) => new GridCell { Type = CellType.Source, ColorIndex = color, IsOccupied = true };
        public static GridCell Target(int color) => new GridCell { Type = CellType.Target, ColorIndex = color };
        public static GridCell Obstacle => new GridCell { Type = CellType.Obstacle, IsOccupied = true };
    }

    public interface IBoardState
    {
        int Width { get; }
        int Height { get; }
        GridCell GetCell(int x, int y);
        void PlacePipe(int x, int y, int colorIndex);
        bool IsValidPosition(int x, int y);
        bool IsComplete { get; }
    }
}
