using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    public class MusicDirector : MonoBehaviour
    {
        private AudioSource _musicSource;

        public void StartMusic()
        {
            var cam = Camera.main;
            if (cam == null) return;
            _musicSource = cam.gameObject.GetComponent<AudioSource>();
            if (_musicSource == null) _musicSource = cam.gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.volume = 0.15f;
            _musicSource.clip = GenerateCyberpunkLoop();
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource != null)
            {
                _musicSource.Stop();
            }
        }

        private AudioClip GenerateCyberpunkLoop()
        {
            int sampleRate = 44100;
            float duration = 16f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("cyberpunk_loop", samples, 1, sampleRate, false);
            var data = new float[samples];

            // D minor — moody cyberpunk key
            float[] bassNotes = { 73.4f, 73.4f, 82.4f, 73.4f, 98.0f, 82.4f, 73.4f, 73.4f };
            float[] arpNotes = { 293.7f, 349.2f, 440.0f, 349.2f, 293.7f, 440.0f, 523.3f, 440.0f };
            float bpm = 100f;
            float beatDur = 60f / bpm;
            float measureDur = beatDur * 4f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Bass: slow pulse, filter sweep
                int bassIdx = Mathf.FloorToInt(t / beatDur) % bassNotes.Length;
                float bassFreq = bassNotes[bassIdx];
                float bassEnv = 1f - Mathf.Abs(Mathf.Sin(Mathf.PI * (t % measureDur) / measureDur));
                sample += Mathf.Sin(2f * Mathf.PI * bassFreq * t) * bassEnv * 0.25f;
                // Sub layer
                sample += Mathf.Sin(2f * Mathf.PI * bassFreq * 0.5f * t) * bassEnv * 0.2f;

                // Arp: detuned saw-ish lead
                int arpIdx = Mathf.FloorToInt(t / (beatDur * 0.25f)) % arpNotes.Length;
                float arpFreq = arpNotes[arpIdx];
                float arpPhase = (t % (beatDur * 0.25f)) / (beatDur * 0.25f);
                if (arpPhase < 0.6f)
                {
                    float arpEnv = 1f - arpPhase / 0.6f;
                    float saw = 2f * ((arpFreq * t) % 1f) - 1f;
                    sample += saw * arpEnv * 0.08f;
                }

                // Pad: slow filtered chord swell
                float padFreq = 146.8f; // D3
                float padEnv = (Mathf.Sin(2f * Mathf.PI * 0.125f * t) + 1f) * 0.5f;
                sample += Mathf.Sin(2f * Mathf.PI * padFreq * t) * padEnv * 0.06f;
                sample += Mathf.Sin(2f * Mathf.PI * padFreq * 1.5f * t) * padEnv * 0.04f;

                // Kick: deep thump on 1 and 3
                float beatInMeasure = (t % measureDur) / beatDur;
                if (beatInMeasure < 0.08f || (beatInMeasure >= 2f && beatInMeasure < 2.08f))
                {
                    float kPhase = beatInMeasure < 0.08f ? beatInMeasure : beatInMeasure - 2f;
                    float kEnv = Mathf.Exp(-kPhase * 50f);
                    sample += Mathf.Sin(2f * Mathf.PI * 55f * t * (1f + kPhase * 3f)) * kEnv * 0.5f;
                }

                // Snare/rim on 2 and 4
                if ((beatInMeasure >= 1f && beatInMeasure < 1.05f) || (beatInMeasure >= 3f && beatInMeasure < 3.05f))
                {
                    float sPhase = beatInMeasure >= 3f ? beatInMeasure - 3f : beatInMeasure - 1f;
                    float sEnv = Mathf.Exp(-sPhase * 30f);
                    sample += Mathf.Sin(2f * Mathf.PI * 200f * t) * sEnv * 0.3f;
                    sample += (Random.value * 2f - 1f) * sEnv * 0.2f;
                }

                // Hi-hat: 16th note pattern
                float sixteenth = (t % (beatDur * 0.25f));
                if (sixteenth < 0.015f)
                    sample += (Random.value * 2f - 1f) * 0.06f;
                // Open hat on off-beats
                if (sixteenth < 0.04f && Mathf.FloorToInt(t / (beatDur * 0.5f)) % 2 == 1)
                    sample += (Random.value * 2f - 1f) * 0.04f;

                // Master filter sweep (low-pass feel)
                float filterCutoff = 0.3f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.05f * t);
                sample *= filterCutoff;

                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        public void PlayBeep(float freq = 440f, float duration = 0.1f)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("beep", samples, 1, sampleRate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / sampleRate) * 0.3f;
            clip.SetData(data, 0);
            var cam = Camera.main;
            if (cam != null)
            {
                var src = cam.gameObject.GetComponent<AudioSource>();
                if (src == null) src = cam.gameObject.AddComponent<AudioSource>();
                src.PlayOneShot(clip);
            }
        }
    }
}
