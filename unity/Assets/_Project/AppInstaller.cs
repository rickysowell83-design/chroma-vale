using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ChromaVale
{
    /// <summary>
    /// VContainer entry point. Registers domain services for DI.
    /// In prototype phase, PuzzleBoardView creates its own dependencies directly
    /// in Start() — DI wiring is here for when we transition to full architecture.
    /// </summary>
    public class AppInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var repo = new LevelRepository();
            var level = repo.GetLevel(1);

            // Domain services
            var board = new GridBoard(level);
            builder.RegisterInstance<IBoardState>(board);
            builder.RegisterInstance<ILevelRepository>(repo);

            // Flow simulation
            builder.Register<IFlowSimulator>(_ => new FlowSimulator(), Lifetime.Singleton);

            // Pipe inventory
            builder.RegisterInstance(new PipeInventory(level.Inventory));

            Debug.Log($"[ChromaVale 2.0] Board ready: {board.Width}x{board.Height} " +
                      $"with {level.Sources.Length} source(s), {level.Targets.Length} target(s), " +
                      $"{level.Inventory.Length} piece(s) in inventory");
        }
    }
}
