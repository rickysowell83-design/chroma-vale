using System;

namespace ChromaVale.Core.GameLogic
{
    public enum SimulationResult
    {
        InProgress,         // Simulation still running
        AllTargetsReached,  // WIN — every target has been reached by matching signal
        SignalStuck,        // STUCK — no more signal can advance; some targets unreached
        PlayerStopped       // Player manually stopped simulation
    }

    /// <summary>
    /// Turn-based signal propagation engine interface.
    /// Pure C# — no Unity dependencies. Fully unit-testable.
    ///
    /// Usage:
    ///   1. StartSimulation(board, level)
    ///   2. Call Tick() repeatedly (e.g., 0.3s intervals via MonoBehaviour)
    ///   3. Subscribe to events for visual updates
    ///   4. Check GetResult() for termination state
    /// </summary>
    public interface ISignalRouter
    {
        /// <summary>Raised each tick when signal advances into a cell.</summary>
        event Action<int, int, int> OnSignalAdvance;

        /// <summary>Raised when a trace short circuits (overloaded). That cell is now impassable.</summary>
        event Action<int, int> OnTraceShort;

        /// <summary>Raised when two colors mix at a cell. (x, y, colorA, colorB)</summary>
        event Action<int, int, int, int> OnColorMix;

        /// <summary>Raised when signal reaches a target and activates it.</summary>
        event Action<int, int, int> OnTargetReached;

        /// <summary>Whether simulation is actively running.</summary>
        bool IsRunning { get; }

        /// <summary>Current simulation tick number.</summary>
        int CurrentTick { get; }

        /// <summary>
        /// Initialize and start the simulation.
        /// Must be called before Tick().
        /// </summary>
        void StartSimulation(IBoardState board, LevelData level);

        /// <summary>
        /// Advance signal by one tick. Signal propagates from each source
        /// outward through traces. Fires events for each change.
        /// </summary>
        void Tick();

        /// <summary>
        /// Manually stop the simulation (e.g., player pressed STOP).
        /// </summary>
        void StopSimulation();

        /// <summary>
        /// Get the current result / termination state.
        /// </summary>
        SimulationResult GetResult();

        /// <summary>
        /// Get the current TraceCellState for a position (for UI inspection).
        /// </summary>
        TraceCellState GetCellState(int x, int y);

        /// <summary>
        /// Get whether a specific target has been reached by matching signal.
        /// </summary>
        bool IsTargetReached(int targetX, int targetY);

        /// <summary>
        /// Number of targets that have been reached so far.
        /// </summary>
        int TargetsReached { get; }
    }
}
