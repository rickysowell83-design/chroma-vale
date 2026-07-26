namespace ChromaVale.Core.GameLogic
{
    public enum SegmentShape
    {
        Straight,      // ─  — basic horizontal/vertical connector
        Corner,        // └  — 90-degree turn (was Elbow)
        Splitter,      // ├  — splits signal into two paths (was TJunction)
        CrossJunction, // ┼  — four-way intersection (was Cross)
        Diode,         // ◇  — one-way gate; prevents backflow (was Valve)
        Repeater,      // ▲  — increases adjacent trace capacity by 1 (was Amplifier)
        Combiner,      // ✕  — deliberately mixes two colors at this cell (was Mixer)
        Breaker        // ■  — emergency stop; halts signal in one cell (was Blocker)
    }

    public enum SegmentState
    {
        InHand,        // Available for placement
        Placed,        // On the board
        Shorted         // Destroyed (permanent obstacle) — short circuit (was Burst)
    }

    public class TraceSegment
    {
        public SegmentShape Shape;
        public int Capacity;       // 1-3 (how much signal it can carry before short circuit)
        public int ColorAffinity;  // -1 = any color, 0-5 = specific color only
        public SegmentState State;
        /// <summary>
        /// Rotation in degrees clockwise (0, 90, 180, 270).
        /// Affects which directions the segment connects.
        /// </summary>
        public int Rotation { get; set; } = 0;
        public TraceDirection Direction; // For Diode segments: the direction signal can exit. TraceDirection.None for non-Diode segments.

        public static TraceSegment Straight(int capacity = 2, int affinity = -1) =>
            new() { Shape = SegmentShape.Straight, Capacity = capacity, ColorAffinity = affinity, State = SegmentState.InHand, Direction = TraceDirection.None };

        public static TraceSegment Corner(int capacity = 2, int affinity = -1) =>
            new() { Shape = SegmentShape.Corner, Capacity = capacity, ColorAffinity = affinity, State = SegmentState.InHand, Direction = TraceDirection.None };

        public static TraceSegment Splitter(int capacity = 2, int affinity = -1) =>
            new() { Shape = SegmentShape.Splitter, Capacity = capacity, ColorAffinity = affinity, State = SegmentState.InHand, Direction = TraceDirection.None };

        public static TraceSegment CrossJunction(int capacity = 2, int affinity = -1) =>
            new() { Shape = SegmentShape.CrossJunction, Capacity = capacity, ColorAffinity = affinity, State = SegmentState.InHand, Direction = TraceDirection.None };

        public static TraceSegment Diode(int capacity = 2, TraceDirection direction = TraceDirection.None) =>
            new() { Shape = SegmentShape.Diode, Capacity = capacity, ColorAffinity = -1, State = SegmentState.InHand, Direction = direction };

        public static TraceSegment Repeater() =>
            new() { Shape = SegmentShape.Repeater, Capacity = 0, ColorAffinity = -1, State = SegmentState.InHand, Direction = TraceDirection.None };

        public static TraceSegment Combiner() =>
            new() { Shape = SegmentShape.Combiner, Capacity = 0, ColorAffinity = -1, State = SegmentState.InHand, Direction = TraceDirection.None };

        public static TraceSegment Breaker() =>
            new() { Shape = SegmentShape.Breaker, Capacity = 0, ColorAffinity = -1, State = SegmentState.InHand, Direction = TraceDirection.None };

        public bool CanCarryColor(int colorIndex) =>
            ColorAffinity == -1 || ColorAffinity == colorIndex;

        /// <summary>
        /// Rotate the segment 90 degrees clockwise, wrapping at 360.
        /// </summary>
        public void Rotate()
        {
            Rotation = (Rotation + 90) % 360;
        }

        public bool IsPlaced => State == SegmentState.Placed;
        public bool IsAvailable => State == SegmentState.InHand;
        public bool IsShorted => State == SegmentState.Shorted;

        public override string ToString() =>
            $"{(Shape)} (cap={Capacity}, aff={ColorAffinity}, dir={Direction}, rot={Rotation}, {State})";
    }
}
