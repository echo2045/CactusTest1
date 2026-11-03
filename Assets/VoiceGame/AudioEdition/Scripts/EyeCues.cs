using UnityEngine;

namespace VoiceGame
{
    /// <summary>
    /// Synthesizes the game's sounds. The three eye states differ in rhythm and tone as well as
    /// pitch, and stay above 300 Hz so they carry on laptop speakers.
    /// Green is a slow soft chime, yellow is beeps that rise and speed up, red is a harsh siren.
    /// </summary>
    public static class EyeCues
    {
        const int SampleRate = 44100;
        const float Tau = Mathf.PI * 2f;

        public static float[] GreenSamples()
        {
            var data = new float[(int)(SampleRate * 1.6f)];
            Chime(data, 0f, 523.25f, 0.5f, 0.28f);
            return data;
        }

        public static float[] YellowSamples()
        {
            var data = new float[(int)(SampleRate * 2.5f)];
            const float beep = 0.09f;
            var time = 0f;
            while (time < 2.5f - beep)
            {
                var progress = time / 2.5f;
                Beep(data, time, beep, Mathf.Lerp(700f, 1500f, progress), 0.6f);
                time += beep + Mathf.Lerp(0.32f, 0.05f, progress);
            }
            return data;
        }

        public static float[] RedSamples()
        {
            var data = new float[(int)(SampleRate * 0.88f)];
            var phase = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var time = i / (float)SampleRate;
                var frequency = (int)(time / 0.22f) % 2 == 0 ? 950f : 1250f;
                phase += Tau * frequency / SampleRate;
                var sample = 0f;
                for (var harmonic = 1; harmonic <= 5; harmonic++) sample += Mathf.Sin(phase * harmonic) / harmonic;
                data[i] = Mathf.Clamp(sample * 0.55f, -0.8f, 0.8f);
            }
            return data;
        }

        public static float[] TalkStartSamples()
        {
            var data = new float[(int)(SampleRate * 0.16f)];
            Beep(data, 0f, 0.07f, 660f, 0.5f);
            Beep(data, 0.08f, 0.07f, 990f, 0.5f);
            return data;
        }

        public static float[] TalkEndSamples()
        {
            var data = new float[(int)(SampleRate * 0.16f)];
            Beep(data, 0f, 0.07f, 990f, 0.5f);
            Beep(data, 0.08f, 0.07f, 660f, 0.5f);
            return data;
        }

        public static AudioClip ToClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static void Chime(float[] data, float start, float frequency, float amplitude, float decay)
        {
            var first = (int)(start * SampleRate);
            for (var i = first; i < data.Length; i++)
            {
                var time = (i - first) / (float)SampleRate;
                var envelope = amplitude * Mathf.Exp(-time / decay) * Mathf.Min(1f, time / 0.004f);
                data[i] += envelope * (Mathf.Sin(Tau * frequency * time) + 0.3f * Mathf.Sin(Tau * frequency * 2f * time));
            }
        }

        static void Beep(float[] data, float start, float length, float frequency, float amplitude)
        {
            var first = (int)(start * SampleRate);
            var count = (int)(length * SampleRate);
            for (var i = 0; i < count && first + i < data.Length; i++)
            {
                var time = i / (float)SampleRate;
                var edge = Mathf.Min(1f, Mathf.Min(time, length - time) / 0.005f);
                data[first + i] += amplitude * edge * (Mathf.Sin(Tau * frequency * time) + 0.3f * Mathf.Sin(Tau * frequency * 3f * time));
            }
        }
    }
}
