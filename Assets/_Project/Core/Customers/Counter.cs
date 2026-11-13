using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Чим закінчилась остання спроба видати страву клієнтові. Числові значення входять
    /// у знімок стану — не змінювати порядок.
    /// </summary>
    public enum DeliveryResult
    {
        /// <summary>Ще нічого не видавали.</summary>
        None = 0,

        /// <summary>Клієнт забрав страву і заплатив.</summary>
        Served = 1,

        /// <summary>Страва не відповідає замовленню названого клієнта: він скаржиться, страва лишається на дошці.</summary>
        WrongDish = 2,

        /// <summary>У названому слоті нікого немає (або такого слота немає, або в слотах узагалі нікого).</summary>
        NoCustomer = 3,

        /// <summary>Видача без названого клієнта: ніхто в слотах не чекає на таку страву.</summary>
        NoMatch = 4,
    }

    /// <summary>
    /// Прилавок (GAME_DESIGN.md, розд. 6): прихід клієнтів за розкладом, черга, слоти, терпіння,
    /// приймання страви з дошки, оплата, чайові, комбо і лічильники зміни. Час — лише такти,
    /// випадковість — лише генератор зміни (запізнення приходів, один раз при створенні).
    /// Зовні доступний лише для читання: стан змінюють тільки такт <see cref="ShiftSimulation"/>
    /// і видача страви з її черги дій.
    /// </summary>
    public sealed class Counter : IDishReceiver
    {
        private const long NoTick = -1L;

        private readonly Customer[] _customers;
        private readonly int[] _arrivalOrder;
        private readonly long _comboWindowTicks;
        private readonly long _wrongDishPenaltyTicks;
        private int _arrivedCount;
        private int _seatedCount;
        private long _nowTick;

        internal Counter(CounterConfig config, BoardConfig board, int ticksPerSecond, IRandom random)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            Slots = new CounterSlots(config.Slots);
            _comboWindowTicks = TickTime.FromMs(config.ComboWindowMs, ticksPerSecond);
            _wrongDishPenaltyTicks = TickTime.FromMs(config.WrongDishPatiencePenaltyMs, ticksPerSecond);
            LastServeTick = NoTick;

            ArrivalSchedule schedule = config.Schedule;
            long[] ticks = schedule.ResolveTicks(ticksPerSecond, random);
            _arrivalOrder = ArrivalSchedule.OrderByTick(ticks);
            _customers = new Customer[schedule.Count];
            for (int i = 0; i < _customers.Length; i++)
            {
                Arrival arrival = schedule.Get(i);
                int typeIndex = config.IndexOfType(arrival.Customer);
                CustomerType type = config.GetCustomerType(typeIndex);
                int recipeIndex;
                int requestedMask;
                int excludeMask;
                CounterConfig.TryResolveOrder(
                    board, type, arrival.Order, null, null, out recipeIndex, out requestedMask, out excludeMask);

                int allOptional = (1 << board.GetRecipe(recipeIndex).OptionalCount) - 1;
                int forbiddenMask = config.ExtraOptionalIsWrong ? allOptional & ~requestedMask : excludeMask;
                _customers[i] = new Customer(
                    i,
                    typeIndex,
                    recipeIndex,
                    requestedMask,
                    excludeMask,
                    forbiddenMask,
                    ticks[i],
                    type.PatienceTicks(ticksPerSecond),
                    TickTime.FromMs(arrival.GraceMs, ticksPerSecond));
            }
        }

        /// <summary>Параметри прилавка і клієнтів.</summary>
        public CounterConfig Config { get; }

        /// <summary>Слоти прилавка.</summary>
        public CounterSlots Slots { get; }

        /// <summary>Скільки клієнтів у розкладі зміни.</summary>
        public int CustomerCount => _customers.Length;

        /// <summary>Скільки клієнтів уже прийшло.</summary>
        public int ArrivedCount => _arrivedCount;

        /// <summary>Скільки клієнтів чекає вільного слота в черзі.</summary>
        public int QueueLength => _arrivedCount - _seatedCount;

        /// <summary>Скільки клієнтів обслужено (servedCount).</summary>
        public long ServedCount { get; private set; }

        /// <summary>Скільки клієнтів пішло без страви (рахуються завжди).</summary>
        public long LeftCount { get; private set; }

        /// <summary>
        /// Скільки втрачених клієнтів зараховано (lostCustomers): росте, лише якщо
        /// <see cref="CounterConfig.CountLostCustomers"/> увімкнено (ADR-020).
        /// </summary>
        public long LostCustomers { get; private set; }

        /// <summary>Скільки разів клієнтові запропонували хибну страву (wrongDishes).</summary>
        public long WrongDishes { get; private set; }

        /// <summary>Поточне комбо: скільки вдалих видач поспіль у вікні; 0 — комбо немає.</summary>
        public long Combo { get; private set; }

        /// <summary>Найбільше комбо за зміну (maxCombo).</summary>
        public long MaxCombo { get; private set; }

        /// <summary>Такт останньої вдалої видачі; -1 — ще не було.</summary>
        public long LastServeTick { get; private set; }

        /// <summary>Усі монети від клієнтів за зміну: плата за страви + чайові + надбавка комбо.</summary>
        public long IncomeCoins { get; private set; }

        /// <summary>Частина доходу: чайові.</summary>
        public long TipCoins { get; private set; }

        /// <summary>Частина доходу: надбавка комбо.</summary>
        public long ComboCoins { get; private set; }

        /// <summary>Чим закінчилась остання спроба видати страву.</summary>
        public DeliveryResult LastDelivery { get; private set; }

        /// <summary>Скільки монет принесла остання спроба видачі (0, якщо страву не взяли).</summary>
        public long LastPaidCoins { get; private set; }

        /// <summary>Клієнт за номером (місце в розкладі, 0..CustomerCount-1).</summary>
        public Customer GetCustomer(int id)
        {
            if (id < 0 || id >= _customers.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            return _customers[id];
        }

        /// <summary>Клієнт у слоті або null, якщо слот порожній.</summary>
        public Customer GetCustomerInSlot(int slot)
        {
            int id = Slots.GetCustomer(slot);
            return id == CounterSlots.None ? null : _customers[id];
        }

        /// <summary>Клієнт у черзі за місцем (0 — перший стане в слот).</summary>
        public Customer GetQueued(int position)
        {
            if (position < 0 || position >= QueueLength)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return _customers[_arrivalOrder[_seatedCount + position]];
        }

        /// <summary>
        /// Дошка видає страву. Названий слот: клієнт бере страву, якщо вона відповідає
        /// замовленню; інакше це хибна страва — він скаржиться і втрачає терпіння, комбо
        /// скидається, страва лишається на дошці. Без названого клієнта
        /// (<see cref="BoardTarget.AnyCustomer"/>): страву бере той, чиє замовлення вона
        /// виконує (з найменшим залишком терпіння, далі — з меншим номером слота); якщо
        /// такого немає — нічого не відбувається.
        /// </summary>
        public bool TryReceive(Dish dish, int target)
        {
            LastPaidCoins = 0L;
            Customer customer;
            if (target == BoardTarget.AnyCustomer)
            {
                bool anyone;
                customer = FindWaitingFor(dish, out anyone);
                if (customer == null)
                {
                    LastDelivery = anyone ? DeliveryResult.NoMatch : DeliveryResult.NoCustomer;
                    return false;
                }
            }
            else
            {
                int id = Slots.IsValidSlot(target) ? Slots.GetCustomer(target) : CounterSlots.None;
                if (id == CounterSlots.None)
                {
                    LastDelivery = DeliveryResult.NoCustomer;
                    return false;
                }

                customer = _customers[id];
                if (!customer.Accepts(dish))
                {
                    Complain(customer);
                    return false;
                }
            }

            Pay(customer, dish);
            return true;
        }

        /// <summary>Знімок повного стану прилавка.</summary>
        public CounterSnapshot CaptureSnapshot()
        {
            var customers = new CustomerSnapshot[_customers.Length];
            for (int i = 0; i < customers.Length; i++)
            {
                Customer customer = _customers[i];
                customers[i] = new CustomerSnapshot(
                    customer.State,
                    customer.Slot,
                    customer.ArrivalTick,
                    customer.PatienceLeftTicks,
                    customer.GraceLeftTicks,
                    customer.WrongDishCount,
                    customer.PaidCoins);
            }

            return new CounterSnapshot(
                customers,
                _arrivedCount,
                _seatedCount,
                ServedCount,
                LeftCount,
                LostCustomers,
                WrongDishes,
                Combo,
                MaxCombo,
                LastServeTick,
                IncomeCoins,
                TipCoins,
                ComboCoins,
                LastDelivery,
                LastPaidCoins);
        }

        /// <summary>
        /// Один такт: спершу терпіння тих, хто в слотах (у кого скінчилось — іде), потім нові
        /// приходи за розкладом, потім черга займає вільні слоти. Клієнт, що став у слот на
        /// цьому такті, має повне терпіння.
        /// </summary>
        internal void Advance(long nowTick)
        {
            _nowTick = nowTick;
            if (Combo > 0L && nowTick - LastServeTick > _comboWindowTicks)
            {
                Combo = 0L;
            }

            for (int slot = 0; slot < Slots.Count; slot++)
            {
                int id = Slots.GetCustomer(slot);
                if (id != CounterSlots.None && _customers[id].TickPatience())
                {
                    Leave(_customers[id]);
                }
            }

            while (_arrivedCount < _arrivalOrder.Length
                && _customers[_arrivalOrder[_arrivedCount]].ArrivalTick <= nowTick)
            {
                _customers[_arrivalOrder[_arrivedCount]].Arrive();
                _arrivedCount++;
            }

            while (_seatedCount < _arrivedCount)
            {
                int slot = Slots.FindFree();
                if (slot == CounterSlots.None)
                {
                    break;
                }

                Customer customer = _customers[_arrivalOrder[_seatedCount]];
                Slots.Seat(slot, customer.Id);
                customer.Seat(slot);
                _seatedCount++;
            }
        }

        private Customer FindWaitingFor(Dish dish, out bool anyone)
        {
            anyone = false;
            Customer found = null;
            for (int slot = 0; slot < Slots.Count; slot++)
            {
                int id = Slots.GetCustomer(slot);
                if (id == CounterSlots.None)
                {
                    continue;
                }

                anyone = true;
                Customer customer = _customers[id];
                if (customer.Accepts(dish)
                    && (found == null || customer.PatienceLeftTicks < found.PatienceLeftTicks))
                {
                    found = customer;
                }
            }

            return found;
        }

        private void Complain(Customer customer)
        {
            WrongDishes++;
            Combo = 0L;
            LastDelivery = DeliveryResult.WrongDish;
            if (customer.Complain(_wrongDishPenaltyTicks))
            {
                Leave(customer);
            }
        }

        private void Leave(Customer customer)
        {
            Slots.Free(customer.Slot);
            customer.Leave();
            LeftCount++;
            if (Config.CountLostCustomers)
            {
                LostCustomers++;
            }

            Combo = 0L;
        }

        private void Pay(Customer customer, Dish dish)
        {
            CustomerType type = Config.GetCustomerType(customer.TypeIndex);
            long pay = CoinMath.Percent(dish.PriceCoins, type.PayPercent);
            long tip = type.Tip.TipCoins(pay, customer.PatienceLeftTicks, customer.PatienceTicks);

            Combo++;
            if (Combo > MaxCombo)
            {
                MaxCombo = Combo;
            }

            long steps = Math.Min(Combo - 1L, Config.ComboMaxSteps);
            long bonus = CoinMath.Percent(pay, steps * Config.ComboStepPercent);
            long total = pay + tip + bonus;

            Slots.Free(customer.Slot);
            customer.Serve(total);
            ServedCount++;
            LastServeTick = _nowTick;
            IncomeCoins += total;
            TipCoins += tip;
            ComboCoins += bonus;
            LastDelivery = DeliveryResult.Served;
            LastPaidCoins = total;
        }
    }
}
