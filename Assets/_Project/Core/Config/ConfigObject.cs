using System;
using System.Collections.Generic;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Обгортка над розібраним JSON-об'єктом для суворого читання конфігів: кожна помилка (немає
    /// поля, не той тип, число поза межами, невідомий ключ) додається в спільний список з повним
    /// шляхом «документ: ключ.підключ[номер]». Читання не кидає винятків: після помилки метод
    /// повертає безпечне значення, а завантажувач просто не збирає конфіг.
    /// </summary>
    internal sealed class ConfigObject
    {
        private const int MillisPerSecond = 1000;
        private const int PercentBase = 100;
        private const double MillisTolerance = 1e-6;
        private const double MaxAbsNumber = 1e9;

        private readonly Dictionary<string, object> _map;
        private readonly string _doc;
        private readonly string _path;
        private readonly List<string> _errors;
        private readonly bool _broken;
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);

        private ConfigObject(
            Dictionary<string, object> map, string doc, string path, List<string> errors, bool broken)
        {
            _map = map;
            _doc = doc;
            _path = path;
            _errors = errors;
            _broken = broken;
        }

        /// <summary>Корінь документа; не об'єкт — помилка, далі читання тихе.</summary>
        public static ConfigObject Root(object value, string doc, List<string> errors)
        {
            return Create(value, doc, string.Empty, errors, false);
        }

        /// <summary>Чи об'єкт справжній (не підмінений після помилки типу).</summary>
        public bool IsValid => !_broken;

        public bool Has(string key)
        {
            return _map.ContainsKey(key);
        }

        /// <summary>Додає помилку з шляхом до ключа цього об'єкта.</summary>
        public void Report(string key, string detail)
        {
            _errors.Add(_doc + ": " + Join(key) + detail);
        }

        /// <summary>Шлях до ключа для повідомлень.</summary>
        public string PathOf(string key)
        {
            return Join(key);
        }

        public int Int(string key, long min, long max)
        {
            int value;
            if (!Has(key))
            {
                ReportMissing(key);
                return 0;
            }

            TryInt(key, min, max, out value);
            return value;
        }

        /// <summary>Необов'язкове ціле: немає поля — false без помилки; помилка значення — false з помилкою.</summary>
        public bool TryInt(string key, long min, long max, out int value)
        {
            value = 0;
            object raw;
            if (!Take(key, out raw))
            {
                return false;
            }

            if (!(raw is long))
            {
                Report(key, ": очікувалось ціле число");
                return false;
            }

            long whole = (long)raw;
            if (whole < min || whole > max)
            {
                Report(key, "=" + whole.ToString(CultureInfo.InvariantCulture) + ": дозволено " + Range(min, max));
                return false;
            }

            value = (int)whole;
            return true;
        }

        /// <summary>Секунди з JSON → цілі мілісекунди; точність не нижче 1 мс. min/max — у мілісекундах.</summary>
        public int Millis(string key, int minMs, int maxMs)
        {
            int value;
            if (!Has(key))
            {
                ReportMissing(key);
                return 0;
            }

            TryMillis(key, minMs, maxMs, out value);
            return value;
        }

        public bool TryMillis(string key, int minMs, int maxMs, out int value)
        {
            return TryScaled(key, MillisPerSecond, minMs, maxMs, "мс", out value);
        }

        /// <summary>Множник з JSON (1.5) → цілі відсотки (150); точність до відсотка.</summary>
        public int Percent(string key, int min, int max)
        {
            int value;
            if (!Has(key))
            {
                ReportMissing(key);
                return 0;
            }

            TryScaled(key, PercentBase, min, max, "%", out value);
            return value;
        }

        public string Str(string key)
        {
            if (!Has(key))
            {
                ReportMissing(key);
                return string.Empty;
            }

            return TryStr(key) ?? string.Empty;
        }

        /// <summary>Необов'язковий непорожній рядок; немає поля — null без помилки.</summary>
        public string TryStr(string key)
        {
            object raw;
            if (!Take(key, out raw))
            {
                return null;
            }

            string text = raw as string;
            if (string.IsNullOrEmpty(text))
            {
                Report(key, ": очікувався непорожній рядок");
                return null;
            }

            return text;
        }

        /// <summary>Ключ локалізації: лише ключ, не видимий текст.</summary>
        public string Key(string key)
        {
            string text = Str(key);
            if (text.Length > 0 && !ConfigText.IsLocalizationKey(text))
            {
                Report(key, "=" + text + ": очікувався ключ локалізації (латиниця, цифри, крапки), а не текст");
            }

            return text;
        }

        public bool Bool(string key)
        {
            if (!Has(key))
            {
                ReportMissing(key);
                return false;
            }

            return OptBool(key, false);
        }

        public bool OptBool(string key, bool fallback)
        {
            object raw;
            if (!Take(key, out raw))
            {
                return fallback;
            }

            if (!(raw is bool))
            {
                Report(key, ": очікувалось true або false");
                return fallback;
            }

            return (bool)raw;
        }

        /// <summary>Поле дозволене, але значення може бути null («немає межі»). true — є число, дано в value.</summary>
        public bool TryNullableInt(string key, long min, long max, out int value)
        {
            value = 0;
            if (!Has(key))
            {
                ReportMissing(key);
                return false;
            }

            if (_map[key] == null)
            {
                _used.Add(key);
                return false;
            }

            return TryInt(key, min, max, out value);
        }

        public ConfigObject Obj(string key)
        {
            if (!Has(key))
            {
                ReportMissing(key);
                return Create(null, _doc, Join(key), _errors, true);
            }

            return OptObj(key);
        }

        /// <summary>Необов'язковий об'єкт; немає поля — порожній об'єкт без помилки.</summary>
        public ConfigObject OptObj(string key)
        {
            object raw;
            if (!Take(key, out raw))
            {
                return new ConfigObject(
                    new Dictionary<string, object>(StringComparer.Ordinal), _doc, Join(key), _errors, false);
            }

            return Create(raw, _doc, Join(key), _errors, false);
        }

        /// <summary>Масив об'єктів; кількість має вкладатись у межі.</summary>
        public List<ConfigObject> ObjList(string key, int minCount, int maxCount)
        {
            var result = new List<ConfigObject>();
            if (!Has(key))
            {
                ReportMissing(key);
                return result;
            }

            List<object> items = TakeList(key);
            if (items == null)
            {
                return result;
            }

            if (items.Count < minCount || items.Count > maxCount)
            {
                Report(key, ".count=" + items.Count.ToString(CultureInfo.InvariantCulture)
                    + ": дозволено " + Range(minCount, maxCount));
            }

            for (int i = 0; i < items.Count; i++)
            {
                result.Add(Create(
                    items[i], _doc, Join(key) + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", _errors, false));
            }

            return result;
        }

        /// <summary>Масив рядків; немає поля — порожній список (якщо required = false) або помилка.</summary>
        public List<string> StrList(string key, bool required, int maxCount, bool asKeys)
        {
            var result = new List<string>();
            if (!Has(key))
            {
                if (required)
                {
                    ReportMissing(key);
                }

                return result;
            }

            List<object> items = TakeList(key);
            if (items == null)
            {
                return result;
            }

            if (items.Count > maxCount)
            {
                Report(key, ".count=" + items.Count.ToString(CultureInfo.InvariantCulture)
                    + ": дозволено 0.." + maxCount.ToString(CultureInfo.InvariantCulture));
            }

            for (int i = 0; i < items.Count; i++)
            {
                string item = items[i] as string;
                string at = key + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (string.IsNullOrEmpty(item))
                {
                    Report(at, ": очікувався непорожній рядок");
                    continue;
                }

                if (asKeys && !ConfigText.IsLocalizationKey(item))
                {
                    Report(at, "=" + item + ": очікувався ключ локалізації, а не текст");
                }

                result.Add(item);
            }

            return result;
        }

        /// <summary>Повідомляє про всі ключі, яких ніхто не прочитав (помилка в назві чи зайве поле).</summary>
        public void Done()
        {
            if (_broken)
            {
                return;
            }

            var unknown = new List<string>();
            foreach (KeyValuePair<string, object> pair in _map)
            {
                if (!_used.Contains(pair.Key))
                {
                    unknown.Add(pair.Key);
                }
            }

            unknown.Sort(StringComparer.Ordinal);
            for (int i = 0; i < unknown.Count; i++)
            {
                Report(unknown[i], ": невідомий ключ");
            }
        }

        private static ConfigObject Create(
            object value, string doc, string path, List<string> errors, bool alreadyReported)
        {
            var map = value as Dictionary<string, object>;
            if (map == null)
            {
                if (!alreadyReported)
                {
                    errors.Add(doc + ": " + (path.Length == 0 ? string.Empty : path + ": ") + "очікувався об'єкт");
                }

                return new ConfigObject(
                    new Dictionary<string, object>(StringComparer.Ordinal), doc, path, errors, true);
            }

            return new ConfigObject(map, doc, path, errors, false);
        }

        private bool TryScaled(string key, int scale, long min, long max, string unit, out int value)
        {
            value = 0;
            object raw;
            if (!Take(key, out raw))
            {
                return false;
            }

            double number;
            if (raw is long)
            {
                number = (long)raw;
            }
            else if (raw is double)
            {
                number = (double)raw;
            }
            else
            {
                Report(key, ": очікувалось число");
                return false;
            }

            if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > MaxAbsNumber)
            {
                Report(key, ": число поза допустимими межами");
                return false;
            }

            double scaled = number * scale;
            double rounded = Math.Round(scaled, MidpointRounding.AwayFromZero);
            if (Math.Abs(scaled - rounded) > MillisTolerance * scale)
            {
                Report(key, "=" + number.ToString(CultureInfo.InvariantCulture)
                    + ": занадто точне значення (потрібна точність до 1 " + unit + ")");
                return false;
            }

            long result = (long)rounded;
            if (result < min || result > max)
            {
                Report(key, "=" + number.ToString(CultureInfo.InvariantCulture) + ": дозволено "
                    + Range(min, max) + " " + unit);
                return false;
            }

            value = (int)result;
            return true;
        }

        private bool Take(string key, out object raw)
        {
            if (!_map.TryGetValue(key, out raw))
            {
                return false;
            }

            _used.Add(key);
            return true;
        }

        private List<object> TakeList(string key)
        {
            object raw;
            Take(key, out raw);
            var list = raw as List<object>;
            if (list == null)
            {
                Report(key, ": очікувався масив");
            }

            return list;
        }

        private void ReportMissing(string key)
        {
            if (!_broken)
            {
                Report(key, ": обов'язкове поле відсутнє");
            }
        }

        private string Join(string key)
        {
            return _path.Length == 0 ? key : _path + "." + key;
        }

        private static string Range(long min, long max)
        {
            return min.ToString(CultureInfo.InvariantCulture) + ".." + max.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Перевірки рядків конфігів: ключ локалізації чи ідентифікатор, а не видимий текст.</summary>
    public static class ConfigText
    {
        /// <summary>
        /// Ключ локалізації: латинські літери, цифри, «_» і «.»; починається з літери, не закінчується
        /// крапкою, без порожніх сегментів і пробілів. Одне слово без крапки теж ключ (напр. «ui»).
        /// </summary>
        public static bool IsLocalizationKey(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsAsciiLetter(text[0]) || text[text.Length - 1] == '.')
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool ok = IsAsciiLetter(c) || (c >= '0' && c <= '9') || c == '_' || c == '.';
                if (!ok || (c == '.' && text[i - 1] == '.'))
                {
                    return false;
                }
            }

            return text.IndexOf('.') > 0;
        }

        /// <summary>Ідентифікатор або ключ: рядок конфіга, що не може бути видимим текстом.</summary>
        public static bool IsIdentifierOrKey(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsAsciiLetter(text[0]))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!(IsAsciiLetter(c) || (c >= '0' && c <= '9') || c == '_' || c == '.'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
    }
}
