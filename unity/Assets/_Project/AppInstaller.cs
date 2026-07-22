using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ChromaVale
{
    public class AppInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Load level 1
            var repo = new LevelRepository();
            var level = repo.GetLevel(1);

            // Register domain services
            var board = new GridBoard(level);
            builder.RegisterInstance<IBoardState>(board);
            builder.RegisterInstance<ILevelRepository>(repo);
            builder.Register<IPipeRouter>(_ => new PipeRouter(board), Lifetime.Singleton);

            Debug.Log($"[ChromaVale] Board ready: {board.Width}x{board.Height} " +
                      $"with {level.Sources.Length} source(s) and {level.Targets.Length} target(s)");
        }
    }
}