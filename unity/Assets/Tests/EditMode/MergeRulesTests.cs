// SPDX-License-Identifier: MIT
// Chroma Vale — MergeRules unit tests (EditMode, NUnit)

using System;
using NUnit.Framework;
using ChromaVale.Core.GameLogic;

namespace ChromaVale.Tests
{
    /// <summary>
    /// Comprehensive test suite for MergeRules (11-color merge system).
    /// Covers every defined mixing recipe, Brown rules, tier merges,
    /// boundary conditions, and CanMerge semantics.
    /// </summary>
    public class MergeRulesTests
    {
        // ── Helpers ──────────────────────────────────────────────────────

        private static OrbData Primary(OrbColor c, OrbTier t = OrbTier.T1) =>
            new(c, t);

        private static MergeResult Merge(OrbColor a, OrbColor b, OrbTier t = OrbTier.T1) =>
            MergeRules.TryMerge(Primary(a, t), Primary(b, t));

        // ── Null / Edge ─────────────────────────────────────────────────

        [Test]
        public void TryMerge_NullSource_ReturnsInvalid()
        {
            var r = MergeRules.TryMerge(null, Primary(OrbColor.Cyan));
            Assert.AreEqual(MergeOutcome.Invalid, r.Outcome);
            Assert.IsFalse(r.ConsumesSource);
            Assert.IsFalse(r.ConsumesTarget);
        }

        [Test]
        public void TryMerge_NullTarget_ReturnsInvalid()
        {
            var r = MergeRules.TryMerge(Primary(OrbColor.Cyan), null);
            Assert.AreEqual(MergeOutcome.Invalid, r.Outcome);
        }

        // ── Tier mismatch ──────────────────────────────────────────────

        [Test]
        public void TryMerge_TierMismatch_ReturnsInvalid()
        {
            var r = MergeRules.TryMerge(Primary(OrbColor.Cyan, OrbTier.T1), Primary(OrbColor.Magenta, OrbTier.T2));
            Assert.AreEqual(MergeOutcome.Invalid, r.Outcome);
        }

        // ── Same-color tier merge ──────────────────────────────────────

        [TestCase(OrbColor.Cyan, OrbTier.T1, OrbTier.T2)]
        [TestCase(OrbColor.Magenta, OrbTier.T2, OrbTier.T3)]
        [TestCase(OrbColor.Yellow, OrbTier.T3, OrbTier.T4)]
        [TestCase(OrbColor.Purple, OrbTier.T4, OrbTier.T5)]
        [TestCase(OrbColor.Green, OrbTier.T1, OrbTier.T2)]
        [TestCase(OrbColor.Orange, OrbTier.T1, OrbTier.T2)]
        [TestCase(OrbColor.Brown, OrbTier.T1, OrbTier.T2)] // Brown+Brown=Clear, but brown+brown same-color is before brown check... wait no, brown check comes first. Let me test brown separately.
        [TestCase(OrbColor.Teal, OrbTier.T1, OrbTier.T2)]
        [TestCase(OrbColor.Vermilion, OrbTier.T2, OrbTier.T3)]
        [TestCase(OrbColor.Amber, OrbTier.T3, OrbTier.T4)]
        [TestCase(OrbColor.Slate, OrbTier.T1, OrbTier.T2)]
        public void TryMerge_SameColor_ReturnsTierMerge(OrbColor color, OrbTier fromTier, OrbTier expectedTier)
        {
            // Skip Brown — it follows different rules
            if (color == OrbColor.Brown) return;

            var r = MergeRules.TryMerge(Primary(color, fromTier), Primary(color, fromTier));
            Assert.AreEqual(MergeOutcome.TierMerge, r.Outcome);
            Assert.IsNotNull(r.ResultOrb);
            Assert.AreEqual(color, r.ResultOrb!.Color);
            Assert.AreEqual(expectedTier, r.ResultOrb.Tier);
            Assert.IsTrue(r.ConsumesSource);
            Assert.IsTrue(r.ConsumesTarget);
        }

        [Test]
        public void TryMerge_SameColorT5_ReturnsInvalid()
        {
            var r = MergeRules.TryMerge(Primary(OrbColor.Cyan, OrbTier.T5), Primary(OrbColor.Cyan, OrbTier.T5));
            Assert.AreEqual(MergeOutcome.Invalid, r.Outcome);
        }

        // ── Brown rules ────────────────────────────────────────────────

        [Test]
        public void TryMerge_BrownPlusBrown_ReturnsClear()
        {
            var r = MergeRules.TryMerge(Primary(OrbColor.Brown, OrbTier.T3), Primary(OrbColor.Brown, OrbTier.T3));
            Assert.AreEqual(MergeOutcome.BrownClear, r.Outcome);
            Assert.IsNull(r.ResultOrb);
            Assert.IsTrue(r.ConsumesSource);
            Assert.IsTrue(r.ConsumesTarget);
        }

        [TestCase(OrbColor.Cyan)]
        [TestCase(OrbColor.Magenta)]
        [TestCase(OrbColor.Yellow)]
        [TestCase(OrbColor.Purple)]
        [TestCase(OrbColor.Green)]
        [TestCase(OrbColor.Orange)]
        [TestCase(OrbColor.Teal)]
        [TestCase(OrbColor.Vermilion)]
        [TestCase(OrbColor.Amber)]
        [TestCase(OrbColor.Slate)]
        public void TryMerge_BrownPlusNonBrown_ReturnsInvalid(OrbColor nonBrown)
        {
            var r = MergeRules.TryMerge(Primary(OrbColor.Brown), Primary(nonBrown));
            Assert.AreEqual(MergeOutcome.Invalid, r.Outcome);

            // Also test the symmetric case
            var r2 = MergeRules.TryMerge(Primary(nonBrown), Primary(OrbColor.Brown));
            Assert.AreEqual(MergeOutcome.Invalid, r2.Outcome);
        }

        // ── Primary + Primary → Secondary ──────────────────────────────

        [Test]
        public void Mix_CyanPlusMagenta_ReturnsPurple()
        {
            var r = Merge(OrbColor.Cyan, OrbColor.Magenta);
            Assert.AreEqual(MergeOutcome.ColorMix, r.Outcome);
            Assert.AreEqual(OrbColor.Purple, r.ResultOrb!.Color);
            Assert.AreEqual(OrbTier.T1, r.ResultOrb.Tier);
        }

        [Test]
        public void Mix_CyanPlusYellow_ReturnsGreen()
        {
            var r = Merge(OrbColor.Cyan, OrbColor.Yellow);
            Assert.AreEqual(MergeOutcome.ColorMix, r.Outcome);
            Assert.AreEqual(OrbColor.Green, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_MagentaPlusYellow_ReturnsOrange()
        {
            var r = Merge(OrbColor.Magenta, OrbColor.Yellow);
            Assert.AreEqual(MergeOutcome.ColorMix, r.Outcome);
            Assert.AreEqual(OrbColor.Orange, r.ResultOrb!.Color);
        }

        // ── Primary + Secondary → Tertiary recipes ─────────────────────

        [Test]
        public void Mix_CyanPlusGreen_ReturnsTeal()
        {
            var r = Merge(OrbColor.Cyan, OrbColor.Green);
            Assert.AreEqual(MergeOutcome.ColorMix, r.Outcome);
            Assert.AreEqual(OrbColor.Teal, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_MagentaPlusOrange_ReturnsVermilion()
        {
            var r = Merge(OrbColor.Magenta, OrbColor.Orange);
            Assert.AreEqual(MergeOutcome.ColorMix, r.Outcome);
            Assert.AreEqual(OrbColor.Vermilion, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_YellowPlusOrange_ReturnsAmber()
        {
            var r = Merge(OrbColor.Yellow, OrbColor.Orange);
            Assert.AreEqual(MergeOutcome.ColorMix, r.Outcome);
            Assert.AreEqual(OrbColor.Amber, r.ResultOrb!.Color);
        }

        [Test]
        // DESIGN PIN: Cyan+Purple=Slate — documented in OrbData.cs
        public void Mix_CyanPlusPurple_ReturnsSlate()
        {
            var r = Merge(OrbColor.Cyan, OrbColor.Purple);
            Assert.AreEqual(MergeOutcome.ColorMix, r.Outcome);
            Assert.AreEqual(OrbColor.Slate, r.ResultOrb!.Color);
        }

        // ── Primary + Secondary → Brown (all-3-primaries / undefined) ──

        [Test]
        public void Mix_CyanPlusOrange_ReturnsBrown()
        {
            var r = Merge(OrbColor.Cyan, OrbColor.Orange);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_MagentaPlusPurple_ReturnsBrown()
        {
            var r = Merge(OrbColor.Magenta, OrbColor.Purple);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_MagentaPlusGreen_ReturnsBrown()
        {
            var r = Merge(OrbColor.Magenta, OrbColor.Green);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_YellowPlusPurple_ReturnsBrown()
        {
            var r = Merge(OrbColor.Yellow, OrbColor.Purple);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_YellowPlusGreen_ReturnsBrown()
        {
            var r = Merge(OrbColor.Yellow, OrbColor.Green);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        // ── Secondary + Secondary → Brown ──────────────────────────────

        [Test]
        public void Mix_PurplePlusGreen_ReturnsBrown()
        {
            var r = Merge(OrbColor.Purple, OrbColor.Green);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_PurplePlusOrange_ReturnsBrown()
        {
            var r = Merge(OrbColor.Purple, OrbColor.Orange);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_GreenPlusOrange_ReturnsBrown()
        {
            var r = Merge(OrbColor.Green, OrbColor.Orange);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        // ── Tertiary + anything → Brown ────────────────────────────────

        [Test]
        public void Mix_TertiaryPlusPrimary_ReturnsBrown(
            [Values(OrbColor.Teal, OrbColor.Vermilion, OrbColor.Amber, OrbColor.Slate)] OrbColor tertiary,
            [Values(OrbColor.Cyan, OrbColor.Magenta, OrbColor.Yellow)] OrbColor primary)
        {
            var r = Merge(tertiary, primary);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_TertiaryPlusSecondary_ReturnsBrown(
            [Values(OrbColor.Teal, OrbColor.Vermilion, OrbColor.Amber, OrbColor.Slate)] OrbColor tertiary,
            [Values(OrbColor.Purple, OrbColor.Green, OrbColor.Orange)] OrbColor secondary)
        {
            var r = Merge(tertiary, secondary);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        [Test]
        public void Mix_TertiaryPlusTertiary_ReturnsBrown(
            [Values(OrbColor.Teal, OrbColor.Vermilion, OrbColor.Amber, OrbColor.Slate)] OrbColor a,
            [Values(OrbColor.Teal, OrbColor.Vermilion, OrbColor.Amber, OrbColor.Slate)] OrbColor b)
        {
            // Skip same-color (that would be tier merge, caught earlier)
            if (a == b) return;
            var r = Merge(a, b);
            Assert.AreEqual(MergeOutcome.BrownProduction, r.Outcome);
            Assert.AreEqual(OrbColor.Brown, r.ResultOrb!.Color);
        }

        // ── Mixing preserves tier of source orbs ───────────────────────

        [Test]
        public void Mix_PreservesTier()
        {
            var r = MergeRules.TryMerge(
                Primary(OrbColor.Cyan, OrbTier.T3),
                Primary(OrbColor.Magenta, OrbTier.T3)
            );
            Assert.AreEqual(OrbTier.T3, r.ResultOrb!.Tier);
        }

        // ── Symmetry ───────────────────────────────────────────────────

        [Test]
        public void Mix_IsSymmetric(
            [Values(OrbColor.Cyan, OrbColor.Magenta, OrbColor.Yellow, OrbColor.Purple, OrbColor.Green, OrbColor.Orange,
                    OrbColor.Teal, OrbColor.Vermilion, OrbColor.Amber, OrbColor.Slate)] OrbColor a,
            [Values(OrbColor.Cyan, OrbColor.Magenta, OrbColor.Yellow, OrbColor.Purple, OrbColor.Green, OrbColor.Orange,
                    OrbColor.Teal, OrbColor.Vermilion, OrbColor.Amber, OrbColor.Slate)] OrbColor b)
        {
            if (a == b) return; // same color → tier merge, not color mix
            var r1 = Merge(a, b);
            var r2 = Merge(b, a);
            Assert.AreEqual(r1.Outcome, r2.Outcome);
            if (r1.ResultOrb != null && r2.ResultOrb != null)
                Assert.AreEqual(r1.ResultOrb.Color, r2.ResultOrb.Color);
        }

        // ── CanMerge ────────────────────────────────────────────────────

        [Test]
        public void CanMerge_ValidMix_ReturnsTrue()
        {
            Assert.IsTrue(MergeRules.CanMerge(Primary(OrbColor.Cyan), Primary(OrbColor.Magenta)));
        }

        [Test]
        public void CanMerge_ValidTierMerge_ReturnsTrue()
        {
            Assert.IsTrue(MergeRules.CanMerge(Primary(OrbColor.Cyan), Primary(OrbColor.Cyan)));
        }

        [Test]
        public void CanMerge_BrownPlusBrown_ReturnsTrue()
        {
            Assert.IsTrue(MergeRules.CanMerge(Primary(OrbColor.Brown), Primary(OrbColor.Brown)));
        }

        [Test]
        public void CanMerge_BrownPlusNonBrown_ReturnsFalse()
        {
            Assert.IsFalse(MergeRules.CanMerge(Primary(OrbColor.Brown), Primary(OrbColor.Cyan)));
            Assert.IsFalse(MergeRules.CanMerge(Primary(OrbColor.Cyan), Primary(OrbColor.Brown)));
        }

        [Test]
        public void CanMerge_TierMismatch_ReturnsFalse()
        {
            Assert.IsFalse(MergeRules.CanMerge(Primary(OrbColor.Cyan, OrbTier.T1), Primary(OrbColor.Cyan, OrbTier.T2)));
        }

        [Test]
        public void CanMerge_T5SameColor_ReturnsFalse()
        {
            Assert.IsFalse(MergeRules.CanMerge(Primary(OrbColor.Cyan, OrbTier.T5), Primary(OrbColor.Cyan, OrbTier.T5)));
        }

        [Test]
        public void CanMerge_Null_ReturnsFalse()
        {
            Assert.IsFalse(MergeRules.CanMerge(null, Primary(OrbColor.Cyan)));
        }
    }
}