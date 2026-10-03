using System;
using System.Linq;
using System.Reflection;
using Game.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Applies the project-wide settings the spec requires: portrait orientation, product name, Input System,
    /// and a URP asset with the 2D renderer. Safe to run more than once.
    /// Batch mode: <c>Unity -batchmode -quit -projectPath . -executeMethod Game.Editor.ProjectSetup.RunBatch</c>
    /// </summary>
    public static class ProjectSetup
    {
        private const string UrpAssetPath = EditorPaths.Settings + "/URP-2D.asset";
        private const string Renderer2DPath = EditorPaths.Settings + "/URP-2D-Renderer.asset";
        private const string UrpAssetType = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";
        private const string Renderer2DType = "UnityEngine.Rendering.Universal.Renderer2DData";

        // PlayerSettings "activeInputHandler": 0 = old Input Manager, 1 = Input System, 2 = both.
        private const int InputHandlerBoth = 2;
        private const int InputHandlerOld = 0;

        /// <summary>Menu entry.</summary>
        [MenuItem(EditorPaths.MenuRoot + "Setup Project", priority = 0)]
        public static void RunMenu()
        {
            Run();
            Debug.Log("[Game] Project settings applied. If Unity asks to restart for the Input System, choose Yes.");
        }

        /// <summary>Entry point for <c>-executeMethod</c>.</summary>
        public static void RunBatch() => Run();

        private static void Run()
        {
            PlayerSettings.productName = GameInfo.Title;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToPortrait = true;

            EnableInputSystem();
            SetupUrp2D();
            AssetDatabase.SaveAssets();
        }

        private static void EnableInputSystem()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var settings = new SerializedObject(assets[0]);
            var handler = settings.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue != InputHandlerOld) return;

            // "Both" keeps the debug IMGUI overlay and any legacy input working alongside the Input System.
            handler.intValue = InputHandlerBoth;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupUrp2D()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                Debug.Log($"[Game] Render pipeline already set ({GraphicsSettings.defaultRenderPipeline.name}); leaving it.");
                return;
            }

            // Looked up by name so this script compiles even if the URP package layout changes between versions.
            var urpType = FindType(UrpAssetType);
            var rendererType = FindType(Renderer2DType);
            if (urpType == null || rendererType == null)
            {
                Debug.LogWarning("[Game] URP 2D types not found. Is com.unity.render-pipelines.universal installed?");
                return;
            }

            try
            {
                SceneBuilder.EnsureFolder(EditorPaths.Settings);
                var rendererData = ScriptableObject.CreateInstance(rendererType);
                AssetDatabase.CreateAsset(rendererData, Renderer2DPath);

                var create = urpType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "Create" && m.GetParameters().Length == 1);
                var pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { rendererData });
                AssetDatabase.CreateAsset(pipeline, UrpAssetPath);

                GraphicsSettings.defaultRenderPipeline = pipeline;
                var currentLevel = QualitySettings.GetQualityLevel();
                for (var level = 0; level < QualitySettings.names.Length; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    QualitySettings.renderPipeline = null; // Use the default pipeline on every quality level.
                }
                QualitySettings.SetQualityLevel(currentLevel, false);
                Debug.Log("[Game] Created and assigned URP asset with the 2D renderer.");
            }
            catch (Exception e)
            {
                Debug.LogError("[Game] Automatic URP 2D setup failed: " + e.Message +
                               "\nSee PROGRESS.md > Manual URP setup for the two-step fallback.");
            }
        }

        private static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);
    }
}
