using System.Collections.Generic;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Параметри симуляції зміни. Незмінний об'єкт: значення приходять із конфігів,
    /// у логіці немає власних чисел. Усі тривалості переводяться в цілі такти.
    /// </summary>
    public sealed class ShiftConfig
    {
        /// <summary>Найменша допустима частота тактів (тактів за секунду).</summary>
        public const int MinTicksPerSecond = 10;

        /// <summary>Найбільша допустима частота тактів (тактів за секунду).</summary>
        public const int MaxTicksPerSecond = 240;

        /// <summary>Найбільша тривалість зміни, секунд.</summary>
        public const int MaxDurationSec = 3600;

        /// <summary>Найбільша межа наздоганяння, тактів за один виклик Advance.</summary>
        public const int MaxCatchUpTicksLimit = 2400;

        /// <summary>Найбільший додатковий час продовження, секунд.</summary>
        public const int MaxContinueBonusSec = 600;

        /// <summary>Найбільша довжина черги дій гравця.</summary>
        public const int MaxActionQueue = 32;

        /// <summary>Зміна без станцій і без черги дій (лише таймер).</summary>
        public ShiftConfig(int ticksPerSecond, int maxCatchUpTicks, int durationSec, int continueBonusSec)
            : this(ticksPerSecond, maxCatchUpTicks, durationSec, continueBonusSec, 0, null)
        {
        }

        /// <summary>Зміна з чергою дій і грилем; grill = null — зміна без гриля.</summary>
        public ShiftConfig(
            int ticksPerSecond,
            int maxCatchUpTicks,
            int durationSec,
            int continueBonusSec,
            int actionQueueMax,
            GrillConfig grill)
        {
            TicksPerSecond = ticksPerSecond;
            MaxCatchUpTicks = maxCatchUpTicks;
            DurationSec = durationSec;
            ContinueBonusSec = continueBonusSec;
            ActionQueueMax = actionQueueMax;
            Grill = grill;
        }

        /// <summary>Частота тактів симуляції, тактів за секунду.</summary>
        public int TicksPerSecond { get; }

        /// <summary>Скільки тактів щонайбільше виконує один виклик Advance; надлишок часу відкидається.</summary>
        public int MaxCatchUpTicks { get; }

        /// <summary>Тривалість зміни, секунд (level.durationSec).</summary>
        public int DurationSec { get; }

        /// <summary>Додатковий час продовження після поразки, секунд; 0 — продовження вимкнено.</summary>
        public int ContinueBonusSec { get; }

        /// <summary>
        /// Скільки дій гравця вміщує черга разом із тією, що виконується (rules.actionQueueMax);
        /// 0 — команди станцій не приймаються.
        /// </summary>
        public int ActionQueueMax { get; }

        /// <summary>Параметри гриля; null — у зміні немає гриля.</summary>
        public GrillConfig Grill { get; }

        /// <summary>Тривалість зміни в тактах.</summary>
        public long DurationTicks => (long)DurationSec * TicksPerSecond;

        /// <summary>Додатковий час продовження в тактах.</summary>
        public long ContinueBonusTicks => (long)ContinueBonusSec * TicksPerSecond;

        /// <summary>
        /// Перевіряє конфігурацію. Порожній список — конфігурація придатна;
        /// інакше кожен рядок називає поле, його значення і дозволені межі.
        /// </summary>
        public IReadOnlyList<string> GetErrors()
        {
            var errors = new List<string>();
            CheckRange(errors, "ticksPerSecond", TicksPerSecond, MinTicksPerSecond, MaxTicksPerSecond);
            CheckRange(errors, "maxCatchUpTicks", MaxCatchUpTicks, 1, MaxCatchUpTicksLimit);
            CheckRange(errors, "durationSec", DurationSec, 1, MaxDurationSec);
            CheckRange(errors, "continueBonusSec", ContinueBonusSec, 0, MaxContinueBonusSec);
            CheckRange(errors, "actionQueueMax", ActionQueueMax, Grill == null ? 0 : 1, MaxActionQueue);
            if (Grill != null)
            {
                errors.AddRange(Grill.GetErrors(TicksPerSecond));
            }

            return errors;
        }

        private static void CheckRange(List<string> errors, string field, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture, "{0}={1}: дозволено {2}..{3}", field, value, min, max));
            }
        }
    }
}
