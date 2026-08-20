// SPDX-License-Identifier: MIT
// Chroma Vale — ILevelJsonProvider: engine-agnostic seam that supplies raw
// merge level fixture JSON (by 1-based level number) to MergeLevelRepository.
// Decouples Core from any loading mechanism (Unity Resources, filesystem,
// embedded assets, network) so the Core assembly stays pure C#.

namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Supplies raw JSON for a merge level fixture, keyed by 1-based level number.
    /// </summary>
    public interface ILevelJsonProvider
    {
        /// <summary>
        /// Returns the raw JSON for level <paramref name="levelNumber"/> (1-based).
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        /// the fixture cannot be located or read.
        /// </exception>
        string GetLevelJson(int levelNumber);
    }
}
