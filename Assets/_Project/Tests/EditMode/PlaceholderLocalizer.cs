using System.Collections.Generic;

namespace KebabStreet.Game
{
    /// <summary>Англійські рядки для тестів каркаса (у грі не використовується; у грі діє JsonLocalizer).</summary>
    public sealed class PlaceholderLocalizer : ILocalizer
    {
        private static readonly Dictionary<string, string> Strings = new Dictionary<string, string>
        {
            { TextKeys.GameTitle, "Kebab Street" },
            { TextKeys.BootLoading, "Loading..." },
            { TextKeys.MenuPlay, "Play" },
            { TextKeys.MenuSettings, "Settings" },
            { TextKeys.MenuSettingsSoon, "Settings are coming soon" },
            { TextKeys.ShiftTimeLeft, "Time left: {0}" },
            { TextKeys.ShiftFinished, "Shift finished" },
            { TextKeys.ShiftExit, "Exit" },
            { TextKeys.NoticeProgressReset, "Your saved progress could not be restored and was reset." },
            { TextKeys.NoticeUpdateGame, "Your save was made by a newer version. Please update the game. Progress will not be saved." },
            { TextKeys.NoticeStorageUnavailable, "Saved data cannot be read right now. Progress will not be saved. Restart the game." },
            { TextKeys.NoticeSaveDisabled, "Progress saving is turned off because of a problem with the saved data." },
            { TextKeys.NoticeSaveFailed, "Progress could not be saved. Please try again later." },
        };

        public string Get(string key)
        {
            string value;
            return key != null && Strings.TryGetValue(key, out value) ? value : "[" + key + "]";
        }
    }
}
