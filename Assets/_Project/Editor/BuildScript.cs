using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KebabStreet.Editor
{
    /// <summary>
    /// Збірка Android для CI (game-ci/unity-builder, параметр buildMethod).
    /// Перед збіркою застосовує налаштування проєкту кодом (<see cref="ProjectSetup"/>).
    /// </summary>
    public static class BuildScript
    {
        private const string DefaultApkPath = "build/Android/KebabStreet.apk";

        // Тимчасова порожня сцена лише на час збірки; справжні сцени з'являться в E0-05.
        private const string PlaceholderScenePath = ProjectSetup.SettingsFolder + "/_BuildPlaceholderScene.unity";

        /// <summary>Збирає APK. Викликається з CI: -executeMethod KebabStreet.Editor.BuildScript.BuildAndroidApk</summary>
        public static void BuildAndroidApk()
        {
            int exitCode = 1;
            try
            {
                exitCode = Build(GetArgument("-customBuildPath") ?? DefaultApkPath) ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static bool Build(string outputPath)
        {
            ProjectSetup.ApplyAll();
            EditorUserBuildSettings.buildAppBundle = false;
            bool customSigning = ConfigureSigning();

            string[] scenes = GetEnabledScenes();
            bool usePlaceholder = scenes.Length == 0;
            if (usePlaceholder)
            {
                CreatePlaceholderScene();
                scenes = new[] { PlaceholderScenePath };
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                Debug.Log($"[BuildScript] Збірка APK -> {outputPath}; сцен: {scenes.Length}; власний підпис: {customSigning}");

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                });

                BuildSummary summary = report.summary;
                Debug.Log(
                    $"[BuildScript] Результат: {summary.result}; розмір: {summary.totalSize} байт; " +
                    $"помилок: {summary.totalErrors}; попереджень: {summary.totalWarnings}; час: {summary.totalTime}");
                return summary.result == BuildResult.Succeeded;
            }
            finally
            {
                if (usePlaceholder)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AssetDatabase.DeleteAsset(PlaceholderScenePath);
                }

                ProjectSetup.RelocateUrpGeneratedAssets();
                AssetDatabase.SaveAssets();
            }
        }

        private static string[] GetEnabledScenes()
        {
            var result = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && File.Exists(scene.path))
                {
                    result.Add(scene.path);
                }
            }

            return result.ToArray();
        }

        private static void CreatePlaceholderScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.12f, 0.10f);
            EditorSceneManager.SaveScene(scene, PlaceholderScenePath);
        }

        /// <summary>
        /// Підпис ключем із CI. game-ci розкодовує keystore у файл у корені проєкту і передає
        /// назву, псевдонім і паролі аргументами командного рядка та змінними середовища.
        /// Значення секретів ніколи не пишуться в журнал. Без ключа — підпис debug (лише для APK).
        /// </summary>
        private static bool ConfigureSigning()
        {
            string keystoreName = GetArgument("-androidKeystoreName") ?? Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_NAME");
            string keystorePass = GetArgument("-androidKeystorePass") ?? Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            string aliasName = GetArgument("-androidKeyaliasName") ?? Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
            string aliasPass = GetArgument("-androidKeyaliasPass") ?? Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");

            bool hasKeystore = !string.IsNullOrEmpty(keystoreName) && File.Exists(keystoreName);
            if (!hasKeystore || string.IsNullOrEmpty(keystorePass) || string.IsNullOrEmpty(aliasName) || string.IsNullOrEmpty(aliasPass))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.Log("[BuildScript] Ключ підпису не передано: APK підписується debug-ключем.");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(keystoreName);
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = aliasName;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            Debug.Log("[BuildScript] Підпис: ключ із секретів CI.");
            return true;
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name && !string.IsNullOrEmpty(args[i + 1]))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
