using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace VoiceGame.EditorTools
{
    /// <summary>
    /// Builds the menu and audio edition scenes. The audio scene starts as a copy of VoiceGame.unity,
    /// which is only read, so the original game stays exactly as it is.
    /// </summary>
    public static class AudioEditionBuilder
    {
        const string Folder = "Assets/VoiceGame/AudioEdition";
        const string NarrationFolder = Folder + "/Narration";
        const string SourceScene = "Assets/VoiceGame/VoiceGame.unity";
        const string AudioScene = Folder + "/AudioGame.unity";
        const string MenuScene = Folder + "/Menu.unity";
        const string BuildSettingsFile = "ProjectSettings/EditorBuildSettings.asset";

        [MenuItem("Voice Game/Rebuild Audio Edition Scenes")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var lines = LoadLines();
            BuildAudioGame(lines);
            BuildMenu(lines);
            AddMissingScenesToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log("[VoiceGame] audio edition scenes built");
        }

        [InitializeOnLoadMethod]
        static void AddScenesAfterImport() => EditorApplication.delayCall += AddMissingScenesToBuild;

        static void AddMissingScenesToBuild()
        {
            if (!File.Exists(MenuScene) || !File.Exists(AudioScene)) return;
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == MenuScene) || !scenes.Exists(scene => scene.path == AudioScene))
            {
                scenes.RemoveAll(scene => scene.path == MenuScene || scene.path == AudioScene || scene.path == SourceScene);
                scenes.InsertRange(0, new[]
                {
                    new EditorBuildSettingsScene(MenuScene, true),
                    new EditorBuildSettingsScene(SourceScene, true),
                    new EditorBuildSettingsScene(AudioScene, true),
                });
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[VoiceGame] added the menu and audio game to the build scenes");
            }
            if (!File.ReadAllText(BuildSettingsFile).Contains(MenuScene)) AssetDatabase.SaveAssets();
        }

        static NarrationLine[] LoadLines()
        {
            var lines = new List<NarrationLine>();
            foreach (var raw in File.ReadAllLines(NarrationFolder + "/narration.txt"))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var split = raw.Split(new[] { '|' }, 2);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{NarrationFolder}/{split[0]}.wav");
                if (clip == null) throw new FileNotFoundException($"No recording for narration line {split[0]}; run Tools~/GenerateNarration.ps1");
                lines.Add(new NarrationLine { id = split[0], text = split[1], clip = clip });
            }
            return lines.ToArray();
        }

        static void BuildAudioGame(NarrationLine[] lines)
        {
            if (File.Exists(AudioScene))
            {
                File.Copy(SourceScene, AudioScene, true);
                AssetDatabase.ImportAsset(AudioScene, ImportAssetOptions.ForceSynchronousImport);
            }
            else if (!AssetDatabase.CopyAsset(SourceScene, AudioScene)) throw new IOException($"Could not copy {SourceScene}");
            var scene = EditorSceneManager.OpenScene(AudioScene, OpenSceneMode.Single);

            var game = Object.FindAnyObjectByType<SentinelGame>();
            var host = game.gameObject;
            Object.DestroyImmediate(host.GetComponent<GameHud>());
            Object.DestroyImmediate(host.GetComponent<CommandRouter>());
            game.asleepSeconds = new Vector2(8f, 12f);

            var mic = host.GetComponent<MicPushToTalk>();
            mic.talkKey = Key.Space;
            var hero = host.GetComponent<HeroCommander>();
            var brain = host.GetComponent<VoiceBrain>();

            var narrator = host.AddComponent<Narrator>();
            narrator.lines = lines;
            narrator.voice = Source(host, "Voice", 1f);

            var guide = host.AddComponent<AudioGuide>();
            guide.narrator = narrator;
            guide.game = game;
            guide.hero = hero;
            guide.mic = mic;
            guide.brain = brain;
            guide.cueSource = Source(host, "EyeCue", 1f);
            guide.earconSource = Source(host, "Earcon", 1f);

            var commands = host.AddComponent<AudioCommands>();
            commands.brain = brain;
            commands.hero = hero;
            commands.game = game;
            commands.guide = guide;
            commands.narrator = narrator;

            var hud = host.AddComponent<AudioHud>();
            hud.guide = guide;
            hud.narrator = narrator;
            hud.mic = mic;
            hud.brain = brain;
            hud.commands = commands;

            EditorSceneManager.SaveScene(scene, AudioScene);
        }

        static void BuildMenu(NarrationLine[] lines)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var view = new GameObject("Camera").AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
            view.gameObject.tag = "MainCamera";
            view.gameObject.AddComponent<AudioListener>();

            var host = new GameObject("Menu");
            var brain = host.AddComponent<VoiceBrain>();
            var narrator = host.AddComponent<Narrator>();
            narrator.lines = lines;
            narrator.voice = Source(host, "Voice", 1f);
            var mic = host.AddComponent<HandsFreeMic>();
            mic.brain = brain;
            mic.narrator = narrator;
            var menu = host.AddComponent<GameMenu>();
            menu.brain = brain;
            menu.narrator = narrator;
            menu.mic = mic;

            EditorSceneManager.SaveScene(scene, MenuScene);
        }

        static AudioSource Source(GameObject host, string name, float volume)
        {
            var child = new GameObject(name);
            child.transform.SetParent(host.transform);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.priority = 0;
            return source;
        }
    }
}
