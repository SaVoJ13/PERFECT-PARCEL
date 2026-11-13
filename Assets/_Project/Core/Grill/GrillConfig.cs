using System.Collections.Generic;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Параметри гриля (stations.json → grill; GAME_DESIGN.md, розд. 3–4). Незмінний об'єкт.
    /// Тривалості задаються цілими мілісекундами (cookSec 2.5 → 2500) і переводяться
    /// в такти округленням до найближчого такту цілочисельною арифметикою.
    /// </summary>
    public sealed class GrillConfig
    {
        /// <summary>Найбільша кількість слотів гриля.</summary>
        public const int MaxSlots = 12;

        /// <summary>Найбільша тривалість приготування чи вікна до пригорання, мс.</summary>
        public const int MaxDurationMs = 600000;

        /// <summary>Найбільша тривалість однієї дії гравця на грилі, мс.</summary>
        public const int MaxActionMs = 60000;

        /// <summary>Найбільший штраф за пригоріле, монет.</summary>
        public const int MaxBurntPenaltyCoins = 1000000;

        private const int MsPerSecond = 1000;

        public GrillConfig(
            int slots,
            int cookMs,
            int burnAfterDoneMs,
            int warnBeforeBurnMs,
            int burntPenaltyCoins,
            int actionMs)
        {
            Slots = slots;
            CookMs = cookMs;
            BurnAfterDoneMs = burnAfterDoneMs;
            WarnBeforeBurnMs = warnBeforeBurnMs;
            BurntPenaltyCoins = burntPenaltyCoins;
            ActionMs = actionMs;
        }

        /// <summary>Кількість слотів м'яса (grill.slots).</summary>
        public int Slots { get; }

        /// <summary>Час приготування Raw → Done, мс (grill.cookSec).</summary>
        public int CookMs { get; }

        /// <summary>Час від готовності до пригорання Done → Burnt, мс (grill.burnAfterDoneSec).</summary>
        public int BurnAfterDoneMs { get; }

        /// <summary>За скільки до пригорання вмикається попередження, мс (grill.warnBeforeBurnSec); 0 — без попередження.</summary>
        public int WarnBeforeBurnMs { get; }

        /// <summary>
        /// Штраф монет за викинуте пригоріле м'ясо (grill.burntPenaltyCoins).
        /// 0 — без штрафу (навчальні рівні 1–2, ADR-020): це значення конфіга, а не гілка коду.
        /// </summary>
        public int BurntPenaltyCoins { get; }

        /// <summary>Тривалість однієї дії гравця на грилі, мс (stations.grill.actionTimeSec); 0 — миттєво.</summary>
        public int ActionMs { get; }

        /// <summary>Час приготування в тактах.</summary>
        public long CookTicks(int ticksPerSecond)
        {
            return ToTicks(CookMs, ticksPerSecond);
        }

        /// <summary>Вікно від готовності до пригорання в тактах.</summary>
        public long BurnAfterDoneTicks(int ticksPerSecond)
        {
            return ToTicks(BurnAfterDoneMs, ticksPerSecond);
        }

        /// <summary>Час попередження перед пригоранням у тактах.</summary>
        public long WarnBeforeBurnTicks(int ticksPerSecond)
        {
            return ToTicks(WarnBeforeBurnMs, ticksPerSecond);
        }

        /// <summary>Тривалість дії гравця в тактах.</summary>
        public long ActionTicks(int ticksPerSecond)
        {
            return ToTicks(ActionMs, ticksPerSecond);
        }

        /// <summary>
        /// Перевіряє параметри гриля для заданої частоти тактів. Порожній список —
        /// параметри придатні; інакше кожен рядок називає поле, значення і дозволені межі.
        /// </summary>
        public IReadOnlyList<string> GetErrors(int ticksPerSecond)
        {
            var errors = new List<string>();
            CheckRange(errors, "grill.slots", Slots, 1, MaxSlots);
            bool cookOk = CheckRange(errors, "grill.cookMs", CookMs, 1, MaxDurationMs);
            bool burnOk = CheckRange(errors, "grill.burnAfterDoneMs", BurnAfterDoneMs, 1, MaxDurationMs);
            bool warnOk = CheckRange(errors, "grill.warnBeforeBurnMs", WarnBeforeBurnMs, 0, MaxDurationMs);
            CheckRange(errors, "grill.burntPenaltyCoins", BurntPenaltyCoins, 0, MaxBurntPenaltyCoins);
            CheckRange(errors, "grill.actionMs", ActionMs, 0, MaxActionMs);

            if (burnOk && warnOk && WarnBeforeBurnMs > BurnAfterDoneMs)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "grill.warnBeforeBurnMs={0}: не більше за grill.burnAfterDoneMs={1}",
                    WarnBeforeBurnMs,
                    BurnAfterDoneMs));
            }

            bool rateOk = ticksPerSecond >= ShiftConfig.MinTicksPerSecond
                && ticksPerSecond <= ShiftConfig.MaxTicksPerSecond;
            if (rateOk && cookOk)
            {
                CheckAtLeastOneTick(errors, "grill.cookMs", CookMs, ticksPerSecond);
            }

            if (rateOk && burnOk)
            {
                CheckAtLeastOneTick(errors, "grill.burnAfterDoneMs", BurnAfterDoneMs, ticksPerSecond);
            }

            return errors;
        }

        private static long ToTicks(int ms, int ticksPerSecond)
        {
            // Округлення до найближчого такту, половина — вгору; лише цілі числа.
            return ((long)ms * ticksPerSecond + MsPerSecond / 2) / MsPerSecond;
        }

        private static bool CheckRange(List<string> errors, string field, int value, int min, int max)
        {
            if (value >= min && value <= max)
            {
                return true;
            }

            errors.Add(string.Format(
                CultureInfo.InvariantCulture, "{0}={1}: дозволено {2}..{3}", field, value, min, max));
            return false;
        }

        private static void CheckAtLeastOneTick(List<string> errors, string field, int ms, int ticksPerSecond)
        {
            if (ToTicks(ms, ticksPerSecond) < 1L)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}={1}: менше одного такту при ticksPerSecond={2}",
                    field,
                    ms,
                    ticksPerSecond));
            }
        }
    }
}
