using System;
using KebabStreet.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Станція збирання на екрані зміни (S1-09c): кнопки станцій рівня (піта, соуси, салатний бар),
    /// дошка з інгредієнтами в порядку, у якому їх поклали (тап знімає), «Очистити дошку», черга дій
    /// (тап скасовує), напис про страву (готова / не за рецептом), підказка і комбо. Видача — тап по
    /// клієнту на прилавку. Лише показує стан Core через <see cref="AssemblyView"/> і шле тапи через
    /// <see cref="ShiftInput"/> у <see cref="ShiftDriver.Tap"/>; правил гри тут немає. Елементи
    /// створюються один раз і оновлюються, лише коли показ змінився. Плейсхолдер без графіки (S1-12).
    /// </summary>
    public sealed class AssemblyPanel
    {
        public const string PanelName = "assembly-panel";

        /// <summary>Скільки кнопок у рядку (станції, дошка): ≈ 290 px логічної ширини на 1080x2340.</summary>
        public const int ButtonsPerRow = 3;
        private const float StationIconSize = 128f;
        private const float ItemIconSize = 96f;
        private const float QueueIconSize = 72f;
        private const float ClearGlyphSize = 64f;
        private const float ClearWidth = 144f;

        /// <summary>Найбільше показаних дій черги (rules.actionQueueMax може бути більшим).</summary>
        public const int MaxQueueShown = 4;

        // Референс 1080x1920 (ART_STYLE_GUIDE 4.1): зона дотику ≥ 144 px (48 dp), текст ≥ 28 px.
        private const float StationHeight = 176f;
        private const float ItemHeight = 144f;
        private const float QueueHeight = 112f;
        private const float Gap = 12f;
        private const float FontSize = 34f;
        private const float SmallFontSize = 28f;

        private static readonly Color StationColor = UiColors.StationButton;
        private static readonly Color ItemColor = UiColors.ItemButton;
        private static readonly Color ClearColor = UiColors.ClearButton;
        private static readonly Color QueueColor = UiColors.QueueButton;
        private static readonly Color ReadyColor = UiColors.ReadyText;
        private static readonly Color WarningColor = UiColors.Warning;
        private static readonly Color CoinColor = UiColors.Coin;

        private readonly ShiftInput _input;
        private readonly ILocalizer _text;
        private readonly int[] _stations;
        private readonly string[] _ingredientNames;
        private readonly string[] _ingredientIcons;
        private readonly string[] _stationTexts;
        private readonly string[] _itemTexts;
        private readonly string[] _queueTexts;
        private readonly VisualElement[] _stationIcons;
        private readonly VisualElement[] _itemIcons;
        private readonly VisualElement[] _queueIcons;
        private readonly Label _queueTitle;
        private readonly string[] _actionNames = new string[(int)ShiftActionType.ServeDish + 1];
        private readonly string _comboFormat;
        private readonly Button[] _stationButtons;
        private readonly Button[] _items;
        private readonly int[] _shownItems;
        private readonly Button[] _queue;
        private readonly ShiftAction[] _shownQueue;
        private readonly Button _clear;
        private readonly string _clearText;
        private readonly TapDecor[] _stationDecors;
        private readonly TapDecor[] _itemDecors;
        private readonly TapDecor _clearDecor;
        private readonly Label _dish;
        private readonly Label _hint;
        private readonly Label _combo;
        private DishCheck _shownCheck;
        private string _shownHint;
        private long _shownCombo;
        private bool _drawn;

        public AssemblyPanel(VisualElement parent, ShiftInput input, ILocalizer text)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            _input = input ?? throw new ArgumentNullException(nameof(input));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            ShiftSimulation simulation = input.Driver.Simulation;
            Board board = simulation.Board;
            _comboFormat = text.Get(TextKeys.AssemblyCombo);
            _stations = board == null ? new int[0] : AssemblyView.Stations(board);
            _ingredientNames = new string[board == null ? 0 : board.Config.IngredientCount];
            _ingredientIcons = new string[_ingredientNames.Length];
            for (int i = 0; i < _ingredientNames.Length; i++)
            {
                _ingredientNames[i] = text.Get(board.Config.GetIngredient(i).NameKey);
                _ingredientIcons[i] = ArtLibrary.IngredientIcon(board.Config.GetIngredient(i).Id);
            }

            for (int type = 0; type < _actionNames.Length; type++)
            {
                string key = AssemblyView.ActionTextKey((ShiftActionType)type);
                _actionNames[type] = key == null ? null : text.Get(key);
            }

            Root = new VisualElement { name = PanelName };
            Root.style.flexGrow = 1;
            Root.style.flexDirection = FlexDirection.Column;
            Root.style.paddingLeft = Gap / 2f;
            Root.style.paddingRight = Gap / 2f;
            parent.Add(Root);

            _stationButtons = Grid("assembly-station", _stations.Length, StationHeight, StationColor, TapStation);
            _stationTexts = new string[_stations.Length];
            _stationIcons = new VisualElement[_stations.Length];
            for (int i = 0; i < _stations.Length; i++)
            {
                // Q-05b (QB-SHIFT-24): станція - велика іконка інгредієнта з глибиною; слово лише як tooltip
                // (і як резерв, якщо спрайта немає).
                Button station = _stationButtons[i];
                _stationTexts[i] = _ingredientNames[_stations[i]];
                station.tooltip = _stationTexts[i];
                UiDecor.AddButtonDepth(station, StationColor);
                _stationIcons[i] = ArtLibrary.AddCenterIcon(station, StationIconSize, "assembly-station-icon-" + i);
                if (!ArtLibrary.ShowIcon(_stationIcons[i], _ingredientIcons[_stations[i]]))
                {
                    station.text = _stationTexts[i];
                }
            }

            _items = Grid("assembly-item", board == null ? 0 : board.Capacity, ItemHeight, ItemColor, TapBoardItem);
            _shownItems = new int[_items.Length];
            _itemTexts = new string[_items.Length];
            _itemIcons = new VisualElement[_items.Length];
            for (int i = 0; i < _items.Length; i++)
            {
                UiDecor.AddButtonDepth(_items[i], ItemColor);
                _itemIcons[i] = ArtLibrary.AddCenterIcon(_items[i], ItemIconSize, "assembly-item-icon-" + i);
                _itemTexts[i] = string.Empty;
            }

            // S1-21: відгук на тап - рамка підсвічування і смужка дії на кожній кнопці.
            _stationDecors = new TapDecor[_stationButtons.Length];
            for (int i = 0; i < _stationDecors.Length; i++)
            {
                _stationDecors[i] = new TapDecor(_stationButtons[i], "assembly-station-" + i);
            }

            _itemDecors = new TapDecor[_items.Length];
            for (int i = 0; i < _itemDecors.Length; i++)
            {
                _itemDecors[i] = new TapDecor(_items[i], "assembly-item-" + i);
            }

            VisualElement status = Row(Root, "assembly-status");
            _dish = AddLabel(status, "assembly-dish", FontSize, Color.white);
            Backdrop(_dish);
            _dish.style.flexGrow = 1;
            _clear = AddButton(status, "assembly-clear", string.Empty, ClearColor, () => TapClear());
            _clear.style.height = QueueHeight;

            // Q-05b (QB-SHIFT-28): «Очистити дошку» - значок смітника (форма з елементів, без ресурсу); слово - tooltip.
            _clearText = text.Get(TextKeys.AssemblyClear);
            _clear.tooltip = _clearText;
            UiDecor.AddButtonDepth(_clear, ClearColor);
            UiDecor.AddTrashGlyph(_clear, ClearGlyphSize, Color.white, ClearColor);

            // S1-23: кнопка не стискується до вузької колонки: ширина за текстом, напис в один рядок.
            _clear.style.flexShrink = 0;
            _clear.style.flexGrow = 0;
            _clear.style.whiteSpace = WhiteSpace.NoWrap;
            _clear.style.width = ClearWidth;
            _clear.style.minWidth = ClearWidth;
            _clear.style.fontSize = SmallFontSize;
            _dish.style.flexBasis = 0;
            _dish.style.flexShrink = 1;
            _dish.style.minWidth = 0;
            _clearDecor = new TapDecor(_clear, "assembly-clear");
            _clear.style.display = board != null && board.IsBinAvailable ? DisplayStyle.Flex : DisplayStyle.None;

            VisualElement queueRow = Row(Root, "assembly-queue");

            // Q-05b (QB-SHIFT-29): підпис «Далі:» не показуємо - чергу читають за значками; слово лишається в tooltip.
            _queueTitle = AddLabel(queueRow, "assembly-queue-title", SmallFontSize, Color.white);
            _queueTitle.text = text.Get(TextKeys.AssemblyQueue);
            _queueTitle.style.display = DisplayStyle.None;
            _queue = new Button[Math.Min(simulation.Config.ActionQueueMax, MaxQueueShown)];
            _shownQueue = new ShiftAction[_queue.Length];
            _queueTexts = new string[_queue.Length];
            _queueIcons = new VisualElement[_queue.Length];
            for (int i = 0; i < _queue.Length; i++)
            {
                int index = i;
                _queue[i] = AddButton(queueRow, "assembly-queued-" + i, string.Empty, QueueColor, () => TapQueued(index));
                _queue[i].style.height = QueueHeight;
                _queue[i].style.fontSize = SmallFontSize;
                _queue[i].style.flexGrow = 1;
                _queue[i].style.flexBasis = 0;
                _queue[i].tooltip = _queueTitle.text;
                _queueTexts[i] = string.Empty;
                _queueIcons[i] = ArtLibrary.AddCenterIcon(_queue[i], QueueIconSize, "assembly-queued-icon-" + i);
            }

            VisualElement notes = Row(Root, "assembly-notes");
            _hint = AddLabel(notes, "assembly-hint", FontSize, WarningColor);
            Backdrop(_hint);
            _hint.style.flexGrow = 1;
            _combo = AddLabel(notes, "assembly-combo", FontSize, CoinColor);
            Backdrop(_combo);

            Refresh();
        }

        public VisualElement Root { get; }

        /// <summary>Скільки кнопок станцій (інгредієнтів, доступних на рівні).</summary>
        public int StationCount => _stations.Length;

        /// <summary>Скільки місць на дошці показано (місткість дошки).</summary>
        public int ItemSlotCount => _items.Length;

        /// <summary>Скільки місць черги дій показано.</summary>
        public int QueueSlotCount => _queue.Length;

        public bool ClearShown => _clear.style.display.value == DisplayStyle.Flex;

        public string DishText => _dish.text;

        public string HintText => _hint.text;

        public string ComboText => _combo.text;

        /// <summary>Відгук на тап кнопки станції (S1-21).</summary>
        public TapDecor GetStationDecor(int index)
        {
            return _stationDecors[index];
        }

        /// <summary>Відгук на тап «Очистити дошку» (смітник, S1-21).</summary>
        public TapDecor ClearDecor => _clearDecor;

        /// <summary>Номер інгредієнта кнопки станції.</summary>
        public int GetStationIngredient(int index)
        {
            return _stations[index];
        }

        /// <summary>Локалізоване ім'я інгредієнта станції (a11y); на екрані станцію показує значок (Q-05b).</summary>
        public string GetStationText(int index)
        {
            return _stationTexts[index];
        }

        /// <summary>Чи станція показує значок (а не напис).</summary>
        public bool IsStationIconShown(int index)
        {
            return _stationIcons[index].style.display.value == DisplayStyle.Flex;
        }

        /// <summary>Локалізований напис кнопки «Очистити дошку» (a11y); на екрані - смітник.</summary>
        public string ClearText => _clearText;

        /// <summary>Напис, що справді видно на кнопці станції (порожньо, коли показано значок).</summary>
        public string GetVisibleStationText(int index)
        {
            return _stationButtons[index].text;
        }

        /// <summary>Чи показано інгредієнт на цьому місці дошки.</summary>
        public bool IsItemShown(int position)
        {
            return _shownItems[position] >= 0;
        }

        public string GetItemText(int position)
        {
            return _itemTexts[position];
        }

        /// <summary>Чи місце дошки показує значок інгредієнта.</summary>
        public bool IsItemIconShown(int position)
        {
            return _itemIcons[position].style.display.value == DisplayStyle.Flex;
        }

        /// <summary>Напис, що справді видно на місці дошки.</summary>
        public string GetVisibleItemText(int position)
        {
            return _items[position].text;
        }

        /// <summary>Чи показано дію черги на цьому місці.</summary>
        public bool IsQueuedShown(int index)
        {
            return _shownQueue[index].Type != ShiftActionType.None;
        }

        public string GetQueuedText(int index)
        {
            return _queueTexts[index];
        }

        /// <summary>Чи дія черги показує значок.</summary>
        public bool IsQueuedIconShown(int index)
        {
            return _queueIcons[index].style.display.value == DisplayStyle.Flex;
        }

        /// <summary>Ім'я спрайта для дії черги (null - значка немає, показується слово).</summary>
        public static string QueueIconName(ShiftAction action, string[] ingredientIcons)
        {
            switch (action.Type)
            {
                case ShiftActionType.AddIngredient:
                    return ingredientIcons != null && action.Slot >= 0 && action.Slot < ingredientIcons.Length
                        ? ingredientIcons[action.Slot]
                        : null;
                case ShiftActionType.PlaceMeat:
                    return ArtLibrary.IngredientIcon("meat");
                case ShiftActionType.TakeMeat:
                    return ArtLibrary.MeatDone;
                case ShiftActionType.DiscardBurntMeat:
                    return ArtLibrary.MeatBurnt;
                case ShiftActionType.ServeDish:
                    return ArtLibrary.Coin;
                default:
                    return null;
            }
        }

        /// <summary>Тап по кнопці станції: інгредієнт на дошку.</summary>
        public bool TapStation(int index)
        {
            return index >= 0 && index < _stations.Length && _input.TapStation(_stations[index]);
        }

        /// <summary>Тап по інгредієнту на дошці: зняти його.</summary>
        public bool TapBoardItem(int position)
        {
            return _input.TapBoardItem(position);
        }

        /// <summary>Тап «Очистити дошку».</summary>
        public bool TapClear()
        {
            return ClearShown && _input.TapClearBoard();
        }

        /// <summary>Тап по дії в черзі: скасувати.</summary>
        public bool TapQueued(int index)
        {
            return index >= 0 && index < _queue.Length && _input.TapQueuedAction(index);
        }

        /// <summary>Оновлює дошку, чергу, написи, що змінились. Викликається щокадру; сміття не створює.</summary>
        public void Refresh()
        {
            ShiftSimulation simulation = _input.Driver.Simulation;
            Board board = simulation.Board;
            for (int s = 0; s < _stationDecors.Length; s++)
            {
                _stationDecors[s].Update(
                    _input.IsLit(FeedbackTarget.Station, _stations[s]),
                    _input.ProgressOf(FeedbackTarget.Station, _stations[s]),
                    StationColor);
            }

            _clearDecor.Update(
                _input.IsLit(FeedbackTarget.Clear, 0), _input.ProgressOf(FeedbackTarget.Clear, 0), ClearColor);
            for (int p = 0; p < _items.Length; p++)
            {
                _itemDecors[p].Update(
                    _input.IsLit(FeedbackTarget.BoardItem, p), _input.ProgressOf(FeedbackTarget.BoardItem, p), ItemColor);
            }

            for (int p = 0; p < _items.Length; p++)
            {
                int item = p < board.ItemCount ? board.GetItem(p) : -1;
                if (!_drawn || item != _shownItems[p])
                {
                    _itemTexts[p] = item >= 0 ? _ingredientNames[item] : string.Empty;
                    _items[p].tooltip = _itemTexts[p];
                    bool itemIcon = ArtLibrary.ShowIcon(_itemIcons[p], item >= 0 ? _ingredientIcons[item] : null);
                    _items[p].text = itemIcon ? string.Empty : _itemTexts[p];
                    _items[p].style.visibility = item >= 0 ? Visibility.Visible : Visibility.Hidden;
                    _shownItems[p] = item;
                }
            }

            for (int i = 0; i < _queue.Length; i++)
            {
                ShiftAction action = i < simulation.QueuedActionCount ? simulation.GetQueuedAction(i) : default(ShiftAction);
                if (!_drawn || !action.Equals(_shownQueue[i]))
                {
                    _queueTexts[i] = NameOf(action);
                    bool queueIcon = ArtLibrary.ShowIcon(
                        _queueIcons[i], action.Type != ShiftActionType.None ? QueueIconName(action, _ingredientIcons) : null);
                    _queue[i].text = queueIcon ? string.Empty : _queueTexts[i];
                    _queue[i].style.visibility = action.Type != ShiftActionType.None ? Visibility.Visible : Visibility.Hidden;
                    _shownQueue[i] = action;
                }
            }

            DishCheck check = board == null ? DishCheck.Empty : board.Check();
            if (!_drawn || check != _shownCheck)
            {
                string key = AssemblyView.DishTextKey(check);
                _dish.text = key == null ? string.Empty : _text.Get(key);
                _dish.style.color = check == DishCheck.Correct ? ReadyColor : (key == TextKeys.AssemblyDishWrong ? WarningColor : Color.white);
                _shownCheck = check;
            }

            string hint = _input.HintKey;
            if (!_drawn || hint != _shownHint)
            {
                _hint.text = hint == null ? string.Empty : _text.Get(hint);
                _shownHint = hint;
            }

            long combo = simulation.Counter == null ? 0L : AssemblyView.ComboShown(simulation.Counter.Combo);
            if (!_drawn || combo != _shownCombo)
            {
                _combo.text = combo > 0L ? string.Format(_comboFormat, combo) : string.Empty;
                _shownCombo = combo;
            }

            _drawn = true;
        }

        private string NameOf(ShiftAction action)
        {
            if (action.Type == ShiftActionType.AddIngredient)
            {
                return action.Slot >= 0 && action.Slot < _ingredientNames.Length ? _ingredientNames[action.Slot] : string.Empty;
            }

            int type = (int)action.Type;
            return type > 0 && type < _actionNames.Length && _actionNames[type] != null ? _actionNames[type] : string.Empty;
        }

        /// <summary>Кнопки рядами по <see cref="ButtonsPerRow"/>; кнопки ділять ширину рядка порівну.</summary>
        private Button[] Grid(string name, int count, float height, Color color, Func<int, bool> tap)
        {
            var buttons = new Button[count];
            VisualElement row = null;
            for (int i = 0; i < count; i++)
            {
                if (i % ButtonsPerRow == 0)
                {
                    row = Row(Root, name + "-row-" + (i / ButtonsPerRow));
                }

                int index = i;
                buttons[i] = AddButton(row, name + "-" + i, string.Empty, color, () => tap(index));
                buttons[i].style.height = height;
                buttons[i].style.flexGrow = 1;
                buttons[i].style.flexBasis = 0;
            }

            for (int i = count; row != null && i % ButtonsPerRow != 0; i++)
            {
                var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
                spacer.style.flexGrow = 1;
                spacer.style.flexBasis = 0;
                spacer.style.marginLeft = Gap / 2f;
                spacer.style.marginRight = Gap / 2f;
                row.Add(spacer);
            }

            return buttons;
        }

        private static VisualElement Row(VisualElement parent, string name)
        {
            var row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.NoWrap;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;
            row.style.marginTop = Gap / 2f;
            parent.Add(row);
            return row;
        }

        private static Button AddButton(VisualElement parent, string name, string text, Color color, Action onTap)
        {
            var button = new Button(SfxHub.WithTapSound(onTap)) { name = name, text = text };
            button.AddToClassList(UiFactory.PressClass);
            button.style.minWidth = 0;
            button.style.marginLeft = Gap / 2f;
            button.style.marginRight = Gap / 2f;
            button.style.fontSize = FontSize;
            button.style.color = Color.white;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.backgroundColor = color;
            parent.Add(button);
            return button;
        }

        // S1-17: сірі й бурштинові написи на дерев'яному фоні зникали - темна підкладка під текстом.
        private static void Backdrop(Label label)
        {
            label.style.backgroundColor = UiColors.LabelBackdrop;
            label.style.paddingLeft = Gap;
            label.style.paddingRight = Gap;
            label.style.paddingTop = Gap / 3f;
            label.style.paddingBottom = Gap / 3f;
            label.style.borderTopLeftRadius = Gap;
            label.style.borderTopRightRadius = Gap;
            label.style.borderBottomLeftRadius = Gap;
            label.style.borderBottomRightRadius = Gap;
        }

        private static Label AddLabel(VisualElement parent, string name, float size, Color color)
        {
            var label = new Label(string.Empty) { name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.flexShrink = 1;
            parent.Add(label);
            return label;
        }
    }
}
