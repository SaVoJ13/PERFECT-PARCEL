using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Робоча дошка (GAME_DESIGN.md, розд. 5): інгредієнти в порядку, в якому їх поклали,
    /// перевірка за рецептами і видача готової страви. Випадковості не використовує.
    /// Зовні доступна лише для читання: стан змінюють тільки виконані дії з черги
    /// <see cref="ShiftSimulation"/>. На дошку можна покласти й хибний інгредієнт —
    /// тоді його знімають або очищають дошку у смітник.
    /// </summary>
    public sealed class Board
    {
        private const int NoIngredient = -1;

        private readonly int[] _items;
        private readonly int[][] _recipeItems;
        private readonly int[] _recipeSteps;
        private readonly bool[] _recipeAvailable;
        private readonly bool[] _ingredientAddable;
        private readonly long[] _ingredientActionTicks;
        private int _count;

        internal Board(BoardConfig config, int ticksPerSecond)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _items = new int[config.Capacity];
            MeatIngredient = NoIngredient;

            _ingredientAddable = new bool[config.IngredientCount];
            _ingredientActionTicks = new long[config.IngredientCount];
            for (int i = 0; i < config.IngredientCount; i++)
            {
                StationKind kind = config.GetIngredient(i).Station;
                if (kind == StationKind.Grill)
                {
                    MeatIngredient = i;
                    continue;
                }

                _ingredientAddable[i] = config.IsStationAvailable(kind);
                _ingredientActionTicks[i] = config.FindStation(kind).ActionTicks(ticksPerSecond);
            }

            _recipeItems = new int[config.RecipeCount][];
            _recipeSteps = new int[config.RecipeCount];
            _recipeAvailable = new bool[config.RecipeCount];
            for (int r = 0; r < config.RecipeCount; r++)
            {
                Recipe recipe = config.GetRecipe(r);
                _recipeSteps[r] = recipe.StepCount;
                _recipeAvailable[r] = config.IsRecipeAvailable(r);
                _recipeItems[r] = new int[recipe.Length];
                for (int k = 0; k < recipe.Length; k++)
                {
                    _recipeItems[r][k] = config.IndexOfIngredient(recipe.GetItem(k));
                }
            }

            BoardActionTicks = config.FindStation(StationKind.Board).ActionTicks(ticksPerSecond);
            IsBinAvailable = config.IsStationAvailable(StationKind.Bin);
            BinActionTicks = IsBinAvailable ? config.FindStation(StationKind.Bin).ActionTicks(ticksPerSecond) : 0L;
        }

        /// <summary>Параметри збирання.</summary>
        public BoardConfig Config { get; }

        /// <summary>Скільки інгредієнтів уміщує дошка.</summary>
        public int Capacity => _items.Length;

        /// <summary>Скільки інгредієнтів зараз на дошці.</summary>
        public int ItemCount => _count;

        /// <summary>Чи заповнена дошка.</summary>
        public bool IsFull => _count >= _items.Length;

        /// <summary>Номер інгредієнта «м'ясо з гриля» або -1, якщо його в конфігу немає.</summary>
        public int MeatIngredient { get; }

        /// <summary>Тривалість дії на самій дошці (зняти інгредієнт, видати страву), тактів.</summary>
        public long BoardActionTicks { get; }

        /// <summary>Тривалість очищення дошки у смітник, тактів.</summary>
        public long BinActionTicks { get; }

        /// <summary>Чи доступний смітник на цьому рівні.</summary>
        public bool IsBinAvailable { get; }

        /// <summary>Скільки інгредієнтів покладено на дошку за зміну (разом із м'ясом).</summary>
        public long AddedCount { get; private set; }

        /// <summary>Скільки інгредієнтів знято з дошки тапом за зміну.</summary>
        public long RemovedCount { get; private set; }

        /// <summary>Скільки разів дошку очищено у смітник за зміну.</summary>
        public long ClearedCount { get; private set; }

        /// <summary>Скільки страв видано за зміну.</summary>
        public long ServedCount { get; private set; }

        /// <summary>
        /// Сума штрафів за очищення дошки (rules.boardClearPenaltyCoins за кожне), монет.
        /// Дошка лише рахує суму; списання з рахунку зміни — справа економіки.
        /// </summary>
        public long PenaltyCoins { get; private set; }

        /// <summary>Остання видана страва (має зміст, коли <see cref="ServedCount"/> більший за 0).</summary>
        public Dish LastServed { get; private set; }

        /// <summary>Інгредієнт на дошці за місцем (0 — покладений першим).</summary>
        public int GetItem(int position)
        {
            if (position < 0 || position >= _count)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return _items[position];
        }

        /// <summary>
        /// Чи можна командою «додати інгредієнт» узяти цей інгредієнт: він є в конфігу,
        /// його станція доступна на цьому рівні, і це не м'ясо (м'ясо — лише з гриля).
        /// </summary>
        public bool CanAdd(int ingredient)
        {
            return ingredient >= 0 && ingredient < _ingredientAddable.Length && _ingredientAddable[ingredient];
        }

        /// <summary>Чи може команда «зняти інгредієнт» назвати таке місце.</summary>
        public bool IsValidPosition(int position)
        {
            return position == BoardTarget.TopItem || (position >= 0 && position < _items.Length);
        }

        /// <summary>Тривалість дії «додати інгредієнт» (за його станцією), тактів.</summary>
        public long AddActionTicks(int ingredient)
        {
            return _ingredientActionTicks[ingredient];
        }

        /// <summary>Перевіряє зібране на дошці за доступними рецептами.</summary>
        public DishCheck Check()
        {
            Dish dish;
            return Check(out dish);
        }

        /// <summary>
        /// Перевіряє зібране на дошці; для <see cref="DishCheck.Correct"/> повертає готову страву.
        /// Якщо рецептів кілька, береться найкращий результат (правильна, далі неповна,
        /// хибний порядок, зайвий інгредієнт); за рівних — рецепт, що стоїть у конфігу раніше.
        /// </summary>
        public DishCheck Check(out Dish dish)
        {
            dish = default(Dish);
            if (_count == 0)
            {
                return DishCheck.Empty;
            }

            DishCheck best = DishCheck.ExtraIngredient;
            for (int r = 0; r < _recipeItems.Length; r++)
            {
                if (!_recipeAvailable[r])
                {
                    continue;
                }

                int optionalMask;
                DishCheck check = CheckRecipe(r, out optionalMask);
                if (check == DishCheck.Correct)
                {
                    dish = new Dish(r, optionalMask, Price(r, optionalMask));
                    return DishCheck.Correct;
                }

                if (Rank(check) > Rank(best))
                {
                    best = check;
                }
            }

            return best;
        }

        /// <summary>Знімок повного стану дошки.</summary>
        public BoardSnapshot CaptureSnapshot()
        {
            var items = new int[_count];
            Array.Copy(_items, items, _count);
            return new BoardSnapshot(
                items, AddedCount, RemovedCount, ClearedCount, ServedCount, PenaltyCoins, LastServed);
        }

        /// <summary>Покласти інгредієнт на дошку (будь-який, навіть хибний).</summary>
        internal ShiftActionResult Add(int ingredient)
        {
            if (_count >= _items.Length)
            {
                return ShiftActionResult.BoardFull;
            }

            _items[_count] = ingredient;
            _count++;
            AddedCount++;
            return ShiftActionResult.Done;
        }

        /// <summary>Зняти інгредієнт: за місцем або (<see cref="BoardTarget.TopItem"/>) останній. Без штрафу.</summary>
        internal ShiftActionResult Remove(int position)
        {
            if (_count == 0)
            {
                return ShiftActionResult.BoardEmpty;
            }

            if (position == BoardTarget.TopItem)
            {
                position = _count - 1;
            }
            else if (position >= _count)
            {
                return ShiftActionResult.NoSuchItem;
            }

            for (int i = position + 1; i < _count; i++)
            {
                _items[i - 1] = _items[i];
            }

            _count--;
            _items[_count] = 0;
            RemovedCount++;
            return ShiftActionResult.Done;
        }

        /// <summary>Очистити дошку у смітник зі штрафом. Порожню дошку очистити не можна.</summary>
        internal ShiftActionResult Clear()
        {
            if (_count == 0)
            {
                return ShiftActionResult.BoardEmpty;
            }

            Reset();
            ClearedCount++;
            PenaltyCoins += Config.ClearPenaltyCoins;
            return ShiftActionResult.Done;
        }

        /// <summary>
        /// Видати страву: лише правильну й повну. receiver = null — отримувача не задано,
        /// страва просто зникає з дошки і рахується виданою.
        /// </summary>
        internal ShiftActionResult Serve(IDishReceiver receiver, int target)
        {
            Dish dish;
            switch (Check(out dish))
            {
                case DishCheck.Empty:
                    return ShiftActionResult.BoardEmpty;
                case DishCheck.Incomplete:
                    return ShiftActionResult.DishIncomplete;
                case DishCheck.WrongOrder:
                    return ShiftActionResult.DishWrongOrder;
                case DishCheck.ExtraIngredient:
                    return ShiftActionResult.DishExtraIngredient;
            }

            if (receiver != null && !receiver.TryReceive(dish, target))
            {
                return ShiftActionResult.DishNotAccepted;
            }

            Reset();
            ServedCount++;
            LastServed = dish;
            return ShiftActionResult.Done;
        }

        private void Reset()
        {
            for (int i = 0; i < _count; i++)
            {
                _items[i] = 0;
            }

            _count = 0;
        }

        /// <summary>
        /// Перевірка за одним рецептом. Повний порядок рецепта: кроки, потім необов'язкові.
        /// Кожен інгредієнт у рецепті один раз (це гарантує перевірка конфіга).
        /// </summary>
        private DishCheck CheckRecipe(int recipe, out int optionalMask)
        {
            optionalMask = 0;
            int[] order = _recipeItems[recipe];
            int steps = _recipeSteps[recipe];

            for (int i = 0; i < _count; i++)
            {
                if (Array.IndexOf(order, _items[i]) < 0)
                {
                    return DishCheck.ExtraIngredient;
                }

                for (int k = 0; k < i; k++)
                {
                    if (_items[k] == _items[i])
                    {
                        return DishCheck.ExtraIngredient;
                    }
                }
            }

            int previous = -1;
            int stepsOnBoard = 0;
            for (int i = 0; i < _count; i++)
            {
                int position = Array.IndexOf(order, _items[i]);
                if (position < previous)
                {
                    return DishCheck.WrongOrder;
                }

                previous = position;
                if (position >= steps)
                {
                    optionalMask |= 1 << (position - steps);
                }
                else if (position != stepsOnBoard)
                {
                    // Пропущено обов'язковий крок (наприклад, м'ясо без піти).
                    return DishCheck.WrongOrder;
                }
                else
                {
                    stepsOnBoard++;
                }
            }

            if (stepsOnBoard < steps)
            {
                // Необов'язковий інгредієнт до завершення кроків уже не виправити додаванням.
                return optionalMask != 0 ? DishCheck.WrongOrder : DishCheck.Incomplete;
            }

            return DishCheck.Correct;
        }

        private int Price(int recipe, int optionalMask)
        {
            int[] order = _recipeItems[recipe];
            int steps = _recipeSteps[recipe];
            int price = Config.GetRecipe(recipe).PriceCoins;
            for (int i = steps; i < order.Length; i++)
            {
                if ((optionalMask & (1 << (i - steps))) != 0)
                {
                    price += Config.GetIngredient(order[i]).ExtraPriceCoins;
                }
            }

            return price;
        }

        private static int Rank(DishCheck check)
        {
            switch (check)
            {
                case DishCheck.Correct:
                    return 3;
                case DishCheck.Incomplete:
                    return 2;
                case DishCheck.WrongOrder:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
