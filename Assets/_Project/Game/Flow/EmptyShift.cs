using System.Globalization;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Порожня зміна каркаса E0-05: лише таймер. ТИМЧАСОВО: числа зашито тут, доки немає конфігів рівнів
    /// (Агент 1); тоді конфіг прийде з Resources/Config.
    /// </summary>
    public static class EmptyShift
    {
        public const int TicksPerSecond = 60;
        public const int MaxCatchUpTicks = 600;
        public const int DurationSec = 90;
        public const int ContinueBonusSec = 30;

        public static ShiftConfig CreateConfig()
        {
            return new ShiftConfig(TicksPerSecond, MaxCatchUpTicks, DurationSec, ContinueBonusSec);
        }

        public static ShiftSimulation Create(ulong seed)
        {
            return new ShiftSimulation(CreateConfig(), seed, new AlwaysAchieved());
        }

        /// <summary>Секунди до кінця для показу (округлення вгору: 0:00 лише коли час справді вийшов).</summary>
        public static long RemainingSeconds(ShiftSimulation shift)
        {
            long ticks = shift.RemainingTicks;
            int tps = shift.Config.TicksPerSecond;
            return (ticks + tps - 1) / tps;
        }

        /// <summary>Формат м:сс.</summary>
        public static string FormatClock(long seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            return (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":"
                + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private sealed class AlwaysAchieved : IShiftGoal
        {
            public bool IsAchieved(ShiftSimulation shift)
            {
                return true;
            }
        }
    }
}
