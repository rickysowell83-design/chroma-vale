using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class LevelRepository : ILevelRepository
    {
        public int LevelCount => 10;

        public LevelData GetLevel(int levelNumber)
        {
            return levelNumber switch
            {
                1 => LevelData.Level1,
                2 => LevelData.Level2,
                3 => LevelData.Level3,
                4 => LevelData.Level4,
                5 => LevelData.Level5,
                6 => LevelData.Level6,
                7 => LevelData.Level7,
                8 => LevelData.Level8,
                9 => LevelData.Level9,
                10 => LevelData.Level10,
                _ => LevelData.Level1
            };
        }
    }
}
