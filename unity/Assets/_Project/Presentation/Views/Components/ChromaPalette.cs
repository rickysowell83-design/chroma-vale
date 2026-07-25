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
        public static readonly Color FlowGateUp = new(0.15f, 0.25f, 0.10f);
        public static readonly Color FlowGateDown = new(0.25f, 0.15f, 0.10f);
        public static readonly Color FlowGateRight = new(0.10f, 0.15f, 0.25f);
        public static readonly Color FlowGateLeft = new(0.20f, 0.10f, 0.15f);

        // ── PCB / cyberpunk circuit-board palette ─────────────────────────

        /// <summary>Copper/gold contact-pad colour for PCB via elements.</summary>
        public static readonly Color CopperVia = new(0.7f, 0.45f, 0.15f);

        /// <summary>Pitch-black base used for the PCB substrate.</summary>
        public static readonly Color PCBDark = new(0.02f, 0.02f, 0.03f);

        /// <summary>Dead-dark unpowered pipe colour.</summary>
        public static readonly Color DeadPipe = new(0.04f, 0.05f, 0.06f);
    }
}
