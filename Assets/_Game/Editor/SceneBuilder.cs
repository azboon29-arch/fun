using Game.App;
using Game.Data;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Generates the Boot, Base and Battle scenes and registers them in Build Settings.
    /// Scenes are generated output: re-running this overwrites them, so add scene content through this builder.
    /// Batch mode: <c>Unity -batchmode -quit -projectPath . -executeMethod Game.Editor.SceneBuilder.BuildScenesBatch</c>
    /// </summary>
    public static class SceneBuilder
    {
        private const float CameraSize = 9.6f; // Half of the 19.2 world-unit portrait height at 100 pixels per unit.
        private static readonly Color BootBackground = new Color(0.08f, 0.09f, 0.12f);
        private static readonly Color BaseBackground = new Color(0.23f, 0.33f, 0.22f);
        private static readonly Color BattleBackground = new Color(0.36f, 0.28f, 0.2f);

        /// <summary>Menu entry: builds all scenes and opens Boot.</summary>
        [MenuItem(EditorPaths.MenuRoot + "Build Scenes", priority = 1)]
        public static void BuildScenesMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot));
            Debug.Log("[Game] Scenes built. Press Play in the Boot scene (or any scene: it boots first).");
        }

        /// <summary>Entry point for <c>-executeMethod</c>.</summary>
        public static void BuildScenesBatch() => BuildAll();

        private static void BuildAll()
        {
            EnsureFolder(EditorPaths.Scenes);
            var config = LoadOrCreateConfig();

            BuildBoot(config);
            BuildPlaceholder(SceneNames.Base, BaseBackground, "Base\n(merge board arrives in Phase 2)");
            BuildPlaceholder(SceneNames.Battle, BattleBackground, "Battle\n(arrives in Phase 4)");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath(SceneNames.Boot), true),
                new EditorBuildSettingsScene(ScenePath(SceneNames.Base), true),
                new EditorBuildSettingsScene(ScenePath(SceneNames.Battle), true),
            };
            AssetDatabase.SaveAssets();
        }

        /// <summary>Loads the GameConfig asset, creating it with default values if missing.</summary>
        public static GameConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(EditorPaths.GameConfig);
            if (config != null) return config;

            EnsureFolder(EditorPaths.Data);
            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, EditorPaths.GameConfig);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void BuildBoot(GameConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(BootBackground);

            var boot = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(boot);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Save(scene, SceneNames.Boot);
        }

        private static void BuildPlaceholder(string name, Color background, string caption)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(background);
            new GameObject("Placeholder").AddComponent<ScenePlaceholder>().SetCaption(caption);
            Save(scene, name);
        }

        private static void CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void Save(Scene scene, string name)
        {
            if (!EditorSceneManager.SaveScene(scene, ScenePath(name)))
            {
                Debug.LogError($"[Game] Could not save scene {name}.");
            }
        }

        private static string ScenePath(string name) => $"{EditorPaths.Scenes}/{name}.unity";

        /// <summary>Creates an asset folder (and parents) if it does not exist.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
