using UnityEngine;

namespace VoiceGame
{
    /// <summary>Large captions and the eye's state, for low-vision players and anyone watching.</summary>
    public sealed class AudioHud : MonoBehaviour
    {
        public AudioGuide guide;
        public Narrator narrator;
        public MicPushToTalk mic;
        public VoiceBrain brain;
        public AudioCommands commands;

        GUIStyle caption;
        GUIStyle banner;
        int builtForHeight;

        void OnGUI()
        {
            if (caption == null || builtForHeight != Screen.height)
            {
                builtForHeight = Screen.height;
                caption = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 28), wordWrap = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                caption.normal.textColor = Color.white;
                banner = new GUIStyle(caption) { fontSize = Mathf.Max(16, Screen.height / 22) };
            }

            var unit = Screen.height / 40f;
            string title;
            Color color;
            switch (guide.CurrentCue)
            {
                case "green":
                    title = "GREEN - SAFE TO MOVE";
                    color = new Color(0.05f, 0.45f, 0.15f);
                    break;
                case "yellow":
                    title = "YELLOW - SAY STOP";
                    color = new Color(0.85f, 0.6f, 0f);
                    break;
                case "red":
                    title = "RED - DO NOT MOVE";
                    color = new Color(0.8f, 0.05f, 0.05f);
                    break;
                default:
                    title = guide.Phase == GuidePhase.Playing ? "ROUND OVER" : "LISTEN";
                    color = new Color(0.1f, 0.1f, 0.15f);
                    break;
            }
            var top = new Rect(Screen.width * 0.1f, unit * 0.5f, Screen.width * 0.8f, unit * 4f);
            Panel(top, color);
            GUI.Label(top, title, banner);

            var bottom = new Rect(Screen.width * 0.05f, Screen.height - unit * 13f, Screen.width * 0.9f, unit * 12.5f);
            Panel(bottom, new Color(0f, 0f, 0f, 0.85f));
            string text;
            if (!brain.Ready) text = brain.LoadError == null ? narrator.Caption : "Could not load the voice models: " + brain.LoadError;
            else if (mic.Listening) text = "Listening...";
            else if (narrator.Caption.Length > 0) text = narrator.Caption;
            else if (commands.LastHeard.Length > 0) text = $"Heard: \"{commands.LastHeard}\"";
            else text = "Hold the space bar and speak";
            GUI.Label(bottom, text, caption);
        }

        static void Panel(Rect box, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
