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

    public struct LevelFlowGate
    {
        public int X, Y;
        public PipeDirection Direction;
    }

    public class LevelData
    {
        public int Width;
        public int Height;
        public LevelSource[] Sources;
        public LevelTarget[] Targets;
        public LevelObstacle[] Obstacles;
        public LevelFlowGate[] FlowGates;

        // ── World 1: Meadow — basic color-to-target routing ──

        public static LevelData Tutorial1 => new()
        {
            Width = 5, Height = 5,
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = System.Array.Empty<LevelFlowGate>()
        };

        public static LevelData Tutorial2 => new()
        {
            Width = 5, Height = 5,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 1, ColorIndex = 0 },  // Cyan
                new LevelSource { X = 0, Y = 3, ColorIndex = 1 },  // Magenta
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 1, ColorIndex = 0 },
                new LevelTarget { X = 4, Y = 3, ColorIndex = 1 },
            },
            Obstacles = new[]
            {
                new LevelObstacle { X = 2, Y = 2 }  // Center blocker
            },
            FlowGates = System.Array.Empty<LevelFlowGate>()
        };

        // ── World 2: Riverlands — directional flow gates ──

        public static LevelData Tutorial3 => new()
        {
            Width = 5, Height = 5,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 2, ColorIndex = 0 },  // Cyan
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 2, ColorIndex = 0 },
            },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = new[]
            {
                // One-way gates force you to enter from the correct side
                new LevelFlowGate { X = 2, Y = 1, Direction = PipeDirection.Right },
                new LevelFlowGate { X = 2, Y = 3, Direction = PipeDirection.Up },
            }
        };

        // ── World 2: Flow gate + obstacle combo ──

        public static LevelData Tutorial4 => new()
        {
            Width = 5, Height = 5,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 0, ColorIndex = 0 },  // Cyan
                new LevelSource { X = 0, Y = 4, ColorIndex = 1 },  // Magenta
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 4, ColorIndex = 0 },
                new LevelTarget { X = 4, Y = 0, ColorIndex = 1 },
            },
            Obstacles = new[]
            {
                new LevelObstacle { X = 2, Y = 2 },  // Center
                new LevelObstacle { X = 2, Y = 1 },
                new LevelObstacle { X = 2, Y = 3 },
            },
            FlowGates = new[]
            {
                // Cyan must go around the obstacles and through the right gate
                new LevelFlowGate { X = 1, Y = 1, Direction = PipeDirection.Down },
                new LevelFlowGate { X = 3, Y = 3, Direction = PipeDirection.Up },
            }
        };
    }

    public interface ILevelRepository
    {
        LevelData GetLevel(int levelNumber);
        int LevelCount { get; }
    }
}
