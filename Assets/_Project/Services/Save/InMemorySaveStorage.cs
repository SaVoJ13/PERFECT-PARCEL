using System;
using System.Collections.Generic;
using System.IO;

namespace KebabStreet.Services
{
    /// <summary>Сховище в пам'яті для тестів і редактора. Поводиться як файлове: ті самі винятки в тих самих випадках.</summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool Exists(string name)
        {
            return _files.ContainsKey(name);
        }

        public string ReadText(string name)
        {
            string text;
            if (!_files.TryGetValue(name, out text))
            {
                throw new FileNotFoundException("Файла немає", name);
            }

            return text;
        }

        public void WriteText(string name, string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            _files[name] = text;
        }

        public void Move(string fromName, string toName)
        {
            string text = ReadText(fromName);
            if (_files.ContainsKey(toName))
            {
                throw new IOException("Файл призначення вже існує: " + toName);
            }

            _files.Remove(fromName);
            _files[toName] = text;
        }

        public void Delete(string name)
        {
            _files.Remove(name);
        }
    }
}
