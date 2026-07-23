using System.Collections.Generic;
using ChromaVale.Core.GameLogic;
using UnityEngine;

namespace ChromaVale.Infrastructure.Audio
{
    /// <summary>
    /// Audio service implementation using a round-robin pool of AudioSource components.
    /// If no clip is assigned in the AudioLibrary for a given sound name, falls back
    /// to Debug.Log — allowing development and testing without audio assets.
    /// </summary>
    public class AudioService : IAudioService
    {
        private const int PoolSize = 8;

        private readonly AudioSource[] _sources;
        private readonly GameObject _poolRoot;
        private readonly AudioLibrary _library;
        private float _volume = 1f;
        private int _nextSourceIndex;

        /// <summary>
        /// Create an AudioService attached to the given parent GameObject.
        /// AudioSource children are created automatically.
        /// </summary>
        /// <param name="poolRoot">Parent GameObject for the AudioSource pool.</param>
        /// <param name="library">Optional AudioLibrary with clip assignments.</param>
        public AudioService(GameObject poolRoot, AudioLibrary library)
        {
            _poolRoot = poolRoot;
            _library = library;
            _sources = new AudioSource[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"AudioSource_{i}");
                go.transform.SetParent(_poolRoot.transform, false);
                _sources[i] = go.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].spatialBlend = 0f;   // 2D by default
            }
        }

        public float Volume
        {
            get => _volume;
        }

        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
        }

        public void PlaySound(string soundName)
        {
            AudioClip clip = GetClip(soundName);

            if (clip == null)
            {
                Debug.Log($"[Audio] {soundName}");
                return;
            }

            var source = NextAvailableSource();
            source.volume = _volume;
            source.spatialBlend = 0f;
            source.clip = clip;
            source.Play();
        }

        public void PlaySoundAtPosition(string soundName, float x, float y)
        {
            AudioClip clip = GetClip(soundName);

            if (clip == null)
            {
                Debug.Log($"[Audio] {soundName} (at ({x:F1}, {y:F1}))");
                return;
            }

            var source = NextAvailableSource();
            source.volume = _volume;
            source.spatialBlend = 1f;   // Full 3D spatial blend
            source.transform.position = new Vector3(x, y, 0f);
            source.clip = clip;
            source.Play();
        }

        private AudioClip GetClip(string soundName)
        {
            if (_library != null)
            {
                AudioClip clip = _library.GetClip(soundName);
                if (clip != null) return clip;
            }
            return null;
        }

        /// <summary>
        /// Returns the next AudioSource in round-robin order.
        /// Sources that are not currently playing get priority; if all are busy
        /// the round-robin wraps and reuses the next one (last-one-wins).
        /// </summary>
        private AudioSource NextAvailableSource()
        {
            // Try to find an idle source first
            for (int i = 0; i < PoolSize; i++)
            {
                int index = (_nextSourceIndex + i) % PoolSize;
                if (!_sources[index].isPlaying)
                {
                    _nextSourceIndex = (index + 1) % PoolSize;
                    return _sources[index];
                }
            }

            // All sources busy — wrap round-robin
            var source = _sources[_nextSourceIndex];
            source.Stop();
            _nextSourceIndex = (_nextSourceIndex + 1) % PoolSize;
            return source;
        }
    }
}
