using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KebabStreet.Game
{
    /// <summary>
    /// Таблиця рядків однієї мови, розібрана з плоского JSON <c>{ "ключ": "рядок" }</c>.
    /// Без залежностей від Unity, щоб перевіряти в EditMode. Некоректний JSON дає <see cref="FormatException"/>.
    /// </summary>
    public sealed class LocalizationTable
    {
        private readonly Dictionary<string, string> _strings;

        private LocalizationTable(Dictionary<string, string> strings)
        {
            _strings = strings;
        }

        public int Count => _strings.Count;

        public IEnumerable<string> Keys => _strings.Keys;

        public bool TryGet(string key, out string value)
        {
            value = null;
            return key != null && _strings.TryGetValue(key, out value);
        }

        public static LocalizationTable Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            int pos = 0;
            if (json.Length > 0 && json[0] == '﻿')
            {
                pos = 1;
            }

            SkipSpace(json, ref pos);
            Expect(json, ref pos, '{');
            SkipSpace(json, ref pos);
            if (Peek(json, pos) == '}')
            {
                pos++;
            }
            else
            {
                while (true)
                {
                    SkipSpace(json, ref pos);
                    string key = ReadString(json, ref pos);
                    SkipSpace(json, ref pos);
                    Expect(json, ref pos, ':');
                    SkipSpace(json, ref pos);
                    string value = ReadString(json, ref pos);
                    if (strings.ContainsKey(key))
                    {
                        throw new FormatException("Повторний ключ: " + key);
                    }

                    strings.Add(key, value);
                    SkipSpace(json, ref pos);
                    char c = Peek(json, pos);
                    pos++;
                    if (c == ',')
                    {
                        continue;
                    }

                    if (c == '}')
                    {
                        break;
                    }

                    throw new FormatException("Очікувалось ',' або '}' на позиції " + (pos - 1));
                }
            }

            SkipSpace(json, ref pos);
            if (pos != json.Length)
            {
                throw new FormatException("Зайві символи після JSON на позиції " + pos);
            }

            return new LocalizationTable(strings);
        }

        private static char Peek(string s, int pos)
        {
            return pos < s.Length ? s[pos] : '\0';
        }

        private static void SkipSpace(string s, ref int pos)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\n' || s[pos] == '\r'))
            {
                pos++;
            }
        }

        private static void Expect(string s, ref int pos, char expected)
        {
            if (Peek(s, pos) != expected || pos >= s.Length)
            {
                throw new FormatException("Очікувалось '" + expected + "' на позиції " + pos);
            }

            pos++;
        }

        private static string ReadString(string s, ref int pos)
        {
            Expect(s, ref pos, '"');
            var sb = new StringBuilder();
            while (true)
            {
                if (pos >= s.Length)
                {
                    throw new FormatException("Рядок не закрито");
                }

                char c = s[pos++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c < ' ')
                {
                    throw new FormatException("Керівний символ у рядку на позиції " + (pos - 1));
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (pos >= s.Length)
                {
                    throw new FormatException("Незавершене екранування");
                }

                char e = s[pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        int code;
                        if (pos + 4 > s.Length || !int.TryParse(s.Substring(pos, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
                        {
                            throw new FormatException("Некоректний \\u на позиції " + pos);
                        }

                        sb.Append((char)code);
                        pos += 4;
                        break;
                    default:
                        throw new FormatException("Невідоме екранування \\" + e);
                }
            }
        }
    }
}
