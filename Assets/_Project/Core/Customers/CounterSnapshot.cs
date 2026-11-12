using System;
using System.Globalization;
using System.Text;

namespace KebabStreet.Core
{
    /// <summary>Знімок стану одного клієнта. Лише цілі числа, порівняння точне.</summary>
    public readonly struct CustomerSnapshot : IEquatable<CustomerSnapshot>
    {
        public CustomerSnapshot(
            CustomerState state,
            int slot,
            long arrivalTick,
            long patienceLeftTicks,
            long graceLeftTicks,
            long wrongDishCount,
            long paidCoins)
        {
            State = state;
            Slot = slot;
            ArrivalTick = arrivalTick;
            PatienceLeftTicks = patienceLeftTicks;
            GraceLeftTicks = graceLeftTicks;
            WrongDishCount = wrongDishCount;
            PaidCoins = paidCoins;
        }

        /// <summary>Стан клієнта.</summary>
        public CustomerState State { get; }

        /// <summary>Номер слота прилавка або <see cref="CounterSlots.None"/>.</summary>
        public int Slot { get; }

        /// <summary>Такт приходу.</summary>
        public long ArrivalTick { get; }

        /// <summary>Залишок терпіння, тактів.</summary>
        public long PatienceLeftTicks { get; }

        /// <summary>Залишок пільгових тактів.</summary>
        public long GraceLeftTicks { get; }

        /// <summary>Скільки хибних страв клієнтові пропонували.</summary>
        public long WrongDishCount { get; }

        /// <summary>Скільки монет клієнт заплатив.</summary>
        public long PaidCoins { get; }

        public bool Equals(CustomerSnapshot other)
        {
            return State == other.State
                && Slot == other.Slot
                && ArrivalTick == other.ArrivalTick
                && PatienceLeftTicks == other.PatienceLeftTicks
                && GraceLeftTicks == other.GraceLeftTicks
                && WrongDishCount == other.WrongDishCount
                && PaidCoins == other.PaidCoins;
        }

        public override bool Equals(object obj)
        {
            return obj is CustomerSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)State;
                hash = (hash * 397) ^ Slot;
                hash = (hash * 397) ^ ArrivalTick.GetHashCode();
                hash = (hash * 397) ^ PatienceLeftTicks.GetHashCode();
                hash = (hash * 397) ^ GraceLeftTicks.GetHashCode();
                hash = (hash * 397) ^ WrongDishCount.GetHashCode();
                hash = (hash * 397) ^ PaidCoins.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}@{2}:{3}+{4}:{5}:{6}",
                State,
                Slot,
                ArrivalTick,
                PatienceLeftTicks,
                GraceLeftTicks,
                WrongDishCount,
                PaidCoins);
        }
    }

    /// <summary>
    /// Знімок повного стану прилавка: усі клієнти розкладу, черга, лічильники, комбо і дохід.
    /// Значення за замовчуванням (default) — прилавка в зміні немає. Лише цілі числа.
    /// </summary>
    public readonly struct CounterSnapshot : IEquatable<CounterSnapshot>
    {
        private readonly CustomerSnapshot[] _customers;

        /// <summary>Масив копіюється; null означає «клієнтів немає».</summary>
        public CounterSnapshot(
            CustomerSnapshot[] customers,
            int arrivedCount,
            int seatedCount,
            long servedCount,
            long leftCount,
            long lostCustomers,
            long wrongDishes,
            long combo,
            long maxCombo,
            long lastServeTick,
            long incomeCoins,
            long tipCoins,
            long comboCoins,
            DeliveryResult lastDelivery,
            long lastPaidCoins)
        {
            _customers = customers == null || customers.Length == 0 ? null : (CustomerSnapshot[])customers.Clone();
            ArrivedCount = arrivedCount;
            SeatedCount = seatedCount;
            ServedCount = servedCount;
            LeftCount = leftCount;
            LostCustomers = lostCustomers;
            WrongDishes = wrongDishes;
            Combo = combo;
            MaxCombo = maxCombo;
            LastServeTick = lastServeTick;
            IncomeCoins = incomeCoins;
            TipCoins = tipCoins;
            ComboCoins = comboCoins;
            LastDelivery = lastDelivery;
            LastPaidCoins = lastPaidCoins;
        }

        /// <summary>Скільки клієнтів у розкладі.</summary>
        public int CustomerCount => _customers == null ? 0 : _customers.Length;

        /// <summary>Скільки клієнтів прийшло.</summary>
        public int ArrivedCount { get; }

        /// <summary>Скільки клієнтів уже ставало в слот (решта тих, що прийшли, — у черзі).</summary>
        public int SeatedCount { get; }

        /// <summary>Скільки клієнтів обслужено.</summary>
        public long ServedCount { get; }

        /// <summary>Скільки клієнтів пішло без страви.</summary>
        public long LeftCount { get; }

        /// <summary>Скільки втрачених клієнтів зараховано.</summary>
        public long LostCustomers { get; }

        /// <summary>Скільки хибних страв запропоновано.</summary>
        public long WrongDishes { get; }

        /// <summary>Поточне комбо.</summary>
        public long Combo { get; }

        /// <summary>Найбільше комбо за зміну.</summary>
        public long MaxCombo { get; }

        /// <summary>Такт останньої вдалої видачі; -1 — ще не було.</summary>
        public long LastServeTick { get; }

        /// <summary>Дохід від клієнтів, монет.</summary>
        public long IncomeCoins { get; }

        /// <summary>Чайові в доході, монет.</summary>
        public long TipCoins { get; }

        /// <summary>Надбавка комбо в доході, монет.</summary>
        public long ComboCoins { get; }

        /// <summary>Результат останньої спроби видачі.</summary>
        public DeliveryResult LastDelivery { get; }

        /// <summary>Монети за останню спробу видачі.</summary>
        public long LastPaidCoins { get; }

        /// <summary>Клієнт за номером у розкладі.</summary>
        public CustomerSnapshot GetCustomer(int id)
        {
            if (id < 0 || id >= CustomerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            return _customers[id];
        }

        public bool Equals(CounterSnapshot other)
        {
            if (CustomerCount != other.CustomerCount
                || ArrivedCount != other.ArrivedCount
                || SeatedCount != other.SeatedCount
                || ServedCount != other.ServedCount
                || LeftCount != other.LeftCount
                || LostCustomers != other.LostCustomers
                || WrongDishes != other.WrongDishes
                || Combo != other.Combo
                || MaxCombo != other.MaxCombo
                || LastServeTick != other.LastServeTick
                || IncomeCoins != other.IncomeCoins
                || TipCoins != other.TipCoins
                || ComboCoins != other.ComboCoins
                || LastDelivery != other.LastDelivery
                || LastPaidCoins != other.LastPaidCoins)
            {
                return false;
            }

            for (int i = 0; i < CustomerCount; i++)
            {
                if (!_customers[i].Equals(other._customers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is CounterSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CustomerCount;
                for (int i = 0; i < CustomerCount; i++)
                {
                    hash = (hash * 397) ^ _customers[i].GetHashCode();
                }

                hash = (hash * 397) ^ ArrivedCount;
                hash = (hash * 397) ^ SeatedCount;
                hash = (hash * 397) ^ ServedCount.GetHashCode();
                hash = (hash * 397) ^ LeftCount.GetHashCode();
                hash = (hash * 397) ^ LostCustomers.GetHashCode();
                hash = (hash * 397) ^ WrongDishes.GetHashCode();
                hash = (hash * 397) ^ Combo.GetHashCode();
                hash = (hash * 397) ^ MaxCombo.GetHashCode();
                hash = (hash * 397) ^ LastServeTick.GetHashCode();
                hash = (hash * 397) ^ IncomeCoins.GetHashCode();
                hash = (hash * 397) ^ TipCoins.GetHashCode();
                hash = (hash * 397) ^ ComboCoins.GetHashCode();
                hash = (hash * 397) ^ (int)LastDelivery;
                hash = (hash * 397) ^ LastPaidCoins.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < CustomerCount; i++)
            {
                if (i > 0)
                {
                    text.Append(' ');
                }

                text.Append(_customers[i].ToString());
            }

            text.Append(string.Format(
                CultureInfo.InvariantCulture,
                "] arrived={0} seated={1} served={2} left={3} lost={4} wrong={5} combo={6} maxCombo={7}"
                + " lastServe={8} income={9} tips={10} comboCoins={11} last={12}:{13}",
                ArrivedCount,
                SeatedCount,
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
                LastPaidCoins));
            return text.ToString();
        }
    }
}
