using System;
using KebabStreet.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Каркас екрана зміни (S1-09a, портрет): верхня смуга (рівень, пауза, таймер, ціль, монети),
    /// підказка, прилавок із клієнтами (S1-09b), порожня зона збирання (S1-09c), гриль, плашка паузи і
    /// мінімальний підсумок. Звичайний клас без MonoBehaviour: будує елементи в переданому
    /// контейнері й показує <see cref="ShiftHudView"/>; тексти оновлює, лише коли число змінилось.
    /// Правил гри тут немає. Плейсхолдер без графіки (S1-12).
    /// </summary>
    public sealed class ShiftScreen
    {
        public const string CounterZoneName = "counter-zone";
        public const string AssemblyZoneName = "assembly-zone";

        // Безпечні зони референсу 1080x1920 (ART_STYLE_GUIDE 4.1): виріз зверху, жестова панель знизу.
        private const float SafeTop = 48f;
        private const float SafeBottom = 64f;
        private const float Gap = 24f;
        private const float HudFontSize = 48f;
        private const float LevelFontSize = 40f;
        private const float PauseButtonWidth = 260f;
        private const float PauseButtonHeight = 144f;
        private const float ZoneBorder = 4f;

        private static readonly Color ZoneBorderColor = new Color(0.42f, 0.27f, 0.19f);
        private static readonly Color OverlayColor = new Color(0.10f, 0.07f, 0.06f, 0.92f);
        private static readonly Color CoinColor = new Color(1f, 0.79f, 0.2f);

        private readonly ShiftDriver _driver;
        private readonly ILocalizer _text;
        private readonly string _goalFormat;
        private readonly string _coinsFormat;
        private readonly string _hintText;
        private readonly Label _timer;
        private readonly Label _goal;
        private readonly Label _coins;
        private readonly Label _hint;
        private readonly VisualElement _pausePanel;
        private readonly VisualElement _resultPanel;
        private readonly Label _resultTitle;
        private readonly Label _resultStars;
        private readonly Label _resultCoins;
        private ShiftHudView _shown;
        private bool _drawn;

        /// <param name="container">Куди будувати екран (повноекранний контейнер контролера).</param>
        /// <param name="onPause">Гравець натиснув «Пауза».</param>
        /// <param name="onResume">Гравець натиснув «Продовжити» на плашці паузи.</param>
        /// <param name="onExit">Гравець натиснув «Вийти» (плашка паузи або підсумок).</param>
        public ShiftScreen(
            VisualElement container, ShiftDriver driver, ILocalizer text, Action onPause, Action onResume, Action onExit)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _goalFormat = text.Get(ShiftHud.GoalTextKey(driver.Level.Goal.Type));
            _coinsFormat = text.Get(TextKeys.ShiftHudCoins);
            _hintText = text.Get(TextKeys.ShiftGoalDoneStar2);

            var root = new VisualElement { name = "shift-screen" };
            root.style.flexGrow = 1;
            root.style.alignSelf = Align.Stretch;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = SafeTop;
            root.style.paddingBottom = SafeBottom;
            container.Add(root);

            VisualElement titleRow = Row(root);
            Label level = HudLabel(
                titleRow, string.Format(text.Get(TextKeys.ShiftLevelTitle), driver.Level.Number), LevelFontSize, Color.white);
            level.style.flexGrow = 1;
            level.style.unityTextAlign = TextAnchor.MiddleLeft;
            Button pause = UiFactory.AddButton(titleRow, text.Get(TextKeys.ShiftPause), onPause);
            pause.name = "pause-button";
            pause.style.width = PauseButtonWidth;
            pause.style.height = PauseButtonHeight;
            pause.style.marginTop = 0;

            VisualElement hud = Row(root);
            hud.name = "shift-hud";
            hud.style.justifyContent = Justify.SpaceBetween;

            // Вузький екран, польська чи великі числа: написи переносяться на другий рядок, а не обрізаються (QA S1-09a).
            hud.style.flexWrap = Wrap.Wrap;
            _timer = HudLabel(hud, string.Empty, HudFontSize, Color.white);
            _goal = HudLabel(hud, string.Empty, HudFontSize, Color.white);
            _coins = HudLabel(hud, string.Empty, HudFontSize, CoinColor);
            _goal.style.marginLeft = Gap;
            _goal.style.marginRight = Gap;

            _hint = UiFactory.Notice(root, string.Empty);

            CounterZone = Zone(root, CounterZoneName);
            Counter = new CounterPanel(CounterZone, driver, text);
            AssemblyZone = Zone(root, AssemblyZoneName);
            Grill = new GrillPanel(root, driver, text);

            _pausePanel = Overlay(root, "pause-panel");
            UiFactory.Title(_pausePanel, text.Get(TextKeys.ShiftPaused));
            UiFactory.AddButton(_pausePanel, text.Get(TextKeys.ShiftResume), onResume);
            UiFactory.AddButton(_pausePanel, text.Get(TextKeys.ShiftExit), onExit);

            _resultPanel = Overlay(root, "result-panel");
            _resultTitle = UiFactory.Title(_resultPanel, string.Empty);
            _resultStars = UiFactory.Body(_resultPanel, string.Empty);
            _resultCoins = UiFactory.Body(_resultPanel, string.Empty);
            UiFactory.AddButton(_resultPanel, text.Get(TextKeys.ShiftExit), onExit);

            Refresh();
        }

        /// <summary>Зона прилавка з клієнтами.</summary>
        public VisualElement CounterZone { get; }

        /// <summary>Прилавок: слоти клієнтів, терпіння, замовлення, черга (S1-09b).</summary>
        public CounterPanel Counter { get; }

        /// <summary>Порожня зона збирання страви (наповнює S1-09c).</summary>
        public VisualElement AssemblyZone { get; }

        /// <summary>Гриль зі слотами.</summary>
        public GrillPanel Grill { get; }

        public string TimerText => _timer.text;

        public string GoalText => _goal.text;

        public string CoinsText => _coins.text;

        public string HintText => _hint.text;

        public string ResultTitleText => _resultTitle.text;

        public string ResultStarsText => _resultStars.text;

        public string ResultCoinsText => _resultCoins.text;

        /// <summary>Чи показано плашку паузи.</summary>
        public bool PausePanelVisible { get; private set; }

        /// <summary>Чи показано підсумок зміни.</summary>
        public bool ResultPanelVisible { get; private set; }

        /// <summary>Оновлює смугу, плашки і гриль за станом драйвера. Викликається щокадру.</summary>
        public void Refresh()
        {
            ShiftHudView view = ShiftHud.FromDriver(_driver);
            if (!_drawn || !view.Equals(_shown))
            {
                Draw(view, _shown, !_drawn);
                _shown = view;
                _drawn = true;
            }

            Counter.Refresh();
            Grill.Refresh();
        }

        private void Draw(ShiftHudView view, ShiftHudView previous, bool first)
        {
            if (first || view.RemainingSeconds != previous.RemainingSeconds)
            {
                _timer.text = EmptyShift.FormatClock(view.RemainingSeconds);
            }

            if (first || view.GoalProgress != previous.GoalProgress || view.GoalTarget != previous.GoalTarget)
            {
                _goal.text = string.Format(_goalFormat, view.GoalProgress, view.GoalTarget);
            }

            if (first || view.Coins != previous.Coins)
            {
                _coins.text = string.Format(_coinsFormat, view.Coins);
            }

            if (first || view.GoalDoneStarHint != previous.GoalDoneStarHint)
            {
                _hint.text = view.GoalDoneStarHint ? _hintText : string.Empty;
            }

            bool pauseVisible = view.Paused && !view.Finished;
            if (first || pauseVisible != PausePanelVisible)
            {
                PausePanelVisible = pauseVisible;
                _pausePanel.style.display = pauseVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (first || view.Finished != ResultPanelVisible)
            {
                ResultPanelVisible = view.Finished;
                _resultPanel.style.display = view.Finished ? DisplayStyle.Flex : DisplayStyle.None;
                if (view.Finished)
                {
                    ShowResult(_driver.Result);
                }
            }
        }

        private void ShowResult(ShiftResult result)
        {
            _resultTitle.text = _text.Get(result.Won ? TextKeys.ShiftResultWon : TextKeys.ShiftResultLost);
            _resultStars.text = string.Format(_text.Get(TextKeys.ShiftResultStars), result.Stars);
            _resultCoins.text = string.Format(_text.Get(TextKeys.ShiftResultCoins), result.RewardCoins);
        }

        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;
            row.style.paddingLeft = Gap;
            row.style.paddingRight = Gap;
            row.style.marginBottom = Gap / 2f;
            parent.Add(row);
            return row;
        }

        private static Label HudLabel(VisualElement parent, string text, float size, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;
            parent.Add(label);
            return label;
        }

        private static VisualElement Zone(VisualElement parent, string name)
        {
            var zone = new VisualElement { name = name };
            zone.style.flexGrow = 1;
            zone.style.marginLeft = Gap;
            zone.style.marginRight = Gap;
            zone.style.marginTop = Gap / 2f;
            zone.style.marginBottom = Gap / 2f;
            zone.style.borderLeftWidth = ZoneBorder;
            zone.style.borderRightWidth = ZoneBorder;
            zone.style.borderTopWidth = ZoneBorder;
            zone.style.borderBottomWidth = ZoneBorder;
            zone.style.borderLeftColor = ZoneBorderColor;
            zone.style.borderRightColor = ZoneBorderColor;
            zone.style.borderTopColor = ZoneBorderColor;
            zone.style.borderBottomColor = ZoneBorderColor;
            parent.Add(zone);
            return zone;
        }

        private static VisualElement Overlay(VisualElement parent, string name)
        {
            var overlay = new VisualElement { name = name };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.flexDirection = FlexDirection.Column;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.backgroundColor = OverlayColor;
            overlay.style.display = DisplayStyle.None;
            parent.Add(overlay);
            return overlay;
        }
    }
}
