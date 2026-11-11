using System;
using System.IO;
using System.Text;

namespace KebabStreet.Services
{
    /// <summary>
    /// Файли збереження в папці, переданій ззовні (у грі — Application.persistentDataPath, у тестах — тимчасова).
    /// Текст — UTF-8 без BOM. Перейменування не покладається на заміну наявного файла:
    /// така заміна поводиться по-різному на Windows, Linux і Android.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _directory;

        public FileSaveStorage(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Не вказано папку збереження", nameof(directory));
            }

            _directory = directory;
        }

        public bool Exists(string name)
        {
            return File.Exists(PathOf(name));
        }

        public string ReadText(string name)
        {
            return File.ReadAllText(PathOf(name), Encoding.UTF8);
        }

        public void WriteText(string name, string text)
        {
            Directory.CreateDirectory(_directory);
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(PathOf(name), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public void Move(string fromName, string toName)
        {
            File.Move(PathOf(fromName), PathOf(toName));
        }

        public void Delete(string name)
        {
            File.Delete(PathOf(name));
        }

        private string PathOf(string name)
        {
            return Path.Combine(_directory, name);
        }
    }
}
