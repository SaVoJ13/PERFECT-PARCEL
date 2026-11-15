using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>Елементи панелі паузи, які можна «натиснути» кодом (тести, бот): те саме, що тап по кнопці.</summary>
    public enum PauseControl
    {
        Resume = 1,
        Restart = 2,
        Menu = 3,
        Music = 4,
        Sounds = 5,
        Haptics = 6,
        ReducedMotion = 7,
        SkipTutorial = 8,
        ConfirmYes = 9,
        ConfirmNo = 10,
    }

    /// <summary>Яке підтвердження зараз показано на панелі паузи.</summary>
    public enum PauseConfirm
    {
        None = 0,
        Restart = 1,
        Menu = 2,
    }

    /// <summary>
    /// Панель паузи (Q-05h, QB-PAUSE-02/04): ігровий шар за нею затемнено на <see cref="OverlayMotion.PauseDimAlpha"/>, картка з тінню
    /// містить перемикачі «Музика», «Звуки», «Вібрація», «Менше анімацій» (значок-перемикач і підпис), «Пропустити навчання»,
    /// другорядні «Заново» і «Меню» (обидві з підтвердженням: у зміні є прогрес) і головну «Продовжити» внизу. Налаштування
    /// пишуться в <see cref="GameSettings"/> (PlayerPrefs) і одразу застосовуються до звуку й руху. Правил гри тут немає.
    /// Поява картки - лише вигляд (<see cref="OverlayMotion.PauseScale"/>): сама пауза фіксується до показу.
    /// </summary>
    public sealed class PausePanel
    {
        public const string PanelName = "pause-panel";
        public const string CardName = "pause-card";

        private const float CardWidthPercent = 90f;
        private const float CardPadding = 32f;
        private const float CardRadius = 36f;
        private const float TitleSize = 64f;
        private const float ToggleHeight = 168f;
        private const float ToggleLabelSize = 28f;
        private const float TrackWidth = 80f;
        private const float TrackHeight = 40f;
        private const float KnobSize = 32f;
        private const float KnobInset = 4f;
        private const float RowButtonHeight = 120f;
        private const float RowButtonFont = 36f;
        private const float ResumeHeight = 180f;
        private const float ResumeFont = 56f;
        private const float Gap = 20f;
        private const int ToggleCount = 4;

        private static readonly Color CardColor = UiColors.PauseCard;
        private static readonly Color TrackOn = UiColors.Saffron;
        private static readonly Color TrackOff = new Color(0.35f, 0.35f, 0.35f);

        private readonly GameSettings _settings;
        private readonly ILocalizer _text;
        private readonly Action _onResume;
        private readonly Action _onRestart;
        private readonly Action _onMenu;
        private readonly Action _onSkipTutorial;
        private readonly Func<bool> _canSkipTutorial;
        private readonly VisualElement _root;
        private readonly VisualElement _card;
        private readonly MotionTarget _cardMotion;
        private readonly VisualElement _secondaryRow;
        private readonly VisualElement _confirmBox;
        private readonly Label _confirmText;
        private readonly Button _skipTutorial;
        private readonly Button _restart;
        private readonly Button[] _toggles = new Button[ToggleCount];
        private readonly VisualElement[] _tracks = new VisualElement[ToggleCount];
        private readonly VisualElement[] _knobs = new VisualElement[ToggleCount];
        private float _openedAt = -1f;

        public PausePanel(
            VisualElement parent,
            ILocalizer text,
            GameSettings settings,
            Action onResume,
            Action onRestart,
            Action onMenu,
            Func<bool> canSkipTutorial = null,
            Action onSkipTutorial = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            _text = text ?? throw new ArgumentNullException(nameof(text));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _onResume = onResume ?? NoAction;
            _onRestart = onRestart;
            _onMenu = onMenu ?? NoAction;
            _canSkipTutorial = canSkipTutorial;
            _onSkipTutorial = onSkipTutorial;

            _root = new VisualElement { name = PanelName };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.10f, 0.07f, 0.06f, OverlayMotion.PauseDimAlpha);
            _root.style.display = DisplayStyle.None;
            parent.Add(_root);

            _card = new VisualElement { name = CardName };
            _card.style.width = Length.Percent(CardWidthPercent);
            _card.style.flexDirection = FlexDirection.Column;
            _card.style.alignItems = Align.Stretch;
            _card.style.paddingLeft = CardPadding;
            _card.style.paddingRight = CardPadding;
            _card.style.paddingTop = CardPadding;
            _card.style.paddingBottom = CardPadding;
            _card.style.backgroundColor = CardColor;
            Round(_card, CardRadius);
            _root.Add(_card);
            UiDecor.AddPanelShadow(_card); // QB-ALL-07
            _cardMotion = new MotionTarget(_card);

            Label title = UiFactory.Title(_card, text.Get(TextKeys.ShiftPaused));
            title.style.fontSize = TitleSize;
            title.style.marginTop = 0;

            VisualElement toggles = new VisualElement { name = "pause-toggles" };
            toggles.style.flexDirection = FlexDirection.Row;
            toggles.style.justifyContent = Justify.SpaceBetween;
            toggles.style.marginTop = Gap;
            _card.Add(toggles);
            AddToggle(toggles, 0, "pause-music", TextKeys.PauseMusic, PauseControl.Music);
            AddToggle(toggles, 1, "pause-sounds", TextKeys.PauseSounds, PauseControl.Sounds);
            AddToggle(toggles, 2, "pause-haptics", TextKeys.PauseHaptics, PauseControl.Haptics);
            AddToggle(toggles, 3, "pause-reduced", TextKeys.PauseReducedMotion, PauseControl.ReducedMotion);

            _skipTutorial = SecondaryButton(_card, text.Get(TextKeys.PauseSkipTutorial), PauseControl.SkipTutorial);
            _skipTutorial.name = "pause-skip-tutorial";
            _skipTutorial.style.marginTop = Gap;
            _skipTutorial.style.display = DisplayStyle.None;

            _secondaryRow = new VisualElement { name = "pause-secondary" };
            _secondaryRow.style.flexDirection = FlexDirection.Row;
            _secondaryRow.style.marginTop = Gap;
            _card.Add(_secondaryRow);
            _restart = SecondaryButton(_secondaryRow, text.Get(TextKeys.PauseRestart), PauseControl.Restart);
            _restart.name = "pause-restart";
            _restart.style.display = onRestart == null ? DisplayStyle.None : DisplayStyle.Flex;
            Button menu = SecondaryButton(_secondaryRow, text.Get(TextKeys.ResultMenu), PauseControl.Menu);
            menu.name = "pause-menu";

            _confirmBox = new VisualElement { name = "pause-confirm" };
            _confirmBox.style.marginTop = Gap;
            _confirmBox.style.display = DisplayStyle.None;
            _card.Add(_confirmBox);
            _confirmText = UiFactory.Body(_confirmBox, string.Empty);
            _confirmText.style.marginTop = 0;
            VisualElement confirmRow = new VisualElement();
            confirmRow.style.flexDirection = FlexDirection.Row;
            confirmRow.style.marginTop = Gap;
            _confirmBox.Add(confirmRow);
            SecondaryButton(confirmRow, text.Get(TextKeys.PauseYes), PauseControl.ConfirmYes).name = "pause-confirm-yes";
            SecondaryButton(confirmRow, text.Get(TextKeys.PauseNo), PauseControl.ConfirmNo).name = "pause-confirm-no";

            // QB-PAUSE-04: «Продовжити» - головна, найбільша, внизу панелі.
            Button resume = UiFactory.AddButton(_card, text.Get(TextKeys.ShiftResume), () => Press(PauseControl.Resume));
            resume.name = "pause-resume";
            resume.style.width = Length.Percent(100);
            resume.style.height = ResumeHeight;
            resume.style.fontSize = ResumeFont;
            resume.style.marginTop = Gap * 1.5f;

            RefreshToggles();
        }

        /// <summary>Панель показано.</summary>
        public bool Visible { get; private set; }

        /// <summary>Яке підтвердження зараз відкрито.</summary>
        public PauseConfirm Confirm { get; private set; }

        public VisualElement Root => _root;

        public VisualElement Card => _card;

        /// <summary>Рух картки (поява).</summary>
        public MotionTarget CardMotion => _cardMotion;

        /// <summary>Кнопка «Пропустити навчання» показана.</summary>
        public bool SkipTutorialVisible => _skipTutorial.style.display.value == DisplayStyle.Flex;

        /// <summary>Кнопка «Заново» показана.</summary>
        public bool RestartVisible => _restart.style.display.value == DisplayStyle.Flex;

        /// <summary>Текст підтвердження (порожній, якщо його немає).</summary>
        public string ConfirmText => _confirmText.text;

        /// <summary>Показує або ховає панель. Стан фіксується одразу; поява картки - лише анімація вигляду.</summary>
        public void SetVisible(bool visible)
        {
            if (visible == Visible)
            {
                return;
            }

            Visible = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                ShowConfirm(PauseConfirm.None);
                _openedAt = UiMotion.Clock();
                _skipTutorial.style.display = _canSkipTutorial != null && _canSkipTutorial() ? DisplayStyle.Flex : DisplayStyle.None;
                RefreshToggles();
                Update();
            }
        }

        /// <summary>Щокадру, поки панель видна: поява картки.</summary>
        public void Update()
        {
            if (!Visible)
            {
                return;
            }

            float t = UiMotion.Clock() - _openedAt;
            float scale = OverlayMotion.PauseScale(t);
            _cardMotion.Set(scale, scale, 0f, 0f, OverlayMotion.PauseOpacity(t));
        }

        /// <summary>Те саме, що тап по відповідній кнопці.</summary>
        public bool Press(PauseControl control)
        {
            switch (control)
            {
                case PauseControl.Resume:
                    _onResume();
                    return true;
                case PauseControl.Restart:
                    if (_onRestart == null)
                    {
                        return false;
                    }

                    ShowConfirm(PauseConfirm.Restart);
                    return true;
                case PauseControl.Menu:
                    ShowConfirm(PauseConfirm.Menu);
                    return true;
                case PauseControl.ConfirmNo:
                    ShowConfirm(PauseConfirm.None);
                    return true;
                case PauseControl.ConfirmYes:
                    return Confirmed();
                case PauseControl.Music:
                    _settings.SetMusic(!_settings.MusicOn);
                    return Changed();
                case PauseControl.Sounds:
                    _settings.SetSounds(!_settings.SoundsOn);
                    return Changed();
                case PauseControl.Haptics:
                    _settings.SetHaptics(!_settings.HapticsEnabled);
                    if (_settings.HapticsEnabled)
                    {
                        HapticsHub.Router.Confirm(); // Q-05i: відгук підтверджує, що вібрація ввімкнена
                    }

                    return Changed();
                case PauseControl.ReducedMotion:
                    _settings.SetReducedMotion(!_settings.ReducedMotion);
                    return Changed();
                case PauseControl.SkipTutorial:
                    if (_onSkipTutorial != null)
                    {
                        _onSkipTutorial();
                    }

                    _skipTutorial.style.display = DisplayStyle.None;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Перемикач зараз увімкнений.</summary>
        public bool IsOn(PauseControl toggle)
        {
            switch (toggle)
            {
                case PauseControl.Music: return _settings.MusicOn;
                case PauseControl.Sounds: return _settings.SoundsOn;
                case PauseControl.Haptics: return _settings.HapticsEnabled;
                case PauseControl.ReducedMotion: return _settings.ReducedMotion;
                default: return false;
            }
        }

        /// <summary>Колір доріжки перемикача (для тесту стану на екрані).</summary>
        public Color TrackColorOf(PauseControl toggle)
        {
            return _tracks[IndexOf(toggle)].style.backgroundColor.value;
        }

        /// <summary>Підпис перемикача (tooltip для доступності), тест перевіряє, що він є.</summary>
        public string ToggleLabel(PauseControl toggle)
        {
            return _toggles[IndexOf(toggle)].tooltip;
        }

        private bool Confirmed()
        {
            PauseConfirm what = Confirm;
            if (what == PauseConfirm.None)
            {
                return false;
            }

            ShowConfirm(PauseConfirm.None);
            if (what == PauseConfirm.Restart)
            {
                _onRestart();
            }
            else
            {
                _onMenu();
            }

            return true;
        }

        private bool Changed()
        {
            _settings.ApplyTo(SfxHub.Router);
            RefreshToggles();
            return true;
        }

        private void ShowConfirm(PauseConfirm what)
        {
            Confirm = what;
            bool confirming = what != PauseConfirm.None;
            _confirmBox.style.display = confirming ? DisplayStyle.Flex : DisplayStyle.None;
            _secondaryRow.style.display = confirming ? DisplayStyle.None : DisplayStyle.Flex;
            _confirmText.text = what == PauseConfirm.Restart
                ? _text.Get(TextKeys.PauseConfirmRestart)
                : (what == PauseConfirm.Menu ? _text.Get(TextKeys.PauseConfirmMenu) : string.Empty);
        }

        private void RefreshToggles()
        {
            Apply(0, _settings.MusicOn);
            Apply(1, _settings.SoundsOn);
            Apply(2, _settings.HapticsEnabled);
            Apply(3, _settings.ReducedMotion);
        }

        private void Apply(int index, bool on)
        {
            _tracks[index].style.backgroundColor = on ? TrackOn : TrackOff;
            _knobs[index].style.left = on ? TrackWidth - KnobSize - KnobInset : KnobInset;
        }

        private static int IndexOf(PauseControl toggle)
        {
            return (int)toggle - (int)PauseControl.Music;
        }

        private void AddToggle(VisualElement row, int index, string name, string textKey, PauseControl control)
        {
            string label = _text.Get(textKey);
            var button = new Button(SfxHub.WithTapSound(() => Press(control))) { name = name };
            button.tooltip = label;
            button.AddToClassList(UiFactory.PressClass);
            button.style.width = Length.Percent(23.5f);
            button.style.height = ToggleHeight;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.backgroundColor = UiColors.ItemButton;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            Round(button, 24f);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.width = TrackWidth;
            track.style.height = TrackHeight;
            track.style.flexShrink = 0;
            Round(track, TrackHeight / 2f);
            var knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.style.position = Position.Absolute;
            knob.style.top = (TrackHeight - KnobSize) / 2f;
            knob.style.width = KnobSize;
            knob.style.height = KnobSize;
            knob.style.backgroundColor = Color.white;
            Round(knob, KnobSize / 2f);
            track.Add(knob);
            button.Add(track);

            var caption = new Label(label) { pickingMode = PickingMode.Ignore };
            caption.style.fontSize = ToggleLabelSize;
            caption.style.color = Color.white;
            caption.style.whiteSpace = WhiteSpace.Normal;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.style.marginTop = 12f;
            button.Add(caption);

            row.Add(button);
            _toggles[index] = button;
            _tracks[index] = track;
            _knobs[index] = knob;
        }

        private Button SecondaryButton(VisualElement parent, string label, PauseControl control)
        {
            var button = new Button(SfxHub.WithTapSound(() => Press(control))) { text = label };
            button.AddToClassList(UiFactory.PressClass);
            button.style.height = RowButtonHeight;
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            button.style.marginLeft = Gap / 4f;
            button.style.marginRight = Gap / 4f;
            button.style.fontSize = RowButtonFont;
            button.style.color = Color.white;
            button.style.backgroundColor = UiColors.StationButton;
            Round(button, 24f);
            parent.Add(button);
            return button;
        }

        private static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void NoAction()
        {
        }
    }
}
