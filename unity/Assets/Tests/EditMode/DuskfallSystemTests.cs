// SPDX-License-Identifier: MIT
// Chroma Vale — DuskfallSystem unit tests (EditMode, NUnit)
// Spec: l8_duskfall_blackout_spec.md — countdown, arm/reset, soft-fail event.

#nullable enable

using System;
using NUnit.Framework;
using ChromaVale.Core.GameLogic;
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

        // ── Tick semantics ───────────────────────────────────────────────

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
            dusk.OnBrownCleared += () => released = true;
            dusk.OnBrownCleared += () => released = true;
            int releaseCount = 0;
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
