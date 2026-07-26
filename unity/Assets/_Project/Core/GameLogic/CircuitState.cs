namespace ChromaVale.Core.GameLogic
{
    public enum CircuitState
    {
        Normal,        // Signal ≤ capacity
        Overloading,   // Signal > capacity — flashing warning for 1 tick
        Shorted        // Trace destroyed — cell is permanent obstacle (was Burst)
    }

    /// <summary>
    /// Per-cell runtime state during signal propagation.
    /// Tracks how much signal is in each cell, whether it's overloaded, and
    /// what color(s) are present.
    /// Class (not struct) so mutations persist when accessed from arrays.
    /// </summary>
    public class TraceCellState
    {
        public int CurrentSignal;       // Units of signal currently in this cell
        public int Capacity;          // Max capacity before short circuit
        public int ColorIndex;        // Color of signal in trace (-1 = none)
        public CircuitState State;    // Normal, Overloading, or Shorted
        public int OverloadTicks;     // Consecutive ticks in overload state (short at 1)

        // For color mixing: track what colors have passed through
        public int MixedColorA = -1;
        public int MixedColorB = -1;
        public int MixedColorCount;
        public int ResultColor = -1;

        public TraceCellState() { }

        public TraceCellState(int capacity)
        {
            Capacity = capacity;
            ColorIndex = -1;
        }

        public static TraceCellState CreateEmpty() => new() { Capacity = 0, ColorIndex = -1 };
        public static TraceCellState CreateTrace(int capacity) => new(capacity);

        /// <summary>
        /// Add signal to this cell. Returns true if still stable, false if shorted.
        /// Mutations persist because this is a class.
        /// </summary>
        public bool AddSignal(int amount, int colorIndex)
        {
            CurrentSignal += amount;

            // Track color mixing
            if (colorIndex != -1)
            {
                if (MixedColorCount == 0)
                {
                    MixedColorA = colorIndex;
                    MixedColorCount = 1;
                    ResultColor = colorIndex;
                }
                else if (MixedColorCount == 1 && MixedColorA != colorIndex)
                {
                    MixedColorB = colorIndex;
                    MixedColorCount = 2;
                    ResultColor = ColorMixer.Mix(MixedColorA, MixedColorB);
                }
                else if (colorIndex != MixedColorA && colorIndex != MixedColorB)
                {
                    MixedColorCount = 3;
                    ResultColor = 9; // Three+ distinct colors = Brown (waste)
                }
                ColorIndex = ResultColor;
            }

            // Check overload
            if (CurrentSignal > Capacity)
            {
                OverloadTicks++;
                State = CircuitState.Overloading;
                if (OverloadTicks >= 1)
                {
                    State = CircuitState.Shorted;
                    return false; // Short circuit!
                }
            }

            return true; // Still stable
        }

        public bool IsStable => State != CircuitState.Shorted;
        public bool IsOverloading => State == CircuitState.Overloading;
    }
}
