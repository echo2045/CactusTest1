using System.Collections.Generic;
using System.IO;
using Synty.AnimationBaseLocomotion.Samples;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VoiceGame.EditorTools
{
    public static class VoiceGameBuilder
    {
        const string Root = "Assets/VoiceGame";
        const string ScenePath = Root + "/VoiceGame.unity";
        const string MaterialFolder = Root + "/Materials";
        const string PlayerPrefab = "Assets/Synty/AnimationBaseLocomotion/Samples/Prefabs/PF_PolygonPlayer.prefab";
        const string HeroTexture = "Assets/Synty/AnimationBaseLocomotion/Samples/Textures/T_Polygon_Dummy_01.png";
        const float RiverZ = 2f;

        static readonly Vector3 StartAt = new Vector3(0f, 0f, -24f);
        static readonly Vector3 ChestAt = new Vector3(-13f, 0f, -8f);
        static readonly Vector3 TowerAt = new Vector3(14f, 0f, -5f);
        static readonly Vector3 BridgeAt = new Vector3(0f, 0f, RiverZ);
        static readonly Vector3 GateAt = new Vector3(0f, 0f, 22f);

        [MenuItem("Voice Game/Rebuild Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(MaterialFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var world = new GameObject("World").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(world);
            ground.transform.localScale = new Vector3(14f, 1f, 14f);
            ground.GetComponent<Renderer>().sharedMaterial = GroundLit();

            var bark = Lit("Bark", new Color(0.3f, 0.2f, 0.12f));
            var leaves = Lit("Leaves", new Color(0.16f, 0.38f, 0.17f));
            PlantTrees(world, bark, leaves);

            var wood = Lit("Wood", new Color(0.42f, 0.27f, 0.14f));
            var stone = Lit("Stone", new Color(0.45f, 0.46f, 0.5f));
            var water = Lit("Water", new Color(0.13f, 0.38f, 0.62f));
            var gold = Lit("Gold", new Color(1f, 0.78f, 0.15f), new Color(1f, 0.65f, 0.1f) * 1.5f);
            var eyeMaterial = Lit("Eye", new Color(0.1f, 0.5f, 0.2f), Color.black);
            var pad = Lit("StartPad", new Color(0.3f, 0.34f, 0.4f));

            Prop("River", world, PrimitiveType.Cube, new Vector3(0f, 0.005f, RiverZ), new Vector3(140f, 0.01f, 6f), water);
            var bridgeRoot = Marker("Bridge", world, BridgeAt);
            Prop("Deck", bridgeRoot, PrimitiveType.Cube, new Vector3(0f, 0.015f, RiverZ), new Vector3(5f, 0.02f, 9f), wood);
            Prop("RailLeft", bridgeRoot, PrimitiveType.Cube, new Vector3(-2.4f, 0.5f, RiverZ), new Vector3(0.15f, 1f, 9f), wood);
            Prop("RailRight", bridgeRoot, PrimitiveType.Cube, new Vector3(2.4f, 0.5f, RiverZ), new Vector3(0.15f, 1f, 9f), wood);

            var startRoot = Marker("Start", world, StartAt);
            Prop("Pad", startRoot, PrimitiveType.Cylinder, StartAt + Vector3.up * 0.01f, new Vector3(5f, 0.01f, 5f), pad);

            var chestRoot = Marker("Chest", world, ChestAt);
            Prop("Box", chestRoot, PrimitiveType.Cube, ChestAt + Vector3.up * 0.5f, new Vector3(2.2f, 1f, 1.4f), wood);
            Prop("Lid", chestRoot, PrimitiveType.Cube, ChestAt + Vector3.up * 1.1f, new Vector3(2.3f, 0.2f, 1.5f), gold);
            var key = new GameObject("Key");
            key.transform.SetParent(chestRoot);
            key.transform.position = ChestAt + Vector3.up * 3f;
            Prop("Ring", key.transform, PrimitiveType.Cylinder, key.transform.position + Vector3.up * 0.35f, new Vector3(0.5f, 0.05f, 0.5f), gold, Quaternion.Euler(90f, 0f, 0f));
            Prop("Shaft", key.transform, PrimitiveType.Cube, key.transform.position - Vector3.up * 0.15f, new Vector3(0.12f, 0.8f, 0.12f), gold);
            Prop("Tooth", key.transform, PrimitiveType.Cube, key.transform.position + new Vector3(0.15f, -0.45f, 0f), new Vector3(0.25f, 0.12f, 0.12f), gold);

            var towerRoot = Marker("Tower", world, TowerAt);
            Prop("Column", towerRoot, PrimitiveType.Cylinder, TowerAt + Vector3.up * 3.5f, new Vector3(3f, 3.5f, 3f), stone);
            Prop("Cap", towerRoot, PrimitiveType.Cylinder, TowerAt + Vector3.up * 7.2f, new Vector3(4f, 0.2f, 4f), stone);
            var eye = Prop("Eye", towerRoot, PrimitiveType.Sphere, TowerAt + Vector3.up * 8.4f, new Vector3(2f, 0.4f, 2f), eyeMaterial);
            var beam = new GameObject("Beam").AddComponent<Light>();
            beam.transform.SetParent(towerRoot);
            beam.transform.position = TowerAt + Vector3.up * 8.4f;
            beam.type = LightType.Spot;
            beam.range = 80f;
            beam.spotAngle = 16f;
            beam.intensity = 400f;
            beam.shadows = LightShadows.None;
            beam.enabled = false;

            var gateRoot = Marker("Gate", world, GateAt);
            Prop("PostLeft", gateRoot, PrimitiveType.Cube, GateAt + new Vector3(-3.5f, 3f, 1f), new Vector3(1.2f, 6f, 1.2f), stone);
            Prop("PostRight", gateRoot, PrimitiveType.Cube, GateAt + new Vector3(3.5f, 3f, 1f), new Vector3(1.2f, 6f, 1.2f), stone);
            Prop("Lintel", gateRoot, PrimitiveType.Cube, GateAt + new Vector3(0f, 6.4f, 1f), new Vector3(8.4f, 1f, 1.2f), stone);
            Prop("WallLeft", gateRoot, PrimitiveType.Cube, GateAt + new Vector3(-37f, 1.75f, 1f), new Vector3(66f, 3.5f, 1f), stone);
            Prop("WallRight", gateRoot, PrimitiveType.Cube, GateAt + new Vector3(37f, 1.75f, 1f), new Vector3(66f, 3.5f, 1f), stone);
            var door = Prop("Door", gateRoot, PrimitiveType.Cube, GateAt + new Vector3(0f, 2.9f, 1f), new Vector3(5.8f, 5.8f, 0.5f), wood);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            player.transform.position = StartAt;
            player.name = "Hero";
            var animationController = player.GetComponentInChildren<SamplePlayerAnimationController>();
            Override(animationController, "_alwaysStrafe", property => property.boolValue = false);
            player.GetComponentInChildren<CharacterController>().minMoveDistance = 0f;
            var heroMaterial = HeroLit();
            foreach (var renderer in player.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                for (var i = 0; i < slots.Length; i++) slots[i] = heroMaterial;
                renderer.sharedMaterials = slots;
            }
            var cameraController = player.GetComponentInChildren<SampleCameraController>();
            Override(cameraController, "_cameraDistance", property => property.floatValue = 10f);
            Override(cameraController, "_cameraHeightOffset", property => property.floatValue = 5f);
            Override(cameraController, "_cameraTiltOffset", property => property.floatValue = 22f);
            Override(cameraController, "_hideCursor", property => property.boolValue = false);
            Override(cameraController, "_mouseSensitivity", property => property.floatValue = 0f);

            var manager = new GameObject("VoiceGame");
            var brain = manager.AddComponent<VoiceBrain>();
            var mic = manager.AddComponent<MicPushToTalk>();
            var hero = manager.AddComponent<HeroCommander>();
            var game = manager.AddComponent<SentinelGame>();
            var router = manager.AddComponent<CommandRouter>();
            var hud = manager.AddComponent<GameHud>();

            mic.brain = brain;
            mic.talkKey = Key.V;
            hero.input = player.GetComponentInChildren<InputReader>();
            hero.cameraController = cameraController;
            hero.controller = player.GetComponentInChildren<CharacterController>();
            hero.bridge = bridgeRoot;
            hero.riverZ = RiverZ;
            hero.landmarks = new[]
            {
                new Landmark { name = "chest", point = chestRoot },
                new Landmark { name = "tower", point = towerRoot },
                new Landmark { name = "bridge", point = bridgeRoot },
                new Landmark { name = "gate", point = gateRoot },
            };
            game.hero = hero;
            game.start = startRoot;
            game.chest = chestRoot;
            game.tower = towerRoot;
            game.gate = gateRoot;
            game.key = key;
            game.gateDoor = door.gameObject;
            game.eye = eye.GetComponent<Renderer>();
            game.eyeLight = beam;
            router.brain = brain;
            router.hero = hero;
            router.game = game;
            hud.brain = brain;
            hud.mic = mic;
            hud.router = router;
            hud.game = game;
            hud.hero = hero;
            hud.view = player.GetComponentInChildren<Camera>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[VoiceGame] scene built at " + ScenePath);
        }

        static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var existing in EditorBuildSettings.scenes)
                if (existing.path != ScenePath) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void Override(Object target, string field, System.Action<SerializedProperty> set)
        {
            var serialized = new SerializedObject(target);
            set(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Marker(string name, Transform parent, Vector3 position)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent);
            marker.position = position;
            return marker;
        }

        static Transform Prop(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            var prop = GameObject.CreatePrimitive(type);
            prop.name = name;
            Object.DestroyImmediate(prop.GetComponent<Collider>());
            prop.transform.SetParent(parent);
            prop.transform.position = position;
            prop.transform.rotation = rotation ?? Quaternion.identity;
            prop.transform.localScale = scale;
            prop.GetComponent<Renderer>().sharedMaterial = material;
            return prop.transform;
        }

        static void PlantTrees(Transform parent, Material bark, Material leaves)
        {
            var random = new System.Random(7);
            var root = new GameObject("Trees").transform;
            root.SetParent(parent);
            for (var i = 0; i < 46; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var position = new Vector3(side * (22f + (float)random.NextDouble() * 38f), 0f, -44f + (float)random.NextDouble() * 62f);
                if (Mathf.Abs(position.z - RiverZ) < 5f) continue;
                var height = 3f + (float)random.NextDouble() * 3f;
                var crown = height * 0.9f;
                Prop("Trunk", root, PrimitiveType.Cylinder, position + Vector3.up * height * 0.5f, new Vector3(0.6f, height * 0.5f, 0.6f), bark);
                Prop("Crown", root, PrimitiveType.Sphere, position + Vector3.up * height, new Vector3(crown, crown, crown), leaves);
            }
        }

        static Material GroundLit()
        {
            var path = MaterialFolder + "/GroundChecker.png";
            if (!File.Exists(path))
            {
                var light = new Color(0.36f, 0.52f, 0.27f);
                var dark = new Color(0.32f, 0.47f, 0.24f);
                var checker = new Texture2D(2, 2, TextureFormat.RGB24, false);
                checker.SetPixels(new[] { light, dark, dark, light });
                File.WriteAllBytes(path, checker.EncodeToPNG());
                Object.DestroyImmediate(checker);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            var material = Lit("Ground", Color.white);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", new Vector2(70f, 70f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material HeroLit()
        {
            var material = Lit("Hero", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(HeroTexture));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Lit(string name, Color color, Color? emission = null)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
