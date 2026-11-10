using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Генератор SplitMix64 (Steele, Lea, Flood; еталон — splitmix64.c Себастьяно Віньї).
    /// Лише цілочисельна арифметика за модулем 2^64, тож послідовність однакова
    /// на Mono, IL2CPP і .NET. Алгоритм зафіксовано «золотими» значеннями в тестах:
    /// його зміна ламає відтворюваність рівнів і бот-тесту.
    /// </summary>
    public sealed class SplitMix64Random : IRandom
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private const ulong MixA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixB = 0x94D049BB133111EBUL;

        /// <summary>Створює генератор; початковий стан дорівнює seed.</summary>
        public SplitMix64Random(ulong seed)
        {
            State = seed;
        }

        /// <summary>Повний внутрішній стан генератора (для знімка симуляції).</summary>
        public ulong State { get; private set; }

        /// <inheritdoc />
        public ulong NextUInt64()
        {
            unchecked
            {
                State += Gamma;
                ulong z = State;
                z = (z ^ (z >> 30)) * MixA;
                z = (z ^ (z >> 27)) * MixB;
                return z ^ (z >> 31);
            }
        }

        /// <inheritdoc />
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive), "Потрібно minInclusive < maxExclusive.");
            }

            ulong range = (ulong)((long)maxExclusive - minInclusive);

            // Відкидаємо перші (2^64 mod range) значень, щоб розподіл був рівномірним.
            ulong threshold = (ulong.MaxValue - range + 1UL) % range;
            ulong value;
            do
            {
                value = NextUInt64();
            }
            while (value < threshold);

            return (int)(minInclusive + (long)(value % range));
        }
    }
}
