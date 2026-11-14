using KebabStreet.Core;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Вибір рівнів (S1-10): розблокування, показ зірок, збереження й відновлення прогресу, підписи кнопок.</summary>
    public sealed class LevelProgressTests
    {
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

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

        [Test]
        public void NothingCompleted_OnlyFirstLevelIsOpen()
        {
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            LevelEntry[] entries = LevelProgress.Build(levels, new SaveData());

            Assert.AreEqual(levels.Count, entries.Length);
            Assert.GreaterOrEqual(entries.Length, 5);
            Assert.IsTrue(entries[0].Unlocked);
            for (int i = 1; i < entries.Length; i++)
            {
                Assert.IsFalse(entries[i].Unlocked, "рівень " + entries[i].Number);
                Assert.AreEqual(0, entries[i].BestStars);
            }
        }

        [Test]
        public void FirstLevelCompleted_OpensSecondOnly()
        {
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            var data = new SaveData();
            data.LevelStars[levels.Get(0).Id] = 1;

            LevelEntry[] entries = LevelProgress.Build(levels, data);

            Assert.AreEqual(1, entries[0].BestStars);
            Assert.IsTrue(entries[1].Unlocked);
            Assert.IsFalse(entries[2].Unlocked);
            Assert.IsTrue(LevelProgress.IsUnlocked(levels, data, 2));
            Assert.IsFalse(LevelProgress.IsUnlocked(levels, data, 3));
            Assert.IsFalse(LevelProgress.IsUnlocked(levels, data, 99), "такого рівня немає");
        }

        [Test]
        public void ZeroStarsRecord_DoesNotOpenNextLevel()
        {
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            var data = new SaveData();
            data.LevelStars[levels.Get(0).Id] = 0;

            Assert.IsFalse(LevelProgress.IsUnlocked(levels, data, 2));
        }

        [Test]
        public void NextNumber_FollowsCatalog_AndIsZeroOnLastOrUnknown()
        {
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            Assert.AreEqual(levels.Get(1).Number, LevelProgress.NextNumber(levels, levels.Get(0).Number));
            Assert.AreEqual(0, LevelProgress.NextNumber(levels, levels.Get(levels.Count - 1).Number));
            Assert.AreEqual(0, LevelProgress.NextNumber(levels, 99));
        }

        [Test]
        public void Stars_SurviveSaveAndLoad_AndUnlockAfterRestart()
        {
            var storage = new InMemorySaveStorage();
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            LevelDefinition first = levels.Get(0);

            ProfileSession before = Session(storage);
            var completion = new ShiftCompletion(before, first);
            Assert.IsTrue(completion.Apply(Won(2, 50L)));
            Assert.AreEqual(SaveNotice.None, completion.Notice);

            ProfileSession after = Session(storage);
            Assert.AreEqual(2, LevelProgress.BestStars(after.Data, first));
            Assert.AreEqual(50L, after.Data.Coins);
            LevelEntry[] entries = LevelProgress.Build(levels, after.Data);
            Assert.AreEqual(2, entries[0].BestStars);
            Assert.IsTrue(entries[1].Unlocked, "прогрес переживає перезапуск");
        }

        [Test]
        public void BestStars_NeverDecrease_AfterWorseReplay_ThroughSaveLoad()
        {
            var storage = new InMemorySaveStorage();
            LevelDefinition first = ConfigTestKit.LoadCatalog().Get(0);

            Assert.IsTrue(new ShiftCompletion(Session(storage), first).Apply(Won(3, 130L)));
            Assert.IsTrue(new ShiftCompletion(Session(storage), first).Apply(Won(1, 20L)));

            ProfileSession restored = Session(storage);
            Assert.AreEqual(3, LevelProgress.BestStars(restored.Data, first));
            Assert.AreEqual(150L, restored.Data.Coins, "монети за обидві перемоги");
        }

        [Test]
        public void ButtonText_LockedStarsOrPlain()
        {
            Assert.AreEqual("Level 3 · locked", MenuController.LevelButtonText(Text, new LevelEntry(3, 0, false)));
            Assert.AreEqual("Level 2", MenuController.LevelButtonText(Text, new LevelEntry(2, 0, true)));
            Assert.AreEqual("Level 1 · 2/3 stars", MenuController.LevelButtonText(Text, new LevelEntry(1, 2, true)));
        }

        [Test]
        public void ContextUnlock_FollowsProfile()
        {
            var context = new GameContext(new InMemorySaveStorage(), new ManualClock(1700000000), Text);
            context.Profile.Load();

            Assert.IsTrue(context.IsLevelUnlocked(1));
            Assert.IsFalse(context.IsLevelUnlocked(2));
            context.Profile.Data.LevelStars[context.Levels.Get(0).Id] = 1;
            Assert.IsTrue(context.IsLevelUnlocked(2));
        }
    }
}
