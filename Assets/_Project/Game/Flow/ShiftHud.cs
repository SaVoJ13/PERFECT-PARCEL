using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Дані верхньої смуги екрана зміни (S1-09a): таймер, ціль, монети, пауза. Лише значення.</summary>
    public readonly struct ShiftHudView : IEquatable<ShiftHudView>
    {
        public ShiftHudView(
            long remainingSeconds,
            GoalType goalType,
            long goalProgress,
            long goalTarget,
            long coins,
            bool paused,
            bool goalDoneStarHint,
            bool finished)
        {
            RemainingSeconds = remainingSeconds;
            GoalType = goalType;
            GoalProgress = goalProgress;
            GoalTarget = goalTarget;
            Coins = coins;
            Paused = paused;
            GoalDoneStarHint = goalDoneStarHint;
            Finished = finished;
        }

        /// <summary>Секунди до кінця зміни (округлення вгору).</summary>
        public long RemainingSeconds { get; }

        /// <summary>Тип цілі рівня: від нього залежить напис.</summary>
        public GoalType GoalType { get; }

        /// <summary>Скільки вже зроблено для цілі (клієнтів або монет).</summary>
        public long GoalProgress { get; }

        /// <summary>Скільки треба (goal.count або goal.coins рівня).</summary>
        public long GoalTarget { get; }

        /// <summary>Монети, зароблені за зміну.</summary>
        public long Coins { get; }

        /// <summary>Гра на паузі: показується плашка паузи.</summary>
        public bool Paused { get; }

        /// <summary>Показати підказку shift.goal_done_star2.</summary>
        public bool GoalDoneStarHint { get; }

        /// <summary>Зміна завершилась: показується підсумок.</summary>
        public bool Finished { get; }

        /// <summary>Ціль виконано (для показу; перемогу визначає Core).</summary>
        public bool GoalReached => GoalProgress >= GoalTarget;

        public bool Equals(ShiftHudView other)
        {
            return RemainingSeconds == other.RemainingSeconds
                && GoalType == other.GoalType
                && GoalProgress == other.GoalProgress
                && GoalTarget == other.GoalTarget
                && Coins == other.Coins
                && Paused == other.Paused
                && GoalDoneStarHint == other.GoalDoneStarHint
                && Finished == other.Finished;
        }

        public override bool Equals(object obj)
        {
            return obj is ShiftHudView other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RemainingSeconds.GetHashCode();
                hash = (hash * 397) ^ (int)GoalType;
                hash = (hash * 397) ^ GoalProgress.GetHashCode();
                hash = (hash * 397) ^ GoalTarget.GetHashCode();
                hash = (hash * 397) ^ Coins.GetHashCode();
                hash = (hash * 397) ^ (Paused ? 1 : 0);
                hash = (hash * 397) ^ (GoalDoneStarHint ? 1 : 0);
                hash = (hash * 397) ^ (Finished ? 1 : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// Модель вигляду верхньої смуги (S1-09a): драйвер зміни → дані для показу. Без UnityEngine
    /// і без правил гри: числа беруться з лічильників Core і цілі рівня.
    /// </summary>
    public static class ShiftHud
    {
        public static ShiftHudView FromDriver(ShiftDriver driver)
        {
            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            ShiftSimulation simulation = driver.Simulation;
            LevelGoal goal = driver.Level.Goal;
            long coins = simulation.CoinsEarned;
            return new ShiftHudView(
                EmptyShift.RemainingSeconds(simulation),
                goal.Type,
                goal.Type == GoalType.Coins ? coins : simulation.ServedCount,
                goal.Target,
                coins,
                driver.IsPaused,
                driver.GoalDoneStarHint,
                driver.IsFinished);
        }

        /// <summary>Ключ локалізації напису цілі; шаблон має {0} — зроблено, {1} — треба.</summary>
        public static string GoalTextKey(GoalType type)
        {
            return type == GoalType.Coins ? TextKeys.ShiftHudGoalCoins : TextKeys.ShiftHudGoalServe;
        }
    }
}
