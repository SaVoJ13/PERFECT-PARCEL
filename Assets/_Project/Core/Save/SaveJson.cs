using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KebabStreet.Core
{
    /// <summary>Текст збереження не вдалося розібрати або він не відповідає схемі.</summary>
    public class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Невеликий розбирач JSON без рефлексії і генерації коду (безпечно для IL2CPP), незалежний від культури.
    /// Результат: об'єкт — Dictionary&lt;string, object&gt;, масив — List&lt;object&gt;, рядок — string,
    /// ціле число — long, дробове — double, true/false — bool, null — null.
    /// Будь-яка помилка тексту — SaveFormatException.
    /// </summary>
    public sealed class SaveJson
    {
        private const int MaxDepth = 32;

        private readonly string _text;
        private int _pos;

        private SaveJson(string text)
        {
            _text = text;
        }

        /// <summary>Розбирає повний текст JSON; зайві символи після значення — помилка.</summary>
        public static object Parse(string text)
        {
            if (text == null)
            {
                throw new SaveFormatException("текст відсутній");
            }

            var reader = new SaveJson(text);
            object value = reader.ReadValue(0);
            reader.SkipWhitespace();
            if (reader._pos != text.Length)
            {
                throw reader.Error("зайві символи після значення");
            }

            return value;
        }

        /// <summary>Дописує рядок у лапках з екрануванням за правилами JSON.</summary>
        public static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value)
            {
                if (c == '"' || c == '\\')
                {
                    builder.Append('\\').Append(c);
                }
                else if (c < ' ')
                {
                    builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(c);
                }
            }

            builder.Append('"');
        }

        private object ReadValue(int depth)
        {
            if (depth > MaxDepth)
            {
                throw Error("завелика вкладеність");
            }

            SkipWhitespace();
            switch (Peek())
            {
                case '{': return ReadObject(depth);
                case '[': return ReadArray(depth);
                case '"': return ReadString();
                case 't': ReadLiteral("true"); return true;
                case 'f': ReadLiteral("false"); return false;
                case 'n': ReadLiteral("null"); return null;
                default: return ReadNumber();
            }
        }

        private Dictionary<string, object> ReadObject(int depth)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            Expect('{');
            SkipWhitespace();
            if (Peek() == '}')
            {
                _pos++;
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                string key = ReadString();
                SkipWhitespace();
                Expect(':');
                result[key] = ReadValue(depth + 1);
                SkipWhitespace();
                if (Peek() == ',')
                {
                    _pos++;
                    continue;
                }

                Expect('}');
                return result;
            }
        }

        private List<object> ReadArray(int depth)
        {
            var result = new List<object>();
            Expect('[');
            SkipWhitespace();
            if (Peek() == ']')
            {
                _pos++;
                return result;
            }

            while (true)
            {
                result.Add(ReadValue(depth + 1));
                SkipWhitespace();
                if (Peek() == ',')
                {
                    _pos++;
                    continue;
                }

                Expect(']');
                return result;
            }
        }

        private string ReadString()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (true)
            {
                char c = Peek();
                _pos++;
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c < ' ')
                {
                    throw Error("керівний символ у рядку");
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                char escape = Peek();
                _pos++;
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/': builder.Append(escape); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u': builder.Append(ReadHexChar()); break;
                    default: throw Error("невідоме екранування");
                }
            }
        }

        private char ReadHexChar()
        {
            int code;
            if (_pos + 4 > _text.Length || !int.TryParse(
                    _text.Substring(_pos, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
            {
                throw Error("некоректне екранування \\u");
            }

            _pos += 4;
            return (char)code;
        }

        private void ReadLiteral(string word)
        {
            if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
            {
                throw Error("невідоме значення");
            }

            _pos += word.Length;
        }

        private object ReadNumber()
        {
            int start = _pos;
            bool integer = true;
            while (_pos < _text.Length)
            {
                char c = _text[_pos];
                if (c == '.' || c == 'e' || c == 'E')
                {
                    integer = false;
                }
                else if (!((c >= '0' && c <= '9') || c == '-' || c == '+'))
                {
                    break;
                }

                _pos++;
            }

            string token = _text.Substring(start, _pos - start);
            long whole;
            if (integer && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out whole))
            {
                return whole;
            }

            double fraction;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out fraction))
            {
                return fraction;
            }

            throw Error("некоректне значення");
        }

        private char Peek()
        {
            if (_pos >= _text.Length)
            {
                throw Error("несподіваний кінець тексту");
            }

            return _text[_pos];
        }

        private void Expect(char expected)
        {
            if (Peek() != expected)
            {
                throw Error("очікувався символ " + expected);
            }

            _pos++;
        }

        private void SkipWhitespace()
        {
            while (_pos < _text.Length
                   && (_text[_pos] == ' ' || _text[_pos] == '\t' || _text[_pos] == '\n' || _text[_pos] == '\r'))
            {
                _pos++;
            }
        }

        private SaveFormatException Error(string message)
        {
            return new SaveFormatException(
                message + " (позиція " + _pos.ToString(CultureInfo.InvariantCulture) + ")");
        }
    }
}
