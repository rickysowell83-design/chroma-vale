using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class LevelRepository : ILevelRepository
    {
        public int LevelCount => 4;

        public LevelData GetLevel(int levelNumber)
        {
            return levelNumber switch
            {
                1 => LevelData.Tutorial1,
                2 => LevelData.Tutorial2,
                3 => LevelData.Tutorial3,
                4 => LevelData.Tutorial4,
                _ => LevelData.Tutorial1
            };
        }
    }
}
