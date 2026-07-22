namespace ChromaVale.Core.GameLogic
{
    public struct LevelSource
    {
        public int X, Y, ColorIndex;
    }

    public struct LevelTarget
    {
        public int X, Y, ColorIndex;
    }

    public struct LevelObstacle
    {
        public int X, Y;
    }

    public class LevelData
    {
        public int Width;
        public int Height;
        public LevelSource[] Sources;
        public LevelTarget[] Targets;
        public LevelObstacle[] Obstacles;

        public static LevelData Tutorial1 => new LevelData
        {
            Width = 5,
            Height = 5,
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = new LevelObstacle[0]
        };
    }

    public interface ILevelRepository
    {
        LevelData GetLevel(int levelNumber);
        int LevelCount { get; }
    }
}
