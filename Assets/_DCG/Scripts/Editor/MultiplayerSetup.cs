using System;
using System.IO;
using System.Linq;
using DCG.Bootstrap.Hub;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DCG.Editor
{
    public static class MultiplayerSetup
    {
        public const string ScenePath = "Assets/_DCG/Scenes/Multiplayer.unity";
        [MenuItem("DCG/Generate Multiplayer")]
        public static void Generate()
        {
            var scene = EditorSceneManager.OpenScene(ControlHubSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var hub = roots.SelectMany(root => root.GetComponentsInChildren<ControlHubController>()).Single();
                hub.ArenaMode = false; hub.NetworkMode = true; hub.EnemyAI = false;
                // Keep configuration and anchors. Geometry is built from the winning map at runtime.
                foreach (var root in roots)
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    foreach (var collider in root.GetComponentsInChildren<Collider>()) collider.enabled = false;
                }
                var multiplayer = hub.gameObject.AddComponent<MultiplayerController>();
                multiplayer.hub = hub;
                hub.gameObject.AddComponent<MultiplayerSmokeProbe>();
                hub.hubCamera.transform.SetPositionAndRotation(new Vector3(0, 24, -20), Quaternion.Euler(50, 0, 0));
                EditorSceneManager.SaveScene(scene, ScenePath, true);
                if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                    EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("DCG_MULTIPLAYER_READY");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [MenuItem("DCG/Build Windows Multiplayer")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/Multiplayer-NGO");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Multiplayer-NGO/DCG-Multiplayer.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Multiplayer build failed.");
            Debug.Log("DCG_MULTIPLAYER_BUILD_OK");
        }
    }
}
