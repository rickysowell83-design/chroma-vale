using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class LevelRepository : ILevelRepository
    {
        public int LevelCount => 2;

        public LevelData GetLevel(int levelNumber)
        {
            return levelNumber switch
            {
                1 => LevelData.Tutorial1,
                2 => LevelData.Tutorial2,
                _ => LevelData.Tutorial1
            };
        }
    }
}