namespace KebabStreet.Game
{
    public enum GameScreen
    {
        Boot = 0,
        Menu = 1,
        Shift = 2,
    }

    /// <summary>Перехід між екранами без залежності від сцен: контролери лише завантажують сцену, названу тут.</summary>
    public sealed class GameFlow
    {
        public const string BootScene = "Boot";
        public const string MenuScene = "Menu";
        public const string ShiftScene = "Shift";

        public GameScreen Current { get; private set; } = GameScreen.Boot;

        public static string SceneOf(GameScreen screen)
        {
            switch (screen)
            {
                case GameScreen.Menu:
                    return MenuScene;
                case GameScreen.Shift:
                    return ShiftScene;
                default:
                    return BootScene;
            }
        }

        /// <summary>Boot - Menu: збереження завантажено.</summary>
        public bool BootCompleted()
        {
            return Move(GameScreen.Boot, GameScreen.Menu);
        }

        /// <summary>Menu - Shift: кнопка "Грати".</summary>
        public bool Play()
        {
            return Move(GameScreen.Menu, GameScreen.Shift);
        }

        /// <summary>Shift - Menu: кнопка виходу.</summary>
        public bool ExitToMenu()
        {
            return Move(GameScreen.Shift, GameScreen.Menu);
        }

        /// <summary>Shift - Shift: «Ще раз» або «Далі» на екрані результату (сцена завантажується заново).</summary>
        public bool Replay()
        {
            return Move(GameScreen.Shift, GameScreen.Shift);
        }

        private bool Move(GameScreen from, GameScreen to)
        {
            if (Current != from)
            {
                return false;
            }

            Current = to;
            return true;
        }
    }
}
