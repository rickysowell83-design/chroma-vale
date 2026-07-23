using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Domain.Progression
{
    /// <summary>
    /// Singleton MonoBehaviour that manages save/load of player progress.
    /// Uses IPersistenceService (injected via AppInstaller) for storage.
    ///
    /// In the prototype, AppInstaller sets the Persistence property before
    /// any scene code runs. Production would resolve through VContainer DI.
    ///
    /// Attach to a persistent GameObject (DontDestroyOnLoad) such as the
    /// AppInstaller or a dedicated bootstrapper.
    /// </summary>
    public class SaveGameManager : MonoBehaviour
    {
        public static SaveGameManager Instance { get; private set; }

        [Header("Persistence")]
        [SerializeField] private IPersistenceService _persistence;

        // ─────────────────────────────────────────────────────────────
        //  Public Properties (loaded/saved via persistence layer)
        // ─────────────────────────────────────────────────────────────

        /// <summary>The highest unlocked level the player can play (1-based).</summary>
        public int CurrentLevel { get; private set; } = 1;

        /// <summary>Total Chroma Stars collected across all levels.</summary>
        public int TotalChromaStars { get; private set; }

        // ─────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("[SaveGameManager] Duplicate singleton destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            LoadProgress();
        }

        // ─────────────────────────────────────────────────────────────
        //  Injection support
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Set the persistence service used for all save/load operations.
        /// Called by AppInstaller at boot. Falls back silently if null
        /// (methods log a warning and become no-ops).
        /// </summary>
        public IPersistenceService Persistence
        {
            get => _persistence;
            set => _persistence = value;
        }

        // ─────────────────────────────────────────────────────────────
        //  Save / Load
        // ─────────────────────────────────────────────────────────────

        private const string KeyCurrentLevel = "CurrentLevel";
        private const string KeyTotalStars = "TotalChromaStars";
        private const string KeyStarsPrefix = "Stars_Level_";

        /// <summary>
        /// Load all persisted progress into memory.
        /// Called automatically in Start().
        /// </summary>
        public void LoadProgress()
        {
            if (_persistence == null)
            {
                Debug.LogWarning("[SaveGameManager] No IPersistenceService set. Progress not loaded.");
                return;
            }

            CurrentLevel = _persistence.LoadInt(KeyCurrentLevel, 1);
            TotalChromaStars = _persistence.LoadInt(KeyTotalStars, 0);

            Debug.Log($"[SaveGameManager] Loaded: CurrentLevel={CurrentLevel}, TotalStars={TotalChromaStars}");
        }

        /// <summary>
        /// Write all current progress to persistent storage.
        /// </summary>
        public void SaveProgress()
        {
            if (_persistence == null)
            {
                Debug.LogWarning("[SaveGameManager] No IPersistenceService set. Progress not saved.");
                return;
            }

            _persistence.SaveInt(KeyCurrentLevel, CurrentLevel);
            _persistence.SaveInt(KeyTotalStars, TotalChromaStars);
            _persistence.SaveAll();

            Debug.Log($"[SaveGameManager] Saved: CurrentLevel={CurrentLevel}, TotalStars={TotalChromaStars}");
        }

        // ─────────────────────────────────────────────────────────────
        //  Level Completion
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Record that a level was completed with the given star rating.
        /// Updates persistent star count for the level and total star tally.
        /// The new star count only increases (best score preserved).
        /// </summary>
        /// <param name="level">1-based level number.</param>
        /// <param name="stars">Stars earned (0-3).</param>
        public void RecordLevelComplete(int level, int stars)
        {
            if (_persistence == null)
            {
                Debug.LogWarning("[SaveGameManager] No IPersistenceService set. Level completion not recorded.");
                return;
            }

            string starKey = KeyStarsPrefix + level;
            int previousStars = _persistence.LoadInt(starKey, 0);

            // Only update if this run earned more stars than previously saved
            if (stars > previousStars)
            {
                _persistence.SaveInt(starKey, stars);
                int starDelta = stars - previousStars;
                TotalChromaStars += starDelta;
                Debug.Log($"[SaveGameManager] Level {level}: {previousStars}★ → {stars}★ (+{starDelta})");
            }

            // Unlock the next level if this is the highest completed
            if (level >= CurrentLevel)
            {
                CurrentLevel = level + 1;
                Debug.Log($"[SaveGameManager] Next level unlocked: {CurrentLevel}");
            }

            SaveProgress();
        }

        /// <summary>
        /// Get the saved star count for a specific level.
        /// </summary>
        public int GetStarsForLevel(int level)
        {
            if (_persistence == null) return 0;
            return _persistence.LoadInt(KeyStarsPrefix + level, 0);
        }
    }
}
