using System;
using KebabStreet.Core;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Адаптер реального часу до симуляції (S1-08): детермінізм, пауза, обмеження кроків, завершення і запис підсумку.</summary>
    public sealed class ShiftDriverTests
    {
        private const ulong Seed = 20284958UL;
        private const ulong BotMask = 0xB07B07B07B07B07BUL;

        private sealed class CountingStorage : ISaveStorage
        {
            private readonly InMemorySaveStorage _inner = new InMemorySaveStorage();

            public int Writes { get; private set; }

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
                Writes++;
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

        private static ShiftDriver Started(int levelNumber, Action<ShiftResult> onFinished = null)
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(levelNumber), Seed, onFinished);
            Assert.IsTrue(driver.Begin());
            return driver;
        }

        private static double TickSeconds(ShiftDriver driver)
        {
            return 1.0 / driver.Level.Shift.TicksPerSecond;
        }

        private static ShiftDriver PlayWithBot(int levelNumber, Action<ShiftResult> onFinished, out bool hintSeen)
        {
            ShiftDriver driver = Started(levelNumber, onFinished);
            var bot = new BotPlayer(driver.Simulation, BotProfile.Expert, Seed ^ BotMask);
            double dt = TickSeconds(driver);
            hintSeen = false;
            for (int guard = 0; guard < 200000 && !driver.IsFinished; guard++)
            {
                bot.Step();
                driver.Frame(dt);
                hintSeen |= driver.GoalDoneStarHint;
            }

            Assert.IsTrue(driver.IsFinished, "зміна має завершитись");
            return driver;
        }

        private static ProfileSession LoadedProfile(ISaveStorage storage)
        {
            var session = new ProfileSession(new SaveService(storage, new ManualClock(1700000000)));
            session.Load();
            return session;
        }

        [TestCase(1)]
        [TestCase(5)]
        public void SameFrames_GiveSameSimulationState(int levelNumber)
        {
            ShiftDriver a = Started(levelNumber);
            ShiftDriver b = Started(levelNumber);
            double[] frames = { 0.016, 0.017, 0.033, 0.0166667, 0.5, 0.016, 0.25 };
            for (int i = 0; i < 400; i++)
            {
                double dt = frames[i % frames.Length];
                Assert.AreEqual(a.Frame(dt), b.Frame(dt));
            }

            Assert.Greater(a.Simulation.ElapsedTicks, 0L);
            Assert.IsTrue(a.Simulation.CaptureSnapshot().Equals(b.Simulation.CaptureSnapshot()));
        }

        [Test]
        public void Pause_DoesNotMoveTheClock_AndFirstFrameAfterResumeIsDropped()
        {
            ShiftDriver paused = Started(1);
            ShiftDriver reference = Started(1);
            double dt = TickSeconds(paused);
            for (int i = 0; i < 10; i++)
            {
                paused.Frame(dt);
                reference.Frame(dt);
            }

            Assert.IsTrue(paused.SetPaused(true));
            Assert.AreEqual(ShiftState.Paused, paused.Simulation.State);
            long frozen = paused.Simulation.ElapsedTicks;
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(0, paused.Frame(5.0));
            }

            Assert.AreEqual(frozen, paused.Simulation.ElapsedTicks);

            Assert.IsFalse(paused.SetPaused(false));
            Assert.AreEqual(ShiftState.Running, paused.Simulation.State);
            Assert.AreEqual(0, paused.Frame(100.0), "довгий кадр після відновлення відкидається");
            Assert.AreEqual(frozen, paused.Simulation.ElapsedTicks);

            for (int i = 0; i < 10; i++)
            {
                paused.Frame(dt);
                reference.Frame(dt);
            }

            Assert.AreEqual(20L, reference.Simulation.ElapsedTicks);
            Assert.IsTrue(
                paused.Simulation.CaptureSnapshot().Equals(reference.Simulation.CaptureSnapshot()),
                "пауза не змінила перебіг зміни");
        }

        [Test]
        public void SetPaused_IsIdempotent_AndIgnoredBeforeBegin()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(1), Seed);
            Assert.IsFalse(driver.SetPaused(true), "до початку паузи немає");
            Assert.AreEqual(ShiftState.Intro, driver.Simulation.State);
            Assert.AreEqual(0, driver.Frame(1.0), "до початку час не йде");

            driver.Begin();
            Assert.IsTrue(driver.SetPaused(true));
            Assert.IsTrue(driver.SetPaused(true));
            Assert.IsFalse(driver.SetPaused(false));
            Assert.IsFalse(driver.SetPaused(false));
            Assert.IsFalse(driver.Begin(), "повторний початок відхиляється");
        }

        [Test]
        public void Frame_LongFrame_IsCappedByMaxCatchUpTicks()
        {
            ShiftDriver driver = Started(1);
            int cap = driver.Level.Shift.MaxCatchUpTicks;
            Assert.Less(cap, driver.Level.Shift.DurationTicks);

            int executed = driver.Frame(1000.0);

            Assert.AreEqual(cap, executed);
            Assert.AreEqual((long)cap, driver.Simulation.ElapsedTicks);
        }

        [Test]
        public void Frame_BadDelta_IsTreatedAsZero()
        {
            ShiftDriver driver = Started(1);
            Assert.AreEqual(0, driver.Frame(double.NaN));
            Assert.AreEqual(0, driver.Frame(-1.0));
            Assert.AreEqual(0L, driver.Simulation.ElapsedTicks);
        }

        [Test]
        public void Taps_ReachTheCoreQueue_OnlyWhileRunning()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(1), Seed);
            Assert.IsFalse(driver.Tap(ShiftTap.PlaceMeat), "до початку тапи відкидаються");

            driver.Begin();
            Assert.IsTrue(driver.Tap(ShiftTap.PlaceMeat));
            Assert.AreEqual(1, driver.Simulation.QueuedActionCount);
            Assert.IsTrue(driver.Tap(ShiftTap.CancelQueuedAction, 0));
            Assert.AreEqual(0, driver.Simulation.QueuedActionCount);

            Assert.IsTrue(driver.Tap(ShiftTap.PlaceMeat));
            driver.SetPaused(true);
            Assert.IsFalse(driver.Tap(ShiftTap.PlaceMeat), "на паузі тапи відкидаються");
            Assert.AreEqual(1, driver.Simulation.QueuedActionCount);

            driver.SetPaused(false);
            driver.Frame(TickSeconds(driver));
            driver.Frame(1.0);
            Assert.GreaterOrEqual(
                driver.Simulation.ExecutedActionCount + driver.Simulation.IgnoredActionCount,
                1L,
                "команда дійшла до виконання на такті");
        }

        [Test]
        public void Shift_ReachesResult_AndProfileIsSavedExactlyOnce()
        {
            var storage = new CountingStorage();
            ProfileSession profile = LoadedProfile(storage);
            LevelDefinition level = ConfigTestKit.Level(1);
            var completion = new ShiftCompletion(profile, level);
            int finishedCalls = 0;

            // Скільки записів дає один звичайний Save (еталон, без припущень про внутрішній порядок SaveService).
            var referenceStorage = new CountingStorage();
            ProfileSession reference = LoadedProfile(referenceStorage);
            int referenceBefore = referenceStorage.Writes;
            reference.Save();
            int writesPerSave = referenceStorage.Writes - referenceBefore;
            Assert.Greater(writesPerSave, 0);

            int before = storage.Writes;
            bool hint;
            ShiftDriver driver = PlayWithBot(
                1,
                r =>
                {
                    finishedCalls++;
                    completion.Apply(r);
                    completion.Apply(r);
                },
                out hint);

            Assert.AreEqual(1, finishedCalls, "кінець зміни повідомляється один раз");
            Assert.AreEqual(ShiftState.Won, driver.Simulation.State);
            Assert.IsTrue(driver.Result.Won);
            Assert.IsTrue(completion.Applied);
            Assert.AreEqual(writesPerSave, storage.Writes - before, "збереження прогресу — один раз");
            Assert.AreEqual(driver.Result.RewardCoins, profile.Data.Coins);
            Assert.AreEqual(driver.Result.Stars, profile.Data.LevelStars[level.Id]);

            driver.Frame(10.0);
            Assert.AreEqual(1, finishedCalls, "після кінця кадри нічого не змінюють");
        }

        [Test]
        public void LostShift_ReachesResult_WithoutRewardOrSave()
        {
            var storage = new CountingStorage();
            ProfileSession profile = LoadedProfile(storage);
            LevelDefinition level = ConfigTestKit.Level(3);
            var completion = new ShiftCompletion(profile, level);
            int before = storage.Writes;
            int finishedCalls = 0;
            ShiftDriver driver = Started(3, r =>
            {
                finishedCalls++;
                completion.Apply(r);
            });

            for (int guard = 0; guard < 100000 && !driver.IsFinished; guard++)
            {
                driver.Frame(1.0);
            }

            Assert.IsTrue(driver.IsFinished);
            Assert.AreEqual(1, finishedCalls);
            Assert.IsFalse(driver.Result.Won, "без гравця рівень 3 не виконати");
            Assert.AreEqual(0, driver.Result.Stars);
            Assert.AreEqual(0L, profile.Data.Coins);
            Assert.AreEqual(0, profile.Data.LevelStars.Count);
            Assert.AreEqual(before, storage.Writes, "нічого не змінилось - нічого зберігати");
        }

        [Test]
        public void Completion_NeverLowersBestStars_AndPaysOnlyOnce()
        {
            ProfileSession profile = LoadedProfile(new InMemorySaveStorage());
            LevelDefinition level = ConfigTestKit.Level(1);
            profile.Data.LevelStars[level.Id] = 3;
            var won = new ShiftResult(ShiftOutcome.Won, 1, null, 0L, 20L, 20L, 3L, 0L, 0L, 0L, 1L);

            var completion = new ShiftCompletion(profile, level);
            Assert.IsTrue(completion.Apply(won));
            Assert.IsFalse(completion.Apply(won), "повторний запис відхилено");

            Assert.AreEqual(20L, profile.Data.Coins, "нагорода одноразова");
            Assert.AreEqual(3, profile.Data.LevelStars[level.Id], "кращі зірки не знижуються");
        }

        [Test]
        public void GoalDoneHint_ShowsBetweenGoalAndSecondStar_OnLevelWithoutEarlyEnd()
        {
            bool hintSeen;
            ShiftDriver driver = PlayWithBot(1, null, out hintSeen);

            Assert.IsFalse(driver.Level.Goal.EndOnGoal);
            Assert.IsTrue(hintSeen, "після 3-го клієнта (39 монет < 40) показується підказка про ★★");
            Assert.IsFalse(driver.GoalDoneStarHint, "по завершенні підказки немає");
        }

        [Test]
        public void ShiftSeeds_DifferPerAttempt_AndAreReproducible()
        {
            Assert.AreNotEqual(ShiftSeeds.Next(1700000000L, 1), ShiftSeeds.Next(1700000000L, 2));
            Assert.AreNotEqual(ShiftSeeds.Next(1700000000L, 1), ShiftSeeds.Next(1700000001L, 1));
            Assert.AreEqual(ShiftSeeds.Next(1700000000L, 1), ShiftSeeds.Next(1700000000L, 1));
            Assert.AreEqual(ShiftSeeds.Next(0L, 0), ShiftSeeds.Next(-5L, 0), "від'ємний годинник не ламає seed");
        }

        [TearDown]
        public void ResetContext()
        {
            GameContext.ResetForTests();
        }

        [Test]
        public void Context_SelectsLevels1To5_AndGivesNewSeedPerAttempt()
        {
            var context = new GameContext(new InMemorySaveStorage(), new ManualClock(1234), new PlaceholderLocalizer());
            Assert.AreEqual(0, context.SelectedLevelNumber);
            for (int n = 1; n <= 5; n++)
            {
                Assert.IsTrue(context.SelectLevel(n), "рівень " + n);
                Assert.AreEqual(n, context.SelectedLevelNumber);
            }

            Assert.IsFalse(context.SelectLevel(99));
            Assert.AreEqual(5, context.SelectedLevelNumber, "хибний вибір не міняє обраний рівень");

            ulong first = context.NextShiftSeed();
            ulong second = context.NextShiftSeed();
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(2, context.AttemptCount);
        }
    }
}
