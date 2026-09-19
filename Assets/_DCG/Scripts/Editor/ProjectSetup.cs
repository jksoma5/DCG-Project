using System;
using System.IO;
using System.Linq;
using DCG.Core;
using DCG.Gameplay;
using DCG.Gameplay.Navigation;
using DCG.Classes.Graves;
using DCG.Presentation;
using DCG.Bootstrap;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DCG.Editor
{
    public static class ProjectSetup
    {
        const string Root = "Assets/_DCG/";
        static Material floor, wall, teal, coral, dark, line;
        [MenuItem("DCG/Generate Control Lab")]
        public static void Generate()
        {
            EnsureLayers();
            ValidateRestoration();
            foreach (string dir in new[] { "Scenes", "Data/Classes", "Data/Weapons", "Data/Movement", "Data/Cameras",
                "Data/Actions", "Data/Navigation", "Prefabs/Actors", "Prefabs/Cameras", "Prefabs/Weapons", "Prefabs/UI",
                "Materials", "Input", "Scripts/Presentation/Audio" })
                Directory.CreateDirectory(Root + dir);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            floor = Material("Floor", new Color(.055f,.085f,.12f));
            wall = Material("Cover", new Color(.16f,.23f,.29f));
            teal = Material("Player", new Color(.2f,.88f,.73f));
            coral = Material("Target", new Color(.98f,.37f,.3f));
            dark = Material("Weapon", new Color(.055f,.07f,.09f));
            line = Material("Line", new Color(.16f,.38f,.4f), true);
            var tuning = Asset<PrototypeTuning>("Data/Classes/Graves_TuningPending.asset");
            var grid = Asset<GridGraph>("Data/Navigation/ControlLabGrid.asset");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.6f,.67f,.74f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(50,-30,0);
            var world = new GameObject("SimulationWorld").AddComponent<SimulationWorld>();
            Cube("Navigation floor", new Vector3(0,-.25f,0), new Vector3(32,.5f,24), floor, "NavigationSurface");
            Cube("North boundary", new Vector3(0,1,12), new Vector3(32,2,.6f), wall, "World");
            Cube("South boundary", new Vector3(0,1,-12), new Vector3(32,2,.6f), wall, "World");
            Cube("East boundary", new Vector3(16,1,0), new Vector3(.6f,2,24), wall, "World");
            Cube("West boundary", new Vector3(-16,1,0), new Vector3(.6f,2,24), wall, "World");
            Cube("Cover A", new Vector3(-3,1.1f,-1), new Vector3(2.5f,2.2f,5), wall, "World");
            Cube("Cover B", new Vector3(3,1.1f,4), new Vector3(3,2.2f,2), wall, "World");
            Cube("Cover C", new Vector3(7,1.1f,-4), new Vector3(4,2.2f,2), wall, "World");
            for (int x = -15; x <= 15; x += 2)
                Line("Grid x", new Vector3(x,.01f,-11.7f), new Vector3(x,.01f,11.7f), .012f, line);
            for (int z = -11; z <= 11; z += 2)
                Line("Grid z", new Vector3(-15.7f,.01f,z), new Vector3(15.7f,.01f,z), .012f, line);
            grid.width = 64; grid.height = 48; grid.cellSize = .5f;
            grid.origin = new Vector3(-16,0,-12); grid.agentRadius = .42f;
            grid.Bake(LayerMask.GetMask("World"), LayerMask.GetMask("NavigationSurface"));
            EditorUtility.SetDirty(grid);

            var player = Actor("Graves", 1, 0, new Vector3(-9,0,-6), tuning, grid, true);
            var moving = Actor("Moving target", 2, 1, new Vector3(9,0,5), tuning, grid, true);
            Actor("Close target", 3, 1, new Vector3(-8,0,1), tuning, grid, false);
            Actor("Covered target", 4, 1, new Vector3(2,0,-1), tuning, grid, false);
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(player.gameObject, Root + "Prefabs/Actors/Graves.prefab");
            var cameraObject = new GameObject("Local TopDown Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.04f,.065f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 150;
            cameraObject.AddComponent<AudioListener>();
            var rig = cameraObject.AddComponent<TopDownRig>(); rig.target = player.transform; rig.tuning = tuning;
            cameraObject.transform.position = player.transform.position + tuning.cameraOffset;
            cameraObject.transform.rotation = Quaternion.LookRotation(-tuning.cameraOffset);
            camera.fieldOfView = tuning.cameraFov;
            var input = cameraObject.AddComponent<GravesInputReader>();
            input.actor = player; input.worldCamera = camera;
            input.controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "Input/DCGControls.inputactions");
            if (input.controls == null) throw new InvalidOperationException("Input asset import failed.");
            var overlay = cameraObject.AddComponent<DebugOverlay>(); overlay.actor = player; overlay.worldCamera = camera;
            var lab = new GameObject("ControlLab").AddComponent<LabController>();
            lab.world = world; lab.player = player; lab.movingTarget = moving; lab.input = input; lab.overlay = overlay;
            lab.pathLine = Line("Active path", Vector3.zero, Vector3.zero, .055f, teal);
            lab.pathLine.positionCount = 0;
            lab.gameObject.AddComponent<LabSmokeProbe>();
            EditorSceneManager.SaveScene(scene, Root + "Scenes/ControlLab.unity");

            foreach (ClassId id in Enum.GetValues(typeof(ClassId)))
            {
                var def = Asset<ClassDefinition>("Data/Classes/" + id + ".asset");
                if (id != ClassId.Graves && def.actorPrefab != null) continue;
                def.classId = id; def.referenceStatus = ReferenceStatus.TuningPending;
                def.playableInLab = id == ClassId.Graves;
                def.referenceGame = id == ClassId.Graves ? "League of Legends / Graves" :
                    id == ClassId.Vendetta ? "Overwatch / Vendetta, third-person adaptation" :
                    id == ClassId.Rifle ? "PUBG / M416" : "Sudden Attack / TRG";
                def.actorPrefab = id == ClassId.Graves ? playerPrefab : null;
                def.prototypeTuning = id == ClassId.Graves ? tuning : null;
                def.pendingNotes = id == ClassId.Graves ?
                    "Lab-only attack timing, ammo, damage and camera tuning. Skills and original-game comparison pending." :
                    "Disabled: original input/action specification and implementation pending. Do not infer missing mechanics.";
                EditorUtility.SetDirty(def);
            }
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<BootstrapEntry>();
            EditorSceneManager.SaveScene(scene, Root + "Scenes/Bootstrap.unity");
            EditorBuildSettings.RemoveConfigObject("com.unity.input.settings.actions");
            var additionalScenes = EditorBuildSettings.scenes.Where(s => s.path != Root + "Scenes/Bootstrap.unity" && s.path != Root + "Scenes/ControlLab.unity" && s.path != "Assets/Scenes/SampleScene.unity");
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(Root + "Scenes/Bootstrap.unity", true),
                new EditorBuildSettingsScene(Root + "Scenes/ControlLab.unity", true)
            }.Concat(additionalScenes).ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Root + "Scenes/ControlLab.unity");
            Debug.Log("DCG_SETUP_OK: ControlLab generated; input, render pipeline and grid validated.");
        }
        static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, Root + path); return asset;
        }
        static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP shader missing.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .15f);
            EditorUtility.SetDirty(material); return material;
        }
        static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, string layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = position; go.transform.localScale = scale;
            go.layer = LayerMask.NameToLayer(layer); go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static LineRenderer Line(string name, Vector3 from, Vector3 to, float width, Material material)
        {
            var renderer = new GameObject(name).AddComponent<LineRenderer>();
            renderer.sharedMaterial = material; renderer.positionCount = 2;
            renderer.SetPosition(0,from); renderer.SetPosition(1,to);
            renderer.startWidth = renderer.endWidth = width; renderer.useWorldSpace = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }
        static ActorSimulation Actor(string name, uint id, int team, Vector3 at, PrototypeTuning tuning, GridGraph grid, bool orders)
        {
            var root = new GameObject(name); root.transform.position = at; root.layer = LayerMask.NameToLayer("CharacterBody");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .35f; controller.center = new Vector3(0,.9f,0);
            controller.skinWidth = .025f; controller.stepOffset = .2f;
            root.AddComponent<CharacterMotor>();
            var actor = root.AddComponent<ActorSimulation>();
            actor.actorNumber = id; actor.team = team; actor.tuning = tuning; actor.classId = ClassId.Graves;
            if (orders) root.AddComponent<GravesOrderController>().grid = grid;
            var hit = new GameObject("Hurtbox"); hit.transform.SetParent(root.transform, false);
            hit.layer = LayerMask.NameToLayer("Hurtbox");
            var capsule = hit.AddComponent<CapsuleCollider>(); capsule.height = 1.8f;
            capsule.radius = .36f; capsule.center = new Vector3(0,.9f,0); capsule.isTrigger = true;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Presentation";
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false); body.transform.localPosition = Vector3.up * .9f;
            body.transform.localScale = new Vector3(.7f,.85f,.7f);
            body.GetComponent<Renderer>().sharedMaterial = team == 0 ? teal : coral;
            var weapon = Cube("Weapon view", at + new Vector3(.32f,1,.45f), new Vector3(.18f,.18f,.85f), dark, "LocalViewModel");
            UnityEngine.Object.DestroyImmediate(weapon.GetComponent<Collider>());
            weapon.transform.SetParent(root.transform, true);
            var view = root.AddComponent<ActorView>(); view.actor = actor; view.body = body.GetComponent<Renderer>();
            view.tracer = Line("Shot tracer", Vector3.zero, Vector3.zero, .055f, team == 0 ? teal : coral);
            view.tracer.transform.SetParent(root.transform, true); view.tracer.enabled = false;
            return actor;
        }
        static void EnsureLayers()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            string[] names = { "World", "CharacterBody", "Hurtbox", "Projectile", "NavigationSurface", "LocalViewModel" };
            foreach (var name in names)
            {
                if (LayerMask.NameToLayer(name) >= 0) continue;
                bool added = false;
                for (int index = 8; index < 32; index++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
                    { layers.GetArrayElementAtIndex(index).stringValue = name; added = true; break; }
                if (!added) throw new InvalidOperationException("No free layer for " + name);
                settings.ApplyModifiedProperties();
            }
            foreach (var name in new[] { "Hurtbox", "Projectile", "LocalViewModel" })
            {
                int layer = LayerMask.NameToLayer(name);
                for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(layer, i, true);
            }
        }
        public static void ValidateRestoration()
        {
            if (GraphicsSettings.defaultRenderPipeline == null)
                throw new InvalidOperationException("URP asset was not restored.");
            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions") == null)
                throw new InvalidOperationException("Original input asset not restored.");
            Debug.Log("DCG_RESTORATION_OK");
        }
        [MenuItem("DCG/Bake Control Lab Grid")]
        public static void BakeGrid()
        {
            var grid = AssetDatabase.LoadAssetAtPath<GridGraph>(Root + "Data/Navigation/ControlLabGrid.asset");
            if (grid == null || SceneManager.GetActiveScene().path != Root + "Scenes/ControlLab.unity")
                throw new InvalidOperationException("Open ControlLab before baking.");
            grid.Bake(LayerMask.GetMask("World"), LayerMask.GetMask("NavigationSurface"));
            EditorUtility.SetDirty(grid); AssetDatabase.SaveAssets();
        }
        [MenuItem("DCG/Build Windows Control Lab")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/ControlLab");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Root + "Scenes/Bootstrap.unity", Root + "Scenes/ControlLab.unity" },
                locationPathName = "Builds/ControlLab/DCG-ControlLab.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("DCG player build failed: " + report.summary.result);
            Debug.Log("DCG_BUILD_OK");
        }
    }
}
