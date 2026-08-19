// SPDX-License-Identifier: MIT
// Chroma Vale — MergeLevelRepository tests (EditMode, NUnit).
// Verifies the validator JSON fixtures (tools/MergeLevelValidator/tests/fixtures/level_01..10.json)
// load into correctly populated LevelData objects.

using System.Linq;
using NUnit.Framework;
using ChromaVale.Core.GameLogic;

namespace ChromaVale.Tests
{
    /// <summary>
    /// Tests for MergeLevelRepository: fixture discovery, JSON parsing, enum mapping,
    /// and merge-mode field population (MergeOrbs / RestorationTargets / ParMoves).
    /// </summary>
    public class MergeLevelRepositoryTests
    {
        private MergeLevelRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _repository = new MergeLevelRepository();
        }

        [Test]
        public void GetMergeLevel_Level1_ReturnsCorrectData()
        {
            LevelData level = _repository.GetMergeLevel(1);

            Assert.AreEqual(4, level.Width, "Level 1 grid width");
            Assert.AreEqual(4, level.Height, "Level 1 grid height");
            Assert.AreEqual(3, level.ParMoves, "Level 1 par moves");

            Assert.AreEqual(4, level.MergeOrbs.Length, "Level 1 orb count");
            Assert.IsTrue(
                level.MergeOrbs.All(o => o.Color == OrbColor.Cyan && o.Tier == OrbTier.T1),
                "Level 1 should be four Cyan T1 orbs");

            Assert.AreEqual(1, level.RestorationTargets.Length, "Level 1 target count");
            RestorationTarget target = level.RestorationTargets[0];
            Assert.AreEqual(3, target.X, "Level 1 target X");
            Assert.AreEqual(3, target.Y, "Level 1 target Y");
            Assert.AreEqual(OrbColor.Cyan, target.Color, "Level 1 target color");
            Assert.AreEqual(OrbTier.T2, target.Tier, "Level 1 target tier");
        }

        [Test]
        public void GetMergeLevel_Level4_HasMixedColors()
        {
            LevelData level = _repository.GetMergeLevel(4);

            Assert.AreEqual(5, level.Width, "Level 4 grid width");
            Assert.AreEqual(5, level.Height, "Level 4 grid height");
            Assert.AreEqual(4, level.ParMoves, "Level 4 par moves");

            Assert.AreEqual(8, level.MergeOrbs.Length, "Level 4 orb count");
            Assert.AreEqual(4, level.MergeOrbs.Count(o => o.Color == OrbColor.Cyan), "Level 4 Cyan orbs");
            Assert.AreEqual(4, level.MergeOrbs.Count(o => o.Color == OrbColor.Magenta), "Level 4 Magenta orbs");

            Assert.AreEqual(2, level.RestorationTargets.Length, "Level 4 target count");
            Assert.IsTrue(
                level.RestorationTargets.All(t => t.Color == OrbColor.Purple),
                "Level 4 targets should both be Purple");
        }

        [Test]
        public void GetMergeLevel_Level8_HasBrownOrbs()
        {
            LevelData level = _repository.GetMergeLevel(8);

            Assert.AreEqual(6, level.Width, "Level 8 grid width");
            Assert.AreEqual(6, level.Height, "Level 8 grid height");
            Assert.AreEqual(9, level.ParMoves, "Level 8 par moves");

            Assert.IsTrue(
                level.MergeOrbs.Any(o => o.Color == OrbColor.Brown && o.Tier == OrbTier.T1),
                "Level 8 should include Brown T1 orbs");
            Assert.AreEqual(2, level.RestorationTargets.Length, "Level 8 target count");
        }

        [Test]
        public void GetMergeLevel_Level10_HasObstacles()
        {
            LevelData level = _repository.GetMergeLevel(10);

            Assert.AreEqual(7, level.Width, "Level 10 grid width");
            Assert.AreEqual(6, level.Height, "Level 10 grid height");
            Assert.AreEqual(15, level.ParMoves, "Level 10 par moves");

            Assert.AreEqual(3, level.RestorationTargets.Length, "Level 10 target count");
            Assert.IsNotNull(level.Obstacles, "Level 10 should have an obstacles array");
            Assert.Greater(level.Obstacles.Length, 0, "Level 10 should have obstacles present");
            Assert.IsTrue(
                level.Obstacles.All(o => o.X >= 0 && o.X < level.Width && o.Y >= 0 && o.Y < level.Height),
                "Level 10 obstacles must lie inside the grid");
        }

        [Test]
        public void GetMergeLevel_All10_LoadWithoutError()
        {
            for (int i = 1; i <= 10; i++)
            {
                LevelData level = _repository.GetMergeLevel(i);
                Assert.Greater(level.ParMoves, 0, $"Level {i} must have ParMoves > 0");
                Assert.Greater(level.Width, 0, $"Level {i} must have a positive grid Width");
                Assert.Greater(level.Height, 0, $"Level {i} must have a positive grid Height");
            }
        }

        [Test]
        public void LevelCount_Is10()
        {
            Assert.AreEqual(10, _repository.LevelCount);
        }
    }
}
