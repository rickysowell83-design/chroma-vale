using System;
using System.Collections.Generic;
using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    /// <summary>
    /// Turn-based flow simulation engine.
    /// Signal propagates from sources outward one cell per tick through
    /// placed traces. Handles capacity, overflow/burst, color mixing,
    /// signal gates, and win/lose detection.
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
    public class SignalRouter : ISignalRouter
    {
        public event Action<int, int, int> OnSignalAdvance;
        public event Action<int, int> OnTraceShort;
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
        private TraceCellState[,] _cellStates;
        private SimulationResult _result;
        private bool _isRunning;
        private HashSet<(int x, int y)> _reachedTargets;
        private Dictionary<(int, int), int> _traceCapacityMap = new(); // (x, y) → capacity (from placed piece)

        // Shape info per cell: (shape, valveDirection, rotationDegrees)
        // rotationDegrees is 0, 90, 180, or 270 (clockwise)
        private Dictionary<(int, int), (SegmentShape shape, TraceDirection direction, int rotation)> _traceShapeMap = new();

        // Cells flagged as Mixers — allow color mixing at this cell
        private HashSet<(int, int)> _mixerCells;

        // Impedance map — resistance cost per cell
        private Dictionary<(int, int), int> _impedanceMap = new();

        // Ghost trace shape map — shapes from LevelData (not from inventory)
        private Dictionary<(int, int), (SegmentShape shape, TraceDirection direction, int rotation)> _ghostShapeMap = new();

        // Active wave fronts: each is (x, y, colorIndex)
        private List<Wave> _activeWaves;
        // Visited: (x, y, colorIndex) — prevents re-entering same cell with same color
        private HashSet<(int x, int y, int colorIndex)> _visited;

        private static readonly (int dx, int dy)[] Directions =
            { (0, 1), (0, -1), (1, 0), (-1, 0) };

        private struct Wave
        {
            public int X, Y, ColorIndex;
            public int SourceIndex;         // Which source emitted this wave
            public int Pressure;            // Units of signal this wave carries (for capacity)
            public int SignalStrength;      // Remaining signal strength (for impedance decay)
            public TraceDirection CameFrom;  // Side this wave entered through (blocks backflow)
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

        public void StartSimulation(IBoardState board, LevelData level, TraceInventory inventory)
        {
            _board = board;
            _level = level;
            _cellStates = new TraceCellState[board.Width, board.Height];
            _traceCapacityMap = new Dictionary<(int, int), int>();
            _traceShapeMap = new Dictionary<(int, int), (SegmentShape, TraceDirection, int)>();
            _mixerCells = new HashSet<(int, int)>();
            _impedanceMap = new Dictionary<(int, int), int>();
            _ghostShapeMap = new Dictionary<(int, int), (SegmentShape, TraceDirection, int)>();
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
                    if (cell.Type == CellType.Trace)
                    {
                        int cap = 2;
                        if (inventory != null)
                        {
                            var piece = inventory.GetPieceAt(x, y);
                            if (piece != null)
                                cap = piece.Capacity > 0 ? piece.Capacity : 2;
                        }
                        _traceCapacityMap[(x, y)] = cap;
                        _cellStates[x, y] = new TraceCellState(cap);
                    }
                    else
                    {
                        _cellStates[x, y] = new TraceCellState();
                    }
                }

            // Initialize piece shapes from inventory (restore shapes for restart/load)
            if (inventory != null)
            {
                for (int x = 0; x < board.Width; x++)
                    for (int y = 0; y < board.Height; y++)
                    {
                        if (_board.GetCell(x, y).Type == CellType.Trace)
                        {
                            var piece = inventory.GetPieceAt(x, y);
                            if (piece != null)
                            {
                                _traceShapeMap[(x, y)] = (piece.Shape, piece.Direction, piece.Rotation);
                            }
                        }
                    }
            }

            // ── Register ghost traces from LevelData ──
            if (_level.GhostTraces != null)
            {
                foreach (var gt in _level.GhostTraces)
                {
                    _ghostShapeMap[(gt.X, gt.Y)] = (gt.Shape, TraceDirection.None, gt.Rotation);
                    _traceCapacityMap[(gt.X, gt.Y)] = gt.Capacity;
                    _cellStates[gt.X, gt.Y] = new TraceCellState(gt.Capacity);
                }
            }

            // ── Register impedance cells from LevelData ──
            if (_level.ImpedanceCells != null)
            {
                foreach (var ic in _level.ImpedanceCells)
                {
                    _impedanceMap[(ic.X, ic.Y)] = ic.ResistanceCost;
                }
            }

            // Apply Amplifier adjacency boosts and flag Mixer cells
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    if (_traceShapeMap.TryGetValue((x, y), out var shapeData))
                    {
                        if (shapeData.shape == SegmentShape.Repeater)
                            ApplyAmplifierBoost(x, y);
                        else if (shapeData.shape == SegmentShape.Combiner)
                            _mixerCells.Add((x, y));
                    }
                }

            // Seed wave fronts from each source
            for (int si = 0; si < _level.Sources.Length; si++)
            {
                var src = _level.Sources[si];
                int fx = src.X, fy = src.Y, color = src.ColorIndex;
                int pressure = src.SignalStrength > 0 ? src.SignalStrength : 1;
                int signalStrength = _level.SignalStrength > 0 ? _level.SignalStrength : pressure;

                // Mark source cell as visited for this color
                _visited.Add((fx, fy, color));

                // Emit waves from source to adjacent pipe/target cells
                EmitFromSource(si, fx, fy, color, pressure, signalStrength);
            }
        }

        /// <summary>
        /// Emit waves from a source cell into adjacent pipe/target/flowgate cells.
        /// Respects neighbor cell shape connections.
        /// </summary>
        private void EmitFromSource(int sourceIndex, int sx, int sy, int color, int pressure, int signalStrength)
        {
            foreach (var (dx, dy) in Directions)
            {
                int nx = sx + dx, ny = sy + dy;
                if (!_board.IsValidPosition(nx, ny)) continue;

                var cell = _board.GetCell(nx, ny);
                var visitKey = (nx, ny, color);

                if (_visited.Contains(visitKey)) continue;
                if (cell.Type == CellType.Obstacle) continue;
                if (_cellStates[nx, ny].State == CircuitState.Shorted) continue;

                if (cell.Type == CellType.Trace || cell.Type == CellType.SignalGate)
                {
                    // ── Shape-aware entry check for Trace cells ──
                    if (cell.Type == CellType.Trace)
                    {
                        TraceDirection neighborEntryDir = OppositeDirection(DirectionFromDelta(dx, dy));
                        if (!CanEnterCell(nx, ny, neighborEntryDir)) continue;
                    }

                    _visited.Add(visitKey);
                    _cellStates[nx, ny].Capacity = GetCapacity(nx, ny);

                    // The wave carries the source's FULL pressure — this is what
                    // makes capacity planning real. flow > capacity ⇒ burst.
                    int prevMixedCount = _cellStates[nx, ny].MixedColorCount;
                    bool stable = _cellStates[nx, ny].AddSignal(pressure, color);
                    OnSignalAdvance?.Invoke(nx, ny, color);

                    // Mixing can happen right next to sources (two sources feeding
                    // one cell) — fire the event here too, not just in Tick().
                    if (_cellStates[nx, ny].MixedColorCount == 2 && prevMixedCount < 2)
                        OnColorMix?.Invoke(nx, ny, _cellStates[nx, ny].MixedColorA, _cellStates[nx, ny].MixedColorB);

                    if (!stable)
                    {
                        OnTraceShort?.Invoke(nx, ny);
                        continue; // Burst on first tick — don't propagate
                    }

                    _activeWaves.Add(new Wave
                    {
                        X = nx, Y = ny, ColorIndex = color, SourceIndex = sourceIndex,
                        Pressure = pressure,
                        SignalStrength = signalStrength, // Impedance counter (not pressure)
                        CameFrom = OppositeDirection(DirectionFromDelta(dx, dy))
                    });
                }
                else if (cell.Type == CellType.Target && cell.ColorIndex == color)
                {
                    _visited.Add(visitKey);
                    ReachTarget(nx, ny, color);
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
                int pressure = wave.Pressure > 0 ? wave.Pressure : 1;

                // ── Pass 1: find all valid exit branches for this wave ──
                // Pressure divides across branches (the "pressure math" mechanic:
                // a T-Junction splits p2 into two p1 streams). Only cells that can
                // actually ACCEPT flow count as branches — empty cells don't absorb pressure.
                var branches = new List<(int dx, int dy)>();
                foreach (var (dx, dy) in Directions)
                {
                    TraceDirection exitDir = DirectionFromDelta(dx, dy);

                    // Never flow back out the side we came in (prevents 1-cell backwash)
                    if (exitDir == wave.CameFrom) continue;
                    if (!CanExitCell(cx, cy, exitDir)) continue;

                    int px = cx + dx, py = cy + dy;
                    if (!_board.IsValidPosition(px, py)) continue;
                    var pcell = _board.GetCell(px, py);
                    if (pcell.Type == CellType.Obstacle) continue;
                    if (_cellStates[px, py].State == CircuitState.Shorted) continue;
                    if (_visited.Contains((px, py, color))) continue;

                    if (pcell.Type == CellType.Trace)
                    {
                        if (!CanEnterCell(px, py, OppositeDirection(exitDir))) continue;
                    }
                    else if (pcell.Type == CellType.SignalGate)
                    {
                        if (!IsValidGateEntry(pcell.SignalDirection, dx, dy)) continue;
                    }
                    else if (pcell.Type != CellType.Target)
                    {
                        continue; // Empty / Source cells are not flow branches
                    }

                    branches.Add((dx, dy));
                }

                // ── Pass 2: propagate, distributing pressure across branches ──
                for (int bi = 0; bi < branches.Count; bi++)
                {
                    var (dx, dy) = branches[bi];
                    TraceDirection exitDir = DirectionFromDelta(dx, dy);
                    int nx = cx + dx, ny = cy + dy;

                    // Even split; remainder goes to earliest branches; minimum 1.
                    int branchPressure = pressure / branches.Count + (bi < pressure % branches.Count ? 1 : 0);
                    if (branchPressure < 1) branchPressure = 1;

                    var cell = _board.GetCell(nx, ny);
                    var visitKey = (nx, ny, color);
                    if (_visited.Contains(visitKey)) continue;

                    // ── CHECK 2: Can flow ENTER the neighbor from the opposite direction? ──
                    TraceDirection neighborEntryDir = OppositeDirection(exitDir);

                    // ── Flow Gate: uses built-in direction check ──
                    if (cell.Type == CellType.SignalGate)
                    {
                        TraceDirection requiredEntry = cell.SignalDirection;
                        if (!IsValidGateEntry(requiredEntry, dx, dy)) continue;

                        _visited.Add(visitKey);
                        OnSignalAdvance?.Invoke(nx, ny, color);
                        nextWaves.Add(new Wave
                        {
                            X = nx, Y = ny, ColorIndex = color, SourceIndex = wave.SourceIndex,
                            Pressure = pressure, SignalStrength = wave.SignalStrength, CameFrom = neighborEntryDir
                        });
                        continue;
                    }

                    // ── Trace cell: add flow ──
                    if (cell.Type == CellType.Trace)
                    {
                        if (!CanEnterCell(nx, ny, neighborEntryDir)) continue;

                        // ── Impedance check: decrement signal strength ──
                        int waveStrength = wave.SignalStrength;
                        if (_impedanceMap.TryGetValue((nx, ny), out int resistCost))
                        {
                            waveStrength -= resistCost;
                            if (waveStrength <= 0) continue; // Signal died — don't propagate
                        }

                        // ── Repeater: restore signal strength to full ──
                        if (_traceShapeMap.TryGetValue((nx, ny), out var cellShape) && cellShape.shape == SegmentShape.Repeater)
                        {
                            waveStrength = GetSourceSignalStrength(wave.SourceIndex);
                        }
                        else if (_ghostShapeMap.TryGetValue((nx, ny), out var ghostShape) && ghostShape.shape == SegmentShape.Repeater)
                        {
                            waveStrength = GetSourceSignalStrength(wave.SourceIndex);
                        }

                        _visited.Add(visitKey);

                        // Get or init cell state
                        int cap = GetCapacity(nx, ny);
                        if (_cellStates[nx, ny].Capacity == 0)
                            _cellStates[nx, ny] = new TraceCellState(cap);

                        // Add flow — track previous state for color mix detection
                        int prevMixedCount = _cellStates[nx, ny].MixedColorCount;
                        bool stable = _cellStates[nx, ny].AddSignal(branchPressure, color);

                        OnSignalAdvance?.Invoke(nx, ny, color);

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
                            // SHORT!
                            OnTraceShort?.Invoke(nx, ny);
                            continue; // Don't propagate from shorted trace
                        }

                        nextWaves.Add(new Wave
                        {
                            X = nx, Y = ny, ColorIndex = color, SourceIndex = wave.SourceIndex,
                            Pressure = branchPressure,
                            SignalStrength = waveStrength,
                            CameFrom = neighborEntryDir
                        });
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
                _result = SimulationResult.SignalStuck;
                _isRunning = false;
            }
        }

        // ────────────────────────────────────────────────────────────────
        // SHAPE-AWARE CONNECTION MAP SYSTEM
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Map a TraceDirection to a bit flag for fast set operations.
        /// </summary>
        private static uint DirectionToFlag(TraceDirection dir)
        {
            switch (dir)
            {
                case TraceDirection.Up:    return UpFlag;
                case TraceDirection.Down:  return DownFlag;
                case TraceDirection.Left:  return LeftFlag;
                case TraceDirection.Right: return RightFlag;
                default:                  return 0;
            }
        }

        /// <summary>
        /// Return the direction opposite to the given one.
        /// </summary>
        private static TraceDirection OppositeDirection(TraceDirection dir)
        {
            switch (dir)
            {
                case TraceDirection.Up:    return TraceDirection.Down;
                case TraceDirection.Down:  return TraceDirection.Up;
                case TraceDirection.Left:  return TraceDirection.Right;
                case TraceDirection.Right: return TraceDirection.Left;
                default:                  return TraceDirection.None;
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
        private static uint GetInputFlags(SegmentShape shape, TraceDirection valveDir, int rotationDegrees)
        {
            uint baseFlags;
            switch (shape)
            {
                case SegmentShape.Straight:    baseFlags = LeftFlag | RightFlag; break;  // ← →
                case SegmentShape.Corner:       baseFlags = UpFlag | LeftFlag;    break;  // ↑ ←
                case SegmentShape.Splitter:   baseFlags = LeftFlag | RightFlag | UpFlag; break; // ← → ↑
                case SegmentShape.CrossJunction:       baseFlags = AllFlags;             break;
                case SegmentShape.Diode:       baseFlags = DirectionToFlag(OppositeDirection(valveDir)); break;
                case SegmentShape.Repeater:   baseFlags = AllFlags;             break;
                case SegmentShape.Combiner:       baseFlags = AllFlags;             break;
                case SegmentShape.Breaker:     baseFlags = 0;                    break;
                default:                     baseFlags = AllFlags;             break; // backward compat
            }
            // Valve direction is absolute — rotation does not change it
            if (shape == SegmentShape.Diode) return baseFlags;
            // Cross, Amplifier, Mixer, Blocker all yield AllFlags or 0 regardless of rotation
            return RotateBitsCW(baseFlags, rotationDegrees / 90);
        }

        /// <summary>
        /// Get the bit-flag set of directions a shape+rotation produces as OUTPUT.
        /// </summary>
        private static uint GetOutputFlags(SegmentShape shape, TraceDirection valveDir, int rotationDegrees)
        {
            uint baseFlags;
            switch (shape)
            {
                case SegmentShape.Straight:    baseFlags = LeftFlag | RightFlag; break;  // ← →
                case SegmentShape.Corner:       baseFlags = DownFlag | RightFlag; break;  // ↓ →
                case SegmentShape.Splitter:   baseFlags = LeftFlag | RightFlag | DownFlag; break; // ← → ↓
                case SegmentShape.CrossJunction:       baseFlags = AllFlags;             break;
                case SegmentShape.Diode:       baseFlags = DirectionToFlag(valveDir); break;
                case SegmentShape.Repeater:   baseFlags = AllFlags;             break;
                case SegmentShape.Combiner:       baseFlags = AllFlags;             break;
                case SegmentShape.Breaker:     baseFlags = 0;                    break;
                default:                     baseFlags = AllFlags;             break; // backward compat
            }
            if (shape == SegmentShape.Diode) return baseFlags;
            return RotateBitsCW(baseFlags, rotationDegrees / 90);
        }

        /// <summary>
        /// Can flow exit cell (x,y) in the given direction?
        /// Cells with no shape info default to omnidirectional (backward compat).
        /// </summary>
        private bool CanExitCell(int x, int y, TraceDirection dir)
        {
            // Check player-placed shapes
            if (_traceShapeMap.TryGetValue((x, y), out var info))
            {
                uint flags = GetOutputFlags(info.shape, info.direction, info.rotation);
                return (flags & DirectionToFlag(dir)) != 0;
            }
            // Check ghost trace shapes
            if (_ghostShapeMap.TryGetValue((x, y), out var ghostInfo))
            {
                uint flags = GetOutputFlags(ghostInfo.shape, ghostInfo.direction, ghostInfo.rotation);
                return (flags & DirectionToFlag(dir)) != 0;
            }
            return true; // No shape info — omnidirectional
        }

        /// <summary>
        /// Can flow enter cell (x,y) from the given direction?
        /// Cells with no shape info default to omnidirectional (backward compat).
        /// </summary>
        private bool CanEnterCell(int x, int y, TraceDirection dir)
        {
            // Check player-placed shapes
            if (_traceShapeMap.TryGetValue((x, y), out var info))
            {
                uint flags = GetInputFlags(info.shape, info.direction, info.rotation);
                return (flags & DirectionToFlag(dir)) != 0;
            }
            // Check ghost trace shapes
            if (_ghostShapeMap.TryGetValue((x, y), out var ghostInfo))
            {
                uint flags = GetInputFlags(ghostInfo.shape, ghostInfo.direction, ghostInfo.rotation);
                return (flags & DirectionToFlag(dir)) != 0;
            }
            return true; // No shape info — omnidirectional
        }

        // ────────────────────────────────────────────────────────────────

        private void ReachTarget(int x, int y, int color)
        {
            if (!_reachedTargets.Contains((x, y)))
            {
                // ── Timing window check ──
                // Find the target in level data and check its accept window
                if (_level != null && _level.Targets != null)
                {
                    for (int ti = 0; ti < _level.Targets.Length; ti++)
                    {
                        var t = _level.Targets[ti];
                        if (t.X == x && t.Y == y && t.AcceptWindow.HasValue)
                        {
                            var w = t.AcceptWindow.Value;
                            if (CurrentTick < w.MinTick || CurrentTick > w.MaxTick)
                                return; // Outside accept window — target rejects signal
                            break;
                        }
                    }
                }

                _reachedTargets.Add((x, y));
                OnTargetReached?.Invoke(x, y, color);
            }
        }

        private int GetCapacity(int x, int y)
        {
            if (_traceCapacityMap.TryGetValue((x, y), out int cap))
                return cap;
            cap = 2; // default capacity
            _traceCapacityMap[(x, y)] = cap;
            return cap;
        }

        /// <summary>
        /// Get the original signal strength from the source that emitted this wave.
        /// Used by Repeater pieces to restore signal strength to full.
        /// </summary>
        private int GetSourceSignalStrength(int sourceIndex)
        {
            if (_level != null)
            {
                int ss = _level.SignalStrength;
                return ss > 0 ? ss : 1;
            }
            return 1;
        }

        /// <summary>
        /// Set the capacity for a specific trace cell (called when a piece is placed from inventory).
        /// </summary>
        public void SetTraceCapacity(int x, int y, int capacity)
        {
            _traceCapacityMap[(x, y)] = capacity;
        }

        /// <summary>
        /// Set the piece shape, direction, and rotation for a specific trace cell.
        /// Controls how flow propagates through the cell.
        /// Also applies Amplifier adjacency boost and flags Mixer cells on placement.
        /// </summary>
        public void SetTraceShape(int x, int y, SegmentShape shape, TraceDirection direction, int rotation)
        {
            _traceShapeMap[(x, y)] = (shape, direction, rotation);

            // ── Amplifier: boost adjacent trace cells on placement ──
            if (shape == SegmentShape.Repeater)
            {
                ApplyAmplifierBoost(x, y);
            }
            // ── Mixer: flag this cell as a mixing zone ──
            else if (shape == SegmentShape.Combiner)
            {
                if (_mixerCells != null)
                    _mixerCells.Add((x, y));
            }
        }

        /// <summary>
        /// Increment capacity of all 4 adjacent trace cells by 1.
        /// Called when an Amplifier is placed (or on simulation restart).
        /// </summary>
        private void ApplyAmplifierBoost(int x, int y)
        {
            foreach (var (dx, dy) in Directions)
            {
                int nx = x + dx, ny = y + dy;
                if (_board != null && !_board.IsValidPosition(nx, ny)) continue;
                if (_traceCapacityMap.ContainsKey((nx, ny)))
                {
                    _traceCapacityMap[(nx, ny)] += 1;
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

        private static TraceDirection DirectionFromDelta(int dx, int dy)
        {
            if (dx == 0 && dy == 1) return TraceDirection.Up;
            if (dx == 0 && dy == -1) return TraceDirection.Down;
            if (dx == 1 && dy == 0) return TraceDirection.Right;
            if (dx == -1 && dy == 0) return TraceDirection.Left;
            return TraceDirection.None;
        }

        /// <summary>
        /// Check if flow entering from (dx, dy) is valid for a gate pointing in `requiredEntry`.
        /// Signal enters from the OPPOSITE direction of the gate's arrow.
        /// Example: Gate points Right → flow must enter from the Left (dx = -1).
        /// </summary>
        private static bool IsValidGateEntry(TraceDirection gateDirection, int dx, int dy)
        {
            return gateDirection switch
            {
                TraceDirection.Right => dx == -1, // Enter from left
                TraceDirection.Left => dx == 1,    // Enter from right
                TraceDirection.Up => dy == -1,     // Enter from bottom
                TraceDirection.Down => dy == 1,    // Enter from top
                _ => true
            };
        }

        public void StopSimulation()
        {
            _result = SimulationResult.PlayerStopped;
            _isRunning = false;
        }

        public SimulationResult GetResult() => _result;

        public TraceCellState GetCellState(int x, int y)
        {
            if (_board == null || !_board.IsValidPosition(x, y))
                return new TraceCellState();
            return _cellStates[x, y];
        }

        public bool IsTargetReached(int targetX, int targetY)
            => _reachedTargets.Contains((targetX, targetY));
    }
}
