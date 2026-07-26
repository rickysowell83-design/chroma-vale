using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.Progression;
using ChromaVale.Domain.PuzzleBoard;
using ChromaVale.Infrastructure.Audio;
using ChromaVale.Infrastructure.Persistence;
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

            // Signal simulation
            builder.Register<ISignalRouter>(_ => new SignalRouter(), Lifetime.Singleton);

            // Trace inventory
            builder.RegisterInstance(new TraceInventory(level.Inventory));

            // ─────────────────────────────────────────────────────────
            //  Persistence
            // ─────────────────────────────────────────────────────────

            var persistenceService = new PlayerPrefsPersistenceService();
            builder.RegisterInstance<IPersistenceService>(persistenceService);

            // Create SaveGameManager on a persistent GameObject so it survives scene loads
            var saveManagerGo = new GameObject("SaveGameManager");
            var saveManager = saveManagerGo.AddComponent<SaveGameManager>();
            saveManager.Persistence = persistenceService;
            Object.DontDestroyOnLoad(saveManagerGo);

            // Register for DI consumers (future use)
            builder.RegisterInstance(saveManager);

            // Load saved progress immediately at boot
            saveManager.LoadProgress();

            // ─────────────────────────────────────────────────────────
            //  Audio Service
            // ─────────────────────────────────────────────────────────

            // Register IAudioService with the persistent AudioServiceInstaller.
            // In the prototype, the AudioServiceInstaller MonoBehaviour is placed
            // in the Bootstrap scene. It creates the AudioService and registers
            // itself via DontDestroyOnLoad. PuzzleBoardView accesses it via
            // AudioServiceInstaller.Instance. This registration prepares for
            // full DI transition.
            var audioInstaller = FindAnyObjectByType<AudioServiceInstaller>();
            if (audioInstaller != null)
            {
                audioInstaller.RegisterWithContainer(builder);
                Debug.Log("[Audio] IAudioService registered with VContainer via AudioServiceInstaller");
            }

            Debug.Log($"[ChromaVale 2.0] Board ready: {board.Width}x{board.Height} " +
                      $"with {level.Sources.Length} source(s), {level.Targets.Length} target(s), " +
                      $"{level.Inventory.Length} piece(s) in inventory");
        }
    }
}
