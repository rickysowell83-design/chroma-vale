// SPDX-License-Identifier: MIT
// Chroma Vale — MergeLevelRepository: loads validator-authored merge level JSON fixtures
// (tools/MergeLevelValidator/tests/fixtures/level_01..10.json, relative to the repo root)
// into LevelData objects with MergeOrbs, RestorationTargets, and ParMoves populated.
// Pure C# — no Unity dependencies (compiles headless via testrunner).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Loads merge-mode level definitions from the validator JSON fixtures and maps them
    /// into <see cref="LevelData"/> with MergeOrbs, RestorationTargets, and ParMoves populated.
    /// Legacy pipe-flow routing fields (Sources, Targets, SignalGates, ...) are left null/default —
    /// these are merge-only levels.
    /// </summary>
    public sealed class MergeLevelRepository : ILevelRepository
    {
        /// <summary>Fixture directory, relative to the repo root.</summary>
        public const string FixturesDirectory = "tools/MergeLevelValidator/tests/fixtures";

        /// <inheritdoc />
        public int LevelCount => 10;

        /// <inheritdoc />
        public LevelData GetLevel(int levelNumber) => GetMergeLevel(levelNumber);

        /// <summary>
        /// Loads merge level <paramref name="levelNumber"/> (1-based, 1..<see cref="LevelCount"/>)
        /// from its JSON fixture.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">levelNumber is outside 1..LevelCount.</exception>
        /// <exception cref="InvalidOperationException">the fixture is missing, unreadable, or malformed.</exception>
        public LevelData GetMergeLevel(int levelNumber)
        {
            if (levelNumber < 1 || levelNumber > LevelCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelNumber), levelNumber,
                    $"Merge level number must be between 1 and {LevelCount}.");
            }

            string fixturePath = LocateFixture(levelNumber);
            string json;
            try
            {
                json = File.ReadAllText(fixturePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    $"Failed to read merge level fixture '{fixturePath}'.", ex);
            }

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                return new LevelData
                {
                    Width = GetGridDimension(root, "width"),
                    Height = GetGridDimension(root, "height"),
                    ParMoves = GetInt32(root, "parMoves"),
                    DisplayName = GetString(root, "displayName") ?? $"Level {levelNumber}",
                    Obstacles = ParseObstacles(root),
                    MergeOrbs = ParseOrbs(root.GetProperty("orbs")),
                    RestorationTargets = ParseTargets(root.GetProperty("targets")),
                };
            }
        }

        private static string LocateFixture(int levelNumber)
        {
            string fileName = $"level_{levelNumber:00}.json";

            foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                DirectoryInfo dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, FixturesDirectory, fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                    dir = dir.Parent;
                }
            }

            throw new InvalidOperationException(
                $"Merge level fixture '{fileName}' not found under '{FixturesDirectory}' relative to the repo root " +
                $"(searched from '{Environment.CurrentDirectory}' and '{AppContext.BaseDirectory}').");
        }

        private static int GetGridDimension(JsonElement root, string dimension)
        {
            if (root.TryGetProperty("grid", out JsonElement grid) &&
                grid.TryGetProperty(dimension, out JsonElement value))
            {
                return value.GetInt32();
            }
            throw new JsonException($"Merge fixture is missing required field 'grid.{dimension}'.");
        }

        private static int GetInt32(JsonElement element, string property)
        {
            return element.GetProperty(property).GetInt32();
        }

        private static string GetString(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
            return null;
        }

        private static MergeOrbPlacement[] ParseOrbs(JsonElement array)
        {
            List<MergeOrbPlacement> orbs = new List<MergeOrbPlacement>();
            foreach (JsonElement orb in array.EnumerateArray())
            {
                orbs.Add(new MergeOrbPlacement(
                    orb.GetProperty("x").GetInt32(),
                    orb.GetProperty("y").GetInt32(),
                    ParseColor(orb.GetProperty("color")),
                    ParseTier(orb.GetProperty("tier"))));
            }
            return orbs.ToArray();
        }

        private static RestorationTarget[] ParseTargets(JsonElement array)
        {
            List<RestorationTarget> targets = new List<RestorationTarget>();
            foreach (JsonElement target in array.EnumerateArray())
            {
                targets.Add(new RestorationTarget(
                    target.GetProperty("x").GetInt32(),
                    target.GetProperty("y").GetInt32(),
                    ParseColor(target.GetProperty("color")),
                    ParseTier(target.GetProperty("tier"))));
            }
            return targets.ToArray();
        }

        private static LevelObstacle[] ParseObstacles(JsonElement root)
        {
            if (!root.TryGetProperty("obstacles", out JsonElement array) ||
                array.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<LevelObstacle>();
            }

            List<LevelObstacle> obstacles = new List<LevelObstacle>();
            foreach (JsonElement obstacle in array.EnumerateArray())
            {
                obstacles.Add(new LevelObstacle
                {
                    X = obstacle.GetProperty("x").GetInt32(),
                    Y = obstacle.GetProperty("y").GetInt32(),
                });
            }
            return obstacles.ToArray();
        }

        private static OrbColor ParseColor(JsonElement element)
        {
            string raw = element.GetString() ?? string.Empty;
            if (Enum.TryParse(raw, ignoreCase: true, out OrbColor color))
            {
                return color;
            }
            throw new JsonException($"Unknown orb color '{raw}' in merge fixture.");
        }

        private static OrbTier ParseTier(JsonElement element)
        {
            int tier = element.GetInt32();
            if (tier < 1 || tier > 5)
            {
                throw new JsonException($"Invalid orb tier '{tier}' in merge fixture.");
            }
            return (OrbTier)tier;
        }
    }
}
