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
        private const float IconSize = 168f;
        private const float EmptyHintOpacity = 0.4f;
        private const float BorderWidth = 8f;
        private const int WarningTextIndex = 5;
        private const int TextCount = 6;

        // Q-05d: пара 2-4/с (QB-SHIFT-17), дим 4-8/с (QB-SHIFT-21); це вигляд, не баланс.
        private const float SteamPerSec = 3f;
        private const float SmokePerSec = 6f;
        private const float MaxEmitStepSec = 0.1f;

        private static readonly Color EmptyColor = UiColors.GrillEmpty;
        private static readonly Color RawColor = UiColors.GrillRaw;
        private static readonly Color CookingColor = UiColors.GrillCooking;
        private static readonly Color DoneColor = UiColors.GrillDone;
        private static readonly Color WarningColor = UiColors.GrillWarning;
        private static readonly Color BurntColor = UiColors.GrillBurnt;
        private static readonly Color BurntBorder = new Color(0.84f, 0.16f, 0.16f);
        private static readonly Color BarColor = new Color(1f, 0.95f, 0.84f);
        private static readonly Color QueuedBorder = Color.white;
        private static readonly Color IdleBorder = new Color(0.23f, 0.14f, 0.08f);

        private readonly ShiftDriver _driver;
        private readonly ShiftInput _input;
        private readonly Button[] _buttons;
        private readonly VisualElement[] _bars;
        private readonly VisualElement[] _icons;
        private readonly TapDecor[] _decors;
        private readonly GrillSlotView[] _shown;
        private readonly bool[] _drawn;
        private readonly string[] _texts = new string[TextCount];
        private readonly string[] _slotTexts;
        private readonly FxLayer _fx;
        private readonly MotionTarget[] _iconMotion;
        private readonly float[] _dropAt;
        private readonly float[] _doneAt;
        private readonly float[] _emitted;
        private float _lastNow;

        public GrillPanel(VisualElement parent, ShiftInput input, ILocalizer text, FxLayer fx = null)
        {
            _fx = fx;
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            _input = input ?? throw new ArgumentNullException(nameof(input));
            _driver = input.Driver;

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

            Grill grill = _driver.Simulation.Grill;
            int count = grill == null ? 0 : grill.SlotCount;
            _buttons = new Button[count];
            _bars = new VisualElement[count];
            _icons = new VisualElement[count];
            _decors = new TapDecor[count];
            _shown = new GrillSlotView[count];
            _slotTexts = new string[count];
            _drawn = new bool[count];
            _iconMotion = new MotionTarget[count];
            _dropAt = new float[count];
            _doneAt = new float[count];
            _emitted = new float[count];
            _lastNow = UiMotion.Now;
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

        /// <summary>
        /// Локалізований напис стану слота (a11y, tooltip). Q-05b: на екрані стан показано значком; сам напис видно
        /// лише коли спрайта немає.
        /// </summary>
        public string GetSlotText(int slot)
        {
            return _slotTexts[slot];
        }

        /// <summary>Чи слот показує значок (а не напис) стану.</summary>
        public bool IsSlotIconShown(int slot)
        {
            return _icons[slot].style.display.value == DisplayStyle.Flex;
        }

        /// <summary>Відгук на тап слота (S1-21): рамка світиться, смужка дії.</summary>
        public TapDecor GetDecor(int slot)
        {
            return _decors[slot];
        }

        /// <summary>Рух значка м'яса в слоті (падіння, вистриб, пульс, тремтіння); для тестів.</summary>
        public MotionTarget GetIconMotion(int slot)
        {
            return _iconMotion[slot];
        }

        /// <summary>Вигляд слота, який зараз показано.</summary>
        public GrillSlotView GetShownView(int slot)
        {
            return _shown[slot];
        }

        /// <summary>
        /// Тап по слоту: команда за поточним виглядом слота йде через <see cref="ShiftInput"/> у
        /// <see cref="ShiftDriver.Tap"/>; тап по м'ясу, що готується, дає підказку (BUG-037).
        /// true — команду прийнято до черги.
        /// </summary>
        public bool TapSlot(int slot)
        {
            return _input.TapGrillSlot(slot);
        }

        /// <summary>Оновлює слоти, вигляд яких змінився. Викликається щокадру; сміття не створює.</summary>
        public void Refresh()
        {
            ShiftSimulation simulation = _driver.Simulation;
            float now = UiMotion.Now;
            float step = Mathf.Clamp(now - _lastNow, 0f, MaxEmitStepSec);
            _lastNow = now;
            for (int i = 0; i < _buttons.Length; i++)
            {
                GrillSlotView view = GrillView.FromSimulation(simulation, i);

                // S1-21: відгук на тап - рамка і смужка дії; оновлюється щокадру, стиль міняється лише за зміни.
                _decors[i].Update(
                    _input.IsLit(FeedbackTarget.GrillSlot, i), _input.ProgressOf(FeedbackTarget.GrillSlot, i), ColorOf(view));
                if (!_drawn[i] || !view.Equals(_shown[i]))
                {
                    bool first = !_drawn[i];
                    GrillSlotView previous = first ? default(GrillSlotView) : _shown[i];
                    Draw(i, view, previous, first);
                    if (!first)
                    {
                        NoteTransition(i, view, previous, now);
                    }

                    _shown[i] = view;
                    _drawn[i] = true;
                }

                UpdateMotion(i, view, now, step);
            }
        }

        /// <summary>Q-05d: падіння шматка при кладенні (QB-SHIFT-22) і «вистриб» при готовності (QB-SHIFT-19).</summary>
        private void NoteTransition(int index, GrillSlotView view, GrillSlotView previous, float now)
        {
            if (view.Display == GrillSlotDisplay.Cooking && previous.Display != GrillSlotDisplay.Cooking)
            {
                _dropAt[index] = now;
            }

            if (view.Display == GrillSlotDisplay.Done && previous.Display != GrillSlotDisplay.Done)
            {
                _doneAt[index] = now;
            }
        }

        /// <summary>
        /// Рух значка м'яса й пара/дим за станом слота (G-09). Пише у стиль лише transform і opacity;
        /// ігровий час, тож пауза все зупиняє.
        /// </summary>
        private void UpdateMotion(int index, GrillSlotView view, float now, float step)
        {
            float scale = 1f;
            float tx = 0f;
            float ty = 0f;
            float rate = 0f;
            bool dark = false;
            switch (view.Display)
            {
                case GrillSlotDisplay.Cooking:
                    ty = UiMotion.Drop(now - _dropAt[index]);
                    rate = SteamPerSec;
                    break;
                case GrillSlotDisplay.Done:
                    if (view.BurnWarning)
                    {
                        scale = UiMotion.WarnPulse(now);
                        tx = UiMotion.WarnShake(now);
                        rate = SteamPerSec;
                        dark = true;
                    }
                    else
                    {
                        float since = now - _doneAt[index];
                        scale = since < UiMotion.DonePopSec ? UiMotion.DonePop(since) : UiMotion.DonePulse(now);
                    }

                    break;
                case GrillSlotDisplay.Burnt:
                    rate = SmokePerSec;
                    break;
                default:
                    break;
            }

            float opacity = view.Display == GrillSlotDisplay.Empty ? EmptyHintOpacity : 1f;
            _iconMotion[index].Set(scale, scale, tx, ty, opacity);
            if (_fx == null || rate <= 0f)
            {
                _emitted[index] = 0f;
                return;
            }

            _emitted[index] += rate * step;
            while (_emitted[index] >= 1f)
            {
                _emitted[index] -= 1f;
                if (view.Display == GrillSlotDisplay.Burnt)
                {
                    _fx.Smoke(_buttons[index]);
                }
                else
                {
                    _fx.Steam(_buttons[index], dark);
                }
            }
        }

        private void AddSlot(int index)
        {
            var button = new Button(SfxHub.WithTapSound(() => TapSlot(index))) { name = "grill-slot-" + index };
            button.AddToClassList(UiFactory.PressClass);
            button.style.width = SlotSize;
            button.style.height = SlotSize;
            button.style.marginLeft = SlotGap / 2f;
            button.style.marginRight = SlotGap / 2f;
            button.style.marginTop = SlotGap / 2f;
            button.style.marginBottom = SlotGap / 2f;
            button.style.fontSize = SlotFontSize;
            button.style.color = Color.white;
            UiDecor.AddButtonDepth(button, EmptyColor); // Q-05b: QB-SHIFT-24 / QB-ALL-06, глибина слота.
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

            // Значок стану м'яса (S1-12, Q-05b): велика іконка по центру слота замість напису стану.
            VisualElement icon = ArtLibrary.AddCenterIcon(button, IconSize, "grill-slot-icon-" + index);
            _icons[index] = icon;
            _iconMotion[index] = new MotionTarget(icon);
            _decors[index] = new TapDecor(button, "grill-slot-" + index);

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
                string full = _texts[warning ? WarningTextIndex : (int)view.Display];
                _slotTexts[index] = full;
                button.tooltip = full;
                Color background = ColorOf(view);
                button.style.backgroundColor = background;
                button.style.color = UiColors.GrillText(background);

                // Порожній слот підказує значком м'яса (напівпрозорий): «сюди кладуть м'ясо».
                bool empty = view.Display == GrillSlotDisplay.Empty;
                string iconName = empty ? ArtLibrary.IngredientIcon("meat") : IconOf(view);
                bool shown = ArtLibrary.ShowIcon(_icons[index], iconName);
                // Прозорість значка веде UpdateMotion (MotionTarget), щоб Draw не розходився з рухом.
                button.text = shown ? string.Empty : full;
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

        private static string IconOf(GrillSlotView view)
        {
            switch (view.Display)
            {
                case GrillSlotDisplay.Raw:
                    return ArtLibrary.MeatRaw;
                case GrillSlotDisplay.Cooking:
                    return ArtLibrary.MeatCooking;
                case GrillSlotDisplay.Done:
                    return view.BurnWarning ? ArtLibrary.MeatWarning : ArtLibrary.MeatDone;
                case GrillSlotDisplay.Burnt:
                    return ArtLibrary.MeatBurnt;
                default:
                    return null;
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
