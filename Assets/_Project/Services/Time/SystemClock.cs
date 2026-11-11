using System;

namespace KebabStreet.Services
{
    /// <summary>Годинник пристрою. Гравець може його перевести — для щоденних механік час пропускають через TimeGuard.</summary>
    public sealed class SystemClock : IClock
    {
        public long UtcNowUnixSec
        {
            get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }
        }
    }
}
