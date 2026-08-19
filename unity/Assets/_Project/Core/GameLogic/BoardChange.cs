// SPDX-License-Identifier: MIT
// Chroma Vale — Board change notification types (Core, no Unity deps)

#nullable enable

namespace ChromaVale.Core.GameLogic
{
    /// <summary>Kind of change that occurred on the board.</summary>
    public enum ChangeType
    {
        /// <summary>An orb appeared on an empty cell.</summary>
        OrbAdded,
        /// <summary>An orb was removed from a cell.</summary>
        OrbRemoved,
        /// <summary>An orb was replaced by another orb (merge result, Brown clear).</summary>
        OrbTransformed,
        /// <summary>Reserved for future cascade/gravity resolution (not yet emitted).</summary>
        Cascade,
    }

    /// <summary>Immutable grid coordinate (column, row).</summary>
    public record GridPosition(int X, int Y);

    /// <summary>Describes a single cell-level change so the view can animate exactly what happened.</summary>
    public record BoardChange(
        ChangeType Type,
        GridPosition Position,
        OrbData? OldOrb,
        OrbData? NewOrb
    );
}

// ───────────────────────────────────────────────────────────────────────────
// C# 9 record/init polyfill — added by game-builder (t_96040ba3).
// Unity's .NET Standard 2.1 profile does not ship IsExternalInit, which the C# 9
// compiler requires for `record` types and `init` accessors. Without this class,
// Core fails to compile with CS0518 under Unity 6.
// ───────────────────────────────────────────────────────────────────────────
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
