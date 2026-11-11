using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Захист щоденних механік від переведення годинника назад. «Ефективний» час ніколи не менший
    /// за найпізніший час, який гра вже бачила (SaveData.LastKnownUnixSec): якщо годинник показує
    /// раніший момент, час стоїть на місці, доки справжній час його не наздожене.
    /// Усі значення — Unix-секунди UTC; показ годинника передається ззовні (IClock у Services).
    /// </summary>
    public static class TimeGuard
    {
        /// <summary>Секунд у добі.</summary>
        public const long SecondsPerDay = 86400;

        /// <summary>Ефективний час: показ годинника, але не раніше за останній відомий час.</summary>
        public static long EffectiveNow(long lastKnownUnixSec, long clockUnixSec)
        {
            return Math.Max(lastKnownUnixSec, clockUnixSec);
        }

        /// <summary>Враховує показ годинника: оновлює останній відомий час у збереженні й повертає ефективний час.</summary>
        public static long Observe(SaveData data, long clockUnixSec)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.LastKnownUnixSec = EffectiveNow(data.LastKnownUnixSec, clockUnixSec);
            return data.LastKnownUnixSec;
        }

        /// <summary>Номер доби UTC від початку епохи (округлення вниз, зокрема для від'ємного часу).</summary>
        public static long DayIndex(long unixSec)
        {
            long day = unixSec / SecondsPerDay;
            return unixSec % SecondsPerDay < 0 ? day - 1 : day;
        }
    }
}
