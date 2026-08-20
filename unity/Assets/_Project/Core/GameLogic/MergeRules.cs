// SPDX-License-Identifier: MIT
// Chroma Vale — Core merge rules (no Unity deps)

using System;

namespace ChromaVale.Core.GameLogic
{
    /// <summary>Outcome classification for a merge attempt.</summary>
    public enum MergeOutcome
    {
        /// <summary>Same color → higher tier.</summary>
        TierMerge,
        /// <summary>Different colors → new mixed color at same tier.</summary>
        ColorMix,
        /// <summary>Secondary+secondary or other mixed+mixed → Brown orb.</summary>
        BrownProduction,
        /// <summary>Brown + Brown → both cleared, no orb produced.</summary>
        BrownClear,
        /// <summary>Invalid merge (tier mismatch, Brown+non-Brown, T5 cap, etc.).</summary>
        Invalid,
    }

    /// <summary>Result of a single merge attempt.</summary>
    public sealed record MergeResult(
        MergeOutcome Outcome,
        OrbData? ResultOrb,
        bool ConsumesSource,
        bool ConsumesTarget
    );

    /// <summary>
    /// Static merge rules for Chroma Vale orbs.
    ///
    /// Implements the 7-color mixing table from DESIGN_CANON (6 chromatic colors + Brown).
    /// References ColorMixer.cs constants for enum alignment but implements
    /// its own corrected mixing logic (ColorMixer.Mix wrongly normalizes
    /// mixed+mixed — this version correctly produces Brown).
    ///
    /// DESIGN PINS:
    ///   - All undefined same-tier different-color pairs → Brown (conservative
    ///     fallback, extendable when more colors are added).
    /// </summary>
    public static class MergeRules
    {
        /// <summary>
        /// Returns true if merging these two orbs would produce any valid outcome
        /// (tier merge, color mix, brown production, or brown clear).
        /// </summary>
        public static bool CanMerge(OrbData? a, OrbData? b)
        {
            if (a is null || b is null) return false;
            if (a.Tier != b.Tier) return false;

            // Brown + non-Brown = invalid
            if (a.IsBrown != b.IsBrown)
                return false;

            // Same color, same tier, both T5 = capped
            if (a.Color == b.Color && a.Tier == OrbTier.T5)
                return false;

            return true;
        }

        /// <summary>
        /// Attempt to merge two orbs. Both must be non-null and same tier.
        /// Returns a MergeResult describing the outcome.
        /// </summary>
        public static MergeResult TryMerge(OrbData? source, OrbData? target)
        {
            if (source is null) return Invalid;
            if (target is null) return Invalid;

            // 1. Brown rules (override everything)
            if (source.IsBrown || target.IsBrown)
            {
                // Brown + Brown → cleared
                if (source.IsBrown && target.IsBrown)
                    return new MergeResult(MergeOutcome.BrownClear, null, true, true);

                // Brown + non-Brown → invalid
                return Invalid;
            }

            // 2. Must be same tier
            if (source.Tier != target.Tier)
                return Invalid;

            // 3. Same color → tier merge
            if (source.Color == target.Color)
            {
                if (source.Tier < OrbTier.T5)
                    return new MergeResult(
                        MergeOutcome.TierMerge,
                        new OrbData(source.Color, source.Tier + 1),
                        true, true
                    );
                return Invalid; // T5 capped
            }

            // 4. Different colors, same tier → color mix
            var mixed = MixColors(source.Color, target.Color);

            if (mixed == OrbColor.Brown)
                return new MergeResult(
                    MergeOutcome.BrownProduction,
                    new OrbData(OrbColor.Brown, source.Tier),
                    true, true
                );

            return new MergeResult(
                MergeOutcome.ColorMix,
                new OrbData(mixed, source.Tier),
                true, true
            );
        }

        /// <summary>Singleton Invalid result (avoids allocation per call).</summary>
        private static readonly MergeResult Invalid = new(MergeOutcome.Invalid, null, false, false);

        /// <summary>
        /// Full 7-color mixing table.
        /// Symmetric — (a,b) is normalized so a ≤ b before lookup.
        /// Every pair not explicitly listed defaults to Brown.
        /// </summary>
        private static OrbColor MixColors(OrbColor a, OrbColor b)
        {
            // Sort so a < b for consistent table lookup
            if (a > b)
            {
                (a, b) = (b, a);
            }

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            return (a, b) switch
            {
                // ── Primary + Primary → Secondary ──
                (OrbColor.Cyan,    OrbColor.Magenta) => OrbColor.Purple,
                (OrbColor.Cyan,    OrbColor.Yellow)  => OrbColor.Green,
                (OrbColor.Magenta, OrbColor.Yellow)  => OrbColor.Orange,

                // ── Primary + Secondary → Brown (all-3-primaries / undefined) ──
                // Cyan + Orange (C+M+Y) → Brown (all 3 primaries)
                (OrbColor.Cyan,    OrbColor.Orange)  => OrbColor.Brown,

                // Magenta + Purple (C+2M) → Brown (no defined mix for 2:1 Magenta)
                (OrbColor.Magenta, OrbColor.Purple)  => OrbColor.Brown,
                // Magenta + Green (C+M+Y) → Brown (all 3 primaries)
                (OrbColor.Magenta, OrbColor.Green)   => OrbColor.Brown,

                // Yellow + Purple (C+M+Y) → Brown (all 3 primaries)
                (OrbColor.Yellow,  OrbColor.Purple)  => OrbColor.Brown,
                // Yellow + Green (C+2Y) → Brown (no defined mix for 2:1 Yellow)
                (OrbColor.Yellow,  OrbColor.Green)   => OrbColor.Brown,

                // ── Secondary + Secondary → Brown ──
                (OrbColor.Purple,  OrbColor.Green)   => OrbColor.Brown,
                (OrbColor.Purple,  OrbColor.Orange)  => OrbColor.Brown,
                (OrbColor.Green,   OrbColor.Orange)  => OrbColor.Brown,

                // ── Anything else → Brown ──
                // Any undefined pair → Brown (mixed + mixed fallback)
                (_, _)                                => OrbColor.Brown,
            };
        }
    }
}