using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class LevelRepository : ILevelRepository
    {
        public int LevelCount => 1;

        public LevelData GetLevel(int levelNumber)
        {
            return LevelData.Level1;
        }
    }
}
