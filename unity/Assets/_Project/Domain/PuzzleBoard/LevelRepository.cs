using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class LevelRepository : ILevelRepository
    {
        public int LevelCount => 20;

        public LevelData GetLevel(int levelNumber)
        {
            return levelNumber switch
            {
                99 => LevelData.Level99,
                _ => LevelData.Level1
            };
        }
    }
}
