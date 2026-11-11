using System;
using System.IO;
using System.Text;
using KebabStreet.Core;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Збереження: файл існує, але не читається (BUG-019), а також BUG-020 (відкладені копії не затираються),
    /// BUG-022 (дані поза схемою, годинник до 1970) і BUG-023 (CanSave відповідає поведінці Save).
    /// Кожен сценарій виконується на сховищі в пам'яті і на справжніх файлах у тимчасовій папці.
    /// Початковий стан більшості тестів: основний файл — 250 монет, резервний — 100 монет.
    /// </summary>
    [TestFixture(false)]
    [TestFixture(true)]
    public sealed class SaveServiceFaultTests
    {
        private const long StartTime = 1700001000;
        private const string Primary = SaveService.PrimaryName;
        private const string Backup = SaveService.BackupName;
        private const string Temp = SaveService.TempName;

        private readonly bool _useFiles;
        private string _directory;
        private ISaveStorage _storage;
        private ManualClock _clock;
        private SaveService _service;

        public SaveServiceFaultTests(bool useFiles)
        {
            _useFiles = useFiles;
        }

        [SetUp]
        public void SetUp()
        {
            _directory = null;
            if (_useFiles)
            {
                _directory = Path.Combine(Path.GetTempPath(), "kebab-fault-tests-" + Guid.NewGuid().ToString("N"));
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

        private void SaveSample(long coins)
        {
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(SaveTestKit.Sample(coins)));
        }

        private void SaveTwice()
        {
            SaveSample(100);
            SaveSample(250);
        }

        private SaveService ServiceWithUnreadable(params string[] names)
        {
            return new SaveService(new UnreadableStorage(_storage, false, names), _clock);
        }

        /// <summary>Повний знімок носія: усі файли збереження, відкладені копії і їхній вміст.</summary>
        private string Snapshot()
        {
            var builder = new StringBuilder();
            AppendFile(builder, Primary);
            AppendFile(builder, Backup);
            AppendFile(builder, Temp);
            for (int copy = 1; copy <= SaveService.MaxCorruptCopies + 1; copy++)
            {
                AppendFile(builder, SaveService.CorruptName(Primary, copy));
                AppendFile(builder, SaveService.CorruptName(Backup, copy));
            }

            return builder.ToString();
        }

        private void AppendFile(StringBuilder builder, string name)
        {
            builder.Append(name).Append(" = ");
            builder.Append(_storage.Exists(name) ? "[" + _storage.ReadText(name) + "]" : "(none)");
            builder.Append('\n');
        }

        private static void AssertUnavailable(SaveLoadResult result)
        {
            Assert.AreEqual(SaveLoadSource.Unavailable, result.Source);
            Assert.IsFalse(result.CanSave, "У стані недоступності запис заборонено");
            Assert.IsFalse(result.ProgressReset, "Недоступний файл — це не втрата прогресу");
            Assert.IsFalse(result.Migrated);
            Assert.AreEqual(0, result.CorruptFilesFound);
            Assert.IsNotNull(result.Data);
        }

        private void AssertRealProgressIsBack()
        {
            SaveLoadResult result = _service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.IsFalse(result.ProgressReset);
            Assert.IsTrue(result.CanSave);
            Assert.AreEqual(250, result.Data.Coins);
            bool migrated;
            Assert.AreEqual(100, SaveSerializer.Deserialize(_storage.ReadText(Backup), out migrated).Coins);
        }

        // ---------- BUG-019: Load, коли файл не читається ----------

        [TestCase(false)]
        [TestCase(true)]
        public void Load_BothFilesUnreadable_ReportsUnavailableAndTouchesNothing(bool accessDenied)
        {
            SaveTwice();
            string before = Snapshot();
            var service = new SaveService(new UnreadableStorage(_storage, accessDenied, Primary, Backup), _clock);

            AssertUnavailable(service.Load());

            Assert.AreEqual(before, Snapshot(), "Справні файли не перейменовано і не змінено");
            AssertRealProgressIsBack();
        }

        [Test]
        public void Load_PrimaryUnreadableAndBackupValid_ReportsUnavailableInsteadOfSilentRollback()
        {
            SaveTwice();
            string before = Snapshot();

            SaveLoadResult result = ServiceWithUnreadable(Primary).Load();

            AssertUnavailable(result);
            Assert.AreEqual(0, result.Data.Coins, "Дані резервної копії не видаються за поточний прогрес");
            Assert.AreEqual(before, Snapshot());
            AssertRealProgressIsBack();
        }

        [Test]
        public void Load_BackupUnreadableAndPrimaryValid_LoadsPrimaryButForbidsSaving()
        {
            SaveTwice();
            string before = Snapshot();

            SaveLoadResult result = ServiceWithUnreadable(Backup).Load();

            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.AreEqual(250, result.Data.Coins);
            Assert.IsFalse(result.ProgressReset);
            Assert.IsFalse(result.CanSave, "Save відмовить, доки резервний файл не читається");
            Assert.AreEqual(before, Snapshot());
            AssertRealProgressIsBack();
        }

        [Test]
        public void Load_BackupUnreadableAndPrimaryCorrupt_ReportsUnavailableAndDoesNotQuarantine()
        {
            SaveTwice();
            _storage.WriteText(Primary, "garbage");
            string before = Snapshot();

            AssertUnavailable(ServiceWithUnreadable(Backup).Load());

            Assert.AreEqual(before, Snapshot(), "Поки резервний файл не читається, нічого не перейменовується");

            // Доступ відновився: пошкоджений основний відкладено, прогрес узято з резервної копії.
            SaveLoadResult restored = _service.Load();
            Assert.AreEqual(SaveLoadSource.Backup, restored.Source);
            Assert.IsFalse(restored.ProgressReset);
            Assert.AreEqual(100, restored.Data.Coins);
            Assert.AreEqual("garbage", _storage.ReadText(SaveService.CorruptName(Primary, 1)));
        }

        [Test]
        public void Load_BackupUnreadableAndPrimaryMissing_ReportsUnavailableNotNewProfile()
        {
            SaveTwice();
            _storage.Delete(Primary);
            string before = Snapshot();

            AssertUnavailable(ServiceWithUnreadable(Backup).Load());

            Assert.AreEqual(before, Snapshot());
            SaveLoadResult restored = _service.Load();
            Assert.AreEqual(SaveLoadSource.Backup, restored.Source);
            Assert.AreEqual(100, restored.Data.Coins);
        }

        [Test]
        public void Load_EveryStorageOperationFails_ReportsUnavailableAndTouchesNothing()
        {
            SaveTwice();
            string before = Snapshot();
            var service = new SaveService(new FaultyStorage(_storage, 1, false), _clock);

            AssertUnavailable(service.Load());

            Assert.AreEqual(before, Snapshot());
            AssertRealProgressIsBack();
        }

        // ---------- BUG-019: Save, коли файл не читається ----------

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Save_WhileFileUnreadable_FailsAndChangesNothing(bool primaryUnreadable, bool backupUnreadable)
        {
            SaveTwice();
            string before = Snapshot();
            SaveService service = primaryUnreadable && backupUnreadable
                ? ServiceWithUnreadable(Primary, Backup)
                : ServiceWithUnreadable(primaryUnreadable ? Primary : Backup);

            Assert.AreEqual(SaveWriteResult.Failed, service.Save(SaveTestKit.Sample(999)));
            Assert.AreEqual(SaveWriteResult.Failed, service.Save(SaveTestKit.Sample(999)));

            Assert.AreEqual(before, Snapshot(), "Жодного запису, перейменування чи видалення");
            AssertRealProgressIsBack();
        }

        [Test]
        public void LoadThenTwoSavesWhileUnreadable_ProgressSurvivesAndSavingWorksAfterAccessReturns()
        {
            SaveTwice();
            string before = Snapshot();
            SaveService blocked = ServiceWithUnreadable(Primary, Backup);

            // Сценарій QA: гра отримала «недоступно», але все одно двічі спробувала зберегти чистий профіль.
            SaveData blank = blocked.Load().Data;
            Assert.AreEqual(SaveWriteResult.RejectedAfterUnavailableLoad, blocked.Save(blank));
            Assert.AreEqual(SaveWriteResult.RejectedAfterUnavailableLoad, blocked.Save(blank));

            Assert.AreEqual(before, Snapshot());
            AssertRealProgressIsBack();

            SaveSample(400);
            SaveLoadResult result = _service.Load();
            Assert.AreEqual(400, result.Data.Coins);
            bool migrated;
            Assert.AreEqual(250, SaveSerializer.Deserialize(_storage.ReadText(Backup), out migrated).Coins);
        }

        // ---------- BUG-025: запобіжник запису після Unavailable / NewerVersion ----------

        [Test]
        public void Save_AfterUnavailableLoadEvenIfAccessReturned_IsRejectedUntilNextSuccessfulLoad()
        {
            SaveTwice();
            string before = Snapshot();
            var toggle = new ToggleUnreadableStorage(_storage, Primary, Backup);
            var service = new SaveService(toggle, _clock);

            toggle.Unreadable = true;
            SaveLoadResult blocked = service.Load();
            AssertUnavailable(blocked);

            // Доступ відновився, але гра тримає чистий профіль: два Save не можуть затерти прогрес.
            toggle.Unreadable = false;
            Assert.AreEqual(SaveWriteResult.RejectedAfterUnavailableLoad, service.Save(blocked.Data));
            Assert.AreEqual(SaveWriteResult.RejectedAfterUnavailableLoad, service.Save(blocked.Data));
            Assert.AreEqual(before, Snapshot(), "Файли не змінено");
            AssertRealProgressIsBack();

            SaveLoadResult reloaded = service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, reloaded.Source);
            Assert.AreEqual(250, reloaded.Data.Coins);
            reloaded.Data.Coins = 400;
            Assert.AreEqual(SaveWriteResult.Saved, service.Save(reloaded.Data));
            Assert.AreEqual(400, _service.Load().Data.Coins);
        }

        [Test]
        public void Save_AfterNewerVersionLoad_IsRejectedAndLaterLoadOfValidFileReenablesSaving()
        {
            _storage.WriteText(Primary, SaveTestKit.NewerVersionText);
            SaveLoadResult newer = _service.Load();
            Assert.AreEqual(SaveLoadSource.NewerVersion, newer.Source);

            // Файл замінили на справний (наприклад, відновлено з копії): Save усе одно чекає на Load.
            _storage.Delete(Primary);
            string before = Snapshot();
            Assert.AreEqual(SaveWriteResult.RejectedNewerSave, _service.Save(newer.Data));
            Assert.AreEqual(before, Snapshot());

            SaveLoadResult fresh = _service.Load();
            Assert.AreEqual(SaveLoadSource.NewProfile, fresh.Source);
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(fresh.Data));
        }

        // ---------- BUG-026: пошкоджена резервна копія при справному основному ----------

        [Test]
        public void Load_ValidPrimaryAndCorruptBackup_ReportsAndQuarantinesBackup()
        {
            SaveTwice();
            _storage.WriteText(Backup, "{\"schemaVersion\":2,\"coi");

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.AreEqual(250, result.Data.Coins);
            Assert.IsFalse(result.ProgressReset);
            Assert.IsTrue(result.CanSave);
            Assert.AreEqual(1, result.CorruptFilesFound);
            Assert.IsFalse(_storage.Exists(Backup));
            Assert.AreEqual("{\"schemaVersion\":2,\"coi", _storage.ReadText(SaveService.CorruptName(Backup, 1)));
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(result.Data));
        }

        [Test]
        public void Save_ValidPrimaryAndCorruptBackupWithoutLoad_QuarantinesInsteadOfDeleting()
        {
            SaveSample(250);
            _storage.WriteText(Backup, "damaged backup");

            SaveSample(300);

            Assert.AreEqual("damaged backup", _storage.ReadText(SaveService.CorruptName(Backup, 1)));
            bool migrated;
            Assert.AreEqual(250, SaveSerializer.Deserialize(_storage.ReadText(Backup), out migrated).Coins);
            Assert.AreEqual(300, _service.Load().Data.Coins);
        }

        // ---------- BUG-023: CanSave відповідає поведінці Save ----------

        [Test]
        public void Load_ValidPrimaryAndNewerBackup_CanSaveIsFalseAndSaveIsRejected()
        {
            SaveSample(250);
            _storage.WriteText(Backup, SaveTestKit.NewerVersionText);
            string before = Snapshot();

            SaveLoadResult result = _service.Load();

            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.AreEqual(250, result.Data.Coins);
            Assert.IsFalse(result.CanSave);
            Assert.AreEqual(SaveWriteResult.RejectedNewerSave, _service.Save(result.Data));
            Assert.AreEqual(before, Snapshot());
        }

        [Test]
        public void Load_CanSaveTrue_MeansSaveSucceeds()
        {
            SaveTwice();
            _storage.WriteText(Primary, "garbage");

            SaveLoadResult fromBackup = _service.Load();
            Assert.IsTrue(fromBackup.CanSave);
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(fromBackup.Data));

            SaveLoadResult fromPrimary = _service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, fromPrimary.Source);
            Assert.IsTrue(fromPrimary.CanSave);
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(fromPrimary.Data));
        }

        // ---------- BUG-020: відкладені копії не затираються ----------

        [Test]
        public void RepeatedCorruption_KeepsEarlierCopies_AndNumberOfCopiesIsLimited()
        {
            Assert.AreEqual(3, SaveService.MaxCorruptCopies, "Тест розрахований на три місця");
            SaveSample(100);
            for (int round = 1; round <= 5; round++)
            {
                _storage.WriteText(Primary, "damage " + round);
                Assert.AreEqual(1, _service.Load().CorruptFilesFound, "Раунд " + round);
                SaveSample(100 + round);
            }

            Assert.AreEqual("damage 1", _storage.ReadText(SaveService.CorruptName(Primary, 1)));
            Assert.AreEqual("damage 2", _storage.ReadText(SaveService.CorruptName(Primary, 2)));
            Assert.AreEqual("damage 5", _storage.ReadText(SaveService.CorruptName(Primary, 3)), "Замінюється лише останнє місце");
            Assert.IsFalse(_storage.Exists(SaveService.CorruptName(Primary, 4)), "Кількість копій обмежена");
            Assert.AreEqual(105, _service.Load().Data.Coins);
        }

        [Test]
        public void SaveOverCorruptPrimary_DoesNotOverwriteEarlierCorruptCopy()
        {
            SaveSample(100);
            _storage.WriteText(Primary, "damage 1");
            _service.Load();
            SaveSample(200);
            _storage.WriteText(Primary, "damage 2");

            SaveSample(300);

            Assert.AreEqual("damage 1", _storage.ReadText(SaveService.CorruptName(Primary, 1)));
            Assert.AreEqual("damage 2", _storage.ReadText(SaveService.CorruptName(Primary, 2)));
            Assert.AreEqual(300, _service.Load().Data.Coins);
        }

        [Test]
        public void CorruptName_FirstCopyHasPlainSuffix_NextAreNumbered()
        {
            Assert.AreEqual("save.json.corrupt", SaveService.CorruptName(Primary, 1));
            Assert.AreEqual("save.json.corrupt.2", SaveService.CorruptName(Primary, 2));
            Assert.AreEqual("save.json.bak.corrupt.3", SaveService.CorruptName(Backup, 3));
        }

        // ---------- BUG-022: дані поза схемою і годинник до 1970 ----------

        [Test]
        public void Save_ClockBeforeEpoch_SavesWithZeroTimestampInsteadOfFailingForever()
        {
            _clock.UtcNowUnixSec = -100;
            SaveData data = SaveTestKit.Sample(100);

            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(data));
            Assert.AreEqual(SaveWriteResult.Saved, _service.Save(data));

            SaveLoadResult result = _service.Load();
            Assert.AreEqual(SaveLoadSource.Primary, result.Source);
            Assert.AreEqual(100, result.Data.Coins);
            Assert.AreEqual(0, result.Data.LastSavedUnixSec);
            Assert.AreEqual(0, result.Data.LastKnownUnixSec);
        }

        [Test]
        public void Save_LevelIdThatCannotBeEncoded_IsRejectedAsInvalidDataAndChangesNothing()
        {
            SaveTwice();
            string before = Snapshot();
            SaveData broken = SaveTestKit.Sample(999);
            broken.LevelStars["half\uD800pair"] = 1;

            Assert.AreEqual(SaveWriteResult.RejectedInvalidData, _service.Save(broken));

            Assert.AreEqual(before, Snapshot());
            AssertRealProgressIsBack();
        }

        [Test]
        public void Save_NegativeValues_AreRejectedAsInvalidDataAndChangeNothing()
        {
            SaveTwice();
            string before = Snapshot();
            SaveData negativeGems = SaveTestKit.Sample(999);
            negativeGems.Gems = -1;
            SaveData negativeStars = SaveTestKit.Sample(999);
            negativeStars.LevelStars["loc1_03"] = -2;

            Assert.AreEqual(SaveWriteResult.RejectedInvalidData, _service.Save(negativeGems));
            Assert.AreEqual(SaveWriteResult.RejectedInvalidData, _service.Save(negativeStars));

            Assert.AreEqual(before, Snapshot());
            AssertRealProgressIsBack();
        }
    }
}
