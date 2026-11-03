using System.Collections.Generic;
using UnityEngine;

namespace VoiceGame
{
    /// <summary>
    /// Decides what a recognized utterance means. Short fixed words are matched on the
    /// transcript first, because Needle is unreliable on one-word commands; anything else
    /// uses the tool calls Needle produced.
    /// </summary>
    public sealed class CommandRouter : MonoBehaviour
    {
        static readonly char[] Punctuation = { ' ', '.', ',', '!', '?', '"', '\'' };

        static readonly Dictionary<string, HeroOrder> Words = new Dictionary<string, HeroOrder>
        {
            ["stop"] = HeroOrder.Stop,
            ["halt"] = HeroOrder.Stop,
            ["freeze"] = HeroOrder.Stop,
            ["wait"] = HeroOrder.Stop,
            ["hold on"] = HeroOrder.Stop,
            ["jump"] = HeroOrder.Jump,
            ["hop"] = HeroOrder.Jump,
            ["crouch"] = HeroOrder.Crouch,
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

        readonly List<HeroCommand> commands = new List<HeroCommand>();

        public string LastHeard { get; private set; } = "";
        public string LastAction { get; private set; } = "";
        public long LastMilliseconds { get; private set; }

        void OnEnable() => brain.Recognized += Handle;

        void OnDisable() => brain.Recognized -= Handle;

        void Handle(VoiceOutcome outcome)
        {
            LastHeard = outcome.Transcript;
            LastMilliseconds = outcome.Milliseconds;
            var phrase = outcome.Transcript.Trim(Punctuation).ToLowerInvariant();
            Debug.Log($"[VoiceGame] heard \"{outcome.Transcript}\" calls={outcome.Calls.Count} confidence={outcome.Confidence:F2} {outcome.Milliseconds} ms error={outcome.Error ?? "none"}");

            if (phrase.Length == 0)
            {
                LastAction = "Didn't catch that";
                return;
            }
            if (phrase == "restart" || phrase == "try again")
            {
                LastAction = "restart";
                game.Restart();
                return;
            }
            if (!game.AcceptsOrders)
            {
                LastAction = "Say \"restart\" to play again";
                return;
            }

            commands.Clear();
            if (Words.TryGetValue(phrase, out var order)) commands.Add(new HeroCommand(order));
            else foreach (var call in outcome.Calls) AddCall(call);

            if (commands.Count == 0)
            {
                LastAction = "Not a command";
                return;
            }
            LastAction = Describe();
            hero.Give(commands);
        }

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

        string Describe()
        {
            var parts = new List<string>();
            foreach (var command in commands) parts.Add(command.Place == null ? command.Order.ToString() : $"{command.Order} {command.Place}");
            return string.Join(", then ", parts);
        }
    }
}
