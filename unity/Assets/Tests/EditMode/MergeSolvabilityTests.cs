// SPDX-License-Identifier: MIT
// Chroma Vale — Merge solvability tests (EditMode, NUnit).
//
// For every validator fixture (tools/MergeLevelValidator/tests/fixtures/level_01..10.json):
//   1. Load the fixture via MergeLevelRepository.GetMergeLevel(n).
//   2. Run a BFS over merge moves in orb-multiset space (the same model the
//      validator Solver uses: free-drag merges, position-agnostic win).
//   3. REPLAY the found solution path on a real BoardController by locating
//      actual cells holding the required orbs and calling TryMergeAt(...)
//      — the exact API the game's presentation layer uses.
//   4. Assert every replay move succeeds, the win condition triggers, and the
//      move count matches the BFS minimum.
//
// Reports per level: solvable? minimum moves found? does the win condition
// trigger? star rating vs par (GDD 5.5: 3★ ≤ par, 2★ ≤ par×1.5, else 1★).
//
// Negative control (level_neg_unsolvable.json): 3 Cyan T1 orbs can never make
// a Cyan T3 — BFS must exhaust with no solution and a real BoardController
// must never flip IsLevelComplete.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;

namespace ChromaVale.Tests
{
    /// <summary>
    /// Verifies every validator-authored merge fixture is solvable through the
    /// real BoardController merge path, and that the negative control is not.
    /// </summary>
    public class MergeSolvabilityTests
    {
        // ── Public API ──────────────────────────────────────────────────

        [Test]
        public void AllValidatorLevels_AreSolvable_WinConditionTriggers()
        {
            var repo = new MergeLevelRepository(new TestFixtureJsonProvider());
            var failures = new List<string>();

            Console.WriteLine("level | orbs | par | minMoves | win | stars | state");
            Console.WriteLine("------|------|-----|----------|-----|-------|------");

            for (int n = 1; n <= repo.LevelCount; n++)
            {
                LevelData level = repo.GetMergeLevel(n);

                SolverResult solve = SolveMultiset(level);
                string stars = "n/a";
                string state = "SOLVABLE";

                if (solve.Solvable)
                {
                    stars = BoardController.CalculateStars(solve.MinMoves, level.ParMoves).ToString();

                    // Replay the BFS path on a real BoardController.
                    BoardController board = ReplaySolution(level, solve.Path, out string replayError);
                    if (board == null || !board.IsLevelComplete)
                    {
                        state = "REPLAY-FAILED";
                        failures.Add($"Level {n}: solution found ({solve.MinMoves} moves) but replay on BoardController did not complete: {replayError}");
                    }
                    else if (board.MoveCount != solve.MinMoves)
                    {
                        state = "MOVE-MISMATCH";
                        failures.Add($"Level {n}: BFS minimum {solve.MinMoves} != replay MoveCount {board.MoveCount}");
                    }
                }
                else
                {
                    state = "UNSOLVABLE";
                    failures.Add($"Level {n}: validator fixture is NOT solvable (BFS exhausted with no win state)");
                }

                Console.WriteLine($"{n,5} | {level.MergeOrbs?.Length ?? 0,4} | {level.ParMoves,3} | {solve.MinMoves,8} | {solve.Solvable,5} | {stars,5} | {state}");
            }

            Assert.IsEmpty(failures,
                "All validator fixtures must be solvable through BoardController.TryMergeAt:\n" +
                string.Join("\n", failures));
        }

        [Test]
        public void NegativeControl_Unsolvable_NeverCompletes()
        {
            // Mirror of tools/MergeLevelValidator/tests/fixtures/level_neg_unsolvable.json
            // (loaded from disk so the test tracks the real fixture).
            LevelData level = LoadNegativeControlFixture();

            SolverResult solve = SolveMultiset(level);
            Console.WriteLine($"neg  | {level.MergeOrbs?.Length ?? 0,4} | {level.ParMoves,3} | {solve.MinMoves,8} | {solve.Solvable,5} |  n/a | {(solve.Solvable ? "SOLVED(!)" : "UNSOLVABLE (correct)")}");

            Assert.IsFalse(solve.Solvable,
                "Negative control must be unsolvable: 3 Cyan T1 orbs can never produce a Cyan T3.");

            // Drive the real controller with a few greedy merges: moves must be
            // accepted (merges happen) but the win condition must never trigger.
            var board = new BoardController();
            board.Initialize(level);

            // All three orbs are Cyan T1; merge pairs until no legal move remains.
            int attempted = 0;
            bool merged = true;
            while (merged && !board.IsLevelComplete && attempted < 8)
            {
                merged = TryGreedyMerge(board);
                attempted++;
                Assert.IsFalse(board.IsLevelComplete,
                    "Negative control: IsLevelComplete must never become true.");
            }

            Console.WriteLine($"neg  greedy: {attempted} rounds, MoveCount={board.MoveCount}, IsLevelComplete={board.IsLevelComplete}");
            Assert.Greater(board.MoveCount, 0, "Greedy merges should have been accepted on the negative control.");
            Assert.IsFalse(board.IsLevelComplete, "Negative control must never complete.");
        }

        // ── Solver (multiset BFS, mirrors tools/MergeLevelValidator/Solver.cs) ──

        // Struct (not record): keeps the headless testrunner and Unity EditMode
        // tests compiling without System.Runtime.CompilerServices.IsExternalInit.
        private readonly struct OrbKey : IEquatable<OrbKey>
        {
            public OrbColor Color { get; }
            public OrbTier Tier { get; }

            public OrbKey(OrbColor color, OrbTier tier)
            {
                Color = color;
                Tier = tier;
            }

            public bool Equals(OrbKey other) => Color == other.Color && Tier == other.Tier;

            public override bool Equals(object? obj) => obj is OrbKey other && Equals(other);

            public override int GetHashCode() => ((int)Color * 397) ^ (int)Tier;
        }

        private sealed class SolverResult
        {
            public bool Solvable;
            public int MinMoves;
            public List<(OrbKey Src, OrbKey Dst)> Path = new();
        }

        private static SolverResult SolveMultiset(LevelData level)
        {
            var result = new SolverResult();

            // Initial multiset from placements (skip out-of-bounds, same as BoardController.Initialize).
            var start = new Dictionary<OrbKey, int>();
            if (level.MergeOrbs != null)
            {
                foreach (var o in level.MergeOrbs)
                {
                    if (o.X < 0 || o.X >= level.Width || o.Y < 0 || o.Y >= level.Height) continue;
                    var key = new OrbKey(o.Color, o.Tier);
                    start[key] = start.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            }

            // Win condition (position-agnostic, matches BoardController.CheckWin).
            bool IsWin(Dictionary<OrbKey, int> state)
            {
                if (level.RestorationTargets == null || level.RestorationTargets.Length == 0) return false;
                foreach (var t in level.RestorationTargets)
                {
                    var key = new OrbKey(t.Color, t.Tier);
                    if (!state.TryGetValue(key, out int c) || c < 1) return false;
                }
                return true;
            }

            if (IsWin(start))
            {
                result.Solvable = true;
                result.MinMoves = 0;
                return result;
            }

            var visited = new HashSet<string> { Canonical(start) };
            var queue = new Queue<(Dictionary<OrbKey, int> State, List<(OrbKey, OrbKey)> Path)>();
            queue.Enqueue((start, new List<(OrbKey, OrbKey)>()));

            int guard = 0;
            while (queue.Count > 0 && guard++ < 2_000_000)
            {
                var (state, path) = queue.Dequeue();

                // Enumerate all mergeable pairs in the multiset.
                var keys = state.Keys.ToArray();
                var moves = new List<(OrbKey A, OrbKey B)>();

                for (int i = 0; i < keys.Length; i++)
                {
                    var a = keys[i];
                    if (state[a] >= 2) moves.Add((a, a));
                    for (int j = i + 1; j < keys.Length; j++)
                    {
                        moves.Add((a, keys[j]));
                    }
                }

                foreach (var (a, b) in moves)
                {
                    var merge = MergeRules.TryMerge(
                        new OrbData(a.Color, a.Tier),
                        new OrbData(b.Color, b.Tier));
                    if (merge.Outcome == MergeOutcome.Invalid) continue;

                    // Apply to multiset.
                    var next = new Dictionary<OrbKey, int>(state);
                    Decrement(next, a);
                    Decrement(next, b);
                    if (merge.ResultOrb != null)
                    {
                        var rk = new OrbKey(merge.ResultOrb.Color, merge.ResultOrb.Tier);
                        next[rk] = next.TryGetValue(rk, out int c) ? c + 1 : 1;
                    }

                    var nextPath = new List<(OrbKey, OrbKey)>(path) { (a, b) };

                    if (IsWin(next))
                    {
                        result.Solvable = true;
                        result.MinMoves = nextPath.Count;
                        result.Path = nextPath;
                        return result;
                    }

                    string sig = Canonical(next);
                    if (visited.Add(sig))
                    {
                        queue.Enqueue((next, nextPath));
                    }
                }
            }

            return result; // unsolvable (or search cap hit — fixtures are small, cap never hit in practice)
        }

        private static void Decrement(Dictionary<OrbKey, int> state, OrbKey key)
        {
            if (state.TryGetValue(key, out int c))
            {
                if (c <= 1) state.Remove(key);
                else state[key] = c - 1;
            }
        }

        private static string Canonical(Dictionary<OrbKey, int> state)
        {
            var sb = new StringBuilder();
            foreach (var kv in state.OrderBy(kv => (int)kv.Key.Color).ThenBy(kv => (int)kv.Key.Tier))
            {
                sb.Append((int)kv.Key.Color).Append(':').Append((int)kv.Key.Tier).Append('x').Append(kv.Value).Append(';');
            }
            return sb.ToString();
        }

        // ── Replay on the real controller ────────────────────────────────

        private static BoardController ReplaySolution(LevelData level, List<(OrbKey Src, OrbKey Dst)> path, out string error)
        {
            error = string.Empty;
            var board = new BoardController();
            board.Initialize(level);

            foreach (var (srcKey, dstKey) in path)
            {
                GridPosition? src = FindCell(board, srcKey);
                GridPosition? dst = FindCell(board, dstKey, exclude: src);
                if (src == null || dst == null)
                {
                    error = $"could not find cells for merge ({srcKey.Color} T{(int)srcKey.Tier} + {dstKey.Color} T{(int)dstKey.Tier}) at move {board.MoveCount}";
                    return null!;
                }
                if (!board.TryMergeAt(src, dst))
                {
                    error = $"TryMergeAt({src.X},{src.Y} → {dst.X},{dst.Y}) rejected at move {board.MoveCount}";
                    return null!;
                }
            }

            return board;
        }

        private static GridPosition? FindCell(BoardController board, OrbKey key, GridPosition? exclude = null)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (exclude != null && exclude.X == x && exclude.Y == y) continue;
                    var orb = board.GetOrbAt(new GridPosition(x, y));
                    if (orb != null && orb.Color == key.Color && orb.Tier == key.Tier)
                    {
                        return new GridPosition(x, y);
                    }
                }
            }
            return null;
        }

        private static bool TryGreedyMerge(BoardController board)
        {
            for (int sy = 0; sy < board.Height; sy++)
            {
                for (int sx = 0; sx < board.Width; sx++)
                {
                    var srcOrb = board.GetOrbAt(new GridPosition(sx, sy));
                    if (srcOrb == null) continue;
                    for (int ty = 0; ty < board.Height; ty++)
                    {
                        for (int tx = 0; tx < board.Width; tx++)
                        {
                            if (sx == tx && sy == ty) continue;
                            var dstOrb = board.GetOrbAt(new GridPosition(tx, ty));
                            if (dstOrb == null) continue;
                            if (MergeRules.CanMerge(srcOrb, dstOrb))
                            {
                                return board.TryMergeAt(new GridPosition(sx, sy), new GridPosition(tx, ty));
                            }
                        }
                    }
                }
            }
            return false;
        }

        // ── Negative-control fixture loading ─────────────────────────────

        private static LevelData LoadNegativeControlFixture()
        {
            string fixturePath = LocateFixture("level_neg_unsolvable.json");
            string json = File.ReadAllText(fixturePath);

            // Minimal parse (same field mapping as MergeLevelRepository).
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var grid = root.GetProperty("grid");
            var orbs = new List<MergeOrbPlacement>();
            foreach (var o in root.GetProperty("orbs").EnumerateArray())
            {
                orbs.Add(new MergeOrbPlacement(
                    o.GetProperty("x").GetInt32(),
                    o.GetProperty("y").GetInt32(),
                    Enum.Parse<OrbColor>(o.GetProperty("color").GetString()!, ignoreCase: true),
                    (OrbTier)o.GetProperty("tier").GetInt32()));
            }
            var targets = new List<RestorationTarget>();
            foreach (var t in root.GetProperty("targets").EnumerateArray())
            {
                targets.Add(new RestorationTarget(
                    t.GetProperty("x").GetInt32(),
                    t.GetProperty("y").GetInt32(),
                    Enum.Parse<OrbColor>(t.GetProperty("color").GetString()!, ignoreCase: true),
                    (OrbTier)t.GetProperty("tier").GetInt32()));
            }

            return new LevelData
            {
                Width = grid.GetProperty("width").GetInt32(),
                Height = grid.GetProperty("height").GetInt32(),
                ParMoves = root.GetProperty("parMoves").GetInt32(),
                DisplayName = root.GetProperty("name").GetString() ?? "Negative Control",
                MergeOrbs = orbs.ToArray(),
                RestorationTargets = targets.ToArray(),
                Obstacles = Array.Empty<LevelObstacle>(),
                Sources = Array.Empty<LevelSource>(),
                Targets = Array.Empty<LevelTarget>(),
                SignalGates = Array.Empty<LevelSignalGate>(),
                GhostTraces = Array.Empty<GhostTrace>(),
                ImpedanceCells = Array.Empty<ImpedanceCell>(),
                Inventory = Array.Empty<TraceSegment>(),
            };
        }

        private static string LocateFixture(string fileName)
        {
            foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "tools", "MergeLevelValidator", "tests", "fixtures", fileName);
                    if (File.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new InvalidOperationException($"Fixture '{fileName}' not found under tools/MergeLevelValidator/tests/fixtures.");
        }
    }
}
