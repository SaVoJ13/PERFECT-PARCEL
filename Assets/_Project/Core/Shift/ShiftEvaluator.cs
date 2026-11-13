using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Оцінює зміну за даними рівня (goal, stars, lose): перемога/поразка, зірки, причина
    /// поразки, нагорода. Без стану: той самий об'єкт можна передати в кілька змін.
    /// Правила:
    /// ціль serve — ServedCount не менший за N; ціль coins — CoinsEarned не менший за N;
    /// межа поразки (втрачено/пригоріло/хибних страв більше за ліміт) = поразка негайно;
    /// ★ ціль, ★★ ще й coinsEarned не менший за stars.two, ★★★ ще й умова stars.three;
    /// причина поразки за пріоритетом: customersLeft, burnt, wrongDishes, інакше tooSlow.
    /// </summary>
    public sealed class ShiftEvaluator : IShiftGoal, IShiftEarlyEnd
    {
        private readonly LevelGoal _goal;
        private readonly LevelStars _stars;
        private readonly LevelLose _lose;

        public ShiftEvaluator(LevelGoal goal, LevelStars stars, LevelLose lose)
        {
            _goal = goal ?? throw new ArgumentNullException(nameof(goal));
            _stars = stars ?? throw new ArgumentNullException(nameof(stars));
            _lose = lose ?? throw new ArgumentNullException(nameof(lose));
        }

        /// <summary>Оцінювач для рівня з конфігів.</summary>
        public static ShiftEvaluator ForLevel(LevelDefinition level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            return new ShiftEvaluator(level.Goal, level.Stars, level.Lose);
        }

        /// <summary>Чи виконана ціль рівня за поточними лічильниками (межі поразки не враховуються).</summary>
        public bool IsGoalReached(ShiftSimulation shift)
        {
            return IsGoalReached(shift.CaptureCounters());
        }

        public bool IsGoalReached(ShiftCounters counters)
        {
            return Progress(counters) >= _goal.Target;
        }

        /// <summary>Перша порушена межа поразки (ключ lost.reason.*) або null.</summary>
        public string FindLimitBreach(ShiftCounters counters)
        {
            if (_lose.MaxLostCustomers.HasValue && counters.LostCustomers > _lose.MaxLostCustomers.Value)
            {
                return LoseReasons.CustomersLeft;
            }

            if (_lose.MaxBurnt.HasValue && counters.BurntCount > _lose.MaxBurnt.Value)
            {
                return LoseReasons.Burnt;
            }

            if (_lose.MaxWrongDishes.HasValue && counters.WrongDishes > _lose.MaxWrongDishes.Value)
            {
                return LoseReasons.WrongDishes;
            }

            return null;
        }

        /// <summary>Результат, якби зміна завершилась зараз.</summary>
        public ShiftResult Evaluate(ShiftSimulation shift)
        {
            if (shift == null)
            {
                throw new ArgumentNullException(nameof(shift));
            }

            return Evaluate(shift.CaptureCounters());
        }

        /// <summary>Результат за лічильниками (чиста функція).</summary>
        public ShiftResult Evaluate(ShiftCounters c)
        {
            string breach = FindLimitBreach(c);
            if (breach == null && IsGoalReached(c))
            {
                return new ShiftResult(
                    ShiftOutcome.Won,
                    CountStars(c),
                    null,
                    0L,
                    c.CoinsEarned,
                    c.CoinsEarned,
                    c.ServedCount,
                    c.LostCustomers,
                    c.WrongDishes,
                    c.BurntCount,
                    c.MaxCombo);
            }

            return new ShiftResult(
                ShiftOutcome.Lost,
                0,
                breach ?? LoseReasons.TooSlow,
                Math.Max(0L, _goal.Target - Progress(c)),
                0L,
                c.CoinsEarned,
                c.ServedCount,
                c.LostCustomers,
                c.WrongDishes,
                c.BurntCount,
                c.MaxCombo);
        }

        /// <inheritdoc />
        public bool IsAchieved(ShiftSimulation shift)
        {
            return Evaluate(shift).Won;
        }

        /// <summary>
        /// Дострокове завершення: межа поразки порушена (негайна поразка) або ціль виконана
        /// при goal.endOnGoal (для «обслужити N» — на N-му обслуженому клієнті).
        /// </summary>
        public bool ShouldEndEarly(ShiftSimulation shift)
        {
            return ShouldEndEarly(shift.CaptureCounters());
        }

        public bool ShouldEndEarly(ShiftCounters counters)
        {
            if (FindLimitBreach(counters) != null)
            {
                return true;
            }

            return _goal.EndOnGoal && IsGoalReached(counters);
        }

        private long Progress(ShiftCounters c)
        {
            return _goal.Type == GoalType.Serve ? c.ServedCount : c.CoinsEarned;
        }

        private int CountStars(ShiftCounters c)
        {
            if (c.CoinsEarned < _stars.TwoCoins)
            {
                return 1;
            }

            return IsThreeStarConditionMet(c) ? 3 : 2;
        }

        private bool IsThreeStarConditionMet(ShiftCounters c)
        {
            switch (_stars.ThreeCondition)
            {
                case StarCondition.NoLostCustomers:
                    return c.LostCustomers == 0L;
                case StarCondition.NoBurnt:
                    return c.BurntCount == 0L;
                case StarCondition.NoWrongDishes:
                    return c.WrongDishes == 0L;
                case StarCondition.Combo:
                    return c.MaxCombo >= _stars.ThreeComboValue;
                default:
                    return false;
            }
        }
    }
}
