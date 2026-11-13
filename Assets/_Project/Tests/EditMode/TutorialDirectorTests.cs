using System;
using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Навчання на живій зміні (S1-11): події, виведені зі стану симуляції, ведуть модель L01 до кінця;
    /// навчання нічого не змінює в ході зміни; плашка показує текст і підсвічує зону.
    /// </summary>
    public sealed class TutorialDirectorTests
    {
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        private static TutorialConfig Config()
        {
            return ResourceConfigSource.LoadTutorial().ValueOrThrow();
        }

        private static ShiftDriver Started(int levelNumber)
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(levelNumber), Seed);
            Assert.IsTrue(driver.Begin());
            return driver;
        }

        private static double Tick(ShiftDriver driver)
        {
            return 1.0 / driver.Level.Shift.TicksPerSecond;
        }

        private static void RunUntil(ShiftDriver driver, Action perFrame, Func<bool> done, string what, double maxSeconds)
        {
            double dt = Tick(driver);
            int guard = (int)(maxSeconds / dt);
            for (int i = 0; i < guard && !done(); i++)
            {
                Assert.IsFalse(driver.IsFinished, "зміна скінчилась раніше, ніж: " + what);
                driver.Frame(dt);
                perFrame();
            }

            Assert.IsTrue(done(), "не дочекались: " + what);
        }

        private static string CurrentId(TutorialDirector director)
        {
            return director.Current == null ? null : director.Current.Id;
        }

        [Test]
        public void Tracker_DerivesEvents_FromLiveShift()
        {
            ShiftDriver driver = Started(1);
            var input = new ShiftInput(driver);
            var tracker = new TutorialEventTracker(driver.Simulation);
            var all = new List<TutorialEvent>();
            var batch = new List<TutorialEvent>();
            Action observe = () =>
            {
                batch.Clear();
                tracker.Observe(batch);
                all.AddRange(batch);
            };

            driver.Frame(Tick(driver));
            observe();
            Assert.AreEqual(TutorialEventKind.ShiftStarted, all[0].Kind, "перша подія зміни");

            Assert.IsTrue(input.TapGrillSlot(0));
            Assert.IsTrue(input.TapStation(driver.Simulation.Board.Config.IndexOfIngredient("pita")));
            RunUntil(driver, observe, () => all.Exists(e => e.Kind == TutorialEventKind.MeatDone), "м'ясо готове", 30);
            Assert.IsTrue(all.Exists(e => e.Kind == TutorialEventKind.PlaceMeat));
            Assert.IsTrue(all.Exists(e => e.Kind == TutorialEventKind.AddIngredient && e.Ingredient == "pita"));
            Assert.Less(
                all.FindIndex(e => e.Kind == TutorialEventKind.PlaceMeat),
                all.FindIndex(e => e.Kind == TutorialEventKind.MeatDone),
                "спершу покладено, потім готове");

            RunUntil(driver, observe, () => driver.Simulation.Counter.Slots.GetCustomer(0) != CounterSlots.None, "клієнт у слоті", 60);
            Assert.IsTrue(all.Exists(e => e.Kind == TutorialEventKind.CustomerArrived && e.Customer == "olya"));
            Assert.IsTrue(all.Exists(e => e.Kind == TutorialEventKind.OrderShown && e.Customer == "olya"));

            Assert.IsTrue(input.TapGrillSlot(0), "зняти готове м'ясо");
            RunUntil(driver, observe, () => all.Exists(e => e.Kind == TutorialEventKind.TakeMeat), "м'ясо знято", 10);
            Assert.IsTrue(input.TapCustomer(0));
            RunUntil(driver, observe, () => all.Exists(e => e.Kind == TutorialEventKind.ServeDish), "страву видано", 10);
            Assert.AreEqual("olya", all.Find(e => e.Kind == TutorialEventKind.ServeDish).Customer);
            Assert.AreEqual(1L, driver.Simulation.ServedCount);
        }

        [Test]
        public void Tracker_ReportsBurntMeat_AndDiscard()
        {
            ShiftDriver driver = Started(1);
            var input = new ShiftInput(driver);
            var tracker = new TutorialEventTracker(driver.Simulation);
            var all = new List<TutorialEvent>();
            var batch = new List<TutorialEvent>();
            Action observe = () =>
            {
                batch.Clear();
                tracker.Observe(batch);
                all.AddRange(batch);
            };

            driver.Frame(Tick(driver));
            observe();
            Assert.IsTrue(input.TapGrillSlot(0));
            RunUntil(driver, observe, () => all.Exists(e => e.Kind == TutorialEventKind.MeatBurnt), "м'ясо пригоріло", 90);
            bool tapped = false;
            RunUntil(driver, observe, () => tapped || (tapped = input.TapGrillSlot(0)), "тап по пригорілому", 5);
            RunUntil(driver, observe, () => all.Exists(e => e.Kind == TutorialEventKind.DiscardBurntMeat), "пригоріле викинуто", 5);

            Assert.IsTrue(all.Exists(e => e.Kind == TutorialEventKind.MeatDone), "перед пригоранням м'ясо було готове");
        }

        [Test]
        public void Director_ForSlowPlayer_ShowsL01StepsInOrder_OneAtATime()
        {
            ShiftDriver driver = Started(1);
            var input = new ShiftInput(driver);
            var profile = new SaveData();
            TutorialDirector director = TutorialDirector.Create(driver, Config(), profile);
            var shown = new List<string>();
            Action frame = () =>
            {
                director.Update();
                string id = CurrentId(director);
                if (id != null && (shown.Count == 0 || shown[shown.Count - 1] != id))
                {
                    shown.Add(id);
                }

                int active = 0;
                for (int i = 0; i < director.Model.StepCount; i++)
                {
                    active += director.Model.GetState(director.Model.GetStep(i).Id) == TutorialStepState.Active ? 1 : 0;
                }

                Assert.LessOrEqual(active, 1, "один крок одночасно");
            };

            driver.Frame(Tick(driver));
            frame();
            Assert.AreEqual("l01.step1", CurrentId(director), "перша підказка одразу на початку зміни");
            Assert.IsTrue(director.Current.ShowWhen.Kind == TutorialEventKind.ShiftStarted);

            Assert.IsTrue(input.TapGrillSlot(0));
            RunUntil(driver, frame, () => CurrentId(director) == "l01.step2", "підказка про піту", 15);

            Assert.IsTrue(input.TapStation(driver.Simulation.Board.Config.IndexOfIngredient("pita")));
            RunUntil(driver, frame, () => CurrentId(director) == "l01.step3", "підказка зняти м'ясо", 30);

            Assert.IsTrue(input.TapGrillSlot(0));
            RunUntil(driver, frame, () => CurrentId(director) == "l01.step4", "підказка про видачу", 15);

            RunUntil(driver, frame, () => driver.Simulation.Counter.Slots.GetCustomer(0) != CounterSlots.None, "клієнт у слоті", 60);
            Assert.IsTrue(input.TapCustomer(0));
            RunUntil(driver, frame, () => director.Model.GetState("l01.step4") == TutorialStepState.Done, "видача завершила навчання", 15);

            CollectionAssert.AreEqual(new[] { "l01.step1", "l01.step2", "l01.step3", "l01.step4" }, shown);
            Assert.IsNull(director.Current);
            Assert.IsTrue(director.Model.Dirty);
            Assert.AreEqual(4, profile.TutorialSeen.Count);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Tutorial_NeverChangesTheShift_BotRunsAreIdentical(int levelNumber)
        {
            ShiftDriver plain = Started(levelNumber);
            ShiftDriver taught = Started(levelNumber);
            TutorialDirector director = TutorialDirector.Create(taught, Config(), new SaveData());
            var botPlain = new UiBot(plain, new InputTaps(new ShiftInput(plain)));
            var botTaught = new UiBot(taught, new InputTaps(new ShiftInput(taught)));
            double dt = Tick(plain);
            var shown = new List<string>();
            for (int guard = 0; guard < 400000 && (!plain.IsFinished || !taught.IsFinished); guard++)
            {
                botPlain.Step();
                botTaught.Step();
                plain.Frame(dt);
                taught.Frame(dt);
                director.Update();
                if (director.Current != null && (shown.Count == 0 || shown[shown.Count - 1] != director.Current.Id))
                {
                    shown.Add(director.Current.Id);
                }
            }

            Assert.IsTrue(plain.IsFinished && taught.IsFinished);
            Assert.AreEqual(plain.Result.Outcome, taught.Result.Outcome);
            Assert.AreEqual(plain.Result.Stars, taught.Result.Stars);
            Assert.AreEqual(plain.Simulation.ElapsedTicks, taught.Simulation.ElapsedTicks);
            Assert.IsTrue(plain.Simulation.CaptureSnapshot().Equals(taught.Simulation.CaptureSnapshot()), "навчання не змінює стан зміни");
            if (levelNumber == 1)
            {
                Assert.AreEqual("l01.step1", shown[0], "підказки на L01 починаються з першого кроку");
                Assert.AreEqual(TutorialStepState.Done, director.Model.GetState("l01.step4"), "бот довів L01 до видачі");
            }
        }

        [Test]
        public void Director_TimeFollowsShiftClock_PauseFreezesHints()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = TutorialDirector.Create(driver, Config(), new SaveData());
            var input = new ShiftInput(driver);
            driver.Frame(Tick(driver));
            director.Update();
            Assert.IsTrue(input.TapGrillSlot(0));
            RunUntil(driver, director.Update, () => director.Model.GetState("l01.step1") == TutorialStepState.Done, "крок 1 завершено", 5);
            Assert.IsNull(director.Current);

            driver.SetPaused(true);
            long frozen = director.NowMs;
            for (int i = 0; i < 600; i++)
            {
                driver.Frame(1.0);
                director.Update();
            }

            Assert.AreEqual(frozen, director.NowMs, "на паузі час зміни стоїть");
            Assert.IsNull(director.Current, "на паузі наступна підказка не з'являється");
        }

        [Test]
        public void Overlay_ShowsTextOfCurrentStep_HighlightsZone_AndHidesAfterSkip()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = TutorialDirector.Create(driver, Config(), new SaveData());
            var changed = 0;
            var screen = new ShiftScreen(
                new VisualElement(), driver, Text, () => { }, () => { }, () => { }, null, null, false, director, () => changed++);
            Assert.IsNotNull(screen.Tutorial);
            Assert.IsTrue(screen.Tutorial.IsVisible, "зміна вже йде: перша підказка видна одразу після побудови екрана");

            driver.Frame(Tick(driver));
            screen.Refresh();

            Assert.IsTrue(screen.Tutorial.IsVisible);
            Assert.AreEqual("l01.step1", screen.Tutorial.ShownStepId);
            Assert.AreEqual(Text.Get(TextKeys.TutorialL01Step1), screen.Tutorial.Text);
            Assert.AreEqual(TutorialZone.Grill, screen.Tutorial.HighlightedZone);
            Assert.IsTrue(screen.Tutorial.NextButtonVisible);
            Assert.IsTrue(screen.Tutorial.SkipAllButtonVisible);

            Assert.IsTrue(director.SkipStep());
            screen.Refresh();
            Assert.IsFalse(screen.Tutorial.IsVisible, "після пропуску плашки немає");
            Assert.AreEqual(TutorialZone.None, screen.Tutorial.HighlightedZone);
            Assert.AreEqual(string.Empty, screen.Tutorial.Text);
        }

        [Test]
        public void Overlay_HighlightIsRemoved_WhenStepEnds_AndMovesToNewZone()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = TutorialDirector.Create(driver, Config(), new SaveData());
            var input = new ShiftInput(driver);
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { }, null, null, false, director);
            VisualElement assembly = screen.AssemblyZone;
            StyleFloat before = assembly.style.borderLeftWidth;

            driver.Frame(Tick(driver));
            screen.Refresh();
            Assert.AreEqual(TutorialZone.Grill, screen.Tutorial.HighlightedZone);
            Assert.IsTrue(input.TapGrillSlot(0));
            RunUntil(driver, screen.Refresh, () => screen.Tutorial.ShownStepId == "l01.step2", "підказка про піту", 15);

            Assert.AreEqual(TutorialZone.Assembly, screen.Tutorial.HighlightedZone, "піта лежить у зоні збирання");
            Assert.AreNotEqual(before, assembly.style.borderLeftWidth, "зону збирання підсвічено");
            Assert.AreEqual(Text.Get(TextKeys.TutorialL01Step2), screen.Tutorial.Text);

            Assert.IsTrue(input.TapStation(driver.Simulation.Board.Config.IndexOfIngredient("pita")));
            RunUntil(driver, screen.Refresh, () => !screen.Tutorial.IsVisible, "крок завершено", 5);
            Assert.AreEqual(before, assembly.style.borderLeftWidth, "підсвітку знято, рамка як була");
        }

        [Test]
        public void Overlay_AbsentWithoutTutorial_AndScreenWorksAsBefore()
        {
            ShiftDriver driver = Started(1);
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });

            Assert.IsNull(screen.Tutorial);
            driver.Frame(Tick(driver));
            Assert.DoesNotThrow(() => screen.Refresh());
        }
    }
}
