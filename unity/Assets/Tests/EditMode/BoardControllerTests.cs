// SPDX-License-Identifier: MIT
// Chroma Vale — BoardController unit tests (EditMode, NUnit)

#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;

namespace ChromaVale.Tests
{
    /// <summary>
    /// Test suite for BoardController: initialization, merging, win condition,
    /// star rating, events, and edge cases.
    /// </summary>
    public class BoardControllerTests
    {
        // ── Helpers ──────────────────────────────────────────────────────

        private static LevelData MakeLevel(int w, int h, int par,
            RestorationTarget[]? targets = null,
            MergeOrbPlacement[]? orbs = null,
            bool mixingEnabled = true)
        {
            return new LevelData
            {
                Width = w,
                Height = h,
                ParMoves = par,
                MixingEnabled = mixingEnabled,
                RestorationTargets = targets ?? Array.Empty<RestorationTarget>(),
                MergeOrbs = orbs ?? Array.Empty<MergeOrbPlacement>(),
                Sources = Array.Empty<LevelSource>(),
                Targets = Array.Empty<LevelTarget>(),
                Obstacles = Array.Empty<LevelObstacle>(),
                SignalGates = Array.Empty<LevelSignalGate>(),
                GhostTraces = Array.Empty<GhostTrace>(),
                ImpedanceCells = Array.Empty<ImpedanceCell>(),
                Inventory = Array.Empty<TraceSegment>(),
                DisplayName = "Test"
            };
        }

        private static BoardController NewBoard(int w, int h, int par,
            RestorationTarget[]? targets = null,
            MergeOrbPlacement[]? orbs = null,
            bool mixingEnabled = true)
        {
            var ctl = new BoardController();
            ctl.Initialize(MakeLevel(w, h, par, targets, orbs, mixingEnabled));
            return ctl;
        }

        private static GridPosition P(int x, int y) => new(x, y);

        // ── Initialize ───────────────────────────────────────────────────

        [Test]
        public void Initialize_PopulatesGridCorrectly()
        {
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(2, 1, OrbColor.Magenta, OrbTier.T3),
            };
            var ctl = NewBoard(4, 3, 0, null, orbs);

            Assert.AreEqual(4, ctl.Width);
            Assert.AreEqual(3, ctl.Height);
            Assert.AreEqual(0, ctl.MoveCount);
            Assert.IsFalse(ctl.IsLevelComplete);

            var a = ctl.GetOrbAt(P(0, 0));
            Assert.IsNotNull(a);
            Assert.AreEqual(OrbColor.Cyan, a!.Color);
            Assert.AreEqual(OrbTier.T1, a.Tier);

            var b = ctl.GetOrbAt(P(2, 1));
            Assert.IsNotNull(b);
            Assert.AreEqual(OrbColor.Magenta, b!.Color);
            Assert.AreEqual(OrbTier.T3, b.Tier);

            // Empty cell returns null.
            Assert.IsNull(ctl.GetOrbAt(P(1, 0)));
        }

        [Test]
        public void Initialize_WithoutMergeOrbs_HasEmptyGrid()
        {
            var ctl = NewBoard(3, 3, 0);
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                Assert.IsNull(ctl.GetOrbAt(new GridPosition(x, y)));
        }

        [Test]
        public void Initialize_NullLevel_Throws()
        {
            var ctl = new BoardController();
            Assert.Throws<ArgumentNullException>(() => ctl.Initialize(null!));
        }

        // ── TryMergeAt — Tier Merge ──────────────────────────────────────

        [Test]
        public void TryMergeAt_ValidTierMerge_UpdatesBoardAndCount()
        {
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            });

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsTrue(success);
            Assert.AreEqual(1, ctl.MoveCount);

            // Source consumed.
            Assert.IsNull(ctl.GetOrbAt(P(0, 0)));

            // Target has tiered-up orb.
            var merged = ctl.GetOrbAt(P(1, 0));
            Assert.IsNotNull(merged);
            Assert.AreEqual(OrbColor.Cyan, merged!.Color);
            Assert.AreEqual(OrbTier.T2, merged.Tier);
        }

        // ── TryMergeAt — Invalid ─────────────────────────────────────────

        [Test]
        public void TryMergeAt_InvalidMerge_ReturnsFalseAndUnchanged()
        {
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T2), // different tier
            });

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsFalse(success);
            Assert.AreEqual(0, ctl.MoveCount);

            // Both orbs still in place.
            Assert.IsNotNull(ctl.GetOrbAt(P(0, 0)));
            Assert.IsNotNull(ctl.GetOrbAt(P(1, 0)));
        }

        [Test]
        public void TryMergeAt_SourceOrTargetEmpty_ReturnsFalse()
        {
            var ctl = NewBoard(3, 3, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                // (1,0) is empty
            });

            Assert.IsFalse(ctl.TryMergeAt(P(0, 0), P(1, 0)));
            Assert.AreEqual(0, ctl.MoveCount);

            // Nothing moved.
            Assert.IsNull(ctl.GetOrbAt(P(1, 0)));
        }

        [Test]
        public void TryMergeAt_OutOfBounds_ReturnsFalse()
        {
            var ctl = NewBoard(3, 3, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
            });

            Assert.IsFalse(ctl.TryMergeAt(P(0, 0), P(99, 0)));
            Assert.IsFalse(ctl.TryMergeAt(P(-1, 0), P(0, 0)));
            Assert.AreEqual(0, ctl.MoveCount);
        }

        // ── TryMergeAt — Color Mix ───────────────────────────────────────

        [Test]
        public void TryMergeAt_ColorMix_PlacesNewOrb()
        {
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Magenta, OrbTier.T1),
            });

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsTrue(success);
            Assert.AreEqual(1, ctl.MoveCount);

            // Source consumed.
            Assert.IsNull(ctl.GetOrbAt(P(0, 0)));

            // Target has Purple (Cyan+Magenta) T1.
            var merged = ctl.GetOrbAt(P(1, 0));
            Assert.IsNotNull(merged);
            Assert.AreEqual(OrbColor.Purple, merged!.Color);
            Assert.AreEqual(OrbTier.T1, merged.Tier);
        }

        // ── TryMergeAt — Mixing Gate (mixingEnabled=false) ──────────────

        [Test]
        public void TryMergeAt_MixingDisabled_CrossColor_ReturnsFalseAndUnchanged()
        {
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Magenta, OrbTier.T1),
            }, mixingEnabled: false);

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsFalse(success);
            Assert.AreEqual(0, ctl.MoveCount);

            // Both orbs still in place, untouched.
            var a = ctl.GetOrbAt(P(0, 0));
            var b = ctl.GetOrbAt(P(1, 0));
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual(OrbColor.Cyan, a!.Color);
            Assert.AreEqual(OrbColor.Magenta, b!.Color);
        }

        [Test]
        public void TryMergeAt_MixingDisabled_SameColor_StillMerges()
        {
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            }, mixingEnabled: false);

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsTrue(success);
            Assert.AreEqual(1, ctl.MoveCount);

            var merged = ctl.GetOrbAt(P(1, 0));
            Assert.IsNotNull(merged);
            Assert.AreEqual(OrbColor.Cyan, merged!.Color);
            Assert.AreEqual(OrbTier.T2, merged.Tier);
        }

        [Test]
        public void TryMergeAt_MixingEnabled_CrossColor_StillMixes()
        {
            // Explicit true = legacy default behavior.
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Magenta, OrbTier.T1),
            }, mixingEnabled: true);

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsTrue(success);
            Assert.AreEqual(1, ctl.MoveCount);
            var merged = ctl.GetOrbAt(P(1, 0));
            Assert.IsNotNull(merged);
            Assert.AreEqual(OrbColor.Purple, merged!.Color);
        }

        // ── TryMergeAt — Brown Production ────────────────────────────────

        [Test]
        public void TryMergeAt_BrownProduction_PlacesBrownAtTarget()
        {
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Purple, OrbTier.T1),    // secondary
                new MergeOrbPlacement(1, 0, OrbColor.Green, OrbTier.T1),     // secondary
            });

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsTrue(success);
            Assert.AreEqual(1, ctl.MoveCount);

            // Source consumed.
            Assert.IsNull(ctl.GetOrbAt(P(0, 0)));

            // Target has Brown T1.
            var merged = ctl.GetOrbAt(P(1, 0));
            Assert.IsNotNull(merged);
            Assert.IsTrue(merged!.IsBrown);
            Assert.AreEqual(OrbTier.T1, merged.Tier);
        }

        // ── TryMergeAt — Brown Clear ─────────────────────────────────────

        [Test]
        public void TryMergeAt_BrownClear_RemovesBoth()
        {
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Brown, OrbTier.T2),
                new MergeOrbPlacement(1, 0, OrbColor.Brown, OrbTier.T2),
            });

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsTrue(success);
            Assert.AreEqual(1, ctl.MoveCount);

            // Both cells empty.
            Assert.IsNull(ctl.GetOrbAt(P(0, 0)));
            Assert.IsNull(ctl.GetOrbAt(P(1, 0)));
        }

        // ── Win Condition ────────────────────────────────────────────────

        [Test]
        public void WinCondition_AllTargetsFilled_IsComplete()
        {
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            };
            var targets = new[]
            {
                new RestorationTarget(1, 0, OrbColor.Cyan, OrbTier.T2),
            };

            var ctl = NewBoard(4, 4, 3, targets, orbs);

            LevelResult? result = null;
            ctl.OnLevelComplete += r => result = r;

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsTrue(success);
            Assert.IsTrue(ctl.IsLevelComplete);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result!.MovesUsed);
            Assert.AreEqual(3, result.Par);
            Assert.AreEqual(3, result.Stars); // 1 ≤ 3 → 3★
        }

        [Test]
        public void WinCondition_NotAllTargets_NotComplete()
        {
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            };
            // Purple T2 cannot be produced from two cyan T1s — win must NOT trigger.
            var targets = new[]
            {
                new RestorationTarget(3, 3, OrbColor.Purple, OrbTier.T2),
            };

            var ctl = NewBoard(5, 5, 5, targets, orbs);
            ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsFalse(ctl.IsLevelComplete);
        }

        [Test]
        public void WinCondition_PositionAgnostic_TargetAtCorner_OrbElsewhere_StillWins()
        {
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            };
            // Target sits at a corner cell (3,3) that stays empty — the produced
            // T2 orb lands at (1,0). Position-agnostic CheckWin must still win.
            var targets = new[]
            {
                new RestorationTarget(3, 3, OrbColor.Cyan, OrbTier.T2),
            };

            var ctl = NewBoard(4, 4, 3, targets, orbs);

            bool success = ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsTrue(success);
            Assert.IsTrue(ctl.IsLevelComplete);
        }

        // ── Star Rating ─────────────────────────────────────────────────

        [Test]
        public void StarRating_MovesEqualPar_3Stars()
        {
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            };
            var target = new[] { new RestorationTarget(1, 0, OrbColor.Cyan, OrbTier.T2) };

            LevelResult? result = null;
            var ctl = NewBoard(4, 4, 1, target, orbs);
            ctl.OnLevelComplete += r => result = r;
            ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsNotNull(result);
            // moves = 1, par = 1 → 1 ≤ 1 → 3★
            Assert.AreEqual(3, result!.Stars);
        }

        [Test]
        public void StarRating_MovesParPlusOne_2Stars()
        {
            // Two merges required to reach target cell, par=2:
            // Pair A at (0,0)+(1,0) → C2 at (1,0) [m1]
            // Pair B at (0,1)+(1,1) → C2 at (1,1) [m2]; then C2+C2 → C3 at (1,1) [m3]
            // target = C3 at (1,1). moves=3, par=2 → 3 ≤ 2×1.5 (=3) → 2★
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(0, 1, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 1, OrbColor.Cyan, OrbTier.T1),
            };
            var target = new[] { new RestorationTarget(1, 1, OrbColor.Cyan, OrbTier.T3) };

            LevelResult? result = null;
            var ctl = NewBoard(4, 4, 2, target, orbs);
            ctl.OnLevelComplete += r => result = r;

            ctl.TryMergeAt(P(0, 0), P(1, 0)); // m1 → C2 at (1,0)
            Assert.AreEqual(1, ctl.MoveCount);
            Assert.IsFalse(ctl.IsLevelComplete);

            ctl.TryMergeAt(P(0, 1), P(1, 1)); // m2 → C2 at (1,1)
            ctl.TryMergeAt(P(1, 0), P(1, 1)); // m3 → C3 at (1,1) = target ✓

            Assert.IsTrue(ctl.IsLevelComplete);
            Assert.IsNotNull(result);
            // moves = 3, par = 2 → 3 ≤ 2×1.5 (=3) → 2★
            Assert.AreEqual(2, result!.Stars);
        }

        [Test]
        public void StarRating_MovesParPlusThree_1Star()
        {
            // Three merges required to reach target.
            // (0,0)C1+(1,0)C1 → C2@(1,0)  [m1]
            // (0,1)C1+(1,1)C1 → C2@(1,1)  [m2]
            // (1,0)C2+(1,1)C2 → C3@(1,1)  [m3] target = C3@(1,1)
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(0, 1, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 1, OrbColor.Cyan, OrbTier.T1),
            };
            var target = new[] { new RestorationTarget(1, 1, OrbColor.Cyan, OrbTier.T3) };

            LevelResult? result = null;
            var ctl = NewBoard(4, 4, 0, target, orbs);
            ctl.OnLevelComplete += r => result = r;

            ctl.TryMergeAt(P(0, 0), P(1, 0)); // m1
            ctl.TryMergeAt(P(0, 1), P(1, 1)); // m2
            ctl.TryMergeAt(P(1, 0), P(1, 1)); // m3 → C3 at (1,1) = target ✓

            Assert.IsTrue(ctl.IsLevelComplete);
            Assert.IsNotNull(result);
            // moves = 3, par = 0 → 3 > 0×1.5 (=0) → 1★
            Assert.AreEqual(1, result!.Stars);
        }

        // ── Events ───────────────────────────────────────────────────────

        [Test]
        public void OnBoardChanged_FiresOnValidMerge()
        {
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            };
            var ctl = NewBoard(4, 4, 5, null, orbs);

            var changes = new List<BoardChange>();
            ctl.OnBoardChanged += changes.Add;

            ctl.TryMergeAt(P(0, 0), P(1, 0));

            // Expect 2 events: source removed, target transformed.
            Assert.AreEqual(2, changes.Count);

            var ev0 = changes[0];
            Assert.AreEqual(ChangeType.OrbRemoved, ev0.Type);
            Assert.AreEqual(P(0, 0), ev0.Position);
            Assert.IsNotNull(ev0.OldOrb);
            Assert.AreEqual(OrbColor.Cyan, ev0.OldOrb!.Color);
            Assert.AreEqual(OrbTier.T1, ev0.OldOrb.Tier);
            Assert.IsNull(ev0.NewOrb);

            var ev1 = changes[1];
            Assert.AreEqual(ChangeType.OrbTransformed, ev1.Type);
            Assert.AreEqual(P(1, 0), ev1.Position);
            Assert.IsNotNull(ev1.OldOrb);
            Assert.AreEqual(OrbColor.Cyan, ev1.OldOrb!.Color);
            Assert.AreEqual(OrbTier.T1, ev1.OldOrb.Tier);
            Assert.IsNotNull(ev1.NewOrb);
            Assert.AreEqual(OrbColor.Cyan, ev1.NewOrb!.Color);
            Assert.AreEqual(OrbTier.T2, ev1.NewOrb.Tier);
        }

        [Test]
        public void OnBoardChanged_BrownClear_FiresTwoRemoved()
        {
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Brown, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Brown, OrbTier.T1),
            };
            var ctl = NewBoard(4, 4, 5, null, orbs);

            var changes = new List<BoardChange>();
            ctl.OnBoardChanged += changes.Add;

            ctl.TryMergeAt(P(0, 0), P(1, 0));

            // 2 OrbRemoved events, neither has a NewOrb.
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(ChangeType.OrbRemoved, changes[0].Type);
            Assert.AreEqual(ChangeType.OrbRemoved, changes[1].Type);
            Assert.IsNull(changes[0].NewOrb);
            Assert.IsNull(changes[1].NewOrb);
        }

        [Test]
        public void OnBoardChanged_InvalidMerge_NoEvents()
        {
            var ctl = NewBoard(4, 4, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T2),
            });

            int eventsFired = 0;
            ctl.OnBoardChanged += _ => eventsFired++;

            Assert.IsFalse(ctl.TryMergeAt(P(0, 0), P(1, 0)));
            Assert.AreEqual(0, eventsFired);
        }

        [Test]
        public void OnLevelComplete_FiresOnWin()
        {
            var orbs = new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            };
            var target = new[] { new RestorationTarget(1, 0, OrbColor.Cyan, OrbTier.T2) };

            LevelResult? result = null;
            var ctl = NewBoard(4, 4, 2, target, orbs);
            ctl.OnLevelComplete += r => result = r;

            ctl.TryMergeAt(P(0, 0), P(1, 0));

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result!.MovesUsed);
            Assert.AreEqual(2, result.Par);
        }

        // ── Adjacency ────────────────────────────────────────────────────

        [Test]
        public void TryMergeAt_NonAdjacent_FreeDrag_ReturnsTrue()
        {
            var ctl = NewBoard(5, 5, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(2, 0, OrbColor.Cyan, OrbTier.T1),
            });

            // Free-drag (playtest fix 2026-08-19): any cell to any cell merges,
            // even with a gap — otherwise T2+T2 merges after a row merge land on
            // non-adjacent cells and the level is unsolvable.
            Assert.IsTrue(ctl.TryMergeAt(P(0, 0), P(2, 0)));
            Assert.AreEqual(1, ctl.MoveCount);
        }

        [Test]
        public void TryMergeAt_SameCell_ReturnsFalse()
        {
            var ctl = NewBoard(3, 3, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
            });

            // Dropping an orb on its own cell is a snap-back (UI), never a merge —
            // free-drag must not allow an orb to self-upgrade in place.
            Assert.IsFalse(ctl.TryMergeAt(P(0, 0), P(0, 0)));
        }

        // ── Edge Cases ───────────────────────────────────────────────────

        [Test]
        public void CalculateStars_StaticMethod()
        {
            // GDD §5.5 canon (designer ruling 2026-08-19): 3★ ≤ par, 2★ ≤ par×1.5, 1★ > par×1.5
            Assert.AreEqual(3, BoardController.CalculateStars(0, 0));   // 0 ≤ 0 → 3
            Assert.AreEqual(3, BoardController.CalculateStars(1, 5));   // 1 ≤ 5 → 3
            Assert.AreEqual(3, BoardController.CalculateStars(2, 2));   // moves == par → 3
            Assert.AreEqual(2, BoardController.CalculateStars(3, 2));   // 3 ≤ 3 (2×1.5) → 2
            Assert.AreEqual(2, BoardController.CalculateStars(5, 4));   // 5 ≤ 6 → 2
            Assert.AreEqual(2, BoardController.CalculateStars(6, 4));   // 6 ≤ 6 boundary → 2
            Assert.AreEqual(1, BoardController.CalculateStars(7, 4));   // 7 > 6 → 1
            Assert.AreEqual(1, BoardController.CalculateStars(99, 0));  // 99 > 0 → 1
        }

        [Test]
        public void TryMergeAt_NullSourceOrTarget_ReturnsFalse()
        {
            var ctl = NewBoard(3, 3, 5, null, new[]
            {
                new MergeOrbPlacement(0, 0, OrbColor.Cyan, OrbTier.T1),
                new MergeOrbPlacement(1, 0, OrbColor.Cyan, OrbTier.T1),
            });

            Assert.IsFalse(ctl.TryMergeAt(null!, P(1, 0)));
            Assert.IsFalse(ctl.TryMergeAt(P(0, 0), null!));
        }
    }
}