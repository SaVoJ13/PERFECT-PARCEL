using System;
using System.Collections.Generic;

namespace KebabStreet.Core
{
    /// <summary>
    /// Замовлення (levels/Lxx.json → waves[].order): рецепт, необов'язкові інгредієнти, які
    /// клієнт просить, і ті, які просить не класти (request.exclude). Незмінний об'єкт;
    /// рецепт та інгредієнти названі так, як у конфігу.
    /// </summary>
    public sealed class Order
    {
        private readonly string[] _optional;
        private readonly string[] _exclude;

        /// <summary>Списки копіюються; null означає порожній список.</summary>
        public Order(string recipe, IEnumerable<string> optional, IEnumerable<string> exclude)
        {
            Recipe = recipe;
            _optional = optional == null ? new string[0] : new List<string>(optional).ToArray();
            _exclude = exclude == null ? new string[0] : new List<string>(exclude).ToArray();
        }

        /// <summary>Назва рецепта.</summary>
        public string Recipe { get; }

        /// <summary>Скільки необов'язкових інгредієнтів просить клієнт.</summary>
        public int OptionalCount => _optional.Length;

        /// <summary>Скільки інгредієнтів клієнт просить не класти.</summary>
        public int ExcludeCount => _exclude.Length;

        /// <summary>Необов'язковий інгредієнт, який просить клієнт (0..OptionalCount-1).</summary>
        public string GetOptional(int index)
        {
            if (index < 0 || index >= _optional.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _optional[index];
        }

        /// <summary>Інгредієнт, який клієнт просить не класти (0..ExcludeCount-1).</summary>
        public string GetExclude(int index)
        {
            if (index < 0 || index >= _exclude.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _exclude[index];
        }
    }

    /// <summary>
    /// Один прихід клієнта (levels/Lxx.json → waves[]): коли, хто і що замовляє. JitterMs —
    /// випадкове запізнення від 0 до цього значення (береться з генератора зміни; 0 — точно
    /// за розкладом). GraceMs — скільки часу в слоті терпіння ще не спадає. Незмінний об'єкт.
    /// </summary>
    public sealed class Arrival
    {
        /// <summary>Прихід точно за розкладом, терпіння спадає одразу.</summary>
        public Arrival(int atMs, string customer, Order order)
            : this(atMs, customer, order, 0, 0)
        {
        }

        public Arrival(int atMs, string customer, Order order, int jitterMs, int graceMs)
        {
            AtMs = atMs;
            Customer = customer;
            Order = order;
            JitterMs = jitterMs;
            GraceMs = graceMs;
        }

        /// <summary>Час приходу від початку зміни, мс (waves[].atSec).</summary>
        public int AtMs { get; }

        /// <summary>Назва типу клієнта (waves[].customer).</summary>
        public string Customer { get; }

        /// <summary>Замовлення.</summary>
        public Order Order { get; }

        /// <summary>Найбільше випадкове запізнення, мс; 0 — без випадковості.</summary>
        public int JitterMs { get; }

        /// <summary>Скільки часу після того, як клієнт став у слот, терпіння не спадає, мс.</summary>
        public int GraceMs { get; }
    }

    /// <summary>
    /// Розклад приходу клієнтів на зміну. Не знає ні Unity, ні системного часу: час — лише
    /// такти, випадковість — лише переданий генератор. Незмінний об'єкт.
    /// </summary>
    public sealed class ArrivalSchedule
    {
        /// <summary>Найбільша кількість приходів за зміну.</summary>
        public const int MaxArrivals = 512;

        /// <summary>Найпізніший час приходу, мс (найдовша зміна разом із продовженням).</summary>
        public const int MaxAtMs = (ShiftConfig.MaxDurationSec + ShiftConfig.MaxContinueBonusSec) * 1000;

        /// <summary>Найбільше випадкове запізнення, мс.</summary>
        public const int MaxJitterMs = 600000;

        private readonly Arrival[] _arrivals;

        /// <summary>Список копіюється; null означає порожній розклад (це помилка конфіга).</summary>
        public ArrivalSchedule(IEnumerable<Arrival> arrivals)
        {
            _arrivals = arrivals == null ? new Arrival[0] : new List<Arrival>(arrivals).ToArray();
        }

        /// <summary>Кількість приходів.</summary>
        public int Count => _arrivals.Length;

        /// <summary>Прихід за номером у розкладі (0..Count-1).</summary>
        public Arrival Get(int index)
        {
            if (index < 0 || index >= _arrivals.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _arrivals[index];
        }

        /// <summary>
        /// Такт приходу для кожного запису розкладу: час із конфіга плюс випадкове запізнення
        /// 0..JitterMs. Генератор використовується лише для записів із запізненням, по одному
        /// числу на запис, у порядку розкладу — тому однаковий seed дає однаковий результат.
        /// </summary>
        public long[] ResolveTicks(int ticksPerSecond, IRandom random)
        {
            var ticks = new long[_arrivals.Length];
            for (int i = 0; i < _arrivals.Length; i++)
            {
                Arrival arrival = _arrivals[i];
                long tick = TickTime.FromMs(arrival.AtMs, ticksPerSecond);
                long jitterTicks = TickTime.FromMs(arrival.JitterMs, ticksPerSecond);
                if (jitterTicks > 0L)
                {
                    if (random == null)
                    {
                        throw new ArgumentNullException(nameof(random));
                    }

                    tick += random.NextInt(0, (int)jitterTicks + 1);
                }

                ticks[i] = tick;
            }

            return ticks;
        }

        /// <summary>
        /// Порядок приходу: номери записів, упорядковані за тактом; за рівного такту раніше
        /// йде той, хто раніше в розкладі.
        /// </summary>
        public static int[] OrderByTick(long[] ticks)
        {
            if (ticks == null)
            {
                throw new ArgumentNullException(nameof(ticks));
            }

            var order = new int[ticks.Length];
            for (int i = 0; i < order.Length; i++)
            {
                int place = i;
                while (place > 0 && ticks[order[place - 1]] > ticks[i])
                {
                    order[place] = order[place - 1];
                    place--;
                }

                order[place] = i;
            }

            return order;
        }
    }
}
