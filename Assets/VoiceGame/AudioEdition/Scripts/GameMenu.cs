using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace VoiceGame
{
    /// <summary>
    /// Chooses which game to play and holds the settings screen. It speaks its options when it
    /// opens and accepts a spoken answer with no key, the keyboard, or the mouse, so every
    /// player can use it.
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        public const string VoiceScene = "VoiceGame";
        public const string AudioScene = "AudioGame";
        const float UnclearCooldownSeconds = 5f;
        const float SliderAnnounceDelaySeconds = 0.35f;

        public VoiceBrain brain;
        public Narrator narrator;
        public HandsFreeMic mic;

        GUIStyle title;
        GUIStyle body;
        GUIStyle button;
        GUIStyle sliderTrack;
        GUIStyle sliderThumb;
        int builtForHeight;
        string pendingScene;
        float nextUnclear;
        float announceAt;

        public bool InSettings { get; private set; }

        void OnEnable() => brain.Recognized += Handle;

        void OnDisable() => brain.Recognized -= Handle;

        void Start()
        {
            MenuReturn.Ensure();
            SpeakOptions();
        }

        void SpeakOptions()
        {
            narrator.Stop();
            narrator.Say("menu_welcome");
            narrator.Say("menu_option1");
            narrator.Say("menu_option2");
            narrator.Say("menu_option3");
            narrator.Say("menu_how");
        }

        void SpeakSettings()
        {
            narrator.Stop();
            narrator.Say("settings_title");
            narrator.Say(GameSettings.VolumeLine);
            narrator.Say("volume_how");
            narrator.Say("settings_back_how");
        }

        public void OpenSettings()
        {
            InSettings = true;
            SpeakSettings();
        }

        public void CloseSettings()
        {
            InSettings = false;
            announceAt = 0f;
            SpeakOptions();
        }

        void Update()
        {
            if (pendingScene != null)
            {
                if (!narrator.Speaking) SceneManager.LoadScene(pendingScene);
                return;
            }
            if (announceAt > 0f && Time.unscaledTime >= announceAt)
            {
                announceAt = 0f;
                narrator.SayNow(GameSettings.VolumeLine);
            }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (InSettings) UpdateSettingsKeys(keyboard);
            else UpdateMenuKeys(keyboard);
        }

        void UpdateMenuKeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) Choose(1);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) Choose(2);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) OpenSettings();
            else if (keyboard.rKey.wasPressedThisFrame) SpeakOptions();
        }

        void UpdateSettingsKeys(Keyboard keyboard)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) Step(1);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) Step(-1);
            else if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) CloseSettings();
            else if (keyboard.rKey.wasPressedThisFrame) SpeakSettings();
        }

        void Step(int direction) => narrator.SayNow(GameSettings.StepVolume(direction));

        void Handle(VoiceOutcome outcome)
        {
            if (pendingScene != null) return;
            var phrase = VoicePhrases.Normalize(outcome.Transcript);
            Debug.Log($"[VoiceGame] menu heard \"{outcome.Transcript}\" {outcome.Milliseconds} ms");
            if (phrase.Length == 0) return;
            if (InSettings) HandleSettings(phrase);
            else HandleMenu(phrase);
        }

        void HandleMenu(string phrase)
        {
            switch (phrase)
            {
                case "one":
                case "1":
                case "won":
                case "option one":
                case "option 1":
                case "first":
                case "voice":
                case "voice control":
                    Choose(1);
                    break;
                case "two":
                case "2":
                case "to":
                case "too":
                case "option two":
                case "option 2":
                case "second":
                case "audio":
                case "audio game":
                    Choose(2);
                    break;
                case "three":
                case "3":
                case "tree":
                case "free":
                case "option three":
                case "option 3":
                case "third":
                case "settings":
                case "setting":
                case "options":
                    OpenSettings();
                    break;
                case "repeat":
                case "again":
                case "say that again":
                    SpeakOptions();
                    break;
                default:
                    if (GameSettings.TryVoice(phrase, false, out var line)) narrator.SayNow(line);
                    else Unclear("menu_unclear");
                    break;
            }
        }

        void HandleSettings(string phrase)
        {
            switch (phrase)
            {
                case "back":
                case "go back":
                case "menu":
                case "main menu":
                case "done":
                case "exit":
                case "return":
                    CloseSettings();
                    break;
                case "repeat":
                case "again":
                case "say that again":
                case "volume":
                    SpeakSettings();
                    break;
                default:
                    if (GameSettings.TryVoice(phrase, true, out var line)) narrator.SayNow(line);
                    else Unclear("settings_unclear");
                    break;
            }
        }

        void Unclear(string line)
        {
            if (Time.unscaledTime < nextUnclear) return;
            nextUnclear = Time.unscaledTime + UnclearCooldownSeconds;
            narrator.SayNow(line);
        }

        public void Choose(int option)
        {
            if (pendingScene != null) return;
            pendingScene = option == 1 ? VoiceScene : AudioScene;
            narrator.SayNow(option == 1 ? "menu_start1" : "menu_start2");
        }

        void OnGUI()
        {
            var unit = Screen.height / 40f;
            if (title == null || builtForHeight != Screen.height)
            {
                builtForHeight = Screen.height;
                title = new GUIStyle(GUI.skin.label) { fontSize = Screen.height / 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                title.normal.textColor = Color.white;
                body = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(13, Screen.height / 34), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                body.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
                button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(14, Screen.height / 30), wordWrap = true, padding = new RectOffset(20, 20, 8, 8) };
                sliderTrack = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = unit * 2.4f };
                sliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = unit * 2.4f, fixedWidth = unit * 2.4f };
            }

            var width = Screen.width * 0.74f;
            var left = (Screen.width - width) / 2f;
            if (InSettings) DrawSettings(left, width, unit);
            else DrawMenu(left, width, unit);

            string status;
            if (mic.Problem != null) status = mic.Problem;
            else if (!brain.Ready) status = "Loading voice models...";
            else if (narrator.Caption.Length > 0) status = narrator.Caption;
            else if (mic.Hearing) status = "Hearing you...";
            else if (brain.Thinking) status = "Thinking...";
            else status = "Listening for your answer";
            GUI.Label(new Rect(left, unit * 32f, width, unit * 7f), status, body);
        }

        void DrawMenu(float left, float width, float unit)
        {
            GUI.Label(new Rect(0f, unit * 1f, Screen.width, unit * 5f), "SENTINEL", title);
            GUI.Label(new Rect(left, unit * 5.8f, width, unit * 2.5f), "Say \"one\", \"two\" or \"three\", press 1, 2 or 3, or click", body);
            if (GUI.Button(new Rect(left, unit * 8.8f, width, unit * 8f), "1.  Voice Control\nWatch the screen and command the hero by voice. For players who cannot use a keyboard or controller.", button)) Choose(1);
            if (GUI.Button(new Rect(left, unit * 17.5f, width, unit * 8f), "2.  Audio Game\nEverything is told through sound, so no screen is needed. For blind players.", button)) Choose(2);
            if (GUI.Button(new Rect(left, unit * 26.2f, width, unit * 4.8f), "3.  Settings", button)) OpenSettings();
        }

        void DrawSettings(float left, float width, float unit)
        {
            GUI.Label(new Rect(0f, unit * 1f, Screen.width, unit * 5f), "SETTINGS", title);
            GUI.Label(new Rect(left, unit * 6.5f, width, unit * 4f), $"Volume: {GameSettings.VolumePercent}%", title);
            var picked = GUI.HorizontalSlider(new Rect(left, unit * 12f, width, unit * 2.4f), GameSettings.VolumePercent, GameSettings.MinVolumePercent, GameSettings.MaxVolumePercent, sliderTrack, sliderThumb);
            var snapped = GameSettings.Snap(Mathf.RoundToInt(picked));
            if (snapped != GameSettings.VolumePercent)
            {
                GameSettings.SetVolumePercent(snapped);
                announceAt = Time.unscaledTime + SliderAnnounceDelaySeconds;
            }
            var half = (width - unit) / 2f;
            if (GUI.Button(new Rect(left, unit * 16f, half, unit * 5f), "Quieter", button)) Step(-1);
            if (GUI.Button(new Rect(left + half + unit, unit * 16f, half, unit * 5f), "Louder", button)) Step(1);
            if (GUI.Button(new Rect(left, unit * 22f, width, unit * 4.5f), "Back to menu", button)) CloseSettings();
            GUI.Label(new Rect(left, unit * 27f, width, unit * 4.5f), "Say \"louder\", \"quieter\", \"volume fifty\" or \"back\", or use the arrow keys", body);
        }
    }

    /// <summary>
    /// Started by the menu and kept across scenes, so Escape returns to the menu from either
    /// game without any change to the games themselves.
    /// </summary>
    public sealed class MenuReturn : MonoBehaviour
    {
        public const string MenuScene = "Menu";

        static MenuReturn instance;

        public static void Ensure()
        {
            if (instance != null) return;
            var holder = new GameObject("MenuReturn");
            DontDestroyOnLoad(holder);
            instance = holder.AddComponent<MenuReturn>();
        }

        public static void GoToMenu() => SceneManager.LoadScene(MenuScene);

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (SceneManager.GetActiveScene().name != MenuScene) GoToMenu();
        }
    }
}
