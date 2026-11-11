using UnityEngine;
using UnityEngine.SceneManagement;

namespace KebabStreet.Game
{
    /// <summary>
    /// Мінімальний лог для logcat (тег Unity): переходи між сценами і результат завантаження (BUG-030).
    /// Особистих даних не містить.
    /// </summary>
    public static class GameLog
    {
        public const string Prefix = "[KS] ";

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Info("start v" + Application.version + " screen=" + Screen.width + "x" + Screen.height
                + " dpi=" + Screen.dpi + " gfx=" + SystemInfo.graphicsDeviceType);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Info("scene loaded: " + scene.name + " mode=" + mode + " roots=" + scene.rootCount);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            Info("scene unloaded: " + scene.name);
        }
    }
}
