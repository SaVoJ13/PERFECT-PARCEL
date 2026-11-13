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

            string levelFormat = text.Get(TextKeys.MenuLevel);
            LevelCatalog levels = Context.Levels;
            for (int i = 0; i < levels.Count; i++)
            {
                int number = levels.Get(i).Number;
                UiFactory.AddButton(Container, string.Format(levelFormat, number), () => OnPlay(number));
            }

            UiFactory.AddButton(Container, text.Get(TextKeys.MenuSettings), OnSettings);
            _settingsHint = UiFactory.Body(Container, string.Empty);
        }

        private void OnPlay(int levelNumber)
        {
            if (Context.SelectLevel(levelNumber) && Context.Flow.Play())
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
