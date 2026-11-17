using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Видиме комбо (Q-11, P3_SPEC розд. 3): «Комбо xN», «наступний бонус +X %», смужка вікна, що спадає після
    /// кожної видачі (мерехтить в останні 2 с), і причина скидання. Лише показує <see cref="ComboTracker"/>:
    /// числа й правила беруться з Core. Елементи створюються один раз; стиль пишеться лише коли показ змінився,
    /// тож щокадрових алокацій немає. «Менше анімацій» (<see cref="UiMotion.Reduced"/>) прибирає підскок і мерехтіння.
    /// </summary>
    public sealed class ComboMeter
    {
        public const string ColumnName = "assembly-combo-column";
        private const float FontSize = 34f;
        private const float SmallFontSize = 28f;
        private const float BarHeight = 12f;
        private const float Gap = 12f;
        private const float PopPeak = 1.3f;
        private const float PopSec = 0.2f;
        private const float NoTime = -1000f;

        private readonly ILocalizer _text;
        private readonly string _comboFormat;
        private readonly string _nextFormat;
        private readonly string _maxFormat;
        private readonly VisualElement _column;
        private readonly Label _combo;
        private readonly Label _next;
        private readonly VisualElement _barFrame;
        private readonly VisualElement _fill;
        private readonly Label _reason;
        private readonly MotionTarget _comboMotion;
        private readonly MotionTarget _barMotion;
        private readonly MotionTarget _reasonMotion;
        private ComboHudView _shown;
        private ComboLostReason _shownReason;
        private long _shownLabelCombo;
        private bool _drawn;
        private bool _warnShown;
        private float _popAt = NoTime;
        private float _reasonAt = NoTime;

        /// <param name="column">Куди додати смужку і підписи (праворуч у рядку приміток).</param>
        /// <param name="reasonParent">Куди додати рядок причини скидання (окремий рядок під примітками).</param>
        public ComboMeter(VisualElement column, VisualElement reasonParent, ILocalizer text)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            if (reasonParent == null)
            {
                throw new ArgumentNullException(nameof(reasonParent));
            }

            _text = text ?? throw new ArgumentNullException(nameof(text));
            _comboFormat = text.Get(TextKeys.AssemblyCombo);
            _nextFormat = text.Get(TextKeys.ComboNext);
            _maxFormat = text.Get(TextKeys.ComboMax);

            _column = new VisualElement { name = ColumnName, pickingMode = PickingMode.Ignore };
            _column.style.flexDirection = FlexDirection.Column;
            _column.style.flexShrink = 0;
            _column.style.alignItems = Align.Stretch;
            _column.style.display = DisplayStyle.None;
            column.Add(_column);

            _combo = AddLabel(_column, "assembly-combo", FontSize, UiColors.Coin);
            _next = AddLabel(_column, "assembly-combo-next", SmallFontSize, UiColors.Coin);

            _barFrame = new VisualElement { name = "assembly-combo-bar-frame", pickingMode = PickingMode.Ignore };
            _barFrame.style.height = BarHeight;
            _barFrame.style.marginTop = Gap / 3f;
            _barFrame.style.backgroundColor = UiColors.LabelBackdrop;
            _barFrame.style.borderTopLeftRadius = BarHeight / 2f;
            _barFrame.style.borderTopRightRadius = BarHeight / 2f;
            _barFrame.style.borderBottomLeftRadius = BarHeight / 2f;
            _barFrame.style.borderBottomRightRadius = BarHeight / 2f;
            _column.Add(_barFrame);
            _fill = new VisualElement { name = "assembly-combo-bar", pickingMode = PickingMode.Ignore };
            _fill.style.height = BarHeight;
            _fill.style.width = Length.Percent(0);
            _fill.style.backgroundColor = UiColors.ComboBar;
            _fill.style.borderTopLeftRadius = BarHeight / 2f;
            _fill.style.borderTopRightRadius = BarHeight / 2f;
            _fill.style.borderBottomLeftRadius = BarHeight / 2f;
            _fill.style.borderBottomRightRadius = BarHeight / 2f;
            _barFrame.Add(_fill);

            _reason = AddLabel(reasonParent, "assembly-combo-reason", SmallFontSize, UiColors.Warning);
            _reason.style.display = DisplayStyle.None;

            _comboMotion = new MotionTarget(_combo);
            _barMotion = new MotionTarget(_barFrame);
            _reasonMotion = new MotionTarget(_reason);
        }

        /// <summary>Напис «Комбо xN» (порожньо, поки комбо менше <see cref="AssemblyView.ComboShownFrom"/>).</summary>
        public string ComboText => _combo.text;

        /// <summary>Напис «наступний бонус» / «максимум» (порожньо, коли смужки немає).</summary>
        public string NextText => _next.text;

        /// <summary>Текст причини скидання; порожньо, коли її не показано.</summary>
        public string ReasonText => _reason.text;

        /// <summary>Чи показано блок комбо (смужка і підписи).</summary>
        public bool Visible => _column.style.display.value == DisplayStyle.Flex;

        /// <summary>Чи показано рядок причини скидання.</summary>
        public bool ReasonVisible => _reason.style.display.value == DisplayStyle.Flex;

        /// <summary>Скільки вікна лишилось, що намальовано, 0..100 %.</summary>
        public int BarPercent => _shown.BarPercent;

        /// <summary>Смужка зараз у стані «спливає».</summary>
        public bool WarningShown => _warnShown;

        /// <summary>Рух напису комбо (підскок з кожним кроком; для тестів).</summary>
        public MotionTarget ComboMotion => _comboMotion;

        /// <summary>Рух смужки (мерехтіння в останні 2 с; для тестів).</summary>
        public MotionTarget BarMotion => _barMotion;

        /// <summary>Оновлює показ за знімком трекера. Викликається щокадру; без алокацій, якщо показ не змінився.</summary>
        public void Refresh(ComboTracker tracker)
        {
            if (tracker == null)
            {
                throw new ArgumentNullException(nameof(tracker));
            }

            float now = UiMotion.Now;
            ComboHudView view = tracker.Hud;
            if (!_drawn || !view.Equals(_shown))
            {
                Draw(view, now);
                _shown = view;
            }

            ComboLostReason reason = tracker.Lost;
            if (!_drawn || reason != _shownReason)
            {
                string key = ComboView.LostTextKey(reason);
                _reason.text = key == null ? string.Empty : _text.Get(key);
                _reason.style.display = key == null ? DisplayStyle.None : DisplayStyle.Flex;
                _reasonAt = key == null ? NoTime : now;
                _shownReason = reason;
            }

            _drawn = true;

            float pop = UiMotion.Pop(now - _popAt, PopSec, PopPeak);
            _comboMotion.Set(pop, pop, 0f, 0f, 1f);
            float blink = view.Warning ? UiMotion.ComboWarnBlink(now) : 1f;
            _barMotion.Set(1f, 1f, 0f, 0f, blink);
            float reasonPop = UiMotion.Pop(now - _reasonAt, PopSec, PopPeak);
            _reasonMotion.Set(reasonPop, reasonPop, 0f, 0f, 1f);
        }

        private void Draw(ComboHudView view, float now)
        {
            _column.style.display = view.BarShown ? DisplayStyle.Flex : DisplayStyle.None;
            long label = AssemblyView.ComboShown(view.Combo);
            if (!_drawn || label != _shownLabelCombo)
            {
                _combo.text = label > 0L ? string.Format(_comboFormat, label) : string.Empty;
                _combo.style.display = label > 0L ? DisplayStyle.Flex : DisplayStyle.None;
                if (_drawn && label > 0L)
                {
                    _popAt = now; // QB-SHIFT-30: «вистриб» з кожним кроком.
                }

                _shownLabelCombo = label;
            }

            if (!_drawn || view.BarShown != _shown.BarShown || view.AtMax != _shown.AtMax
                || view.NextBonusPercent != _shown.NextBonusPercent)
            {
                _next.text = !view.BarShown
                    ? string.Empty
                    : string.Format(view.AtMax ? _maxFormat : _nextFormat, view.NextBonusPercent);
            }

            _fill.style.width = Length.Percent(view.BarPercent);
            bool warn = view.BarShown && view.Warning;
            if (!_drawn || warn != _warnShown)
            {
                // Без «менше анімацій» мерехтить прозорість (рух), з ним - лише колір: тривога читається без руху.
                _fill.style.backgroundColor = warn ? UiColors.ComboBarWarning : UiColors.ComboBar;
                _warnShown = warn;
            }
        }

        private static Label AddLabel(VisualElement parent, string name, float size, Color color)
        {
            var label = new Label(string.Empty) { name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.flexShrink = 1;
            label.style.backgroundColor = UiColors.LabelBackdrop;
            label.style.paddingLeft = Gap;
            label.style.paddingRight = Gap;
            label.style.paddingTop = Gap / 3f;
            label.style.paddingBottom = Gap / 3f;
            label.style.borderTopLeftRadius = Gap;
            label.style.borderTopRightRadius = Gap;
            label.style.borderBottomLeftRadius = Gap;
            label.style.borderBottomRightRadius = Gap;
            parent.Add(label);
            return label;
        }
    }
}
