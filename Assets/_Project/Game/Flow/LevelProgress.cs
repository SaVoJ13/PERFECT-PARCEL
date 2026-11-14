using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Рядок меню вибору рівня: номер, найкращі зірки (0 — не пройдено), чи відкритий.</summary>
    public readonly struct LevelEntry
    {
        public LevelEntry(int number, int bestStars, bool unlocked)
        {
            Number = number;
            BestStars = bestStars;
            Unlocked = unlocked;
        }

        public int Number { get; }

        public int BestStars { get; }

        public bool Unlocked { get; }
    }

    /// <summary>
    /// Прогрес рівнів (S1-10), без UnityEngine: рівень відкритий, якщо попередній у каталозі пройдено хоч на ★
    /// (перший рівень каталогу відкритий завжди). Зірки беруться зі збереження (<see cref="SaveData.LevelStars"/>).
    /// </summary>
    public static class LevelProgress
    {
        /// <summary>Найкращі зірки рівня зі збереження; 0, якщо його не пройдено.</summary>
        public static int BestStars(SaveData data, LevelDefinition level)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            int stars;
            return data.LevelStars.TryGetValue(level.Id, out stars) && stars > 0 ? stars : 0;
        }

        /// <summary>Рядки меню для всіх рівнів каталогу за порядком.</summary>
        public static LevelEntry[] Build(LevelCatalog levels, SaveData data)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            var entries = new LevelEntry[levels.Count];
            for (int i = 0; i < levels.Count; i++)
            {
                LevelDefinition level = levels.Get(i);
                bool unlocked = i == 0 || BestStars(data, levels.Get(i - 1)) >= 1;
                entries[i] = new LevelEntry(level.Number, BestStars(data, level), unlocked);
            }

            return entries;
        }

        /// <summary>Чи рівень з таким номером відкритий; false, якщо його немає.</summary>
        public static bool IsUnlocked(LevelCatalog levels, SaveData data, int number)
        {
            LevelEntry[] entries = Build(levels, data);
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Number == number)
                {
                    return entries[i].Unlocked;
                }
            }

            return false;
        }

        /// <summary>Номер наступного рівня каталогу після <paramref name="number"/>; 0, якщо це останній або рівня немає.</summary>
        public static int NextNumber(LevelCatalog levels, int number)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            for (int i = 0; i < levels.Count - 1; i++)
            {
                if (levels.Get(i).Number == number)
                {
                    return levels.Get(i + 1).Number;
                }
            }

            return 0;
        }
    }
}
