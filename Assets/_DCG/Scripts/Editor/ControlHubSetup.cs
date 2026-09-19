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
    // Stage 2 replaces the stage 1 placeholder with the single unified map of doc 14 section 4.
    // The map is one continuous environment, not zones: a plaza that is at once Paul's stage and
    // Graves' open ground, a cover cluster on its west edge that serves Graves' detours, M416's
    // stances and Vendetta's collisions, a long lane running north for the M416 and the TRG, and
    // high ground to the south that is both Vendetta's flight destination and a TRG firing position.
    // No practice targets are placed here; the active module creates and removes its own set.
    public static class ControlHubSetup
    {
        const string Root = "Assets/_DCG/";
        public const string ScenePath = Root + "Scenes/ControlHub.unity";
        static Material floor, cover, teal, coral, line, blade, stage, accent;

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
                stage = Load<Material>("Materials/PaulFloor.mat");
                accent = Load<Material>("Materials/RifleAccent.mat");

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

                // UNIFIED MAP (doc 14, section 4).
                // Axes: X is east-west and carries Paul's stage, so screen right is always east
                // (doc 12, 1-1). Z is north-south and carries the long lane, which therefore never
                // lies along the fighting axis.
                //
                // Plaza: X -30..18, Z -16..16. Continuous ground, no internal walls.
                Cube("Plaza floor", new Vector3(-6, -.25f, 0), new Vector3(48, .5f, 32), floor, "NavigationSurface");
                Cube("West wall", new Vector3(-30.3f, 1.5f, 0), new Vector3(.6f, 3, 32), cover, "World");
                Cube("East wall", new Vector3(18.3f, 1.5f, 0), new Vector3(.6f, 3, 32), cover, "World");
                Cube("South wall", new Vector3(-6, 1.5f, -16.3f), new Vector3(48, 3, .6f), cover, "World");
                // The north wall is split: the gap between X -9 and 9 is the mouth of the lane.
                Cube("North wall west", new Vector3(-19.5f, 1.5f, 16.3f), new Vector3(21, 3, .6f), cover, "World");
                Cube("North wall east", new Vector3(13.5f, 1.5f, 16.3f), new Vector3(9, 3, .6f), cover, "World");

                // Long lane: Z 16..110, 18 m wide. TRG sight line and M416 firing line in one.
                Cube("Lane floor", new Vector3(0, -.25f, 63), new Vector3(18, .5f, 94), floor, "NavigationSurface");
                Cube("Lane west wall", new Vector3(-9.3f, 2, 63), new Vector3(.6f, 4, 94), cover, "World");
                Cube("Lane east wall", new Vector3(9.3f, 2, 63), new Vector3(.6f, 4, 94), cover, "World");
                Cube("Lane backstop", new Vector3(0, 3, 110.3f), new Vector3(18, 6, .6f), cover, "World");

                // Cover cluster, west half. These three are the ControlLab covers translated by -14 on X,
                // so Graves' detours and path replacements are the same shapes he had in his own lab.
                Cube("Cover A", new Vector3(-17, 1.1f, -1), new Vector3(2.5f, 2.2f, 5), cover, "World");
                Cube("Cover B", new Vector3(-11, 1.1f, 4), new Vector3(3, 2.2f, 2), cover, "World");
                Cube("Cover C", new Vector3(-7, 1.1f, -4), new Vector3(4, 2.2f, 2), cover, "World");

                // Cover at the lane mouth: the RifleLab set, for stance changes, leaning and corners.
                // They flank the mouth and leave the centre line clear for the TRG.
                Cube("Tall cover", new Vector3(-6, 1.5f, 13), new Vector3(3, 3, 2), cover, "World");
                Cube("Crouch cover", new Vector3(6, .6f, 13), new Vector3(4, 1.2f, 1), cover, "World");
                Cube("Low ceiling", new Vector3(10, 1.5f, 10), new Vector3(4, .3f, 5), cover, "World");

                // Vendetta's dash collision wall, at the VendettaLab offset from its spawn (+5, +9).
                Cube("Dash collision wall", new Vector3(11, 1.5f, 1), new Vector3(3, 3, 6), cover, "World");

                // High ground, south edge, opposite the lane. Two steps let a jumping class climb it;
                // the steps double as low cover. Top surface is y = 4.
                Cube("High ground step 1", new Vector3(0, .7f, -10), new Vector3(14, 1.4f, 1.5f), cover, "World");
                Cube("High ground step 2", new Vector3(0, 1.35f, -11.5f), new Vector3(14, 2.7f, 1.5f), cover, "World");
                Cube("High ground", new Vector3(0, 2, -14), new Vector3(14, 4, 4), cover, "World");

                // Floor markings only. Nothing below is geometry: the fighting stage is a change of
                // floor material, not a platform, and every marker sits on LocalViewModel so it is
                // neither an obstacle nor a nav surface.
                Marker("Fight stage floor", new Vector3(0, .02f, 0), new Vector3(14, .04f, 10), stage);
                Marker("Fight stage axis", new Vector3(0, .035f, 0), new Vector3(14, .04f, .08f), accent);
                Marker("Fight start west mark", new Vector3(-.85f, .035f, 0), new Vector3(.08f, .04f, 2), accent);
                Marker("Fight start east mark", new Vector3(.85f, .035f, 0), new Vector3(.08f, .04f, 2), accent);

                // The nav grid covers the plaza only, so its boundary is drawn on the floor. Graves
                // moves inside this rectangle; the lane beyond the mouth stripe is outside the grid.
                Marker("Grid boundary north", new Vector3(-6, .03f, 15.7f), new Vector3(47.4f, .04f, .15f), line);
                Marker("Grid boundary south", new Vector3(-6, .03f, -15.7f), new Vector3(47.4f, .04f, .15f), line);
                Marker("Grid boundary west", new Vector3(-29.7f, .03f, 0), new Vector3(.15f, .04f, 31.4f), line);
                Marker("Grid boundary east", new Vector3(17.7f, .03f, 0), new Vector3(.15f, .04f, 31.4f), line);
                Marker("Grid mouth stripe", new Vector3(0, .04f, 15.7f), new Vector3(18, .04f, .3f), accent);

                // Distance markers down the lane, every 10 m from the plaza centre, wider every 50 m.
                // Marker Z equals the distance from the origin, so bullet drop can be read off the floor.
                for (int z = 10; z <= 100; z += 10)
                    Marker("Range " + z + "m", new Vector3(0, .02f, z),
                        new Vector3(18, .04f, z % 50 == 0 ? .3f : .08f), accent);

                var grid = Load<GridGraph>("Data/Navigation/ControlHubGrid.asset");
                if (grid == null)
                {
                    grid = ScriptableObject.CreateInstance<GridGraph>();
                    AssetDatabase.CreateAsset(grid, Root + "Data/Navigation/ControlHubGrid.asset");
                }
                // The grid is baked over the plaza and its cover only. Baking to the end of the lane
                // would multiply the cell count for ground no ordered class ever walks on.
                grid.width = 96; grid.height = 64; grid.cellSize = .5f;
                grid.origin = new Vector3(-30, 0, -16); grid.agentRadius = .42f;
                grid.Bake(LayerMask.GetMask("World"), LayerMask.GetMask("NavigationSurface"));
                EditorUtility.SetDirty(grid);

                var cameraObject = new GameObject("Hub Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .04f, .065f);
                camera.nearClipPlane = .08f; camera.farClipPlane = 400;   // the lane reaches Z 110
                cameraObject.AddComponent<AudioListener>();

                // Map anchors. A module reads these instead of looking for geometry itself.
                var anchorRoot = new GameObject("Map anchors").transform;
                var anchors = new HubMapAnchors {
                    plazaCenter = Anchor("Plaza centre", Vector3.zero, anchorRoot),
                    fightWest = Anchor("Fight west", new Vector3(-.85f, 0, 0), anchorRoot),
                    fightEast = Anchor("Fight east", new Vector3(.85f, 0, 0), anchorRoot),
                    gravesSpawn = Anchor("Graves spawn", new Vector3(-23, 0, -6), anchorRoot),
                    vendettaSpawn = Anchor("Vendetta spawn", new Vector3(6, 0, -8), anchorRoot),
                    laneStart = Anchor("Lane firing line", new Vector3(0, 0, 12), anchorRoot),
                    laneEnd = Anchor("Lane end", new Vector3(0, 0, 110), anchorRoot),
                    highGround = Anchor("High ground top", new Vector3(0, 4, -14), anchorRoot)
                };

                var hubObject = new GameObject("Control Hub");
                var hub = hubObject.AddComponent<ControlHubController>();
                hub.world = world; hub.hubCamera = camera; hub.controls = controls;
                hub.map = anchors;
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
                Debug.Log("DCG_HUB_SETUP_OK: ControlHub generated with the unified map and 2 modules.");
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

        // A floor marking: visible, but not an obstacle and not a nav surface.
        static GameObject Marker(string name, Vector3 at, Vector3 size, Material material)
        {
            var go = Cube(name, at, size, material, "LocalViewModel");
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
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
