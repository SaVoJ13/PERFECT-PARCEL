using System;
using System.Collections.Generic;

namespace KebabStreet.Core
{
    /// <summary>
    /// Прогрес гравця, що зберігається між запусками. У пам'яті об'єкт завжди має поточну схему:
    /// старі версії переводить у неї SaveSerializer під час читання.
    /// Час — лише цілі Unix-секунди UTC (у Core немає системного годинника).
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>Поточна версія схеми. Зміна моделі = нова версія + міграція + тест міграції.</summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>Версія схеми цього об'єкта (завжди поточна).</summary>
        public int SchemaVersion => CurrentSchemaVersion;

        /// <summary>Монети.</summary>
        public long Coins { get; set; }

        /// <summary>Кристали.</summary>
        public long Gems { get; set; }

        /// <summary>Найкращі зірки за рівнями: ідентифікатор рівня → зірки. Порядок ключів — порядковий (Ordinal).</summary>
        public SortedDictionary<string, int> LevelStars { get; } =
            new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Показ годинника пристрою під час останнього збереження, Unix-секунди UTC.</summary>
        public long LastSavedUnixSec { get; set; }

        /// <summary>
        /// Найпізніший час, який гра будь-коли бачила, Unix-секунди UTC (схема 2).
        /// Не зменшується, навіть якщо годинник пристрою перевели назад (див. TimeGuard).
        /// </summary>
        public long LastKnownUnixSec { get; set; }
    }
}
