// SPDX-License-Identifier: MIT
// Chroma Vale — MergeLevelRepository: loads merge level definitions from raw
// JSON supplied through an injected ILevelJsonProvider (game: TextAssets in
// Resources/Levels; tests: validator fixture files on disk) into LevelData
// objects with MergeOrbs, RestorationTargets, and ParMoves populated.
// Pure C# — no Unity dependencies (compiles headless via testrunner).

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Loads merge-mode level definitions from level fixture JSON and maps them
    /// into <see cref="LevelData"/> with MergeOrbs, RestorationTargets, and ParMoves populated.
    /// Legacy pipe-flow routing fields (Sources, Targets, SignalGates, ...) are left null/default —
    /// these are merge-only levels.
    /// </summary>
    public sealed class MergeLevelRepository : ILevelRepository
    {
        private readonly ILevelJsonProvider _jsonProvider;

        /// <inheritdoc />
        public int LevelCount => 10;

        /// <summary>
        /// Creates a repository that loads level JSON through
        /// <paramref name="jsonProvider"/> (no filesystem access of its own).
        /// </summary>
        public MergeLevelRepository(ILevelJsonProvider jsonProvider)
        {
            _jsonProvider = jsonProvider ?? throw new ArgumentNullException(nameof(jsonProvider));
        }

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

            string json;
            try
            {
                json = _jsonProvider.GetLevelJson(levelNumber);
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load merge level {levelNumber} fixture from its JSON provider.", ex);
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
                    MixingEnabled = GetBool(root, "mixingEnabled", true),
                    Obstacles = ParseObstacles(root),
                    MergeOrbs = ParseOrbs(root.GetProperty("orbs")),
                    RestorationTargets = ParseTargets(root.GetProperty("targets")),
                };
            }
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

        private static bool GetBool(JsonElement element, string property, bool defaultValue)
        {
            if (element.TryGetProperty(property, out JsonElement value) &&
                (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
            {
                return value.GetBoolean();
            }
            return defaultValue;
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
