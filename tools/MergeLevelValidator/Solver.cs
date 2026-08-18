// Solver.cs — BFS over the merge-state tree.
// State = multiset of orbs + remaining targets. A move is either:
//   1. Merge two legal orbs (produces 0/1 result orb, consumes the pair)
//   2. Fill a target with a matching orb (consumes orb + target)
// Win = all targets filled. BFS guarantees the minimum move count.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChromaMerge.Validator
{
    public sealed class SolveResult
    {
        public bool Solved;
        public int MinMoves;
        public bool ThreeStarPossible;
        public int StatesExplored;
        public int MaxQueueDepth;
        public double AvgBranching;
        public string SolutionPath;      // human-readable move list
        public string Error;
    }

    public static class Solver
    {
        private const int MaxStates = 5_000_000;

        public static SolveResult Solve(Level level, bool adjacencyOnly = false)
        {
            var start = new GameState(level);
            var result = new SolveResult();
            if (start.Targets.Count == 0)
            {
                result.Error = "level has no targets";
                return result;
            }

            var visited = new HashSet<string>();
            var queue = new Queue<GameState>();
            var parent = new Dictionary<string, (string prev, string desc)>();
            queue.Enqueue(start);
            visited.Add(start.Key);
            result.StatesExplored = 1;
            int branchSum = 0;
            int branchCount = 0;

            while (queue.Count > 0)
            {
                result.MaxQueueDepth = Math.Max(result.MaxQueueDepth, queue.Count);
                var s = queue.Dequeue();
                result.StatesExplored++;

                if (s.Targets.Count == 0)
                {
                    result.Solved = true;
                    result.MinMoves = s.Moves;
                    result.ThreeStarPossible = s.Moves <= level.ParMoves;
                    result.SolutionPath = Reconstruct(parent, s.Key);
                    result.AvgBranching = branchCount > 0 ? (double)branchSum / branchCount : 0;
                    return result;
                }

                if (visited.Count >= MaxStates)
                {
                    result.Error = "state limit reached (" + MaxStates + ")";
                    return result;
                }

                var moves = EnumerateMoves(s, adjacencyOnly);
                branchSum += moves.Count;
                branchCount++;
                foreach (var m in moves)
                {
                    var ns = m.Apply(s);
                    if (ns == null || !visited.Add(ns.Key)) continue;
                    parent[ns.Key] = (s.Key, m.Description);
                    queue.Enqueue(ns);
                }
            }

            result.Error = "exhausted all states — unsolvable";
            result.AvgBranching = branchCount > 0 ? (double)branchSum / branchCount : 0;
            return result;
        }

        private static List<Move> EnumerateMoves(GameState s, bool adjacencyOnly)
        {
            var moves = new List<Move>();
            int n = s.Orbs.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var a = s.Orbs[i];
                    var b = s.Orbs[j];
                    if (adjacencyOnly && !AreAdjacent(a, b)) continue;
                    var res = MergeRules.Merge(a, b);
                    if (res == null) continue;
                    moves.Add(new Move
                    {
                        ConsumeA = a, ConsumeB = b, Produce = res.Produced, Kind = res.Kind,
                        Description = Describe(a, b, res)
                    });
                }
            }
            // Fill moves: any orb matching a remaining target can fill it.
            for (int i = 0; i < n; i++)
            {
                var orb = s.Orbs[i];
                foreach (var t in s.Targets)
                {
                    if (orb.Color == t.Color && orb.Tier == t.Tier)
                    {
                        moves.Add(new Move
                        {
                            ConsumeA = orb, ConsumeB = null, Produce = new List<Orb>(),
                            FillTarget = t, Kind = MergeKind.BrownClear /* reuse enum: fill */,
                            Description = "Fill " + t.ToString() + " with " + orb.ToString()
                        });
                        break;
                    }
                }
            }
            return moves;
        }

        private static bool AreAdjacent(Orb a, Orb b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
        }

        private static string Describe(Orb a, Orb b, MergeResult res)
        {
            switch (res.Kind)
            {
                case MergeKind.TierMerge:
                    return a + " + " + b + " -> " + (res.Produced.Count > 0 ? res.Produced[0].ToString() : "?");
                case MergeKind.ColorMix:
                    return a + " + " + b + " mix -> " + (res.Produced.Count > 0 ? res.Produced[0].ToString() : "?");
                case MergeKind.BrownClear:
                    return a + " + " + b + " clear";
                default:
                    return a + " + " + b;
            }
        }

        private static string Reconstruct(Dictionary<string, (string prev, string desc)> parent, string key)
        {
            var steps = new List<string>();
            while (parent.TryGetValue(key, out var p))
            {
                steps.Add(p.desc);
                key = p.prev;
            }
            steps.Reverse();
            return string.Join(" | ", steps);
        }
    }

    public sealed class Move
    {
        public Orb ConsumeA;
        public Orb ConsumeB;           // null for fill moves
        public List<Orb> Produce = new List<Orb>();
        public Target FillTarget;      // non-null for fill moves
        public MergeKind Kind;
        public string Description;

        public GameState Apply(GameState s)
        {
            return s.CloneWith(this);
        }
    }

    public sealed class GameState
    {
        public List<Orb> Orbs;
        public List<Target> Targets;
        public int Moves;
        private readonly string _key;

        public GameState(Level level)
        {
            Orbs = level.Orbs.Select(o => o.Clone()).ToList();
            Targets = level.Targets.Select(t => new Target { Color = t.Color, Tier = t.Tier, X = t.X, Y = t.Y }).ToList();
            Moves = 0;
            _key = ComputeKey();
        }

        private GameState(List<Orb> orbs, List<Target> targets, int moves)
        {
            Orbs = orbs;
            Targets = targets;
            Moves = moves;
            _key = ComputeKey();
        }

        public string Key => _key;

        public GameState CloneWith(Move m)
        {
            var orbs = Orbs.ToList();
            var targets = Targets.ToList();
            // Remove consumed orbs (match by reference identity).
            orbs.Remove(m.ConsumeA);
            if (m.ConsumeB != null) orbs.Remove(m.ConsumeB);
            // Add produced.
            orbs.AddRange(m.Produce.Select(o => o.Clone()));
            // Fill target if this is a fill move.
            if (m.FillTarget != null)
            {
                var t = targets.FirstOrDefault(x => x.X == m.FillTarget.X && x.Y == m.FillTarget.Y);
                if (t == null) return null;
                targets.Remove(t);
            }
            // Auto-fill: any produced orb matching a remaining target may fill immediately
            // (the player would do so; keep the simple model — the fill move already covers choice).
            return new GameState(orbs, targets, Moves + 1);
        }

        private string ComputeKey()
        {
            var orbKeys = Orbs.Select(o => o.Key).OrderBy(x => x);
            var targetKeys = Targets.Select(t => t.Key).OrderBy(x => x);
            return string.Join(",", orbKeys) + "|" + string.Join(",", targetKeys);
        }
    }
}
