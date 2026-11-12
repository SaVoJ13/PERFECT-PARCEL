using System;
using System.Globalization;
using System.Text;

namespace KebabStreet.Core
{
    /// <summary>
    /// Знімок повного стану дошки: інгредієнти в порядку, в якому їх поклали, лічильники зміни
    /// та остання видана страва. Значення за замовчуванням (default) — дошки немає або вона
    /// ще не використовувалась. Лише цілі числа, порівняння точне.
    /// </summary>
    public readonly struct BoardSnapshot : IEquatable<BoardSnapshot>
    {
        private readonly int[] _items;

        /// <summary>Масив копіюється; null означає порожню дошку.</summary>
        public BoardSnapshot(
            int[] items,
            long addedCount,
            long removedCount,
            long clearedCount,
            long servedCount,
            long penaltyCoins,
            Dish lastServed)
        {
            _items = items == null || items.Length == 0 ? null : (int[])items.Clone();
            AddedCount = addedCount;
            RemovedCount = removedCount;
            ClearedCount = clearedCount;
            ServedCount = servedCount;
            PenaltyCoins = penaltyCoins;
            LastServed = lastServed;
        }

        /// <summary>Скільки інгредієнтів на дошці.</summary>
        public int ItemCount => _items == null ? 0 : _items.Length;

        /// <summary>Скільки інгредієнтів покладено на дошку.</summary>
        public long AddedCount { get; }

        /// <summary>Скільки інгредієнтів знято тапом.</summary>
        public long RemovedCount { get; }

        /// <summary>Скільки разів дошку очищено у смітник.</summary>
        public long ClearedCount { get; }

        /// <summary>Скільки страв видано.</summary>
        public long ServedCount { get; }

        /// <summary>Сума штрафів за очищення дошки, монет.</summary>
        public long PenaltyCoins { get; }

        /// <summary>Остання видана страва.</summary>
        public Dish LastServed { get; }

        /// <summary>Інгредієнт за місцем (0 — покладений першим).</summary>
        public int GetItem(int position)
        {
            if (position < 0 || position >= ItemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return _items[position];
        }

        public bool Equals(BoardSnapshot other)
        {
            if (ItemCount != other.ItemCount
                || AddedCount != other.AddedCount
                || RemovedCount != other.RemovedCount
                || ClearedCount != other.ClearedCount
                || ServedCount != other.ServedCount
                || PenaltyCoins != other.PenaltyCoins
                || !LastServed.Equals(other.LastServed))
            {
                return false;
            }

            for (int i = 0; i < ItemCount; i++)
            {
                if (_items[i] != other._items[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is BoardSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ItemCount;
                for (int i = 0; i < ItemCount; i++)
                {
                    hash = (hash * 397) ^ _items[i];
                }

                hash = (hash * 397) ^ AddedCount.GetHashCode();
                hash = (hash * 397) ^ RemovedCount.GetHashCode();
                hash = (hash * 397) ^ ClearedCount.GetHashCode();
                hash = (hash * 397) ^ ServedCount.GetHashCode();
                hash = (hash * 397) ^ PenaltyCoins.GetHashCode();
                hash = (hash * 397) ^ LastServed.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            var text = new StringBuilder("[");
            for (int i = 0; i < ItemCount; i++)
            {
                if (i > 0)
                {
                    text.Append(' ');
                }

                text.Append(_items[i].ToString(CultureInfo.InvariantCulture));
            }

            text.Append(string.Format(
                CultureInfo.InvariantCulture,
                "] added={0} removed={1} cleared={2} served={3} penalty={4} last={5}",
                AddedCount,
                RemovedCount,
                ClearedCount,
                ServedCount,
                PenaltyCoins,
                LastServed));
            return text.ToString();
        }
    }
}
