using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KebabStreet.Core
{
    /// <summary>Збереження створене новішою версією гри, ніж ця: читати й перезаписувати його не можна.</summary>
    public sealed class SaveVersionTooNewException : SaveFormatException
    {
        public SaveVersionTooNewException(long foundVersion)
            : base("версія схеми " + foundVersion.ToString(CultureInfo.InvariantCulture) + " новіша за відому грі")
        {
            FoundVersion = foundVersion;
        }

        /// <summary>Версія схеми, знайдена в тексті.</summary>
        public long FoundVersion { get; }
    }

    /// <summary>
    /// Переводить SaveData в текст JSON і назад. Формат детермінований: сталий порядок полів,
    /// ключі рівнів за порядковим порівнянням, без пробілів, числа в інваріантній культурі.
    /// Невідомі поля під час читання пропускаються. Старі версії схеми проходять ланцюжок міграцій.
    /// </summary>
    public static class SaveSerializer
    {
        private const string SchemaVersionKey = "schemaVersion";
        private const string CoinsKey = "coins";
        private const string GemsKey = "gems";
        private const string LevelStarsKey = "levelStars";
        private const string LastSavedKey = "lastSavedUnixSec";
        private const string LastKnownKey = "lastKnownUnixSec";
        private const string TutorialSkipAllKey = "tutorialSkipAll";
        private const string TutorialSeenKey = "tutorialSeen";

        /// <summary>Текст збереження поточної схеми. Однакові дані завжди дають однаковий текст.</summary>
        public static string Serialize(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var builder = new StringBuilder(256);
            builder.Append('{');
            AppendNumber(builder, SchemaVersionKey, SaveData.CurrentSchemaVersion).Append(',');
            AppendNumber(builder, CoinsKey, data.Coins).Append(',');
            AppendNumber(builder, GemsKey, data.Gems).Append(',');
            SaveJson.AppendString(builder, LevelStarsKey);
            builder.Append(":{");
            bool first = true;
            foreach (KeyValuePair<string, int> pair in data.LevelStars)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                AppendNumber(builder, pair.Key, pair.Value);
            }

            builder.Append("},");
            AppendNumber(builder, LastSavedKey, data.LastSavedUnixSec).Append(',');
            AppendNumber(builder, LastKnownKey, data.LastKnownUnixSec).Append(',');
            SaveJson.AppendString(builder, TutorialSkipAllKey);
            builder.Append(':').Append(data.TutorialSkipAll ? "true" : "false").Append(',');
            SaveJson.AppendString(builder, TutorialSeenKey);
            builder.Append(":[");
            bool firstSeen = true;
            foreach (string id in data.TutorialSeen)
            {
                if (!firstSeen)
                {
                    builder.Append(',');
                }

                firstSeen = false;
                SaveJson.AppendString(builder, id);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        /// <summary>
        /// Читає текст збереження будь-якої відомої версії. Пошкоджений або неповний текст —
        /// SaveFormatException; версія, новіша за поточну, — SaveVersionTooNewException.
        /// </summary>
        /// <param name="json">Текст збереження.</param>
        /// <param name="migrated">true, якщо текст був старої версії і пройшов міграцію.</param>
        public static SaveData Deserialize(string json, out bool migrated)
        {
            migrated = false;
            var root = SaveJson.Parse(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new SaveFormatException("корінь збереження не є об'єктом");
            }

            long version = GetLong(root, SchemaVersionKey);
            if (version < 1)
            {
                throw new SaveFormatException("некоректна версія схеми");
            }

            if (version > SaveData.CurrentSchemaVersion)
            {
                throw new SaveVersionTooNewException(version);
            }

            migrated = version < SaveData.CurrentSchemaVersion;
            for (; version < SaveData.CurrentSchemaVersion; version++)
            {
                Migrate(root, version);
            }

            var data = new SaveData
            {
                Coins = GetNonNegative(root, CoinsKey),
                Gems = GetNonNegative(root, GemsKey),
                LastSavedUnixSec = GetNonNegative(root, LastSavedKey),
                LastKnownUnixSec = GetNonNegative(root, LastKnownKey),
            };

            data.TutorialSkipAll = GetBool(root, TutorialSkipAllKey);
            object seenValue;
            root.TryGetValue(TutorialSeenKey, out seenValue);
            var seen = seenValue as List<object>;
            if (seen == null)
            {
                throw new SaveFormatException("поле " + TutorialSeenKey + " відсутнє або не є масивом");
            }

            foreach (object item in seen)
            {
                var id = item as string;
                if (string.IsNullOrEmpty(id))
                {
                    throw new SaveFormatException("поле " + TutorialSeenKey + " містить не рядок або порожній рядок");
                }

                data.TutorialSeen.Add(id);
            }

            object starsValue;
            root.TryGetValue(LevelStarsKey, out starsValue);
            var stars = starsValue as Dictionary<string, object>;
            if (stars == null)
            {
                throw new SaveFormatException("поле " + LevelStarsKey + " відсутнє або не є об'єктом");
            }

            foreach (KeyValuePair<string, object> pair in stars)
            {
                long value = GetNonNegative(stars, pair.Key);
                if (value > int.MaxValue)
                {
                    throw new SaveFormatException("завелике значення зірок рівня " + pair.Key);
                }

                data.LevelStars[pair.Key] = (int)value;
            }

            return data;
        }

        /// <summary>Один крок ланцюжка: змінює дерево версії fromVersion на версію fromVersion + 1.</summary>
        private static void Migrate(Dictionary<string, object> root, long fromVersion)
        {
            switch (fromVersion)
            {
                case 1:
                    MigrateV1ToV2(root);
                    break;
                case 2:
                    MigrateV2ToV3(root);
                    break;
                default:
                    throw new SaveFormatException(
                        "немає міграції з версії " + fromVersion.ToString(CultureInfo.InvariantCulture));
            }

            root[SchemaVersionKey] = fromVersion + 1;
        }

        /// <summary>
        /// Схема 2: додано lastKnownUnixSec (захист від переведення годинника назад).
        /// Початкове значення — час останнього збереження зі схеми 1.
        /// </summary>
        private static void MigrateV1ToV2(Dictionary<string, object> root)
        {
            root[LastKnownKey] = GetLong(root, LastSavedKey);
        }

        /// <summary>Схема 3: додано стан навчання. Для старого профілю навчання ще не пройдено.</summary>
        private static void MigrateV2ToV3(Dictionary<string, object> root)
        {
            root[TutorialSkipAllKey] = false;
            root[TutorialSeenKey] = new List<object>();
        }

        private static bool GetBool(Dictionary<string, object> source, string key)
        {
            object value;
            if (!source.TryGetValue(key, out value) || !(value is bool))
            {
                throw new SaveFormatException("поле " + key + " відсутнє або не є true/false");
            }

            return (bool)value;
        }

        private static long GetLong(Dictionary<string, object> source, string key)
        {
            object value;
            if (!source.TryGetValue(key, out value) || !(value is long))
            {
                throw new SaveFormatException("поле " + key + " відсутнє або не є цілим числом");
            }

            return (long)value;
        }

        private static long GetNonNegative(Dictionary<string, object> source, string key)
        {
            long value = GetLong(source, key);
            if (value < 0)
            {
                throw new SaveFormatException("поле " + key + " від'ємне");
            }

            return value;
        }

        private static StringBuilder AppendNumber(StringBuilder builder, string key, long value)
        {
            SaveJson.AppendString(builder, key);
            return builder.Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
