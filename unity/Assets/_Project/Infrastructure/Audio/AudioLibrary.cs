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
    ///   pipe_place     — player places a pipe piece on the grid
    ///   flow_tick      — flow advances into a pipe cell
    ///   pipe_burst     — a pipe bursts from overload
    ///   target_reached — flow reaches a target
    ///   win_fanfare    — level complete fanfare
    ///   color_mix      — two colors mix at a cell
    ///   undo           — player undoes a placement
    ///   level_start    — level begins
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
