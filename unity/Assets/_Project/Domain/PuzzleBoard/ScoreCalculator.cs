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
        public static int Calculate(PipeInventory inventory, FlowSimulator simulator, LevelData level)
        {
            int stars = 1; // Base: completed the level
            int unused = inventory.AvailableCount;
            int total = inventory.Pieces.Count;

            // 2 stars: completed with leftover pieces (used less than provided)
            if (unused > 0)
                stars = 2;

            // 3 stars: completed with leftover pieces AND under par time
            if (unused > 0 && simulator.CurrentTick <= level.ParTicks)
                stars = 3;

            return stars;
        }
    }
}
