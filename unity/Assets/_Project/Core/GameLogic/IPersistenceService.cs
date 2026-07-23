namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Abstraction for persistent key-value storage.
    /// Pure C# — no Unity dependencies. Fully unit-testable.
    ///
    /// Implementations:
    ///   - PlayerPrefs (prototype — Infrastructure/Persistence)
    ///   - JSON file (production — Infrastructure/Persistence)
    /// </summary>
    public interface IPersistenceService
    {
        void SaveInt(string key, int value);
        int LoadInt(string key, int defaultValue);
        void SaveString(string key, string value);
        string LoadString(string key, string defaultValue);
        void SaveBool(string key, bool value);
        bool LoadBool(string key, bool defaultValue);
        void DeleteKey(string key);
        void SaveAll(); // Commit to disk
    }
}
