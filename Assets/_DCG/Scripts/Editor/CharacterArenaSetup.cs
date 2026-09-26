using System.Linq;
using DCG.Bootstrap.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DCG.Editor
{
    public static class CharacterArenaSetup
    {
        public const string ScenePath = "Assets/_DCG/Scenes/CharacterArena.unity";

        [MenuItem("DCG/Generate Character Arena")]
        public static void Generate()
        {
            var scene = EditorSceneManager.OpenScene(ControlHubSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var hub = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ControlHubController>()).Single();
                hub.ArenaMode = true;
                hub.EnemyAI = true;
                EditorSceneManager.SaveScene(scene, ScenePath, true);
                if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                    EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] {
                        new EditorBuildSettingsScene(ScenePath, true)
                    }).ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("DCG_CHARACTER_ARENA_READY");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
