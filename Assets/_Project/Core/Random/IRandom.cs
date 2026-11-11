namespace KebabStreet.Core
{
    /// <summary>
    /// Єдине джерело випадковості для симуляції. Реалізація детермінована:
    /// однаковий seed дає однакову послідовність на будь-якому рантаймі й пристрої.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Наступні 64 випадкові біти.</summary>
        ulong NextUInt64();

        /// <summary>
        /// Рівномірне ціле з проміжку [minInclusive, maxExclusive), без зсуву розподілу.
        /// </summary>
        int NextInt(int minInclusive, int maxExclusive);
    }
}
