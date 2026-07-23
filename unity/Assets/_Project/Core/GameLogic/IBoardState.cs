namespace ChromaVale.Core.GameLogic
{
    public enum PipeDirection { Up, Down, Left, Right, None }

    public enum CellType { Empty, Source, Target, Obstacle, Pipe, FlowGate }

    public struct GridCell
    {
        public CellType Type;
        public int ColorIndex;   // 0-5 for our 6 colors
        public bool IsOccupied;
        public PipeDirection FlowDirection; // for FlowGate cells — forces one-way flow

        public static GridCell Empty => new() { Type = CellType.Empty, ColorIndex = -1, FlowDirection = PipeDirection.None };
        public static GridCell Source(int color) => new() { Type = CellType.Source, ColorIndex = color, IsOccupied = true, FlowDirection = PipeDirection.None };
        public static GridCell Target(int color) => new() { Type = CellType.Target, ColorIndex = color, FlowDirection = PipeDirection.None };
        public static GridCell Obstacle => new() { Type = CellType.Obstacle, IsOccupied = true, FlowDirection = PipeDirection.None };
        public static GridCell FlowGate(PipeDirection dir) => new() { Type = CellType.FlowGate, ColorIndex = -1, IsOccupied = true, FlowDirection = dir };
    }

    public interface IBoardState
    {
        int Width { get; }
        int Height { get; }
        GridCell GetCell(int x, int y);
        void PlacePipe(int x, int y);
        bool IsValidPosition(int x, int y);
        bool IsComplete { get; }
    }
}
