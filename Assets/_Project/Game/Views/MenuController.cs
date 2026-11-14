using KebabStreet.Core;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>Головне меню: вибір рівня (кнопка на кожен рівень каталогу), "Налаштування" (заглушка) і повідомлення про стан збереження.</summary>
    public sealed class MenuController : ScreenController
    {
        private Label _settingsHint;

        protected override void Build()
        {
            ILocalizer text = Context.Text;
            UiFactory.Title(Container, text.Get(TextKeys.GameTitle));

            string noticeKey = SaveNoticePolicy.KeyOf(Context.Profile.LastNotice);
            if (noticeKey != null)
            {
                UiFactory.Notice(Container, text.Get(noticeKey));
            }

            LevelEntry[] entries = LevelProgress.Build(Context.Levels, Context.Profile.Data);
            for (int i = 0; i < entries.Length; i++)
            {
                LevelEntry entry = entries[i];
                int number = entry.Number;
                Button button = UiFactory.AddButton(Container, LevelButtonText(text, entry), () => OnPlay(number));
                button.SetEnabled(entry.Unlocked);
                AddLevelIcons(button, entry);
            }

            UiFactory.AddButton(Container, text.Get(TextKeys.MenuSettings), OnSettings);
            _settingsHint = UiFactory.Body(Container, string.Empty);
        }

        private const float LevelIconSize = 44f;
        private const float LevelIconMargin = 20f;
        private const int MaxStars = 3;

        /// <summary>Замок або зірки на кнопці рівня (S1-18, S1-23: біля лівого краю, текст не перекривається).</summary>
        private static void AddLevelIcons(Button button, LevelEntry entry)
        {
            if (!entry.Unlocked)
            {
                ArtLibrary.AddLeadingIcons(button, new[] { ArtLibrary.Lock }, LevelIconSize, LevelIconMargin);
                return;
            }

            if (entry.BestStars > 0)
            {
                var names = new string[MaxStars];
                for (int s = 0; s < MaxStars; s++)
                {
                    names[s] = s < entry.BestStars ? ArtLibrary.StarFull : ArtLibrary.StarEmpty;
                }

                ArtLibrary.AddLeadingIcons(button, names, LevelIconSize, LevelIconMargin);
            }
        }

        /// <summary>Підпис кнопки рівня: замок, зірки або просто номер (відкритий, ще не пройдений).</summary>
        public static string LevelButtonText(ILocalizer text, LevelEntry entry)
        {
            if (!entry.Unlocked)
            {
                return string.Format(text.Get(TextKeys.MenuLevelLocked), entry.Number);
            }

            return entry.BestStars > 0
                ? string.Format(text.Get(TextKeys.MenuLevelStars), entry.Number, entry.BestStars)
                : string.Format(text.Get(TextKeys.MenuLevel), entry.Number);
        }

        private void OnPlay(int levelNumber)
        {
            if (Context.IsLevelUnlocked(levelNumber) && Context.SelectLevel(levelNumber) && Context.Flow.Play())
            {
                Open(GameFlow.SceneOf(Context.Flow.Current));
            }
        }

        private void OnSettings()
        {
            _settingsHint.text = Context.Text.Get(TextKeys.MenuSettingsSoon);
        }
    }
}
