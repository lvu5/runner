using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InfiniteRunner.Editor
{
    [InitializeOnLoad]
    internal static class InfiniteRunnerProjectSetup
    {
        private const string SceneDirectory = "Assets/InfiniteRunner/Scenes";
        private const string MainScenePath = SceneDirectory + "/Main.unity";
        private const string WebTemplate = "PROJECT:InfiniteRunnerPose";
        private const string WebBuildDirectory = "Builds/Web";

        static InfiniteRunnerProjectSetup()
        {
            EditorApplication.delayCall += EnsureProjectIsReady;
        }

        [MenuItem("Infinite Runner/Open Main Scene", priority = 1)]
        private static void OpenMainScene()
        {
            EnsureMainSceneExists();
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Infinite Runner/Project Setup", priority = 20)]
        private static void RunSetupFromMenu()
        {
            EnsureProjectIsReady();
            Debug.Log("Infinite Runner project setup is complete. Open Main and press Play.");
        }

        [MenuItem("Infinite Runner/Build & Run Web Game", priority = 30)]
        public static void BuildWebGame()
        {
            EnsureProjectIsReady();
            Directory.CreateDirectory(WebBuildDirectory);

            string[] scenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = WebBuildDirectory,
                target = BuildTarget.WebGL,
                options = Application.isBatchMode ? BuildOptions.None : BuildOptions.AutoRunPlayer
            };

            if (System.Environment.GetCommandLineArgs().Contains("-cleanWebBuild"))
                options.options |= BuildOptions.CleanBuildCache;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException(
                    $"Web build failed with {report.summary.totalErrors} error(s). See the Unity Console for details.");
            }

            Debug.Log($"Web game built successfully at {Path.GetFullPath(WebBuildDirectory)}");
        }

        private static void EnsureProjectIsReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            EnsureMainSceneExists();
            EnsureMainSceneIsInBuild();

            PlayerSettings.companyName = "Runner Lab";
            PlayerSettings.productName = "Saigon Infinite Runner";
            PlayerSettings.WebGL.template = WebTemplate;

            Scene activeScene = SceneManager.GetActiveScene();
            bool looksLikeUntitledStartupScene =
                string.IsNullOrEmpty(activeScene.path) &&
                (!activeScene.isDirty || activeScene.rootCount <= 2);

            if (looksLikeUntitledStartupScene)
            {
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }
        }

        private static void EnsureMainSceneExists()
        {
            if (File.Exists(MainScenePath))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(SceneDirectory))
            {
                AssetDatabase.CreateFolder("Assets/InfiniteRunner", "Scenes");
            }

            Scene originalScene = SceneManager.GetActiveScene();
            Scene generatedScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(generatedScene, MainScenePath);

            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
            }

            EditorSceneManager.CloseScene(generatedScene, true);
            AssetDatabase.Refresh();
        }

        private static void EnsureMainSceneIsInBuild()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool alreadyPresent = scenes.Any(scene => scene.path == MainScenePath && scene.enabled);
            if (alreadyPresent)
            {
                return;
            }

            EditorBuildSettingsScene[] updatedScenes = scenes
                .Where(scene => scene.path != MainScenePath)
                .Concat(new[] { new EditorBuildSettingsScene(MainScenePath, true) })
                .ToArray();

            EditorBuildSettings.scenes = updatedScenes;
        }
    }
}
