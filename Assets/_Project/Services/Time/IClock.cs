namespace KebabStreet.Services
{
    /// <summary>
    /// Джерело поточного часу. Логіка гри не читає системний годинник напряму:
    /// у тестах час задається вручну (ManualClock).
    /// </summary>
    public interface IClock
    {
        /// <summary>Поточний час UTC, цілі Unix-секунди.</summary>
        long UtcNowUnixSec { get; }
    }
}
