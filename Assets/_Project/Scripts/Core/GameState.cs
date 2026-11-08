namespace PerfectParcel.Core
{
    /// <summary>
    /// Стани життєвого циклу ігрової сесії.
    /// </summary>
    public enum GameState
    {
        None,
        Loading,
        MainMenu,
        Playing,
        Paused,
        LevelComplete,
        GameOver
    }
}
