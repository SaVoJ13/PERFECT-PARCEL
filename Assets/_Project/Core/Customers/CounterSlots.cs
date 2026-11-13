using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Слоти прилавка (level.counterSlots): у кожному — номер клієнта або нікого. Зовні
    /// доступні лише для читання: клієнтів ставить і знімає тільки <see cref="Counter"/>.
    /// </summary>
    public sealed class CounterSlots
    {
        /// <summary>Слот порожній / клієнт не в слоті.</summary>
        public const int None = -1;

        private readonly int[] _customers;

        internal CounterSlots(int count)
        {
            _customers = new int[count];
            for (int i = 0; i < _customers.Length; i++)
            {
                _customers[i] = None;
            }
        }

        /// <summary>Кількість слотів.</summary>
        public int Count => _customers.Length;

        /// <summary>Кількість порожніх слотів.</summary>
        public int FreeCount
        {
            get
            {
                int free = 0;
                for (int i = 0; i < _customers.Length; i++)
                {
                    if (_customers[i] == None)
                    {
                        free++;
                    }
                }

                return free;
            }
        }

        /// <summary>Чи є слот із таким номером.</summary>
        public bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < _customers.Length;
        }

        /// <summary>Номер клієнта в слоті або <see cref="None"/>.</summary>
        public int GetCustomer(int slot)
        {
            if (!IsValidSlot(slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return _customers[slot];
        }

        /// <summary>Перший порожній слот або <see cref="None"/>.</summary>
        internal int FindFree()
        {
            for (int i = 0; i < _customers.Length; i++)
            {
                if (_customers[i] == None)
                {
                    return i;
                }
            }

            return None;
        }

        internal void Seat(int slot, int customer)
        {
            _customers[slot] = customer;
        }

        internal void Free(int slot)
        {
            _customers[slot] = None;
        }
    }
}
