// SPDX-License-Identifier: MIT
// Chroma Vale — Board controller interface (Core, no Unity deps)
//
// The Unity presentation layer (PuzzleBoardView) talks to this interface;
// the Domain implementation (BoardController) orchestrates merges using the
// pure-C# Core merge rules and exposes the grid state + win condition.

#nullable enable

using System;

namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Contract for the game-board state manager. Sits between the pure-C#
    /// MergeRules and the Unity presentation layer.
    /// </summary>
    public interface IBoardController
    {
        /// <summary>Initialize the board from level data.</summary>
        void Initialize(LevelData levelData);

        /// <summary>
        /// Attempt to merge the orb at <paramref name="source"/> onto the orb at
        /// <paramref name="target"/>. Requires both cells in bounds and adjacent.
        /// On success: both orbs consumed, result orb placed at the target cell,
        /// move counter incremented, OnBoardChanged fired, win condition re-checked.
        /// </summary>
        /// <returns>True if merge succeeded, false if invalid.</returns>
        bool TryMergeAt(GridPosition source, GridPosition target);

        /// <summary>Get the orb at a grid position (null if empty or out of bounds).</summary>
        OrbData? GetOrbAt(GridPosition pos);

        /// <summary>True when every RestorationTarget cell holds its required orb.</summary>
        bool IsLevelComplete { get; }

        /// <summary>Number of successful merges made this level.</summary>
        int MoveCount { get; }

        /// <summary>Event fired when the board state changes (orb added, removed, transformed).</summary>
        event Action<BoardChange> OnBoardChanged;

        /// <summary>Event fired when the level is completed (all targets restored).</summary>
        event Action<LevelResult> OnLevelComplete;
    }
}
