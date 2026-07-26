namespace ChromaVale.Core.GameLogic
{
    public enum TraceDirection { Up, Down, Left, Right, None }

    public enum CellType { Empty, Source, Target, Obstacle, Trace, SignalGate }

    public struct GridCell
    {
        public CellType Type;
        public int ColorIndex;   // 0-5 for our 6 colors
        public bool IsOccupied;
        public TraceDirection SignalDirection; // for SignalGate cells — forces one-way signal

        public static GridCell Empty => new() { Type = CellType.Empty, ColorIndex = -1, SignalDirection = TraceDirection.None };
        public static GridCell Source(int color) => new() { Type = CellType.Source, ColorIndex = color, IsOccupied = true, SignalDirection = TraceDirection.None };
        public static GridCell Target(int color) => new() { Type = CellType.Target, ColorIndex = color, SignalDirection = TraceDirection.None };
        public static GridCell Obstacle => new() { Type = CellType.Obstacle, IsOccupied = true, SignalDirection = TraceDirection.None };
        public static GridCell SignalGate(TraceDirection dir) => new() { Type = CellType.SignalGate, ColorIndex = -1, IsOccupied = true, SignalDirection = dir };
    }

    public interface IBoardState
    {
        int Width { get; }
        int Height { get; }
        GridCell GetCell(int x, int y);
        void PlaceTrace(int x, int y);
        bool IsValidPosition(int x, int y);
        bool IsComplete { get; }
    }
}
