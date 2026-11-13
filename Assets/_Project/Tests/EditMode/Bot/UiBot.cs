using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Тапи, які має гравець на екрані зміни (S1-09c).</summary>
    internal interface IUiTaps
    {
        bool Grill(int slot);

        bool Customer(int slot);

        /// <summary>Кнопка станції за номером інгредієнта.</summary>
        bool Station(int ingredient);

        bool BoardItem(int position);
    }

    /// <summary>Тапи через <see cref="ShiftInput"/> — той самий шар, яким користуються панелі.</summary>
    internal sealed class InputTaps : IUiTaps
    {
        private readonly ShiftInput _input;

        public InputTaps(ShiftInput input)
        {
            _input = input;
        }

        public bool Grill(int slot) => _input.TapGrillSlot(slot);

        public bool Customer(int slot) => _input.TapCustomer(slot);

        public bool Station(int ingredient) => _input.TapStation(ingredient);

        public bool BoardItem(int position) => _input.TapBoardItem(position);
    }

    /// <summary>
    /// Бот-експерт, що грає лише тапами екрана (S1-09c): бачить те, що показують моделі вигляду
    /// (стан слотів гриля, клієнти з терпінням і значками замовлення, черга, дошка), і тапає
    /// гриль, клієнта, станцію, інгредієнт на дошці. Політика — як у <see cref="BotPlayer"/>:
    /// прибрати пригоріле; видати готову страву; зняти зайве; покласти м'ясо, якщо його менше,
    /// ніж клієнтів; збирати для найнетерплячішого. Діє, лише коли черга дій порожня.
    /// </summary>
    internal sealed class UiBot
    {
        private readonly ShiftDriver _driver;
        private readonly IUiTaps _taps;
        private readonly List<int> _items = new List<int>(16);

        public UiBot(ShiftDriver driver, IUiTaps taps)
        {
            _driver = driver;
            _taps = taps;
        }

        /// <summary>Скільки тапів бот зробив (прийнятих до черги).</summary>
        public int Taps { get; private set; }

        /// <summary>Перед кожним кадром: бот тапає, якщо вирішив діяти.</summary>
        public void Step()
        {
            ShiftSimulation sim = _driver.Simulation;
            if (_driver.IsFinished || sim.State != ShiftState.Running || sim.QueuedActionCount > 0)
            {
                return;
            }

            if (Decide(sim))
            {
                Taps++;
            }
        }

        private bool Decide(ShiftSimulation sim)
        {
            Board board = sim.Board;
            int grillSlots = sim.Grill.SlotCount;
            int freeGrill = -1;
            int doneGrill = -1;
            int supplied = 0;
            for (int s = 0; s < grillSlots; s++)
            {
                GrillSlotDisplay display = GrillView.FromSimulation(sim, s).Display;
                if (display == GrillSlotDisplay.Burnt)
                {
                    return _taps.Grill(s);
                }

                if (display == GrillSlotDisplay.Empty)
                {
                    freeGrill = freeGrill < 0 ? s : freeGrill;
                }
                else
                {
                    supplied++;
                    doneGrill = display == GrillSlotDisplay.Done && doneGrill < 0 ? s : doneGrill;
                }
            }

            int count = board.ItemCount;
            int target = -1;
            if (count > 0)
            {
                int receiver = FindCustomer(sim, true);
                if (receiver >= 0)
                {
                    return _taps.Customer(receiver);
                }

                target = FindCustomer(sim, false);
                if (target < 0 && Waiting(sim) > 0)
                {
                    return _taps.BoardItem(count - 1);
                }
            }

            for (int i = 0; i < count; i++)
            {
                supplied += board.GetItem(i) == board.MeatIngredient ? 1 : 0;
            }

            int freeSeats = sim.Counter.Slots.Count - Waiting(sim);
            int queue = CounterView.QueueLength(sim);
            int demand = Waiting(sim) + (queue < freeSeats ? queue : freeSeats);
            if (freeGrill >= 0 && supplied < demand)
            {
                return _taps.Grill(freeGrill);
            }

            if (count == 0)
            {
                target = MostUrgent(sim);
            }

            if (target < 0)
            {
                return false;
            }

            BuildItems(sim, target);
            int next = _items[count];
            if (next == board.MeatIngredient)
            {
                return doneGrill >= 0 && _taps.Grill(doneGrill);
            }

            return _taps.Station(next);
        }

        /// <summary>
        /// Слот клієнта з найменшим терпінням (смужка, %): exact — чиє замовлення збігається з дошкою;
        /// інакше — чиє замовлення довше за дошку й починається з неї. -1 — такого немає.
        /// </summary>
        private int FindCustomer(ShiftSimulation sim, bool exact)
        {
            Board board = sim.Board;
            int best = -1;
            int bestPercent = int.MaxValue;
            for (int s = 0; s < sim.Counter.Slots.Count; s++)
            {
                CustomerSlotView view = CounterView.FromSimulation(sim, s, CounterView.DefaultHearts);
                if (!view.Occupied || view.Patience.Percent >= bestPercent)
                {
                    continue;
                }

                BuildItems(sim, s);
                bool fits = exact ? _items.Count == board.ItemCount : _items.Count > board.ItemCount;
                for (int i = 0; fits && i < board.ItemCount; i++)
                {
                    fits = board.GetItem(i) == _items[i];
                }

                if (fits)
                {
                    best = s;
                    bestPercent = view.Patience.Percent;
                }
            }

            return best;
        }

        private static int MostUrgent(ShiftSimulation sim)
        {
            int best = -1;
            int bestPercent = int.MaxValue;
            for (int s = 0; s < sim.Counter.Slots.Count; s++)
            {
                CustomerSlotView view = CounterView.FromSimulation(sim, s, CounterView.DefaultHearts);
                if (view.Occupied && view.Patience.Percent < bestPercent)
                {
                    best = s;
                    bestPercent = view.Patience.Percent;
                }
            }

            return best;
        }

        private static int Waiting(ShiftSimulation sim)
        {
            int waiting = 0;
            for (int s = 0; s < sim.Counter.Slots.Count; s++)
            {
                waiting += CounterView.FromSimulation(sim, s, CounterView.DefaultHearts).Occupied ? 1 : 0;
            }

            return waiting;
        }

        /// <summary>Склад страви за значками замовлення клієнта в слоті: кроки, потім те, що він просить.</summary>
        private void BuildItems(ShiftSimulation sim, int slot)
        {
            _items.Clear();
            CustomerSlotView view = CounterView.FromSimulation(sim, slot, CounterView.DefaultHearts);
            OrderIcon[] icons = CounterView.OrderOf(sim.Board.Config, sim.Counter.GetCustomer(view.CustomerId));
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i].Kind != OrderIconKind.Excluded)
                {
                    _items.Add(icons[i].Ingredient);
                }
            }
        }
    }
}
