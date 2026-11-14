using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Плашка дядька Алі на екрані зміни (S1-11): текст поточного кроку за ключем, кнопки «Далі» (пропустити крок)
    /// і «Пропустити все», підсвітка зони, на яку вказує якір кроку. Лише показує <see cref="TutorialDirector.Current"/>;
    /// правил гри тут немає. Плейсхолдер без фінальної графіки (S1-12). Плашка не перехоплює дотики поза кнопками,
    /// тож гра під нею лишається доступною (навчання не зупиняє симуляцію).
    /// </summary>
    public sealed class TutorialOverlay
    {
        public const string PlaqueName = "tutorial-plaque";

        // Вигляд, не баланс. Плашка стоїть над гриллем: безпечна зона знизу + висота слота гриля + відступи.
        private const float BottomOffset = 352f;
        private const float SideOffset = 24f;
        private const float Padding = 24f;
        private const float TextSize = 40f;
        private const float ButtonHeight = 120f;
        private const float ButtonFontSize = 36f;
        private const float HighlightWidth = 8f;

        private static readonly Color PlaqueColor = new Color(0.20f, 0.13f, 0.09f, 0.96f);
        private static readonly Color PlaqueBorder = new Color(1f, 0.85f, 0.4f);
        private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.4f);
        private const float AliSize = 120f;
        private static readonly Color ButtonColor = new Color(0.85f, 0.45f, 0.15f);

        private readonly TutorialDirector _director;
        private readonly ILocalizer _text;
        private readonly Action _onChanged;
        private readonly VisualElement _plaque;
        private readonly Label _label;
        private readonly Button _nextButton;
        private readonly Button _skipAllButton;
        private readonly VisualElement _grillZone;
        private readonly VisualElement _assemblyZone;
        private readonly VisualElement _counterZone;
        private TutorialStep _shown;
        private VisualElement _highlighted;
        private StyleFloat _savedWidth;
        private StyleColor _savedColor;

        /// <param name="onChanged">Викликається, коли профіль змінився через кнопку (наприклад «Пропустити все») і його варто зберегти; може бути null.</param>
        public TutorialOverlay(
            VisualElement parent,
            TutorialDirector director,
            ILocalizer text,
            VisualElement grillZone,
            VisualElement assemblyZone,
            VisualElement counterZone,
            Action onChanged = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            _director = director ?? throw new ArgumentNullException(nameof(director));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _grillZone = grillZone;
            _assemblyZone = assemblyZone;
            _counterZone = counterZone;
            _onChanged = onChanged;

            _plaque = new VisualElement { name = PlaqueName };
            _plaque.style.position = Position.Absolute;
            _plaque.style.left = SideOffset;
            _plaque.style.right = SideOffset;
            _plaque.style.bottom = BottomOffset;
            _plaque.style.flexDirection = FlexDirection.Column;
            _plaque.style.paddingLeft = Padding;
            _plaque.style.paddingRight = Padding;
            _plaque.style.paddingTop = Padding;
            _plaque.style.paddingBottom = Padding;
            _plaque.style.backgroundColor = PlaqueColor;
            _plaque.style.borderLeftWidth = 4f;
            _plaque.style.borderRightWidth = 4f;
            _plaque.style.borderTopWidth = 4f;
            _plaque.style.borderBottomWidth = 4f;
            _plaque.style.borderLeftColor = PlaqueBorder;
            _plaque.style.borderRightColor = PlaqueBorder;
            _plaque.style.borderTopColor = PlaqueBorder;
            _plaque.style.borderBottomColor = PlaqueBorder;
            _plaque.style.display = DisplayStyle.None;
            _plaque.pickingMode = PickingMode.Ignore;
            parent.Add(_plaque);

            // Дядько Алі (S1-12): портрет над краєм плашки, праворуч; немає спрайта — просто не показується.
            var portrait = new VisualElement { name = "tutorial-portrait", pickingMode = PickingMode.Ignore };
            portrait.style.position = Position.Absolute;
            portrait.style.top = -AliSize;
            portrait.style.right = Padding;
            portrait.style.width = AliSize;
            portrait.style.height = AliSize;
            ArtLibrary.Apply(portrait, ArtLibrary.Ali);
            _plaque.Add(portrait);

            _label = new Label(string.Empty);
            _label.style.fontSize = TextSize;
            _label.style.color = Color.white;
            _label.style.whiteSpace = WhiteSpace.Normal;
            _label.style.unityTextAlign = TextAnchor.MiddleLeft;
            _label.pickingMode = PickingMode.Ignore;
            _plaque.Add(_label);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = Padding;
            row.pickingMode = PickingMode.Ignore;
            _plaque.Add(row);

            _nextButton = AddButton(row, text.Get(TextKeys.TutorialNext), OnNext);
            _nextButton.name = "tutorial-next";
            _skipAllButton = AddButton(row, text.Get(TextKeys.TutorialSkip), OnSkipAll);
            _skipAllButton.name = "tutorial-skip-all";
        }

        /// <summary>Плашку показано.</summary>
        public bool IsVisible => _shown != null;

        /// <summary>Id показаного кроку; null — плашки немає.</summary>
        public string ShownStepId => _shown == null ? null : _shown.Id;

        /// <summary>Показаний текст підказки.</summary>
        public string Text => _label.text;

        public bool NextButtonVisible => _nextButton.style.display.value != DisplayStyle.None;

        public bool SkipAllButtonVisible => _skipAllButton.style.display.value != DisplayStyle.None;

        /// <summary>Зона, яку зараз підсвічено; None, якщо підсвітки немає.</summary>
        public TutorialZone HighlightedZone { get; private set; }

        /// <summary>Синхронізує плашку з поточним кроком; нічого не робить, поки крок не змінився.</summary>
        public void Refresh()
        {
            TutorialStep step = _director.Current;
            if (ReferenceEquals(step, _shown))
            {
                return;
            }

            ClearHighlight();
            _shown = step;
            if (step == null)
            {
                _plaque.style.display = DisplayStyle.None;
                _label.text = string.Empty;
                return;
            }

            _label.text = _text.Get(step.TextKey);
            _nextButton.style.display = _director.Model.CanSkipStep ? DisplayStyle.Flex : DisplayStyle.None;
            _skipAllButton.style.display = _director.Model.CanSkipAll ? DisplayStyle.Flex : DisplayStyle.None;
            _plaque.style.display = DisplayStyle.Flex;
            SetHighlight(TutorialAnchors.ZoneOf(step.Anchor));
        }

        private void OnNext()
        {
            if (_director.SkipStep())
            {
                Changed();
            }
        }

        private void OnSkipAll()
        {
            if (_director.SkipAll())
            {
                Changed();
            }
        }

        private void Changed()
        {
            Refresh();
            if (_onChanged != null)
            {
                _onChanged();
            }
        }

        private void SetHighlight(TutorialZone zone)
        {
            VisualElement target = ElementOf(zone);
            if (target == null)
            {
                return;
            }

            _highlighted = target;
            HighlightedZone = zone;
            _savedWidth = target.style.borderLeftWidth;
            _savedColor = target.style.borderLeftColor;
            target.style.borderLeftWidth = HighlightWidth;
            target.style.borderRightWidth = HighlightWidth;
            target.style.borderTopWidth = HighlightWidth;
            target.style.borderBottomWidth = HighlightWidth;
            target.style.borderLeftColor = HighlightColor;
            target.style.borderRightColor = HighlightColor;
            target.style.borderTopColor = HighlightColor;
            target.style.borderBottomColor = HighlightColor;
        }

        private void ClearHighlight()
        {
            if (_highlighted != null)
            {
                _highlighted.style.borderLeftWidth = _savedWidth;
                _highlighted.style.borderRightWidth = _savedWidth;
                _highlighted.style.borderTopWidth = _savedWidth;
                _highlighted.style.borderBottomWidth = _savedWidth;
                _highlighted.style.borderLeftColor = _savedColor;
                _highlighted.style.borderRightColor = _savedColor;
                _highlighted.style.borderTopColor = _savedColor;
                _highlighted.style.borderBottomColor = _savedColor;
                _highlighted = null;
            }

            HighlightedZone = TutorialZone.None;
        }

        private VisualElement ElementOf(TutorialZone zone)
        {
            switch (zone)
            {
                case TutorialZone.Grill:
                    return _grillZone;
                case TutorialZone.Assembly:
                    return _assemblyZone;
                case TutorialZone.Counter:
                    return _counterZone;
                default:
                    return null;
            }
        }

        private static Button AddButton(VisualElement parent, string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.height = ButtonHeight;
            button.style.flexGrow = 1;
            button.style.marginLeft = 8f;
            button.style.marginRight = 8f;
            button.style.fontSize = ButtonFontSize;
            button.style.color = Color.white;
            button.style.backgroundColor = ButtonColor;
            parent.Add(button);
            return button;
        }
    }
}
