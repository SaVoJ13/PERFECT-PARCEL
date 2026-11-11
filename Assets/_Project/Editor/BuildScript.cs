using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KebabStreet.Editor
{
    /// <summary>
    /// Збірка Android для CI (game-ci/unity-builder, параметр buildMethod).
    /// Перед збіркою застосовує налаштування проєкту кодом (<see cref="ProjectSetup"/>).
    /// </summary>
    public static class BuildScript
    {
        private const string DefaultApkPath = "build/Android/KebabStreet.apk";
        private const string DefaultAabPath = "build/Android/KebabStreet.aab";

        /// <summary>Збирає APK. Викликається з CI: -executeMethod KebabStreet.Editor.BuildScript.BuildAndroidApk</summary>
        public static void BuildAndroidApk()
        {
            Run(buildApk: true, buildAab: false);
        }

        /// <summary>Збирає APK і AAB (формат Google Play) за один запуск редактора.</summary>
        public static void BuildAndroidApkAndAab()
        {
            Run(buildApk: true, buildAab: true);
        }

        private static void Run(bool buildApk, bool buildAab)
        {
            int exitCode = 1;
            try
            {
                bool ok = true;
                if (buildApk)
                {
                    ok = Build(GetArgument("-customBuildPath") ?? DefaultApkPath, false);
                }

                if (ok && buildAab)
                {
                    ok = Build(DefaultAabPath, true);
                }

                exitCode = ok ? 0 : 1;
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

        private static bool Build(string outputPath, bool appBundle)
        {
            ProjectSetup.ApplyAll();
            EditorUserBuildSettings.buildAppBundle = appBundle;

            // Сцени Boot/Menu/Shift генеруються кодом перед кожною збіркою (E0-05) і прибираються після неї.
            string[] scenes = SceneSetup.Generate();

            try
            {
                bool customSigning = ConfigureSigning();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                Debug.Log($"[BuildScript] Збірка {(appBundle ? "AAB" : "APK")} -> {outputPath}; сцен: {scenes.Length}; власний підпис: {customSigning}");

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
                if (!appBundle && summary.result == BuildResult.Succeeded)
                {
                    LogScenesInApk(outputPath, scenes.Length);
                }

                return summary.result == BuildResult.Succeeded;
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SceneSetup.Cleanup();

                // Дані підпису не повинні лишатися в ProjectSettings.asset (псевдонім ключа — секрет CI).
                ProjectSetup.ClearAndroidSigning();
                ProjectSetup.RelocateUrpGeneratedAssets();
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Доказ у журналі збірки: файли сцен (assets/bin/Data/levelN) у готовому APK і їх кількість.
        /// </summary>
        private static void LogScenesInApk(string apkPath, int expected)
        {
            try
            {
                int count = 0;
                using (ZipArchive zip = ZipFile.OpenRead(apkPath))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        if (entry.FullName.StartsWith("assets/bin/Data/level", StringComparison.Ordinal)
                            && !entry.FullName.EndsWith(".resS", StringComparison.Ordinal)
                            && !entry.FullName.EndsWith(".resource", StringComparison.Ordinal))
                        {
                            count++;
                            Debug.Log("[BuildScript] Сцена в APK: " + entry.FullName);
                        }
                    }
                }

                Debug.Log($"[BuildScript] Сцен в APK (файли levelN): {count}; очікувалось: {expected}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[BuildScript] Не вдалося перелічити сцени в APK: " + exception.Message);
            }
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
                ProjectSetup.ClearAndroidSigning();
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
