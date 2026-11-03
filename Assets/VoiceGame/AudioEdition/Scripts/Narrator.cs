using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoiceGame
{
    [Serializable]
    public struct NarrationLine
    {
        public string id;
        public string text;
        public AudioClip clip;
    }

    /// <summary>
    /// Plays recorded lines and sound examples one after another, and exposes the current
    /// line as a caption. Timing follows clip length, so it does not depend on the audio device.
    /// </summary>
    public sealed class Narrator : MonoBehaviour
    {
        const float GapSeconds = 0.25f;

        struct Entry
        {
            public string Id;
            public string Caption;
            public AudioClip Clip;
            public float Seconds;
        }

        public NarrationLine[] lines;
        public AudioSource voice;

        readonly LinkedList<Entry> queue = new LinkedList<Entry>();
        readonly Dictionary<string, int> lineIndex = new Dictionary<string, int>();
        Entry current;
        bool playing;
        bool held;
        float busyUntil;

        public event Action<string> Started;

        public string Caption { get; private set; } = "";
        public bool Speaking => playing || queue.Count > 0;

        void Awake()
        {
            for (var i = 0; i < lines.Length; i++) lineIndex[lines[i].id] = i;
        }

        public void Say(string id)
        {
            if (TryEntry(id, out var entry)) queue.AddLast(entry);
        }

        public void SayNow(string id)
        {
            Stop();
            Say(id);
        }

        public void SayFirst(string id)
        {
            if (TryEntry(id, out var entry)) queue.AddFirst(entry);
        }

        public void PlaySound(string id, AudioClip clip, string caption, float seconds = 0f)
        {
            queue.AddLast(new Entry { Id = id, Caption = caption, Clip = clip, Seconds = Mathf.Max(seconds, clip.length) });
        }

        public void Stop()
        {
            queue.Clear();
            voice.Stop();
            voice.loop = false;
            playing = false;
            held = false;
            Caption = "";
        }

        /// <summary>Stops the current line so the microphone stays clean, and keeps it to replay.</summary>
        public void Interrupt()
        {
            if (playing)
            {
                voice.Stop();
                voice.loop = false;
                queue.AddFirst(current);
                playing = false;
                Caption = "";
            }
            held = true;
        }

        public void Resume() => held = false;

        void Update()
        {
            if (held) return;
            if (playing)
            {
                if (Time.unscaledTime < busyUntil) return;
                voice.Stop();
                voice.loop = false;
                playing = false;
                Caption = "";
            }
            if (queue.Count == 0) return;
            current = queue.First.Value;
            queue.RemoveFirst();
            voice.clip = current.Clip;
            voice.loop = current.Seconds > current.Clip.length;
            voice.Play();
            playing = true;
            busyUntil = Time.unscaledTime + current.Seconds + GapSeconds;
            Caption = current.Caption;
            Started?.Invoke(current.Id);
        }

        bool TryEntry(string id, out Entry entry)
        {
            entry = default;
            if (!lineIndex.TryGetValue(id, out var index) || lines[index].clip == null)
            {
                Debug.LogWarning($"[VoiceGame] missing narration line {id}");
                return false;
            }
            var line = lines[index];
            entry = new Entry { Id = id, Caption = line.text, Clip = line.clip, Seconds = line.clip.length };
            return true;
        }
    }
}
