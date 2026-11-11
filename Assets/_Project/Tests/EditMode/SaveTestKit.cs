using System.IO;
using KebabStreet.Core;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Спільні дані й помічники тестів збереження (E0-07).</summary>
    internal static class SaveTestKit
    {
        /// <summary>Зафіксований текст збереження схеми 1 (без lastKnownUnixSec). Не змінювати: це «старий файл гравця».</summary>
        public const string V1Text =
            "{\"schemaVersion\":1,\"coins\":120,\"gems\":5,\"levelStars\":{\"loc1_01\":3,\"loc1_02\":1},"
            + "\"lastSavedUnixSec\":1700000000}";

        /// <summary>Той самий прогрес у схемі 2 після міграції.</summary>
        public const string V1MigratedToV2Text =
            "{\"schemaVersion\":2,\"coins\":120,\"gems\":5,\"levelStars\":{\"loc1_01\":3,\"loc1_02\":1},"
            + "\"lastSavedUnixSec\":1700000000,\"lastKnownUnixSec\":1700000000}";

        /// <summary>Збереження «з майбутнього»: версія схеми, якої ця гра ще не знає.</summary>
        public const string NewerVersionText =
            "{\"schemaVersion\":3,\"coins\":777,\"futureField\":{\"a\":[1,2.5,\"x\"]}}";

        public static SaveData Sample(long coins)
        {
            var data = new SaveData { Coins = coins, Gems = coins / 10 };
            data.LevelStars["loc1_01"] = 3;
            data.LevelStars["loc1_02"] = (int)(coins % 4);
            return data;
        }

        public static void AssertSame(SaveData expected, SaveData actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Coins, actual.Coins, "Coins");
            Assert.AreEqual(expected.Gems, actual.Gems, "Gems");
            Assert.AreEqual(expected.LastSavedUnixSec, actual.LastSavedUnixSec, "LastSavedUnixSec");
            Assert.AreEqual(expected.LastKnownUnixSec, actual.LastKnownUnixSec, "LastKnownUnixSec");
            CollectionAssert.AreEqual(expected.LevelStars, actual.LevelStars, "LevelStars");
        }
    }

    /// <summary>
    /// Сховище зі збоєм. Модель аварії: операції до номера failFrom виконуються, операція failFrom
    /// і всі наступні кидають IOException (процес «загинув»). tornWrite — запис, на якому стався збій,
    /// встигає записати половину тексту. SilentTruncation — запис без помилки, але на носій потрапляє половина.
    /// </summary>
    internal sealed class FaultyStorage : ISaveStorage
    {
        private readonly ISaveStorage _inner;
        private readonly int _failFrom;
        private readonly bool _tornWrite;

        public FaultyStorage(ISaveStorage inner, int failFrom, bool tornWrite)
        {
            _inner = inner;
            _failFrom = failFrom;
            _tornWrite = tornWrite;
        }

        public int Operations { get; private set; }

        public bool SilentTruncation { get; set; }

        public bool Exists(string name)
        {
            Step();
            return _inner.Exists(name);
        }

        public string ReadText(string name)
        {
            Step();
            return _inner.ReadText(name);
        }

        public void WriteText(string name, string text)
        {
            Operations++;
            if (Operations >= _failFrom)
            {
                if (_tornWrite && Operations == _failFrom)
                {
                    _inner.WriteText(name, text.Substring(0, text.Length / 2));
                }

                throw new IOException("Імітація збою запису");
            }

            _inner.WriteText(name, SilentTruncation ? text.Substring(0, text.Length / 2) : text);
        }

        public void Move(string fromName, string toName)
        {
            Step();
            _inner.Move(fromName, toName);
        }

        public void Delete(string name)
        {
            Step();
            _inner.Delete(name);
        }

        private void Step()
        {
            Operations++;
            if (Operations >= _failFrom)
            {
                throw new IOException("Імітація збою");
            }
        }
    }
}
