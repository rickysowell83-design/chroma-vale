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
        /// <summary>
        /// Rotation in degrees clockwise (0, 90, 180, 270).
        /// Affects which directions the piece connects.
        /// </summary>
        public int Rotation { get; set; } = 0;
        public PipeDirection Direction; // For Valve pieces: the direction flow can exit. PipeDirection.None for non-Valve pieces.

        public static PipePiece Straight(int capacity = 2, int affinity = -1) =>
            new() { Shape = PieceShape.Straight, Capacity = capacity, ColorAffinity = affinity, State = PieceState.InHand, Direction = PipeDirection.None };

        public static PipePiece Elbow(int capacity = 2, int affinity = -1) =>
            new() { Shape = PieceShape.Elbow, Capacity = capacity, ColorAffinity = affinity, State = PieceState.InHand, Direction = PipeDirection.None };

        public static PipePiece TJunction(int capacity = 2, int affinity = -1) =>
            new() { Shape = PieceShape.TJunction, Capacity = capacity, ColorAffinity = affinity, State = PieceState.InHand, Direction = PipeDirection.None };

        public static PipePiece Cross(int capacity = 2, int affinity = -1) =>
            new() { Shape = PieceShape.Cross, Capacity = capacity, ColorAffinity = affinity, State = PieceState.InHand, Direction = PipeDirection.None };

        public static PipePiece Valve(int capacity = 2, PipeDirection direction = PipeDirection.None) =>
            new() { Shape = PieceShape.Valve, Capacity = capacity, ColorAffinity = -1, State = PieceState.InHand, Direction = direction };

        public static PipePiece Amplifier() =>
            new() { Shape = PieceShape.Amplifier, Capacity = 0, ColorAffinity = -1, State = PieceState.InHand, Direction = PipeDirection.None };

        public static PipePiece Mixer() =>
            new() { Shape = PieceShape.Mixer, Capacity = 0, ColorAffinity = -1, State = PieceState.InHand, Direction = PipeDirection.None };

        public static PipePiece Blocker() =>
            new() { Shape = PieceShape.Blocker, Capacity = 0, ColorAffinity = -1, State = PieceState.InHand, Direction = PipeDirection.None };

        public bool CanCarryColor(int colorIndex) =>
            ColorAffinity == -1 || ColorAffinity == colorIndex;

        /// <summary>
        /// Rotate the piece 90 degrees clockwise, wrapping at 360.
        /// </summary>
        public void Rotate()
        {
            Rotation = (Rotation + 90) % 360;
        }

        public bool IsPlaced => State == PieceState.Placed;
        public bool IsAvailable => State == PieceState.InHand;
        public bool IsBurst => State == PieceState.Burst;

        public override string ToString() =>
            $"{(Shape)} (cap={Capacity}, aff={ColorAffinity}, dir={Direction}, rot={Rotation}, {State})";
    }
}

/*
 * ── TEST CASES: Valve piece direction enforcement ─────────────────────────
 *
 * FlowSimulator now enforces shape-aware flow via GetInputFlags / GetOutputFlags.
 * For Valve pieces specifically:
 *   Input  = opposite of Valve.Direction (flow enters from the opposite side)
 *   Output = Valve.Direction          (flow exits only in the valve's direction)
 *
 * Test 1 — Valve blocks entry from wrong side:
 *   Board: 3x1, Src(0,0) → Valve(Right, 1,0) → Tgt(2,0)
 *   Source emits Right into Valve(Right). Entry from Right is valid?
 *   GetInputFlags(Valve, Right) → DirectionToFlag(Opposite(Right)) = LeftFlag
 *   Flow entering from Left (dx=-1)? DirectionToFlag(Left) & LeftFlag != 0 → yes.
 *   Flow entering from Right (dx=1)? DirectionToFlag(Right) & LeftFlag → 0 → blocked.
 *   With Src(0,0) → Valve(1,0) going Right (dx=1), entry is from the Right side,
 *   which is NOT the input side (Left). → BLOCKED. Result: FlowStopped.
 *
 * Test 2 — Valve allows correct-direction flow:
 *   Board: 3x1, Src(2,0) → Valve(Left, 1,0) → Tgt(0,0)
 *   Source emits Left (dx=-1) into Valve(Left).
 *   GetInputFlags(Valve, Left) → DirectionToFlag(Opposite(Left)) = RightFlag
 *   Flow entering from Right (dx=-1)? DirectionToFlag(Right) & RightFlag != 0 → allowed.
 *   Flow enters valve. Next tick: CanExitCell?
 *   GetOutputFlags(Valve, Left) → DirectionToFlag(Left) = LeftFlag
 *   Exit Left (dx=-1): DirectionToFlag(Left) & LeftFlag != 0 → allowed.
 *   Flow goes to Tgt(0,0). Result: AllTargetsReached.
 *
 * Test 3 — Valve exit blocks non-valve directions:
 *   Same setup, but target is ABOVE valve instead of LEFT.
 *   CanExitCell(Valve, Left, Up) → DirectionToFlag(Up) & LeftFlag → 0 → blocked.
 *   Flow trapped in valve. Target not reached. Result: FlowStopped.
 *
 * Integration test (when Unity Test Framework is configured):
 *   Create a GridBoard + FlowSimulator + PipeInventory.
 *   Place a Valve piece, set up source/target, run ticks, assert result.
 *   See the shape-aware flow test suite in Assets/_Project/Tests/ for full tests.
 */



