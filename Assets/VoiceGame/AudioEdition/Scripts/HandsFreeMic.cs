using Needle;
using UnityEngine;

namespace VoiceGame
{
    /// <summary>Decides when someone starts and stops talking, from microphone loudness alone.</summary>
    public sealed class SpeechGate
    {
        public enum Change { None, Began, Ended }

        const float MinLevel = 0.012f;
        const float FloorFactor = 3f;
        const float OnsetSeconds = 0.12f;
        const float HangSeconds = 0.7f;
        const float MaxSeconds = 6f;
        const float FloorFollowPerSecond = 0.5f;

        float floor = 0.004f;
        float onset;
        float sinceLoud;
        float elapsed;

        public bool Active { get; private set; }

        public void Reset()
        {
            Active = false;
            onset = 0f;
            sinceLoud = 0f;
            elapsed = 0f;
        }

        public Change Feed(float level, float deltaTime)
        {
            var threshold = Mathf.Max(MinLevel, floor * FloorFactor);
            if (!Active)
            {
                if (level < threshold)
                {
                    floor = Mathf.Lerp(floor, level, FloorFollowPerSecond * deltaTime);
                    onset = 0f;
                    return Change.None;
                }
                onset += deltaTime;
                if (onset < OnsetSeconds) return Change.None;
                Active = true;
                elapsed = 0f;
                sinceLoud = 0f;
                return Change.Began;
            }
            elapsed += deltaTime;
            sinceLoud = level >= threshold * 0.7f ? 0f : sinceLoud + deltaTime;
            if (sinceLoud < HangSeconds && elapsed < MaxSeconds) return Change.None;
            Reset();
            return Change.Ended;
        }
    }

    /// <summary>
    /// Listens without any key, so the menu can be used by voice alone. It stays deaf while the
    /// narrator talks, so the game does not hear itself.
    /// </summary>
    public sealed class HandsFreeMic : MonoBehaviour
    {
        const int RingSeconds = 10;
        const float PreRollSeconds = 0.35f;
        const float MeasureSeconds = 0.05f;
        const float TargetPeak = 0.6f;
        const float NoiseFloorPeak = 0.01f;

        public VoiceBrain brain;
        public Narrator narrator;

        readonly SpeechGate gate = new SpeechGate();
        AudioClip ring;
        float[] measure;
        int rate;
        int startSample;

        public string Problem { get; private set; }
        public float Level { get; private set; }
        public bool Hearing => gate.Active;

        void Start()
        {
            if (Application.isBatchMode)
            {
                enabled = false;
                return;
            }
            if (Microphone.devices.Length == 0)
            {
                Problem = "No microphone found";
                return;
            }
            Microphone.GetDeviceCaps(null, out var minRate, out var maxRate);
            rate = maxRate == 0 || (minRate <= Wav.SampleRate && Wav.SampleRate <= maxRate) ? Wav.SampleRate : maxRate;
            ring = Microphone.Start(null, true, RingSeconds, rate);
            if (ring == null)
            {
                Problem = "Could not open the microphone";
                return;
            }
            measure = new float[(int)(rate * MeasureSeconds) * ring.channels];
        }

        void Update()
        {
            if (ring == null) return;
            if (narrator.Speaking || !brain.Ready)
            {
                gate.Reset();
                Level = 0f;
                return;
            }
            var position = Microphone.GetPosition(null);
            ring.GetData(measure, Wrap(position - measure.Length / ring.channels));
            var sum = 0f;
            foreach (var sample in measure) sum += sample * sample;
            Level = Mathf.Sqrt(sum / measure.Length);
            switch (gate.Feed(Level, Time.unscaledDeltaTime))
            {
                case SpeechGate.Change.Began:
                    startSample = Wrap(position - (int)(PreRollSeconds * rate));
                    break;
                case SpeechGate.Change.Ended:
                    Send(position);
                    break;
            }
        }

        void Send(int position)
        {
            var count = Wrap(position - startSample);
            var channels = ring.channels;
            var raw = new float[count * channels];
            ring.GetData(raw, startSample);
            var pcm = new float[count];
            for (var i = 0; i < count; i++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++) sum += raw[i * channels + c];
                pcm[i] = sum / channels;
            }
            if (rate != Wav.SampleRate) pcm = Wav.Resample(pcm, rate, Wav.SampleRate);
            var peak = 0f;
            foreach (var sample in pcm) peak = Mathf.Max(peak, Mathf.Abs(sample));
            if (peak >= NoiseFloorPeak && peak < TargetPeak)
                for (var i = 0; i < pcm.Length; i++) pcm[i] *= TargetPeak / peak;
            brain.Submit(pcm);
        }

        int Wrap(int sample)
        {
            var length = ring.samples;
            return (sample % length + length) % length;
        }

        void OnDestroy()
        {
            if (ring != null) Microphone.End(null);
        }
    }
}
