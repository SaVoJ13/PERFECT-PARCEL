using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Відгук на тап для однієї кнопки (S1-21, ADR-027): рамка підсвічування і смужка прогресу дії
    /// зверху кнопки. Обидва елементи створюються один раз, не ловлять дотики (<c>PickingMode.Ignore</c>);
    /// стиль міняється лише коли значення змінилось, тож у <see cref="Update"/> немає виділення пам'яті.
    /// </summary>
    public sealed class TapDecor
    {
        private const float FrameWidth = 10f;
        private const float BarHeight = 16f;

        private readonly VisualElement _frame;
        private readonly VisualElement _track;
        private readonly VisualElement _fill;
        private bool _lit;
        private Color _frameColor;
        private int _percent = -1;

        public TapDecor(VisualElement host, string name)
        {
            _frame = new VisualElement { name = name + "-tap-frame", pickingMode = PickingMode.Ignore };
            _frame.style.position = Position.Absolute;
            _frame.style.left = 0;
            _frame.style.top = 0;
            _frame.style.right = 0;
            _frame.style.bottom = 0;
            _frame.style.borderLeftWidth = FrameWidth;
            _frame.style.borderRightWidth = FrameWidth;
            _frame.style.borderTopWidth = FrameWidth;
            _frame.style.borderBottomWidth = FrameWidth;
            _frame.style.display = DisplayStyle.None;
            host.Add(_frame);

            _track = new VisualElement { name = name + "-action-bar", pickingMode = PickingMode.Ignore };
            _track.style.position = Position.Absolute;
            _track.style.left = 0;
            _track.style.top = 0;
            _track.style.right = 0;
            _track.style.height = BarHeight;
            _track.style.backgroundColor = UiColors.ActionTrack;
            _track.style.display = DisplayStyle.None;
            host.Add(_track);

            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.style.height = BarHeight;
            _fill.style.width = Length.Percent(0);
            _fill.style.backgroundColor = UiColors.ActionFill;
            _track.Add(_fill);
        }

        /// <summary>Чи рамка підсвічування показана.</summary>
        public bool Lit => _lit;

        /// <summary>Скільки відсотків смужки заповнено; -1 — смужка прихована.</summary>
        public int Percent => _percent;

        /// <param name="lit">Елемент щойно натиснули.</param>
        /// <param name="percent">Прогрес дії на елементі, 0..99; від'ємне — дії немає.</param>
        /// <param name="background">Поточний фон кнопки: рамка бере контрастніший із двох кольорів.</param>
        public void Update(bool lit, int percent, Color background)
        {
            if (lit)
            {
                Color color = UiColors.TapFrameFor(background);
                if (!_lit || color != _frameColor)
                {
                    _frame.style.borderLeftColor = color;
                    _frame.style.borderRightColor = color;
                    _frame.style.borderTopColor = color;
                    _frame.style.borderBottomColor = color;
                    _frameColor = color;
                }
            }

            if (lit != _lit)
            {
                _lit = lit;
                _frame.style.display = lit ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (percent != _percent)
            {
                if (percent < 0 != _percent < 0)
                {
                    _track.style.display = percent < 0 ? DisplayStyle.None : DisplayStyle.Flex;
                }

                if (percent >= 0)
                {
                    _fill.style.width = Length.Percent(percent);
                }

                _percent = percent;
            }
        }
    }
}
