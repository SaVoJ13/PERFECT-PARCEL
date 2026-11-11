namespace KebabStreet.Services
{
    /// <summary>Керований годинник для тестів і редактора: час змінюється лише явно.</summary>
    public sealed class ManualClock : IClock
    {
        public ManualClock(long startUnixSec)
        {
            UtcNowUnixSec = startUnixSec;
        }

        public long UtcNowUnixSec { get; set; }

        /// <summary>Зсуває час на вказану кількість секунд (від'ємне значення — назад).</summary>
        public void Advance(long seconds)
        {
            UtcNowUnixSec += seconds;
        }
    }
}
