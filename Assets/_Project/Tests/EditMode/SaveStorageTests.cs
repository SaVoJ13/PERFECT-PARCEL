using System;
using System.IO;
using System.Text;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Контракт ISaveStorage (E0-07): однакова поведінка сховища в пам'яті і файлового,
    /// плюс властивості саме файлів (UTF-8 без BOM, створення папки, які файли лишаються на носії).
    /// Файли — лише в тимчасовій папці з унікальним ім'ям; після тесту папка видаляється.
    /// </summary>
    [TestFixture(false)]
    [TestFixture(true)]
    public sealed class SaveStorageTests
    {
        private readonly bool _useFiles;
        private string _directory;
        private ISaveStorage _storage;

        public SaveStorageTests(bool useFiles)
        {
            _useFiles = useFiles;
        }

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "kebab-storage-tests-" + Guid.NewGuid().ToString("N"));
            _storage = _useFiles ? (ISaveStorage)new FileSaveStorage(_directory) : new InMemorySaveStorage();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void Exists_IsFalseBeforeAnyWrite()
        {
            Assert.IsFalse(_storage.Exists("a.json"));
        }

        [Test]
        public void WriteThenRead_ReturnsSameText()
        {
            const string text = "{\"k\":\"їжа \\\"кебаб\\\" 100%\"}";

            _storage.WriteText("a.json", text);

            Assert.IsTrue(_storage.Exists("a.json"));
            Assert.AreEqual(text, _storage.ReadText("a.json"));
        }

        [Test]
        public void Write_ReplacesLongerOldContentCompletely()
        {
            _storage.WriteText("a.json", "a much longer old content");
            _storage.WriteText("a.json", "new");

            Assert.AreEqual("new", _storage.ReadText("a.json"));
        }

        [Test]
        public void WriteEmptyText_ReadsBackEmpty()
        {
            _storage.WriteText("a.json", string.Empty);

            Assert.IsTrue(_storage.Exists("a.json"));
            Assert.AreEqual(string.Empty, _storage.ReadText("a.json"));
        }

        [Test]
        public void Read_MissingFile_ThrowsIoException()
        {
            Assert.Catch<IOException>(() => _storage.ReadText("missing.json"));
        }

        [Test]
        public void Move_RenamesFile()
        {
            _storage.WriteText("a.json", "content");

            _storage.Move("a.json", "b.json");

            Assert.IsFalse(_storage.Exists("a.json"));
            Assert.AreEqual("content", _storage.ReadText("b.json"));
        }

        [Test]
        public void Move_MissingSource_ThrowsIoException()
        {
            _storage.WriteText("other.json", "content");

            Assert.Catch<IOException>(() => _storage.Move("missing.json", "b.json"));
            Assert.IsFalse(_storage.Exists("b.json"));
        }

        [Test]
        public void Delete_RemovesFile()
        {
            _storage.WriteText("a.json", "content");

            _storage.Delete("a.json");

            Assert.IsFalse(_storage.Exists("a.json"));
        }

        [Test]
        public void FileStorage_WritesUtf8WithoutBom_AndCreatesMissingFolder()
        {
            string nested = Path.Combine(_directory, "profile", "slot");
            var storage = new FileSaveStorage(nested);
            const string text = "{\"k\":\"їжа\"}";

            storage.WriteText("a.json", text);

            byte[] expected = new UTF8Encoding(false).GetBytes(text);
            CollectionAssert.AreEqual(expected, File.ReadAllBytes(Path.Combine(nested, "a.json")));
        }

        [Test]
        public void FileStorage_AfterTwoSaves_FolderHoldsOnlyPrimaryAndBackup()
        {
            var service = new SaveService(new FileSaveStorage(_directory), new ManualClock(1700001000));
            Assert.AreEqual(SaveWriteResult.Saved, service.Save(SaveTestKit.Sample(100)));
            Assert.AreEqual(SaveWriteResult.Saved, service.Save(SaveTestKit.Sample(250)));

            string[] files = Directory.GetFiles(_directory);
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = Path.GetFileName(files[i]);
            }

            CollectionAssert.AreEquivalent(new[] { SaveService.PrimaryName, SaveService.BackupName }, files);
        }

        [Test]
        public void FileStorage_RejectsMissingFolderPath()
        {
            Assert.Throws<ArgumentException>(() => new FileSaveStorage(null));
            Assert.Throws<ArgumentException>(() => new FileSaveStorage(string.Empty));
        }

        [Test]
        public void SaveService_RejectsNullDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new SaveService(null, new ManualClock(0)));
            Assert.Throws<ArgumentNullException>(() => new SaveService(_storage, null));
        }
    }
}
