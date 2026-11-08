#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PerfectParcel.Editor
{
    /// <summary>
    /// Автоматичний скрипт збірки для Game-CI GitHub Actions.
    /// Дозволяє збирати APK (тестування) та AAB (Google Play Store Release)
    /// у headless режимі з опціональним автоматичним підписом Keystore.
    /// </summary>
    public static class BuildScript
    {
        private static readonly string[] BuildScenes = { "Assets/_Project/Scenes/PrototypeScene.unity" };

        public static void BuildAndroidApk()
        {
            PerformBuild(isAppBundle: false, "build/Android/PerfectParcel.apk");
        }

        public static void BuildAndroidAll()
        {
            PerformBuild(isAppBundle: false, "build/Android/PerfectParcel.apk");
            PerformBuild(isAppBundle: true, "build/Android/PerfectParcel.aab");
        }

        public static void BuildAndroidAab()
        {
            PerformBuild(isAppBundle: true, "build/Android/PerfectParcel.aab");
        }

        private const string ScenePath = "Assets/_Project/Scenes/PrototypeScene.unity";

        /// <summary>
        /// Сцени немає в репозиторії — без неї BuildPlayer падає. Створюємо мінімальну сцену
        /// з PrototypeBootstrapper (він сам будує камеру, конвеєр, UI та менеджери).
        /// </summary>
        private static void EnsureBuildScene()
        {
            if (File.Exists(ScenePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            var go = new GameObject("PrototypeBootstrapper");
            go.AddComponent<PerfectParcel.Core.PrototypeBootstrapper>();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[BuildScript] Сцену {ScenePath} згенеровано автоматично.");
        }

        private static void PerformBuild(bool isAppBundle, string outputPath)
        {
            Directory.CreateDirectory("build/Android");
            EnsureBuildScene();

            // Загальні налаштування збірки під вимоги Google Play 2026
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
            EditorUserBuildSettings.buildAppBundle = isAppBundle;

            // Налаштування підпису Keystore, якщо передано змінні середовища
            bool signed = ConfigureKeystoreSigning();
            if (isAppBundle && !signed && Environment.GetEnvironmentVariable("ALLOW_UNSIGNED_AAB") != "true")
            {
                // AAB, підписаний debug-ключем, Google Play відхиляє — краще впасти одразу, ніж видати хибний «Release»
                Debug.LogError("[BuildScript] Release AAB потребує Keystore (ANDROID_KEYSTORE_*). Задайте секрети або ALLOW_UNSIGNED_AAB=true для тестової збірки.");
                EditorApplication.Exit(1);
                return;
            }

            var buildOptions = new BuildPlayerOptions
            {
                scenes = BuildScenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            string buildType = isAppBundle ? "Android App Bundle (AAB)" : "APK";
            Debug.Log($"[BuildScript] Початок автоматичної збірки {buildType}...");
            var report = BuildPipeline.BuildPlayer(buildOptions);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] Збірка {buildType} успішна! Розмір: {summary.totalSize} байт, Час: {summary.totalTime}");
            }
            else
            {
                Debug.LogError($"[BuildScript] Збірка {buildType} завершилася помилкою: {summary.result}, Помилок: {summary.totalErrors}");
                EditorApplication.Exit(1);
            }
        }

        private static bool ConfigureKeystoreSigning()
        {
            string keystoreBase64 = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_BASE64");
            string keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            string keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
            string keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");

            if (!string.IsNullOrEmpty(keystoreBase64) && !string.IsNullOrEmpty(keystorePass))
            {
                try
                {
                    string keystorePath = Path.Combine(Directory.GetCurrentDirectory(), "release.keystore");
                    byte[] keystoreBytes = Convert.FromBase64String(keystoreBase64);
                    File.WriteAllBytes(keystorePath, keystoreBytes);

                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = keystorePath;
                    PlayerSettings.Android.keystorePass = keystorePass;
                    PlayerSettings.Android.keyaliasName = !string.IsNullOrEmpty(keyaliasName) ? keyaliasName : "perfectparcel";
                    PlayerSettings.Android.keyaliasPass = !string.IsNullOrEmpty(keyaliasPass) ? keyaliasPass : keystorePass;

                    Debug.Log("[BuildScript] Keystore успішно згенеровано з секретів та підключено до збірки.");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BuildScript] Помилка налаштування Keystore: {ex.Message}. Збірка продовжиться без кастомного підпису.");
                    return false;
                }
            }
            else
            {
                Debug.Log("[BuildScript] Змінні Keystore не виявлені. Збірка виконується з дефолтним Debug-підписом.");
                return false;
            }
        }
    }
}
#endif
