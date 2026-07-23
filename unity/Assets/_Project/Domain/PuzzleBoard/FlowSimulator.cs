using System;
using System.Collections.Generic;
using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    /// <summary>
    /// Turn-based flow simulation engine.
    /// Flow propagates from sources outward one cell per tick through
    /// placed pipes. Handles capacity, overflow/burst, color mixing,
    /// flow gates, and win/lose detection.
    ///
    /// Usage (from MonoBehaviour):
    ///   _sim.StartSimulation(board, level);
    ///   while (_sim.GetResult() == SimulationResult.InProgress)
    ///       yield return new WaitForSeconds(0.3f); _sim.Tick();
    /// </summary>
    public class FlowSimulator : IFlowSimulator
    {
        public event Action<int, int, int> OnFlowAdvance;
        public event Action<int, int> OnPipeBurst;
        public event Action<int, int, int, int> OnColorMix;
        public event Action<int, int, int> OnTargetReached;

        public bool IsRunning
        {
            get
            {
                if (_result != SimulationResult.InProgress) return false;
                return _isRunning;
            }
            private set => _isRunning = value;
        }
        public int CurrentTick { get; private set; }
        public int TargetsReached => _reachedTargets.Count;

        private IBoardState _board;
        private LevelData _level;
        private PipeCellState[,] _cellStates;
        private SimulationResult _result;
        private bool _isRunning;
        private HashSet<(int x, int y)> _reachedTargets;
        private Dictionary<(int, int), int> _pipeCapacityMap = new(); // (x, y) → capacity (from placed piece)

        // Active wave fronts: each is (x, y, colorIndex)
        private List<Wave> _activeWaves;
        // Visited: (x, y, colorIndex) — prevents re-entering same cell with same color
        private HashSet<(int x, int y, int colorIndex)> _visited;

        private static readonly (int dx, int dy)[] Directions =
            { (0, 1), (0, -1), (1, 0), (-1, 0) };

        private struct Wave
        {
            public int X, Y, ColorIndex;
            public int SourceIndex; // Which source emitted this wave
        }

        public void StartSimulation(IBoardState board, LevelData level)
        {
            StartSimulation(board, level, null);
        }

        public void StartSimulation(IBoardState board, LevelData level, PipeInventory inventory)
        {
            _board = board;
            _level = level;
            _cellStates = new PipeCellState[board.Width, board.Height];
            _pipeCapacityMap = new Dictionary<(int, int), int>();
            _reachedTargets = new HashSet<(int x, int y)>();
            _activeWaves = new List<Wave>();
            _visited = new HashSet<(int x, int y, int colorIndex)>();
            _result = SimulationResult.InProgress;
            _isRunning = true;
            CurrentTick = 0;

            // Initialize per-cell capacities from board state
            // Default: each pipe cell has capacity 2 until overridden
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    var cell = board.GetCell(x, y);
                    if (cell.Type == CellType.Pipe)
                    {
                        // Look up actual capacity from placed piece, default to 2
                        int cap = 2;
                        if (inventory != null)
                        {
                            var piece = inventory.GetPieceAt(x, y);
                            if (piece != null)
                                cap = piece.Capacity > 0 ? piece.Capacity : 2;
                        }
                        _pipeCapacityMap[(x, y)] = cap;
                        _cellStates[x, y] = new PipeCellState(cap);
                    }
                    else
                    {
                        _cellStates[x, y] = new PipeCellState();
                    }
                }

            // Seed wave fronts from each source
            for (int si = 0; si < _level.Sources.Length; si++)
            {
                var src = _level.Sources[si];
                int fx = src.X, fy = src.Y, color = src.ColorIndex;
                int pressure = src.FlowPressure > 0 ? src.FlowPressure : 1;

                // Mark source cell as visited for this color
                _visited.Add((fx, fy, color));

                // Emit waves from source to adjacent pipe/target cells
                EmitFromSource(si, fx, fy, color, pressure);
            }
        }

        /// <summary>
        /// Emit waves from a source cell into adjacent pipe/target/flowgate cells.
        /// </summary>
        private void EmitFromSource(int sourceIndex, int sx, int sy, int color, int pressure)
        {
            // For level simplicity, each source emits one wave per tick.
            // Pressure > 1 means it emits that many waves in the first tick
            // (increasing flow through downstream pipes).
            for (int p = 0; p < pressure; p++)
            {
                foreach (var (dx, dy) in Directions)
                {
                    int nx = sx + dx, ny = sy + dy;
                    if (!_board.IsValidPosition(nx, ny)) continue;

                    var cell = _board.GetCell(nx, ny);
                    var visitKey = (nx, ny, color);

                    if (_visited.Contains(visitKey)) continue;
                    if (cell.Type == CellType.Obstacle) continue;
                    if (_cellStates[nx, ny].State == OverloadState.Burst) continue;

                    if (cell.Type == CellType.Pipe || cell.Type == CellType.FlowGate)
                    {
                        _visited.Add(visitKey);
                        _cellStates[nx, ny].Capacity = GetCapacity(nx, ny);

                        bool stable = _cellStates[nx, ny].AddFlow(1, color);
                        if (!stable) continue; // Burst on first tick — edge case

                        OnFlowAdvance?.Invoke(nx, ny, color);
                        _activeWaves.Add(new Wave { X = nx, Y = ny, ColorIndex = color, SourceIndex = sourceIndex });
                    }
                    else if (cell.Type == CellType.Target && cell.ColorIndex == color)
                    {
                        _visited.Add(visitKey);
                        ReachTarget(nx, ny, color);
                    }
                }
            }
        }

        public void Tick()
        {
            if (_result != SimulationResult.InProgress) return;
            CurrentTick++;

            var nextWaves = new List<Wave>();
            var wavesToAdd = new List<Wave>(); // For bursts, we may add contaminant waves

            foreach (var wave in _activeWaves)
            {
                int cx = wave.X, cy = wave.Y, color = wave.ColorIndex;

                foreach (var (dx, dy) in Directions)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!_board.IsValidPosition(nx, ny)) continue;

                    var cell = _board.GetCell(nx, ny);
                    var visitKey = (nx, ny, color);

                    if (_visited.Contains(visitKey)) continue;
                    if (cell.Type == CellType.Obstacle) continue;
                    if (_cellStates[nx, ny].State == OverloadState.Burst) continue;

                    // ── Flow Gate: only allow if direction matches ──
                    if (cell.Type == CellType.FlowGate)
                    {
                        // Flow must enter FROM the correct direction
                        PipeDirection requiredEntry = cell.FlowDirection;
                        PipeDirection actualEntry = DirectionFromDelta(dx, dy);

                        // Reverse: if gate points Right, flow must enter from Left (dx=-1)
                        if (!IsValidGateEntry(requiredEntry, dx, dy)) continue;

                        // Pass through gate
                        _visited.Add(visitKey);
                        OnFlowAdvance?.Invoke(nx, ny, color);
                        nextWaves.Add(new Wave { X = nx, Y = ny, ColorIndex = color, SourceIndex = wave.SourceIndex });
                        continue;
                    }

                    // ── Pipe cell: add flow ──
                    if (cell.Type == CellType.Pipe)
                    {
                        _visited.Add(visitKey);

                        // Get or init cell state
                        int cap = GetCapacity(nx, ny);
                        if (_cellStates[nx, ny].Capacity == 0)
                            _cellStates[nx, ny] = new PipeCellState(cap);

                        // Add flow — track previous state for color mix detection
                        int prevMixedCount = _cellStates[nx, ny].MixedColorCount;
                        bool stable = _cellStates[nx, ny].AddFlow(1, color);

                        OnFlowAdvance?.Invoke(nx, ny, color);

                        // Detect color mixing
                        int newMixedCount = _cellStates[nx, ny].MixedColorCount;
                        if (newMixedCount == 2 && prevMixedCount < 2)
                        {
                            int a = _cellStates[nx, ny].MixedColorA;
                            int b = _cellStates[nx, ny].MixedColorB;
                            OnColorMix?.Invoke(nx, ny, a, b);
                        }

                        if (!stable)
                        {
                            // BURST!
                            OnPipeBurst?.Invoke(nx, ny);
                            continue; // Don't propagate from burst pipe
                        }

                        nextWaves.Add(new Wave { X = nx, Y = ny, ColorIndex = color, SourceIndex = wave.SourceIndex });
                    }
                    // ── Target: check match ──
                    else if (cell.Type == CellType.Target)
                    {
                        if (cell.ColorIndex == color)
                        {
                            _visited.Add(visitKey);
                            ReachTarget(nx, ny, color);
                        }
                        // Could also check mixed colors matching target
                        else if (_cellStates[cx, cy].MixedColorCount >= 1)
                        {
                            int mixedResult = _cellStates[cx, cy].ResultColor;
                            if (mixedResult == cell.ColorIndex && cell.ColorIndex != -1)
                            {
                                _visited.Add(visitKey);
                                ReachTarget(nx, ny, mixedResult);
                            }
                        }
                    }
                } // foreach direction
            } // foreach wave

            _activeWaves = nextWaves;

            // ── Termination checks ──
            if (_reachedTargets.Count >= _level.Targets.Length)
            {
                _result = SimulationResult.AllTargetsReached;
                _isRunning = false;
            }
            else if (_activeWaves.Count == 0)
            {
                _result = SimulationResult.FlowStopped;
                _isRunning = false;
            }
        }

        private void ReachTarget(int x, int y, int color)
        {
            if (!_reachedTargets.Contains((x, y)))
            {
                _reachedTargets.Add((x, y));
                OnTargetReached?.Invoke(x, y, color);
            }
        }

        private int GetCapacity(int x, int y)
        {
            if (_pipeCapacityMap.TryGetValue((x, y), out int cap))
                return cap;
            cap = 2; // default capacity
            _pipeCapacityMap[(x, y)] = cap;
            return cap;
        }

        /// <summary>
        /// Set the capacity for a specific pipe cell (called when a piece is placed from inventory).
        /// </summary>
        public void SetPipeCapacity(int x, int y, int capacity)
        {
            _pipeCapacityMap[(x, y)] = capacity;
        }

        /// <summary>
        /// Set the piece shape for a specific pipe cell (affects flow propagation).
        /// Not used in v1 but reserves the slot for directed flow.
        /// </summary>
        public void SetPipeShape(int x, int y, PieceShape shape)
        {
            // Reserved for v2: T-junctions split flow, valves block backflow, etc.
        }

        // ── Flow Gate helpers ──

        private static PipeDirection DirectionFromDelta(int dx, int dy)
        {
            if (dx == 0 && dy == 1) return PipeDirection.Up;
            if (dx == 0 && dy == -1) return PipeDirection.Down;
            if (dx == 1 && dy == 0) return PipeDirection.Right;
            if (dx == -1 && dy == 0) return PipeDirection.Left;
            return PipeDirection.None;
        }

        /// <summary>
        /// Check if flow entering from (dx, dy) is valid for a gate pointing in `requiredEntry`.
        /// Flow enters from the OPPOSITE direction of the gate's arrow.
        /// Example: Gate points Right → flow must enter from the Left (dx = -1).
        /// </summary>
        private static bool IsValidGateEntry(PipeDirection gateDirection, int dx, int dy)
        {
            return gateDirection switch
            {
                PipeDirection.Right => dx == -1, // Enter from left
                PipeDirection.Left => dx == 1,    // Enter from right
                PipeDirection.Up => dy == -1,     // Enter from bottom
                PipeDirection.Down => dy == 1,    // Enter from top
                _ => true
            };
        }

        public void StopSimulation()
        {
            _result = SimulationResult.PlayerStopped;
            _isRunning = false;
        }

        public SimulationResult GetResult() => _result;

        public PipeCellState GetCellState(int x, int y)
        {
            if (_board == null || !_board.IsValidPosition(x, y))
                return new PipeCellState();
            return _cellStates[x, y];
        }

        public bool IsTargetReached(int targetX, int targetY)
            => _reachedTargets.Contains((targetX, targetY));
    }
}
