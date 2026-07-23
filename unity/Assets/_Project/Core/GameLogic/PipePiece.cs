namespace ChromaVale.Core.GameLogic
{
    public enum PieceShape
    {
        Straight,      // ─  — basic horizontal/vertical connector
        Elbow,         // └  — 90-degree turn
        TJunction,     // ├  — splits flow into two paths
        Cross,         // ┼  — four-way intersection
        Valve,         // ◇  — one-way flow gate (prevents backflow)
        Amplifier,     // ▲  — increases adjacent pipe capacity by 1
        Mixer,         // ✕  — deliberately mixes two colors at this cell
        Blocker        // ■  — emergency stop; halts flow in one cell
    }

    public enum PieceState
    {
        InHand,        // Available for placement
        Placed,        // On the board
        Burst          // Destroyed (permanent obstacle)
    }

    public class PipePiece
    {
        public PieceShape Shape;
        public int Capacity;       // 1-3 (how much flow it can carry before burst)
        public int ColorAffinity;  // -1 = any color, 0-5 = specific color only
        public PieceState State;

        public static PipePiece Straight(int capacity = 2, int affinity = -1) =>
            new() { Shape = PieceShape.Straight, Capacity = capacity, ColorAffinity = affinity, State = PieceState.InHand };

        public static PipePiece Elbow(int capacity = 2, int affinity = -1) =>
            new() { Shape = PieceShape.Elbow, Capacity = capacity, ColorAffinity = affinity, State = PieceState.InHand };

        public static PipePiece TJunction(int capacity = 2, int affinity = -1) =>
            new() { Shape = PieceShape.TJunction, Capacity = capacity, ColorAffinity = affinity, State = PieceState.InHand };

        public static PipePiece Cross(int capacity = 2, int affinity = -1) =>
            new() { Shape = PieceShape.Cross, Capacity = capacity, ColorAffinity = affinity, State = PieceState.InHand };

        public static PipePiece Valve(int capacity = 2, PipeDirection direction = PipeDirection.None) =>
            new() { Shape = PieceShape.Valve, Capacity = capacity, ColorAffinity = -1, State = PieceState.InHand };

        public static PipePiece Amplifier() =>
            new() { Shape = PieceShape.Amplifier, Capacity = 0, ColorAffinity = -1, State = PieceState.InHand };

        public static PipePiece Mixer() =>
            new() { Shape = PieceShape.Mixer, Capacity = 0, ColorAffinity = -1, State = PieceState.InHand };

        public static PipePiece Blocker() =>
            new() { Shape = PieceShape.Blocker, Capacity = 0, ColorAffinity = -1, State = PieceState.InHand };

        public bool CanCarryColor(int colorIndex) =>
            ColorAffinity == -1 || ColorAffinity == colorIndex;

        public bool IsPlaced => State == PieceState.Placed;
        public bool IsAvailable => State == PieceState.InHand;
        public bool IsBurst => State == PieceState.Burst;

        public override string ToString() =>
            $"{Shape} (cap={Capacity}, aff={ColorAffinity}, {State})";
    }
}
