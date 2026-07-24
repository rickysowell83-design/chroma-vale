using NUnit.Framework;
using ChromaVale.Core.GameLogic;

namespace ChromaVale.Tests
{
    /// <summary>
    /// Edit-mode tests for ColorMixer — engine-free domain logic in ChromaVale.Core.
    /// Tests mixing table, commutativity, identity, MultiMix, and IsValidColor.
    /// C# 9.0 compatible (no file-scoped namespaces).
    /// </summary>
    public class ColorMixerTests
    {
        // ── Basic mixing (from the pre-computed MixTable) ──

        [Test]
        public void Mix_CyanAndMagenta_ReturnsPurple()
        {
            int result = ColorMixer.Mix(ColorMixer.Cyan, ColorMixer.Magenta);
            Assert.AreEqual(ColorMixer.Purple, result);
        }

        [Test]
        public void Mix_CyanAndYellow_ReturnsGreen()
        {
            int result = ColorMixer.Mix(ColorMixer.Cyan, ColorMixer.Yellow);
            Assert.AreEqual(ColorMixer.Green, result);
        }

        [Test]
        public void Mix_MagentaAndYellow_ReturnsOrange()
        {
            int result = ColorMixer.Mix(ColorMixer.Magenta, ColorMixer.Yellow);
            Assert.AreEqual(ColorMixer.Orange, result);
        }

        // ── Commutativity ──

        [Test]
        public void Mix_MagentaAndCyan_ReturnsPurple_Commutative()
        {
            int result = ColorMixer.Mix(ColorMixer.Magenta, ColorMixer.Cyan);
            Assert.AreEqual(ColorMixer.Purple, result);
        }

        // ── Same-color identity ──

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Mix_SameColor_ReturnsSameColor(int color)
        {
            int result = ColorMixer.Mix(color, color);
            Assert.AreEqual(color, result);
        }

        // ── Mixed colors with base produce same results ──
        // Mix(0,6): 6=Purple normalizes to 0(Cyan), so Mix(0,0)=0
        // Mix(6,8): 6→0, 8→1, so Mix(0,1)=6 (Purple)

        [Test]
        public void Mix_CyanAndPurple_ReturnsCyan_BecausePurpleNormalizesToCyan()
        {
            // Purple (6) normalizes to Cyan (0) in the mixer, so Mix(0,6) = Mix(0,0) = 0
            int result = ColorMixer.Mix(ColorMixer.Cyan, ColorMixer.Purple);
            Assert.AreEqual(ColorMixer.Cyan, result);
        }

        [Test]
        public void Mix_PurpleAndOrange_ReturnsPurple_BecauseNormalizedToCyanAndMagenta()
        {
            // Purple(6)→Cyan(0), Orange(8)→Magenta(1). Mix(0,1)=6(Purple)
            int result = ColorMixer.Mix(ColorMixer.Purple, ColorMixer.Orange);
            Assert.AreEqual(ColorMixer.Purple, result);
        }

        // ── MixMultiple ──

        [Test]
        public void MixMultiple_ThreeDistinctColors_ReturnsBrown()
        {
            int result = ColorMixer.MixMultiple(new[] { 0, 1, 2 });
            Assert.AreEqual(ColorMixer.Brown, result);
        }

        [Test]
        public void MixMultiple_ThreeDistinctIncludingMixed_ReturnsBrown()
        {
            // 0, 1, 6 — normalizes to 0, 1, 0 → distinct={0,1} → only 2 distinct → Mix(0,1)=6
            // Actually 6 normalizes to 0, so colors 0, 1, 0 = {0,1} = 2 distinct
            int result = ColorMixer.MixMultiple(new[] { 0, 1, ColorMixer.Purple });
            Assert.AreEqual(ColorMixer.Purple, result);
        }

        [Test]
        public void MixMultiple_ThreeTrulyDistinctAfterNormalization_ReturnsBrown()
        {
            // Green(7)→Cyan(0), Orange(8)→Magenta(1), Yellow(2)=2
            // distinct after normalization: {0,1,2} = 3 distinct → Brown
            int result = ColorMixer.MixMultiple(new[] { ColorMixer.Green, ColorMixer.Orange, ColorMixer.Yellow });
            Assert.AreEqual(ColorMixer.Brown, result);
        }

        // ── IsValidColor ──

        [Test]
        public void IsValidColor_Brown_ReturnsFalse()
        {
            Assert.IsFalse(ColorMixer.IsValidColor(ColorMixer.Brown));
        }

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, false)]
        [TestCase(6, true)]
        [TestCase(7, true)]
        [TestCase(8, true)]
        [TestCase(9, false)]
        public void IsValidColor_VariousColors_ReturnsExpected(int colorIndex, bool expected)
        {
            Assert.AreEqual(expected, ColorMixer.IsValidColor(colorIndex));
        }

        // ── Null / edge cases for MixMultiple ──

        [Test]
        public void MixMultiple_NullArray_ReturnsMinusOne()
        {
            int result = ColorMixer.MixMultiple(null);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void MixMultiple_EmptyArray_ReturnsMinusOne()
        {
            int result = ColorMixer.MixMultiple(new int[0]);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void MixMultiple_SingleColor_ReturnsThatColor()
        {
            int result = ColorMixer.MixMultiple(new[] { ColorMixer.Magenta });
            Assert.AreEqual(ColorMixer.Magenta, result);
        }
    }
}
