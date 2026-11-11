using SceneSetup = KebabStreet.Editor.SceneSetup;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Ловить "UI не з'явився на екрані" (BUG-029) ще до телефона: перевіряє сцени, які SceneSetup
    /// генерує для збірки. Увага: перевіряє налаштування, а не пікселі; пікселі - L3 на телефоні.
    /// </summary>
    public sealed class GeneratedScenesTests
    {
        private string[] _paths;

        [OneTimeSetUp]
        public void Generate()
        {
            _paths = SceneSetup.Generate();
        }

        [OneTimeTearDown]
        public void CleanUp()
        {
            EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneSetup.Cleanup();
        }

        [Test]
        public void ThreeScenesAreGenerated_BootFirst()
        {
            Assert.AreEqual(3, _paths.Length);
            StringAssert.EndsWith("/Boot.unity", _paths[0]);
        }

        [Test]
        public void CameraColorDiffersFromUiBackground()
        {
            // Інакше не відрізнити "панель не малюється" від "панель порожня" (колір обох збігався).
            Assert.AreNotEqual(SceneSetup.CameraColor, UiFactory.Background);
        }

        [Test]
        public void EverySceneHasUiDocumentWithThemedPanelSettingsAndController()
        {
            foreach (string path in _paths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var document = Object.FindFirstObjectByType<UIDocument>();
                    Assert.IsNotNull(document, path + ": немає UIDocument");
                    Assert.IsNotNull(document.panelSettings, path + ": немає PanelSettings");
                    Assert.IsNotNull(document.panelSettings.themeStyleSheet, path + ": PanelSettings без теми (UI не стилізується)");
                    Assert.IsNotNull(document.GetComponent<ScreenController>(), path + ": немає ScreenController");
                    Assert.IsNotNull(Camera.main, path + ": немає головної камери");
                    Assert.AreEqual(SceneSetup.CameraColor, Camera.main.backgroundColor, path);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
