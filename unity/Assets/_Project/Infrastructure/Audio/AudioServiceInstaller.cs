using ChromaVale.Core.GameLogic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ChromaVale.Infrastructure.Audio
{
    /// <summary>
    /// MonoBehaviour that bootstraps the AudioService on Awake, persists it
    /// across scenes (DontDestroyOnLoad), and provides a static Instance
    /// accessor for prototype-phase code (e.g. PuzzleBoardView) that isn't
    /// yet wired through VContainer injection.
    ///
    /// In production, consume IAudioService via VContainer DI injection.
    /// In the prototype, call AudioServiceInstaller.Instance.PlaySound(...).
    /// </summary>
    public class AudioServiceInstaller : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private AudioLibrary _audioLibrary;
        [SerializeField] [Range(0f, 1f)] private float _initialVolume = 0.8f;

        private AudioService _audioService;

        /// <summary>
        /// Static accessor for prototype code that isn't injected via VContainer.
        /// Returns null before Awake() completes.
        /// </summary>
        public static IAudioService Instance { get; private set; }

        /// <summary>
        /// The underlying AudioService, exposed for register-with-VContainer
        /// scenarios.
        /// </summary>
        public AudioService Service => _audioService;

        private void Awake()
        {
            // Prevent duplicates
            if (Instance != null)
            {
                Debug.LogWarning("[Audio] AudioServiceInstaller already exists. " +
                                 "Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            // Persist across scene loads
            DontDestroyOnLoad(gameObject);

            // Create the service
            _audioService = new AudioService(gameObject, _audioLibrary);
            _audioService.SetVolume(_initialVolume);
            Instance = _audioService;

            Debug.Log("[Audio] AudioService initialized with " +
                      $"_audioLibrary reference assigned: {_audioLibrary != null}");
        }

        /// <summary>
        /// Registers IAudioService → AudioService in a VContainer builder.
        /// Call this from AppInstaller.Configure() via
        ///   builder.Register<IAudioService>(_ => instantiator.Service, ...)
        /// or more simply:
        ///   builder.RegisterInstance<IAudioService>(instantiator.Service);
        /// </summary>
        public void RegisterWithContainer(IContainerBuilder builder)
        {
            if (_audioService != null)
            {
                builder.RegisterInstance<IAudioService>(_audioService);
            }
        }

        private void OnDestroy()
        {
            if (Instance == _audioService as IAudioService)
            {
                Instance = null;
            }
        }
    }
}
