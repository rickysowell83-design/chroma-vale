namespace ChromaVale.Core.GameLogic
{
    /// <summary>
    /// Pure C# audio service interface. No Unity dependencies.
    /// Implementations in Infrastructure/Audio handle actual playback.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>Play a named sound effect globally.</summary>
        void PlaySound(string soundName);

        /// <summary>Play a named sound effect at a world-space position (spatial audio).</summary>
        void PlaySoundAtPosition(string soundName, float x, float y);

        /// <summary>Set master volume (0.0 – 1.0).</summary>
        void SetVolume(float volume);

        /// <summary>Current master volume.</summary>
        float Volume { get; }
    }
}
