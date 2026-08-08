using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    public class LevelRepository : ILevelRepository
    {
        public int LevelCount => 21;

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
                11 => LevelData.Level11,
                12 => LevelData.Level12,
                13 => LevelData.Level13,
                14 => LevelData.Level14,
                15 => LevelData.Level15,
                16 => LevelData.Level16,
                17 => LevelData.Level17,
                18 => LevelData.Level18,
                19 => LevelData.Level19,
                20 => LevelData.Level20,
                99 => LevelData.Level99,
                _ => LevelData.Level1
            };
        }
    }
}
