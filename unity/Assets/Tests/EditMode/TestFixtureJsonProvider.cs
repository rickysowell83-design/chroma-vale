// SPDX-License-Identifier: MIT
// Chroma Vale — TestFixtureJsonProvider: test-only ILevelJsonProvider that reads
// the validator-authored fixtures from the repo on disk. Works in Unity
// EditMode tests and in the headless testrunner (which has no UnityEngine).

using System;
using System.IO;
using ChromaVale.Core.GameLogic;

namespace ChromaVale.Tests
{
    /// <summary>
    /// Supplies level fixture JSON from
    /// tools/MergeLevelValidator/tests/fixtures/level_0N.json (searched upward
    /// from the current directory and the app base directory).
    /// </summary>
    public sealed class TestFixtureJsonProvider : ILevelJsonProvider
    {
        private const string FixturesDirectory = "tools/MergeLevelValidator/tests/fixtures";

        /// <inheritdoc />
        public string GetLevelJson(int levelNumber)
        {
            string fileName = $"level_{levelNumber:00}.json";
            foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, FixturesDirectory, fileName);
                    if (File.Exists(candidate))
                    {
                        return File.ReadAllText(candidate);
                    }
                    dir = dir.Parent;
                }
            }
            throw new InvalidOperationException(
                $"Merge fixture '{fileName}' not found under '{FixturesDirectory}' relative to the repo root.");
        }
    }
}
