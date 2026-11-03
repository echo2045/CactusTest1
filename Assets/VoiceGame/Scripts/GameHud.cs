using UnityEngine;

namespace VoiceGame
{
    public sealed class GameHud : MonoBehaviour
    {
        public VoiceBrain brain;
        public MicPushToTalk mic;
        public CommandRouter router;
        public SentinelGame game;
        public HeroCommander hero;
        public Camera view;

        GUIStyle text;
        GUIStyle centered;
        GUIStyle big;
        GUIStyle label;
        int builtForHeight;

        void OnGUI()
        {
            BuildStyles();
            var unit = Screen.height / 40f;

            foreach (var landmark in hero.landmarks) DrawLandmark(landmark, unit);

            if (!brain.Ready)
            {
                var status = brain.LoadError == null ? "Loading voice models..." : "Could not load the voice models: " + brain.LoadError;
                Panel(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.7f));
                GUI.Label(new Rect(0f, Screen.height * 0.45f, Screen.width, unit * 3f), status, centered);
                return;
            }

            DrawSentinel(unit);
            DrawGoal(unit);
            DrawCommands(unit);
            DrawVoice(unit);

            var message = game.ShownMessage;
            if (message.Length > 0)
            {
                var box = new Rect(Screen.width * 0.15f, Screen.height * 0.32f, Screen.width * 0.7f, unit * 3f);
                Panel(box, new Color(0f, 0f, 0f, 0.65f));
                GUI.Label(box, message, big);
            }
        }

        void DrawSentinel(float unit)
        {
            string title;
            Color color;
            if (game.Round != RoundState.Playing)
            {
                var won = game.Round == RoundState.Won;
                var banner = new Rect(Screen.width * 0.25f, unit * 0.5f, Screen.width * 0.5f, unit * 2.4f);
                Panel(banner, won ? new Color(0.75f, 0.55f, 0.05f) : new Color(0.4f, 0.05f, 0.05f));
                GUI.Label(banner, won ? "YOU ESCAPED" : "GAME OVER", big);
                return;
            }
            switch (game.Sentinel)
            {
                case SentinelState.Waking:
                    title = "THE EYE IS OPENING - SAY STOP";
                    color = new Color(0.85f, 0.6f, 0f);
                    break;
                case SentinelState.Watching:
                    title = "WATCHING - DON'T MOVE";
                    color = new Color(0.8f, 0.1f, 0.05f);
                    break;
                case SentinelState.Stunned:
                    title = $"JAMMED - {game.StateSecondsLeft:F0}s";
                    color = new Color(0.15f, 0.4f, 0.8f);
                    break;
                default:
                    title = "THE SENTINEL SLEEPS - MOVE";
                    color = new Color(0.1f, 0.5f, 0.2f);
                    break;
            }
            var box = new Rect(Screen.width * 0.25f, unit * 0.5f, Screen.width * 0.5f, unit * 2.4f);
            Panel(box, color);
            GUI.Label(box, title, big);
        }

        void DrawGoal(float unit)
        {
            var box = new Rect(unit * 0.5f, unit * 3.4f, Screen.width * 0.34f, unit * 4.6f);
            Panel(box, new Color(0f, 0f, 0f, 0.55f));
            var hidden = hero.Crouching ? "  (hidden)" : "";
            GUI.Label(Inset(box, unit), $"{game.Goal}\nLives: {game.Lives}   Key: {(game.HasKey ? "yes" : "no")}{hidden}\nTime: {game.ElapsedSeconds:F0}s", text);
        }

        void DrawCommands(float unit)
        {
            var width = Screen.width * 0.27f;
            var box = new Rect(Screen.width - width - unit * 0.5f, unit * 3.4f, width, unit * 10.4f);
            Panel(box, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(Inset(box, unit), "Try saying:\n\"Run to the chest\"\n\"Walk to the bridge\"\n\"Run to the chest and jump\"\n\"Jump\"\n\"Stop\"\n\"Crouch\" / \"Stand up\"\n\"Restart\"", text);
        }

        void DrawVoice(float unit)
        {
            var box = new Rect(Screen.width * 0.15f, Screen.height - unit * 6.4f, Screen.width * 0.7f, unit * 5.8f);
            Panel(box, new Color(0f, 0f, 0f, 0.65f));
            var inner = Inset(box, unit);

            var status = $"Hold V and speak   (M changes mic: {mic.DeviceName})";
            if (mic.Problem != null) status = mic.Problem;
            else if (mic.Listening) status = "Listening...";
            else if (brain.Thinking) status = "Thinking...";
            GUI.Label(new Rect(inner.x, inner.y, inner.width, unit * 1.6f), status, text);

            if (mic.Listening)
            {
                var meter = new Rect(inner.x + inner.width * 0.4f, inner.y + unit * 0.4f, inner.width * 0.6f, unit * 0.7f);
                Panel(meter, new Color(1f, 1f, 1f, 0.2f));
                Panel(new Rect(meter.x, meter.y, meter.width * mic.Level, meter.height), new Color(0.3f, 0.9f, 0.4f));
            }

            if (router.LastHeard.Length > 0 || router.LastAction.Length > 0)
            {
                var heard = $"Heard: \"{router.LastHeard}\"\n{router.LastAction}  ({router.LastMilliseconds} ms)";
                GUI.Label(new Rect(inner.x, inner.y + unit * 1.6f, inner.width, unit * 3f), heard, text);
            }
        }

        void DrawLandmark(Landmark landmark, float unit)
        {
            var world = landmark.point.position + Vector3.up * 3f;
            var screen = view.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            var box = new Rect(screen.x - unit * 4f, Screen.height - screen.y - unit, unit * 8f, unit * 1.8f);
            GUI.Label(box, landmark.name.ToUpperInvariant(), label);
        }

        static Rect Inset(Rect box, float amount) => new Rect(box.x + amount * 0.5f, box.y + amount * 0.3f, box.width - amount, box.height - amount * 0.6f);

        static void Panel(Rect box, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void BuildStyles()
        {
            if (text != null && builtForHeight == Screen.height) return;
            builtForHeight = Screen.height;
            var size = Mathf.Max(11, Screen.height / 42);
            text = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, alignment = TextAnchor.UpperLeft };
            text.normal.textColor = Color.white;
            centered = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter, fontSize = (int)(size * 1.3f) };
            big = new GUIStyle(centered) { fontStyle = FontStyle.Bold };
            label = new GUIStyle(centered) { fontStyle = FontStyle.Bold, fontSize = size };
            label.normal.textColor = new Color(1f, 0.95f, 0.6f);
        }
    }
}
