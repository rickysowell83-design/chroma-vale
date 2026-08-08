using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    /// <summary>
    /// Pure static scorer for puzzle level completion.
    /// Calculates 1-3 stars based on efficiency and par time.
    /// Engine-free — fully unit-testable.
    /// </summary>
    public static class ScoreCalculator
    {
        /// <summary>Inventory efficiency threshold for 2+ stars — player must use ≤60% of provided traces.</summary>
        public const float EfficiencyThreshold = 0.60f;

        public static int Calculate(TraceInventory inventory, SignalRouter simulator, LevelData level)
        {
            int stars = 1; // Base: completed the level
            int placed = inventory.PlacedCount;
            int total = inventory.Pieces.Count;

            if (total == 0) return 1;

            float usageRatio = (float)placed / total;

            // 2 stars: ≤60% inventory used
            if (usageRatio <= EfficiencyThreshold)
                stars = 2;

            // 3 stars: ≤60% inventory used AND under par time
            if (usageRatio <= EfficiencyThreshold && simulator.CurrentTick <= level.ParTicks)
                stars = 3;

            return stars;
        }
    }
}
