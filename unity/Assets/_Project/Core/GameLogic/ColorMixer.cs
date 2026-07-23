using System.Collections.Generic;

namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Color mixing table and utilities.
    /// When two colored flows meet at the same cell, they mix into a new color.
    ///
    /// Color indices:
    ///   0 = Cyan, 1 = Magenta, 2 = Yellow (base colors)
    ///   6 = Purple (Cyan + Magenta)
    ///   7 = Green  (Cyan + Yellow)
    ///   8 = Orange (Magenta + Yellow)
    ///   9 = Brown  (waste — three+ distinct colors or invalid mix)
    /// </summary>
    public static class ColorMixer
    {
        // Pre-computed mixing table: (colorA, colorB) → result
        // Only populated for the 3 beneficial pairs.
        private static readonly Dictionary<(int, int), int> MixTable = new()
        {
            { (0, 1), 6 },  // Cyan + Magenta = Purple
            { (0, 2), 7 },  // Cyan + Yellow = Green
            { (1, 2), 8 },  // Magenta + Yellow = Orange
        };

        public const int Cyan = 0;
        public const int Magenta = 1;
        public const int Yellow = 2;
        public const int Purple = 6;
        public const int Green = 7;
        public const int Orange = 8;
        public const int Brown = 9;  // Waste — cannot satisfy any target

        /// <summary>
        /// Mix two colors. Returns the resulting color index.
        /// Same color + same color = same color (reinforcement).
        /// Different base colors = mix result.
        /// Any mix involving a non-base color (6-8) behaves like the base had been added.
        /// </summary>
        public static int Mix(int colorA, int colorB)
        {
            // Same color = no change
            if (colorA == colorB) return colorA;

            // Brown mixed with anything = still brown
            if (colorA == Brown || colorB == Brown) return Brown;

            // Normalize to an ordered pair for table lookup
            int a = NormalizeColor(colorA);
            int b = NormalizeColor(colorB);

            if (a == b) return colorA; // After normalization, same = no change

            int keyA = a < b ? a : b;
            int keyB = a < b ? b : a;

            if (MixTable.TryGetValue((keyA, keyB), out int result))
                return result;

            // Any pair not in the table = Brown (waste)
            return Brown;
        }

        /// <summary>
        /// Mix 3+ colors — always Brown (waste).
        /// </summary>
        public static int MixMultiple(int[] colors)
        {
            if (colors == null || colors.Length == 0) return -1;
            if (colors.Length == 1) return colors[0];
            if (colors.Length == 2) return Mix(colors[0], colors[1]);

            // 3+ distinct colors = brown
            var distinct = new HashSet<int>();
            foreach (int c in colors) distinct.Add(c);
            if (distinct.Count >= 3) return Brown;

            // All same → no change
            if (distinct.Count == 1)
            {
                foreach (int c in distinct) return c;
            }

            // 2 distinct → mix
            int first = -1, second = -1;
            foreach (int c in distinct)
            {
                if (first == -1) first = c;
                else second = c;
            }
            return Mix(first, second);
        }

        /// <summary>
        /// A color is "valid" if it can satisfy a target. Brown (9) is always invalid.
        /// Base colors (0-2) and mix results (6-8) are valid.
        /// </summary>
        public static bool IsValidColor(int colorIndex) =>
            colorIndex >= 0 && colorIndex <= 8 && colorIndex != 3 && colorIndex != 4 && colorIndex != 5;

        /// <summary>
        /// Check if two colors can be mixed to produce the desired target.
        /// For a single color, check direct match.
        /// </summary>
        public static bool CanProduceColor(int[] sourceColors, int targetColor)
        {
            if (sourceColors == null || sourceColors.Length == 0) return false;
            int result = MixMultiple(sourceColors);
            return result == targetColor;
        }

        /// <summary>
        /// Get a human-readable name for a color index.
        /// </summary>
        public static string GetColorName(int index) => index switch
        {
            0 => "Cyan",
            1 => "Magenta",
            2 => "Yellow",
            6 => "Purple",
            7 => "Green",
            8 => "Orange",
            9 => "Brown",
            _ => $"Color {index}"
        };

        /// <summary>
        /// Map derived colors back to base colors for display.
        /// Purple(6) → Cyan+Magenta, Green(7) → Cyan+Yellow, etc.
        /// </summary>
        private static int NormalizeColor(int color)
        {
            return color switch
            {
                Purple => Cyan,   // Purple treated as its components for mixing
                Green => Cyan,
                Orange => Magenta,
                _ => color
            };
        }
    }
}
