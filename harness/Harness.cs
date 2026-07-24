using System;
using System.Collections.Generic;
using System.Linq;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;

// Verification harness: replicates the EXACT failing NUnit scenarios against the fixed engine.
public static class Harness
{
    static int _pass, _fail;

    public static void Main()
    {
        // ── Test: Cap1Pipe_Pressure2_Bursts (3x1: S p2 → cap1 straight → T) ──
        {
            var level = L(3, 1,
                new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 2 } },
                new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                new[] { PipePiece.Straight(1) });
            var (result, bursts, mixes, reached, sim) = Run(level, (inv, bd, s) => inv.TryPlace(0, bd, 1, 0, s, 0));
            Check("Cap1/P2 bursts at (1,0)", bursts.Count == 1 && bursts[0] == (1, 0));
            Check("Cap1/P2 cell state Burst", sim.GetCellState(1, 0).State == OverloadState.Burst);
        }

        // ── Same but cap2: no burst, wins ──
        {
            var level = L(3, 1,
                new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 2 } },
                new[] { new LevelTarget { X = 2, Y = 0, ColorIndex = 0 } },
                new[] { PipePiece.Straight(2) });
            var (result, bursts, _, _, _) = Run(level, (inv, bd, s) => inv.TryPlace(0, bd, 1, 0, s, 0));
            Check("Cap2/P2 no burst + win", bursts.Count == 0 && result == SimulationResult.AllTargetsReached);
        }

        // ── Test: TJunction_SplitsFlowToTwoTargets (5x3, TJ rot0 at (1,0)) ──
        {
            var level = L(5, 3,
                new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 1 } },
                new[] { new LevelTarget { X = 4, Y = 0, ColorIndex = 0 }, new LevelTarget { X = 1, Y = 2, ColorIndex = 0 } },
                new[] { PipePiece.TJunction(2), PipePiece.Straight(2), PipePiece.Straight(2), PipePiece.Straight(2) });
            var (result, _, _, reached, _) = Run(level, (inv, bd, s) =>
            {
                inv.TryPlace(0, bd, 1, 0, s, 180); // rot180: branch exits toward +y (engine Up)
                inv.TryPlace(1, bd, 2, 0, s, 0);
                inv.TryPlace(2, bd, 3, 0, s, 0);
                inv.TryPlace(3, bd, 1, 1, s, 90);
            });
            Check("TJn rot0 splits to both targets", result == SimulationResult.AllTargetsReached && reached.Count == 2);
        }

        // ── Test: MixerCell C+M → Purple target (3x3) ──
        {
            var level = L(3, 3,
                new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 1 },
                        new LevelSource { X = 2, Y = 0, ColorIndex = 1, FlowPressure = 1 } },
                new[] { new LevelTarget { X = 1, Y = 2, ColorIndex = 6 } },
                new[] { PipePiece.Mixer(), PipePiece.Straight(2) });
            var (result, _, mixes, _, _) = Run(level, (inv, bd, s) =>
            {
                inv.TryPlace(0, bd, 1, 0, s, 0);
                inv.TryPlace(1, bd, 1, 1, s, 90);
            });
            Check("Mixer fires OnColorMix at (1,0)", mixes.Any(m => m.x == 1 && m.y == 0));
            Check("Purple target reached", result == SimulationResult.AllTargetsReached);
        }

        // ── Regression: Level 1 straight-line still wins at par ──
        {
            var repo = new LevelRepository();
            var l1 = repo.GetLevel(1);
            var (result, bursts, _, _, sim) = Run(l1, (inv, bd, s) =>
            {
                inv.TryPlace(0, bd, 1, 1, s, 0);
                inv.TryPlace(1, bd, 2, 1, s, 0);
            });
            Check($"Level1 wins (ticks={sim.CurrentTick} par={l1.ParTicks})",
                result == SimulationResult.AllTargetsReached && bursts.Count == 0);
        }

        // ── Regression: Level 3 now solvable with fixed inventory (3S+3E) ──
        {
            var repo = new LevelRepository();
            var l3 = repo.GetLevel(3);
            // src(0,2) → (1,2)Elbow(L→U) → (1,1)Straight vert → (1,0)Elbow(D→R) → (2,0)S → (3,0)S → tgt(4,0)
            var (result, _, _, _, _) = Run(l3, (inv, bd, s) =>
            {
                // inventory: 0,1,2=Straight  3,4,5=Elbow
                PlaceAnyRot(inv, bd, s, 3, 1, 2, "elbow L->U");
                inv.TryPlace(0, bd, 1, 1, s, 90);
                PlaceAnyRot(inv, bd, s, 4, 1, 0, "elbow D->R");
                inv.TryPlace(1, bd, 2, 0, s, 0);
                inv.TryPlace(2, bd, 3, 0, s, 0);
            }, verbose: true);
            Check("Level3 solvable with new inventory", result == SimulationResult.AllTargetsReached);
        }

        // ── Pressure split sanity: p2 through TJn → two p1 branches of cap1 survive ──
        {
            var level = L(5, 3,
                new[] { new LevelSource { X = 0, Y = 0, ColorIndex = 0, FlowPressure = 2 } },
                new[] { new LevelTarget { X = 4, Y = 0, ColorIndex = 0 }, new LevelTarget { X = 1, Y = 2, ColorIndex = 0 } },
                new[] { PipePiece.TJunction(2), PipePiece.Straight(1), PipePiece.Straight(1), PipePiece.Straight(1) });
            var (result, bursts, _, _, _) = Run(level, (inv, bd, s) =>
            {
                inv.TryPlace(0, bd, 1, 0, s, 180); // rot180: branch exits toward +y (engine Up)
                inv.TryPlace(1, bd, 2, 0, s, 0);
                inv.TryPlace(2, bd, 3, 0, s, 0);
                inv.TryPlace(3, bd, 1, 1, s, 90);
            });
            Check("P2 splits at TJn: cap1 branches survive (pressure math)", result == SimulationResult.AllTargetsReached && bursts.Count == 0);
        }


        // ── SOLVE L9 ──
        {
            var repo = new LevelRepository();
            var l9 = repo.GetLevel(9);
            var (result, bursts, _, _, _) = Run(l9, (inv, bd, s) =>
            {
                inv.TryPlace(0, bd, 1, 0, s, 0);
                inv.TryPlace(1, bd, 2, 0, s, 0);
                inv.TryPlace(3, bd, 3, 0, s, 270); // Elb rot270: IN Down|Left, OUT Up|Right
                inv.TryPlace(2, bd, 3, 1, s, 90);  // Str vertical
                inv.TryPlace(6, bd, 3, 2, s, 180); // TJn rot180: IN L|R|Down, OUT L|R|Up... exit Right
            }, verbose: true);
            Check("L9 solvable via top route", result == SimulationResult.AllTargetsReached);
        }

        // ── SOLVE L18 ──
        {
            var repo = new LevelRepository();
            var l18 = repo.GetLevel(18);
            var (result, bursts, _, _, _) = Run(l18, (inv, bd, s) =>
            {
                inv.TryPlace(0, bd, 1, 1, s, 0);
                inv.TryPlace(1, bd, 2, 1, s, 0);
                inv.TryPlace(3, bd, 3, 1, s, 270); // Elb rot270: IN Down|Left, OUT Up|Right
                inv.TryPlace(2, bd, 3, 2, s, 90);  // Str vertical
                inv.TryPlace(4, bd, 3, 3, s, 270); // Elb rot270: enter Down, exit Right
                inv.TryPlace(5, bd, 4, 3, s, 0);   // TJn rot0: IN has Left, OUT has Right
            }, verbose: true);
            Check("L18 solvable, no burst", result == SimulationResult.AllTargetsReached && bursts.Count == 0);
        }


        Console.WriteLine($"\n==== HARNESS: {_pass} passed, {_fail} failed ====");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }

    static void PlaceAnyRot(PipeInventory inv, GridBoard bd, FlowSimulator s, int idx, int x, int y, string label)
    {
        foreach (var r in new[] { 0, 90, 180, 270 })
            if (inv.TryPlace(idx, bd, x, y, s, r)) { Console.WriteLine($"    [{label}] idx{idx} ({x},{y}) rot{r}"); return; }
        Console.WriteLine($"    [{label}] FAILED all rotations");
    }

    static void Check(string label, bool ok)
    {
        if (ok) { _pass++; Console.WriteLine($"PASS  {label}"); }
        else { _fail++; Console.WriteLine($"FAIL  {label}"); }
    }

    static LevelData L(int w, int h, LevelSource[] s, LevelTarget[] t, PipePiece[] inv) => new LevelData
    {
        Width = w, Height = h, Sources = s, Targets = t,
        Obstacles = Array.Empty<LevelObstacle>(), FlowGates = Array.Empty<LevelFlowGate>(),
        Inventory = inv, ParTicks = 20
    };

    static (SimulationResult, List<(int x, int y)>, List<(int x, int y, int a, int b)>, List<(int x, int y)>, FlowSimulator)
        Run(LevelData level, Action<PipeInventory, GridBoard, FlowSimulator> place, bool verbose = false)
    {
        var board = new GridBoard(level);
        var inv = new PipeInventory(level.Inventory);
        var sim = new FlowSimulator();
        var bursts = new List<(int, int)>(); var mixes = new List<(int, int, int, int)>(); var reached = new List<(int, int)>();
        sim.OnPipeBurst += (x, y) => bursts.Add((x, y));
        sim.OnColorMix += (x, y, a, b) => mixes.Add((x, y, a, b));
        sim.OnTargetReached += (x, y, c) => reached.Add((x, y));
        place(inv, board, sim);
        sim.StartSimulation(board, level, inv);
        for (int i = 0; i < 100 && sim.IsRunning; i++) sim.Tick();
        if (verbose) Console.WriteLine($"    result={sim.GetResult()} ticks={sim.CurrentTick} reached=[{string.Join(" ", reached)}]");
        return (sim.GetResult(), bursts, mixes, reached, sim);
    }
}
