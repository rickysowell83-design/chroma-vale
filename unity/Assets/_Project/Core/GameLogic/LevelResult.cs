// SPDX-License-Identifier: MIT
// Chroma Vale — Level completion result (Core, no Unity deps)

namespace ChromaVale.Core.GameLogic
{
    /// <summary>Result of a completed level: how many moves were used, the level par, and the star rating earned.</summary>
    public record LevelResult(int MovesUsed, int Par, int Stars);
}
