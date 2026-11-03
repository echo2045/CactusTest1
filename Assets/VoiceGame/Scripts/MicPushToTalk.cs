using System;
using Needle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoiceGame
{
    /// <summary>
    /// Keeps the microphone running in a ring buffer and, while the talk key is held, marks
    /// the samples to send. Starting early and reading slightly past the key press and
    /// release keeps the first and last words from being clipped.
    /// </summary>
    public sealed class MicPushToTalk : MonoBehaviour
    {
        const int RingSeconds = 15;
        const float PreRollSeconds = 0.25f;
        const float PostRollSeconds = 0.25f;
        const float MinSeconds = 0.5f;
        const float MaxHoldSeconds = 12f;
        const float TargetPeak = 0.6f;
        const float NoiseFloorPeak = 0.01f;

        public VoiceBrain brain;
        public Key talkKey = Key.V;
        public Key nextMicKey = Key.M;

        AudioClip ring;
        string device;
        int deviceIndex = -1;
        int rate;
        int startSample;
        float startedAt;
        float stopAt;

        public bool Listening { get; private set; }
        public float Level { get; private set; }
        public string Problem { get; private set; }
        public string DeviceName => device ?? "Windows default";

        void Start()
        {
            if (Application.isBatchMode)
            {
                enabled = false;
                return;
            }
            OpenDevice();
        }

        void OpenDevice()
        {
            if (ring != null) Microphone.End(device);
            ring = null;
            Listening = false;
            Level = 0f;
            stopAt = 0f;
            var devices = Microphone.devices;
            if (devices.Length == 0)
            {
                Problem = "No microphone found";
                return;
            }
            Problem = null;
            device = deviceIndex >= 0 && deviceIndex < devices.Length ? devices[deviceIndex] : null;
            Microphone.GetDeviceCaps(device, out var minRate, out var maxRate);
            rate = maxRate == 0 || (minRate <= Wav.SampleRate && Wav.SampleRate <= maxRate) ? Wav.SampleRate : maxRate;
            ring = Microphone.Start(device, true, RingSeconds, rate);
            if (ring == null) Problem = "Could not open the microphone";
        }

        void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current[nextMicKey].wasPressedThisFrame)
            {
                deviceIndex = deviceIndex + 1 >= Microphone.devices.Length ? -1 : deviceIndex + 1;
                OpenDevice();
            }
            if (ring == null) return;
            var key = Keyboard.current[talkKey];
            if (key.wasPressedThisFrame) Begin();
            if (key.wasReleasedThisFrame && Listening) stopAt = Time.unscaledTime + PostRollSeconds;
            if (!Listening) return;
            Level = MeasureLevel();
            var held = Time.unscaledTime - startedAt;
            if ((stopAt > 0f && Time.unscaledTime >= stopAt) || held >= MaxHoldSeconds) Finish();
        }

        void Begin()
        {
            stopAt = 0f;
            if (Listening) return;
            Listening = true;
            startedAt = Time.unscaledTime;
            startSample = Wrap(Microphone.GetPosition(device) - (int)(PreRollSeconds * rate));
        }

        void Finish()
        {
            Listening = false;
            Level = 0f;
            stopAt = 0f;
            var count = Wrap(Microphone.GetPosition(device) - startSample);
            if (count < MinSeconds * rate) return;
            var pcm = ReadMono(startSample, count);
            if (rate != Wav.SampleRate) pcm = Wav.Resample(pcm, rate, Wav.SampleRate);
            Normalize(pcm);
            brain.Submit(pcm);
        }

        float MeasureLevel()
        {
            var count = Mathf.Min(rate / 10, ring.samples);
            var recent = ReadMono(Wrap(Microphone.GetPosition(device) - count), count);
            var sum = 0f;
            foreach (var sample in recent) sum += sample * sample;
            return Mathf.Clamp01(Mathf.Sqrt(sum / recent.Length) * 6f);
        }

        float[] ReadMono(int start, int count)
        {
            var channels = ring.channels;
            var raw = new float[count * channels];
            var untilEnd = Mathf.Min(count, ring.samples - start);
            var head = new float[untilEnd * channels];
            ring.GetData(head, start);
            Array.Copy(head, raw, head.Length);
            if (count > untilEnd)
            {
                var tail = new float[(count - untilEnd) * channels];
                ring.GetData(tail, 0);
                Array.Copy(tail, 0, raw, head.Length, tail.Length);
            }
            if (channels == 1) return raw;
            var mono = new float[count];
            for (var i = 0; i < count; i++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++) sum += raw[i * channels + c];
                mono[i] = sum / channels;
            }
            return mono;
        }

        static void Normalize(float[] pcm)
        {
            var peak = 0f;
            foreach (var sample in pcm) peak = Mathf.Max(peak, Mathf.Abs(sample));
            if (peak < NoiseFloorPeak || peak >= TargetPeak) return;
            var gain = TargetPeak / peak;
            for (var i = 0; i < pcm.Length; i++) pcm[i] *= gain;
        }

        int Wrap(int sample)
        {
            var length = ring.samples;
            return (sample % length + length) % length;
        }

        void OnDestroy()
        {
            if (ring != null) Microphone.End(device);
        }
    }
}
