using System;
using UnityEngine;

namespace ChromaVale.Infrastructure.Audio
{
    /// <summary>
    /// ScriptableObject that maps sound effect names to AudioClip references.
    /// Create via Assets → Create → Chroma Vale → Audio Library,
    /// or at Assets/Audio/AudioLibrary.asset in the Unity Editor.
    ///
    /// Sound names (used by IAudioService.PlaySound):
    ///   button_tap  — level select button press
    ///   level_start — level loads / begins
    ///   merge       — two orbs merge into one
    ///   win_fanfare — level complete celebration
    ///   lock_flash  — target position locks/unlocks
    ///   spawn       — new orb spawns on the board
    /// </summary>
    [CreateAssetMenu(
        fileName = "AudioLibrary",
        menuName = "Chroma Vale/Audio Library",
        order = 10)]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public struct SoundEntry
        {
            public string name;
            public AudioClip clip;
        }

        [Header("Sound Effect Assignments")]
        [Tooltip("Leave clip unassigned to fall back to console logging. " +
                 "Assign actual .wav/.ogg files during production polish.")]
        public SoundEntry[] sounds;

        /// <summary>
        /// Look up an AudioClip by sound name. Returns null if not assigned.
        /// </summary>
        public AudioClip GetClip(string soundName)
        {
            if (sounds == null) return null;
            for (int i = 0; i < sounds.Length; i++)
            {
                if (string.Equals(sounds[i].name, soundName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return sounds[i].clip;
                }
            }
            return null;
        }
    }
}
