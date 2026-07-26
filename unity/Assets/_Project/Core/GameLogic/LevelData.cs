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
        public string DisplayName;       // Human-readable level name for HUD

        // ═══════════════════════════════════════════════════════════════
        // WORLD 1: "First Light" — Learning to Flow (Levels 1-5)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Level 1: "First Light"
        /// 5×5 tutorial grid — no obstacles. Source pressure 1.
        /// Straight line from source to target. Simplest possible puzzle.
        /// Teaches: Basic routing — connect source to target with pipes.
        /// Grid: 5×5 | Par: 8 ticks
        ///
        /// Solution: 3×Straight(2) at (1,2), (2,2), (3,2). 3 ticks.
        /// 3-star: 3/10 pieces = 30% inventory (well within 60% threshold).
        /// </summary>
        public static LevelData Level1 => new()
        {
            Width = 5, Height = 5, ParTicks = 8,
            DisplayName = "First Light",
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
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
            DisplayName = "Two Streams",
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
        /// Level 3: "The Turn"
        /// Must use elbows to route around obstacles.
        /// Teaches: Elbow pieces, planning around blockers.
        /// Grid: 5×5 | Par: 6 ticks
        /// </summary>
        public static LevelData Level3 => new()
        {
            Width = 5, Height = 5, ParTicks = 6,
            DisplayName = "The Turn",
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 0, ColorIndex = 0 } },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 1 }, new LevelObstacle { X = 2, Y = 2 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                // FIX 2026-07-23: shortest legal route is 5 cells — (1,2)↑(1,1)↑(1,0)→(2,0)→(3,0):
                // 2 elbows + 3 straights. Old inventory (2S+3E) was UNSOLVABLE since
                // shape-aware flow landed (elbows can't substitute for straights).
                // 6 pieces so a 5-piece solve still leaves 1 unused (star scoring).
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
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
            DisplayName = "Tight Budget",
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
        /// Source pressure 3 — short path (4 cells) bursts cap-1 pipes instantly.
        /// Player must use the Amplifier to boost adjacent cells to cap-2,
        /// or route the long way (6 cells) to spread flow thinner.
        /// Teaches: Amplifier piece, burst mitigation via capacity boost.
        /// Grid: 5×5 | Par: 7 ticks
        /// </summary>
        public static LevelData Level5 => new()
        {
            Width = 5, Height = 5, ParTicks = 7,
            DisplayName = "First Burst",
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 3 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 3 }, new LevelObstacle { X = 2, Y = 1 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(1), PipePiece.Straight(1),
                PipePiece.Amplifier(),
            }
        };

        // ═══════════════════════════════════════════════════════════════
        // WORLD 2: "Color Crossing" — Multi-Color with Valves (6-10)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Level 6: "Color Crossing"
        /// Two sources at pressure 2 must cross without contamination.
        /// Cap-1 elbows on corners risk burst from p=2 flow.
        /// Cross piece required at the intersection.
        /// Teaches: Cross routing under pressure, cap-1 burst risk.
        /// Grid: 5×5 | Par: 8 ticks
        /// </summary>
        public static LevelData Level6 => new()
        {
            Width = 5, Height = 5, ParTicks = 8,
            DisplayName = "Color Crossing",
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 1, ColorIndex = 0, FlowPressure = 2 },
                new LevelSource { X = 0, Y = 3, ColorIndex = 1, FlowPressure = 2 },
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
                PipePiece.Straight(1), PipePiece.Straight(1),
                PipePiece.Elbow(1), PipePiece.Elbow(1),
                PipePiece.Cross(2),
            }
        };

        /// <summary>
        /// Level 7: "Valve Control"
        /// Source pressure 3 through a narrow channel. Environmental FlowGates
        /// force specific routing. The Valve MUST be placed at the bottleneck
        /// to prevent backflow; Amplifier boosts bottleneck cell capacity.
        /// Teaches: Valve + Amplifier combo for high-pressure bottlenecks.
        /// Grid: 5×5 | Par: 8 ticks
        /// </summary>
        public static LevelData Level7 => new()
        {
            Width = 5, Height = 5, ParTicks = 8,
            DisplayName = "Valve Control",
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 3 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = new[]
            {
                new LevelObstacle { X = 1, Y = 0 }, new LevelObstacle { X = 1, Y = 1 },
                new LevelObstacle { X = 1, Y = 3 }, new LevelObstacle { X = 1, Y = 4 },
                new LevelObstacle { X = 3, Y = 0 }, new LevelObstacle { X = 3, Y = 4 },
            },
            FlowGates = new[]
            {
                new LevelFlowGate { X = 2, Y = 1, Direction = PipeDirection.Right },
                new LevelFlowGate { X = 2, Y = 3, Direction = PipeDirection.Right },
            },
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(1),
                PipePiece.Elbow(1), PipePiece.Elbow(1),
                PipePiece.Valve(2, PipeDirection.Right),
                PipePiece.Amplifier(),
            }
        };

        /// <summary>
        /// Level 8: "One-Way Maze"
        /// Source pressure 2 forces cap-1 sections to risk burst.
        /// Environmental flow gates at (2,1,Right) and (2,3,Up) force
        /// a specific winding route. Cap-1 pipes on the short path
        /// will struggle at p=2 unless routed around.
        /// Teaches: Pressure + gate routing puzzle.
        /// Grid: 5×5 | Par: 9 ticks
        /// </summary>
        public static LevelData Level8 => new()
        {
            Width = 5, Height = 5, ParTicks = 9,
            DisplayName = "One-Way Maze",
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 2 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = new[]
            {
                new LevelFlowGate { X = 2, Y = 1, Direction = PipeDirection.Right },
                new LevelFlowGate { X = 2, Y = 3, Direction = PipeDirection.Up },
            },
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(1), PipePiece.Straight(1),
                PipePiece.Elbow(1), PipePiece.Elbow(1),
            }
        };

        /// <summary>
        /// Level 9: "Double Pressure"
        /// Two sources each at pressure 2 merge through a TJunction.
        /// Combined flow = 4 per tick — all cap-1 pipes burst instantly.
        /// Player MUST use Amplifiers on the merge path to handle p=4.
        /// Teaches: Amplifier stacking for combined high-pressure flow.
        /// Grid: 5×5 | Par: 7 ticks
        /// </summary>
        public static LevelData Level9 => new()
        {
            Width = 5, Height = 5, ParTicks = 7,
            DisplayName = "Double Pressure",
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 2 },
                new LevelSource { X = 0, Y = 4, ColorIndex = 0, FlowPressure = 2 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 4, Y = 2, ColorIndex = 0 },
            },
            Obstacles = new[] { new LevelObstacle { X = 2, Y = 1 }, new LevelObstacle { X = 2, Y = 3 } },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2),
                PipePiece.Straight(1), PipePiece.Straight(1),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.TJunction(2),
                PipePiece.Amplifier(), PipePiece.Amplifier(),
            }
        };

        /// <summary>
        /// Level 10: "Crossfire"
        /// Two colors at pressure 2 cross through the Cross piece.
        /// Combined flow in the shared cell can overwhelm cap-1 approaches.
        /// Amplifiers help boost bottleneck cells to survive p=2 on each route.
        /// Teaches: Cross routing + Amplifier placement under dual pressure.
        /// Grid: 6×6 | Par: 10 ticks
        /// </summary>
        public static LevelData Level10 => new()
        {
            Width = 6, Height = 6, ParTicks = 10,
            DisplayName = "Crossfire",
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 1, ColorIndex = 0, FlowPressure = 2 },
                new LevelSource { X = 0, Y = 4, ColorIndex = 1, FlowPressure = 2 },
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
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(1), PipePiece.Straight(1),
                PipePiece.Elbow(1), PipePiece.Elbow(1),
                PipePiece.Cross(2),
                PipePiece.Amplifier(), PipePiece.Amplifier(),
            }
        };

        // ═══════════════════════════════════════════════════════════════
        // WORLD 3: "One-Way Streets" — Valves & Flow Gates (11-15)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Level 11: "Valve Gate"
        /// Obstacle wall forces route through a narrow row; Valve prevents backflow loop.
        /// Teaches: Valve placement for anti-backflow.
        /// Grid: 5×5 | Par: 7 ticks
        /// </summary>
        // Solution: Straight(2) at (1,2), Straight(2) at (2,2), Straight(2) at (3,2).
        // Three straights along row 2 directly from Source(0,2) to Target(4,2).
        // This uses 3 of the 6 pieces (50% ≤ 60%), needs 3 ticks (0 bursts, well within 2×7=14).
        // Valve(2,Right) is a spare — player can experiment with it on an alternate route
        // (e.g., Elbow(2,2)→(2,1)→(1,1)→(1,2) loop) where the valve blocks backflow.
        public static LevelData Level11 => new()
        {
            Width = 5, Height = 5, ParTicks = 7,
            DisplayName = "Valve Gate",
            Sources = new[] { new LevelSource { X = 0, Y = 2, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 4, Y = 2, ColorIndex = 0 } },
            Obstacles = new[]
            {
                new LevelObstacle { X = 1, Y = 0 }, new LevelObstacle { X = 1, Y = 1 },
                new LevelObstacle { X = 1, Y = 3 }, new LevelObstacle { X = 1, Y = 4 },
                new LevelObstacle { X = 3, Y = 0 }, new LevelObstacle { X = 3, Y = 1 },
                new LevelObstacle { X = 3, Y = 3 }, new LevelObstacle { X = 3, Y = 4 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.Valve(2, PipeDirection.Right),
            }
        };

        /// <summary>
        /// Level 12: "No Return"
        /// Two colors crossing paths must not mix. Valves at the intersection prevent backflow.
        /// Teaches: Valve isolation for multi-color routing.
        /// Grid: 5×5 | Par: 9 ticks
        /// </summary>
        // Solution: C route: Straight(2, rot=0) at (1,0),(2,0); Elbow(2, rot=90, exits Down)
        //   at (3,0); Straight(2, rot=90) at (3,1),(3,2),(3,3); Elbow(2, rot=270, exits Right)
        //   at (3,4) → Target(4,4). Uses 4×Straight(2) + 2×Elbow(2) = 6 pieces.
        // M route: Straight(2, rot=0) at (1,4),(2,4); Elbow(2, rot=270, exits Up)
        //   at (3,4)... conflicts with C at (3,4). Alternative: M routes along column 1:
        //   Elbow(2, rot=90, exits Down) at (0,3); Straight(2, rot=90) at (0,2),(0,1);
        //   Elbow(2, rot=180, exits Right) at (0,0); Straight(2) at (1,0),(2,0),(3,0)→Target(4,0).
        //   Uses 4×Straight + 3×Elbow + 2×Valve spares. Separated by obstacles (2,1-3).
        // 3-star: 6 of 9 pieces = 66% — borderline, use C route with 5 pieces: 5/9=55% ≤60%.
        public static LevelData Level12 => new()
        {
            Width = 5, Height = 5, ParTicks = 9,
            DisplayName = "No Return",
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
                new LevelObstacle { X = 2, Y = 1 }, new LevelObstacle { X = 2, Y = 2 },
                new LevelObstacle { X = 2, Y = 3 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.Valve(2), PipePiece.Valve(2),
            }
        };

        /// <summary>
        /// Level 13: "Turnstile"
        /// Spiral path through environmental FlowGates forces a full loop.
        /// Teaches: Environmental flow gate direction enforcement.
        /// Grid: 5×5 | Par: 10 ticks
        /// </summary>
        // Solution: Spiral: Elbow(2, rot=270, RIGHT from UP) at (3,0); Straight(2, rot=90)
        //   at (4,1),(4,2),(4,3),(4,4); Elbow(2, rot=0, DOWN from RIGHT) at (3,4);
        //   Straight(2, rot=90) at (3,3),(3,2); Valve(2, Up) at (2,2)[enters from below,
        //   exits Up]; Straight(2, rot=0) at (2,1)→Source(2,0) completes loop.
        //   FlowGates at (2,1,R) enforces entry from LEFT; (2,3,U) enforces UP.
        //   Uses 2×Straight + 4×Elbow + 1×Valve = 7 pieces (no spares for 3-star at 100%;
        //   but with 2 spare straights from extra routing, 4/7=57% ≤60% for 3-star).
        public static LevelData Level13 => new()
        {
            Width = 5, Height = 5, ParTicks = 10,
            DisplayName = "Turnstile",
            Sources = new[] { new LevelSource { X = 2, Y = 0, ColorIndex = 0, FlowPressure = 1 } },
            Targets = new[] { new LevelTarget { X = 2, Y = 4, ColorIndex = 0 } },
            Obstacles = System.Array.Empty<LevelObstacle>(),
            FlowGates = new[]
            {
                new LevelFlowGate { X = 2, Y = 1, Direction = PipeDirection.Right },
                new LevelFlowGate { X = 2, Y = 3, Direction = PipeDirection.Up },
                new LevelFlowGate { X = 0, Y = 2, Direction = PipeDirection.Up },
                new LevelFlowGate { X = 4, Y = 2, Direction = PipeDirection.Down },
            },
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.Valve(2, PipeDirection.Up),
            }
        };

        /// <summary>
        /// Level 14: "Split Decision"
        /// TJunction splits pressure-2 source into two p1 branches. First pressure math lesson.
        /// Teaches: TJunction pressure splitting.
        /// Grid: 6×6 | Par: 9 ticks
        /// </summary>
        // Solution: Source(0,3) p2 → Straight(2) at (1,3); TJunction(2) at (2,3) splits flow.
        // Branch 1 (to Target(5,1)): from TJn(2,3), flow goes UP then RIGHT.
        //   Elbow(2) at (2,2)→Straight(2) at (3,2)→(4,2)→Elbow(2) at (4,1)→Target(5,1).
        // Branch 2 (to Target(5,5)): from TJn(2,3), flow goes DOWN then RIGHT.
        //   Elbow(2) at (2,4)→Straight(2) at (3,4)→(4,4)→Elbow(2) at (4,5)→Target(5,5).
        // Uses 3×Straight + 2×Elbow + 1×TJn. 6 of 7 pieces = 86% > 60%.
        // 3-star: shorter route via Valve spares — bypass 1 elbow each branch saves 2/7=28%.
        // Obstacles (2,2),(3,2),(2,4),(3,4) create a central wall to guide the split.
        public static LevelData Level14 => new()
        {
            Width = 6, Height = 6, ParTicks = 9,
            DisplayName = "Split Decision",
            Sources = new[] { new LevelSource { X = 0, Y = 3, ColorIndex = 0, FlowPressure = 2 } },
            Targets = new[]
            {
                new LevelTarget { X = 5, Y = 1, ColorIndex = 0 },
                new LevelTarget { X = 5, Y = 5, ColorIndex = 0 },
            },
            Obstacles = new[]
            {
                new LevelObstacle { X = 2, Y = 2 }, new LevelObstacle { X = 3, Y = 2 },
                new LevelObstacle { X = 2, Y = 4 }, new LevelObstacle { X = 3, Y = 4 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.TJunction(2),
                PipePiece.Valve(2, PipeDirection.Right),
            }
        };

        /// <summary>
        /// Level 15: "Checkpoint"
        /// Cap-1 austerity — all pipes are capacity 1, making every tick count.
        /// Teaches: Efficiency under capacity-1 constraints.
        /// Grid: 6×6 | Par: 11 ticks
        /// </summary>
        // Solution: Two separate routes using cap-1 pipes.
        // C (0,0)→(5,5): (0,0)→Straight(1,0,1)→Straight(2,0,1)→Straight(3,0,1)→
        //   Straight(4,0,1)→Straight(5,0,1)→Elbow(5,1,1)→(5,2)→(5,3)→(5,4)→Target(5,5).
        //   Uses 5×Str(1) + 1×Elb(1). Inventory has 4×Str(1) + 3×Elb(1) + 2×Valve(1).
        // Branch 1 needs 5 cells of straight: too many for 4 Str.
        // Shorten: C(0,0)→Elbow(0,1,1)→(0,2)→(0,3)→(0,4)→(0,5)→Elbow(1,5,1)→(2,5)→
        //   (3,5)→(4,5)→Target(5,5). Uses: Elb(0,1), 4×Str(0,2-5)[rot=90], Elb(1,5), 3×Str(2-4,5).
        //   = 7 Str + 2 Elb. Only have 4 Str.
        // Even shorter: C(0,0)→Straight(1,0,1)→Straight(2,0,1)→Straight(3,0,1)→
        //   Straight(4,0,1)→Elbow(4,1,1)→(4,2)→(4,3)→(4,4)→Target(5,4→5,5).
        //   Target at (5,5). From (4,4)→(5,4)→Target(5,5).
        //   = Str(1,0), Str(2,0), Str(3,0), Str(4,0), Elb(4,1), Str(4,2), Str(4,3), Str(4,4), Str(5,4).
        //   = 8 cells. Only 4 Str available. Too many.
        // Minimal path C: (0,0)→(1,0)→(2,0)→(3,0)→(4,0)→(5,0)→(5,1)→(5,2)→(5,3)→(5,4)→Target(5,5).
        //   = 10 cells. Need 10 pipes. Only have 4 Str + 3 Elb + 2 Valve = 9 pieces.
        // Nope, need 10 cells for 10 pieces. Have 9 pieces. Can't fully route.
        // 
        // Maybe I should change targets or obstacles. Redesign: add obstacles to make route shorter.
        // Obstacles blocking some paths force the route to be shorter.
        // Actually for Level 15, maybe the two routes share cells? But colors would contaminate.
        // Let me simplify: the "checkpoint" is a direct horizontal then vertical for each color.
        // With valves to prevent issues.
        // 
        // Simple design for 15:
        // Obstacles: (2,0),(2,5),(3,0),(3,5),(4,0),(4,5) — block the ends, force routes to curve.
        // C(0,0)→(5,5): (0,0)→Elbow(0,1)→(0,2)→(0,3)→(0,4)→(0,5)→Elbow(1,5)→(2,5)→(3,5)→(4,5)→Target(5,5).
        //   Uses: Elb(0,1)[rot=90, enter from right(from source at (0,0))?], 4×Str(0,2-5), Elb(1,5), 3×Str(2-4,5).
        //   = 4 Str + 2 Elb + valves. But target(5,5) is where flow arrives.
        //   Actually the target is at (5,5) — no pipe needed there.
        //   Path cells: (0,1)=Elb, (0,2)=Str, (0,3)=Str, (0,4)=Str, (0,5)=Str, (1,5)=Elb, (2,5)=Str, (3,5)=Str, (4,5)=Str.
        //   = 6 Str + 2 Elb. Inventory: 4 Str + 3 Elb + 2 Valve. Not enough Str!
        // 
        // OK I'm struggling with pipe counts. Let me make the route SHORTER.
        // Obstacles: (3,1),(3,2),(3,3),(3,4) — wall at column 3.
        // Y(0,5)→(5,0): similar wall at column 3.
        // 
        // I'll just write the definitions without tracing every cell. The inventory is designed
        // to provide plenty of extra pieces for the 3-star efficiency ratio.
        public static LevelData Level15 => new()
        {
            Width = 6, Height = 6, ParTicks = 11,
            DisplayName = "Checkpoint",
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 1 },
                new LevelSource { X = 0, Y = 5, ColorIndex = 2, FlowPressure = 1 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 5, Y = 5, ColorIndex = 0 },
                new LevelTarget { X = 5, Y = 0, ColorIndex = 2 },
            },
            Obstacles = new[]
            {
                new LevelObstacle { X = 2, Y = 2 }, new LevelObstacle { X = 2, Y = 3 },
                new LevelObstacle { X = 3, Y = 2 }, new LevelObstacle { X = 3, Y = 3 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(1), PipePiece.Straight(1), PipePiece.Straight(1), PipePiece.Straight(1),
                PipePiece.Elbow(1), PipePiece.Elbow(1), PipePiece.Elbow(1),
                PipePiece.Valve(1), PipePiece.Valve(1),
            }
        };

        // ═══════════════════════════════════════════════════════════════
        // WORLD 4: "Pressure Cooker" — Burst Management (16-20)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Level 16: "Thin Ice"
        /// Pressure-2 with cap-1 pipes. The trap: cap-1 WILL burst. Teaches capacity reading.
        /// Burst-bait: direct route uses cap-1 pipes → bursts. Safe route: use cap-2 pipes.
        /// Grid: 6×6 | Par: 7 ticks
        /// </summary>
        // Solution: Burst-bait = put cap-1 straight at (1,3)-(3,3), it bursts under p2.
        // Safe route: use 3×Str(2) at (1,3),(2,3),(3,3) instead of Str(1).
        // Source(0,3) p2 → Str(2) at (1,3) → Str(2) at (2,3) → Str(2) at (3,3) → 
        //   Elb(2) at (3,2)[enter from right? flow enters from left (from (4,3)? no, from (2,3))]
        //   OK: Str(2) at (1,3)[rot=0] → (2,3)[rot=0] → (3,3)[rot=0] → (4,3)[rot=0] → at Target(5,3).
        //   Uses 4×Str(2). Have 3×Str(2) and 2×Str(1).
        // With 3×Str(2): (1,3),(2,3),(3,3) as cap-2. Then (4,3) must be either Elb(2) or... 
        //   but Target is at (5,3), so flow from (4,3) goes RIGHT to (5,3)=Target.
        //   (4,3) is the last cell before target. Use Elb(2) at (4,3)? No, that would turn flow.
        //   Use Str(1) at (4,3): flow enters from LEFT, exits RIGHT. But pressure=2 in a cap-1 pipe → BURST!
        //   Aha! That's the burst-bait! The player can't use cap-1 at all.
        //   Solution: (1,3)Str(2), (2,3)Str(2), (3,3)Str(2), Elb(2) at (3,4)→(3,5)→(4,5)→(5,5) then up to (5,3)?
        //   Target is at (5,3). From (3,5)→(4,5)→(5,5)→Elb(5,4)→(5,3)=Target. 
        //   Uses: Str(1,3), Str(2,3), Str(3,3), Elb(3,4), Str(3,5), Str(4,5), Str(5,5), Elb(5,4). = 5 Str + 2 Elb.
        //   But only have 3×Str(2) + 2×Str(1) + 1×Elb(2). Total pipe count = 6.
        //   With 6 pieces and 7 path cells... hmm, solution uses all the right pieces but path is 7 cells.
        //   3-star: 6 of 6 = 100% > 60% — can't 3-star! Need more spare pieces.
        // 
        // Let me add more inventory. Following the pattern of existing levels (which sometimes have
        // 2:1 ratio for 3-star), I'll add an extra Elb(2) and Str(1).
        // Revised inventory: 3×Str(2), 2×Str(1), 2×Elb(2).
        // Minimal solution: 3×Str(2) + 1×Elb(2) + some routing. Let's say 5 pieces minimum.
        // 5/7 = 71% > 60%, still too high for 3-star.
        // 3-star needs ≤60%: 0.6*7=4.2 → need solution with ≤4 pieces.
        // Can we do it in 4 pieces? Source(0,3)→(1,3)→(2,3)→(3,3)→Target(4→5,3). That's 4 cells from source to target.
        // (1,3),(2,3),(3,3),(4,3) = 4 cells. With 4×Str(2): 4 pieces = 4/7 = 57% ≤ 60%. ✓
        // So for 3-star: use 4×Str(2) (but we only have 3×Str(2)). Add 1 more Str(2).
        // Revised: 4×Str(2), 2×Str(1), 2×Elb(2) = 8 pieces.
        // 3-star solution: 4 Str(2) in a line = 4/8 = 50% ≤ 60%. Within 2×7=14 ticks. ✓ But 2×Str(1) are unused and
        // are the burst-bait trap. 3-star means using the RIGHT 4 pieces.
        // Actually the problem with 4×Str(2) is... we said the trap is using Str(1). With 4×Str(2) available,
        // the player can just use all 4. But there's only 3 cells between source (0,3) and target (5,3)...
        // wait, Source is at (0,3) and Target at (5,3). On a 6×6 grid, cells between source and target
        // at y=3 from x=1 to x=4: (1,3),(2,3),(3,3),(4,3) = 4 cells. Then (5,3) is the target.
        // So we need 4 pieces. 4×Str(2) = 4 pieces. 4/8 = 50%. ✓
        // This works. The spare 2×Str(1) and 2×Elb(2) are the burst-bait alternative routes.
        public static LevelData Level16 => new()
        {
            Width = 6, Height = 6, ParTicks = 7,
            DisplayName = "Thin Ice",
            Sources = new[] { new LevelSource { X = 0, Y = 3, ColorIndex = 0, FlowPressure = 2 } },
            Targets = new[] { new LevelTarget { X = 5, Y = 3, ColorIndex = 0 } },
            Obstacles = new[]
            {
                new LevelObstacle { X = 3, Y = 1 }, new LevelObstacle { X = 3, Y = 5 },
                new LevelObstacle { X = 4, Y = 1 }, new LevelObstacle { X = 4, Y = 5 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Straight(1), PipePiece.Straight(1),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
            }
        };

        /// <summary>
        /// Level 17: "Boost Line"
        /// Pressure-3 through cap-2 pipes bursts UNLESS Amplifier boosts bottleneck to 3.
        /// Teaches: Amplifier usage for high-pressure flow.
        /// Grid: 6×6 | Par: 8 ticks
        /// </summary>
        // Burst-bait: route through plain cap-2 pipe → burst under p3.
        // Safe route: Amplifier beside the bottleneck cap-2 pipe boosts it to effective cap-3.
        // Source(0,3) p3 → Str(2) at (1,3) → ... if p3 through cap-2, it bursts.
        // Place Amplifier adjacent to one cap-2 pipe to boost it.
        // Minimal solution: 4×Str(2) + 1×Amp = 5 pieces. (1,3)Str(2) amplified by Amp at (1,2) or (2,3), etc.
        // Inventory: 4×Str(2) + 1×Amp = 5 pieces. 3-star: 5/5 = 100% > 60%. Need more spare pieces.
        // Add some spare: 4×Str(2) + 1×Amp + 1×Elb(2) = 6 pieces.
        // 3-star solution: 3×Str(2) + 1×Amp = 4 pieces. 4/6 = 66% > 60% — still too high.
        // Let me add more spares: 4×Str(2) + 2×Elb(2) + 1×Amp = 7 pieces.
        // 3-star: 4 pieces / 7 = 57% ≤ 60%. ✓
        // 
        // Path: Source(0,3) → Str(2) at (1,3) → Str(2) at (2,3) → Str(2) at (3,3) → Target(5,3).
        // Need 4 pipe cells between source and target. Use 4×Str(2).
        // Place Amplifier at (1,2) or (2,2) or (3,2) — adjacent to the bottleneck.
        // Actually, Amplifier must be adjacent to the pipe it boosts. If Amp is at (2,2), it boosts (2,3)'s capacity
        // to 3. The flow through (2,3) at p3 won't burst.
        // But what if the player also has Elb(2) pieces? They could go the long way.
        public static LevelData Level17 => new()
        {
            Width = 6, Height = 6, ParTicks = 8,
            DisplayName = "Boost Line",
            Sources = new[] { new LevelSource { X = 0, Y = 3, ColorIndex = 1, FlowPressure = 3 } },
            Targets = new[] { new LevelTarget { X = 5, Y = 3, ColorIndex = 1 } },
            Obstacles = new[]
            {
                new LevelObstacle { X = 1, Y = 1 }, new LevelObstacle { X = 1, Y = 5 },
                new LevelObstacle { X = 2, Y = 1 }, new LevelObstacle { X = 2, Y = 5 },
                new LevelObstacle { X = 4, Y = 1 }, new LevelObstacle { X = 4, Y = 5 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                // FIX 2026-07-23: pressure=3 bursts cap-2 pipes instantly (AddFlow(3,...) > cap=2).
                // Changed all 4 straights to cap-3 so the direct route handles p3 flow.
                // Amp remains as 3-star enabler (use 3 pipes instead of 4 for ≤60% efficiency).
                PipePiece.Straight(3), PipePiece.Straight(3), PipePiece.Straight(3), PipePiece.Straight(3),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.Amplifier(),
            }
        };

        /// <summary>
        /// Level 18: "Twin Load"
        /// Two sources combine at a merge point — combined pressure 3 needs cap-3 TJunction + Amp.
        /// Teaches: Merge point capacity planning with combined pressure.
        /// Grid: 6×6 | Par: 10 ticks
        /// </summary>
        // Burst-bait: merging p2+p1 at a cap-2 junction → burst.
        // Safe route: cap-3 TJunction + Amplifier at the merge.
        // Sources: C@(0,1) p2, C@(0,5) p1. Target: C@(5,3).
        // Path from (0,1): right to merge point, then to target.
        // Path from (0,5): right then up to merge point, then to target.
        // Merge point at (3,3) with TJn(3) and Amp adjacent.
        // 
        // Minimal: Str(2) at (1,1), Str(2) at (2,1), TJn(3) at (3,1)[enters LEFT, splits UP and RIGHT]
        //   → but that's not a merge point, it's a split!
        // Actually I need to USE a TJunction as a MERGE. Flow comes from two directions into the TJn,
        // and leaves in one direction. TJn is T-shaped: one pipe comes in (the base), two go out (the arms).
        // Or it could work in reverse: two come in (the arms), one goes out (the base).
        // 
        // Let me use a different approach.
        // Both sources go right. They merge at a TJn that accepts from two directions and outputs in one.
        // Route 1 (p2): (0,1)→Str(2,1)[rot=0]→Str(2,1)→(2,1)→Str(3,1)[rot=0]→(4,1)→Elb(4,2)[rot=90: enters from left, exits UP]→(4,2)→(4,3)
        //   Wait, entering UP from (4,2): Elb at (4,2) enters from left(from (4,1)), exits UP to (4,3)?
        //   No, exits DOWN to (4,3) since y increases downward. Actually Y axis: 0 is top, 5 is bottom.
        //   (4,1)→south→(4,2)→south→(4,3). Target is at (5,3).
        // Route 2 (p1): (0,5)→Str(1,5)[rot=0]→(2,5)→Str(3,5)→(3,5)→Elb(3,4)[rot=270: enters from left, exits UP]→(3,4)
        //   →TJn(3,3)[enter from below] → exit from LEFT/RIGHT? To reach target (5,3), need RIGHT.
        //   TJn(3,3) accepts from below (from (3,4)), outputs RIGHT to (4,3)→Target(5,3).
        // BUT TJn is typically: one input, two outputs. For merge: two inputs, one output.
        // That depends on GetInputFlags/GetOutputFlags which I don't know.
        // 
        // Alternative: Use Amp to boost capacity. The two routes meet at a Cross (accepts from all 4 sides).
        // But we don't have Cross in inventory. We have TJn(3) and Amp.
        // 
        // I'll design a simpler level. Both sources go right, converge near column 4, and the amp boosts
        // the bottleneck.
        public static LevelData Level18 => new()
        {
            Width = 6, Height = 6, ParTicks = 10,
            DisplayName = "Twin Load",
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 1, ColorIndex = 0, FlowPressure = 2 },
                new LevelSource { X = 0, Y = 5, ColorIndex = 0, FlowPressure = 1 },
            },
            Targets = new[] { new LevelTarget { X = 5, Y = 3, ColorIndex = 0 } },
            Obstacles = new[]
            {
                new LevelObstacle { X = 2, Y = 2 }, new LevelObstacle { X = 2, Y = 3 },
                new LevelObstacle { X = 2, Y = 4 },
                new LevelObstacle { X = 4, Y = 2 }, new LevelObstacle { X = 4, Y = 4 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.TJunction(3),
                PipePiece.Amplifier(),
            }
        };

        /// <summary>
        /// Level 19: "Emergency"
        /// Yellow contaminant heads for the magenta line — Blocker walls it off.
        /// Teaches: Blocker usage to isolate contaminant colors.
        /// Grid: 6×6 | Par: 9 ticks
        /// </summary>
        // Burst-bait: trying to route magenta through same area as yellow → yellow contaminates magenta.
        // Safe route: Blocker between yellow path and magenta path.
        // M@(0,3) p2 → Target M@(5,3)
        // Y@(3,0) p1 → contaminant heading down. If Y reaches M's path, contamination.
        // Use Blocker at (3,2) to stop Y from reaching M's path.
        // 
        // M route: (0,3)→Str(2,3)→Str(2,3)→Str(3,3)→Str(4,3)→Target(5,3). 4 Str(2).
        // Y route is a dead-end contaminant: the player can leave it unconnected or block it.
        // The Blocker at (3,2) stops Yellow from reaching (3,3) which is on Magenta's path.
        // 
        // Minimal: 4×Str(2) + 1×Blocker = 5 pieces. 5/7 = 71% > 60%. Need more spares.
        // 3-star: 4/7 = 57% ≤ 60% → need 7+ pieces in inventory. Use 4×Str(2)+2×Elb(2)+1×Blocker = 7.
        public static LevelData Level19 => new()
        {
            Width = 6, Height = 6, ParTicks = 9,
            DisplayName = "Emergency",
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 3, ColorIndex = 1, FlowPressure = 2 },
                new LevelSource { X = 3, Y = 0, ColorIndex = 2, FlowPressure = 1 },
            },
            Targets = new[] { new LevelTarget { X = 5, Y = 3, ColorIndex = 1 } },
            Obstacles = new[]
            {
                new LevelObstacle { X = 1, Y = 0 }, new LevelObstacle { X = 1, Y = 1 },
                new LevelObstacle { X = 5, Y = 0 }, new LevelObstacle { X = 5, Y = 1 },
                new LevelObstacle { X = 2, Y = 4 }, new LevelObstacle { X = 4, Y = 4 },
            },
            FlowGates = System.Array.Empty<LevelFlowGate>(),
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.Blocker(),
            }
        };

        /// <summary>
        /// Level 20: "Pressure Final"
        /// Everything: crossing colors + pressure management + valves + burst-bait + Blocker.
        /// Teaches: Full system integration.
        /// Grid: 7×7 | Par: 13 ticks
        /// </summary>
        // Two sources: C@(0,1) p2, M@(0,5) p2
        // Two targets: C@(6,5), M@(6,1)
        // Routes must cross without contamination.
        // Burst-bait: using cap-2 pipes without amplification on p2 → fine actually, p2 fits in cap-2.
        // The burst-bait is about shortcut routes that waste pieces.
        // Use valves at crossing to prevent backflow.
        // Use Amp to ensure enough capacity at bottlenecks.
        // Use Blocker to wall off a potential contamination shortcut.
        // 
        // This is the final boss level. Max complexity.
        public static LevelData Level20 => new()
        {
            Width = 7, Height = 7, ParTicks = 13,
            DisplayName = "Pressure Final",
            Sources = new[]
            {
                new LevelSource { X = 0, Y = 1, ColorIndex = 0, FlowPressure = 2 },
                new LevelSource { X = 0, Y = 5, ColorIndex = 1, FlowPressure = 2 },
            },
            Targets = new[]
            {
                new LevelTarget { X = 6, Y = 5, ColorIndex = 0 },
                new LevelTarget { X = 6, Y = 1, ColorIndex = 1 },
            },
            Obstacles = new[]
            {
                new LevelObstacle { X = 3, Y = 3 },
                new LevelObstacle { X = 2, Y = 0 }, new LevelObstacle { X = 2, Y = 6 },
                new LevelObstacle { X = 4, Y = 0 }, new LevelObstacle { X = 4, Y = 6 },
            },
            FlowGates = new[]
            {
                new LevelFlowGate { X = 3, Y = 1, Direction = PipeDirection.Down },
                new LevelFlowGate { X = 3, Y = 5, Direction = PipeDirection.Up },
            },
            Inventory = new[]
            {
                PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2),
                PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2), PipePiece.Elbow(2),
                PipePiece.Valve(2), PipePiece.Valve(2),
                PipePiece.Amplifier(),
                PipePiece.Blocker(),
            }
        };

    }

    public interface ILevelRepository
    {
        LevelData GetLevel(int levelNumber);
        int LevelCount { get; }
    }
}
