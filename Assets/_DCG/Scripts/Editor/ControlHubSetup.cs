using System;
using System.IO;
using System.Linq;
using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Navigation;
using DCG.Classes.Vendetta;
using DCG.Bootstrap.Hub;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace DCG.Editor
{
    // Generates ControlHub.unity: one scene that holds every control.
    // Stage 1 builds the skeleton and a PLACEHOLDER map. The single unified map that has to satisfy
    // all five classes at once is stage 2 (doc 14, section 4); nothing here is final geometry.
    public static class ControlHubSetup
    {
        const string Root = "Assets/_DCG/";
        public const string ScenePath = Root + "Scenes/ControlHub.unity";
        static Material floor, cover, teal, coral, line, blade;

        [MenuItem("DCG/Generate Control Hub")]
        public static void Generate()
        {
            ProjectSetup.ValidateRestoration();
            foreach (string dir in new[] { "Scenes", "Data/Navigation", "Materials" })
                Directory.CreateDirectory(Root + dir);
            AssetDatabase.Refresh();

            var previous = SceneManager.GetActiveScene();
            var mode = Application.isBatchMode && string.IsNullOrEmpty(previous.path)
                ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                floor = Load<Material>("Materials/Floor.mat");
                cover = Load<Material>("Materials/Cover.mat");
                teal = Load<Material>("Materials/Player.mat");
                coral = Load<Material>("Materials/Target.mat");
                line = Load<Material>("Materials/Line.mat");
                blade = Load<Material>("Materials/VendettaBlade.mat");

                var gravesTuning = Load<PrototypeTuning>("Data/Classes/Graves_TuningPending.asset");
                var vendettaTuning = Load<VendettaTuning>("Data/Classes/Vendetta_TuningPending.asset");
                var vendettaCommon = Load<PrototypeTuning>("Data/Classes/Vendetta_Common.asset");
                var gravesPrefab = Load<GameObject>("Prefabs/Actors/Graves.prefab");
                var vendettaPrefab = Load<GameObject>("Prefabs/Actors/Vendetta.prefab");
                var controls = Load<InputActionAsset>("Input/DCGControls.inputactions");

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.6f, .66f, .73f);
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.5f;
                sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(50, -30, 0);

                var world = new GameObject("Hub Simulation").AddComponent<SimulationWorld>();

                // PLACEHOLDER MAP. Enough for both stage 1 controls to be exercised: open ground and
                // cover for Graves pathing, a wall and a raised platform for Vendetta dash and flight.
                Cube("Placeholder ground", new Vector3(0, -.25f, 0), new Vector3(32, .5f, 24), floor, "NavigationSurface");
                Cube("North boundary", new Vector3(0, 1.5f, 12), new Vector3(32, 3, .6f), cover, "World");
                Cube("South boundary", new Vector3(0, 1.5f, -12), new Vector3(32, 3, .6f), cover, "World");
                Cube("East boundary", new Vector3(16, 1.5f, 0), new Vector3(.6f, 3, 24), cover, "World");
                Cube("West boundary", new Vector3(-16, 1.5f, 0), new Vector3(.6f, 3, 24), cover, "World");
                Cube("Cover A", new Vector3(-3, 1.1f, -1), new Vector3(2.5f, 2.2f, 5), cover, "World");
                Cube("Cover B", new Vector3(3, 1.1f, 4), new Vector3(3, 2.2f, 2), cover, "World");
                Cube("Cover C", new Vector3(7, 1.1f, -4), new Vector3(4, 2.2f, 2), cover, "World");
                Cube("Dash collision wall", new Vector3(11, 1.5f, 2), new Vector3(3, 3, 6), cover, "World");
                Cube("Flight platform", new Vector3(-10, 1.5f, 6), new Vector3(5, 3, 5), cover, "World");

                var grid = Load<GridGraph>("Data/Navigation/ControlHubGrid.asset");
                if (grid == null)
                {
                    grid = ScriptableObject.CreateInstance<GridGraph>();
                    AssetDatabase.CreateAsset(grid, Root + "Data/Navigation/ControlHubGrid.asset");
                }
                grid.width = 64; grid.height = 48; grid.cellSize = .5f;
                grid.origin = new Vector3(-16, 0, -12); grid.agentRadius = .42f;
                grid.Bake(LayerMask.GetMask("World"), LayerMask.GetMask("NavigationSurface"));
                EditorUtility.SetDirty(grid);

                var cameraObject = new GameObject("Hub Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .04f, .065f);
                camera.nearClipPlane = .08f; camera.farClipPlane = 300;
                cameraObject.AddComponent<AudioListener>();

                var spawns = new GameObject("Spawns").transform;
                var playerSpawn = Anchor("Player spawn", new Vector3(-9, 0, -6), spawns);
                var targets = new[] {
                    Anchor("Target spawn A", new Vector3(9, 0, 5), spawns),
                    Anchor("Target spawn B", new Vector3(-8, 0, 1), spawns),
                    Anchor("Target spawn C", new Vector3(2, 0, -1), spawns)
                };

                var hubObject = new GameObject("Control Hub");
                var hub = hubObject.AddComponent<ControlHubController>();
                hub.world = world; hub.hubCamera = camera; hub.controls = controls;
                hub.playerSpawn = playerSpawn; hub.targetSpawns = targets;
                hub.selectScreen = hubObject.AddComponent<ClassSelectScreen>();
                hub.hud = hubObject.AddComponent<HubHud>();

                var gravesHost = new GameObject("Graves module");
                gravesHost.transform.SetParent(hubObject.transform, false);
                var graves = gravesHost.AddComponent<GravesModule>();
                graves.actorPrefab = gravesPrefab; graves.tuning = gravesTuning; graves.grid = grid;
                graves.playerMaterial = teal; graves.targetMaterial = coral; graves.lineMaterial = line;

                var vendettaHost = new GameObject("Vendetta module");
                vendettaHost.transform.SetParent(hubObject.transform, false);
                var vendetta = vendettaHost.AddComponent<VendettaModule>();
                vendetta.actorPrefab = vendettaPrefab; vendetta.tuning = vendettaTuning;
                vendetta.common = vendettaCommon; vendetta.playerMaterial = teal;
                vendetta.targetMaterial = coral; vendetta.bladeMaterial = blade;

                hub.modules = new ControlModuleBase[] { graves, vendetta };

                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                    EditorBuildSettings.scenes = EditorBuildSettings.scenes
                        .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
                Debug.Log("DCG_HUB_SETUP_OK: ControlHub generated with placeholder map and 2 modules.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static T Load<T>(string relative) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + relative);
            if (asset == null && !relative.Contains("ControlHubGrid"))
                throw new InvalidOperationException("Missing asset: " + Root + relative +
                    ". Run DCG/Generate Control Lab and the per-class lab generators first.");
            return asset;
        }

        static Transform Anchor(string name, Vector3 at, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            return go.transform;
        }

        static GameObject Cube(string name, Vector3 at, Vector3 size, Material material, string layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.position = at; go.transform.localScale = size;
            go.layer = LayerMask.NameToLayer(layer);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        [MenuItem("DCG/Build Windows Control Hub")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/ControlHub");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/ControlHub/DCG-ControlHub.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Control Hub build failed.");
            Debug.Log("DCG_HUB_BUILD_OK");
        }
    }
}
