using KebabStreet.Core;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Проста локальна перевірка цілі рівня для бота (підрахунок зірок і екран результату — S1-06):
    /// «обслужити N» — ServedCount не менший за N, «заробити N» — CoinsEarned не менший за N;
    /// якщо в рівні є ліміт втрат — поразка, щойно втрачено більше за ліміт. Результат «защіпається»
    /// на першому такті виконання, тому бот-тест знає, коли саме ціль досягнуто.
    /// </summary>
    internal sealed class BotGoal : IShiftGoal
    {
        private readonly LevelGoal _goal;
        private readonly int? _maxLost;

        public BotGoal(LevelGoal goal, LevelLose lose)
        {
            _goal = goal;
            _maxLost = lose.MaxLostCustomers;
            AchievedTick = -1L;
            FailedTick = -1L;
        }

        /// <summary>Ціль виконано (до того, як вичерпано ліміт втрат).</summary>
        public bool Achieved { get; private set; }

        /// <summary>Втрачено більше клієнтів, ніж дозволено, ще до виконання цілі.</summary>
        public bool Failed { get; private set; }

        /// <summary>Такт зміни, на якому ціль виконано; -1 — ще ні.</summary>
        public long AchievedTick { get; private set; }

        /// <summary>Такт, на якому вичерпано ліміт втрат; -1 — ще ні.</summary>
        public long FailedTick { get; private set; }

        /// <summary>Оглядає стан зміни після такту; перше виконання або провал зберігається.</summary>
        public void Observe(ShiftSimulation shift)
        {
            if (Achieved || Failed)
            {
                return;
            }

            if (_maxLost.HasValue && shift.LostCustomers > _maxLost.Value)
            {
                Failed = true;
                FailedTick = shift.ElapsedTicks;
                return;
            }

            if (IsReached(shift))
            {
                Achieved = true;
                AchievedTick = shift.ElapsedTicks;
            }
        }

        /// <inheritdoc />
        public bool IsAchieved(ShiftSimulation shift)
        {
            Observe(shift);
            return Achieved;
        }

        private bool IsReached(ShiftSimulation shift)
        {
            if (_goal.Type == GoalType.Serve)
            {
                return shift.ServedCount >= _goal.Target;
            }

            return shift.CoinsEarned >= _goal.Target;
        }
    }
}
