using System;
using KebabStreet.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Каркас екрана зміни (S1-09a, портрет): верхня смуга (рівень, пауза, таймер, ціль, монети),
    /// підказка, прилавок із клієнтами (S1-09b), станція збирання (S1-09c), гриль, плашка паузи і
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
        private const int StarIconCount = 3;
        private const float IconSize = 64f;
        private const float StarIconSize = 96f;
        private const float PauseButtonHeight = 144f;
        private const float ZoneBorder = 4f;

        private static readonly Color ZoneBorderColor = new Color(0.42f, 0.27f, 0.19f);
        private static readonly Color OverlayColor = UiColors.Overlay;
        private static readonly Color CoinColor = UiColors.Coin;

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
        private readonly VisualElement[] _starIcons = new VisualElement[StarIconCount];
        private readonly Label _resultCoins;
        private readonly Label _resultReason;
        private readonly Label _resultShortfall;
        private readonly Label _resultHint;
        private readonly Label _resultNotice;
        private readonly TutorialOverlay _tutorialOverlay;
        private readonly TutorialDirector _tutorial;
        private readonly Button _nextButton;
        private readonly Button _retryButton;
        private readonly bool _hasNextLevel;
        private ShiftHudView _shown;
        private bool _drawn;

        /// <param name="container">Куди будувати екран (повноекранний контейнер контролера).</param>
        /// <param name="onPause">Гравець натиснув «Пауза».</param>
        /// <param name="onResume">Гравець натиснув «Продовжити» на плашці паузи.</param>
        /// <param name="onExit">Гравець натиснув «Вийти» (плашка паузи або підсумок).</param>
        /// <param name="onRetry">«Ще раз» на екрані результату (S1-10); null — кнопки немає.</param>
        /// <param name="onNext">«Далі» на екрані результату; кнопка видима лише після перемоги й коли <paramref name="hasNextLevel"/>.</param>
        /// <param name="tutorial">Навчання дядька Алі (S1-11); null — без підказок.</param>
        /// <param name="onTutorialChanged">Профіль навчання змінився кнопкою (наприклад «Пропустити все»): час зберегти.</param>
        public ShiftScreen(
            VisualElement container,
            ShiftDriver driver,
            ILocalizer text,
            Action onPause,
            Action onResume,
            Action onExit,
            Action onRetry = null,
            Action onNext = null,
            bool hasNextLevel = false,
            TutorialDirector tutorial = null,
            Action onTutorialChanged = null)
        {
            _hasNextLevel = hasNextLevel;
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
            ArtLibrary.Apply(root, ArtLibrary.Background, true);
            root.style.flexGrow = 1;
            root.style.alignSelf = Align.Stretch;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = SafeTop;
            root.style.paddingBottom = SafeBottom;
            container.Add(root);

            // S1-17: відступи за справжньою безпечною зоною телефона (виріз, жестова панель), а не лише за референсом.
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea(root));

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
            pause.style.flexDirection = FlexDirection.Row;
            pause.style.alignItems = Align.Center;
            pause.style.justifyContent = Justify.Center;
            ArtLibrary.AddIcon(pause, ArtLibrary.Pause, IconSize, true);

            Backdrop(titleRow);
            VisualElement hud = Row(root);
            Backdrop(hud);
            hud.name = "shift-hud";
            hud.style.justifyContent = Justify.SpaceBetween;

            // Вузький екран, польська чи великі числа: написи переносяться на другий рядок, а не обрізаються (QA S1-09a).
            hud.style.flexWrap = Wrap.Wrap;
            _timer = HudLabel(hud, string.Empty, HudFontSize, Color.white);
            _goal = HudLabel(hud, string.Empty, HudFontSize, Color.white);
            VisualElement coinBox = Row(hud);
            coinBox.style.alignItems = Align.Center;
            coinBox.style.flexShrink = 1;
            ArtLibrary.AddIcon(coinBox, ArtLibrary.Coin, IconSize);
            _coins = HudLabel(coinBox, string.Empty, HudFontSize, CoinColor);
            _goal.style.marginLeft = Gap;
            _goal.style.marginRight = Gap;

            _hint = UiFactory.Notice(root, string.Empty);

            Input = new ShiftInput(driver);
            CounterZone = Zone(root, CounterZoneName);
            Counter = new CounterPanel(CounterZone, Input, text);
            AssemblyZone = Zone(root, AssemblyZoneName);
            Assembly = new AssemblyPanel(AssemblyZone, Input, text);
            Grill = new GrillPanel(root, Input, text);

            _tutorial = tutorial;
            if (tutorial != null)
            {
                _tutorialOverlay = new TutorialOverlay(
                    root, tutorial, text, Grill.Root, AssemblyZone, CounterZone, onTutorialChanged);
            }

            _pausePanel = Overlay(root, "pause-panel");
            UiFactory.Title(_pausePanel, text.Get(TextKeys.ShiftPaused));
            UiFactory.AddButton(_pausePanel, text.Get(TextKeys.ShiftResume), onResume);
            UiFactory.AddButton(_pausePanel, text.Get(TextKeys.ShiftExit), onExit);

            _resultPanel = Overlay(root, "result-panel");
            _resultTitle = UiFactory.Title(_resultPanel, string.Empty);
            _resultStars = UiFactory.Body(_resultPanel, string.Empty);
            VisualElement starRow = Row(_resultPanel);
            starRow.style.justifyContent = Justify.Center;
            starRow.name = "result-star-icons";
            for (int i = 0; i < StarIconCount; i++)
            {
                _starIcons[i] = ArtLibrary.AddIcon(starRow, ArtLibrary.StarEmpty, StarIconSize);
            }

            _resultCoins = UiFactory.Body(_resultPanel, string.Empty);
            _resultReason = UiFactory.Notice(_resultPanel, string.Empty);
            _resultShortfall = UiFactory.Body(_resultPanel, string.Empty);
            _resultHint = UiFactory.Notice(_resultPanel, string.Empty);
            _resultNotice = UiFactory.Notice(_resultPanel, string.Empty);
            _nextButton = UiFactory.AddButton(_resultPanel, text.Get(TextKeys.ResultNext), onNext ?? NoAction);
            _nextButton.name = "result-next";
            _nextButton.style.display = DisplayStyle.None;
            _retryButton = UiFactory.AddButton(_resultPanel, text.Get(TextKeys.ResultRetry), onRetry ?? NoAction);
            _retryButton.name = "result-retry";
            _retryButton.style.display = onRetry == null ? DisplayStyle.None : DisplayStyle.Flex;
            Button menu = UiFactory.AddButton(_resultPanel, text.Get(TextKeys.ResultMenu), onExit);
            menu.name = "result-menu";
            menu.style.flexDirection = FlexDirection.Row;
            menu.style.alignItems = Align.Center;
            menu.style.justifyContent = Justify.Center;
            ArtLibrary.AddIcon(menu, ArtLibrary.Menu, IconSize, true);

            Refresh();
        }

        /// <summary>Зона прилавка з клієнтами.</summary>
        public VisualElement CounterZone { get; }

        /// <summary>Прилавок: слоти клієнтів, терпіння, замовлення, черга (S1-09b).</summary>
        public CounterPanel Counter { get; }

        /// <summary>Зона збирання страви.</summary>
        public VisualElement AssemblyZone { get; }

        /// <summary>Станція збирання: станції, дошка, черга дій, підказки, комбо (S1-09c).</summary>
        public AssemblyPanel Assembly { get; }

        /// <summary>Усі тапи екрана: панелі шлють команди лише через нього в ShiftDriver.Tap.</summary>
        public ShiftInput Input { get; }

        /// <summary>Гриль зі слотами.</summary>
        public GrillPanel Grill { get; }

        public string TimerText => _timer.text;

        public string GoalText => _goal.text;

        public string CoinsText => _coins.text;

        public string HintText => _hint.text;

        public string ResultTitleText => _resultTitle.text;

        public string ResultStarsText => _resultStars.text;

        public string ResultCoinsText => _resultCoins.text;

        public string ResultReasonText => _resultReason.text;

        public string ResultShortfallText => _resultShortfall.text;

        public string ResultStarHintText => _resultHint.text;

        /// <summary>Повідомлення про невдале збереження на екрані результату (BUG-043); порожньо, якщо все гаразд.</summary>
        public string ResultNoticeText => _resultNotice.text;

        /// <summary>Плашка навчання; null, якщо навчання не підключено.</summary>
        public TutorialOverlay Tutorial => _tutorialOverlay;

        /// <summary>Модель показаного результату; null, поки зміна не завершилась.</summary>
        public ResultView Result { get; private set; }

        /// <summary>Кнопка «Далі» показана (перемога й наступний рівень є).</summary>
        public bool NextVisible => _nextButton.style.display.value == DisplayStyle.Flex;

        /// <summary>Кнопка «Ще раз» показана.</summary>
        public bool RetryVisible => _retryButton.style.display.value != DisplayStyle.None;

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

            Input.Update();
            Counter.Refresh();
            Assembly.Refresh();
            Grill.Refresh();
            if (_tutorial != null)
            {
                _tutorial.Update();
                _tutorialOverlay.Refresh();
            }
        }

        /// <summary>
        /// Показує на екрані результату повідомлення про збереження (BUG-043): ключ береться з
        /// <see cref="SaveNoticePolicy.KeyOf"/>; None гасить напис. Викликається після запису підсумку в профіль.
        /// </summary>
        public void ShowSaveNotice(SaveNotice notice)
        {
            string key = SaveNoticePolicy.KeyOf(notice);
            _resultNotice.text = key == null ? string.Empty : _text.Get(key);
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
            ResultView view = ResultView.Build(result, _driver.Level, _hasNextLevel);
            Result = view;
            _resultTitle.text = _text.Get(view.TitleKey);
            _resultStars.text = string.Format(_text.Get(TextKeys.ShiftResultStars), view.Stars);
            for (int i = 0; i < StarIconCount; i++)
            {
                if (_starIcons[i] != null)
                {
                    ArtLibrary.Apply(_starIcons[i], i < view.Stars ? ArtLibrary.StarFull : ArtLibrary.StarEmpty);
                }
            }

            _resultCoins.text = string.Format(_text.Get(TextKeys.ShiftResultCoins), view.RewardCoins);
            _resultReason.text = ResultView.Format(_text, view.Reason);
            _resultShortfall.text = ResultView.Format(_text, view.Shortfall);
            _resultHint.text = ResultView.Format(_text, view.StarHint);
            _nextButton.style.display = view.CanNext ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void ApplySafeArea(VisualElement root)
        {
            Rect safe = UnityEngine.Screen.safeArea;
            float scale = SafeAreaMath.ScaleOf(UnityEngine.Screen.height, root.layout.height);
            float top = Mathf.Max(SafeTop, SafeAreaMath.Top(UnityEngine.Screen.height, safe.y, safe.height, scale));
            float bottom = Mathf.Max(SafeBottom, SafeAreaMath.Bottom(safe.y, scale));
            if (Mathf.Abs(root.resolvedStyle.paddingTop - top) > 0.5f || Mathf.Abs(root.resolvedStyle.paddingBottom - bottom) > 0.5f)
            {
                root.style.paddingTop = top;
                root.style.paddingBottom = bottom;
            }
        }

        private static void NoAction()
        {
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

        // S1-17: белий текст на білих смугах навісу не читався - темна підкладка під смугою.
        private static void Backdrop(VisualElement row)
        {
            row.style.backgroundColor = UiColors.HudBackdrop;
            row.style.borderBottomLeftRadius = Gap;
            row.style.borderBottomRightRadius = Gap;
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
