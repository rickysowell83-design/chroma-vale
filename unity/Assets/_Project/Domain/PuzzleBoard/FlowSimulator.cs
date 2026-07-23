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
    /// SHAPE-AWARE FLOW: Each pipe piece shape + rotation defines which
    /// directions accept INPUT and which produce OUTPUT. Flow only
    /// propagates when both cells agree on the connection.
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

        // Shape info per cell: (shape, valveDirection, rotationDegrees)
        // rotationDegrees is 0, 90, 180, or 270 (clockwise)
        private Dictionary<(int, int), (PieceShape shape, PipeDirection direction, int rotation)> _pipeShapeMap = new();

        // Cells flagged as Mixers — allow color mixing at this cell
        private HashSet<(int, int)> _mixerCells;

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

        // ── Direction bit-flag constants ──
        private const uint UpFlag    = 1; // 1 << 0
        private const uint DownFlag  = 2; // 1 << 1
        private const uint LeftFlag  = 4; // 1 << 2
        private const uint RightFlag = 8; // 1 << 3
        private const uint AllFlags  = UpFlag | DownFlag | LeftFlag | RightFlag;

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
            _pipeShapeMap = new Dictionary<(int, int), (PieceShape, PipeDirection, int)>();
            _mixerCells = new HashSet<(int, int)>();
            _reachedTargets = new HashSet<(int x, int y)>();
            _activeWaves = new List<Wave>();
            _visited = new HashSet<(int x, int y, int colorIndex)>();
            _result = SimulationResult.InProgress;
            _isRunning = true;
            CurrentTick = 0;

            // Initialize per-cell capacities from board state
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    var cell = board.GetCell(x, y);
                    if (cell.Type == CellType.Pipe)
                    {
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

            // Initialize piece shapes from inventory (restore shapes for restart/load)
            if (inventory != null)
            {
                for (int x = 0; x < board.Width; x++)
                    for (int y = 0; y < board.Height; y++)
                    {
                        if (_board.GetCell(x, y).Type == CellType.Pipe)
                        {
                            var piece = inventory.GetPieceAt(x, y);
                            if (piece != null)
                            {
                                _pipeShapeMap[(x, y)] = (piece.Shape, piece.Direction, piece.Rotation);
                            }
                        }
                    }
            }

            // Apply Amplifier adjacency boosts and flag Mixer cells
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    if (_pipeShapeMap.TryGetValue((x, y), out var shapeData))
                    {
                        if (shapeData.shape == PieceShape.Amplifier)
                            ApplyAmplifierBoost(x, y);
                        else if (shapeData.shape == PieceShape.Mixer)
                            _mixerCells.Add((x, y));
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
        /// Respects neighbor cell shape connections.
        /// </summary>
        private void EmitFromSource(int sourceIndex, int sx, int sy, int color, int pressure)
        {
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
                        // ── Shape-aware entry check for Pipe cells ──
                        if (cell.Type == CellType.Pipe)
                        {
                            PipeDirection neighborEntryDir = OppositeDirection(DirectionFromDelta(dx, dy));
                            if (!CanEnterCell(nx, ny, neighborEntryDir)) continue;
                        }

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

            foreach (var wave in _activeWaves)
            {
                int cx = wave.X, cy = wave.Y, color = wave.ColorIndex;

                foreach (var (dx, dy) in Directions)
                {
                    PipeDirection exitDir = DirectionFromDelta(dx, dy);

                    // ── CHECK 1: Can flow EXIT the current cell in this direction? ──
                    if (!CanExitCell(cx, cy, exitDir)) continue;

                    int nx = cx + dx, ny = cy + dy;
                    if (!_board.IsValidPosition(nx, ny)) continue;

                    var cell = _board.GetCell(nx, ny);
                    var visitKey = (nx, ny, color);

                    if (_visited.Contains(visitKey)) continue;
                    if (cell.Type == CellType.Obstacle) continue;
                    if (_cellStates[nx, ny].State == OverloadState.Burst) continue;

                    // ── CHECK 2: Can flow ENTER the neighbor from the opposite direction? ──
                    PipeDirection neighborEntryDir = OppositeDirection(exitDir);

                    // ── Flow Gate: uses built-in direction check ──
                    if (cell.Type == CellType.FlowGate)
                    {
                        PipeDirection requiredEntry = cell.FlowDirection;
                        if (!IsValidGateEntry(requiredEntry, dx, dy)) continue;

                        _visited.Add(visitKey);
                        OnFlowAdvance?.Invoke(nx, ny, color);
                        nextWaves.Add(new Wave { X = nx, Y = ny, ColorIndex = color, SourceIndex = wave.SourceIndex });
                        continue;
                    }

                    // ── Pipe cell: add flow ──
                    if (cell.Type == CellType.Pipe)
                    {
                        if (!CanEnterCell(nx, ny, neighborEntryDir)) continue;

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

        // ────────────────────────────────────────────────────────────────
        // SHAPE-AWARE CONNECTION MAP SYSTEM
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Map a PipeDirection to a bit flag for fast set operations.
        /// </summary>
        private static uint DirectionToFlag(PipeDirection dir)
        {
            switch (dir)
            {
                case PipeDirection.Up:    return UpFlag;
                case PipeDirection.Down:  return DownFlag;
                case PipeDirection.Left:  return LeftFlag;
                case PipeDirection.Right: return RightFlag;
                default:                  return 0;
            }
        }

        /// <summary>
        /// Return the direction opposite to the given one.
        /// </summary>
        private static PipeDirection OppositeDirection(PipeDirection dir)
        {
            switch (dir)
            {
                case PipeDirection.Up:    return PipeDirection.Down;
                case PipeDirection.Down:  return PipeDirection.Up;
                case PipeDirection.Left:  return PipeDirection.Right;
                case PipeDirection.Right: return PipeDirection.Left;
                default:                  return PipeDirection.None;
            }
        }

        /// <summary>
        /// Rotate a bit-flag direction set N steps clockwise (each step = 90°).
        /// </summary>
        private static uint RotateBitsCW(uint bits, int steps)
        {
            steps = steps % 4;
            if (steps == 0) return bits;
            for (int i = 0; i < steps; i++)
                bits = ((bits & 1) << 3) | ((bits & 8) >> 2) | ((bits & 2) << 1) | ((bits & 4) >> 2);
            return bits;
        }

        /// <summary>
        /// Get the bit-flag set of directions a shape+rotation accepts as INPUT.
        /// </summary>
        private static uint GetInputFlags(PieceShape shape, PipeDirection valveDir, int rotationDegrees)
        {
            uint baseFlags;
            switch (shape)
            {
                case PieceShape.Straight:    baseFlags = LeftFlag | RightFlag; break;  // ← →
                case PieceShape.Elbow:       baseFlags = UpFlag | LeftFlag;    break;  // ↑ ←
                case PieceShape.TJunction:   baseFlags = LeftFlag | RightFlag | UpFlag; break; // ← → ↑
                case PieceShape.Cross:       baseFlags = AllFlags;             break;
                case PieceShape.Valve:       baseFlags = DirectionToFlag(OppositeDirection(valveDir)); break;
                case PieceShape.Amplifier:   baseFlags = AllFlags;             break;
                case PieceShape.Mixer:       baseFlags = AllFlags;             break;
                case PieceShape.Blocker:     baseFlags = 0;                    break;
                default:                     baseFlags = AllFlags;             break; // backward compat
            }
            // Valve direction is absolute — rotation does not change it
            if (shape == PieceShape.Valve) return baseFlags;
            // Cross, Amplifier, Mixer, Blocker all yield AllFlags or 0 regardless of rotation
            return RotateBitsCW(baseFlags, rotationDegrees / 90);
        }

        /// <summary>
        /// Get the bit-flag set of directions a shape+rotation produces as OUTPUT.
        /// </summary>
        private static uint GetOutputFlags(PieceShape shape, PipeDirection valveDir, int rotationDegrees)
        {
            uint baseFlags;
            switch (shape)
            {
                case PieceShape.Straight:    baseFlags = LeftFlag | RightFlag; break;  // ← →
                case PieceShape.Elbow:       baseFlags = DownFlag | RightFlag; break;  // ↓ →
                case PieceShape.TJunction:   baseFlags = LeftFlag | RightFlag | DownFlag; break; // ← → ↓
                case PieceShape.Cross:       baseFlags = AllFlags;             break;
                case PieceShape.Valve:       baseFlags = DirectionToFlag(valveDir); break;
                case PieceShape.Amplifier:   baseFlags = AllFlags;             break;
                case PieceShape.Mixer:       baseFlags = AllFlags;             break;
                case PieceShape.Blocker:     baseFlags = 0;                    break;
                default:                     baseFlags = AllFlags;             break; // backward compat
            }
            if (shape == PieceShape.Valve) return baseFlags;
            return RotateBitsCW(baseFlags, rotationDegrees / 90);
        }

        /// <summary>
        /// Can flow exit cell (x,y) in the given direction?
        /// Cells with no shape info default to omnidirectional (backward compat).
        /// </summary>
        private bool CanExitCell(int x, int y, PipeDirection dir)
        {
            if (!_pipeShapeMap.TryGetValue((x, y), out var info))
                return true;
            uint flags = GetOutputFlags(info.shape, info.direction, info.rotation);
            return (flags & DirectionToFlag(dir)) != 0;
        }

        /// <summary>
        /// Can flow enter cell (x,y) from the given direction?
        /// Cells with no shape info default to omnidirectional (backward compat).
        /// </summary>
        private bool CanEnterCell(int x, int y, PipeDirection dir)
        {
            if (!_pipeShapeMap.TryGetValue((x, y), out var info))
                return true;
            uint flags = GetInputFlags(info.shape, info.direction, info.rotation);
            return (flags & DirectionToFlag(dir)) != 0;
        }

        // ────────────────────────────────────────────────────────────────

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
        /// Set the piece shape, direction, and rotation for a specific pipe cell.
        /// Controls how flow propagates through the cell.
        /// Also applies Amplifier adjacency boost and flags Mixer cells on placement.
        /// </summary>
        public void SetPipeShape(int x, int y, PieceShape shape, PipeDirection direction, int rotation)
        {
            _pipeShapeMap[(x, y)] = (shape, direction, rotation);

            // ── Amplifier: boost adjacent pipe cells on placement ──
            if (shape == PieceShape.Amplifier)
            {
                ApplyAmplifierBoost(x, y);
            }
            // ── Mixer: flag this cell as a mixing zone ──
            else if (shape == PieceShape.Mixer)
            {
                if (_mixerCells != null)
                    _mixerCells.Add((x, y));
            }
        }

        /// <summary>
        /// Increment capacity of all 4 adjacent pipe cells by 1.
        /// Called when an Amplifier is placed (or on simulation restart).
        /// </summary>
        private void ApplyAmplifierBoost(int x, int y)
        {
            foreach (var (dx, dy) in Directions)
            {
                int nx = x + dx, ny = y + dy;
                if (_board != null && !_board.IsValidPosition(nx, ny)) continue;
                if (_pipeCapacityMap.ContainsKey((nx, ny)))
                {
                    _pipeCapacityMap[(nx, ny)] += 1;
                    // Also update live cell state capacity if simulation is running
                    if (_cellStates != null && _board != null && _board.IsValidPosition(nx, ny))
                    {
                        if (_cellStates[nx, ny].Capacity > 0)
                            _cellStates[nx, ny].Capacity += 1;
                    }
                }
            }
        }

        /// <summary>
        /// Check whether a cell is flagged as a Mixer zone.
        /// Mixer cells allow color mixing even if it would otherwise not be possible.
        /// </summary>
        public bool IsMixerCell(int x, int y)
        {
            return _mixerCells != null && _mixerCells.Contains((x, y));
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
