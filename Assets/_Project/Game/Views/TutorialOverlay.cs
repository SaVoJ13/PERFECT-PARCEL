using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Навчання дядька Алі на екрані зміни (S1-11, Q-05h): плашка з текстом поточного кроку (друкується, тап - показати
    /// повністю), кнопки «Далі» і «Пропустити все», підсвітка зони, а для кроків із дією - прожектор (екран затемнено
    /// крім вирізу навколо зони, тап поза вирізом нічого не робить, а лише пульсує рамкою) і рука-вказівник із елементів.
    /// Лише показує <see cref="TutorialDirector.Current"/>; правил гри тут немає. Плашка не перехоплює дотики поза кнопками.
    /// Час анімацій - справжній годинник <see cref="UiMotion.Clock"/>, у «менше анімацій» усе з'являється одразу.
    /// </summary>
    public sealed class TutorialOverlay
    {
        public const string PlaqueName = "tutorial-plaque";
        public const string DimName = "tutorial-dim";
        public const string FrameName = "tutorial-frame";
        public const string HandName = "tutorial-hand";

        // Вигляд, не баланс. Плашка стоїть над гриллем: безпечна зона знизу + висота слота гриля + відступи.
        private const float BottomOffset = 352f;
        private const float SideOffset = 24f;
        private const float Padding = 24f;
        private const float TextSize = 40f;
        private const float ButtonHeight = 120f;
        private const float ButtonFontSize = 36f;
        private const float HighlightWidth = 8f;
        private const float FrameWidth = 6f;
        private const float FrameRadius = 28f;
        private const float HandWidth = 96f;
        private const float HandHeight = 132f;
        private const float RectEpsilon = 0.5f;

        private static readonly Color PlaqueColor = UiColors.Plaque;
        private static readonly Color PlaqueBorder = new Color(1f, 0.85f, 0.4f);
        private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.4f);
        private const float AliSize = 120f;
        private static readonly Color ButtonColor = UiColors.TutorialButton;
        private static readonly Color DimColor = new Color(0f, 0f, 0f, OverlayMotion.DimAlpha);

        private readonly TutorialDirector _director;
        private readonly ILocalizer _text;
        private readonly Action _onChanged;
        private readonly VisualElement _parent;
        private readonly VisualElement _keepFree;
        private readonly VisualElement _plaque;
        private readonly MotionTarget _plaqueMotion;
        private readonly Label _label;
        private readonly Button _nextButton;
        private readonly Button _skipAllButton;
        private readonly VisualElement _grillZone;
        private readonly VisualElement _assemblyZone;
        private readonly VisualElement _counterZone;
        private readonly VisualElement _dim;
        private readonly MotionTarget _dimMotion;
        private readonly VisualElement[] _dimmers = new VisualElement[4];
        private readonly Rect[] _dimRects = new Rect[4];
        private readonly VisualElement _frame;
        private readonly MotionTarget _frameMotion;
        private readonly VisualElement _hand;
        private readonly MotionTarget _handMotion;
        private TutorialStep _shown;
        private VisualElement _highlighted;
        private StyleFloat _savedWidth;
        private StyleColor _savedColor;
        private string _fullText = string.Empty;
        private int _typed = -1;
        private float _stepAt;
        private float _blockedAt = -1f;
        private bool _spotlight;
        private Rect _lastCutout;

        /// <param name="onChanged">Викликається, коли профіль змінився через кнопку (наприклад «Пропустити все») і його варто зберегти; може бути null.</param>
        /// <param name="keepFree">Елемент угорі екрана (смуга з кнопкою паузи): прожектор його не затемнює й не блокує; може бути null.</param>
        public TutorialOverlay(
            VisualElement parent,
            TutorialDirector director,
            ILocalizer text,
            VisualElement grillZone,
            VisualElement assemblyZone,
            VisualElement counterZone,
            Action onChanged = null,
            VisualElement keepFree = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            _parent = parent;
            _keepFree = keepFree;
            _director = director ?? throw new ArgumentNullException(nameof(director));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _grillZone = grillZone;
            _assemblyZone = assemblyZone;
            _counterZone = counterZone;
            _onChanged = onChanged;

            _dim = Fill(DimName);
            _dim.style.display = DisplayStyle.None;
            parent.Add(_dim);
            _dimMotion = new MotionTarget(_dim);
            for (int i = 0; i < _dimmers.Length; i++)
            {
                var dimmer = new VisualElement { name = DimName + "-" + i };
                dimmer.style.position = Position.Absolute;
                dimmer.style.backgroundColor = DimColor;
                dimmer.pickingMode = PickingMode.Position; // QB-TUT-05: тап поза вирізом не доходить до гри.
                dimmer.RegisterCallback<PointerDownEvent>(_ => OnBlockedTap());
                _dim.Add(dimmer);
                _dimmers[i] = dimmer;
            }

            _frame = new VisualElement { name = FrameName, pickingMode = PickingMode.Ignore };
            _frame.style.position = Position.Absolute;
            SetBorder(_frame, FrameWidth, HighlightColor);
            SetRadius(_frame, FrameRadius);
            _dim.Add(_frame);
            _frameMotion = new MotionTarget(_frame);

            _hand = BuildHand();
            _dim.Add(_hand);
            _handMotion = new MotionTarget(_hand);

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
            SetBorder(_plaque, 4f, PlaqueBorder);
            _plaque.style.display = DisplayStyle.None;
            _plaque.pickingMode = PickingMode.Ignore;
            parent.Add(_plaque);
            _plaqueMotion = new MotionTarget(_plaque);
            UiDecor.AddPanelShadow(_plaque);

            // Дядько Алі (S1-12): портрет у рядку з текстом, ліворуч (S1-17: раніше стояв над краєм плашки
            // й перекривав кнопку «Очистити дошку»). Немає спрайта — просто не показується.
            var head = new VisualElement { name = "tutorial-head", pickingMode = PickingMode.Ignore };
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            _plaque.Add(head);

            var portrait = new VisualElement { name = "tutorial-portrait", pickingMode = PickingMode.Ignore };
            portrait.style.flexShrink = 0;
            portrait.style.width = AliSize;
            portrait.style.height = AliSize;
            portrait.style.marginRight = Padding;
            ArtLibrary.Apply(portrait, ArtLibrary.Ali);
            head.Add(portrait);

            _label = new Label(string.Empty);
            _label.style.fontSize = TextSize;
            _label.style.color = Color.white;
            _label.style.whiteSpace = WhiteSpace.Normal;
            _label.style.unityTextAlign = TextAnchor.MiddleLeft;
            _label.pickingMode = PickingMode.Position; // QB-TUT-03: тап по репліці показує її повністю.
            _label.RegisterCallback<PointerDownEvent>(_ => CompleteTyping());
            _label.style.flexGrow = 1;
            _label.style.flexShrink = 1;
            head.Add(_label);

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

            // S1-17: плашка стоїть над грилем за його справжнім положенням, а не на сталих 352 px.
            if (grillZone != null)
            {
                grillZone.RegisterCallback<GeometryChangedEvent>(_ => AnchorAboveGrill(parent, grillZone));
            }
        }

        /// <summary>Плашку показано.</summary>
        public bool IsVisible => _shown != null;

        /// <summary>Id показаного кроку; null — плашки немає.</summary>
        public string ShownStepId => _shown == null ? null : _shown.Id;

        /// <summary>Повний текст підказки (на екрані він друкується, див. <see cref="ShownText"/>).</summary>
        public string Text => _fullText;

        /// <summary>Те, що зараз надруковано на плашці.</summary>
        public string ShownText => _label.text;

        public bool NextButtonVisible => _nextButton.style.display.value != DisplayStyle.None;

        public bool SkipAllButtonVisible => _skipAllButton.style.display.value != DisplayStyle.None;

        /// <summary>Зона, яку зараз підсвічено; None, якщо підсвітки немає.</summary>
        public TutorialZone HighlightedZone { get; private set; }

        /// <summary>Чи крок із дією: прожектор і рука показуються лише для таких (QB-TUT-05: не блокуємо інформаційні кроки).</summary>
        public bool SpotlightActive => _spotlight;

        /// <summary>Чи показано затемнення з вирізом.</summary>
        public bool DimVisible => _dim.style.display.value == DisplayStyle.Flex;

        /// <summary>Поточний виріз прожектора (локальні координати екрана зміни).</summary>
        public Rect Cutout => _lastCutout;

        /// <summary>Рух руки-вказівника (для тестів).</summary>
        public MotionTarget HandMotion => _handMotion;

        /// <summary>Рух рамки вирізу (для тестів).</summary>
        public MotionTarget FrameMotion => _frameMotion;

        /// <summary>Рух плашки (поява).</summary>
        public MotionTarget PlaqueMotion => _plaqueMotion;

        /// <summary>Тап поза вирізом: рамка коротко пульсує (м'який відгук замість «мертвої» тиші).</summary>
        public void OnBlockedTap()
        {
            _blockedAt = UiMotion.Clock();
        }

        /// <summary>Показує репліку повністю (тап по тексту).</summary>
        public void CompleteTyping()
        {
            ShowTyped(_fullText.Length);
        }

        /// <summary>Синхронізує плашку з поточним кроком і рухає анімації.</summary>
        public void Refresh()
        {
            TutorialStep step = _director.Current;
            if (!ReferenceEquals(step, _shown))
            {
                ClearHighlight();
                _shown = step;
                _spotlight = false;
                if (step == null)
                {
                    _plaque.style.display = DisplayStyle.None;
                    _dim.style.display = DisplayStyle.None;
                    _fullText = string.Empty;
                    _typed = -1;
                    _label.text = string.Empty;
                    return;
                }

                _fullText = _text.Get(step.TextKey);
                _typed = -1;
                _label.text = string.Empty;
                _stepAt = UiMotion.Clock();
                _nextButton.style.display = _director.Model.CanSkipStep ? DisplayStyle.Flex : DisplayStyle.None;
                _skipAllButton.style.display = _director.Model.CanSkipAll ? DisplayStyle.Flex : DisplayStyle.None;
                _plaque.style.display = DisplayStyle.Flex;
                SetHighlight(TutorialAnchors.ZoneOf(step.Anchor));
                _spotlight = step.CompleteWhen != null && HighlightedZone != TutorialZone.None;
                _lastCutout = default(Rect);
                _hand.style.display = _spotlight ? DisplayStyle.Flex : DisplayStyle.None;
                _frame.style.display = _spotlight ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_shown == null)
            {
                return;
            }

            float t = UiMotion.Clock() - _stepAt;
            _plaqueMotion.Set(1f, 1f, 0f, OverlayMotion.PlaqueOffset(t), OverlayMotion.PlaqueOpacity(t));
            ShowTyped(OverlayMotion.TypedChars(t, _fullText.Length));
            if (_spotlight)
            {
                UpdateSpotlight(t);
            }
        }

        /// <summary>
        /// Розставляє затемнення, рамку й руку навколо цілі. <paramref name="target"/> і <paramref name="bounds"/> -
        /// у локальних координатах екрана зміни; <paramref name="elapsedSec"/> - час від показу кроку (відкриття вирізу).
        /// </summary>
        public void ApplySpotlight(Rect target, Rect bounds, float elapsedSec)
        {
            float progress = OverlayMotion.SpotlightProgress(elapsedSec);
            Rect cut = OverlayMotion.Cutout(target, bounds, progress);
            _dim.style.display = DisplayStyle.Flex;
            _dimMotion.Set(1f, 1f, 0f, 0f, progress);
            if (!SameRect(cut, _lastCutout))
            {
                _lastCutout = cut;
                OverlayMotion.DimRects(bounds, cut, _dimRects);
                for (int i = 0; i < _dimmers.Length; i++)
                {
                    Place(_dimmers[i], _dimRects[i]);
                }

                Place(_frame, cut);
                _hand.style.left = cut.center.x - (HandWidth / 2f);
                _hand.style.top = cut.center.y - (HandHeight * 0.1f);
            }

            float pulse = OverlayMotion.FramePulse(elapsedSec) * OverlayMotion.BlockedPulse(_blockedAt < 0f ? -1f : UiMotion.Clock() - _blockedAt);
            _frameMotion.Set(pulse, pulse, 0f, 0f, 1f);
            _handMotion.Set(OverlayMotion.HandScale(elapsedSec), OverlayMotion.HandScale(elapsedSec), 0f, OverlayMotion.HandOffset(elapsedSec), 1f);
        }

        private void UpdateSpotlight(float t)
        {
            VisualElement target = ElementOf(HighlightedZone);
            if (target == null)
            {
                return;
            }

            Rect world = target.worldBound;
            if (float.IsNaN(world.width) || float.IsNaN(world.height) || world.width <= 0f || world.height <= 0f)
            {
                return;
            }

            float parentWidth = _parent.layout.width;
            float parentHeight = _parent.layout.height;
            if (float.IsNaN(parentWidth) || float.IsNaN(parentHeight) || parentWidth <= 0f || parentHeight <= 0f)
            {
                return;
            }

            // Верхня смуга з кнопкою паузи лишається світлою й доступною.
            float top = 0f;
            if (_keepFree != null)
            {
                Rect free = _keepFree.worldBound;
                if (!float.IsNaN(free.yMax))
                {
                    top = Mathf.Max(0f, _parent.WorldToLocal(new Vector2(free.x, free.yMax)).y);
                }
            }

            var bounds = new Rect(0f, top, parentWidth, Mathf.Max(0f, parentHeight - top));
            var local = new Rect(_parent.WorldToLocal(world.position), world.size);
            ApplySpotlight(local, bounds, t);
        }

        private void ShowTyped(int count)
        {
            if (count == _typed)
            {
                return;
            }

            _typed = count;
            _label.text = count >= _fullText.Length ? _fullText : _fullText.Substring(0, count);
        }

        private void AnchorAboveGrill(VisualElement parent, VisualElement grill)
        {
            float parentHeight = parent.layout.height;
            float top = grill.layout.y;
            if (float.IsNaN(parentHeight) || float.IsNaN(top) || parentHeight <= 0f)
            {
                return;
            }

            float bottom = parentHeight - top + (Padding / 2f);
            if (Mathf.Abs(_plaque.style.bottom.value.value - bottom) > 0.5f)
            {
                _plaque.style.bottom = bottom;
            }
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
            SetBorder(target, HighlightWidth, HighlightColor);
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

        private static bool SameRect(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < RectEpsilon && Mathf.Abs(a.y - b.y) < RectEpsilon
                && Mathf.Abs(a.width - b.width) < RectEpsilon && Mathf.Abs(a.height - b.height) < RectEpsilon;
        }

        private static void Place(VisualElement element, Rect rect)
        {
            element.style.left = rect.x;
            element.style.top = rect.y;
            element.style.width = Mathf.Max(0f, rect.width);
            element.style.height = Mathf.Max(0f, rect.height);
        }

        private static VisualElement Fill(string name)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
            return element;
        }

        private static void SetBorder(VisualElement element, float width, Color color)
        {
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        /// <summary>Рука-вказівник (плейсхолдер без графіки): палець і долоня з білих заокруглених форм із темною обводкою.</summary>
        private static VisualElement BuildHand()
        {
            var hand = new VisualElement { name = HandName, pickingMode = PickingMode.Ignore };
            hand.style.position = Position.Absolute;
            hand.style.width = HandWidth;
            hand.style.height = HandHeight;
            hand.style.alignItems = Align.Center;
            hand.style.display = DisplayStyle.None;

            VisualElement finger = HandPart(40f, 72f, 20f);
            hand.Add(finger);
            VisualElement palm = HandPart(HandWidth, 62f, 28f);
            palm.style.marginTop = -8f;
            hand.Add(palm);
            return hand;
        }

        private static VisualElement HandPart(float width, float height, float radius)
        {
            var part = new VisualElement { pickingMode = PickingMode.Ignore };
            part.style.width = width;
            part.style.height = height;
            part.style.flexShrink = 0;
            part.style.backgroundColor = Color.white;
            SetBorder(part, 4f, UiColors.Espresso);
            SetRadius(part, radius);
            return part;
        }

        private static Button AddButton(VisualElement parent, string text, Action onClick)
        {
            var button = new Button(SfxHub.WithTapSound(onClick)) { text = text };
            button.AddToClassList(UiFactory.PressClass);
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
