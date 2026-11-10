using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KebabStreet.Editor
{
    /// <summary>
    /// Налаштування проєкту кодом (headless-first): PlayerSettings для Android і конвеєр URP 2D.
    /// Усі методи ідемпотентні: повторний виклик нічого не змінює, якщо стан уже правильний.
    /// Значення продубльовано в docs/architecture/TECH_STACK.md.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ProductName = "Kebab Street";
        public const string CompanyName = "SaVoJ";

        // Робочий ідентифікатор до рішення D-08 (картка E0-03).
        public const string ApplicationId = "com.savoj.kebabstreet";

        // Unity 6.3 не підтримує API нижче 25 (docs.unity.com, Android requirements),
        // тому API 24 з ADR-017 тут недосяжний. Див. журнал E0-03 і TECH_STACK.md.
        public const AndroidSdkVersions MinSdk = AndroidSdkVersions.AndroidApiLevel25;
        public const AndroidSdkVersions TargetSdk = AndroidSdkVersions.AndroidApiLevel36;

        public const string SettingsFolder = "Assets/_Project/Settings";
        public const string Renderer2DPath = SettingsFolder + "/KebabStreet_Renderer2D.asset";
        public const string PipelineAssetPath = SettingsFolder + "/KebabStreet_URP.asset";

        private static readonly string[] UrpGeneratedAssets =
        {
            "UniversalRenderPipelineGlobalSettings.asset",
            "DefaultVolumeProfile.asset",
        };

        // Значення поля activeInputHandler у ProjectSettings.asset: 0 — старий ввід, 1 — Input System, 2 — обидва.
        private const int InputHandlerNewInputSystem = 1;

        /// <summary>Точка входу для batchmode: застосовує всі налаштування і зберігає їх.</summary>
        public static void ApplyAll()
        {
            ApplyPlayerSettings();
            EnsureUrp2D();
            RelocateUrpGeneratedAssets();
            AssetDatabase.SaveAssets();
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = MinSdk;
            PlayerSettings.Android.targetSdkVersion = TargetSdk;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            SetActiveInputHandler(InputHandlerNewInputSystem);

            Debug.Log(
                "[ProjectSetup] PlayerSettings: " +
                $"product='{PlayerSettings.productName}', " +
                $"id='{PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}', " +
                $"minSdk={(int)PlayerSettings.Android.minSdkVersion}, " +
                $"targetSdk={(int)PlayerSettings.Android.targetSdkVersion}, " +
                $"backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}, " +
                $"arch={PlayerSettings.Android.targetArchitectures}, " +
                $"orientation={PlayerSettings.defaultInterfaceOrientation}, " +
                $"colorSpace={PlayerSettings.colorSpace}, " +
                $"activeInputHandler={GetActiveInputHandler()}");
        }

        /// <summary>Створює (якщо немає) ресурси URP з 2D Renderer і призначає їх у Graphics та всі рівні Quality.</summary>
        public static void EnsureUrp2D()
        {
            EnsureFolder(SettingsFolder);

            var rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>(Renderer2DPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(rendererData, Renderer2DPath);
                Debug.Log($"[ProjectSetup] Створено {Renderer2DPath}");
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
                Debug.Log($"[ProjectSetup] Створено {PipelineAssetPath}");
            }

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
            }

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != pipeline)
                {
                    QualitySettings.renderPipeline = pipeline;
                }
            }
            QualitySettings.SetQualityLevel(current, false);

            Debug.Log(
                $"[ProjectSetup] URP: default='{AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline)}', " +
                $"renderer='{AssetDatabase.GetAssetPath(rendererData)}', qualityLevels={QualitySettings.names.Length}");
        }

        /// <summary>
        /// URP сам створює свої службові ресурси в корені Assets.
        /// Переносимо їх у папку Settings (посилання зберігаються), щоб корінь Assets лишався чистим.
        /// </summary>
        public static void RelocateUrpGeneratedAssets()
        {
            foreach (string fileName in UrpGeneratedAssets)
            {
                string source = "Assets/" + fileName;
                string destination = SettingsFolder + "/" + fileName;
                if (AssetDatabase.LoadMainAssetAtPath(source) == null)
                {
                    continue;
                }

                EnsureFolder(SettingsFolder);
                string error = AssetDatabase.MoveAsset(source, destination);
                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log($"[ProjectSetup] Перенесено {source} -> {destination}");
                }
                else
                {
                    Debug.LogWarning($"[ProjectSetup] Не вдалося перенести {source}: {error}");
                }
            }
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        /// <summary>
        /// Прибирає дані підпису з PlayerSettings, щоб вони не лишалися в ProjectSettings.asset
        /// (псевдонім ключа — секрет CI). Поля додатково очищаються напряму: сетер keystoreName
        /// перетворює шлях і не гарантує порожнього значення у файлі.
        /// </summary>
        public static void ClearAndroidSigning()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = string.Empty;
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasName = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;

            SerializedObject settings = LoadProjectSettings();
            if (settings == null)
            {
                return;
            }

            SetString(settings, "AndroidKeystoreName", string.Empty);
            SetString(settings, "AndroidKeyaliasName", string.Empty);
            SerializedProperty useCustom = settings.FindProperty("androidUseCustomKeystore");
            if (useCustom != null)
            {
                useCustom.boolValue = false;
            }

            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(SerializedObject target, string propertyName, string value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static SerializedObject LoadProjectSettings()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            return assets.Length > 0 ? new SerializedObject(assets[0]) : null;
        }

        private static int GetActiveInputHandler()
        {
            SerializedProperty property = LoadProjectSettings()?.FindProperty("activeInputHandler");
            return property != null ? property.intValue : -1;
        }

        private static void SetActiveInputHandler(int value)
        {
            SerializedObject settings = LoadProjectSettings();
            SerializedProperty property = settings?.FindProperty("activeInputHandler");
            if (property == null)
            {
                Debug.LogError("[ProjectSetup] Поле activeInputHandler не знайдено в ProjectSettings.asset.");
                return;
            }

            if (property.intValue != value)
            {
                property.intValue = value;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
