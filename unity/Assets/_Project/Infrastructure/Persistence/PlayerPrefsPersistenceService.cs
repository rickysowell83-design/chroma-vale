using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Infrastructure.Persistence
{
    /// <summary>
    /// Prototype-grade persistence using Unity's PlayerPrefs.
    ///
    /// PRODUCTION UPGRADE NOTE:
    /// Replace this implementation with JSON file persistence (e.g., Newtonsoft.Json
    /// writing to Application.persistentDataPath) for better data integrity, portability,
    /// and the ability to inspect/edit save files outside the game.
    /// PlayerPrefs is convenient for rapid prototyping but is platform-specific and
    /// stores all data in a single opaque blob per app.
    /// </summary>
    public class PlayerPrefsPersistenceService : IPersistenceService
    {
        public void SaveInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public int LoadInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public void SaveString(string key, string value) => PlayerPrefs.SetString(key, value);
        public string LoadString(string key, string defaultValue) => PlayerPrefs.GetString(key, defaultValue);
        public void SaveBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);
        public bool LoadBool(string key, bool defaultValue) => PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void SaveAll() => PlayerPrefs.Save();
    }
}
