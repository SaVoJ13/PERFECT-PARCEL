using System.Globalization;
using KebabStreet.Core;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Чим закінчилась гра бота.</summary>
    internal enum BotOutcome
    {
        /// <summary>Ціль виконано.</summary>
        Won = 1,

        /// <summary>Час вийшов, ціль не виконано.</summary>
        TimeUp = 2,

        /// <summary>Втрачено більше клієнтів, ніж дозволяє рівень.</summary>
        TooManyLost = 3,
    }

    /// <summary>
    /// Результат однієї гри бота: підсумок і метрики для балансу. Значення метрик — на кінець
    /// зміни (бот грає до кінця таймера, якщо не програв раніше), окрім такту виконання цілі.
    /// </summary>
    internal sealed class BotResult
    {
        public string LevelId;
        public ulong Seed;
        public string Profile;
        public BotOutcome Outcome;
        public GoalType GoalType;
        public int GoalTarget;
        public int? MaxLost;
        public int TicksPerSecond;
        public long DurationTicks;

        /// <summary>Такт, на якому виконано ціль; -1, якщо ні.</summary>
        public long GoalTick;

        /// <summary>Такт, на якому гра бота закінчилась (кінець таймера або провал за втратами).</summary>
        public long EndTick;

        public long Served;
        public long Coins;
        public long Lost;
        public long Left;
        public long WrongDishes;
        public long Burnt;
        public long MaxCombo;
        public long Mistakes;
        public long ExecutedActions;
        public long IgnoredActions;

        /// <summary>Результат зміни за спільними правилами Core (ShiftEvaluator): зірки, причина поразки.</summary>
        public ShiftResult Result;

        /// <summary>Повний знімок симуляції на кінці гри (для перевірки детермінізму).</summary>
        public ShiftSnapshot FinalSnapshot;

        public bool Won
        {
            get { return Outcome == BotOutcome.Won; }
        }

        /// <summary>Скільки секунд лишалось до кінця таймера, коли ціль виконано; 0, якщо не виконано.</summary>
        public double LeftoverSeconds
        {
            get
            {
                if (!Won)
                {
                    return 0d;
                }

                return (double)(DurationTicks - GoalTick) / TicksPerSecond;
            }
        }

        /// <summary>Секунда зміни, на якій виконано ціль; -1, якщо не виконано.</summary>
        public double GoalSeconds
        {
            get { return Won ? (double)GoalTick / TicksPerSecond : -1d; }
        }

        /// <summary>Усі числа результату одним рядком: два однакові запуски мають давати однакові рядки.</summary>
        public string Fingerprint()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/{2}: {3} goalTick={4} endTick={5} served={6} coins={7} lost={8} left={9} wrong={10} burnt={11} combo={12} mistakes={13} done={14} ignored={15}",
                LevelId,
                Seed,
                Profile,
                Outcome,
                GoalTick,
                EndTick,
                Served,
                Coins,
                Lost,
                Left,
                WrongDishes,
                Burnt,
                MaxCombo,
                Mistakes,
                ExecutedActions,
                IgnoredActions);
        }

        /// <summary>Зрозуміле пояснення результату: скільки обслужено і чому програш.</summary>
        public string Explain()
        {
            string goal = GoalType == GoalType.Serve
                ? "обслужити " + GoalTarget.ToString(CultureInfo.InvariantCulture)
                : "заробити " + GoalTarget.ToString(CultureInfo.InvariantCulture) + " монет";
            string head = LevelId + " seed " + Seed.ToString(CultureInfo.InvariantCulture) + " [" + Profile + "]: ";
            string reason;
            switch (Outcome)
            {
                case BotOutcome.Won:
                    reason = "ПЕРЕМОГА, ціль (" + goal + ") на "
                        + GoalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " с";
                    break;
                case BotOutcome.TooManyLost:
                    reason = "ПРОГРАШ: втрачено клієнтів " + Lost.ToString(CultureInfo.InvariantCulture)
                        + ", дозволено не більше " + (MaxLost.HasValue
                            ? MaxLost.Value.ToString(CultureInfo.InvariantCulture)
                            : "-");
                    break;
                default:
                    reason = "ПРОГРАШ: час вийшов, ціль (" + goal + ") не виконано";
                    break;
            }

            return head + reason
                + "; обслужено " + Served.ToString(CultureInfo.InvariantCulture)
                + ", монет " + Coins.ToString(CultureInfo.InvariantCulture)
                + ", втрачено " + Lost.ToString(CultureInfo.InvariantCulture)
                + " (пішло " + Left.ToString(CultureInfo.InvariantCulture) + ")"
                + ", хибних страв " + WrongDishes.ToString(CultureInfo.InvariantCulture)
                + ", пригоріло " + Burnt.ToString(CultureInfo.InvariantCulture);
        }
    }
}
