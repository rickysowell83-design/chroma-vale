using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    public static class ChromaPalette
    {
        public static readonly Color NeonCyan = new(0.2f, 0.9f, 0.95f);
        public static readonly Color NeonMagenta = new(0.95f, 0.2f, 0.7f);
        public static readonly Color NeonYellow = new(0.95f, 0.9f, 0.1f);
        public static readonly Color NeonPurple = new(0.65f, 0.2f, 0.95f);
        public static readonly Color NeonGreen = new(0.2f, 0.95f, 0.3f);
        public static readonly Color NeonOrange = new(0.95f, 0.5f, 0.1f);
        public static readonly Color NeonRed = new(0.95f, 0.15f, 0.2f);
        public static readonly Color DarkTile = new(0.08f, 0.08f, 0.1f);
        public static readonly Color DarkBG = new(0.02f, 0.02f, 0.04f);
        public static readonly Color CyanHint = new(0.06f, 0.16f, 0.20f);
        public static readonly Color MagentaHint = new(0.16f, 0.06f, 0.13f);
        public static readonly Color YellowHint = new(0.14f, 0.14f, 0.06f);
        public static readonly Color PurpleHint = new(0.1f, 0.05f, 0.15f);
        public static readonly Color ObstacleCol = new(0.18f, 0.07f, 0.07f);
        public static readonly Color SignalGateUp = new(0.15f, 0.25f, 0.10f);
        public static readonly Color SignalGateDown = new(0.25f, 0.15f, 0.10f);
        public static readonly Color SignalGateRight = new(0.10f, 0.15f, 0.25f);
        public static readonly Color SignalGateLeft = new(0.20f, 0.10f, 0.15f);

        // ── PCB / cyberpunk circuit-board palette (v3 mockup) ─────────

        /// <summary>Copper/gold contact-pad colour for PCB via elements.</summary>
        public static readonly Color CopperVia = new(0.7f, 0.45f, 0.15f);

        /// <summary>Pitch-black base used for the PCB substrate.</summary>
        public static readonly Color PCBDark = new(0.02f, 0.02f, 0.03f);

        /// <summary>Dead-dark unpowered pipe colour.</summary>
        public static readonly Color DeadPipe = new(0.04f, 0.05f, 0.06f);

        /// <summary>Copper idle pipe colour — warm metallic bronze for placed traces.</summary>
        public static readonly Color CopperIdle = new(0.72f, 0.42f, 0.18f);

        /// <summary>Darker copper for rotation preview state.</summary>
        public static readonly Color CopperDark = new(0.55f, 0.32f, 0.12f);

        // ── v3 Mockup PCB Palette ────────────────────────────────────────

        /// <summary>PCB substrate dark green (#0A160A) — boosted for orthographic visibility.</summary>
        public static readonly Color PCB_Substrate = new(0.18f, 0.30f, 0.18f);

        /// <summary>ENIG gold pad rim (#D4A843).</summary>
        public static readonly Color ENIG_Gold = new(0.831f, 0.659f, 0.263f);

        /// <summary>Source via center cyan (#00E5FF).</summary>
        public static readonly Color ViaCyan = new(0f, 0.898f, 1f);

        /// <summary>Destination dark void (#020802).</summary>
        public static readonly Color DestVoid = new(0.008f, 0.031f, 0.008f);

        /// <summary>Oxidized copper trace — dark/unlit (#5C3A1E).</summary>
        public static readonly Color CopperOxidized = new(0.361f, 0.227f, 0.118f);

        /// <summary>Active lit copper base (#B87333).</summary>
        public static readonly Color CopperActive = new(0.722f, 0.451f, 0.200f);

        /// <summary>Cyan emission overlay for active traces (#00E5FF).</summary>
        public static readonly Color TraceCyanEmission = new(0f, 0.898f, 1f);

        /// <summary>ROUTE button inactive (#3A3A3A).</summary>
        public static readonly Color ButtonInactive = new(0.227f, 0.227f, 0.227f);

        /// <summary>ROUTE button active teal glow (#00E5FF).</summary>
        public static readonly Color ButtonActiveTeal = new(0f, 0.898f, 1f);

        /// <summary>Win popup background (#0D1117).</summary>
        public static readonly Color WinPopupBG = new(0.051f, 0.067f, 0.090f);

        /// <summary>Ghost component outline opacity (8-12%).</summary>
        public static readonly Color GhostComponent = new(0.08f, 0.10f, 0.08f, 0.10f);

        /// <summary>Silkscreen label opacity (12-18%).</summary>
        public static readonly Color SilkscreenLabel = new(0.9f, 0.9f, 0.85f, 0.15f);
    }
}
