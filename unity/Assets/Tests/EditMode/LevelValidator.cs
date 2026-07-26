using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;

namespace ChromaVale.Tests
{
    public class ValidationResult
    {
        public bool Passed;
        public string Error;
        public int TicksToSolve;
        public int PiecesUsed;
        public bool ThreeStarPossible;
        public string SolutionPath;
    }

    public static class LevelValidator
    {
        private static readonly Dictionary<string, int> ColorMap = new()
        {
            { "cyan", 0 }, { "magenta", 1 }, { "yellow", 2 }
        };

        private static readonly Dictionary<string, (PieceShape shape, int capacity)> InventoryMap = new()
        {
            { "straight_1",   (PieceShape.Straight, 1) },
            { "straight_2",   (PieceShape.Straight, 2) },
            { "straight_3",   (PieceShape.Straight, 3) },
            { "elbow_1",      (PieceShape.Elbow, 1) },
            { "elbow_2",      (PieceShape.Elbow, 2) },
            { "tjunction_2",  (PieceShape.TJunction, 2) },
            { "cross_2",      (PieceShape.Cross, 2) },
            { "valve_2",      (PieceShape.Valve, 2) },
            { "amplifier",    (PieceShape.Amplifier, 0) },
            { "mixer",        (PieceShape.Mixer, 0) },
            { "blocker",      (PieceShape.Blocker, 0) },
        };

        public static ValidationResult ValidateLevel(string jsonLevel)
        {
            var result = new ValidationResult();

            // Step 1: Parse JSON
            JsonDocument doc;
            try { doc = JsonDocument.Parse(jsonLevel); }
            catch (Exception ex)
            {
                result.Error = $"JSON parse error: {ex.Message}";
                return result;
            }
            var root = doc.RootElement;

            if (!root.TryGetProperty("grid", out var grid))
            { result.Error = "Missing required field: grid"; return result; }
            if (!grid.TryGetProperty("width", out var jw) || !grid.TryGetProperty("height", out var jh))
            { result.Error = "Missing required field: grid.width or grid.height"; return result; }
            int width = jw.GetInt32();
            int height = jh.GetInt32();

            if (!root.TryGetProperty("sources", out var sourcesJson))
            { result.Error = "Missing required field: sources"; return result; }
            if (!root.TryGetProperty("targets", out var targetsJson))
            { result.Error = "Missing required field: targets"; return result; }
            if (!root.TryGetProperty("inventory", out var inventoryJson))
            { result.Error = "Missing required field: inventory"; return result; }
            if (!root.TryGetProperty("parTicks", out var parTicksJson))
            { result.Error = "Missing required field: parTicks"; return result; }
            int parTicks = parTicksJson.GetInt32();

            var sources = ParseSources(sourcesJson);
            if (sources == null) { result.Error = "Invalid sources array"; return result; }
            var targets = ParseTargets(targetsJson);
            if (targets == null) { result.Error = "Invalid targets array"; return result; }
            var obstacles = ParseObstacles(root);
            var flowGates = ParseFlowGates(root);
            var (inventoryPieces, invError) = ParseInventory(inventoryJson);
            if (invError != null) { result.Error = invError; return result; }
            int totalInventory = inventoryPieces.Count;

            // Step 2: Bounds check
            var boundsError = CheckBounds(width, height, sources, targets, obstacles, flowGates);
            if (boundsError != null) { result.Error = boundsError; return result; }

            // Step 3: Color match
            var colorError = CheckColorMatch(sources, targets);
            if (colorError != null) { result.Error = colorError; return result; }

            // Step 4: No overlaps
            var overlapError = CheckOverlaps(sources, targets, obstacles, flowGates);
            if (overlapError != null) { result.Error = overlapError; return result; }

            // Step 5: BFS reachability
            var reachError = CheckBfsReachability(width, height, sources, targets, obstacles);
            if (reachError != null) { result.Error = reachError; return result; }

            // Step 6 + 7: Solvability + 3-star
            var solveResult = TrySolve(width, height, sources, targets, obstacles, flowGates,
                inventoryPieces, parTicks);

            if (!solveResult.solved)
            {
                result.Error = solveResult.error ?? "Level is unsolvable: no placement sequence reaches all targets";
                return result;
            }

            result.Passed = true;
            result.TicksToSolve = solveResult.ticks;
            result.PiecesUsed = solveResult.piecesUsed;
            result.ThreeStarPossible = solveResult.ticks <= parTicks
                && solveResult.piecesUsed <= (int)Math.Ceiling(totalInventory * 0.6);
            result.SolutionPath = solveResult.solutionPath;
            return result;
        }

        // ──────────────── Parsers ────────────────

        private static List<(int x, int y, int color, int pressure)> ParseSources(JsonElement arr)
        {
            var result = new List<(int, int, int, int)>();
            foreach (var s in arr.EnumerateArray())
            {
                if (!s.TryGetProperty("x", out var sx) || !s.TryGetProperty("y", out var sy)
                    || !s.TryGetProperty("color", out var sc))
                    return null;
                string colorName = sc.GetString().ToLowerInvariant();
                if (!ColorMap.TryGetValue(colorName, out int colorIdx))
                    return null;
                int pressure = 1;
                if (s.TryGetProperty("pressure", out var sp))
                    pressure = sp.GetInt32();
                result.Add((sx.GetInt32(), sy.GetInt32(), colorIdx, pressure));
            }
            return result;
        }

        private static List<(int x, int y, int color)> ParseTargets(JsonElement arr)
        {
            var result = new List<(int, int, int)>();
            foreach (var t in arr.EnumerateArray())
            {
                if (!t.TryGetProperty("x", out var tx) || !t.TryGetProperty("y", out var ty)
                    || !t.TryGetProperty("color", out var tc))
                    return null;
                string colorName = tc.GetString().ToLowerInvariant();
                if (!ColorMap.TryGetValue(colorName, out int colorIdx))
                    return null;
                result.Add((tx.GetInt32(), ty.GetInt32(), colorIdx));
            }
            return result;
        }

        private static HashSet<(int, int)> ParseObstacles(JsonElement root)
        {
            var result = new HashSet<(int, int)>();
            if (root.TryGetProperty("obstacles", out var obsJson))
                foreach (var o in obsJson.EnumerateArray())
                    result.Add((o.GetProperty("x").GetInt32(), o.GetProperty("y").GetInt32()));
            return result;
        }

        private static List<(int x, int y, PipeDirection dir)> ParseFlowGates(JsonElement root)
        {
            var result = new List<(int, int, PipeDirection)>();
            if (root.TryGetProperty("flowGates", out var fgJson))
            {
                foreach (var fg in fgJson.EnumerateArray())
                {
                    string ds = fg.GetProperty("direction").GetString().ToLowerInvariant();
                    PipeDirection dir = ds switch
                    {
                        "up" => PipeDirection.Up, "down" => PipeDirection.Down,
                        "left" => PipeDirection.Left, "right" => PipeDirection.Right,
                        _ => PipeDirection.None,
                    };
                    result.Add((fg.GetProperty("x").GetInt32(), fg.GetProperty("y").GetInt32(), dir));
                }
            }
            return result;
        }

        private static (List<PipePiece> pieces, string error) ParseInventory(JsonElement inv)
        {
            var pieces = new List<PipePiece>();
            foreach (var p in inv.EnumerateObject())
            {
                if (!InventoryMap.TryGetValue(p.Name, out var m))
                    return (null, $"Unknown inventory key: '{p.Name}'");
                int count = p.Value.GetInt32();
                for (int i = 0; i < count; i++)
                    pieces.Add(m.shape switch
                    {
                        PieceShape.Straight => PipePiece.Straight(m.capacity),
                        PieceShape.Elbow => PipePiece.Elbow(m.capacity),
                        PieceShape.TJunction => PipePiece.TJunction(m.capacity),
                        PieceShape.Cross => PipePiece.Cross(m.capacity),
                        PieceShape.Valve => PipePiece.Valve(m.capacity),
                        PieceShape.Amplifier => PipePiece.Amplifier(),
                        PieceShape.Mixer => PipePiece.Mixer(),
                        PieceShape.Blocker => PipePiece.Blocker(),
                        _ => PipePiece.Straight(m.capacity),
                    });
            }
            return (pieces, null);
        }

        // ──────────────── Validation checks ────────────────

        private static string CheckBounds(int w, int h,
            List<(int x, int y, int color, int pressure)> sources,
            List<(int x, int y, int color)> targets,
            HashSet<(int, int)> obstacles,
            List<(int x, int y, PipeDirection dir)> flowGates)
        {
            foreach (var (x, y, _, _) in sources)
                if (x < 0 || x >= w || y < 0 || y >= h)
                    return $"Source at ({x},{y}) is outside grid bounds ({w}x{h})";
            foreach (var (x, y, _) in targets)
                if (x < 0 || x >= w || y < 0 || y >= h)
                    return $"Target at ({x},{y}) is outside grid bounds ({w}x{h})";
            foreach (var (x, y) in obstacles)
                if (x < 0 || x >= w || y < 0 || y >= h)
                    return $"Obstacle at ({x},{y}) is outside grid bounds ({w}x{h})";
            foreach (var (x, y, _) in flowGates)
                if (x < 0 || x >= w || y < 0 || y >= h)
                    return $"FlowGate at ({x},{y}) is outside grid bounds ({w}x{h})";
            return null;
        }

        private static string CheckColorMatch(
            List<(int x, int y, int color, int pressure)> sources,
            List<(int x, int y, int color)> targets)
        {
            foreach (var (sx, sy, sc, _) in sources)
            {
                if (!targets.Any(t => t.color == sc))
                    return $"Source {ColorMixer.GetColorName(sc)} at ({sx},{sy}) has no matching-color target";
            }
            return null;
        }

        private static string CheckOverlaps(
            List<(int x, int y, int color, int pressure)> sources,
            List<(int x, int y, int color)> targets,
            HashSet<(int, int)> obstacles,
            List<(int x, int y, PipeDirection dir)> flowGates)
        {
            var occ = new HashSet<(int, int)>();
            foreach (var (x, y, _, _) in sources)
                if (!occ.Add((x, y))) return $"Overlap: multiple sources at ({x},{y})";
            foreach (var (x, y, _) in targets)
                if (!occ.Add((x, y))) return $"Overlap at ({x},{y}): target shares cell with another element";
            foreach (var (x, y) in obstacles)
                if (!occ.Add((x, y))) return $"Overlap at ({x},{y}): obstacle shares cell with another element";
            foreach (var (x, y, _) in flowGates)
                if (!occ.Add((x, y))) return $"Overlap at ({x},{y}): flowGate shares cell with another element";
            return null;
        }

        private static string CheckBfsReachability(int w, int h,
            List<(int x, int y, int color, int pressure)> sources,
            List<(int x, int y, int color)> targets,
            HashSet<(int, int)> obstacles)
        {
            foreach (var (sx, sy, sc, _) in sources)
            {
                var tset = new HashSet<(int, int)>();
                foreach (var (tx, ty, tc) in targets)
                    if (tc == sc) tset.Add((tx, ty));
                if (tset.Count == 0) continue;
                if (!BfsReachable(sx, sy, w, h, obstacles, tset))
                    return $"Source {ColorMixer.GetColorName(sc)} at ({sx},{sy}) cannot reach any {ColorMixer.GetColorName(sc)} target (path blocked by obstacles)";
            }
            return null;
        }

        private static bool BfsReachable(int sx, int sy, int w, int h,
            HashSet<(int, int)> obstacles, HashSet<(int, int)> targetSet)
        {
            var visited = new HashSet<(int, int)>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue((sx, sy));
            visited.Add((sx, sy));
            (int dx, int dy)[] dirs = { (0, 1), (0, -1), (1, 0), (-1, 0) };
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                if (targetSet.Contains((cx, cy))) return true;
                foreach (var (dx, dy) in dirs)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (obstacles.Contains((nx, ny))) continue;
                    if (!visited.Add((nx, ny))) continue;
                    queue.Enqueue((nx, ny));
                }
            }
            return false;
        }

        // ──────────────── Solver ────────────────

        private static (bool solved, string error, int ticks, int piecesUsed, string solutionPath) TrySolve(
            int width, int height,
            List<(int x, int y, int color, int pressure)> sources,
            List<(int x, int y, int color)> targets,
            HashSet<(int, int)> obstacles,
            List<(int x, int y, PipeDirection dir)> flowGates,
            List<PipePiece> inventory,
            int parTicks)
        {
            var level = BuildLevelData(width, height, sources, targets, obstacles, flowGates, inventory, parTicks);
            var occupied = AllOccupied(sources, targets, obstacles, flowGates);

            // Path zone: cells within BFS corridor between sources and matching targets
            var pathZone = ComputePathZone(sources, targets, width, height, obstacles);

            // Only try cells in the path corridor
            var candidates = new List<(int, int)>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (!occupied.Contains((x, y)) && pathZone.Contains((x, y)))
                        candidates.Add((x, y));

            var placements = new List<(int, int, int, int)>();
            var used = new HashSet<int>();
            return DfsSolve(level, candidates, 0, placements, used);
        }

        private static HashSet<(int, int)> ComputePathZone(
            List<(int x, int y, int color, int pressure)> sources,
            List<(int x, int y, int color)> targets,
            int w, int h, HashSet<(int, int)> obstacles)
        {
            var zone = new HashSet<(int, int)>();
            foreach (var (sx, sy, sc, _) in sources)
            {
                var matching = targets.Where(t => t.color == sc).ToList();
                if (matching.Count == 0) continue;
                var tset = new HashSet<(int, int)>(matching.Select(t => (t.x, t.y)));

                // BFS from source to get distances
                var distFromSrc = BfsDistances(sx, sy, w, h, obstacles);
                // BFS from any target to get distances (reverse)
                var distFromTgt = BfsDistancesMulti(tset, w, h, obstacles);

                // A cell is on-path if distFromSrc + distFromTgt <= shortest path
                int shortest = int.MaxValue;
                foreach (var t in matching)
                {
                    if (distFromSrc.TryGetValue((t.x, t.y), out int d))
                        shortest = Math.Min(shortest, d);
                }

                foreach (var kv in distFromSrc)
                {
                    if (distFromTgt.TryGetValue(kv.Key, out int dt))
                        if (kv.Value + dt <= shortest + 1) // allow slight variation
                            zone.Add(kv.Key);
                }
            }
            return zone;
        }

        private static Dictionary<(int, int), int> BfsDistances(int sx, int sy, int w, int h,
            HashSet<(int, int)> obstacles)
        {
            var dist = new Dictionary<(int, int), int>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue((sx, sy));
            dist[(sx, sy)] = 0;
            (int dx, int dy)[] dirs = { (0, 1), (0, -1), (1, 0), (-1, 0) };
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                int nd = dist[(cx, cy)] + 1;
                foreach (var (dx, dy) in dirs)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (obstacles.Contains((nx, ny))) continue;
                    if (dist.ContainsKey((nx, ny))) continue;
                    dist[(nx, ny)] = nd;
                    queue.Enqueue((nx, ny));
                }
            }
            return dist;
        }

        private static Dictionary<(int, int), int> BfsDistancesMulti(HashSet<(int, int)> starts,
            int w, int h, HashSet<(int, int)> obstacles)
        {
            var dist = new Dictionary<(int, int), int>();
            var queue = new Queue<(int, int)>();
            foreach (var s in starts)
            {
                queue.Enqueue(s);
                dist[s] = 0;
            }
            (int dx, int dy)[] dirs = { (0, 1), (0, -1), (1, 0), (-1, 0) };
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                int nd = dist[(cx, cy)] + 1;
                foreach (var (dx, dy) in dirs)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (obstacles.Contains((nx, ny))) continue;
                    if (dist.ContainsKey((nx, ny))) continue;
                    dist[(nx, ny)] = nd;
                    queue.Enqueue((nx, ny));
                }
            }
            return dist;
        }

        private static HashSet<(int, int)> AllOccupied(
            List<(int x, int y, int color, int pressure)> sources,
            List<(int x, int y, int color)> targets,
            HashSet<(int, int)> obstacles,
            List<(int x, int y, PipeDirection dir)> flowGates)
        {
            var set = new HashSet<(int, int)>();
            foreach (var s in sources) set.Add((s.x, s.y));
            foreach (var t in targets) set.Add((t.x, t.y));
            foreach (var o in obstacles) set.Add(o);
            foreach (var fg in flowGates) set.Add((fg.x, fg.y));
            return set;
        }

        private static (bool solved, string error, int ticks, int piecesUsed, string solutionPath) DfsSolve(
            LevelData level, List<(int, int)> cells, int idx,
            List<(int, int, int, int)> placements, HashSet<int> used)
        {
            var (stateSolved, stateTicks) = SimulatePlacements(level, placements);
            if (stateSolved)
                return (true, null, stateTicks, placements.Count, FormatSolution(placements));
            if (idx >= cells.Count) return default;
            if (used.Count >= level.Inventory.Length) return default;

            var (cx, cy) = cells[idx];

            // Skip
            { var r = DfsSolve(level, cells, idx + 1, placements, used); if (r.solved) return r; }

            // Try each piece
            for (int pi = 0; pi < level.Inventory.Length; pi++)
            {
                if (used.Contains(pi)) continue;
                var piece = level.Inventory[pi];
                foreach (int rot in GetRotations(piece.Shape))
                {
                    placements.Add((cx, cy, pi, rot));
                    used.Add(pi);
                    var r = DfsSolve(level, cells, idx + 1, placements, used);
                    if (r.solved) return r;
                    placements.RemoveAt(placements.Count - 1);
                    used.Remove(pi);
                }
            }
            return default;
        }

        private static (bool solved, int ticks) SimulatePlacements(LevelData level,
            List<(int, int, int, int)> placements)
        {
            if (placements.Count == 0) return (false, 0);
            var board = new GridBoard(level);
            var pieces = level.Inventory.Select(ClonePiece).ToArray();
            var inventory = new PipeInventory(pieces);
            var sim = new FlowSimulator();

            foreach (var (x, y, pieceIdx, rot) in placements)
                if (!inventory.TryPlace(pieceIdx, board, x, y, sim, rot))
                    return (false, 0);

            sim.StartSimulation(board, level, inventory);
            int maxTicks = Math.Max(level.ParTicks * 4, 100);
            while (sim.GetResult() == SimulationResult.InProgress && sim.CurrentTick < maxTicks)
                sim.Tick();

            return sim.GetResult() == SimulationResult.AllTargetsReached
                ? (true, sim.CurrentTick) : (false, sim.CurrentTick);
        }

        private static int[] GetRotations(PieceShape shape) => shape switch
        {
            PieceShape.Straight => new[] { 0, 90 },
            PieceShape.Elbow => new[] { 0, 90, 180, 270 },
            PieceShape.TJunction => new[] { 0, 90, 180, 270 },
            PieceShape.Cross => new[] { 0 },
            PieceShape.Valve => new[] { 0, 90, 180, 270 },
            _ => new[] { 0 },
        };

        // ──────────────── Helpers ────────────────

        private static LevelData BuildLevelData(int w, int h,
            List<(int x, int y, int color, int pressure)> sources,
            List<(int x, int y, int color)> targets,
            HashSet<(int, int)> obstacles,
            List<(int x, int y, PipeDirection dir)> flowGates,
            List<PipePiece> inventory, int parTicks) => new()
        {
            Width = w, Height = h,
            Sources = sources.Select(s => new LevelSource
            { X = s.x, Y = s.y, ColorIndex = s.color, FlowPressure = s.pressure }).ToArray(),
            Targets = targets.Select(t => new LevelTarget
            { X = t.x, Y = t.y, ColorIndex = t.color }).ToArray(),
            Obstacles = obstacles.Select(o => new LevelObstacle { X = o.Item1, Y = o.Item2 }).ToArray(),
            FlowGates = flowGates.Select(fg => new LevelFlowGate
            { X = fg.x, Y = fg.y, Direction = fg.dir }).ToArray(),
            Inventory = inventory.ToArray(),
            ParTicks = parTicks,
        };

        private static PipePiece ClonePiece(PipePiece p) => new()
        {
            Shape = p.Shape, Capacity = p.Capacity,
            ColorAffinity = p.ColorAffinity, State = PieceState.InHand,
            Rotation = 0, Direction = p.Direction,
        };

        private static string FormatSolution(List<(int, int, int, int)> placements)
        {
            var sb = new StringBuilder();
            sb.Append(placements.Count).Append(" placements:");
            foreach (var (x, y, pi, rot) in placements)
                sb.Append(' ').Append('(').Append(x).Append(',').Append(y)
                  .Append(')').Append("p[").Append(pi).Append("]r").Append(rot);
            return sb.ToString();
        }
    }
}