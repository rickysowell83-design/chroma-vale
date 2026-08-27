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
            _repository = new MergeLevelRepository(new TestFixtureJsonProvider());
        }

        [Test]
        public void GetMergeLevel_Level1_ReturnsCorrectData()
        {
            LevelData level = _repository.GetMergeLevel(1);

            Assert.AreEqual(4, level.Width, "Level 1 grid width");
            Assert.AreEqual(4, level.Height, "Level 1 grid height");
            Assert.AreEqual(3, level.ParMoves, "Level 1 par moves (canon)");

            Assert.AreEqual(6, level.MergeOrbs.Length, "Level 1 orb count");
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
            Assert.AreEqual(8, level.ParMoves, "Level 4 par moves (canon)");

            Assert.AreEqual(8, level.MergeOrbs.Length, "Level 4 orb count");
            Assert.AreEqual(4, level.MergeOrbs.Count(o => o.Color == OrbColor.Cyan), "Level 4 Cyan orbs");
            Assert.AreEqual(4, level.MergeOrbs.Count(o => o.Color == OrbColor.Magenta), "Level 4 Magenta orbs");

            Assert.AreEqual(1, level.RestorationTargets.Length, "Level 4 target count");
            Assert.AreEqual(OrbColor.Purple, level.RestorationTargets[0].Color, "Level 4 target is Purple T2");
            Assert.AreEqual(OrbTier.T2, level.RestorationTargets[0].Tier, "Level 4 target is Purple T2");
        }

        [Test]
        public void GetMergeLevel_Level8_StillWaters_NoBrown_ThreeT2Targets()
        {
            // DESIGN_CANON §11: L8 "Still Waters" is a calm breather — NO brown, NO obstacles,
            // NO Duskfall (Duskfall/Brown deferred to Act IV per canon v2.3.0). Targets are the
            // three primary T2s (Cyan/Magenta/Yellow T2) so it is always solvable.
            LevelData level = _repository.GetMergeLevel(8);

            Assert.AreEqual(6, level.Width, "Level 8 grid width");
            Assert.AreEqual(6, level.Height, "Level 8 grid height");
            Assert.AreEqual(8, level.ParMoves, "Level 8 par moves (canon)");

            Assert.IsFalse(
                level.MergeOrbs.Any(o => o.Color == OrbColor.Brown),
                "Level 8 (Still Waters) must NOT include Brown orbs");
            Assert.IsEmpty(level.Obstacles, "Level 8 (Still Waters) must have no obstacles");
            Assert.AreEqual(3, level.RestorationTargets.Length, "Level 8 target count");
            Assert.IsTrue(
                level.RestorationTargets.All(t => t.Tier == OrbTier.T2),
                "Level 8 targets should all be primary T2");
        }

        [Test]
        public void GetMergeLevel_Level10_NoObstacles_ThreeT2Targets()
        {
            // Obstacles were deferred (t_40de6adf); L10 "Convergence" ships with no obstacles.
            LevelData level = _repository.GetMergeLevel(10);

            Assert.AreEqual(7, level.Width, "Level 10 grid width");
            Assert.AreEqual(7, level.Height, "Level 10 grid height");
            Assert.AreEqual(15, level.ParMoves, "Level 10 par moves (canon)");

            Assert.AreEqual(3, level.RestorationTargets.Length, "Level 10 target count");
            Assert.IsEmpty(level.Obstacles, "Level 10 should have NO obstacles (deferred)");
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
        public void GetMergeLevel_FixturesWithoutKey_DefaultMixingEnabledTrue()
        {
            // Current validator fixtures carry no "mixingEnabled" key —
            // backward-compatible default must be true (mixing allowed).
            for (int i = 1; i <= 10; i++)
            {
                LevelData level = _repository.GetMergeLevel(i);
                Assert.IsTrue(level.MixingEnabled, $"Level {i} should default MixingEnabled to true when the key is absent");
            }
        }

        [Test]
        public void GetMergeLevel_MixingEnabledFalse_ParsesFalse()
        {
            var repo = new MergeLevelRepository(new InlineJsonProvider(
                "{\"grid\":{\"width\":3,\"height\":3},\"parMoves\":2,\"displayName\":\"Gate\","
                + "\"mixingEnabled\":false,\"orbs\":[],\"targets\":[]}"));

            LevelData level = repo.GetMergeLevel(1);

            Assert.IsFalse(level.MixingEnabled, "explicit \"mixingEnabled\":false must parse to false");
        }

        [Test]
        public void GetMergeLevel_MixingEnabledTrue_ParsesTrue()
        {
            var repo = new MergeLevelRepository(new InlineJsonProvider(
                "{\"grid\":{\"width\":3,\"height\":3},\"parMoves\":2,\"displayName\":\"Gate\","
                + "\"mixingEnabled\":true,\"orbs\":[],\"targets\":[]}"));

            LevelData level = repo.GetMergeLevel(1);

            Assert.IsTrue(level.MixingEnabled, "explicit \"mixingEnabled\":true must parse to true");
        }

        [Test]
        public void LevelCount_Is10()
        {
            Assert.AreEqual(10, _repository.LevelCount);
        }

        /// <summary>
        /// Minimal provider that returns a single canned JSON string for any level
        /// number — lets tests exercise parse paths without touching fixtures.
        /// </summary>
        private sealed class InlineJsonProvider : ILevelJsonProvider
        {
            private readonly string _json;

            public InlineJsonProvider(string json) => _json = json;

            public string GetLevelJson(int levelNumber) => _json;
        }
    }
}
