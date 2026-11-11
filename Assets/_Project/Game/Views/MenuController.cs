using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>Головне меню: "Грати", "Налаштування" (заглушка) і повідомлення про стан збереження.</summary>
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

            UiFactory.AddButton(Container, text.Get(TextKeys.MenuPlay), OnPlay);
            UiFactory.AddButton(Container, text.Get(TextKeys.MenuSettings), OnSettings);
            _settingsHint = UiFactory.Body(Container, string.Empty);
        }

        private void OnPlay()
        {
            if (Context.Flow.Play())
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
