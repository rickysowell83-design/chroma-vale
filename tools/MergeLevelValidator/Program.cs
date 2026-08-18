// Program.cs — MergeLevelValidator CLI.
// Usage: MergeLevelValidator <level.json> [<level2.json> ...] [--adjacency]
//   or    MergeLevelValidator --dir <folder>
// Exits 0 if all levels PASS, 1 if any FAIL or a file is unparseable.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChromaMerge.Validator
{
    public static class Program
    {
        private static readonly Dictionary<string, OrbColor> ColorNames = new Dictionary<string, OrbColor>
        {
            { "cyan", OrbColor.Cyan }, { "magenta", OrbColor.Magenta }, { "yellow", OrbColor.Yellow },
            { "purple", OrbColor.Purple }, { "green", OrbColor.Green }, { "orange", OrbColor.Orange },
            { "brown", OrbColor.Brown },
        };

        public static int Main(string[] args)
        {
            var files = new List<string>();
            bool adjacency = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--dir" && i + 1 < args.Length)
                    files.AddRange(Directory.GetFiles(args[++i], "*.json", SearchOption.TopDirectoryOnly).OrderBy(f => f));
                else if (args[i] == "--adjacency")
                    adjacency = true;
                else
                    files.Add(args[i]);
            }
            if (files.Count == 0)
            {
                Console.Error.WriteLine("Usage: MergeLevelValidator <level.json> ... [--dir <folder>] [--adjacency]");
                return 2;
            }

            int failures = 0;
            foreach (var file in files)
            {
                bool pass = ValidateFile(file, adjacency);
                if (!pass) failures++;
            }
            Console.WriteLine();
            Console.WriteLine(failures == 0
                ? "RESULT: ALL LEVELS PASS"
                : "RESULT: " + failures + " LEVEL(S) FAILED");
            return failures == 0 ? 0 : 1;
        }

        private static bool ValidateFile(string path, bool adjacency)
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("FILE: " + path);
            try
            {
                var level = LoadLevel(path);
                var issues = new List<string>();
                if (!level.Validate(out issues))
                {
                    Console.WriteLine("  [SCHEMA FAIL]");
                    foreach (var e in issues) Console.WriteLine("    - " + e);
                    return false;
                }
                Console.WriteLine("  schema: OK  (" + level.Orbs.Count + " orbs, " + level.Targets.Count
                    + " targets, par " + level.ParMoves + ", " + level.Difficulty + ")");
                var solve = Solver.Solve(level, adjacency);
                if (!solve.Solved)
                {
                    Console.WriteLine("  [SOLVE FAIL] " + solve.Error);
                    return false;
                }
                Console.WriteLine("  [SOLVE PASS] min moves " + solve.MinMoves
                    + (solve.ThreeStarPossible ? " (<= par " + level.ParMoves + ", 3-star OK)" : " (> par " + level.ParMoves + ", 3-star NOT possible)")
                    + " | states " + solve.StatesExplored + " | avg branching " + solve.AvgBranching.ToString("0.00"));
                Console.WriteLine("  solution: " + solve.SolutionPath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [ERROR] " + ex.Message);
                return false;
            }
        }

        public static Level LoadLevel(string path)
        {
            var json = MiniJson.Parse(File.ReadAllText(path));
            var lvl = new Level
            {
                Id = json["id"].AsString,
                Name = json["name"].AsString,
                Width = json["grid"]["width"].AsInt,
                Height = json["grid"]["height"].AsInt,
                ParMoves = json.HasKey("parMoves") ? json["parMoves"].AsInt : int.MaxValue,
                Difficulty = json["difficulty"].AsString,
                Teaches = json["teaches"].AsString,
            };
            if (json.HasKey("orbs"))
                foreach (var o in json["orbs"].Array)
                    lvl.Orbs.Add(new Orb
                    {
                        Color = ParseColor(o["color"].AsString),
                        Tier = o["tier"].AsInt,
                        X = o["x"].AsInt,
                        Y = o["y"].AsInt,
                    });
            if (json.HasKey("targets"))
                foreach (var t in json["targets"].Array)
                    lvl.Targets.Add(new Target
                    {
                        Color = ParseColor(t["color"].AsString),
                        Tier = t["tier"].AsInt,
                        X = t["x"].AsInt,
                        Y = t["y"].AsInt,
                    });
            if (json.HasKey("obstacles"))
                foreach (var ob in json["obstacles"].Array)
                    lvl.Obstacles.Add((ob["x"].AsInt, ob["y"].AsInt));
            return lvl;
        }

        private static OrbColor ParseColor(string s)
        {
            if (ColorNames.TryGetValue(s.Trim().ToLowerInvariant(), out var c)) return c;
            throw new FormatException("unknown color: '" + s + "'");
        }
    }
}
