using System.Collections.Generic;
using System.Linq;
using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    /// <summary>
    /// Manages the player's hand of trace segments — placement, undo, and capacity tracking.
    /// Tracks which pieces have been placed, which are still available, and
    /// enforces undo limits and capacity mapping to the board.
    /// </summary>
    public class TraceInventory
    {
        private readonly List<TraceSegment> _pieces;
        private readonly int _maxUndos;
        private int _undosUsed;
        private readonly Dictionary<(int x, int y), int> _placementMap; // grid pos → piece index

        /// <summary>All pieces in the inventory. Some may be Placed/Burst.</summary>
        public IReadOnlyList<TraceSegment> Pieces => _pieces;

        /// <summary>Number of undo actions remaining.</summary>
        public int UndosRemaining => _maxUndos - _undosUsed;

        /// <summary>Whether any undos are still available.</summary>
        public bool CanUndo => UndosRemaining > 0;

        /// <summary>Pieces still in hand and available for placement.</summary>
        public int AvailableCount => _pieces.Count(p => p.State == SegmentState.InHand);

        /// <summary>Pieces that have been placed on the board.</summary>
        public int PlacedCount => _pieces.Count(p => p.State == SegmentState.Placed);

        /// <summary>Pieces that have shorted during simulation.</summary>
        public int ShortedCount => _pieces.Count(p => p.State == SegmentState.Shorted);

        public TraceInventory(TraceSegment[] pieces, int maxUndos = 3)
        {
            _pieces = pieces?.ToList() ?? new List<TraceSegment>();
            _maxUndos = maxUndos;
            _undosUsed = 0;
            _placementMap = new Dictionary<(int, int), int>();
        }

        /// <summary>
        /// Get all pieces currently in hand (available for placement).
        /// </summary>
        public TraceSegment[] GetAvailablePieces()
        {
            return _pieces.Where(p => p.State == SegmentState.InHand).ToArray();
        }

        /// <summary>
        /// Get count of available pieces of a specific shape.
        /// </summary>
        public int GetCount(SegmentShape shape)
        {
            return _pieces.Count(p => p.State == SegmentState.InHand && p.Shape == shape);
        }

        /// <summary>
        /// Get all available pieces grouped by shape.
        /// </summary>
        public Dictionary<SegmentShape, int> GetAvailableCounts()
        {
            var counts = new Dictionary<SegmentShape, int>();
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].State != SegmentState.InHand) continue;
                var shape = _pieces[i].Shape;
                counts.TryGetValue(shape, out int c);
                counts[shape] = c + 1;
            }
            return counts;
        }

        /// <summary>
        /// Try to place a piece from the inventory onto the board.
        /// </summary>
        /// <param name="pieceIndex">Index in the Pieces list.</param>
        /// <param name="board">The board to place on.</param>
        /// <param name="x">Grid X coordinate.</param>
        /// <param name="y">Grid Y coordinate.</param>
        /// <param name="flowSimulator">For registering capacity.</param>
        /// <param name="rotation">Initial rotation in degrees (0, 90, 180, 270). Default 0.</param>
        /// <returns>True if placement succeeded.</returns>
        public bool TryPlace(int pieceIndex, GridBoard board, int x, int y, SignalRouter flowSimulator = null, int rotation = 0)
        {
            if (pieceIndex < 0 || pieceIndex >= _pieces.Count) return false;
            var piece = _pieces[pieceIndex];
            if (piece.State != SegmentState.InHand) return false;
            if (!board.IsValidPosition(x, y)) return false;

            var cell = board.GetCell(x, y);
            if (cell.Type != CellType.Empty) return false;
            if (cell.IsOccupied) return false;

            // Place on board (uncolored trace — color is assigned by flow)
            board.PlaceTrace(x, y);
            _pieces[pieceIndex].State = SegmentState.Placed;
            _pieces[pieceIndex].Rotation = rotation % 360; // Store initial rotation
            _placementMap[(x, y)] = pieceIndex;

            // Register capacity with flow simulator
            if (piece.Capacity > 0)
            {
                flowSimulator?.SetTraceCapacity(x, y, piece.Capacity);
            }
            // Register shape info (all shapes — Straight rotation matters for connection maps)
            flowSimulator?.SetTraceShape(x, y, piece.Shape, piece.Direction, piece.Rotation);

            return true;
        }

        /// <summary>
        /// Try to undo the last placement. Returns the piece to the player's hand.
        /// </summary>
        /// <param name="board">The board to undo from.</param>
        /// <returns>True if undo succeeded.</returns>
        public bool TryUndo(GridBoard board)
        {
            if (!CanUndo) return false;
            if (_placementMap.Count == 0) return false;

            // Remove last placement
            var lastEntry = _placementMap.Last();
            int x = lastEntry.Key.Item1, y = lastEntry.Key.Item2;
            int pieceIndex = lastEntry.Value;

            if (pieceIndex < 0 || pieceIndex >= _pieces.Count) return false;

            // Return piece to hand
            _pieces[pieceIndex].State = SegmentState.InHand;
            _pieces[pieceIndex].Rotation = 0; // Reset rotation when returning to hand
            _placementMap.Remove((x, y));

            // Clear board cell
            board.UndoLast();
            _undosUsed++;

            return true;
        }

        /// <summary>
        /// Mark a placed piece as shorted (during simulation).
        /// </summary>
        public bool MarkShorted(int x, int y)
        {
            if (!_placementMap.TryGetValue((x, y), out int pieceIndex)) return false;
            if (pieceIndex < 0 || pieceIndex >= _pieces.Count) return false;

            _pieces[pieceIndex].State = SegmentState.Shorted;
            return true;
        }

        /// <summary>
        /// Get the piece at a specific board position (if any).
        /// </summary>
        public TraceSegment GetPieceAt(int x, int y)
        {
            if (!_placementMap.TryGetValue((x, y), out int idx)) return null;
            if (idx < 0 || idx >= _pieces.Count) return null;
            return _pieces[idx];
        }

        /// <summary>
        /// Get the index of the piece at a board position.
        /// </summary>
        public int GetPieceIndexAt(int x, int y)
        {
            return _placementMap.TryGetValue((x, y), out int idx) ? idx : -1;
        }

        /// <summary>
        /// Reset all placed pieces back to hand. Clears the placement map.
        /// Used for full level reset.
        /// </summary>
        public void ResetAll()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].State == SegmentState.Placed)
                    _pieces[i].State = SegmentState.InHand;
            }
            _placementMap.Clear();
            _undosUsed = 0;
        }

        /// <summary>
        /// Get the total capacity deployed on the board. Used for hint systems.
        /// </summary>
        public int GetTotalDeployedCapacity()
        {
            int total = 0;
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].State == SegmentState.Placed)
                    total += _pieces[i].Capacity;
            }
            return total;
        }

        /// <summary>
        /// Calculate efficiency multiplier: 1.0 = all pieces used,
        /// 0.5 = only half used. Higher is better for star scoring.
        /// </summary>
        public float GetEfficiency()
        {
            int total = _pieces.Count;
            int unused = AvailableCount;
            if (total == 0) return 1f;
            return (float)unused / total; // 0 = all used, 1 = none used
        }

        /// <summary>
        /// Debug: dump inventory state.
        /// </summary>
        public override string ToString()
        {
            return $"TraceInventory: {AvailableCount} available, {PlacedCount} placed, " +
                   $"{ShortedCount} shorted, {_placementMap.Count} on board, {UndosRemaining} undos left";
        }
    }
}
