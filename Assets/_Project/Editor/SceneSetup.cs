using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using KebabStreet.Game;

namespace KebabStreet.Editor
{
    /// <summary>
    /// Створює сцени Boot, Menu, Shift кодом (headless-first) і вносить їх у Build Settings.
    /// Сцени генеруються перед кожною збіркою в папку <see cref="GeneratedFolder"/> (вона в .gitignore),
    /// тому їх не треба комітити, а ручних кроків у редакторі немає.
    /// </summary>
    public static class SceneSetup
    {
        public const string GeneratedFolder = "Assets/_Project/Generated";
        // Unity сам створює тему за замовчуванням для PanelSettings у корені Assets; вона теж тимчасова.
        public const string AutoThemeFolder = "Assets/UI Toolkit";
        public const string PanelSettingsPath = GeneratedFolder + "/PanelSettings.asset";

        private static readonly Vector2Int ReferenceResolution = new Vector2Int(1080, 1920);
        // Колір камери навмисно відрізняється від UiFactory.Background: видно, чи панель UI малюється (BUG-029).
        public static readonly Color CameraColor = new Color(0.08f, 0.20f, 0.35f);
        public const string DefaultThemePath = AutoThemeFolder + "/UnityThemes/UnityDefaultRuntimeTheme.tss";

        /// <summary>Порядок важливий: перша сцена - Boot.</summary>
        public static readonly string[] SceneNames =
        {
            GameFlow.BootScene,
            GameFlow.MenuScene,
            GameFlow.ShiftScene,
        };

        [MenuItem("KebabStreet/Generate Scenes")]
        public static void GenerateMenuItem()
        {
            Generate();
        }

        /// <summary>Створює три сцени, прописує їх у Build Settings і повертає шляхи в порядку збірки.</summary>
        public static string[] Generate()
        {
            EnsureFolder(GeneratedFolder);
            EnsurePanelSettings();

            var paths = new string[SceneNames.Length];
            var buildScenes = new EditorBuildSettingsScene[SceneNames.Length];
            for (int i = 0; i < SceneNames.Length; i++)
            {
                paths[i] = GeneratedFolder + "/" + SceneNames[i] + ".unity";
                CreateScene(paths[i], ControllerTypeOf(SceneNames[i]));
                buildScenes[i] = new EditorBuildSettingsScene(paths[i], true);
            }

            EditorBuildSettings.scenes = buildScenes;
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] Сцени: " + string.Join(", ", paths));
            return paths;
        }

        /// <summary>Прибирає згенероване й повертає Build Settings до порожнього стану (файл у git не змінюється).</summary>
        public static void Cleanup()
        {
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];
            if (AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.DeleteAsset(GeneratedFolder);
            }

            if (AssetDatabase.IsValidFolder(AutoThemeFolder))
            {
                AssetDatabase.DeleteAsset(AutoThemeFolder);
            }
        }

        private static Type ControllerTypeOf(string sceneName)
        {
            switch (sceneName)
            {
                case GameFlow.MenuScene:
                    return typeof(MenuController);
                case GameFlow.ShiftScene:
                    return typeof(ShiftController);
                default:
                    return typeof(BootController);
            }
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            if (settings.themeStyleSheet == null)
            {
                settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(DefaultThemePath);
                Debug.Log("[SceneSetup] Тема PanelSettings: " + (settings.themeStyleSheet != null ? DefaultThemePath : "НЕ ЗНАЙДЕНО"));
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void CreateScene(string path, Type controllerType)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CameraColor;

            var ui = new GameObject("UI");
            var document = ui.AddComponent<UIDocument>();
            // Ресурс перезавантажується щоразу: NewScene вивантажує невикористані ресурси, і посилання,
            // отримане до цього, стає знищеним (BUG-029: у сценах після Boot panelSettings був null).
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            ui.AddComponent(controllerType);

            EditorSceneManager.SaveScene(scene, path);
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
    }
}
