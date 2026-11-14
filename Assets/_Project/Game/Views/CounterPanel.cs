using System;
using KebabStreet.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Прилавок на екрані зміни (S1-09b): картка на кожен слот конфіга рівня (ім'я клієнта, сердечка
    /// й смужка терпіння, значки замовлення, подія — оплата/чайові, скарга, відхід) і кількість тих,
    /// хто чекає в черзі. Лише показує <see cref="CustomerSlotView"/> і <see cref="CounterFeed"/>, тап
    /// по клієнту шле «видати страву цьому клієнту» в <see cref="ShiftDriver.Tap"/>; правил гри тут
    /// немає. Елементи оновлюються, лише коли вигляд змінився. Плейсхолдер без графіки (S1-12):
    /// значки — плашки з назвою інгредієнта, «не класти» — перекреслена плашка.
    /// </summary>
    public sealed class CounterPanel
    {
        /// <summary>Ім'я кореня прилавка в дереві UI.</summary>
        public const string PanelName = "counter-panel";

        /// <summary>Найбільше карток в одному рядку (12 слотів — 3 рядки по 4; картка ≈ 210 px логічної ширини).</summary>
        public const int MaxCardsPerRow = 4;

        // Референс 1080x1920 (ART_STYLE_GUIDE 4.1): зона дотику ≥ 144 px, сердечка по 48 px, текст ≥ 28 px.
        private const float CardMinHeight = 240f;
        private const float CardGap = 16f;
        private const float CardBorder = 6f;
        private const float NameFontSize = 34f;
        private const float ChipFontSize = 28f;
        private const float FeedbackFontSize = 34f;
        private const float QueueFontSize = 34f;
        private const float HeartSize = 48f;
        private const float PortraitSize = 96f;
        private const float HeartGap = 8f;
        private const float HeartBorder = 4f;
        private const float BarHeight = 12f;
        private const float ChipPadding = 8f;
        private const float ChipGap = 6f;
        private const float StrikeHeight = 6f;
        private const float HalfPercent = 50f;
        private const float FullPercent = 100f;

        private static readonly Color CardColor = UiColors.CustomerCard;
        private static readonly Color FreeCardColor = UiColors.CustomerFreeCard;
        private static readonly Color IdleBorder = new Color(0.23f, 0.14f, 0.08f);
        private static readonly Color LowBorder = new Color(0.84f, 0.16f, 0.16f);
        private static readonly Color QueuedBorder = Color.white;
        private static readonly Color HeartColor = new Color(0.84f, 0.16f, 0.16f);
        private static readonly Color HeartFrame = new Color(1f, 0.95f, 0.84f);
        private static readonly Color BarColor = new Color(1f, 0.95f, 0.84f);
        private static readonly Color StepChip = UiColors.ChipStep;
        private static readonly Color RequestedChip = UiColors.ChipRequested;
        private static readonly Color ExcludedChip = UiColors.ChipExcluded;
        private static readonly Color CoinColor = UiColors.Coin;
        private static readonly Color WarningColor = UiColors.Warning;

        private readonly ShiftDriver _driver;
        private readonly ShiftInput _input;
        private readonly CounterFeed _feed;
        private readonly int _hearts;
        private readonly string _freeText;
        private readonly string _queueFormat;
        private readonly string _leftText;
        private readonly string _wrongText;
        private readonly string _paidFormat;
        private readonly string _paidTipFormat;
        private readonly string[] _typeNames;
        private readonly string[] _ingredientNames;
        private readonly string[] _portraitNames;
        private readonly OrderIcon[][] _orders;
        private readonly Label _queue;
        private readonly Card[] _cards;
        private int _shownQueue = -1;

        public CounterPanel(VisualElement parent, ShiftInput input, ILocalizer text)
            : this(parent, input, text, CounterView.DefaultHearts)
        {
        }

        public CounterPanel(VisualElement parent, ShiftInput input, ILocalizer text, int hearts)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (hearts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hearts));
            }

            _input = input ?? throw new ArgumentNullException(nameof(input));
            ShiftDriver driver = input.Driver;
            _driver = driver;
            _hearts = hearts;
            _feed = new CounterFeed(driver.Simulation);
            _freeText = text.Get(TextKeys.CounterSlotFree);
            _queueFormat = text.Get(TextKeys.CounterQueue);
            _leftText = text.Get(TextKeys.CounterLeft);
            _wrongText = text.Get(TextKeys.CounterWrongDish);
            _paidFormat = text.Get(TextKeys.CounterPaid);
            _paidTipFormat = text.Get(TextKeys.CounterPaidTip);

            Counter counter = driver.Simulation.Counter;
            Board board = driver.Simulation.Board;
            _typeNames = new string[counter == null ? 0 : counter.Config.TypeCount];
            for (int i = 0; i < _typeNames.Length; i++)
            {
                _typeNames[i] = text.Get(counter.Config.GetCustomerType(i).NameKey);
            }

            _portraitNames = new string[_typeNames.Length];
            for (int i = 0; i < _portraitNames.Length; i++)
            {
                _portraitNames[i] = ArtLibrary.PortraitName(counter.Config.GetCustomerType(i).NameKey);
            }

            _ingredientNames = new string[board == null ? 0 : board.Config.IngredientCount];
            for (int i = 0; i < _ingredientNames.Length; i++)
            {
                _ingredientNames[i] = text.Get(board.Config.GetIngredient(i).NameKey);
            }

            _orders = new OrderIcon[counter == null ? 0 : counter.CustomerCount][];

            Root = new VisualElement { name = PanelName };
            Root.style.flexGrow = 1;
            Root.style.flexDirection = FlexDirection.Column;
            Root.style.alignItems = Align.Stretch;
            parent.Add(Root);

            _queue = new Label(string.Empty) { name = "counter-queue" };
            _queue.style.fontSize = QueueFontSize;
            _queue.style.color = Color.white;
            _queue.style.whiteSpace = WhiteSpace.Normal;
            _queue.style.unityTextAlign = TextAnchor.MiddleCenter;
            _queue.style.display = DisplayStyle.None;
            Root.Add(_queue);

            // BUG-040: ширина картки — частка ширини зони (рядок без переносу, картки ділять його порівну),
            // а не стала: 3 картки вміщаються в один рядок на 1080x2340, 12 слотів — 3 рядки по 4.
            int slots = counter == null ? 0 : counter.Slots.Count;
            int perRow = CardsPerRow(slots);
            _cards = new Card[slots];
            VisualElement row = null;
            int rows = 0;
            for (int i = 0; i < slots; i++)
            {
                if (i % perRow == 0)
                {
                    row = new VisualElement { name = "counter-slots-" + (i / perRow) };
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.flexWrap = Wrap.NoWrap;
                    row.style.alignItems = Align.FlexStart;
                    Root.Add(row);
                    rows++;
                }

                _cards[i] = new Card(row, i, hearts, this);
            }

            // Неповний останній рядок: порожні місця тримають ширину карток такою ж, як у повних рядках.
            for (int i = slots; row != null && i % perRow != 0; i++)
            {
                var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
                Grow(spacer);
                row.Add(spacer);
            }

            RowCount = rows;

            Refresh();
        }

        /// <summary>Корінь прилавка.</summary>
        public VisualElement Root { get; }

        /// <summary>Скільки слотів показано (counterSlots рівня).</summary>
        public int SlotCount => _cards.Length;

        /// <summary>Скільки рядків карток.</summary>
        public int RowCount { get; }

        /// <summary>Скільки карток у рядку: усі в одному, якщо їх не більше за <see cref="MaxCardsPerRow"/>.</summary>
        public static int CardsPerRow(int slots)
        {
            return slots < 1 ? 1 : Math.Min(slots, MaxCardsPerRow);
        }

        /// <summary>Напис кількості клієнтів у черзі (порожньо — черги немає).</summary>
        public string QueueText => _queue.text;

        /// <summary>Чи показано напис черги.</summary>
        public bool QueueVisible => _shownQueue > 0;

        /// <summary>Вигляд слота, який зараз показано.</summary>
        public CustomerSlotView GetShownView(int slot)
        {
            return _cards[slot].Shown;
        }

        /// <summary>Подія слота, яку зараз показано.</summary>
        public CounterEvent GetShownEvent(int slot)
        {
            return _cards[slot].ShownEvent;
        }

        /// <summary>Ім'я клієнта в слоті (або напис «вільно»).</summary>
        public string GetNameText(int slot)
        {
            return _cards[slot].Name.text;
        }

        /// <summary>Напис події в слоті (порожньо — події немає).</summary>
        public string GetEventText(int slot)
        {
            return _cards[slot].Feedback.text;
        }

        /// <summary>Скільки значків замовлення показано в слоті.</summary>
        public int GetOrderChipCount(int slot)
        {
            return _cards[slot].VisibleChips;
        }

        /// <summary>Напис значка замовлення.</summary>
        public string GetOrderChipText(int slot, int index)
        {
            return _cards[slot].Chips[index].text;
        }

        /// <summary>Чи перекреслено значок («не класти»).</summary>
        public bool IsOrderChipStruck(int slot, int index)
        {
            return _cards[slot].Strikes[index].style.display.value == DisplayStyle.Flex;
        }

        /// <summary>
        /// Тап по клієнту: «видати страву цьому клієнту» йде через <see cref="ShiftInput"/> у
        /// <see cref="ShiftDriver.Tap"/>. true — команду прийнято до черги.
        /// </summary>
        public bool TapSlot(int slot)
        {
            return slot >= 0 && slot < _cards.Length && _input.TapCustomer(slot);
        }

        /// <summary>Оновлює чергу, слоти й події, що змінились. Викликається щокадру; сміття не створює.</summary>
        public void Refresh()
        {
            ShiftSimulation simulation = _driver.Simulation;
            if (simulation.Counter == null)
            {
                return;
            }

            _feed.Update(simulation);
            int queue = CounterView.QueueLength(simulation);
            if (queue != _shownQueue)
            {
                _queue.text = queue > 0 ? string.Format(_queueFormat, queue) : string.Empty;
                _queue.style.display = queue > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _shownQueue = queue;
            }

            for (int i = 0; i < _cards.Length; i++)
            {
                _cards[i].Refresh(CounterView.FromSimulation(simulation, i, _hearts), _feed.GetEvent(i));
            }
        }

        private OrderIcon[] OrderOf(int customerId)
        {
            if (_orders[customerId] == null)
            {
                Counter counter = _driver.Simulation.Counter;
                _orders[customerId] = CounterView.OrderOf(_driver.Simulation.Board.Config, counter.GetCustomer(customerId));
            }

            return _orders[customerId];
        }

        private string EventText(CounterEvent counterEvent)
        {
            switch (counterEvent.Kind)
            {
                case CounterEventKind.Served:
                    return counterEvent.TipCoins > 0L
                        ? string.Format(_paidTipFormat, counterEvent.PaidCoins, counterEvent.TipCoins)
                        : string.Format(_paidFormat, counterEvent.PaidCoins);
                case CounterEventKind.Left:
                    return _leftText;
                case CounterEventKind.WrongDish:
                    return _wrongText;
                default:
                    return string.Empty;
            }
        }

        /// <summary>Картка (або порожнє місце) ділить ширину рядка порівну з іншими.</summary>
        private static void Grow(VisualElement element)
        {
            element.style.flexGrow = 1;
            element.style.flexShrink = 1;
            element.style.flexBasis = 0;
            element.style.minWidth = 0;
            element.style.marginLeft = CardGap / 2f;
            element.style.marginRight = CardGap / 2f;
            element.style.marginTop = CardGap / 2f;
            element.style.marginBottom = CardGap / 2f;
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

        private static Color ColorOf(OrderIconKind kind)
        {
            switch (kind)
            {
                case OrderIconKind.Requested:
                    return RequestedChip;
                case OrderIconKind.Excluded:
                    return ExcludedChip;
                default:
                    return StepChip;
            }
        }

        private static Color ColorOf(CounterEventKind kind)
        {
            switch (kind)
            {
                case CounterEventKind.Served:
                    return CoinColor;
                case CounterEventKind.Left:
                    return UiColors.LeftText;
                default:
                    return WarningColor;
            }
        }

        /// <summary>Елементи однієї картки слота.</summary>
        private sealed class Card
        {
            private readonly CounterPanel _owner;
            private readonly Button _button;
            private readonly VisualElement _heartsRow;
            private readonly VisualElement[] _heartFills;
            private readonly VisualElement _barFrame;
            private readonly VisualElement _bar;
            private readonly VisualElement _order;
            private readonly VisualElement _portrait;
            private readonly bool _showPortrait;
            private bool _drawn;

            public Card(VisualElement parent, int index, int hearts, CounterPanel owner)
            {
                _owner = owner;
                _button = new Button(() => owner.TapSlot(index)) { name = "counter-slot-" + index };
                Grow(_button);
                _button.style.minHeight = CardMinHeight;
                _button.style.flexDirection = FlexDirection.Column;
                _button.style.alignItems = Align.Center;
                _button.style.justifyContent = Justify.FlexStart;
                _button.style.paddingTop = CardGap / 2f;
                _button.style.paddingBottom = CardGap / 2f;
                SetBorder(_button, CardBorder, IdleBorder);
                parent.Add(_button);

                // Портрет клієнта (S1-12) лише коли слотів небагато (один рядок): інакше картки стають задовгими.
                _portrait = new VisualElement { name = "counter-portrait-" + index, pickingMode = PickingMode.Ignore };
                _portrait.style.width = PortraitSize;
                _portrait.style.height = PortraitSize;
                _portrait.style.display = DisplayStyle.None;
                _showPortrait = owner._cards.Length <= MaxCardsPerRow;
                _button.Add(_portrait);

                Name = AddLabel(_button, "counter-name-" + index, NameFontSize, Color.white);

                _heartsRow = new VisualElement { name = "counter-hearts-" + index, pickingMode = PickingMode.Ignore };
                _heartsRow.style.flexDirection = FlexDirection.Row;
                _heartsRow.style.marginTop = HeartGap;
                _button.Add(_heartsRow);
                _heartFills = new VisualElement[hearts];
                for (int h = 0; h < hearts; h++)
                {
                    var frame = new VisualElement { pickingMode = PickingMode.Ignore };
                    frame.style.width = HeartSize;
                    frame.style.height = HeartSize;
                    frame.style.marginLeft = HeartGap / 2f;
                    frame.style.marginRight = HeartGap / 2f;
                    SetBorder(frame, HeartBorder, HeartFrame);
                    var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                    fill.style.position = Position.Absolute;
                    fill.style.left = 0;
                    fill.style.top = 0;
                    fill.style.bottom = 0;
                    fill.style.width = Length.Percent(0);
                    fill.style.backgroundColor = HeartColor;
                    frame.Add(fill);
                    _heartsRow.Add(frame);
                    _heartFills[h] = fill;
                }

                _barFrame = new VisualElement { pickingMode = PickingMode.Ignore };
                _barFrame.style.height = BarHeight;
                _barFrame.style.alignSelf = Align.Stretch;
                _barFrame.style.marginTop = HeartGap;
                _barFrame.style.marginLeft = CardGap;
                _barFrame.style.marginRight = CardGap;
                _button.Add(_barFrame);
                _bar = new VisualElement { name = "counter-bar-" + index, pickingMode = PickingMode.Ignore };
                _bar.style.height = BarHeight;
                _bar.style.width = Length.Percent(0);
                _bar.style.backgroundColor = BarColor;
                _barFrame.Add(_bar);

                _order = new VisualElement { name = "counter-order-" + index, pickingMode = PickingMode.Ignore };
                _order.style.flexDirection = FlexDirection.Row;
                _order.style.flexWrap = Wrap.Wrap;
                _order.style.justifyContent = Justify.Center;
                _order.style.marginTop = HeartGap;
                _button.Add(_order);
                Chips = new Label[0];
                Strikes = new VisualElement[0];

                Feedback = AddLabel(_button, "counter-event-" + index, FeedbackFontSize, CoinColor);
            }

            public Label Name { get; }

            public Label Feedback { get; }

            public Label[] Chips { get; private set; }

            public VisualElement[] Strikes { get; private set; }

            public int VisibleChips { get; private set; }

            public CustomerSlotView Shown { get; private set; }

            public CounterEvent ShownEvent { get; private set; }

            public void Refresh(CustomerSlotView view, CounterEvent counterEvent)
            {
                bool first = !_drawn;
                _drawn = true;
                CustomerSlotView previous = Shown;
                if (first || view.CustomerId != previous.CustomerId)
                {
                    DrawCustomer(view);
                }

                if (first || !view.Patience.Equals(previous.Patience) || view.CustomerId != previous.CustomerId)
                {
                    DrawPatience(view);
                }

                if (first || view.ServeQueued != previous.ServeQueued || view.Patience.Low != previous.Patience.Low
                    || view.Occupied != previous.Occupied)
                {
                    Color border = view.ServeQueued ? QueuedBorder : (view.Occupied && view.Patience.Low ? LowBorder : IdleBorder);
                    SetBorder(_button, CardBorder, border);
                }

                if (first || !counterEvent.Equals(ShownEvent))
                {
                    Feedback.text = _owner.EventText(counterEvent);
                    Feedback.style.color = ColorOf(counterEvent.Kind);
                    ShownEvent = counterEvent;
                }

                Shown = view;
            }

            private void DrawCustomer(CustomerSlotView view)
            {
                bool occupied = view.Occupied;
                Name.text = occupied ? _owner._typeNames[view.TypeIndex] : _owner._freeText;
                _button.style.backgroundColor = occupied ? CardColor : FreeCardColor;
                DisplayStyle shown = occupied ? DisplayStyle.Flex : DisplayStyle.None;
                _heartsRow.style.display = shown;
                _portrait.style.display = occupied && _showPortrait && ArtLibrary.Apply(_portrait, _owner._portraitNames[view.TypeIndex])
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _barFrame.style.display = shown;
                _order.style.display = shown;

                OrderIcon[] icons = occupied ? _owner.OrderOf(view.CustomerId) : null;
                int count = icons == null ? 0 : icons.Length;
                EnsureChips(count);
                for (int i = 0; i < Chips.Length; i++)
                {
                    bool visible = i < count;
                    Chips[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!visible)
                    {
                        continue;
                    }

                    OrderIcon icon = icons[i];
                    Chips[i].text = _owner._ingredientNames[icon.Ingredient];
                    Chips[i].style.backgroundColor = ColorOf(icon.Kind);
                    Strikes[i].style.display = icon.Kind == OrderIconKind.Excluded ? DisplayStyle.Flex : DisplayStyle.None;
                }

                VisibleChips = count;
            }

            private void DrawPatience(CustomerSlotView view)
            {
                PatienceView patience = view.Patience;
                for (int h = 0; h < _heartFills.Length; h++)
                {
                    HeartFill fill = view.Occupied ? patience.GetHeart(h) : HeartFill.Empty;
                    float percent = fill == HeartFill.Full ? FullPercent : (fill == HeartFill.Half ? HalfPercent : 0f);
                    _heartFills[h].style.width = Length.Percent(percent);
                }

                _bar.style.width = Length.Percent(view.Occupied ? patience.Percent : 0);
                _bar.style.backgroundColor = patience.Low ? LowBorder : BarColor;
            }

            private void EnsureChips(int count)
            {
                if (count <= Chips.Length)
                {
                    return;
                }

                var chips = new Label[count];
                var strikes = new VisualElement[count];
                Array.Copy(Chips, chips, Chips.Length);
                Array.Copy(Strikes, strikes, Strikes.Length);
                for (int i = Chips.Length; i < count; i++)
                {
                    var chip = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
                    chip.style.fontSize = ChipFontSize;
                    chip.style.color = Color.white;
                    chip.style.paddingLeft = ChipPadding;
                    chip.style.paddingRight = ChipPadding;
                    chip.style.marginLeft = ChipGap / 2f;
                    chip.style.marginRight = ChipGap / 2f;
                    chip.style.marginTop = ChipGap / 2f;
                    chip.style.marginBottom = ChipGap / 2f;
                    chip.style.unityTextAlign = TextAnchor.MiddleCenter;

                    var strike = new VisualElement { pickingMode = PickingMode.Ignore };
                    strike.style.position = Position.Absolute;
                    strike.style.left = 0;
                    strike.style.right = 0;
                    strike.style.top = Length.Percent(HalfPercent);
                    strike.style.height = StrikeHeight;
                    strike.style.backgroundColor = LowBorder;
                    strike.style.display = DisplayStyle.None;
                    chip.Add(strike);

                    _order.Add(chip);
                    chips[i] = chip;
                    strikes[i] = strike;
                }

                Chips = chips;
                Strikes = strikes;
            }

            private static Label AddLabel(VisualElement parent, string name, float size, Color color)
            {
                var label = new Label(string.Empty) { name = name, pickingMode = PickingMode.Ignore };
                label.style.fontSize = size;
                label.style.color = color;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                parent.Add(label);
                return label;
            }
        }
    }
}
