// SPDX-License-Identifier: MIT
// Chroma Vale — DuskfallSystem unit tests (EditMode, NUnit)
//
// DESIGN_CANON v2.3.0 (2026-08-23): Duskfall (Brown trap / countdown fail-state)
// and the L8 "Duskfall Blackout" wiring were DEFERRED to Act IV. The DuskfallSystem
// code remains shipped but DISABLED in Levels 1-10 — it is a correct no-op unless a
// level explicitly sets duskEnabled=true (never the case for L1-10). These tests pin
// that deferred (safe, inert) behavior and the opt-in arming path used when Act IV ships.

#nullable enable

using System;
using NUnit.Framework;
using ChromaVale.Core.GameLogic;
using ChromaVale.Infrastructure.LevelData;
using ChromaVale.Domain.PuzzleBoard;
namespace ChromaVale.Tests
{
    public class DuskfallSystemTests
    {
        private static LevelData MakeLevel(bool duskEnabled, int beats = 6,
            MergeOrbPlacement[]? orbs = null,
            RestorationTarget[]? targets = null)
        {
            return new LevelData
            {
                Width = 4,
                Height = 4,
                ParMoves = 8,
                MixingEnabled = true,
                DuskEnabled = duskEnabled,
                DuskBeats = beats,
                RestorationTargets = targets ?? Array.Empty<RestorationTarget>(),
                MergeOrbs = orbs ?? Array.Empty<MergeOrbPlacement>(),
                Sources = Array.Empty<LevelSource>(),
                Targets = Array.Empty<LevelTarget>(),
                Obstacles = Array.Empty<LevelObstacle>(),
                SignalGates = Array.Empty<LevelSignalGate>(),
                GhostTraces = Array.Empty<GhostTrace>(),
                ImpedanceCells = Array.Empty<ImpedanceCell>(),
                Inventory = Array.Empty<TraceSegment>(),
                DisplayName = "DuskTest"
            };
        }

        // ── Construction ─────────────────────────────────────────────────

        [Test]
        public void Disabled_WhenLevelFlagOff()
        {
            var dusk = new DuskfallSystem(MakeLevel(duskEnabled: false));
            Assert.IsFalse(dusk.Enabled);
            Assert.AreEqual(0, dusk.Counter);
            Assert.IsFalse(dusk.Armed);
        }

        [Test]
        public void Defaults_FullBeats_NotArmed()
        {
            // System is enabled (opt-in path used when Act IV ships), but with no
            // Browns on the board it stays disarmed and the counter is full.
            var dusk = new DuskfallSystem(MakeLevel(duskEnabled: true, beats: 6));
            Assert.IsTrue(dusk.Enabled);
            Assert.AreEqual(6, dusk.Counter);
            Assert.AreEqual(1f, dusk.CounterRatio);
            Assert.IsFalse(dusk.Armed);
        }

        [Test]
        public void NonPositiveBeats_FallBackToDefault()
        {
            var dusk = new DuskfallSystem(MakeLevel(duskEnabled: true, beats: 0));
            Assert.AreEqual(DuskfallSystem.DefaultDuskBeats, dusk.DuskBeats);
        }

        [Test]
        public void Level1Through10_Deferred_DuskDisabled()
        {
            // DESIGN_CANON §11: every L1-10 level ships with duskEnabled=false.
            // The DuskfallSystem must be inert for the entire shipped act.
            for (int i = 1; i <= 10; i++)
            {
                MergeLevelRepository repo = new MergeLevelRepository(new ResourcesLevelJsonProvider());
                LevelData level = repo.GetMergeLevel(i);
                Assert.IsFalse(level.DuskEnabled, $"Level {i} must not enable Duskfall (deferred to Act IV)");
                var dusk = new DuskfallSystem(level);
                Assert.IsFalse(dusk.Enabled, $"Level {i} DuskfallSystem must be disabled");
                Assert.IsFalse(dusk.Armed, $"Level {i} must not arm (no Browns in L1-10)");
            }
        }

        // ── Tick semantics (opt-in arming path; exercised only when Act IV enables it) ──

        [Test]
        public void TickWithoutBrowns_IsNoOp()
        {
            var board = new BoardController();
            board.Initialize(MakeLevel(true));
            var dusk = board.Duskfall!;
            dusk.TickForTest();
            Assert.AreEqual(6, dusk.Counter); // never armed, no browns
        }

        [Test]
        public void ArmThenTick_DecrementsOncePerMove()
        {
            var board = new BoardController();
            board.Initialize(MakeLevel(true, 6, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Brown, OrbTier.T1),
            }));
            var dusk = board.Duskfall!;
            Assert.IsTrue(dusk.Armed); // pre-placed Brown arms at Initialize
            dusk.TickForTest();
            Assert.AreEqual(5, dusk.Counter);
            dusk.TickForTest();
            Assert.AreEqual(4, dusk.Counter);
        }

        [Test]
        public void CounterReachesZero_StaysAtZero_NoDoubleFire()
        {
            var board = new BoardController();
            board.Initialize(MakeLevel(true, 2, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Brown, OrbTier.T1),
            }));
            var dusk = board.Duskfall!;
            int fired = 0;
            dusk.OnDuskfall += () => fired++;
            dusk.TickForTest();
            dusk.TickForTest();
            dusk.TickForTest(); // already at zero
            Assert.AreEqual(0, dusk.Counter);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void ClearAllBrowns_ResetsAndFiresRelease()
        {
            var board = new BoardController();
            board.Initialize(MakeLevel(true, 6, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Brown, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            }));
            var dusk = board.Duskfall!;
            dusk.TickForTest();
            dusk.TickForTest();
            Assert.AreEqual(4, dusk.Counter);

            // Simulate the last Brown leaving the board.
            ((BoardController)board).DebugClearCell(0, 0);
            bool released = false;
            int releaseCount = 0;
            dusk.OnBrownCleared += () => released = true;
            dusk.OnBrownCleared += () => releaseCount++;
            dusk.TickForTest();

            Assert.IsTrue(released, "OnBrownCleared did not fire");
            Assert.AreEqual(1, releaseCount, "release event must fire exactly once per clear");
            Assert.AreEqual(6, dusk.Counter, "counter resets to full beats");
            Assert.IsFalse(dusk.Armed, "no Browns left → disarmed");
        }

        // ── Integration through TryMergeAt ───────────────────────────────

        [Test]
        public void RealMerge_DecrementsCounter()
        {
            var targets = new[] { new RestorationTarget(3, 3, OrbColor.Cyan, (OrbTier)3) };
            var board = new BoardController();
            board.Initialize(MakeLevel(true, 6, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Brown, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(2, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(3, 0, OrbColor.Cyan, OrbTier.T2),
            }, targets));
            var dusk = board.Duskfall!;

            // Cyan T1 + T1 → T2 (real move; Brown still on board).
            bool moved = board.TryMergeAt(new GridPosition(1, 0), new GridPosition(2, 0));
            Assert.IsTrue(moved);
            Assert.AreEqual(5, dusk.Counter);
        }
    }
}
