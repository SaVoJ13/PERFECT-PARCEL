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

        /// <summary>Q-05b: ціль на екрані - лише числа «зроблено/потрібно», без слів.</summary>
        public const string GoalShownFormat = "{0}/{1}";

        // Безпечні зони референсу 1080x1920 (ART_STYLE_GUIDE 4.1): виріз зверху, жестова панель знизу.
        private const float SafeTop = 48f;
        private const float SafeBottom = 64f;
        private const float Gap = 24f;
        private const float HudFontSize = 48f;
        private const float LevelFontSize = 40f;
        private const int StarIconCount = 3;
        private const int ResultButtonCount = 3;
        private const float IconSize = 64f;
        private const float ButtonIconMargin = 16f;
        private const float StarIconSize = 96f;
        private const float PauseButtonSize = 144f;
        private const float PauseIconSize = 96f;
        private const float ZoneBorder = 4f;

        private static readonly Color ZoneBorderColor = new Color(0.42f, 0.27f, 0.19f);
        private static readonly Color OverlayColor = UiColors.Overlay;
        private static readonly Color CoinColor = UiColors.Coin;

        private readonly ShiftDriver _driver;
        private readonly ILocalizer _text;
        private readonly string _goalFormat;
        private readonly string _coinsFormat;
        private readonly string _hintText;
        private string _goalFullText = string.Empty;
        private string _coinsFullText = string.Empty;
        private readonly Label _timer;
        private readonly Label _goal;
        private readonly Label _coins;
        private readonly Label _hint;
        private readonly PausePanel _pause;
        private readonly Action _onTutorialChanged;
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
        private readonly Button _menuButton;
        private readonly VisualElement _screenRoot;
        private readonly MotionTarget _bannerMotion;
        private readonly MotionTarget[] _starMotion = new MotionTarget[StarIconCount];
        private readonly MotionTarget[] _buttonMotion = new MotionTarget[ResultButtonCount];
        private readonly Button[] _resultButtons = new Button[ResultButtonCount];
        private string _resultCoinsFull = string.Empty;
        private float _resultAt = -1f;
        private bool _resultSkipped;
        private bool _firstWin;
        private int _starsRevealed;
        private bool _confettiFired;
        private long _resultCoinsShown = -1;
        private readonly bool _hasNextLevel;
        private ShiftHudView _shown;
        private bool _drawn;
        private readonly MotionTarget _timerMotion;
        private readonly MotionTarget _goalMotion;
        private readonly MotionTarget _coinBoxMotion;
        private float _timerTickAt = -1f;
        private float _goalPopAt = -1f;
        private float _coinsAt = -1f;
        private long _coinsFrom;
        private long _coinsTarget;
        private long _coinsDisplayed = -1;
        private bool _timerWarningShown;

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
            Action onTutorialChanged = null,
            Action onRestart = null,
            GameSettings settings = null)
        {
            _hasNextLevel = hasNextLevel;
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            UiMotion.ResetPause(); // нова зміна: облік пауз з нуля.
            Fx = new FxLayer((int)(driver.Seed & int.MaxValue));
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
            _screenRoot = root;
            UiDecor.AddVignette(root); // Q-05a: QB-ALL-08, краї темніші поверх ілюстрації фону.
            // S1-17: відступи за справжньою безпечною зоною телефона (виріз, жестова панель), а не лише за референсом.
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea(root));

            VisualElement titleRow = Row(root);
            Label level = HudLabel(
                titleRow, string.Format(text.Get(TextKeys.ShiftLevelTitle), driver.Level.Number), LevelFontSize, Color.white);
            level.style.flexGrow = 1;
            level.style.unityTextAlign = TextAnchor.MiddleLeft;
            // Q-05b (QB-SHIFT-05): пауза - квадратна кнопка-значок у куті HUD (>= 48 dp), без слова; слово лишається
            // у tooltip (a11y). Немає спрайта - показуємо текст, щоб кнопку було видно.
            string pauseText = text.Get(TextKeys.ShiftPause);
            Button pause = UiFactory.AddButton(titleRow, string.Empty, onPause);
            pause.name = "pause-button";
            pause.tooltip = pauseText;
            pause.style.width = PauseButtonSize;
            pause.style.height = PauseButtonSize;
            pause.style.marginTop = 0;
            pause.style.flexShrink = 0;
            VisualElement pauseIcon = ArtLibrary.AddCenterIcon(pause, PauseIconSize, "pause-icon");
            if (!ArtLibrary.ShowIcon(pauseIcon, ArtLibrary.Pause))
            {
                pause.text = pauseText;
            }

            Backdrop(titleRow);
            VisualElement hud = Row(root);
            Backdrop(hud);
            hud.name = "shift-hud";
            UiDecor.AddPanelShadow(hud); // Q-05a: QB-ALL-07, тінь HUD-панелі.
            hud.style.justifyContent = Justify.SpaceBetween;

            // Вузький екран, польська чи великі числа: написи переносяться на другий рядок, а не обрізаються (QA S1-09a).
            hud.style.flexWrap = Wrap.Wrap;
            _timer = HudLabel(hud, string.Empty, HudFontSize, Color.white);
            _goal = HudLabel(hud, string.Empty, HudFontSize, Color.white);
            VisualElement coinBox = Row(hud);
            coinBox.style.alignItems = Align.Center;
            coinBox.style.flexShrink = 1;
            Fx.CoinTarget = ArtLibrary.AddIcon(coinBox, ArtLibrary.Coin, IconSize) ?? coinBox;
            _coins = HudLabel(coinBox, string.Empty, HudFontSize, CoinColor);
            _goal.style.marginLeft = Gap;
            _goal.style.marginRight = Gap;
            _timerMotion = new MotionTarget(_timer);
            _goalMotion = new MotionTarget(_goal);
            _coinBoxMotion = new MotionTarget(coinBox);

            _hint = UiFactory.Notice(root, string.Empty);
            _hint.name = "shift-hint";

            Input = new ShiftInput(driver);
            CounterZone = Zone(root, CounterZoneName);
            Counter = new CounterPanel(CounterZone, Input, text, Fx);
            AssemblyZone = Zone(root, AssemblyZoneName);
            Assembly = new AssemblyPanel(AssemblyZone, Input, text, Fx);
            Grill = new GrillPanel(root, Input, text, Fx);
            root.Add(Fx.Root); // Q-05d: частинки поверх панелей, під плашками паузи й результату.

            _tutorial = tutorial;
            if (tutorial != null)
            {
                _tutorialOverlay = new TutorialOverlay(
                    root, tutorial, text, Grill.Root, AssemblyZone, CounterZone, onTutorialChanged, titleRow);
            }

            // Q-05h: панель паузи - затемнення, картка з перемикачами, «Заново»/«Меню» з підтвердженням, «Продовжити».
            _pause = new PausePanel(
                root,
                text,
                settings ?? GameSettings.Shared,
                onResume,
                onRestart,
                onExit,
                _tutorial == null ? (Func<bool>)null : CanSkipTutorial,
                SkipTutorialFromPause);
            _onTutorialChanged = onTutorialChanged;

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
            ArtLibrary.AddLeadingIcons(menu, new[] { ArtLibrary.Menu }, IconSize, ButtonIconMargin);
            _menuButton = menu;

            // Q-05e: ритуал результату. Банер - заголовок; кнопки з'являються після анімації; тап по панелі пропускає її.
            _bannerMotion = new MotionTarget(_resultTitle);
            for (int i = 0; i < StarIconCount; i++)
            {
                if (_starIcons[i] != null)
                {
                    _starMotion[i] = new MotionTarget(_starIcons[i]);
                }
            }

            _resultButtons[0] = _nextButton;
            _resultButtons[1] = _retryButton;
            _resultButtons[2] = _menuButton;
            for (int i = 0; i < ResultButtonCount; i++)
            {
                _buttonMotion[i] = new MotionTarget(_resultButtons[i]);
            }

            _resultPanel.RegisterCallback<PointerDownEvent>(_ => SkipResult());

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

        /// <summary>Шар частинок: пара, дим, монети, політ інгредієнта, «+N» (Q-05d).</summary>
        public FxLayer Fx { get; }

        /// <summary>Гриль зі слотами.</summary>
        public GrillPanel Grill { get; }

        public string TimerText => _timer.text;

        /// <summary>Повний (локалізований) текст цілі для a11y; на екрані - лише «N/M» (Q-05b).</summary>
        public string GoalText => _goalFullText;

        /// <summary>Повний (локалізований) текст монет для a11y; на екрані - значок монети й число (Q-05b).</summary>
        public string CoinsText => _coinsFullText;

        /// <summary>Те, що справді показано в смузі цілі.</summary>
        public string GoalShownText => _goal.text;

        /// <summary>Те, що справді показано біля монети.</summary>
        public string CoinsShownText => _coinsTarget.ToString();

        /// <summary>Q-05c: число, яке зараз на екрані (під час набігання відстає від <see cref="CoinsShownText"/>).</summary>
        public string CoinsAnimatedText => _coins.text;

        /// <summary>Рух числа таймера (пульс в останні 10 с).</summary>
        public MotionTarget TimerMotion => _timerMotion;

        /// <summary>Рух смуги цілі (підскок при прогресі).</summary>
        public MotionTarget GoalMotion => _goalMotion;

        /// <summary>Рух блоку монет (пульс значка при набіганні).</summary>
        public MotionTarget CoinsMotion => _coinBoxMotion;

        /// <summary>Чи таймер показано в стані попередження (колір Ember Red).</summary>
        public bool TimerWarningShown => _timerWarningShown;

        public string HintText => _hint.text;

        public string ResultTitleText => _resultTitle.text;

        public string ResultStarsText => _resultStars.text;

        /// <summary>Підсумкова (повна) сума монет результату; на екрані число набігає до неї (<see cref="ResultCoinsShownText"/>).</summary>
        public string ResultCoinsText => _resultCoinsFull;

        /// <summary>Те, що зараз написано на лічильнику монет результату (під час набігання відстає).</summary>
        public string ResultCoinsShownText => _resultCoins.text;

        /// <summary>Скільки зірок результату вже «засвітилось» (Q-05e).</summary>
        public int StarsRevealed => _starsRevealed;

        /// <summary>Чи кнопки результату вже доступні для тапу.</summary>
        public bool ResultButtonsReady { get; private set; }

        /// <summary>Чи гравець пропустив анімацію результату тапом.</summary>
        public bool ResultSkipped => _resultSkipped;

        /// <summary>Рух банера результату.</summary>
        public MotionTarget BannerMotion => _bannerMotion;

        /// <summary>Рух зірки результату (null, якщо спрайта зірки немає).</summary>
        public MotionTarget StarMotion(int index)
        {
            return _starMotion[index];
        }

        /// <summary>Рух кнопки результату: 0 - «Далі», 1 - «Ще раз», 2 - «Меню».</summary>
        public MotionTarget ResultButtonMotion(int index)
        {
            return _buttonMotion[index];
        }

        /// <summary>Чи це перша перемога на рівні (для конфеті, QB-RES-04); викликається контролером до збереження підсумку.</summary>
        public void MarkFirstWin(bool firstWin)
        {
            _firstWin = firstWin;
        }

        /// <summary>Тап по екрану результату: миттєво доводить анімацію до кінця (QB-RES-03). Повторний тап нічого не змінює.</summary>
        public void SkipResult()
        {
            if (!ResultPanelVisible || _resultSkipped || _resultAt < 0f)
            {
                return;
            }

            _resultSkipped = true;
            UpdateResult();
        }

        public string ResultReasonText => _resultReason.text;

        public string ResultShortfallText => _resultShortfall.text;

        public string ResultStarHintText => _resultHint.text;

        /// <summary>Повідомлення про невдале збереження на екрані результату (BUG-043); порожньо, якщо все гаразд.</summary>
        public string ResultNoticeText => _resultNotice.text;

        /// <summary>Плашка навчання; null, якщо навчання не підключено.</summary>
        public TutorialOverlay Tutorial => _tutorialOverlay;

        /// <summary>Панель паузи (Q-05h).</summary>
        public PausePanel Pause => _pause;

        private bool CanSkipTutorial()
        {
            return _tutorial != null && !_tutorial.Model.IsSkippedAll && _tutorial.Model.StepCount > 0;
        }

        private void SkipTutorialFromPause()
        {
            if (_tutorial != null && _tutorial.SkipAll())
            {
                _tutorialOverlay.Refresh();
                if (_onTutorialChanged != null)
                {
                    _onTutorialChanged();
                }
            }
        }

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

            UiMotion.SetPaused(_shown.Paused && !_shown.Finished); // Q-05d: пауза зупиняє ігровий час руху.
            UpdateMotion();
            Input.Update();
            Counter.Refresh();
            Assembly.Refresh();
            Grill.Refresh();
            UpdateResult();
            _pause.Update();
            Fx.Update();
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
            float now = UiMotion.Now;
            if (first || view.RemainingSeconds != previous.RemainingSeconds)
            {
                _timer.text = EmptyShift.FormatClock(view.RemainingSeconds);
                _timerTickAt = now;
            }

            if (first || view.GoalProgress != previous.GoalProgress || view.GoalTarget != previous.GoalTarget)
            {
                _goalFullText = string.Format(_goalFormat, view.GoalProgress, view.GoalTarget);
                _goal.text = string.Format(GoalShownFormat, view.GoalProgress, view.GoalTarget);
                _goal.tooltip = _goalFullText;
                if (!first && view.GoalProgress > previous.GoalProgress)
                {
                    _goalPopAt = now; // QB-SHIFT-03: число цілі підскакує.
                }
            }

            if (first || view.Coins != previous.Coins)
            {
                _coinsFullText = string.Format(_coinsFormat, view.Coins);
                _coins.tooltip = _coinsFullText;
                _coinsFrom = first ? view.Coins : _coinsDisplayed;
                _coinsTarget = view.Coins;
                _coinsAt = first ? -1f : now; // QB-SHIFT-04: набігання замість стрибка.
                if (first)
                {
                    _coinsDisplayed = view.Coins;
                    _coins.text = view.Coins.ToString();
                }
            }

            if (first || view.GoalDoneStarHint != previous.GoalDoneStarHint)
            {
                _hint.text = view.GoalDoneStarHint ? _hintText : string.Empty;
            }

            bool pauseVisible = view.Paused && !view.Finished;
            if (first || pauseVisible != PausePanelVisible)
            {
                PausePanelVisible = pauseVisible;
                _pause.SetVisible(pauseVisible);
            }

            if (first || view.Finished != ResultPanelVisible)
            {
                ResultPanelVisible = view.Finished;
                _resultPanel.style.display = view.Finished ? DisplayStyle.Flex : DisplayStyle.None;
                if (view.Finished)
                {
                    ShowResult(_driver.Result);
                    BeginResultRitual();
                }
            }
        }

        /// <summary>Q-05c: рух HUD щокадру; записує стиль і текст лише коли значення змінилось.</summary>
        private void UpdateMotion()
        {
            float now = UiMotion.Now;
            long remaining = _shown.RemainingSeconds;
            bool warning = UiMotion.IsTimerWarning(remaining) && !_shown.Finished;
            if (warning != _timerWarningShown)
            {
                _timerWarningShown = warning;
                _timer.style.color = warning ? UiColors.EmberRed : Color.white; // QB-SHIFT-02
            }

            float pulse = warning && !_shown.Paused ? UiMotion.TimerPulse(now - _timerTickAt, remaining) : 1f;
            _timerMotion.Set(pulse, pulse, 0f, 0f, 1f);

            float goalPop = UiMotion.Pop(now - _goalPopAt, UiMotion.GoalPopSec, UiMotion.GoalPopPeak);
            _goalMotion.Set(goalPop, goalPop, 0f, 0f, 1f);

            long shownCoins = UiMotion.CountUp(_coinsFrom, _coinsTarget, now - _coinsAt, UiMotion.CoinCountSec);
            if (shownCoins != _coinsDisplayed)
            {
                _coinsDisplayed = shownCoins;
                _coins.text = shownCoins.ToString();
            }

            float coinPop = _coinsAt < 0f ? 1f : UiMotion.Pop(now - _coinsAt, UiMotion.CoinPopSec, UiMotion.CoinPopPeak);
            _coinBoxMotion.Set(coinPop, coinPop, 0f, 0f, 1f);
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

            _resultCoinsFull = string.Format(_text.Get(TextKeys.ShiftResultCoins), view.RewardCoins);
            _resultCoins.text = _resultCoinsFull;
            _resultReason.text = ResultView.Format(_text, view.Reason);
            _resultShortfall.text = ResultView.Format(_text, view.Shortfall);
            _resultHint.text = ResultView.Format(_text, view.StarHint);
            _nextButton.style.display = view.CanNext ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Q-05e: початок ритуалу - частинки ігрового екрана гасяться, шар частинок піднімається над плашкою, шкала стартує.</summary>
        private void BeginResultRitual()
        {
            Fx.Clear();
            _screenRoot.Add(Fx.Root); // повторне додавання переносить шар на верх: конфеті й іскри над плашкою результату.
            _resultAt = UiMotion.Now;
            _resultSkipped = false;
            _starsRevealed = 0;
            _confettiFired = false;
            _resultCoinsShown = -1;
            ResultButtonsReady = false;
            for (int i = 0; i < StarIconCount; i++)
            {
                if (_starIcons[i] != null)
                {
                    ArtLibrary.Apply(_starIcons[i], ArtLibrary.StarEmpty); // зароблені «засвітяться» по одній
                }
            }

            UpdateResult();
        }

        /// <summary>
        /// Q-05e (QB-RES-01...05): банер, зірки по одній з іскрами й «дзинь», набігання монет, конфеті, поява кнопок.
        /// Після тапу-пропуску шкала стоїть у кінці (без іскор і звуків). Пише стиль лише при зміні значень.
        /// </summary>
        private void UpdateResult()
        {
            if (!ResultPanelVisible || _resultAt < 0f || Result == null)
            {
                return;
            }

            int stars = Result.Stars;
            float elapsed = _resultSkipped ? ResultMotion.TotalSec(stars) : UiMotion.Now - _resultAt;
            _bannerMotion.Set(1f, 1f, 0f, ResultMotion.BannerOffset(elapsed), ResultMotion.BannerOpacity(elapsed));

            for (int i = 0; i < StarIconCount; i++)
            {
                bool earned = i < stars;
                if (earned && i >= _starsRevealed && ResultMotion.StarReached(i, elapsed))
                {
                    _starsRevealed = i + 1;
                    if (_starIcons[i] != null)
                    {
                        ArtLibrary.Apply(_starIcons[i], ArtLibrary.StarFull);
                    }

                    if (!_resultSkipped)
                    {
                        Fx.Sparks(_starIcons[i]);
                        SfxHub.Play(SfxCue.StarChime, i); // QB-RES-02: «дзинь» зі зростаючою висотою
                    }
                }

                if (_starMotion[i] != null)
                {
                    float scale = earned ? ResultMotion.StarScale(i, elapsed) : 1f;
                    _starMotion[i].Set(scale, scale, 0f, 0f, 1f);
                }
            }

            long coins = ResultMotion.CoinsShown(Result.RewardCoins, elapsed);
            if (coins != _resultCoinsShown)
            {
                bool counting = _resultCoinsShown >= 0 && coins > _resultCoinsShown && !_resultSkipped;
                _resultCoinsShown = coins;
                if (counting)
                {
                    SfxHub.Play(SfxCue.CoinTick); // QB-RES-03: «тік-тік» не частіше 15/с (ліміт у SfxCatalog)
                }

                _resultCoins.text = coins == Result.RewardCoins
                    ? _resultCoinsFull
                    : string.Format(_text.Get(TextKeys.ShiftResultCoins), coins);
            }

            if (!_confettiFired && Result.Won && (stars >= 3 || _firstWin) && elapsed >= ResultMotion.ConfettiAt(stars))
            {
                _confettiFired = true;
                if (!_resultSkipped)
                {
                    Fx.Confetti(ResultMotion.ConfettiCount);
                }
            }

            float buttons = ResultMotion.ButtonsOpacity(stars, elapsed);
            bool ready = ResultMotion.ButtonsReady(stars, elapsed);
            for (int i = 0; i < ResultButtonCount; i++)
            {
                _buttonMotion[i].Set(1f, 1f, 0f, 0f, buttons);
                _resultButtons[i].pickingMode = ready ? PickingMode.Position : PickingMode.Ignore;
            }

            ResultButtonsReady = ready;
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
