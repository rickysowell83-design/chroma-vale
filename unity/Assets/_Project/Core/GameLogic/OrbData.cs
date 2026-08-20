// SPDX-License-Identifier: MIT
// Chroma Vale — Orb data structures (Core, no Unity deps)

using System;

namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// The 7 playable orb colors in Chroma Vale (3 primaries + 3 secondaries + Brown).
    /// Maps to the legacy ColorMixer int values for adjacent mixing (Cyan=0, Magenta=1, Yellow=2,
    /// Purple=6, Green=7, Orange=8, Brown=9).
    /// </summary>
    public enum OrbColor
    {
        // --- Primaries ---
        Cyan = 0,
        Magenta = 1,
        Yellow = 2,

        // --- Secondaries ---
        Purple = 6,
        Green = 7,
        Orange = 8,

        // --- Neutral ---
        Brown = 9,
    }

    /// <summary>
    /// Orb power tiers. T1 (weakest) through T5 (max).
    /// T5 orbs cannot tier-merge (capped).
    /// </summary>
    public enum OrbTier
    {
        T1 = 1,
        T2 = 2,
        T3 = 3,
        T4 = 4,
        T5 = 5,
    }

    /// <summary>
    /// An orb on the game board. Pure data — no Unity dependencies.
    /// </summary>
    public sealed record OrbData(OrbColor Color, OrbTier Tier)
    {
        /// <summary>True if this orb is a primary color (Cyan, Magenta, Yellow).</summary>
        public bool IsPrimary => Color is OrbColor.Cyan or OrbColor.Magenta or OrbColor.Yellow;

        /// <summary>True if this orb is a secondary color (Purple, Green, Orange).</summary>
        public bool IsSecondary => Color is OrbColor.Purple or OrbColor.Green or OrbColor.Orange;

        /// <summary>True if this orb is any mixed color (secondary).</summary>
        public bool IsMixed => IsSecondary;

        /// <summary>True if this orb is Brown (dead / cleared).</summary>
        public bool IsBrown => Color is OrbColor.Brown;

        /// <summary>True if this orb can tier-merge (not T5).</summary>
        public bool CanTierMerge => Tier < OrbTier.T5;
    }
}