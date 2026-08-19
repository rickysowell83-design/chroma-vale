using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// TRANSITIONAL STUB — Replaces deleted cyberpunk ChromaPalette.
    /// Same static member names so all referencing code compiles.
    /// Colors mapped to Chroma Vale Art Style Guide values where possible.
    /// Full refactor will rename/replace these with ChromaValePalette.
    /// </summary>
    public static class ChromaPalette
    {
        // === Chroma Vale orb primaries (Art Style Guide §3.1) ===
        public static readonly Color NeonCyan      = HexToColor("#4ECDC4"); // Cyan primary
        public static readonly Color NeonMagenta   = HexToColor("#FF6B9D"); // Magenta primary
        public static readonly Color NeonYellow    = HexToColor("#FFD93D"); // Yellow primary
        public static readonly Color NeonPurple    = HexToColor("#9B59B6"); // Purple (mixed)
        public static readonly Color NeonGreen     = HexToColor("#6BCB77"); // Green (mixed)
        public static readonly Color NeonOrange    = HexToColor("#FF8C42"); // Orange (mixed)
        public static readonly Color NeonRed       = HexToColor("#FF6B6B"); // Coral (warm accent)

        // === Hint/indicator colors (transitional) ===
        public static readonly Color CyanHint      = HexToColor("#4ECDC4"); // Cyan at 60% opacity context
        public static readonly Color MagentaHint   = HexToColor("#FF6B9D"); // Magenta at 60% opacity context
        public static readonly Color ObstacleCol   = HexToColor("#8B6F47"); // Brown — obstacle/blocker

        // === UI palette (Art Style Guide §3.1) ===
        public static readonly Color DarkTile          = HexToColor("#2D6B6B"); // Dark Teal — primary text
        public static readonly Color SilkscreenLabel   = HexToColor("#6A9A9A"); // Muted Gray-Teal — secondary text
        public static readonly Color ButtonActiveTeal  = HexToColor("#4ECDC4"); // Accent cool
        public static readonly Color ButtonInactive    = HexToColor("#D6D6D6"); // Disabled fill
        public static readonly Color WinPopupBG        = HexToColor("#FFFFFF"); // Pure White — panel surface

        // === Legacy mappings (transitional — will be removed in Presentation refactor) ===
        // These map old cyberpunk names to closest Chroma Vale equivalents so code compiles.
        public static readonly Color CopperActive      = HexToColor("#FF6B6B"); // Coral — accent warm
        public static readonly Color CopperOxidized    = HexToColor("#8B6F47"); // Brown — failure color
        public static readonly Color PlayerTraceCopper = HexToColor("#FF6B6B"); // Coral — player action
        public static readonly Color GhostTraceCopper  = HexToColor("#6A9A9A"); // Muted Gray-Teal — ghost/recessed
        public static readonly Color GhostComponent    = HexToColor("#D6D6D6"); // Light Gray — disabled
        public static readonly Color ENIG_Gold         = HexToColor("#FFD93D"); // Warm Gold — star/achievement
        public static readonly Color PCB_Substrate     = HexToColor("#F8F4E8"); // Warm Cream — background
        public static readonly Color TraceCyanEmission = HexToColor("#4ECDC4"); // Cyan — emission
        public static readonly Color ViaCyan           = HexToColor("#4ECDC4"); // Cyan — via/connection

        private static Color HexToColor(string hex)
        {
            // Remove # prefix
            hex = hex.StartsWith("#") ? hex.Substring(1) : hex;
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color32(r, g, b, 255);
        }
    }
}
