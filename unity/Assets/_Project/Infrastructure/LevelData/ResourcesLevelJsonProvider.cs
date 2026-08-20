// SPDX-License-Identifier: MIT
// Chroma Vale — ResourcesLevelJsonProvider: ILevelJsonProvider backed by
// TextAssets embedded in a Resources folder (Assets/_Project/Resources/Levels/).
// Fixture-agnostic: the designer's L1-10 redesign fixtures drop into the same
// Resources/Levels/ path and are picked up automatically by name.

using System;
using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Infrastructure.LevelData
{
    /// <summary>
    /// Loads level fixture JSON via <see cref="Resources.Load{T}"/> from
    /// "Levels/level_0N" (N = 1-based level number).
    /// </summary>
    public sealed class ResourcesLevelJsonProvider : ILevelJsonProvider
    {
        /// <inheritdoc />
        public string GetLevelJson(int levelNumber)
        {
            string resourcePath = $"Levels/level_{levelNumber:00}";
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Level fixture TextAsset '{resourcePath}' not found in any Resources folder.");
            }
            return asset.text;
        }
    }
}
