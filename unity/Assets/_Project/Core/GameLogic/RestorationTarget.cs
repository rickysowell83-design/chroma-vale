// SPDX-License-Identifier: MIT
// Chroma Vale — Merge-level data records (Core, no Unity deps)
//
// These records describe the CHROMA MERGE game mode board layout. They are
// additive to the legacy pipe-flow LevelData: a merge level carries its orb
// placement and restoration targets in these arrays, while the pipe-flow
// fields (Sources, Targets, Inventory, ...) remain untouched (null/empty).

namespace ChromaVale.Core.GameLogic
{
    /// <summary>Initial orb placement on a merge level board.</summary>
    public sealed record MergeOrbPlacement(int X, int Y, OrbColor Color, OrbTier Tier);

    /// <summary>
    /// A win-condition cell: the cell at (X, Y) must hold an orb of the given
    /// Color AND Tier for the level to be complete.
    /// </summary>
    public sealed record RestorationTarget(int X, int Y, OrbColor Color, OrbTier Tier);
}
