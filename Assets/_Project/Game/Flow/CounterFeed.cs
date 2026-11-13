using System;
using System.Globalization;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Подія біля слота прилавка, яку коротко показує екран. Числові значення — номери в таблицях показу.</summary>
    public enum CounterEventKind
    {
        None = 0,

        /// <summary>Клієнт забрав страву і заплатив.</summary>
        Served = 1,

        /// <summary>Клієнт не дочекався й пішов.</summary>
        Left = 2,

        /// <summary>Клієнтові запропонували не ту страву: він скаржиться.</summary>
        WrongDish = 3,
    }

    /// <summary>Подія слота: що сталося, скільки заплачено (разом із чайовими й комбо), скільки з них чайові.</summary>
    public readonly struct CounterEvent : IEquatable<CounterEvent>
    {
        public CounterEvent(CounterEventKind kind, long paidCoins, long tipCoins, long untilTick)
        {
            Kind = kind;
            PaidCoins = paidCoins;
            TipCoins = tipCoins;
            UntilTick = untilTick;
        }

        public CounterEventKind Kind { get; }

        /// <summary>Усе, що заплатив клієнт (Served), монет.</summary>
        public long PaidCoins { get; }

        /// <summary>Чайові в цій платі; 0 — немає або невідомо (кілька видач за один кадр).</summary>
        public long TipCoins { get; }

        /// <summary>До якого такту зміни подію показувати.</summary>
        public long UntilTick { get; }

        public bool Equals(CounterEvent other)
        {
            return Kind == other.Kind
                && PaidCoins == other.PaidCoins
                && TipCoins == other.TipCoins
                && UntilTick == other.UntilTick;
        }

        public override bool Equals(object obj)
        {
            return obj is CounterEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ PaidCoins.GetHashCode();
                hash = (hash * 397) ^ TipCoins.GetHashCode();
                hash = (hash * 397) ^ UntilTick.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} paid={1} tip={2} until={3}", Kind, PaidCoins, TipCoins, UntilTick);
        }
    }

    /// <summary>
    /// Стрічка подій прилавка (S1-09b): порівнює стан клієнтів Core між кадрами й запам'ятовує для
    /// кожного слота останню подію — оплату (з чайовими), відхід, скаргу на хибну страву — на
    /// <see cref="ShowMs"/> часу зміни. Нічого не рахує замість Core: суми беруться з лічильників
    /// клієнта і прилавка. Без UnityEngine; <see cref="Update"/> не виділяє пам'яті.
    /// </summary>
    public sealed class CounterFeed
    {
        /// <summary>Скільки показувати подію, мс часу зміни (вигляд, не баланс; на паузі час стоїть).</summary>
        public const int ShowMs = 1500;

        private readonly CustomerState[] _states;
        private readonly long[] _wrongDishes;
        private readonly int[] _slots;
        private readonly CounterEvent[] _events;
        private readonly long _showTicks;
        private long _tipCoins;

        public CounterFeed(ShiftSimulation simulation)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            Counter counter = simulation.Counter;
            int customers = counter == null ? 0 : counter.CustomerCount;
            _states = new CustomerState[customers];
            _wrongDishes = new long[customers];
            _slots = new int[customers];
            _events = new CounterEvent[counter == null ? 0 : counter.Slots.Count];
            _showTicks = TickTime.FromMs(ShowMs, simulation.Config.TicksPerSecond);
            for (int id = 0; id < customers; id++)
            {
                Customer customer = counter.GetCustomer(id);
                _states[id] = customer.State;
                _wrongDishes[id] = customer.WrongDishCount;
                _slots[id] = customer.Slot;
            }

            _tipCoins = counter == null ? 0L : counter.TipCoins;
        }

        /// <summary>Скільки слотів прилавка.</summary>
        public int SlotCount => _events.Length;

        /// <summary>Подія слота, яку зараз показувати (Kind = None — нічого).</summary>
        public CounterEvent GetEvent(int slot)
        {
            return _events[slot];
        }

        /// <summary>Звіряє стан клієнтів із попереднім кадром. Викликається щокадру.</summary>
        public void Update(ShiftSimulation simulation)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            Counter counter = simulation.Counter;
            if (counter == null)
            {
                return;
            }

            long now = simulation.ElapsedTicks;
            long tipDelta = counter.TipCoins - _tipCoins;
            _tipCoins = counter.TipCoins;
            int served = 0;
            int servedSlot = CounterSlots.None;

            for (int id = 0; id < _states.Length; id++)
            {
                Customer customer = counter.GetCustomer(id);
                if (customer.State == CustomerState.Waiting)
                {
                    _slots[id] = customer.Slot;
                }

                int slot = _slots[id];
                if (customer.WrongDishCount != _wrongDishes[id])
                {
                    _wrongDishes[id] = customer.WrongDishCount;
                    Show(slot, new CounterEvent(CounterEventKind.WrongDish, 0L, 0L, now + _showTicks));
                }

                CustomerState state = customer.State;
                if (state == _states[id])
                {
                    continue;
                }

                _states[id] = state;
                if (state == CustomerState.Served)
                {
                    served++;
                    servedSlot = slot;
                    Show(slot, new CounterEvent(CounterEventKind.Served, customer.PaidCoins, 0L, now + _showTicks));
                }
                else if (state == CustomerState.Left)
                {
                    Show(slot, new CounterEvent(CounterEventKind.Left, 0L, 0L, now + _showTicks));
                }
            }

            // Чайові Core рахує лише сумою за зміну: якщо за кадр була одна видача, приріст — її чайові.
            if (served == 1 && tipDelta > 0L && IsShown(servedSlot))
            {
                CounterEvent paid = _events[servedSlot];
                _events[servedSlot] = new CounterEvent(paid.Kind, paid.PaidCoins, tipDelta, paid.UntilTick);
            }

            for (int slot = 0; slot < _events.Length; slot++)
            {
                if (_events[slot].Kind != CounterEventKind.None && now >= _events[slot].UntilTick)
                {
                    _events[slot] = default(CounterEvent);
                }
            }
        }

        private bool IsShown(int slot)
        {
            return slot >= 0 && slot < _events.Length;
        }

        private void Show(int slot, CounterEvent counterEvent)
        {
            if (IsShown(slot))
            {
                _events[slot] = counterEvent;
            }
        }
    }
}
