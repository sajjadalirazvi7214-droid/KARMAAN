using System.Collections.Generic;
using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// Every sound in the demo build is generated as a sine wave at runtime. No audio assets,
    /// no download, no licence. It is not a substitute for the recorded Hindi/Santali voice
    /// tracks — those are Phase 3 — but an alarm has to *sound* like an alarm for the fire
    /// lesson to land, and this is enough to prove the loop.
    /// </summary>
    public static class ToneBank
    {
        private const int SampleRate = 44100;

        private static AudioSource _source;
        private static readonly Dictionary<string, AudioClip> Cache =
            new Dictionary<string, AudioClip>();

        public static void Init(AudioSource source)
        {
            _source = source;
        }

        public static void Click()
        {
            Play("click", 880f, 0.05f, 0.35f, false);
        }

        public static void Correct()
        {
            Play("correct", 660f, 0.16f, 0.45f, true);
        }

        public static void Wrong()
        {
            Play("wrong", 196f, 0.28f, 0.45f, false);
        }

        public static void Alarm()
        {
            Play("alarm", 1180f, 0.55f, 0.4f, true);
        }

        public static void Beep()
        {
            Play("beep", 1040f, 0.12f, 0.5f, false);
        }

        public static void Pass()
        {
            Play("pass", 523f, 0.45f, 0.5f, true);
        }

        private static void Play(string key, float freq, float duration, float gain, bool twoTone)
        {
            if (_source == null) return;

            AudioClip clip;
            if (!Cache.TryGetValue(key, out clip) || clip == null)
            {
                clip = Build(key, freq, duration, gain, twoTone);
                Cache[key] = clip;
            }

            _source.PlayOneShot(clip);
        }

        private static AudioClip Build(string key, float freq, float duration, float gain, bool twoTone)
        {
            var samples = Mathf.Max(16, Mathf.CeilToInt(SampleRate * duration));
            var data = new float[samples];

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)SampleRate;
                var progress = i / (float)samples;

                var f = freq;
                if (twoTone)
                {
                    // Second half jumps a fifth up: reads as "success" or "urgent", never as "flat".
                    f = progress < 0.5f ? freq : freq * 1.5f;
                }

                var envelope = Envelope(progress);
                var wave = Mathf.Sin(2f * Mathf.PI * f * t);

                // A touch of second harmonic so it cuts through a noisy shed.
                wave += 0.25f * Mathf.Sin(4f * Mathf.PI * f * t);

                data[i] = wave * envelope * gain * 0.5f;
            }

            var clip = AudioClip.Create(key, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Envelope(float progress)
        {
            const float attack = 0.04f;
            const float release = 0.35f;

            if (progress < attack) return progress / attack;
            if (progress > 1f - release) return (1f - progress) / release;
            return 1f;
        }
    }
}
