using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Основа екранів: бере корінь UIDocument, будує інтерфейс у Start. Правил гри тут немає.
    /// Якщо сцену запущено в редакторі повз Boot, контекста немає - спершу завантажується Boot.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract class ScreenController : MonoBehaviour
    {
        private const long DiagnosticDelayMs = 700;

        protected GameContext Context { get; private set; }

        protected VisualElement Container { get; private set; }

        protected virtual bool NeedsContext => true;

        private void Start()
        {
            Context = GameContext.Current;
            if (NeedsContext && Context == null)
            {
                GameLog.Info(GetType().Name + ": немає контексту, завантажую " + GameFlow.BootScene);
                SceneManager.LoadScene(GameFlow.BootScene);
                return;
            }

            UIDocument document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            GameLog.Info(GetType().Name + ".Start: panelSettings=" + (document.panelSettings != null)
                + " theme=" + (document.panelSettings != null && document.panelSettings.themeStyleSheet != null)
                + " root=" + (root != null) + " panel=" + (root != null && root.panel != null));
            Container = UiFactory.Screen(root);
            Build();
            GameLog.Info(GetType().Name + ".Build: дітей у контейнері=" + Container.childCount);
            root.schedule.Execute(() => LogLayout(root)).StartingIn(DiagnosticDelayMs);
        }

        private void LogLayout(VisualElement root)
        {
            GameLog.Info(GetType().Name + " layout: root=" + root.layout + " container=" + Container.layout
                + " display=" + root.resolvedStyle.display + " visibility=" + root.resolvedStyle.visibility
                + " cameras=" + Camera.allCamerasCount);
        }

        protected abstract void Build();

        protected static void Open(string sceneName)
        {
            GameLog.Info("Open " + sceneName + " (з " + SceneManager.GetActiveScene().name + ")");
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            if (load == null)
            {
                GameLog.Info("Open " + sceneName + ": сцену не знайдено в збірці");
                return;
            }

            load.completed += _ => GameLog.Info("Open " + sceneName + ": завантажено");
        }
    }
}
