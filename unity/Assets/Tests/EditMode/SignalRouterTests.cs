using System;
using System.Collections.Generic;
using NUnit.Framework;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;

namespace ChromaVale.Tests
{
    /// <summary>
    /// Edit-mode tests for SignalRouter — the crown jewel of the regression suite.
    /// Tests shape-aware flow propagation, capacity/burst, valve direction,
    /// elbow rotation, T-Junction splitting, mixer cell color mixing,
    /// amplifier boost, and blocker halting.
    ///
    /// IMPORTANT: These tests PIN THE ENGINE'S CURRENT BEHAVIOR.
    /// If a refactor changes the behavior, the test fails — forcing a conscious decision.
    /// If an expectation doesn't match the actual engine, we prefer to ADJUST THE TEST
    /// (with a comment explaining the actual behavior) rather than change the engine.
    ///
    /// C# 9.0 compatible (no file-scoped namespaces).
    /// </summary>
    public class SignalRouterTests
    {
        /// <summary>
        /// Helper: run a flow simulation to completion or maxTicks.
        /// Builds GridBoard from LevelData, creates TraceInventory from level.Inventory,
        /// places pieces via TryPlace (passing SignalRouter for shape/capacity registration),
        /// calls StartSimulation, then loops Tick() up to maxTicks.
        /// Returns the final SimulationResult.
        /// </summary>
        private SimulationResult RunToCompletion(
            LevelData level,
            Action<TraceInventory, GridBoard, SignalRouter> placementAction,
            int maxTicks = 100)
        {
            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();

            // Track burst events
            var burstEvents = new List<(int x, int y)>();
            simulator.OnTraceShort += (x, y) => burstEvents.Add((x, y));

            // Place pieces via the caller's placement logic
            placementAction(inventory, board, simulator);

            simulator.StartSimulation(board, level, inventory);

            for (int i = 0; i < maxTicks; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            return simulator.GetResult();
        }

        // ────────────────────────────────────────────────────────────────
        // 1. WIN — Level 1 with straights
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Level1_StraightPath_AllTargetsReached()
        {
            var level = LevelData.Level1;
            // Level 1 v3: Source(0,2,C) → Target(4,2,C). Place straights at (1,2),(2,2),(3,2) rot=0 (horizontal).
            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 2, sim, 0);
                inv.TryPlace(1, board, 2, 2, sim, 0);
                inv.TryPlace(2, board, 3, 2, sim, 0);
            });

            Assert.AreEqual(SimulationResult.AllTargetsReached, result);
        }

        [Test]
        public void Level1_StraightPath_FinishesWithinParTicks()
        {
            // Level 1 v3: Source(0,2,C) → straights at (1,2),(2,2),(3,2) → Target(4,2,C)
            // After StartSimulation, wave is at (1,2).
            // Tick 1: (1,2)→(2,2). Tick 2: (2,2)→(3,2). Tick 3: (3,2)→(4,2)=Target.
            var level = LevelData.Level1;
            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();

            inventory.TryPlace(0, board, 1, 2, simulator, 0);
            inventory.TryPlace(1, board, 2, 2, simulator, 0);
            inventory.TryPlace(2, board, 3, 2, simulator, 0);

            simulator.StartSimulation(board, level, inventory);

            // Run to completion
            int tickCount = 0;
            while (simulator.IsRunning && tickCount < 100)
            {
                simulator.Tick();
                tickCount++;
            }

            Assert.AreEqual(SimulationResult.AllTargetsReached, simulator.GetResult());
            // 3 ticks + 0 for initial placement. ParTicks is 6. +1 margin.
            Assert.That(tickCount, Is.LessThanOrEqualTo(level.ParTicks + 1));
        }

        // ────────────────────────────────────────────────────────────────
        // 2. LOSE — disconnected pipe => SignalStuck
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void DisconnectedPipe_SignalStuck()
        {
            var level = LevelData.Level1;
            // Place only one straight — flow reaches it but can't reach the target
            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 1, sim, 0);
                // (2,1) is empty — flow stops at (1,1)
            });

            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 3. BURST — cap-1 pipe under pressure-2 source
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Cap1Pipe_Pressure2_Bursts()
        {
            // Custom level: Source(0,0,cyan,pressure=2) → Straight(cap1,1,0) → Target(2,0,cyan)
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 2 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Straight(1) },
            };

            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();
            var burstCells = new List<(int x, int y)>();
            simulator.OnTraceShort += (x, y) => burstCells.Add((x, y));

            inventory.TryPlace(0, board, 1, 0, simulator, 0);

            simulator.StartSimulation(board, level, inventory);
            for (int i = 0; i < 10; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            // Source pressure 2 emits 2 units into (1,0) with cap 1 → burst
            Assert.AreEqual(1, burstCells.Count);
            Assert.AreEqual((1, 0), burstCells[0]);
            // Cell should be in Burst state
            var cellState = simulator.GetCellState(1, 0);
            Assert.AreEqual(CircuitState.Shorted, cellState.State);
        }

        // ────────────────────────────────────────────────────────────────
        // 4. NO BURST — same layout with cap-2
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Cap2Pipe_Pressure2_NoBurst()
        {
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 2 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Straight(2) },
            };

            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();
            var burstCells = new List<(int x, int y)>();
            simulator.OnTraceShort += (x, y) => burstCells.Add((x, y));

            inventory.TryPlace(0, board, 1, 0, simulator, 0);

            simulator.StartSimulation(board, level, inventory);
            for (int i = 0; i < 10; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            // Capacity 2 handles pressure 2 — no burst, flow reaches target
            Assert.AreEqual(0, burstCells.Count);
            Assert.AreEqual(SimulationResult.AllTargetsReached, simulator.GetResult());
        }

        // ────────────────────────────────────────────────────────────────
        // 5. VALVE — forward flow passes
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Valve_ForwardFlow_Passes()
        {
            // Source at (2,0) emits Left into Valve(Left,1,0) → flow passes
            // Board 3×1: Src(2,0,C) → Valve(Left,1,0) → Tgt(0,0,C)
            // Valve direction=Left: Input=Right (Opposite of Left), Output=Left
            // Source at (2,0) emits Left (dx=-1) to (1,0). 
            // CanEnterCell(1,0, Right): GetInputFlags(Valve,Left) = Opposite(Left)=Right. RightFlag→OK.
            // On next tick, CanExitCell(1,0, Left): GetOutputFlags(Valve,Left)=LeftFlag→OK.
            // Signal goes to (0,0) target.
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 2, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 0, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Diode(2, TraceDirection.Left) },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.AllTargetsReached, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 6. VALVE REVERSE — flow entering against direction is blocked
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Valve_ReverseFlow_Blocked()
        {
            // Source at (0,0) emits Right into Valve(Right,1,0)
            // Valve direction=Right: Input=Left(Opposite of Right), Output=Right
            // Source at (0,0) emits Right (dx=1) to (1,0).
            // CanEnterCell(1,0, Left): GetInputFlags(Valve,Right) = Opposite(Right)=Left. LeftFlag→OK.
            // So it DOES enter.
            // Hmm, the test case from TraceSegment.cs says:
            // "Board: 3×1, Src(0,0) → Valve(Right,1,0) → Tgt(2,0)."
            // "GetInputFlags(Valve, Right) → DirectionToFlag(Opposite(Right)) = LeftFlag"
            // "Flow entering from Right (dx=1)? DirectionToFlag(Right) & LeftFlag → 0 → blocked."
            // Wait, the source at (0,0) emits Right (dx=1). The neighbor (1,0) is entered from 
            // Left (dx=-1). No wait, let me think about this more carefully.
            // 
            // From Tick(): exitDir = DirectionFromDelta(dx, dy). For dx=1, dy=0 → Right.
            // neighborEntryDir = OppositeDirection(Right) = Left.
            // CanEnterCell(1,0, Left): GetInputFlags(Valve, Right) → DirectionToFlag(Opposite(Right)) = LeftFlag.
            // DirectionToFlag(Left) = 4 (LeftFlag). (LeftFlag & LeftFlag) != 0 → YES, allowed!
            //
            // Wait, that means it DOES pass. The comment in TraceSegment.cs says the REVERSE
            // case is Src(0,0) → Valve(Right) → Tgt(2,0) and it says flow entering from 
            // Right (dx=1) is blocked. But DirectionFromDelta(1,0) = Right, and 
            // OppositeDirection(Right) = Left. So CanEnterCell checks Left flag.
            // 
            // GetInputFlags(Valve, Right) = DirectionToFlag(Opposite(Right)) = LeftFlag.
            // DirectionToFlag(Left) = LeftFlag.
            // (LeftFlag & LeftFlag) != 0 → true → ALLOWED!
            //
            // So actually Src(0,0) → Valve(Right,1,0) DOES allow flow through.
            // The flow enters the valve, and then CanExitCell(1,0, Left):
            // GetOutputFlags(Valve, Right) = DirectionToFlag(Right) = RightFlag.
            // DirectionToFlag(Left) = LeftFlag. (LeftFlag & RightFlag) = 0 → BLOCKED!
            //
            // So flow enters the valve but can't exit toward the target at (0,0) because
            // output is Right and target is to the Left. The flow is trapped in the valve.
            // Result: SignalStuck.
            //
            // For true REVERSE (flow entering from the output side):
            // Src(2,0) emitting Left into Valve(Right,1,0). 
            // Source at (2,0) emits Left (dx=-1). neighborEntryDir = Opposite(Left) = Right.
            // CanEnterCell(1,0, Right): GetInputFlags(Valve,Right)=LeftFlag. 
            // DirectionToFlag(Right)=RightFlag. (RightFlag & LeftFlag)=0 → BLOCKED!
            //
            // The reverse test should be: Source to the RIGHT of the valve, flowing LEFT
            // into a Valve that points RIGHT.

            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 2, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 0, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Diode(2, TraceDirection.Right) },
            };

            // Source at (2,0) emits Left toward valve at (1,0) which outputs Right.
            // CanEnterCell(1,0, Right): Valve(Right) Input=LeftFlag. Right not in Left → BLOCKED.
            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 7. ELBOW ROTATION changes reachability
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Elbow_Rotation0_ReachesTargetToTheRight()
        {
            // Source(0,0,cyan) → Elbow(1,0,rot=0) → Target(2,0,cyan)
            // Elbow rot=0: Input=Up|Left, Output=Down|Right
            // Enter from Left (from source at 0,0), exit Right → Target at (2,0)
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Corner(2) },
            };

            // Rot=0: Input=Up|Left, enter from Left ✓. Output=Down|Right, exit Right ✓.
            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0); // rot=0
            });

            Assert.AreEqual(SimulationResult.AllTargetsReached, result);
        }

        [Test]
        public void Elbow_Rotation90_BlocksHorizontalFlow_Blocked()
        {
            // Same board as above but elbow at rot=90.
            // Elbow rot=90: Input=Right|Up, Output=Left|Down
            // Enter from Left: DirectionToFlag(Left) & (Right|Up) = LeftFlag & (RightFlag|UpFlag) = 0 → BLOCKED
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Corner(2) },
            };

            // Rot=90: Input=Right|Up, enter from Left → Left not in [Right,Up] → BLOCKED
            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 90);
            });

            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 8. TJUNCTION — split to two targets
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void TJunction_SplitsFlowToTwoTargets()
        {
            // Board:
            //   S(0,0,C) → TJ(1,0) → S(2,0) → S(3,0) → T(4,0,C)  [Right branch]
            //                ↓
            //              S(1,1) → T(1,2,C)                     [Down branch]
            //
            // TJ rot=0: Input=Left|Right|Up, Output=Left|Right|Down
            // Enter from Left (from source). Output Right→(2,0), Down→(1,1).
            var level = new LevelData
            {
                Width = 5,
                Height = 3,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[]
                {
                    new LevelTarget { X = 4, Y = 0, ColorIndex = 0 },
                    new LevelTarget { X = 1, Y = 2, ColorIndex = 0 },
                },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[]
                {
                    TraceSegment.Splitter(2), // index 0
                    TraceSegment.Straight(2), // index 1
                    TraceSegment.Straight(2), // index 2
                    TraceSegment.Straight(2), // index 3 — (1,1) going down
                },
            };

            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();
            var targetsReached = new List<(int x, int y)>();
            simulator.OnTargetReached += (x, y, c) => targetsReached.Add((x, y));

            inventory.TryPlace(0, board, 1, 0, simulator, 180); // TJ at (1,0), rot=180 — see note below
            inventory.TryPlace(1, board, 2, 0, simulator, 0); // Straight at (2,0) → Right
            inventory.TryPlace(2, board, 3, 0, simulator, 0); // Straight at (3,0) → Right
            inventory.TryPlace(3, board, 1, 1, simulator, 90); // Straight rot=90: Up|Down at (1,1)

            simulator.StartSimulation(board, level, inventory);

            for (int i = 0; i < 20; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            Assert.AreEqual(SimulationResult.AllTargetsReached, simulator.GetResult());

            // PINNED ENGINE BEHAVIOR (verified via console harness 2026-07-23):
            // Grid +y is engine "Up". The side target (1,2) sits at +y of the TJn,
            // so the branch must exit Up. TJn rot=0 outputs Left|Right|DOWN (−y) —
            // wrong side. TJn rot=180 outputs Left|Right|UP and accepts entry from
            // Left, which routes both branches: Right→(2,0)→(3,0)→tgt(4,0), and
            // Up(+y)→(1,1)→tgt(1,2).
            Assert.Contains((4, 0), targetsReached);
            Assert.Contains((1, 2), targetsReached);
        }

        // ────────────────────────────────────────────────────────────────
        // 9. MIXER — color mixing at mixer cell
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void MixerCell_MixesCyanAndMagentaToPurple_ReachesPurpleTarget()
        {
            // Two sources feed into a shared Mixer cell.
            // Cyan from (0,0) → (1,0)[Mixer]
            // Magenta from (0,2) → (1,2)[Mixer] ... wait, they need to share the SAME cell.
            // Actually, both sources should feed into the SAME mixer cell from different directions.
            // 
            // Board:
            //   S(0,0,C) → (1,0)[Mixer] → (2,0) → T(3,0,6)  [Purple target]
            //   S(0,1,M) → (1,0)[Mixer] (enters from Up)
            // 
            // Wait, the sources are at (0,0) and (0,1). (0,0) flows Right to (1,0).
            // (0,1) flows Right to (1,1)... not (1,0).
            //
            // Better layout:
            //   S(0,0,C) → (1,0)[Mixer] ← S(2,0,M)
            //                ↓
            //              (1,1)[Straight rot=90] → T(1,2,6) [Purple]
            //
            // Hmm, Elbow rot=0: Input=Up|Left, Output=Down|Right  
            // Mixer: Input=AllFlags, Output=AllFlags
            //
            // Source(0,0,C) emits Right→(1,0) Mixer. MixedColorA=0.
            // Source(2,0,M) emits Left→(1,0) Mixer. MixedColorB=1. Result=6(Purple).
            // From (1,0) Mixer: Output=AllFlags. Exit Down→(1,1).
            // (1,1)=Straight rot=90: Input=Up|Down, Output=Up|Down. Enter Up✓. Exit Down→(1,2)=T(6).
            // Target color 6 matches! ✓

            var level = new LevelData
            {
                Width = 3,
                Height = 3,
                Sources = new[]
                {
                    new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 }, // Cyan
                    new LevelSource { X = 2, Y = 0, ColorIndex = 1, SignalStrength = 1 }, // Magenta
                },
                Targets = new[] { new LevelTarget { X = 1, Y = 2, ColorIndex = 6 } }, // Purple
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[]
                {
                    TraceSegment.Combiner(),      // index 0: at (1,0) — capacity 0
                    TraceSegment.Straight(2),  // index 1: at (1,1) — rot=90 (Up|Down)
                },
            };

            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();
            var mixEvents = new List<(int x, int y, int a, int b)>();
            simulator.OnColorMix += (x, y, a, b) => mixEvents.Add((x, y, a, b));

            inventory.TryPlace(0, board, 1, 0, simulator, 0); // Mixer
            inventory.TryPlace(1, board, 1, 1, simulator, 90); // Straight rot=90

            simulator.StartSimulation(board, level, inventory);

            for (int i = 0; i < 20; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            Assert.AreEqual(SimulationResult.AllTargetsReached, simulator.GetResult());

            // Check mix event fired at (1,0) with colors 0 and 1
            bool mixAtMixer = mixEvents.Exists(e => e.x == 1 && e.y == 0 && e.a == 0 && e.b == 1);
            Assert.IsTrue(mixAtMixer, "Color mix (Cyan+Magenta) should fire at mixer cell (1,0)");
        }

        // ────────────────────────────────────────────────────────────────
        // 10. AMPLIFIER — boosts adjacent capacity to survive pressure 3
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Amplifier_BoostsAdjacentCell_ToSurvivePressure3()
        {
            // Source pressure 3 → Straight(cap2) amplified by adjacent Amplifier → Target
            // Layout:
            //   S(0,0,C,p=3) → (1,0)[Straight cap2] → T(2,0,C)
            //   Amp(1,1) adjacent to (1,0) boosts its capacity from 2 to 3
            //
            // Without amplifier: cap2 pipe receives 3 flow units → bursts
            // With amplifier: ApplyAmplifierBoost at (1,1) → (1,0).Capacity += 1 → 3
            // Pipe capacity becomes 3, handles pressure 3 → stable
            var level = new LevelData
            {
                Width = 3,
                Height = 2,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 3 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[]
                {
                    TraceSegment.Straight(2), // index 0: at (1,0) — cap 2
                    TraceSegment.Repeater(), // index 1: at (1,1) — boosts (1,0)
                },
            };

            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();
            var burstCells = new List<(int x, int y)>();
            simulator.OnTraceShort += (x, y) => burstCells.Add((x, y));

            // Place amplifier first so its boost is registered before the pipe
            // Actually, SetPipeShape on amplifier calls ApplyAmplifierBoost at placement time.
            // And StartSimulation also calls ApplyAmplifierBoost for existing amps.
            // So order shouldn't matter, but let's place amp first.
            inventory.TryPlace(1, board, 1, 1, simulator, 0); // Amplifier at (1,1)
            inventory.TryPlace(0, board, 1, 0, simulator, 0); // Straight cap2 at (1,0)

            // Verify the amplifier set capacity boost
            // ApplyAmplifierBoost iterates 4 directions; (1,0) is Up from (1,1)
            // After boost: _pipeCapacityMap[(1,0)] should be 2+1 = 3
            // But it might have been SetPipeCapacity before boost...
            // Actually TryPlace sets capacity to piece.Capacity (2), then SetPipeShape
            // calls ApplyAmplifierBoost which increments by 1.
            // Then StartSimulation re-reads from inventory and calls ApplyAmplifierBoost again.
            // So capacity after StartSimulation: 2 (from TryPlace) + 1 (from SetPipeShape) = 3
            // Then start sim calls ApplyAmplifierBoost again: 3 + 1 = 4?

            // Hmm, actually StartSimulation creates a fresh _pipeCapacityMap.
            // Let me trace:
            // 1. TryPlace → SetPipeCapacity(1,0, 2) → _pipeCapacityMap[(1,0)] = 2
            // 2. TryPlace → SetPipeShape(1,0, Straight, None, 0) → no amp adj since shape=Straight
            // 3. TryPlace → SetPipeShape(1,1, Amplifier, None, 0) → ApplyAmplifierBoost →
            //    increments _pipeCapacityMap[(1,0)] from 2 to 3.
            // 4. StartSimulation: creates new _pipeCapacityMap, iterates board,
            //    reads piece from inventory: piece.Capacity=2 → _pipeCapacityMap[(1,0)]=2
            //    then iterates again for shapes: shape=Amplifier→ApplyAmplifierBoost
            //    → _pipeCapacityMap[(1,0)] from 2 to 3.

            // So final capacity is 3. Good. Pressure 3 → 3 units of flow → stable.

            simulator.StartSimulation(board, level, inventory);

            for (int i = 0; i < 20; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            Assert.AreEqual(0, burstCells.Count,
                "Amplifier should boost cap-2 pipe to cap-3, surviving pressure-3 flow");
            Assert.AreEqual(SimulationResult.AllTargetsReached, simulator.GetResult());
        }

        // ────────────────────────────────────────────────────────────────
        // 11. BLOCKER — flow halts at blocker piece
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Blocker_HaltsFlow_BlocksTarget()
        {
            // Source(0,0,C) → (1,0)[Blocker] → Target(2,0,C)
            // Blocker: Input=0, Output=0 → no flow can enter or exit
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Breaker() },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 12. STRAIGHT ROTATION — rot-90 allows vertical but blocks horizontal
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Straight_Rotation90_AllowsVerticalFlow_BlocksHorizontal()
        {
            // Source(0,0,C) → Straight(1,0,rot=90) → Target(2,0,C)
            // Straight rot=90: Input=Up|Down, Output=Up|Down
            // Enter from Left → Left not in [Up,Down] → BLOCKED
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Straight(2) },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 90);
            });

            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 13. GHOST TRACE — signal propagates through pre-existing ghost trace
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void GhostTrace_PropagatesSignalToTarget()
        {
            // Source(0,0,C) → GhostTrace Straight at (1,0) → Target(2,0,C)
            // Ghost trace is pre-existing on the board — player doesn't place it.
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                GhostTraces = new[]
                {
                    new GhostTrace { X = 1, Y = 0, Shape = SegmentShape.Straight, Rotation = 0, Capacity = 2 }
                },
                ImpedanceCells = System.Array.Empty<ImpedanceCell>(),
                Inventory = System.Array.Empty<TraceSegment>(),
            };

            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();

            simulator.StartSimulation(board, level, inventory);

            for (int i = 0; i < 10; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            Assert.AreEqual(SimulationResult.AllTargetsReached, simulator.GetResult());
        }

        // ────────────────────────────────────────────────────────────────
        // 14. IMPEDANCE — signal weakens and dies when strength hits 0
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Impedance_SignalDiesWhenStrengthExhausted()
        {
            // Source(0,0,C) SignalStrength=2 → Straight at (1,0) → GhostStraight+Impedance(2,0,cost=2) → Target(3,0,C)
            // Signal strength 2, impedance cost 2 → dies at (2,0)
            var level = new LevelData
            {
                Width = 4,
                Height = 1,
                SignalStrength = 2,  // Explicit: signal dies with strength 2 against cost 2
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 3, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                GhostTraces = new[]
                {
                    new GhostTrace { X = 2, Y = 0, Shape = SegmentShape.Straight, Rotation = 0, Capacity = 2 }
                },
                ImpedanceCells = new[]
                {
                    new ImpedanceCell { X = 2, Y = 0, ResistanceCost = 2 }
                },
                Inventory = new[] { TraceSegment.Straight(2) },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        [Test]
        public void Impedance_SignalSurvivesWithEnoughStrength()
        {
            // Source(0,0,C) SignalStrength=3 → Straight at (1,0) → GhostStraight+Impedance(2,0,cost=2) → Target(3,0,C)
            // Signal strength 3, impedance cost 2 → survives with 1
            var level = new LevelData
            {
                Width = 4,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 3, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                GhostTraces = new[]
                {
                    new GhostTrace { X = 2, Y = 0, Shape = SegmentShape.Straight, Rotation = 0, Capacity = 2 }
                },
                ImpedanceCells = new[]
                {
                    new ImpedanceCell { X = 2, Y = 0, ResistanceCost = 2 }
                },
                Inventory = new[] { TraceSegment.Straight(2) },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.AllTargetsReached, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 15. TIMING WINDOW — target rejects signal outside accept window
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void TimingWindow_SignalTooEarly_Rejected()
        {
            // Source(0,0,C) → Target(1,0,C) with AcceptWindow(3, 5)
            // Signal arrives at tick 1 (directly adjacent to source) → too early → rejected
            // NOTE: EmitFromSource fires in StartSimulation before CurrentTick is incremented.
            // However, ReachTarget is called from Tick (not from EmitFromSource) for target cells
            // that are not adjacent to the source. For adjacent targets, EmitFromSource calls
            // ReachTarget directly. CurrentTick is 0 at that point.
            // So we need the target NOT adjacent so it goes through Tick path.
            // Source(0,0,C) → Straight(1,0) → Target(2,0,C) with AcceptWindow(3,5)
            // Tick 1: wave at (1,0). Tick 2: wave reaches (2,0) = Target. CurrentTick=2 < 3 → rejected.
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0, AcceptWindow = new AcceptWindow { MinTick = 3, MaxTick = 5 } } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Straight(2) },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
            });

            // Signal arrives too early → rejected → target not reached → SignalStuck
            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        [Test]
        public void TimingWindow_SignalWithinWindow_Accepted()
        {
            // Source(0,0,C) → Straight(1,0) → Target(2,0,C) with AcceptWindow(1, 5)
            // Tick 1: wave at (1,0). Tick 2: wave reaches target. CurrentTick=2, window [1,5] → accepted!
            var level = new LevelData
            {
                Width = 3,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0, AcceptWindow = new AcceptWindow { MinTick = 1, MaxTick = 5 } } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[] { TraceSegment.Straight(2) },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.AllTargetsReached, result);
        }

        [Test]
        public void TimingWindow_SignalTooLate_Rejected()
        {
            // Source(0,0,C) → long path of 6 straights → Target(7,0,C) with AcceptWindow(1, 2)
            // Signal takes 7 ticks to reach target. MaxTick=2 → too late → rejected.
            var level = new LevelData
            {
                Width = 8,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 7, Y = 0, ColorIndex = 0, AcceptWindow = new AcceptWindow { MinTick = 1, MaxTick = 2 } } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[]
                {
                    TraceSegment.Straight(2), TraceSegment.Straight(2), TraceSegment.Straight(2),
                    TraceSegment.Straight(2), TraceSegment.Straight(2), TraceSegment.Straight(2),
                },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
                inv.TryPlace(1, board, 2, 0, sim, 0);
                inv.TryPlace(2, board, 3, 0, sim, 0);
                inv.TryPlace(3, board, 4, 0, sim, 0);
                inv.TryPlace(4, board, 5, 0, sim, 0);
                inv.TryPlace(5, board, 6, 0, sim, 0);
            });

            // Signal takes 7 ticks but window closes at tick 2 → rejected → SignalStuck
            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        [Test]
        public void TimingWindow_NullWindow_AlwaysAccepts()
        {
            // Same as above but no AcceptWindow → always accepts
            var level = new LevelData
            {
                Width = 8,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 7, Y = 0, ColorIndex = 0, AcceptWindow = null } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                Inventory = new[]
                {
                    TraceSegment.Straight(2), TraceSegment.Straight(2), TraceSegment.Straight(2),
                    TraceSegment.Straight(2), TraceSegment.Straight(2), TraceSegment.Straight(2),
                },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
                inv.TryPlace(1, board, 2, 0, sim, 0);
                inv.TryPlace(2, board, 3, 0, sim, 0);
                inv.TryPlace(3, board, 4, 0, sim, 0);
                inv.TryPlace(4, board, 5, 0, sim, 0);
                inv.TryPlace(5, board, 6, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.AllTargetsReached, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 16. REPEATER — restores signal strength
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Repeater_RestoresSignalStrength_AfterImpedance()
        {
            // Source(0,0,C,p=3) → Straight(1,0) → Ghost+Imp(2,0,cost=2) → Repeater(3,0) → Ghost+Imp(4,0,cost=2) → Straight(5,0) → Target(6,0,C)
            // Without Repeater: 3-2-2 = -1 → dies at (4,0)
            // With Repeater: 3-2=1 at (2,0) → restored to 3 at (3,0) → 3-2=1 at (4,0) → survives → reaches target
            var level = new LevelData
            {
                Width = 7,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 6, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                GhostTraces = new[]
                {
                    new GhostTrace { X = 2, Y = 0, Shape = SegmentShape.Straight, Rotation = 0, Capacity = 2 },
                    new GhostTrace { X = 4, Y = 0, Shape = SegmentShape.Straight, Rotation = 0, Capacity = 2 },
                },
                ImpedanceCells = new[]
                {
                    new ImpedanceCell { X = 2, Y = 0, ResistanceCost = 2 },
                    new ImpedanceCell { X = 4, Y = 0, ResistanceCost = 2 },
                },
                Inventory = new[]
                {
                    TraceSegment.Straight(2),  // (1,0)
                    TraceSegment.Repeater(),   // (3,0)
                    TraceSegment.Straight(2),  // (5,0)
                },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
                inv.TryPlace(1, board, 3, 0, sim, 0);
                inv.TryPlace(2, board, 5, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.AllTargetsReached, result);
        }

        [Test]
        public void Repeater_WithoutRepeater_SignalDies()
        {
            // Same layout as above but Straight at (3,0) instead of Repeater
            // Signal: 3-2=1 at (2,0) → 1 at (3,0) → 1-2=-1 at (4,0) → DEAD
            var level = new LevelData
            {
                Width = 7,
                Height = 1,
                Sources = new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, SignalStrength = 1 } },
                Targets = new[] { new LevelTarget { X = 6, Y = 0, ColorIndex = 0 } },
                Obstacles = System.Array.Empty<LevelObstacle>(),
                SignalGates = System.Array.Empty<LevelSignalGate>(),
                GhostTraces = new[]
                {
                    new GhostTrace { X = 2, Y = 0, Shape = SegmentShape.Straight, Rotation = 0, Capacity = 2 },
                    new GhostTrace { X = 4, Y = 0, Shape = SegmentShape.Straight, Rotation = 0, Capacity = 2 },
                },
                ImpedanceCells = new[]
                {
                    new ImpedanceCell { X = 2, Y = 0, ResistanceCost = 2 },
                    new ImpedanceCell { X = 4, Y = 0, ResistanceCost = 2 },
                },
                Inventory = new[]
                {
                    TraceSegment.Straight(2),  // (1,0)
                    TraceSegment.Straight(2),  // (3,0) — no Repeater!
                    TraceSegment.Straight(2),  // (5,0)
                },
            };

            var result = RunToCompletion(level, (inv, board, sim) =>
            {
                inv.TryPlace(0, board, 1, 0, sim, 0);
                inv.TryPlace(1, board, 3, 0, sim, 0);
                inv.TryPlace(2, board, 5, 0, sim, 0);
            });

            Assert.AreEqual(SimulationResult.SignalStuck, result);
        }

        // ────────────────────────────────────────────────────────────────
        // 17. LEVEL 99 — end-to-end integration test
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void Level99_AllMechanics_ReachesTarget()
        {
            // Level 99: ghost trace, impedance, timing window, Repeater
            // Full trace: S(0,0) → Str(1,0) → GhostStr(2,0,imp1) → Repeater(3,0) → Imp(4,0,2) → Str(5,0) → T(6,0,window=5-8)
            var level = LevelData.Level99;
            var board = new GridBoard(level);
            var inventory = new TraceInventory(level.Inventory);
            var simulator = new SignalRouter();
            var targetsReached = new List<(int x, int y)>();

            simulator.OnTargetReached += (x, y, c) => targetsReached.Add((x, y));

            // Place pieces: Straight at (1,0), Repeater at (3,0), Straight at (5,0)
            // Ghost trace at (2,0) is pre-existing
            inventory.TryPlace(0, board, 1, 0, simulator, 0); // index 0 = Straight(2) at (1,0)
            inventory.TryPlace(1, board, 3, 0, simulator, 0); // index 1 = Repeater at (3,0)
            inventory.TryPlace(2, board, 5, 0, simulator, 0); // index 2 = Straight(2) at (5,0)

            simulator.StartSimulation(board, level, inventory);

            for (int i = 0; i < 20; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            Assert.AreEqual(SimulationResult.AllTargetsReached, simulator.GetResult(),
                "Level 99: Repeater should restore signal strength after first impedance, " +
                "survive second impedance, and reach target within timing window");
            Assert.Contains((6, 0), targetsReached);
        }

        [Test]
        public void Level99_WithoutRepeater_SignalDies()
        {
            // Same as Level99 but without placing the Repeater — just use a Straight instead
            // This tests that the level truly requires the Repeater
            var level = LevelData.Level99;
            var board = new GridBoard(level);
            // Use a custom inventory with 3 straights, no Repeater
            var noRepeaterInventory = new TraceInventory(new[]
            {
                TraceSegment.Straight(2),
                TraceSegment.Straight(2),
                TraceSegment.Straight(2),
            });
            var simulator = new SignalRouter();

            noRepeaterInventory.TryPlace(0, board, 1, 0, simulator, 0);
            noRepeaterInventory.TryPlace(1, board, 3, 0, simulator, 0); // Straight instead of Repeater
            noRepeaterInventory.TryPlace(2, board, 5, 0, simulator, 0);

            simulator.StartSimulation(board, level, noRepeaterInventory);

            for (int i = 0; i < 20; i++)
            {
                if (!simulator.IsRunning) break;
                simulator.Tick();
            }

            Assert.AreEqual(SimulationResult.SignalStuck, simulator.GetResult(),
                "Without Repeater, signal should die at the second impedance (cost 2 with only 1 strength left)");
        }
    }
}
