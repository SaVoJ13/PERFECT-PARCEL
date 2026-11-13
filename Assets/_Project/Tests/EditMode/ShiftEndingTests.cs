using System.IO;
using KebabStreet.Core;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Запис наприкінці зміни (S1-11): стан навчання переживає перезапуск, невдале збереження дає видиме
    /// повідомлення на екрані результату (BUG-043), нагорода не нараховується двічі.
    /// </summary>
    public sealed class ShiftEndingTests
    {
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        /// <summary>Сховище, у яке можна «вимкнути» запис (диск повний, немає доступу).</summary>
        private sealed class SwitchableStorage : ISaveStorage
        {
            private readonly InMemorySaveStorage _inner = new InMemorySaveStorage();

            public bool FailWrites { get; set; }

            public bool Exists(string name)
            {
                return _inner.Exists(name);
            }

            public string ReadText(string name)
            {
                return _inner.ReadText(name);
            }

            public void WriteText(string name, string text)
            {
                if (FailWrites)
                {
                    throw new IOException("Імітація: диск повний");
                }

                _inner.WriteText(name, text);
            }

            public void Move(string fromName, string toName)
            {
                _inner.Move(fromName, toName);
            }

            public void Delete(string name)
            {
                _inner.Delete(name);
            }
        }

        private static ProfileSession Session(ISaveStorage storage)
        {
            var session = new ProfileSession(new SaveService(storage, new ManualClock(1700000000)));
            session.Load();
            return session;
        }

        private static ShiftResult Won(int stars, long coins)
        {
            return new ShiftResult(ShiftOutcome.Won, stars, null, 0L, coins, coins, 3L, 0L, 0L, 0L, 1L);
        }

        private static ShiftResult Lost()
        {
            return new ShiftResult(ShiftOutcome.Lost, 0, LoseReasons.TooSlow, 2L, 0L, 5L, 1L, 0L, 0L, 0L, 0L);
        }

        private static TutorialModel FinishedStep(SaveData profile)
        {
            TutorialConfig config = ResourceConfigSource.LoadTutorial().ValueOrThrow();
            var model = new TutorialModel(config, "L01", profile);
            model.Handle(new TutorialEvent(TutorialEventKind.ShiftStarted), 0);
            model.Handle(new TutorialEvent(TutorialEventKind.PlaceMeat), 500);
            Assert.IsTrue(model.Dirty);
            return model;
        }

        [Test]
        public void Win_SavesTutorialStateTogetherWithResult_AndSurvivesRestart()
        {
            var storage = new InMemorySaveStorage();
            ProfileSession profile = Session(storage);
            TutorialModel model = FinishedStep(profile.Data);
            var ending = new ShiftEnding(new ShiftCompletion(profile, ConfigTestKit.Level(1)), profile, model);

            SaveNotice notice = ending.OnFinished(Won(2, 50L));

            Assert.AreEqual(SaveNotice.None, notice);
            Assert.IsFalse(model.Dirty, "один запис покрив і підсумок, і навчання");
            ProfileSession restored = Session(storage);
            Assert.IsTrue(restored.Data.TutorialSeen.Contains("l01.step1"));
            Assert.AreEqual(50L, restored.Data.Coins);
            Assert.AreEqual(2, restored.Data.LevelStars["L01"]);
        }

        [Test]
        public void Loss_StillSavesTutorialState()
        {
            var storage = new InMemorySaveStorage();
            ProfileSession profile = Session(storage);
            TutorialModel model = FinishedStep(profile.Data);
            var ending = new ShiftEnding(new ShiftCompletion(profile, ConfigTestKit.Level(1)), profile, model);

            SaveNotice notice = ending.OnFinished(Lost());

            Assert.AreEqual(SaveNotice.None, notice);
            Assert.IsFalse(model.Dirty);
            ProfileSession restored = Session(storage);
            Assert.IsTrue(restored.Data.TutorialSeen.Contains("l01.step1"), "прогрес навчання не втрачено після поразки");
            Assert.AreEqual(0L, restored.Data.Coins, "поразка нагороди не дає");
            Assert.IsFalse(restored.Data.LevelStars.ContainsKey("L01"));
        }

        [Test]
        public void SkipAll_IsSaved_AndNextSessionHasNoTutorial()
        {
            var storage = new InMemorySaveStorage();
            ProfileSession profile = Session(storage);
            TutorialConfig config = ResourceConfigSource.LoadTutorial().ValueOrThrow();
            var model = new TutorialModel(config, "L01", profile.Data);
            model.Handle(new TutorialEvent(TutorialEventKind.ShiftStarted), 0);
            Assert.IsTrue(model.SkipAll(100));
            var ending = new ShiftEnding(new ShiftCompletion(profile, ConfigTestKit.Level(1)), profile, model);

            Assert.AreEqual(SaveNotice.None, ending.SaveTutorialIfDirty());

            ProfileSession restored = Session(storage);
            Assert.IsTrue(restored.Data.TutorialSkipAll);
            var next = new TutorialModel(config, "L01", restored.Data);
            next.Handle(new TutorialEvent(TutorialEventKind.ShiftStarted), 0);
            Assert.IsNull(next.Current);
        }

        [Test]
        public void NothingToSave_NoWriteHappens()
        {
            var storage = new SwitchableStorage { FailWrites = true };
            ProfileSession profile = Session(storage);
            TutorialConfig config = ResourceConfigSource.LoadTutorial().ValueOrThrow();
            var model = new TutorialModel(config, "L01", profile.Data);
            var ending = new ShiftEnding(new ShiftCompletion(profile, ConfigTestKit.Level(1)), profile, model);

            Assert.AreEqual(SaveNotice.None, ending.SaveTutorialIfDirty(), "профіль не змінювався: запису немає, збою теж");
            Assert.AreEqual(SaveNotice.None, ending.OnFinished(Lost()), "поразка без змін профілю не пише");
        }

        [Test]
        public void Bug043_FailedSaveAfterWin_GivesVisibleNoticeOnResultScreen()
        {
            var storage = new SwitchableStorage();
            ProfileSession profile = Session(storage);
            storage.FailWrites = true;
            var completion = new ShiftCompletion(profile, ConfigTestKit.Level(1));
            var ending = new ShiftEnding(completion, profile, null);
            ShiftDriver driver = new ShiftDriver(ConfigTestKit.Level(1), 20284958UL);
            driver.Begin();
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
            Assert.AreEqual(string.Empty, screen.ResultNoticeText, "до збою напису немає");

            SaveNotice notice = ending.OnFinished(Won(3, 130L));
            screen.ShowSaveNotice(notice);

            Assert.AreEqual(SaveNotice.SaveFailed, notice);
            Assert.AreEqual(SaveNotice.SaveFailed, completion.Notice);
            Assert.AreEqual(Text.Get(TextKeys.NoticeSaveFailed), screen.ResultNoticeText);
            Assert.AreEqual(130L, profile.Data.Coins, "у пам'яті нагорода є (рівні відкриваються до перезапуску)");
        }

        [Test]
        public void Bug043_NoticeDisappears_WhenSaveSucceeds()
        {
            var storage = new SwitchableStorage();
            ProfileSession profile = Session(storage);
            var ending = new ShiftEnding(new ShiftCompletion(profile, ConfigTestKit.Level(1)), profile, null);
            ShiftDriver driver = new ShiftDriver(ConfigTestKit.Level(1), 20284958UL);
            driver.Begin();
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
            screen.ShowSaveNotice(SaveNotice.SaveFailed);
            Assert.AreNotEqual(string.Empty, screen.ResultNoticeText);

            screen.ShowSaveNotice(ending.OnFinished(Won(1, 10L)));

            Assert.AreEqual(string.Empty, screen.ResultNoticeText);
        }

        [Test]
        public void FailedTutorialSave_KeepsDirty_AndRetriesLater()
        {
            var storage = new SwitchableStorage();
            ProfileSession profile = Session(storage);
            TutorialModel model = FinishedStep(profile.Data);
            var ending = new ShiftEnding(new ShiftCompletion(profile, ConfigTestKit.Level(1)), profile, model);
            storage.FailWrites = true;

            Assert.AreEqual(SaveNotice.SaveFailed, ending.SaveTutorialIfDirty());
            Assert.IsTrue(model.Dirty, "не збережено: спробуємо ще раз");

            storage.FailWrites = false;
            Assert.AreEqual(SaveNotice.None, ending.SaveTutorialIfDirty());
            Assert.IsFalse(model.Dirty);
            Assert.IsTrue(Session(storage).Data.TutorialSeen.Contains("l01.step1"));
        }

        [Test]
        public void ResultNotice_ForEveryKind_UsesItsOwnKey()
        {
            ShiftDriver driver = new ShiftDriver(ConfigTestKit.Level(1), 20284958UL);
            driver.Begin();
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });

            screen.ShowSaveNotice(SaveNotice.StorageUnavailable);
            Assert.AreEqual(Text.Get(TextKeys.NoticeStorageUnavailable), screen.ResultNoticeText);
            screen.ShowSaveNotice(SaveNotice.UpdateGame);
            Assert.AreEqual(Text.Get(TextKeys.NoticeUpdateGame), screen.ResultNoticeText);
            screen.ShowSaveNotice(SaveNotice.None);
            Assert.AreEqual(string.Empty, screen.ResultNoticeText);
        }

        [Test]
        public void Constructor_RejectsNulls()
        {
            var storage = new InMemorySaveStorage();
            ProfileSession profile = Session(storage);
            var completion = new ShiftCompletion(profile, ConfigTestKit.Level(1));
            Assert.Throws<System.ArgumentNullException>(() => new ShiftEnding(null, profile, null));
            Assert.Throws<System.ArgumentNullException>(() => new ShiftEnding(completion, null, null));
            Assert.DoesNotThrow(() => new ShiftEnding(completion, profile, null));
        }
    }
}
