using KebabStreet.Services;
using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Сцена Boot: створює контекст гри (один SaveService), завантажує збереження і відкриває меню.
    /// Load викликається тут, до будь-якого Save.
    /// </summary>
    public sealed class BootController : ScreenController
    {
        protected override bool NeedsContext => false;

        protected override void Build()
        {
            if (GameContext.Current == null)
            {
                GameContext.Install(new GameContext(
                    new FileSaveStorage(Application.persistentDataPath), new SystemClock(), new PlaceholderLocalizer()));
            }

            GameContext context = GameContext.Current;
            UiFactory.Title(Container, context.Text.Get(TextKeys.GameTitle));
            UiFactory.Body(Container, context.Text.Get(TextKeys.BootLoading));

            context.Profile.Load();
            context.Flow.BootCompleted();
            Open(GameFlow.SceneOf(context.Flow.Current));
        }
    }
}
