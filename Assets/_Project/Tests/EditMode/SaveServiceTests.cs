using System;
using System.IO;
using KebabStreet.Core;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Збереження і відновлення (E0-07). Кожен сценарій виконується двічі: на сховищі в пам'яті
    /// і на справжніх файлах у тимчасовій папці з унікальним ім'ям (після тесту папка видаляється).
    /// </summary>
    [TestFixture(false)]
    [TestFixture(true)]
    public sealed class SaveServiceTests
    {
        private const long StartTime = 1700001000;
        private const string Primary = SaveService.PrimaryName;
        private const string Backup = SaveService.BackupName;
        private const string Temp = SaveService.TempName;
        private const string PrimaryCorrupt = SaveService.PrimaryName + SaveService.CorruptSuffix;
        private const string BackupCorrupt = SaveService.BackupName + SaveService.CorruptSuffix;

        private readonly bool _useFiles;
        private string _directory;
        private ISaveStorage _storage;
        private ManualClock _clock;
        private SaveService _service;

        public SaveServiceTests(bool useFiles)
        {
            _useFiles = useFiles;
        }

        [SetUp]
        public void SetUp()
        {
            _directory = null;
            if (_useFiles)
            {
                _directory = Path.Combine(Path.GetTempPath(), "kebab-save-tests-" + Guid.NewGuid().ToString("N"));
                _storage = new FileSaveStorage(_directory);
            }
            else
            {
                _storage = new InMemorySaveStorage();
            }

            _clock = new ManualClock(StartTime);
            _service = new SaveService(_storage, _clock);
        }

        [TearDown]
        public void TearDown()
        {
            if (_directory != null && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private SaveData SaveSample(long coins)
        {
            SaveData data = SaveTestKit.Sample(coins);
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(data));
            return data;
        }

        private void AssertMissing(params string[] names)
        {
            foreach (string name in names)
            {
                Assert.IsFalse(_storage.Exists(name), "Не має існувати: " + name);
            }
        }

        private static string Corrupted(string kind, string validText)
        {
            switch (kind)
            {
                case "Garbage": return "\u0000\u0001 not json %$#";
                case "Truncated": return validText.Substring(0, validText.Length / 2);
                case "WithoutLastChar": return validText.Substring(0, validText.Length - 1);
                case "Empty": return string.Empty;
                case "Whitespace": return " \n ";
                case "NullLiteral": return "null";
                case "EmptyObject": return "{}";
                case "WrongFieldType": return validText.Replace("\"coins\":", "\"coins\":\"many\",\"was\":");
                case "ExtraTail": return validText + "}";
                default: throw new ArgumentException("Невідомий вид пошкодження: " + kind);
            }
        }

        // ---------- Перший запуск і звичайний цикл ----------

        [Test]
        public void Load_FirstRun_ReturnsNewProfileWithoutResetAndWritesNothing()
        {
            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.NewProfile, result.Source);
            Assert.IsFalse(result.ProgressReset, "Перший запуск — це не скидання прогресу");
            Assert.IsFalse(result.Migrated);
            Assert.AreEqual(0, result.CorruptFilesFound);
            Assert.IsTrue(result.CanSave);
            SaveTestKit.AssertSame(new SaveData(), result.Data);
            AssertMissing(Primary, Backup, Temp, PrimaryCorrupt, BackupCorrupt);
        }

        [Test]
        public void SaveThenLoad_ReturnsIdenticalData()
        {
            SaveData saved = SaveSample(120);

            SaveLoadResult result = new SaveService(_storage, _clock).Load();

            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.IsFalse(result.ProgressReset);
            Assert.IsFalse(result.Migrated);
            Assert.AreEqual(0, result.CorruptFilesFound);
            Assert.AreNotSame(saved, result.Data);
            SaveTestKit.AssertSame(saved, result.Data);
            AssertMissing(Backup, Temp);
        }

        [Test]
        public void SecondSave_BackupHoldsPreviousState()
        {
            SaveData first = SaveSample(100);
            string firstText = _storage.ReadText(Primary);
            SaveData second = SaveSample(250);

            Assert.AreEqual(firstText, _storage.ReadText(Backup));
            bool migrated;
            SaveTestKit.AssertSame(first, SaveSerializer.Deserialize(_storage.ReadText(Backup), out migrated));
            SaveTestKit.AssertSame(second, _service.Load().Data);
            AssertMissing(Temp);
        }

        [Test]
        public void ThirdSave_BackupHoldsSecondState()
        {
            SaveSample(100);
            SaveSample(250);
            string secondText = _storage.ReadText(Primary);
            SaveData third = SaveSample(400);

            Assert.AreEqual(secondText, _storage.ReadText(Backup));
            SaveTestKit.AssertSame(third, _service.Load().Data);
        }

        [Test]
        public void Save_StampsClockTime_AndLastKnownTimeNeverGoesBack()
        {
            SaveData data = SaveSample(10);
            Assert.AreEqual(StartTime, data.LastSavedUnixSec);
            Assert.AreEqual(StartTime, data.LastKnownUnixSec);

            _clock.UtcNowUnixSec = StartTime - 5000;
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(data));

            SaveData loaded = _service.Load().Data;
            Assert.AreEqual(StartTime - 5000, loaded.LastSavedUnixSec, "Показ годинника записується як є");
            Assert.AreEqual(StartTime, loaded.LastKnownUnixSec, "Останній відомий час не зменшується");

            _clock.UtcNowUnixSec = StartTime + 60;
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(loaded));
            Assert.AreEqual(StartTime + 60, _service.Load().Data.LastKnownUnixSec);
        }

        // ---------- Пошкодження і відновлення ----------

        [TestCase("Garbage")]
        [TestCase("Truncated")]
        [TestCase("WithoutLastChar")]
        [TestCase("Empty")]
        [TestCase("Whitespace")]
        [TestCase("NullLiteral")]
        [TestCase("EmptyObject")]
        [TestCase("WrongFieldType")]
        [TestCase("ExtraTail")]
        public void Load_CorruptPrimary_RestoresBackupAndSetsCorruptFileAside(string kind)
        {
            SaveData previous = SaveSample(100);
            SaveSample(250);
            string bad = Corrupted(kind, _storage.ReadText(Primary));
            _storage.WriteText(Primary, bad);

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.Backup, result.Source);
            Assert.IsFalse(result.ProgressReset);
            Assert.AreEqual(1, result.CorruptFilesFound);
            SaveTestKit.AssertSame(previous, result.Data);
            AssertMissing(Primary);
            Assert.AreEqual(bad, _storage.ReadText(PrimaryCorrupt), "Пошкоджений файл відкладено без змін");
            Assert.IsTrue(_storage.Exists(Backup));
        }

        [Test]
        public void Load_MissingPrimary_UsesBackup()
        {
            SaveData previous = SaveSample(100);
            SaveSample(250);
            _storage.Delete(Primary);

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.Backup, result.Source);
            Assert.IsFalse(result.ProgressReset);
            Assert.AreEqual(0, result.CorruptFilesFound);
            SaveTestKit.AssertSame(previous, result.Data);
        }

        [Test]
        public void Load_BothFilesCorrupt_ReturnsNewProfileWithResetFlagAndKeepsBothAside()
        {
            SaveSample(100);
            SaveSample(250);
            _storage.WriteText(Primary, "garbage");
            _storage.WriteText(Backup, "{\"schemaVersion\":2,\"coi");

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.NewProfile, result.Source);
            Assert.IsTrue(result.ProgressReset, "Скидання прогресу має бути явним");
            Assert.AreEqual(2, result.CorruptFilesFound);
            Assert.IsTrue(result.CanSave);
            SaveTestKit.AssertSame(new SaveData(), result.Data);
            AssertMissing(Primary, Backup);
            Assert.AreEqual("garbage", _storage.ReadText(PrimaryCorrupt));
            Assert.AreEqual("{\"schemaVersion\":2,\"coi", _storage.ReadText(BackupCorrupt));
        }

        [Test]
        public void Load_CorruptPrimaryWithoutBackup_ReturnsNewProfileWithResetFlag()
        {
            SaveSample(100);
            _storage.WriteText(Primary, string.Empty);

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.NewProfile, result.Source);
            Assert.IsTrue(result.ProgressReset);
            Assert.AreEqual(1, result.CorruptFilesFound);
            Assert.IsTrue(_storage.Exists(PrimaryCorrupt));
        }

        [Test]
        public void Load_MissingPrimaryAndCorruptBackup_ReturnsNewProfileWithResetFlag()
        {
            _storage.WriteText(Backup, "[1,2");

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.NewProfile, result.Source);
            Assert.IsTrue(result.ProgressReset);
            Assert.AreEqual(1, result.CorruptFilesFound);
            Assert.AreEqual("[1,2", _storage.ReadText(BackupCorrupt));
        }

        [Test]
        public void Load_AfterReportedReset_SaveAndLoadWorkAgain()
        {
            _storage.WriteText(Primary, "garbage");
            Assert.IsTrue(_service.Load().ProgressReset);

            SaveData fresh = SaveSample(5);
            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.IsFalse(result.ProgressReset);
            SaveTestKit.AssertSame(fresh, result.Data);
            Assert.AreEqual("garbage", _storage.ReadText(PrimaryCorrupt), "Відкладений файл лишається для діагностики");
        }

        [Test]
        public void Load_SecondCorruption_KeepsNewestCorruptFile()
        {
            SaveSample(100);
            SaveSample(250);
            _storage.WriteText(Primary, "first damage");
            _service.Load();
            SaveSample(300);
            _storage.WriteText(Primary, "second damage");

            SaveLoadResult result = _service.Load();

            // BUG-020: раніше відкладений файл не затирається — новий іде в наступне вільне місце.
            Assert.AreEqual(SaveLoadSource.Backup, result.Source);
            Assert.AreEqual("first damage", _storage.ReadText(PrimaryCorrupt));
            Assert.AreEqual("second damage", _storage.ReadText(SaveService.CorruptName(Primary, 2)));
        }

        [Test]
        public void SaveAfterRecoveryFromBackup_KeepsBackupUntouched()
        {
            SaveSample(100);
            string firstText = _storage.ReadText(Primary);
            SaveSample(250);
            _storage.WriteText(Primary, "garbage");
            Assert.AreEqual(SaveLoadSource.Backup, _service.Load().Source);

            SaveData next = SaveSample(300);

            Assert.AreEqual(firstText, _storage.ReadText(Backup));
            SaveLoadResult result = _service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            SaveTestKit.AssertSame(next, result.Data);
        }

        [Test]
        public void Save_OverCorruptPrimaryWithoutLoad_DoesNotReplaceGoodBackupWithGarbage()
        {
            SaveSample(100);
            string firstText = _storage.ReadText(Primary);
            SaveSample(250);
            _storage.WriteText(Primary, "garbage");

            SaveData next = SaveSample(300);

            Assert.AreEqual(firstText, _storage.ReadText(Backup), "Справна резервна копія не затерта пошкодженим файлом");
            Assert.AreEqual("garbage", _storage.ReadText(PrimaryCorrupt));
            SaveLoadResult result = _service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            SaveTestKit.AssertSame(next, result.Data);
        }

        // ---------- Збій посеред запису ----------

        [Test]
        public void Load_LeftoverTempFile_IsIgnoredAndNextSaveReplacesIt()
        {
            SaveData saved = SaveSample(100);
            _storage.WriteText(Temp, "{\"schemaVersion\":2,\"coi");

            SaveLoadResult result = _service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            SaveTestKit.AssertSame(saved, result.Data);

            SaveData next = SaveSample(250);
            AssertMissing(Temp);
            SaveTestKit.AssertSame(next, _service.Load().Data);
        }

        [Test]
        public void Load_OnlyTempFile_IsNotTrusted()
        {
            _storage.WriteText(Temp, SaveSerializer.Serialize(SaveTestKit.Sample(999)));

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.NewProfile, result.Source);
            Assert.IsFalse(result.ProgressReset);
            Assert.AreEqual(0, result.Data.Coins);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Save_CrashAtAnyOperation_KeepsPreviousOrNewDataAndNextSaveWorks(bool tornWrite)
        {
            SaveSample(100);
            SaveSample(250);
            var counter = new FaultyStorage(_storage, int.MaxValue, false);
            Assert.AreEqual(SaveWriteResult.Saved, new SaveService(counter, _clock).Save(SaveTestKit.Sample(999)));
            int totalOperations = counter.Operations;
            Assert.GreaterOrEqual(totalOperations, 8, "Запис із ротацією резервної копії складається з багатьох операцій");

            for (int failFrom = 1; failFrom <= totalOperations; failFrom++)
            {
                TearDown();
                SetUp();
                SaveSample(100);
                SaveData previous = SaveSample(250);
                string previousText = SaveSerializer.Serialize(previous);
                SaveData next = SaveTestKit.Sample(999);
                var faulty = new FaultyStorage(_storage, failFrom, tornWrite);

                SaveWriteResult writeResult = new SaveService(faulty, _clock).Save(next);

                string step = "Збій на операції " + failFrom + " з " + totalOperations;
                Assert.AreEqual(SaveWriteResult.Failed, writeResult, step);
                SaveLoadResult afterCrash = new SaveService(_storage, _clock).Load();
                Assert.AreNotEqual(SaveLoadSource.NewProfile, afterCrash.Source, step);
                Assert.IsFalse(afterCrash.ProgressReset, step);
                Assert.AreEqual(0, afterCrash.CorruptFilesFound, step);
                string loadedText = SaveSerializer.Serialize(afterCrash.Data);
                Assert.IsTrue(
                    loadedText == previousText || loadedText == SaveSerializer.Serialize(next),
                    step + ": завантажено не попередній і не новий стан: " + loadedText);

                SaveData recovery = SaveSample(5);
                SaveLoadResult afterRecovery = _service.Load();
                Assert.AreEqual(SaveLoadSource.Primary, afterRecovery.Source, step);
                SaveTestKit.AssertSame(recovery, afterRecovery.Data);
            }
        }

        [Test]
        public void Save_CrashDuringVeryFirstSave_LoadsAsFirstRun()
        {
            var faulty = new FaultyStorage(_storage, 3, true);
            var crashing = new SaveService(faulty, _clock);

            Assert.AreEqual(SaveWriteResult.Failed, crashing.Save(SaveTestKit.Sample(100)));

            SaveLoadResult result = _service.Load();
            Assert.AreEqual(SaveLoadSource.NewProfile, result.Source);
            Assert.IsFalse(result.ProgressReset);
            AssertMissing(Primary, Backup);
        }

        [Test]
        public void Save_WriteSilentlyTruncated_FailsAndKeepsPreviousData()
        {
            SaveData previous = SaveSample(100);
            var truncating = new FaultyStorage(_storage, int.MaxValue, false) { SilentTruncation = true };

            SaveWriteResult result = new SaveService(truncating, _clock).Save(SaveTestKit.Sample(999));

            Assert.AreEqual(SaveWriteResult.Failed, result, "Перевірка після запису має помітити неповний файл");
            SaveLoadResult loaded = _service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, loaded.Source);
            SaveTestKit.AssertSame(previous, loaded.Data);
        }

        [Test]
        public void Save_DataThatCouldNotBeLoadedBack_FailsAndKeepsPreviousData()
        {
            SaveData previous = SaveSample(100);
            SaveData broken = SaveTestKit.Sample(999);
            broken.Coins = -1;

            // BUG-022: непридатні дані — окремий результат, а не Failed (який означає тимчасову помилку сховища).
            Assert.AreEqual(SaveWriteResult.RejectedInvalidData, _service.Save(broken));
            AssertMissing(Temp);

            SaveLoadResult loaded = _service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, loaded.Source);
            SaveTestKit.AssertSame(previous, loaded.Data);
        }

        [Test]
        public void Save_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _service.Save(null));
        }

        // ---------- Версії схеми ----------

        [Test]
        public void Load_V1File_MigratesAndLeavesFileUntouched()
        {
            _storage.WriteText(Primary, SaveTestKit.V1Text);

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.IsTrue(result.Migrated);
            Assert.IsFalse(result.ProgressReset);
            Assert.AreEqual(SaveTestKit.V1MigratedToCurrentText, SaveSerializer.Serialize(result.Data));
            Assert.AreEqual(SaveTestKit.V1Text, _storage.ReadText(Primary), "Завантаження не переписує файл");
        }

        [Test]
        public void SaveAfterMigration_WritesCurrentVersionAndKeepsV1AsBackup()
        {
            _storage.WriteText(Primary, SaveTestKit.V1Text);
            SaveData migrated = _service.Load().Data;

            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(migrated));

            Assert.AreEqual(SaveTestKit.V1Text, _storage.ReadText(Backup), "Старий файл лишається резервною копією");
            StringAssert.StartsWith("{\"schemaVersion\":3,", _storage.ReadText(Primary));
            SaveLoadResult result = _service.Load();
            Assert.IsFalse(result.Migrated);
            Assert.AreEqual(120, result.Data.Coins);
            Assert.AreEqual(StartTime, result.Data.LastKnownUnixSec);
        }

        [Test]
        public void Load_CorruptPrimaryAndV1Backup_MigratesBackup()
        {
            _storage.WriteText(Primary, "garbage");
            _storage.WriteText(Backup, SaveTestKit.V1Text);

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.Backup, result.Source);
            Assert.IsTrue(result.Migrated);
            Assert.AreEqual(120, result.Data.Coins);
        }

        [Test]
        public void Load_NewerVersion_ReportsItAndTouchesNothing()
        {
            _storage.WriteText(Primary, SaveTestKit.NewerVersionText);

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.NewerVersion, result.Source);
            Assert.IsFalse(result.CanSave);
            Assert.IsFalse(result.ProgressReset);
            Assert.AreEqual(0, result.CorruptFilesFound);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(SaveTestKit.NewerVersionText, _storage.ReadText(Primary));
            AssertMissing(Backup, Temp, PrimaryCorrupt);
        }

        [Test]
        public void Save_OverNewerVersionPrimary_IsRejectedAndFileIsUnchanged()
        {
            _storage.WriteText(Primary, SaveTestKit.NewerVersionText);
            SaveData fromLoad = _service.Load().Data;

            Assert.AreEqual(SaveWriteResult.RejectedNewerSave, _service.Save(fromLoad));
            Assert.AreEqual(SaveWriteResult.RejectedNewerSave, _service.Save(SaveTestKit.Sample(100)));

            Assert.AreEqual(SaveTestKit.NewerVersionText, _storage.ReadText(Primary));
            AssertMissing(Backup, Temp, PrimaryCorrupt);
        }

        [Test]
        public void Save_WhenOnlyBackupIsNewerVersion_IsRejectedAndBackupIsUnchanged()
        {
            _storage.WriteText(Backup, SaveTestKit.NewerVersionText);

            Assert.AreEqual(SaveLoadSource.NewerVersion, _service.Load().Source);
            Assert.AreEqual(SaveWriteResult.RejectedNewerSave, _service.Save(SaveTestKit.Sample(100)));

            Assert.AreEqual(SaveTestKit.NewerVersionText, _storage.ReadText(Backup));
            AssertMissing(Primary, Temp);
        }

        [Test]
        public void Load_CorruptPrimaryAndNewerBackup_ReportsNewerVersion()
        {
            _storage.WriteText(Primary, "garbage");
            _storage.WriteText(Backup, SaveTestKit.NewerVersionText);

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.NewerVersion, result.Source);
            Assert.IsFalse(result.ProgressReset);
            Assert.AreEqual(1, result.CorruptFilesFound);
            Assert.AreEqual(SaveTestKit.NewerVersionText, _storage.ReadText(Backup));
            Assert.AreEqual(SaveWriteResult.RejectedNewerSave, _service.Save(SaveTestKit.Sample(100)));
        }

        [Test]
        public void Load_UnknownFields_DoNotBreakLoading()
        {
            _storage.WriteText(
                Primary,
                "{\"schemaVersion\":2,\"coins\":7,\"extra\":{\"nested\":[true,null,1.5e3,\"s\"]},\"gems\":1,"
                + "\"levelStars\":{},\"lastSavedUnixSec\":10,\"lastKnownUnixSec\":20,\"zzz\":\"tail\"}");

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.IsFalse(result.ProgressReset);
            Assert.AreEqual(7, result.Data.Coins);
            Assert.AreEqual(20, result.Data.LastKnownUnixSec);
        }
    }
}
