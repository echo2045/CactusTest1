using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Needle;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VoiceGame
{
    public readonly struct ToolCall
    {
        public readonly string Name;
        public readonly string Place;

        public ToolCall(string name, string place)
        {
            Name = name;
            Place = place;
        }
    }

    public sealed class VoiceOutcome
    {
        public string Transcript = "";
        public readonly List<ToolCall> Calls = new List<ToolCall>();
        public float Confidence;
        public long Milliseconds;
        public string Error;
    }

    /// <summary>
    /// Runs Whistle (speech to text) and Needle (text to tool call) on one worker thread,
    /// so the game thread only hands over audio and collects finished outcomes.
    /// </summary>
    public sealed class VoiceBrain : MonoBehaviour
    {
        const string ToolsJson = """
            [
            {"name":"walk_to","description":"Walk the hero to a place.","parameters":{"type":"object","properties":{"place":{"type":"string","enum":["chest","tower","gate","bridge"]}},"required":["place"]}},
            {"name":"run_to","description":"Run or sprint the hero to a place.","parameters":{"type":"object","properties":{"place":{"type":"string","enum":["chest","tower","gate","bridge"]}},"required":["place"]}},
            {"name":"jump","description":"Jump in the air.","parameters":{"type":"object","properties":{}}},
            {"name":"crouch","description":"Crouch down or duck.","parameters":{"type":"object","properties":{}}},
            {"name":"stop","description":"Stop moving and stand still.","parameters":{"type":"object","properties":{}}}
            ]
            """;

        public int threads = 2;

        readonly ConcurrentQueue<VoiceOutcome> finished = new ConcurrentQueue<VoiceOutcome>();
        InferenceThread worker;
        Model needleModel;
        Model whistleModel;
        VoiceSession voice;
        int pending;
        volatile bool ready;
        volatile string loadError;

        public event Action<VoiceOutcome> Recognized;

        public bool Ready => ready;
        public bool Thinking => Volatile.Read(ref pending) > 0;
        public string LoadError => loadError;

        void Awake()
        {
            var folder = Application.streamingAssetsPath;
            var threadCount = Mathf.Max(1, threads);
            worker = new InferenceThread("voice-game");
            worker.Run(() => Load(folder, threadCount));
        }

        void Load(string folder, int threadCount)
        {
            try
            {
                needleModel = Model.Open(Path.Combine(folder, "needle3.cact"));
                whistleModel = Model.Open(Path.Combine(folder, "whistle.cact"));
                voice = new VoiceSession(needleModel, whistleModel, ToolsJson, new NeedleOptions { Threading = ThreadPolicy.Game.WithThreads(threadCount) });
                voice.Needle.WarmUp();
                var noise = WarmUpNoise();
                for (var i = 0; i < 2; i++)
                {
                    voice.Needle.Reset();
                    voice.Complete(noise, "en");
                }
                ready = true;
            }
            catch (Exception e)
            {
                loadError = e.Message;
                Debug.LogException(e);
            }
        }

        static float[] WarmUpNoise()
        {
            var random = new System.Random(1);
            var noise = new float[Wav.SampleRate * 2];
            for (var i = 0; i < noise.Length; i++) noise[i] = (float)(random.NextDouble() - 0.5) * 0.4f;
            return noise;
        }

        public bool Submit(float[] pcm)
        {
            if (!ready || pcm == null || pcm.Length == 0) return false;
            Interlocked.Increment(ref pending);
            worker.Run(() => finished.Enqueue(Recognize(pcm)));
            return true;
        }

        VoiceOutcome Recognize(float[] pcm)
        {
            var outcome = new VoiceOutcome();
            var clock = Stopwatch.StartNew();
            try
            {
                voice.Needle.Reset();
                var result = voice.Complete(pcm, "en");
                outcome.Transcript = result.Speech.Text ?? "";
                outcome.Confidence = result.Call.Confidence ?? 0f;
                if (!result.Call.Success) outcome.Error = result.Call.Error;
                foreach (var call in result.Call.FunctionCalls)
                    outcome.Calls.Add(new ToolCall(call.Name, JsonValue.Parse(call.ArgumentsJson).GetString("place")));
            }
            catch (Exception e)
            {
                outcome.Error = e.Message;
                Debug.LogException(e);
            }
            outcome.Milliseconds = clock.ElapsedMilliseconds;
            Interlocked.Decrement(ref pending);
            return outcome;
        }

        void Update()
        {
            while (finished.TryDequeue(out var outcome)) Recognized?.Invoke(outcome);
        }

        void OnDestroy()
        {
            if (worker == null) return;
            worker.Run(() =>
            {
                voice?.Dispose();
                needleModel?.Dispose();
                whistleModel?.Dispose();
            }).Wait();
            worker.Dispose();
            worker = null;
        }
    }
}
