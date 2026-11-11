using System;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Обробка станів збереження в каркасі гри (E0-05): Load до Save, відмови без запису чистого профілю.</summary>
    public sealed class ProfileSessionTests
    {
        private const string Primary = SaveService.PrimaryName;
        private const string Backup = SaveService.BackupName;
        private const string Garbage = "{ not json";

        private static ProfileSession Session(ISaveStorage storage)
        {
            return new ProfileSession(new SaveService(storage, new ManualClock(1700000000)));
        }

        [Test]
        public void Save_BeforeLoad_Throws_AndWritesNothing()
        {
            var storage = new InMemorySaveStorage();
            ProfileSession session = Session(storage);

            Assert.IsFalse(session.IsLoaded);
            Assert.Throws<InvalidOperationException>(() => session.Save());
            Assert.Throws<InvalidOperationException>(() => { var unused = session.Data; });
            Assert.IsFalse(storage.Exists(Primary));
            Assert.IsFalse(storage.Exists(SaveService.TempName));
        }

        [Test]
        public void FirstLaunch_LoadNewProfile_NoNotice_AndSaveWritesFile()
        {
            var storage = new InMemorySaveStorage();
            ProfileSession session = Session(storage);

            session.Load();
            Assert.IsTrue(session.IsLoaded);
            Assert.AreEqual(SaveNotice.None, session.LastNotice);
            Assert.AreEqual(SaveNotice.None, session.Save());
            Assert.IsTrue(storage.Exists(Primary));
        }

        [Test]
        public void ValidSave_LoadsBack_WithoutNotice()
        {
            var storage = new InMemorySaveStorage();
            ProfileSession first = Session(storage);
            first.Load();
            first.Data.Coins = 42;
            Assert.AreEqual(SaveNotice.None, first.Save());

            ProfileSession second = Session(storage);
            second.Load();
            Assert.AreEqual(42, second.Data.Coins);
            Assert.AreEqual(SaveNotice.None, second.LastNotice);
        }

        [Test]
        public void ProgressReset_ShowsNotice_ThenNewProfileCanBeSaved()
        {
            var storage = new InMemorySaveStorage();
            storage.WriteText(Primary, Garbage);
            storage.WriteText(Backup, Garbage);
            ProfileSession session = Session(storage);

            session.Load();
            Assert.AreEqual(SaveNotice.ProgressReset, session.LastNotice);
            Assert.AreEqual(SaveNotice.None, session.Save(), "після явного скидання новий профіль зберігається");
        }

        [Test]
        public void NewerVersion_ShowsUpdateNotice_AndNeverOverwritesFile()
        {
            var storage = new InMemorySaveStorage();
            storage.WriteText(Primary, SaveTestKit.NewerVersionText);
            ProfileSession session = Session(storage);

            session.Load();
            Assert.AreEqual(SaveNotice.UpdateGame, session.LastNotice);
            Assert.AreEqual(SaveNotice.UpdateGame, session.Save());
            Assert.AreEqual(SaveTestKit.NewerVersionText, storage.ReadText(Primary), "файл новішої версії не змінено");
            Assert.IsFalse(storage.Exists(Backup));
            Assert.IsFalse(storage.Exists(SaveService.TempName));
        }

        [Test]
        public void Unavailable_ShowsNotice_AndSaveDoesNotWriteCleanProfile()
        {
            var inner = new InMemorySaveStorage();
            inner.WriteText(Primary, SaveTestKit.V1Text);
            ProfileSession session = Session(new UnreadableStorage(inner, false, Primary));

            session.Load();
            Assert.AreEqual(SaveNotice.StorageUnavailable, session.LastNotice);
            Assert.AreEqual(SaveNotice.StorageUnavailable, session.Save());
            Assert.AreEqual(SaveTestKit.V1Text, inner.ReadText(Primary), "справжній прогрес не затерто чистим профілем");
            Assert.IsFalse(inner.Exists(Backup));
        }

        [Test]
        public void Unavailable_ThenAccessRestored_SaveStaysBlockedUntilNewLoad()
        {
            var inner = new InMemorySaveStorage();
            inner.WriteText(Primary, SaveTestKit.V1Text);
            var toggle = new ToggleUnreadableStorage(inner, Primary) { Unreadable = true };
            ProfileSession session = Session(toggle);

            session.Load();
            toggle.Unreadable = false;
            Assert.AreEqual(SaveNotice.StorageUnavailable, session.Save(), "без нового Load запис заборонено");
            Assert.AreEqual(SaveTestKit.V1Text, inner.ReadText(Primary));

            session.Load();
            Assert.AreEqual(SaveNotice.None, session.LastNotice);
            Assert.AreEqual(120, session.Data.Coins, "справжній прогрес прочитано");
            Assert.AreEqual(SaveNotice.None, session.Save());
        }

        [Test]
        public void BackupNewer_PrimaryValid_ShowsSaveDisabled_AndSaveIsRejected()
        {
            var storage = new InMemorySaveStorage();
            storage.WriteText(Primary, SaveTestKit.V1Text);
            storage.WriteText(Backup, SaveTestKit.NewerVersionText);
            ProfileSession session = Session(storage);

            session.Load();
            Assert.AreEqual(SaveNotice.SaveDisabled, session.LastNotice);
            Assert.AreEqual(SaveNotice.UpdateGame, session.Save());
            Assert.AreEqual(SaveTestKit.NewerVersionText, storage.ReadText(Backup));
        }

        [TestCase(SaveWriteResult.Saved, SaveNotice.None)]
        [TestCase(SaveWriteResult.RejectedNewerSave, SaveNotice.UpdateGame)]
        [TestCase(SaveWriteResult.RejectedAfterUnavailableLoad, SaveNotice.StorageUnavailable)]
        [TestCase(SaveWriteResult.Failed, SaveNotice.SaveFailed)]
        [TestCase(SaveWriteResult.RejectedInvalidData, SaveNotice.SaveFailed)]
        public void FromWrite_MapsEveryWriteResult(SaveWriteResult result, SaveNotice expected)
        {
            Assert.AreEqual(expected, SaveNoticePolicy.FromWrite(result));
        }

        [Test]
        public void EveryNotice_ExceptNone_HasATextKey()
        {
            Assert.IsNull(SaveNoticePolicy.KeyOf(SaveNotice.None));
            var localizer = new PlaceholderLocalizer();
            foreach (SaveNotice notice in Enum.GetValues(typeof(SaveNotice)))
            {
                if (notice == SaveNotice.None)
                {
                    continue;
                }

                string key = SaveNoticePolicy.KeyOf(notice);
                Assert.IsNotNull(key, notice.ToString());
                Assert.AreNotEqual("[" + key + "]", localizer.Get(key), notice.ToString());
            }
        }
    }
}
