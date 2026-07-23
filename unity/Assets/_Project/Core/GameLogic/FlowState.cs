namespace ChromaVale.Core.GameLogic
{
    public enum OverloadState
    {
        Normal,        // Flow ≤ capacity
        Overloading,   // Flow > capacity — flashing warning for 1 tick
        Burst          // Pipe destroyed — cell is permanent obstacle
    }

    /// <summary>
    /// Per-cell runtime state during flow simulation.
    /// Tracks how much flow is in each cell, whether it's overloaded, and
    /// what color(s) are present.
    /// Class (not struct) so mutations persist when accessed from arrays.
    /// </summary>
    public class PipeCellState
    {
        public int CurrentFlow;       // Units of flow currently in this cell
        public int Capacity;          // Max capacity before burst
        public int ColorIndex;        // Color of flow in pipe (-1 = none)
        public OverloadState State;   // Normal, Overloading, or Burst
        public int OverloadTicks;     // Consecutive ticks in overload state (burst at 1)

        // For color mixing: track what colors have passed through
        public int MixedColorA = -1;
        public int MixedColorB = -1;
        public int MixedColorCount;
        public int ResultColor = -1;

        public PipeCellState() { }

        public PipeCellState(int capacity)
        {
            Capacity = capacity;
            ColorIndex = -1;
        }

        public static PipeCellState CreateEmpty() => new() { Capacity = 0, ColorIndex = -1 };
        public static PipeCellState CreatePipe(int capacity) => new(capacity);

        /// <summary>
        /// Add flow to this cell. Returns true if still stable, false if burst.
        /// Mutations persist because this is a class.
        /// </summary>
        public bool AddFlow(int amount, int colorIndex)
        {
            CurrentFlow += amount;

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

            // Check overflow
            if (CurrentFlow > Capacity)
            {
                OverloadTicks++;
                State = OverloadState.Overloading;
                if (OverloadTicks >= 1)
                {
                    State = OverloadState.Burst;
                    return false; // Burst!
                }
            }

            return true; // Still stable
        }

        public bool IsStable => State != OverloadState.Burst;
        public bool IsOverloading => State == OverloadState.Overloading;
    }
}
