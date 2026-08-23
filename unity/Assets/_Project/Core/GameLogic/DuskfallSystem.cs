// SPDX-License-Identifier: MIT
// Chroma Vale — DuskfallSystem (Core, no Unity deps)
//
// "Duskfall / The Blackout" mechanic (game-designer t_37293ae0 design,
// l8_duskfall_blackout_spec.md): a global, VISIBLE countdown replaces the
// retired SpoilingBrown multiplication (DESIGN_CANON §3.3.1 — failed
// playtest: invisible accumulation). duskCounter starts at duskBeats and
// decrements each completed move while any Brown is on the board. Clearing
// the LAST Brown resets the counter (release). Reaching 0 with any Brown
// remaining is a Duskfall fail (soft fail — retry, no progression loss).
//
// Engine-free core: pure C# state + decisions. The presentation layer reads
// CounterRatio for the vignette shader and listens to OnDuskfall /
// OnBrownCleared. Do NOT add UnityEngine here.

using System;
using System.Collections.Generic;

namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Global screen-darkness countdown ("the dark closes in") for levels with
    /// <see cref="LevelData.DuskEnabled"/>. Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class DuskfallSystem
    {
        private readonly bool _enabled;
        private readonly int _duskBeats;

        private int _counter;
        private bool _armed;

        /// <summary>Raised when the counter reaches 0 with any Brown remaining (soft fail → retry).</summary>
        public event Action? OnDuskfall;

        /// <summary>Raised when the last Brown is cleared (counter reset, vignette snaps clear).</summary>
        public event Action? OnBrownCleared;

        /// <summary>Creates the system from level configuration.</summary>
        public DuskfallSystem(LevelData level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            _enabled = level.DuskEnabled;
            _duskBeats = level.DuskBeats > 0 ? level.DuskBeats : DefaultDuskBeats;
            Reset();
        }

        /// <summary>Default beat count when a level does not set duskBeats.</summary>
        public const int DefaultDuskBeats = 6;

        /// <summary>True when the mechanic is active for this level (JSON "duskEnabled": true).</summary>
        public bool Enabled => _enabled;

        /// <summary>Total beats in a full countdown.</summary>
        public int DuskBeats => _duskBeats;

        /// <summary>Current countdown value (beats remaining). 0 = Duskfall threshold.</summary>
        public int Counter => _counter;

        /// <summary>
        /// Vignette opacity 0..1 — 0 fully clear, 1 fully dark. Driven by the
        /// presentation layer's vignette shader every frame.
        /// </summary>
        public float CounterRatio => _armed && _duskBeats > 0 ? 1f - ((float)_counter / _duskBeats) : 0f;

        /// <summary>True once the countdown has been armed by at least one Brown on the board.</summary>
        public bool Armed => _armed;

        /// <summary>Arms the countdown at full beats without consuming a move (pre-placed Brown at level start).</summary>
        public void Arm()
        {
            if (!_enabled) return;
            _counter = _duskBeats;
            _armed = true;
        }

        /// <summary>True when any Brown orb remains on the board.</summary>
        public static bool HasBrowns(IBoardController board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    OrbData? orb = board.GetOrbAt(new GridPosition(x, y));
                    if (orb != null && orb.IsBrown) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Advances the mechanic after one successful move. No-op when disabled.
        /// Rules (spec §2):
        ///   - If NO Browns remain on the board: disarm + reset counter (release).
        ///     Fires <see cref="OnBrownCleared"/> only on the transition from
        ///   - Otherwise decrement; at 0 fire <see cref="OnDuskfall"/> and re-arm
        ///     at full beats so a retry-in-place keeps playing (no soft-lock).
        /// </summary>
        /// <param name="board">Read-only view of the current board state.</param>
        public void OnMoveCompleted(IBoardController board)
        {
            if (!_enabled || board == null) return;

            bool brownsRemain = HasBrowns(board);
            if (!brownsRemain)
            {
                if (_armed)
                {
                    // Release: last Brown just cleared this move.
                    Reset();
                    OnBrownCleared?.Invoke();
                }
                return;
            }

            // Arm lazily on first Brown (spec edge case: never runs brown-free).
            _armed = true;
            _counter--;

            if (_counter <= 0)
            {
                OnDuskfall?.Invoke();
                // Soft-fail recovery: restart the countdown so play continues
                // (presentation shows the retry/Duskfall animation).
                _counter = _duskBeats;
            }
        }

        /// <summary>Clears all duskfall state (level start / restart).</summary>
        public void Reset()
        {
            _counter = _duskBeats;
            _armed = false;
        }

#if UNITY_EDITOR
        /// <summary>EditMode test hook — advances the countdown exactly like a
        /// completed move, bypassing the board fixture (tests construct bare systems).</summary>
        public void TickForTest() => OnMoveCompleted(null!);
#endif
    }
}
