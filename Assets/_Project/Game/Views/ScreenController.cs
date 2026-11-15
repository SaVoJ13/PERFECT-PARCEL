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

        /// <summary>Чи екран має плавні переходи (Q-05e). Boot - ні: це миттєва службова сцена.</summary>
        protected virtual bool UseTransitions => true;

        private ScreenFader _fader;

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
            UnitySfxSink.EnsureInstalled();
            Container = UiFactory.Screen(root);
            Build();
            GameLog.Info(GetType().Name + ".Build: дітей у контейнері=" + Container.childCount);
            if (UseTransitions)
            {
                // Q-05e (QB-ALL-04): поява з затемнення; ввід під накладкою ігнорується, доки перехід іде.
                _fader = new ScreenFader(root, Container);
                _fader.Attach();
                _fader.PlayIn();
            }

            root.schedule.Execute(() => LogLayout(root)).StartingIn(DiagnosticDelayMs);
        }

        private void LogLayout(VisualElement root)
        {
            GameLog.Info(GetType().Name + " layout: root=" + root.layout + " container=" + Container.layout
                + " display=" + root.resolvedStyle.display + " visibility=" + root.resolvedStyle.visibility
                + " cameras=" + Camera.allCamerasCount);
        }

        protected abstract void Build();

        /// <summary>
        /// Відкриває сцену. Якщо екран має переходи, спершу відбувається затемнення (200-300 мс); повторні виклики під час
        /// переходу ігноруються, тож подвійний тап не завантажує сцену двічі.
        /// </summary>
        protected void Open(string sceneName)
        {
            if (_fader == null)
            {
                LoadScene(sceneName);
                return;
            }

            _fader.Leave(() => LoadScene(sceneName));
        }

        private static void LoadScene(string sceneName)
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
