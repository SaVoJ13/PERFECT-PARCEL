using KebabStreet.Core;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Спільні заготовки для тестів симуляції зміни (значення — лише для тестів).</summary>
    internal static class ShiftTestKit
    {
        public const int TicksPerSecond = 60;
        public const int MaxCatchUpTicks = 600;
        public const int DurationSec = 10;
        public const int ContinueBonusSec = 30;
        public const ulong Seed = 20261110UL;

        public static ShiftConfig Config(
            int durationSec = DurationSec,
            int ticksPerSecond = TicksPerSecond,
            int maxCatchUpTicks = MaxCatchUpTicks,
            int continueBonusSec = ContinueBonusSec)
        {
            return new ShiftConfig(ticksPerSecond, maxCatchUpTicks, durationSec, continueBonusSec);
        }

        /// <summary>Нова зміна в стані Intro з ціллю, що завжди дає заданий результат.</summary>
        public static ShiftSimulation Create(bool goalAchieved = true, ShiftConfig config = null, ulong seed = Seed)
        {
            return new ShiftSimulation(config ?? Config(), seed, new FixedGoal(goalAchieved));
        }

        /// <summary>Нова зміна, вже переведена в Running.</summary>
        public static ShiftSimulation CreateRunning(bool goalAchieved = true, ShiftConfig config = null, ulong seed = Seed)
        {
            ShiftSimulation shift = Create(goalAchieved, config, seed);
            shift.Start();
            return shift;
        }

        /// <summary>Виконує такти, доки зміна в Running; повертає кількість виконаних.</summary>
        public static long TickUntilNotRunning(ShiftSimulation shift)
        {
            long ticks = 0;
            while (shift.Tick())
            {
                ticks++;
            }

            return ticks;
        }
    }

    /// <summary>Ціль із наперед заданим результатом; запам'ятовує, як її викликали.</summary>
    internal sealed class FixedGoal : IShiftGoal
    {
        public FixedGoal(bool achieved)
        {
            Achieved = achieved;
        }

        public bool Achieved { get; set; }

        public int Calls { get; private set; }

        public ShiftState StateSeen { get; private set; }

        public long RemainingTicksSeen { get; private set; }

        public bool IsAchieved(ShiftSimulation shift)
        {
            Calls++;
            StateSeen = shift.State;
            RemainingTicksSeen = shift.RemainingTicks;
            return Achieved;
        }
    }

    /// <summary>Ціль, результат якої залежить лише від генератора зміни.</summary>
    internal sealed class RandomGoal : IShiftGoal
    {
        public bool IsAchieved(ShiftSimulation shift)
        {
            return shift.Random.NextInt(0, 2) == 0;
        }
    }
}
