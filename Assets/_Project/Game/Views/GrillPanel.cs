using System;
using KebabStreet.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Гриль на екрані зміни (S1-09a): кнопка на кожен слот конфіга рівня, у ній напис стану
    /// і смужка поступу. Лише показує <see cref="GrillSlotView"/> і шле тапи в
    /// <see cref="ShiftDriver.Tap"/>; правил гри тут немає. Елементи оновлюються, лише коли
    /// вигляд слота змінився. Плейсхолдер без графіки (S1-12).
    /// </summary>
    public sealed class GrillPanel
    {
        /// <summary>Ім'я елемента зони гриля в дереві UI.</summary>
        public const string ZoneName = "grill-zone";

        // 48 dp ≈ 144 px на референсі 1080x1920; станція — не менше 200x200 px (ART_STYLE_GUIDE 4.1).
        private const float SlotSize = 240f;
        private const float SlotGap = 24f;
        private const float SlotFontSize = 34f;
        private const float BarHeight = 28f;
        private const float BorderWidth = 8f;
        private const int WarningTextIndex = 5;
        private const int TextCount = 6;

        private static readonly Color EmptyColor = new Color(0.42f, 0.27f, 0.19f);
        private static readonly Color RawColor = new Color(0.91f, 0.51f, 0.48f);
        private static readonly Color CookingColor = new Color(0.80f, 0.42f, 0.30f);
        private static readonly Color DoneColor = new Color(0.18f, 0.62f, 0.31f);
        private static readonly Color WarningColor = new Color(0.96f, 0.62f, 0.04f);
        private static readonly Color BurntColor = new Color(0.10f, 0.08f, 0.08f);
        private static readonly Color BurntBorder = new Color(0.84f, 0.16f, 0.16f);
        private static readonly Color BarColor = new Color(1f, 0.95f, 0.84f);
        private static readonly Color QueuedBorder = Color.white;
        private static readonly Color IdleBorder = new Color(0.23f, 0.14f, 0.08f);

        private readonly ShiftDriver _driver;
        private readonly Button[] _buttons;
        private readonly VisualElement[] _bars;
        private readonly GrillSlotView[] _shown;
        private readonly bool[] _drawn;
        private readonly string[] _texts = new string[TextCount];

        public GrillPanel(VisualElement parent, ShiftDriver driver, ILocalizer text)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            _driver = driver ?? throw new ArgumentNullException(nameof(driver));

            _texts[(int)GrillSlotDisplay.Empty] = text.Get(TextKeys.GrillSlotEmpty);
            _texts[(int)GrillSlotDisplay.Raw] = text.Get(TextKeys.GrillSlotRaw);
            _texts[(int)GrillSlotDisplay.Cooking] = text.Get(TextKeys.GrillSlotCooking);
            _texts[(int)GrillSlotDisplay.Done] = text.Get(TextKeys.GrillSlotDone);
            _texts[(int)GrillSlotDisplay.Burnt] = text.Get(TextKeys.GrillSlotBurnt);
            _texts[WarningTextIndex] = text.Get(TextKeys.GrillSlotBurning);

            Root = new VisualElement { name = ZoneName };
            Root.style.flexDirection = FlexDirection.Row;
            Root.style.flexWrap = Wrap.Wrap;
            Root.style.justifyContent = Justify.Center;
            Root.style.flexShrink = 0;
            parent.Add(Root);

            Grill grill = driver.Simulation.Grill;
            int count = grill == null ? 0 : grill.SlotCount;
            _buttons = new Button[count];
            _bars = new VisualElement[count];
            _shown = new GrillSlotView[count];
            _drawn = new bool[count];
            for (int i = 0; i < count; i++)
            {
                AddSlot(i);
            }

            Refresh();
        }

        /// <summary>Контейнер зони гриля.</summary>
        public VisualElement Root { get; }

        /// <summary>Скільки слотів показано (grill.slots рівня).</summary>
        public int SlotCount => _buttons.Length;

        /// <summary>Напис, який зараз показано в слоті.</summary>
        public string GetSlotText(int slot)
        {
            return _buttons[slot].text;
        }

        /// <summary>Вигляд слота, який зараз показано.</summary>
        public GrillSlotView GetShownView(int slot)
        {
            return _shown[slot];
        }

        /// <summary>
        /// Тап по слоту: команда за поточним виглядом слота йде в <see cref="ShiftDriver.Tap"/>.
        /// true — команду прийнято до черги.
        /// </summary>
        public bool TapSlot(int slot)
        {
            if (slot < 0 || slot >= _buttons.Length)
            {
                return false;
            }

            ShiftTap tap;
            GrillSlotView view = GrillView.FromSimulation(_driver.Simulation, slot);
            return GrillView.TryGetTap(view, out tap) && _driver.Tap(tap, slot);
        }

        /// <summary>Оновлює слоти, вигляд яких змінився. Викликається щокадру; сміття не створює.</summary>
        public void Refresh()
        {
            ShiftSimulation simulation = _driver.Simulation;
            for (int i = 0; i < _buttons.Length; i++)
            {
                GrillSlotView view = GrillView.FromSimulation(simulation, i);
                if (_drawn[i] && view.Equals(_shown[i]))
                {
                    continue;
                }

                Draw(i, view, !_drawn[i] ? default(GrillSlotView) : _shown[i], !_drawn[i]);
                _shown[i] = view;
                _drawn[i] = true;
            }
        }

        private void AddSlot(int index)
        {
            var button = new Button(() => TapSlot(index)) { name = "grill-slot-" + index };
            button.style.width = SlotSize;
            button.style.height = SlotSize;
            button.style.marginLeft = SlotGap / 2f;
            button.style.marginRight = SlotGap / 2f;
            button.style.marginTop = SlotGap / 2f;
            button.style.marginBottom = SlotGap / 2f;
            button.style.fontSize = SlotFontSize;
            button.style.color = Color.white;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.borderLeftWidth = BorderWidth;
            button.style.borderRightWidth = BorderWidth;
            button.style.borderTopWidth = BorderWidth;
            button.style.borderBottomWidth = BorderWidth;

            var bar = new VisualElement { name = "grill-slot-bar-" + index };
            bar.pickingMode = PickingMode.Ignore;
            bar.style.position = Position.Absolute;
            bar.style.left = 0;
            bar.style.bottom = 0;
            bar.style.height = BarHeight;
            bar.style.width = Length.Percent(0);
            bar.style.backgroundColor = BarColor;
            button.Add(bar);

            Root.Add(button);
            _buttons[index] = button;
            _bars[index] = bar;
        }

        private void Draw(int index, GrillSlotView view, GrillSlotView previous, bool first)
        {
            Button button = _buttons[index];
            if (first || view.ProgressPercent != previous.ProgressPercent)
            {
                _bars[index].style.width = Length.Percent(view.ProgressPercent);
            }

            if (first || view.Display != previous.Display || view.BurnWarning != previous.BurnWarning)
            {
                bool warning = view.Display == GrillSlotDisplay.Done && view.BurnWarning;
                button.text = _texts[warning ? WarningTextIndex : (int)view.Display];
                button.style.backgroundColor = ColorOf(view);
            }

            if (first || view.Display != previous.Display || view.ActionQueued != previous.ActionQueued)
            {
                Color border = view.ActionQueued
                    ? QueuedBorder
                    : (view.Display == GrillSlotDisplay.Burnt ? BurntBorder : IdleBorder);
                button.style.borderLeftColor = border;
                button.style.borderRightColor = border;
                button.style.borderTopColor = border;
                button.style.borderBottomColor = border;
            }
        }

        private static Color ColorOf(GrillSlotView view)
        {
            switch (view.Display)
            {
                case GrillSlotDisplay.Raw:
                    return RawColor;
                case GrillSlotDisplay.Cooking:
                    return CookingColor;
                case GrillSlotDisplay.Done:
                    return view.BurnWarning ? WarningColor : DoneColor;
                case GrillSlotDisplay.Burnt:
                    return BurntColor;
                default:
                    return EmptyColor;
            }
        }
    }
}
