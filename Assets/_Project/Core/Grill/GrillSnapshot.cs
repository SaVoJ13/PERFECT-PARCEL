using System;
using System.Globalization;
using System.Text;

namespace KebabStreet.Core
{
    /// <summary>
    /// Знімок повного стану гриля: стан і вік м'яса в кожному слоті та лічильники зміни.
    /// Значення за замовчуванням (default) — «гриля немає». Лише цілі числа, порівняння точне.
    /// </summary>
    public readonly struct GrillSnapshot : IEquatable<GrillSnapshot>
    {
        private readonly MeatState[] _states;
        private readonly long[] _ages;

        /// <summary>Масиви копіюються; null означає «без слотів».</summary>
        public GrillSnapshot(
            MeatState[] states,
            long[] ageTicks,
            long placedCount,
            long takenCount,
            long burntCount,
            long discardedCount,
            long penaltyCoins)
        {
            int stateCount = states == null ? 0 : states.Length;
            int ageCount = ageTicks == null ? 0 : ageTicks.Length;
            if (stateCount != ageCount)
            {
                throw new ArgumentException("Кількість станів і віків слотів має збігатися.", nameof(ageTicks));
            }

            _states = stateCount == 0 ? null : (MeatState[])states.Clone();
            _ages = ageCount == 0 ? null : (long[])ageTicks.Clone();
            PlacedCount = placedCount;
            TakenCount = takenCount;
            BurntCount = burntCount;
            DiscardedCount = discardedCount;
            PenaltyCoins = penaltyCoins;
        }

        /// <summary>Кількість слотів; 0 — гриля немає.</summary>
        public int SlotCount => _states == null ? 0 : _states.Length;

        /// <summary>Скільки шматків покладено на гриль.</summary>
        public long PlacedCount { get; }

        /// <summary>Скільки готових шматків знято.</summary>
        public long TakenCount { get; }

        /// <summary>Скільки шматків пригоріло.</summary>
        public long BurntCount { get; }

        /// <summary>Скільки пригорілих шматків викинуто.</summary>
        public long DiscardedCount { get; }

        /// <summary>Сума штрафів за пригоріле, монет.</summary>
        public long PenaltyCoins { get; }

        /// <summary>Стан м'яса в слоті.</summary>
        public MeatState GetState(int slot)
        {
            CheckSlot(slot);
            return _states[slot];
        }

        /// <summary>Вік м'яса в слоті, тактів.</summary>
        public long GetAgeTicks(int slot)
        {
            CheckSlot(slot);
            return _ages[slot];
        }

        public bool Equals(GrillSnapshot other)
        {
            if (SlotCount != other.SlotCount
                || PlacedCount != other.PlacedCount
                || TakenCount != other.TakenCount
                || BurntCount != other.BurntCount
                || DiscardedCount != other.DiscardedCount
                || PenaltyCoins != other.PenaltyCoins)
            {
                return false;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (_states[i] != other._states[i] || _ages[i] != other._ages[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GrillSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SlotCount;
                for (int i = 0; i < SlotCount; i++)
                {
                    hash = (hash * 397) ^ (int)_states[i];
                    hash = (hash * 397) ^ _ages[i].GetHashCode();
                }

                hash = (hash * 397) ^ PlacedCount.GetHashCode();
                hash = (hash * 397) ^ TakenCount.GetHashCode();
                hash = (hash * 397) ^ BurntCount.GetHashCode();
                hash = (hash * 397) ^ DiscardedCount.GetHashCode();
                hash = (hash * 397) ^ PenaltyCoins.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < SlotCount; i++)
            {
                if (i > 0)
                {
                    text.Append(' ');
                }

                text.Append(_states[i].ToString());
                text.Append(':');
                text.Append(_ages[i].ToString(CultureInfo.InvariantCulture));
            }

            text.Append(string.Format(
                CultureInfo.InvariantCulture,
                "] placed={0} taken={1} burnt={2} discarded={3} penalty={4}",
                PlacedCount,
                TakenCount,
                BurntCount,
                DiscardedCount,
                PenaltyCoins));
            return text.ToString();
        }

        private void CheckSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }
}
