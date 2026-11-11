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
        protected GameContext Context { get; private set; }

        protected VisualElement Container { get; private set; }

        protected virtual bool NeedsContext => true;

        private void Start()
        {
            Context = GameContext.Current;
            if (NeedsContext && Context == null)
            {
                SceneManager.LoadScene(GameFlow.BootScene);
                return;
            }

            Container = UiFactory.Screen(GetComponent<UIDocument>().rootVisualElement);
            Build();
        }

        protected abstract void Build();

        protected static void Open(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
