using UnityEngine;

namespace VoiceGame
{
    public enum GuidePhase { Briefing, WaitingToStart, Paused, Playing }

    /// <summary>
    /// Makes the game playable by ear. It teaches the rules and the eye's sounds before play,
    /// plays the sound for the eye's current state during play, and speaks what happens.
    /// It only reads SentinelGame and HeroCommander and uses their public methods.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class AudioGuide : MonoBehaviour
    {
        const float HoldSeconds = 1000000f;
        const float NearDistance = 3.5f;
        const float WaitingHintSeconds = 30f;
        const float GreenExampleSeconds = 3.3f;
        const float YellowExampleSeconds = 2.5f;
        const float RedExampleSeconds = 2.6f;
        const float ReviewSeconds = 1.8f;
        const float ListeningCueVolume = 0.1f;

        static readonly string[] RuleLines =
        {
            "intro_goal", "intro_places", "intro_moves", "intro_actions", "intro_eye", "intro_crouch", "intro_tower",
        };

        public Narrator narrator;
        public SentinelGame game;
        public HeroCommander hero;
        public MicPushToTalk mic;
        public VoiceBrain brain;
        public AudioSource cueSource;
        public AudioSource earconSource;

        AudioClip green;
        AudioClip yellow;
        AudioClip red;
        AudioClip talkStart;
        AudioClip talkEnd;
        bool wasListening;
        bool hadKey;
        bool jamAnnounced;
        int lastLives;
        RoundState lastRound;
        SentinelState lastSentinel;
        string lastDestination;
        string lastMic;
        float waitingSince;

        public GuidePhase Phase { get; private set; }
        public string CurrentCue { get; private set; } = "";
        public bool SuppressNextArrival { get; set; }

        void Start()
        {
            green = EyeCues.ToClip("Green", EyeCues.GreenSamples());
            yellow = EyeCues.ToClip("Yellow", EyeCues.YellowSamples());
            red = EyeCues.ToClip("Red", EyeCues.RedSamples());
            talkStart = EyeCues.ToClip("TalkStart", EyeCues.TalkStartSamples());
            talkEnd = EyeCues.ToClip("TalkEnd", EyeCues.TalkEndSamples());
            hero.input.enabled = false;
            lastMic = mic.DeviceName;
            BeginBriefing();
            if (mic.Problem != null) narrator.SayFirst("no_mic");
        }

        public void BeginBriefing()
        {
            HoldGame();
            Phase = GuidePhase.Briefing;
            narrator.Stop();
            narrator.Say("intro_welcome");
            narrator.Say("intro_talk");
            narrator.PlaySound("sound_talk_start", talkStart, "Listening sound");
            narrator.Say("intro_talk_off");
            narrator.PlaySound("sound_talk_end", talkEnd, "Stopped listening sound");
            narrator.Say("intro_mic");
            SayRules();
            TeachSounds();
            narrator.Say("intro_meta");
            narrator.Say("intro_ready");
        }

        public void HelpWithRules()
        {
            PauseGame();
            SayRules();
        }

        public void HelpWithSounds()
        {
            PauseGame();
            TeachSounds();
        }

        public void StartGame(string line = "game_go")
        {
            Phase = GuidePhase.Playing;
            narrator.Stop();
            game.Restart();
            ResetTracking();
            narrator.Say(line);
        }

        public void ResumeGame()
        {
            Phase = GuidePhase.Playing;
            narrator.SayNow("back_to_game");
            game.Stun(0f);
            ResetTracking();
        }

        /// <summary>Gets the game running before an order is carried out. True if a new round began.</summary>
        public bool EnsurePlaying()
        {
            if (Phase == GuidePhase.Playing) return false;
            if (Phase == GuidePhase.Paused)
            {
                Phase = GuidePhase.Playing;
                narrator.Stop();
                game.Stun(0f);
                ResetTracking();
                return false;
            }
            StartGame();
            return true;
        }

        /// <summary>Speaks a reply without losing the rest of a briefing.</summary>
        public void Reply(string line)
        {
            if (Phase == GuidePhase.Playing || Phase == GuidePhase.WaitingToStart) narrator.SayNow(line);
            else narrator.SayFirst(line);
        }

        public void SayStatus()
        {
            narrator.SayNow(NearestPlaceLine());
            narrator.Say(game.HasKey ? "status_key_yes" : "status_key_no");
            narrator.Say("status_lives_" + Mathf.Clamp(game.Lives, 1, 3));
        }

        void SayRules()
        {
            foreach (var line in RuleLines) narrator.Say(line);
        }

        void TeachSounds()
        {
            narrator.Say("cues_intro");
            narrator.Say("cue_green");
            narrator.PlaySound("sound_green", green, "Green sound: slow soft chime", GreenExampleSeconds);
            narrator.Say("cue_yellow");
            narrator.PlaySound("sound_yellow", yellow, "Yellow sound: beeps rising and speeding up", YellowExampleSeconds);
            narrator.Say("cue_red");
            narrator.PlaySound("sound_red", red, "Red sound: siren", RedExampleSeconds);
            narrator.Say("cues_review");
            narrator.PlaySound("sound_green", green, "Green sound", ReviewSeconds);
            narrator.Say("review_green");
            narrator.PlaySound("sound_yellow", yellow, "Yellow sound", YellowExampleSeconds);
            narrator.Say("review_yellow");
            narrator.PlaySound("sound_red", red, "Red sound", ReviewSeconds);
            narrator.Say("review_red");
        }

        void PauseGame()
        {
            HoldGame();
            Phase = GuidePhase.Paused;
            narrator.Stop();
        }

        void HoldGame()
        {
            hero.Halt();
            game.Stun(HoldSeconds);
            jamAnnounced = false;
        }

        void ResetTracking()
        {
            hadKey = game.HasKey;
            lastLives = game.Lives;
            lastRound = game.Round;
            lastSentinel = game.Sentinel;
            lastDestination = null;
            jamAnnounced = false;
            SuppressNextArrival = false;
        }

        void Update()
        {
            UpdateListening();
            UpdateMicChoice();
            switch (Phase)
            {
                case GuidePhase.Briefing:
                    if (!narrator.Speaking && !mic.Listening && !brain.Thinking) EnterWaiting();
                    break;
                case GuidePhase.WaitingToStart:
                    UpdateWaiting();
                    break;
                case GuidePhase.Paused:
                    if (!narrator.Speaking && !mic.Listening && !brain.Thinking) ResumeGame();
                    break;
                case GuidePhase.Playing:
                    WatchGame();
                    break;
            }
            UpdateCue();
        }

        void EnterWaiting()
        {
            Phase = GuidePhase.WaitingToStart;
            waitingSince = Time.unscaledTime;
        }

        void UpdateWaiting()
        {
            if (narrator.Speaking || mic.Listening || brain.Thinking)
            {
                waitingSince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - waitingSince < WaitingHintSeconds) return;
            narrator.Say("waiting_hint");
        }

        void UpdateListening()
        {
            if (mic.Listening != wasListening)
            {
                wasListening = mic.Listening;
                earconSource.PlayOneShot(wasListening ? talkStart : talkEnd);
                if (wasListening)
                {
                    if (Phase == GuidePhase.Playing) narrator.Stop();
                    else narrator.Interrupt();
                }
            }
            if (!mic.Listening && !brain.Thinking) narrator.Resume();
        }

        void UpdateMicChoice()
        {
            if (mic.DeviceName == lastMic) return;
            lastMic = mic.DeviceName;
            Reply(mic.Problem == null ? "mic_changed" : "no_mic");
        }

        void WatchGame()
        {
            if (game.Round != lastRound)
            {
                lastRound = game.Round;
                if (game.Round == RoundState.Won) narrator.SayNow("won");
                else if (game.Round == RoundState.Lost) narrator.SayNow("lost");
            }
            if (game.Lives < lastLives && game.Lives > 0) narrator.SayNow("seen_" + game.Lives);
            lastLives = game.Lives;
            if (game.HasKey && !hadKey) narrator.SayNow("key_got");
            hadKey = game.HasKey;
            if (game.Sentinel != lastSentinel)
            {
                if (game.Sentinel == SentinelState.Stunned && Distance(hero.Position, game.tower.position) < NearDistance)
                {
                    jamAnnounced = true;
                    narrator.SayNow("jammed");
                }
                else if (lastSentinel == SentinelState.Stunned && jamAnnounced)
                {
                    jamAnnounced = false;
                    narrator.Say("jam_over");
                }
                lastSentinel = game.Sentinel;
            }
            if (lastDestination != null && hero.Destination == null) Arrived(lastDestination);
            lastDestination = hero.Destination;
        }

        void Arrived(string place)
        {
            var suppress = SuppressNextArrival;
            SuppressNextArrival = false;
            if (suppress || game.Round != RoundState.Playing) return;
            var point = hero.PlacePosition(place);
            if (point == null || Distance(hero.Position, point.Value) > NearDistance) return;
            switch (place)
            {
                case "chest": narrator.Say(game.HasKey ? "arrive_chest" : "arrive_chest_nokey"); break;
                case "bridge": narrator.Say("arrive_bridge"); break;
                case "gate": narrator.Say("arrive_gate_locked"); break;
                case "tower": if (!jamAnnounced) narrator.Say("arrive_tower"); break;
            }
        }

        void UpdateCue()
        {
            AudioClip wanted = null;
            CurrentCue = "";
            if (Phase == GuidePhase.Playing && game.Round == RoundState.Playing)
            {
                switch (game.Sentinel)
                {
                    case SentinelState.Waking:
                        wanted = yellow;
                        CurrentCue = "yellow";
                        break;
                    case SentinelState.Watching:
                        wanted = red;
                        CurrentCue = "red";
                        break;
                    default:
                        wanted = green;
                        CurrentCue = "green";
                        break;
                }
            }
            if (cueSource.clip != wanted)
            {
                cueSource.Stop();
                cueSource.clip = wanted;
                cueSource.loop = true;
                if (wanted != null) cueSource.Play();
            }
            var volume = wanted == green ? 0.6f : 1f;
            if (mic.Listening) volume *= ListeningCueVolume;
            else if (narrator.Speaking) volume *= 0.7f;
            cueSource.volume = volume;
        }

        string NearestPlaceLine()
        {
            var line = "near_start";
            var best = Distance(hero.Position, game.start.position);
            foreach (var landmark in hero.landmarks)
            {
                var distance = Distance(hero.Position, landmark.point.position);
                if (distance >= best) continue;
                best = distance;
                line = "near_" + landmark.name;
            }
            return line;
        }

        static float Distance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }

    public static class HeroCommanderPlaces
    {
        public static Vector3? PlacePosition(this HeroCommander hero, string place)
        {
            foreach (var landmark in hero.landmarks)
                if (landmark.name == place) return landmark.point.position;
            return null;
        }
    }
}
