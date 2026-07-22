using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class LevelRepository : ILevelRepository
    {
        public int LevelCount => 1;

        public LevelData GetLevel(int levelNumber)
        {
            return levelNumber switch
            {
                1 => LevelData.Tutorial1,
                _ => LevelData.Tutorial1
            };
        }
    }
}