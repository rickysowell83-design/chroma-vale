using System.Linq;

namespace ChromaVale.Core.GameLogic
{
    public struct LevelSource
    {
        public int X, Y, ColorIndex;
        public int FlowPressure; // Units of flow emitted per tick (default 1)
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
        public PipePiece[] Inventory;    // Available pieces for this level
        public int ParTicks = 20;        // Par completion time in ticks (for 3-star)

        // ═══════════════════════════════════════════════════════════════
        // WORLD 1: "First Light" — Learning to Flow (Levels 1-5)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Level 1: "First Light"
        /// Simplest possible level. One source, one target, straight line.
        /// Teaches: Place pipes, press FLOW ON, watch flow reach target.
        /// Grid: 4×4 | Par: 4 ticks
        /// </summary>
        public static LevelData Level1 => new()
        {
            Width = 4, Height = 4, ParTicks = 4,
            Sources = new[] { new LevelSource { X = 0, Y = 1, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 3, Y = 1, ColorIndex = 0 } },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
            }
        };

        /// <summary>
        /// Level 2: "Two Streams"
        /// Two colors, two routes. Can't cross — must route separately.
        /// Teaches: Multiple colors, obstacle avoidance.
        /// Grid: 4×4 | Par: 5 ticks
        /// </summary>
        public static LevelData Level2 => new()
        {
            Width = 4, Height = 4, ParTicks = 5,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 1 },
                new LevelSource { X = 0, Y = 3, ColorIndex = 1, FlowPressure = 1 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 3, Y = 0, ColorIndex = 0 },
                new LevelTarget { X = 3, Y = 3, ColorIndex = 1 },
            },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 1 }, new LevelObstacle { X = 2, Y = 2 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
            }
        };

        /// <summary>
        /// Level 3: "The Turn"
        /// Must use elbows to route around obstacles.
        /// Teaches: Elbow pieces, planning around blockers.
        /// Grid: 5×5 | Par: 6 ticks
        /// </summary>
        public static LevelData Level3 => new()
        {
            Width = 5, Height = 5, ParTicks = 6,
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 0, ColorIndex = 0 } },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 1 }, new LevelObstacle { X = 2, Y = 2 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
            }
        };

        /// <summary>
        /// Level 4: "Tight Budget"
        /// First level where piece scarcity matters. Multiple valid routes,
        /// but only one efficient one. Cross piece required for crossing path.
        /// Teaches: Inventory management, Cross piece usage.
        /// Grid: 5×5 | Par: 8 ticks
        /// </summary>
        public static LevelData Level4 => new()
        {
            Width = 5, Height = 5, ParTicks = 8,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 1 },
                new LevelSource { X = 0, Y = 4, ColorIndex = 1, FlowPressure = 1 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 4, ColorIndex = 0 },
                new LevelTarget { X = 4, Y = 0, ColorIndex = 1 },
            },
            Obstacles = new[]
            {
                new LevelObstacle { X = 1, Y = 2 }, new LevelObstacle { X = 2, Y = 1 },
                new LevelObstacle { X = 2, Y = 3 }, new LevelObstacle { X = 3, Y = 2 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.Cross(2),
            }
        };

        /// <summary>
        /// Level 5: "First Burst"
        /// First overflow risk. Source has pressure 2, but some pipes are capacity 1.
        /// Must use the capacity-2 pipe for the high-flow segment or it bursts.
        /// Teaches: Capacity management, overflow/burst mechanic.
        /// Grid: 5×5 | Par: 5 ticks
        /// </summary>
        public static LevelData Level5 => new()
        {
            Width = 5, Height = 5, ParTicks = 5,
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 2 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 3 }, new LevelObstacle { X = 2, Y = 1 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                // Only one capacity-2 pipe — must use it on the bottleneck segment
                PipePiece.Straight(1), PipePiece.Straight(1),
                PipePiece.Straight(2),
                PipePiece.Elbow(1), PipePiece.Elbow(1),
            }
        };

        // ═══════════════════════════════════════════════════════════════
        // WORLD 2: "Color Crossing" — Multi-Color with Valves (6-10)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Level 6: "Color Crossing"
        /// Two colors must avoid contamination. No shared cells.
        /// Teaches: Color separation, spatial planning.
        /// Grid: 5×5 | Par: 7 ticks
        /// </summary>
        public static LevelData Level6 => new()
        {
            Width = 5, Height = 5, ParTicks = 7,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 1, ColorIndex = 0, FlowPressure = 1 },
                new LevelSource { X = 0, Y = 3, ColorIndex = 1, FlowPressure = 1 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 1, ColorIndex = 0 },
                new LevelTarget { X = 4, Y = 3, ColorIndex = 1 },
            },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 2 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
            }
        };

        /// <summary>
        /// Level 7: "Valve Control"
        /// Introduces Valve pieces — one-way flow gates placed by the player.
        /// Teaches: Valve placement, forcing flow direction.
        /// Grid: 5×5 | Par: 7 ticks
        /// </summary>
        public static LevelData Level7 => new()
        {
            Width = 5, Height = 5, ParTicks = 7,
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = new[]
            {
                new LevelObstacle { X = 1, Y = 0 }, new LevelObstacle { X = 1, Y = 1 },
                new LevelObstacle { X = 1, Y = 3 }, new LevelObstacle { X = 1, Y = 4 },
                new LevelObstacle { X = 3, Y = 0 }, new LevelObstacle { X = 3, Y = 4 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
                // Valves force one-way flow — good for preventing backflow
                PipePiece.Valve(2, PipeDirection.Right),
            }
        };

        /// <summary>
        /// Level 8: "One-Way Maze"
        /// Environmental flow gates (pre-placed) force specific routing.
        /// Must enter gates from the correct side.
        /// Teaches: Environmental flow gates, BFS direction enforcement.
        /// Grid: 5×5 | Par: 8 ticks
        /// </summary>
        public static LevelData Level8 => new()
        {
            Width = 5, Height = 5, ParTicks = 8,
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = new[]
            {
                new LevelFlowGate { X = 2, Y = 1, Direction = PipeDirection.Right },
                new LevelFlowGate { X = 2, Y = 3, Direction = PipeDirection.Up },
            },
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
            }
        };

        /// <summary>
        /// Level 9: "Double Pressure"
        /// Two sources with different flow pressures share a bottleneck.
        /// The shared pipe must handle combined pressure or burst.
        /// Teaches: Capacity planning with multiple sources.
        /// Grid: 5×5 | Par: 6 ticks
        /// </summary>
        public static LevelData Level9 => new()
        {
            Width = 5, Height = 5, ParTicks = 6,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 1 },
                new LevelSource { X = 0, Y = 4, ColorIndex = 0, FlowPressure = 1 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 2, ColorIndex = 0 },
            },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 1 }, new LevelObstacle { X = 2, Y = 3 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(1), // This one will burst if both flows go through it
                PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.TJunction(2), // Merge point
            }
        };

        /// <summary>
        /// Level 10: "Crossfire"
        /// Two colors with crossing routes. Must use Cross piece strategically.
        /// First level with a genuine "plan ahead or fail" decision.
        /// Teaches: Cross routing, color isolation within shared junctions.
        /// Grid: 6×6 | Par: 10 ticks
        /// </summary>
        public static LevelData Level10 => new()
        {
            Width = 6, Height = 6, ParTicks = 10,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 1, ColorIndex = 0, FlowPressure = 1 },
                new LevelSource { X = 0, Y = 4, ColorIndex = 1, FlowPressure = 1 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 5, Y = 4, ColorIndex = 0 },
                new LevelTarget { X = 5, Y = 1, ColorIndex = 1 },
            },
            Obstacles = new[]
            {
                new LevelObstacle { X = 2, Y = 0 }, new LevelObstacle { X = 2, Y = 5 },
                new LevelObstacle { X = 3, Y = 2 }, new LevelObstacle { X = 3, Y = 3 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.Cross(2),
            }
        };

        // ═══════════════════════════════════════════════════════════════
        // Holdover: old tutorial levels (1-4) for backward compat
        // These match the original design; new levels start at Level1-10 above.
        // LevelRepository can map old index 1-4 to Level1-4 above,
        // or keep the old levels at higher indices for testing.
        // ═══════════════════════════════════════════════════════════════

        public static LevelData LegacyTutorial1 => new()
        {
            Width = 5, Height = 5, ParTicks = 10,
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[] { PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2) }
        };

        public static LevelData LegacyTutorial2 => new()
        {
            Width = 5, Height = 5, ParTicks = 10,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 1, ColorIndex = 0, FlowPressure = 1 },
                new LevelSource { X = 0, Y = 3, ColorIndex = 1, FlowPressure = 1 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 1, ColorIndex = 0 },
                new LevelTarget { X = 4, Y = 3, ColorIndex = 1 },
            },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 2 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
            }
        };

        public static LevelData LegacyTutorial3 => new()
        {
            Width = 5, Height = 5, ParTicks = 10,
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = new[]
            {
                new LevelFlowGate { X = 2, Y = 1, Direction = PipeDirection.Right },
                new LevelFlowGate { X = 2, Y = 3, Direction = PipeDirection.Up },
            },
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
            }
        };

        public static LevelData LegacyTutorial4 => new()
        {
            Width = 5, Height = 5, ParTicks = 15,
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 1 },
                new LevelSource { X = 0, Y = 4, ColorIndex = 1, FlowPressure = 1 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 4, ColorIndex = 0 },
                new LevelTarget { X = 4, Y = 0, ColorIndex = 1 },
            },
            Obstacles = new[]
            {
                new LevelObstacle { X = 2, Y = 2 }, new LevelObstacle { X = 2, Y = 1 }, new LevelObstacle { X = 2, Y = 3 },
            },
            FlowGates = new[]
            {
                new LevelFlowGate { X = 1, Y = 1, Direction = PipeDirection.Down },
                new LevelFlowGate { X = 3, Y = 3, Direction = PipeDirection.Up },
            },
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
            }
        };
    }

    public interface ILevelRepository
    {
        LevelData GetLevel(int levelNumber);
        int LevelCount { get; }
    }
}
