using System;
using System.Collections.Generic;

namespace KebabStreet.Core
{
    /// <summary>Арифметика монет у цілих числах: частки задаються відсотками (150 = ×1,5).</summary>
    internal static class CoinMath
    {
        /// <summary>Скільки відсотків у цілому.</summary>
        public const int PercentBase = 100;

        /// <summary>Відсоток від суми, округлення до найближчої монети, половина — вгору.</summary>
        public static long Percent(long coins, long percent)
        {
            return (coins * percent + PercentBase / 2) / PercentBase;
        }
    }

    /// <summary>
    /// Правило чайових (customers[id].tipRule; GAME_DESIGN.md, розд. 6): чайові залежать від
    /// залишку терпіння. Чайові = плата за страву × MaxPercent × (залишок терпіння / усе терпіння);
    /// якщо залишок менший за MinPatiencePercent від усього терпіння — чайових немає.
    /// Незмінний об'єкт; числа — лише з конфіга.
    /// </summary>
    public sealed class TipRule
    {
        public TipRule(int maxPercent, int minPatiencePercent)
        {
            MaxPercent = maxPercent;
            MinPatiencePercent = minPatiencePercent;
        }

        /// <summary>Найбільші чайові (за повного терпіння), відсотків від плати за страву; 0 — без чайових.</summary>
        public int MaxPercent { get; }

        /// <summary>Найменший залишок терпіння, за якого ще дають чайові, відсотків; 0 — завжди.</summary>
        public int MinPatiencePercent { get; }

        /// <summary>Чайові в монетах (округлення до найближчої монети, половина — вгору).</summary>
        public long TipCoins(long payCoins, long patienceLeftTicks, long patienceTicks)
        {
            if (MaxPercent <= 0 || payCoins <= 0L || patienceTicks <= 0L || patienceLeftTicks <= 0L)
            {
                return 0L;
            }

            if (patienceLeftTicks > patienceTicks)
            {
                patienceLeftTicks = patienceTicks;
            }

            if (patienceLeftTicks * CoinMath.PercentBase < MinPatiencePercent * patienceTicks)
            {
                return 0L;
            }

            long divisor = CoinMath.PercentBase * patienceTicks;
            return (payCoins * MaxPercent * patienceLeftTicks + divisor / 2) / divisor;
        }
    }

    /// <summary>
    /// Тип клієнта (customers.json → customers[id]; GAME_DESIGN.md, розд. 6): терпіння, множник
    /// плати, правило чайових і особливість. Особливість задається даними, а не гілками коду:
    /// «завжди просить» (Марко — гострий соус) і «не кладіть» (Ганна — без цибулі).
    /// Незмінний об'єкт; інгредієнти названі так, як у конфігу.
    /// </summary>
    public sealed class CustomerType
    {
        private readonly string[] _always;
        private readonly string[] _exclude;

        /// <summary>Списки копіюються; null означає порожній список.</summary>
        public CustomerType(
            string id,
            int patienceMs,
            int payPercent,
            TipRule tip,
            IEnumerable<string> always,
            IEnumerable<string> exclude)
            : this(id, null, patienceMs, payPercent, tip, always, exclude)
        {
        }

        /// <summary>Списки копіюються; null означає порожній список. nameKey — ключ локалізації імені (лише для показу).</summary>
        public CustomerType(
            string id,
            string nameKey,
            int patienceMs,
            int payPercent,
            TipRule tip,
            IEnumerable<string> always,
            IEnumerable<string> exclude)
        {
            Id = id;
            NameKey = nameKey;
            PatienceMs = patienceMs;
            PayPercent = payPercent;
            Tip = tip;
            _always = always == null ? new string[0] : new List<string>(always).ToArray();
            _exclude = exclude == null ? new string[0] : new List<string>(exclude).ToArray();
        }

        /// <summary>Назва в конфігах («olya», «marko», «hanna»).</summary>
        public string Id { get; }

        /// <summary>
        /// Ключ локалізації імені (customers[id].nameKey); null — не задано (тести без конфіга).
        /// Лише для показу: на правила гри не впливає.
        /// </summary>
        public string NameKey { get; }

        /// <summary>Терпіння в слоті прилавка, мс (customers[id].patienceSec).</summary>
        public int PatienceMs { get; }

        /// <summary>Множник плати за страву, відсотків (customers[id].payMultiplier 1.5 → 150).</summary>
        public int PayPercent { get; }

        /// <summary>Правило чайових.</summary>
        public TipRule Tip { get; }

        /// <summary>Скільки необов'язкових інгредієнтів цей тип просить завжди.</summary>
        public int AlwaysCount => _always.Length;

        /// <summary>Скільки інгредієнтів цей тип просить не класти.</summary>
        public int ExcludeCount => _exclude.Length;

        /// <summary>Інгредієнт, який цей тип просить завжди (0..AlwaysCount-1).</summary>
        public string GetAlways(int index)
        {
            if (index < 0 || index >= _always.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _always[index];
        }

        /// <summary>Інгредієнт, який цей тип просить не класти (0..ExcludeCount-1).</summary>
        public string GetExclude(int index)
        {
            if (index < 0 || index >= _exclude.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _exclude[index];
        }

        /// <summary>Терпіння в тактах (округлення до найближчого такту).</summary>
        public long PatienceTicks(int ticksPerSecond)
        {
            return TickTime.FromMs(PatienceMs, ticksPerSecond);
        }
    }
}
