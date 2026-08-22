// SPDX-License-Identifier: MIT
// Chroma Vale — BrownSpoilSystem (Core, no Unity deps)
//
// "Spoiling Brown" mechanic (game-designer t_ddfb2474 design, PART 1 of 3):
// neglected brown orbs accumulate a decay counter each move and, once a brown's
// decay reaches the level threshold, it spawns a new Brown T1 on the nearest
// valid empty cell. Applies pressure to clear browns promptly.
//
// This is the ENGINE-FREE core mechanic. ChromaVale.Core has
// noEngineReferences: true and AGENTS.md forbids UnityEngine/MonoBehaviour in
// Core, so this class exposes pure-C# state and returns spawn DECISIONS. A
// separate Domain/Presentation MonoBehaviour (follow-up task) listens to
// BoardController's move event and drives this system, applying the returned
// spawns to the live board. Do NOT add UnityEngine here.

using System;
using System.Collections.Generic;

namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Tracks per-cell decay for the Spoiling Brown mechanic and decides where
    /// new Brown T1 orbs spawn after each completed move. Pure C# — no Unity
    /// dependencies. Configure from <see cref="LevelData"/> via
    /// <see cref="SpoilEnabled"/> / <see cref="SpoilMaxDecay"/>.
    /// </summary>
    public sealed class BrownSpoilSystem
    {
        /// <summary>Default decay threshold when a level does not set spoilMaxDecay.</summary>
        public const int DefaultMaxDecay = 3;

        private readonly bool _enabled;
        private readonly int _maxDecay;
        private readonly int _width;
        private readonly int _height;
        private readonly HashSet<GridPosition> _targets = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> _obstacles = new HashSet<GridPosition>();

        /// <summary>Decay counter per cell, keyed by the cell's grid position.</summary>
        private readonly Dictionary<GridPosition, int> _decay = new Dictionary<GridPosition, int>();

        /// <summary>
        /// Creates the system from level configuration.
        /// </summary>
        public BrownSpoilSystem(LevelData level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            _enabled = level.SpoilEnabled;
            _maxDecay = level.SpoilMaxDecay > 0 ? level.SpoilMaxDecay : DefaultMaxDecay;
            _width = level.Width;
            _height = level.Height;

            if (level.RestorationTargets != null)
            {
                foreach (RestorationTarget t in level.RestorationTargets)
                {
                    _targets.Add(new GridPosition(t.X, t.Y));
                }
            }

            if (level.Obstacles != null)
            {
                foreach (LevelObstacle o in level.Obstacles)
                {
                    _obstacles.Add(new GridPosition(o.X, o.Y));
                }
            }
        }

        /// <summary>True when the mechanic is active for this level (JSON "spoilEnabled": true).</summary>
        public bool Enabled => _enabled;

        /// <summary>Decay threshold at which a neglected brown spawns.</summary>
        public int MaxDecay => _maxDecay;

        /// <summary>Current decay counter for the brown at a cell (0 if none tracked).</summary>
        public int GetDecay(GridPosition cell)
        {
            return _decay.TryGetValue(cell, out int value) ? value : 0;
        }

        /// <summary>
        /// Advances the mechanic after one successful move. Increments decay for
        /// every brown currently on the board; each brown at or past
        /// <see cref="MaxDecay"/> spawns a new Brown T1 on the nearest valid empty
        /// cell and resets its decay to 0. When the mechanic is disabled this is a
        /// no-op returning an empty list.
        /// </summary>
        /// <param name="board">Read-only view of the current board state.</param>
        /// <returns>Cells where a new Brown T1 should be spawned this move.</returns>
        public IReadOnlyList<GridPosition> OnMoveCompleted(IBoardController board)
        {
            var spawns = new List<GridPosition>();
            if (!_enabled) return spawns;
            if (board == null) throw new ArgumentNullException(nameof(board));

            // Snapshot current brown cells so decay is consistent within one move.
            var brownCells = new List<GridPosition>();
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var pos = new GridPosition(x, y);
                    OrbData? orb = board.GetOrbAt(pos);
                    if (orb != null && orb.IsBrown)
                    {
                        brownCells.Add(pos);
                    }
                }
            }

            // Prune decay for cells that no longer hold a brown (brown cleared/moved).
            var stale = new List<GridPosition>();
            foreach (var cell in _decay.Keys)
            {
                if (!brownCells.Contains(cell))
                {
                    stale.Add(cell);
                }
            }
            foreach (var cell in stale)
            {
                _decay.Remove(cell);
            }

            // Increment decay for each brown currently on the board.
            foreach (var cell in brownCells)
            {
                _decay[cell] = GetDecay(cell) + 1;
            }

            // Cells occupied by a brown (or claimed by a spawn this move) cannot be
            // chosen as a spawn target — tracked so two browns never target one cell.
            var claimed = new HashSet<GridPosition>(brownCells);

            foreach (var cell in brownCells)
            {
                if (_decay[cell] < _maxDecay) continue;

                GridPosition? target = FindNearestValidEmptyCell(board, cell, claimed);
                if (target != null)
                {
                    spawns.Add(target);
                    claimed.Add(target);
                }

                // Reset regardless of whether a spawn occurred (no soft-lock).
                _decay[cell] = 0;
            }

            return spawns;
        }

        /// <summary>
        /// Finds the nearest valid empty cell to <paramref name="origin"/> by
        /// Manhattan distance, tie-breaking on lowest X then lowest Y. Valid =
        /// in bounds, empty, not a target cell, not an obstacle cell, and not
        /// already claimed by another spawn this move. Returns null if none.
        /// </summary>
        private GridPosition? FindNearestValidEmptyCell(
            IBoardController board, GridPosition origin, HashSet<GridPosition> claimed)
        {
            GridPosition? best = null;
            int bestDistance = int.MaxValue;
            int bestX = int.MaxValue;
            int bestY = int.MaxValue;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var pos = new GridPosition(x, y);

                    if (_targets.Contains(pos) || _obstacles.Contains(pos)) continue;
                    if (claimed.Contains(pos)) continue;
                    if (board.GetOrbAt(pos) != null) continue; // occupied

                    int distance = Math.Abs(x - origin.X) + Math.Abs(y - origin.Y);
                    if (distance < bestDistance ||
                        (distance == bestDistance && (x < bestX || (x == bestX && y < bestY))))
                    {
                        best = pos;
                        bestDistance = distance;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            return best;
        }

        /// <summary>Clears all decay state (e.g. on level restart).</summary>
        public void Reset()
        {
            _decay.Clear();
        }
    }
}
