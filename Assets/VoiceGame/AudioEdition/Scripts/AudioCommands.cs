using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoiceGame
{
    public static class VoicePhrases
    {
        /// <summary>Lower-cases the transcript and drops punctuation, so "Where am I?" matches "where am i".</summary>
        public static string Normalize(string transcript)
        {
            var result = new StringBuilder(transcript.Length);
            var pendingSpace = false;
            foreach (var character in transcript)
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSpace && result.Length > 0) result.Append(' ');
                    pendingSpace = false;
                    result.Append(char.ToLowerInvariant(character));
                }
                else if (char.IsWhiteSpace(character)) pendingSpace = true;
            }
            return result.ToString();
        }
    }

    /// <summary>
    /// The audio edition's version of CommandRouter. It adds the spoken help commands and says
    /// aloud what it understood, then gives the hero the same orders.
    /// </summary>
    public sealed class AudioCommands : MonoBehaviour
    {
        const float MenuDelaySeconds = 1.3f;
        const float SureConfidence = 0.5f;

        enum Meta { Help, Sounds, Status, Start, Restart, Menu, Repeat, Volume }

        static readonly Dictionary<string, Meta> Metas = new Dictionary<string, Meta>
        {
            ["help"] = Meta.Help,
            ["rules"] = Meta.Help,
            ["instructions"] = Meta.Help,
            ["what do i do"] = Meta.Help,
            ["sounds"] = Meta.Sounds,
            ["sound"] = Meta.Sounds,
            ["teach me the sounds"] = Meta.Sounds,
            ["what are the sounds"] = Meta.Sounds,
            ["where am i"] = Meta.Status,
            ["status"] = Meta.Status,
            ["how am i doing"] = Meta.Status,
            ["start"] = Meta.Start,
            ["starts"] = Meta.Start,
            ["started"] = Meta.Start,
            ["starved"] = Meta.Start,
            ["star"] = Meta.Start,
            ["stars"] = Meta.Start,
            ["skip"] = Meta.Start,
            ["skipped"] = Meta.Start,
            ["begin"] = Meta.Start,
            ["go"] = Meta.Start,
            ["ready"] = Meta.Start,
            ["im ready"] = Meta.Start,
            ["lets go"] = Meta.Start,
            ["lets start"] = Meta.Start,
            ["start the game"] = Meta.Start,
            ["restart"] = Meta.Restart,
            ["try again"] = Meta.Restart,
            ["play again"] = Meta.Restart,
            ["menu"] = Meta.Menu,
            ["main menu"] = Meta.Menu,
            ["go to the menu"] = Meta.Menu,
            ["repeat"] = Meta.Repeat,
            ["say that again"] = Meta.Repeat,
            ["settings"] = Meta.Volume,
            ["setting"] = Meta.Volume,
            ["volume"] = Meta.Volume,
            ["what is the volume"] = Meta.Volume,
        };

        static readonly Dictionary<string, HeroOrder> Words = new Dictionary<string, HeroOrder>
        {
            ["stop"] = HeroOrder.Stop,
            ["stops"] = HeroOrder.Stop,
            ["stopped"] = HeroOrder.Stop,
            ["stopping"] = HeroOrder.Stop,
            ["stop it"] = HeroOrder.Stop,
            ["stop now"] = HeroOrder.Stop,
            ["stup"] = HeroOrder.Stop,
            ["scop"] = HeroOrder.Stop,
            ["scott"] = HeroOrder.Stop,
            ["scot"] = HeroOrder.Stop,
            ["top"] = HeroOrder.Stop,
            ["stock"] = HeroOrder.Stop,
            ["halt"] = HeroOrder.Stop,
            ["freeze"] = HeroOrder.Stop,
            ["wait"] = HeroOrder.Stop,
            ["hold on"] = HeroOrder.Stop,
            ["jump"] = HeroOrder.Jump,
            ["jumps"] = HeroOrder.Jump,
            ["jumped"] = HeroOrder.Jump,
            ["jumping"] = HeroOrder.Jump,
            ["hop"] = HeroOrder.Jump,
            ["crouch"] = HeroOrder.Crouch,
            ["crouching"] = HeroOrder.Crouch,
            ["crouched"] = HeroOrder.Crouch,
            ["couch"] = HeroOrder.Crouch,
            ["duck"] = HeroOrder.Crouch,
            ["get down"] = HeroOrder.Crouch,
            ["sneak"] = HeroOrder.Crouch,
            ["hide"] = HeroOrder.Crouch,
            ["stand"] = HeroOrder.Stand,
            ["stand up"] = HeroOrder.Stand,
            ["get up"] = HeroOrder.Stand,
        };

        public VoiceBrain brain;
        public HeroCommander hero;
        public SentinelGame game;
        public AudioGuide guide;
        public Narrator narrator;

        readonly List<HeroCommand> commands = new List<HeroCommand>();
        string lastReply = "didnt_catch";

        public string LastHeard { get; private set; } = "";

        void OnEnable() => brain.Recognized += Handle;

        void OnDisable() => brain.Recognized -= Handle;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) guide.Reply(GameSettings.StepVolume(1));
            else if (keyboard.downArrowKey.wasPressedThisFrame) guide.Reply(GameSettings.StepVolume(-1));
        }

        void Handle(VoiceOutcome outcome)
        {
            LastHeard = outcome.Transcript;
            var phrase = VoicePhrases.Normalize(outcome.Transcript);
            Debug.Log($"[VoiceGame] heard \"{outcome.Transcript}\" calls={outcome.Calls.Count} confidence={outcome.Confidence:F2} {outcome.Milliseconds} ms phase={guide.Phase} error={outcome.Error ?? "none"}");

            if (phrase.Length == 0)
            {
                guide.Reply("didnt_catch");
                return;
            }
            if (Metas.TryGetValue(phrase, out var meta))
            {
                Run(meta);
                return;
            }
            if (GameSettings.TryVoice(phrase, false, out var volumeLine))
            {
                guide.Reply(volumeLine);
                return;
            }
            if (!game.AcceptsOrders && guide.Phase == GuidePhase.Playing)
            {
                guide.Reply("say_restart");
                return;
            }

            commands.Clear();
            if (Words.TryGetValue(phrase, out var order)) commands.Add(new HeroCommand(order));
            else if (guide.Phase == GuidePhase.Playing || outcome.Confidence >= SureConfidence)
                foreach (var call in outcome.Calls) AddCall(call);

            if (commands.Count == 0)
            {
                if (guide.Phase == GuidePhase.WaitingToStart) guide.StartGame();
                else guide.Reply("not_command");
                return;
            }
            var startedNow = guide.EnsurePlaying();
            var reply = ReplyFor(commands[0]);
            if (reply != null)
            {
                lastReply = reply;
                if (startedNow) narrator.Say(reply);
                else narrator.SayNow(reply);
            }
            if (commands.Count > 1 && commands[0].IsMove) guide.SuppressNextArrival = true;
            hero.Give(commands);
        }

        void Run(Meta meta)
        {
            switch (meta)
            {
                case Meta.Help:
                    guide.HelpWithRules();
                    break;
                case Meta.Sounds:
                    guide.HelpWithSounds();
                    break;
                case Meta.Status:
                    if (guide.Phase == GuidePhase.Playing) guide.SayStatus();
                    else guide.Reply("waiting_hint");
                    break;
                case Meta.Start:
                    if (guide.Phase == GuidePhase.Paused) guide.ResumeGame();
                    else if (guide.Phase != GuidePhase.Playing) guide.StartGame();
                    break;
                case Meta.Restart:
                    guide.StartGame("restart");
                    break;
                case Meta.Volume:
                    guide.Reply(GameSettings.VolumeLine);
                    if (guide.Phase == GuidePhase.Playing || guide.Phase == GuidePhase.WaitingToStart) narrator.Say("volume_how");
                    break;
                case Meta.Menu:
                    narrator.SayNow("going_to_menu");
                    enabled = false;
                    Invoke(nameof(LeaveForMenu), MenuDelaySeconds);
                    break;
                case Meta.Repeat:
                    if (guide.Phase == GuidePhase.Playing) narrator.SayNow(lastReply);
                    else guide.BeginBriefing();
                    break;
            }
        }

        void LeaveForMenu() => MenuReturn.GoToMenu();

        void AddCall(ToolCall call)
        {
            switch (call.Name)
            {
                case "walk_to": commands.Add(new HeroCommand(HeroOrder.WalkTo, call.Place)); break;
                case "run_to": commands.Add(new HeroCommand(HeroOrder.RunTo, call.Place)); break;
                case "jump": commands.Add(new HeroCommand(HeroOrder.Jump)); break;
                case "crouch": commands.Add(new HeroCommand(HeroOrder.Crouch)); break;
                case "stop": commands.Add(new HeroCommand(HeroOrder.Stop)); break;
            }
        }

        static string ReplyFor(HeroCommand command)
        {
            switch (command.Order)
            {
                case HeroOrder.RunTo: return "run_" + command.Place;
                case HeroOrder.WalkTo: return "walk_" + command.Place;
                case HeroOrder.Stop: return "stopping";
                case HeroOrder.Crouch: return "crouching";
                case HeroOrder.Stand: return "standing";
                default: return null;
            }
        }
    }
}
