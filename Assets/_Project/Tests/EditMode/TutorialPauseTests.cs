using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Q-05k (ADR-031): поки показано підказку навчання, зміна стоїть; закриття підказки («Далі», «Пропустити все»)
    /// і перший тап по грі на кроці з дією її знімають; пауза гравця з паузою підказки не плутається.
    /// </summary>
    public sealed class TutorialPauseTests
    {
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        private float _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = 100f;
            UiMotion.Clock = () => _clock;
            UiMotion.Reduced = false;
        }

        [TearDown]
        public void TearDown()
        {
            UiMotion.Clock = null;
            UiMotion.Reduced = false;
        }

        private static ShiftDriver Started(int level)
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(level), Seed);
            Assert.IsTrue(driver.Begin());
            return driver;
        }

        private static TutorialDirector Director(ShiftDriver driver)
        {
            return TutorialDirector.Create(driver, ResourceConfigSource.LoadTutorial().ValueOrThrow(), new SaveData());
        }

        private static double Tick(ShiftDriver driver)
        {
            return 1.0 / driver.Level.Shift.TicksPerSecond;
        }

        /// <summary>Один кадр у порядку контролера: пауза підказки, крок симуляції, навчання.</summary>
        private static void Frame(ShiftDriver driver, TutorialDirector director, TutorialPause pause, double dt)
        {
            pause.Update();
            driver.Frame(dt);
            director.Update();
        }

        private static void ShowFirstStep(ShiftDriver driver, TutorialDirector director, TutorialPause pause)
        {
            Frame(driver, director, pause, Tick(driver));
            Assert.AreEqual("l01.step1", director.Current.Id, "підказка з'явилась на початку зміни");
            pause.Update();
        }

        [Test]
        public void ShownTip_FreezesShift_NothingAdvances()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = Director(driver);
            var pause = new TutorialPause(driver, director);
            ShowFirstStep(driver, director, pause);
            Assert.IsTrue(driver.IsTutorialPaused);

            long ticks = driver.Simulation.ElapsedTicks;
            for (int i = 0; i < 1200; i++)
            {
                Frame(driver, director, pause, 1.0);
            }

            Assert.AreEqual(ticks, driver.Simulation.ElapsedTicks, "такти не йдуть, поки підказка на екрані: таймер, терпіння, гриль стоять");
            Assert.AreEqual("l01.step1", director.Current.Id, "підказка не зникла сама");
        }

        [Test]
        public void Next_ClosesTip_ShiftContinuesFromSameMoment()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = Director(driver);
            var pause = new TutorialPause(driver, director);
            ShowFirstStep(driver, director, pause);
            long ticks = driver.Simulation.ElapsedTicks;

            Assert.IsTrue(director.SkipStep());
            for (int i = 0; i < 20; i++)
            {
                Frame(driver, director, pause, Tick(driver));
            }

            Assert.IsFalse(driver.IsTutorialPaused);
            Assert.IsFalse(driver.IsPaused);
            Assert.Greater(driver.Simulation.ElapsedTicks, ticks, "час пішов далі");
            Assert.LessOrEqual(driver.Simulation.ElapsedTicks - ticks, 20L, "без стрибка: не більше, ніж кадрів");
        }

        [Test]
        public void SkipAll_ReleasesPause()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = Director(driver);
            var pause = new TutorialPause(driver, director);
            ShowFirstStep(driver, director, pause);

            Assert.IsTrue(director.SkipAll());
            for (int i = 0; i < 10; i++)
            {
                Frame(driver, director, pause, Tick(driver));
            }

            Assert.IsFalse(driver.IsPaused);
            Assert.IsNull(director.Current);
        }

        [Test]
        public void ActionStep_FirstTapOnGameReleasesPause_AndActionHappens()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = Director(driver);
            var pause = new TutorialPause(driver, director);
            ShowFirstStep(driver, director, pause);
            var input = new ShiftInput(driver);

            Assert.IsTrue(input.TapGrillSlot(0), "тап по грилю проходить, хоча підказка зупинила час");
            Assert.IsFalse(driver.IsTutorialPaused);
            int guard = (int)(10 / Tick(driver));
            for (int i = 0; i < guard && director.Model.GetState("l01.step1") != TutorialStepState.Done; i++)
            {
                Frame(driver, director, pause, Tick(driver));
            }

            Assert.AreEqual(TutorialStepState.Done, director.Model.GetState("l01.step1"), "дію зараховано");
        }

        [Test]
        public void ReleasedStep_DoesNotPauseAgain()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = Director(driver);
            var pause = new TutorialPause(driver, director);
            ShowFirstStep(driver, director, pause);
            Assert.IsTrue(new ShiftInput(driver).TapGrillSlot(0));
            for (int i = 0; i < 3; i++)
            {
                pause.Update();
            }

            Assert.IsFalse(driver.IsPaused, "після тапу на цьому кроці пауза вдруге не вмикається");
        }

        [Test]
        public void PlayerPause_IsNotLiftedByClosingTip_AndTipPauseNotLiftedByResume()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = Director(driver);
            var pause = new TutorialPause(driver, director);
            ShowFirstStep(driver, director, pause);

            driver.SetPaused(true);
            Assert.IsTrue(director.SkipStep());
            pause.Update();
            Assert.IsFalse(driver.IsTutorialPaused, "підказку закрито");
            Assert.IsTrue(driver.IsPaused, "пауза гравця лишилась");
            long ticks = driver.Simulation.ElapsedTicks;
            driver.Frame(1.0);
            Assert.AreEqual(ticks, driver.Simulation.ElapsedTicks);
            driver.SetPaused(false);
            Assert.IsFalse(driver.IsPaused);

            ShiftDriver second = Started(1);
            TutorialDirector director2 = Director(second);
            var pause2 = new TutorialPause(second, director2);
            ShowFirstStep(second, director2, pause2);
            second.SetPaused(true);
            second.SetPaused(false);
            Assert.IsTrue(second.IsPaused, "гравець зняв свою паузу, а підказка ще на екрані: гра стоїть");
            Assert.IsTrue(second.IsTutorialPaused);
        }

        [Test]
        public void TipPause_DoesNotShowPausePanel_AndFreezesUiMotion()
        {
            ShiftDriver driver = Started(1);
            TutorialDirector director = Director(driver);
            var pause = new TutorialPause(driver, director);
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { }, null, null, false, director);
            Frame(driver, director, pause, Tick(driver));
            pause.Update();
            screen.Refresh();

            Assert.IsTrue(driver.IsTutorialPaused);
            Assert.IsFalse(screen.PausePanelVisible, "панель паузи лише для паузи гравця");
            float frozen = UiMotion.Now;
            _clock += 5f;
            Assert.AreEqual(frozen, UiMotion.Now, 0.001f, "анімації ігрового часу на паузі");

            Assert.IsTrue(director.SkipStep());
            pause.Update();
            screen.Refresh();
            _clock += 1f;
            Assert.AreEqual(frozen + 1f, UiMotion.Now, 0.001f, "після закриття підказки рух іде без стрибка");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ExpertUiBot_WithTutorialPause_StillFinishesShift(int levelNumber)
        {
            ShiftDriver driver = Started(levelNumber);
            TutorialDirector director = Director(driver);
            var pause = new TutorialPause(driver, director);
            var bot = new UiBot(driver, new InputTaps(new ShiftInput(driver)));
            double dt = Tick(driver);
            int readingFrames = 0;
            for (int guard = 0; guard < 800000 && !driver.IsFinished; guard++)
            {
                bot.Step();
                Frame(driver, director, pause, dt);

                // Гравець прочитав підказку без дії в грі й натиснув «Далі» (бот сам цього не робить).
                readingFrames = driver.IsTutorialPaused ? readingFrames + 1 : 0;
                if (readingFrames > 60)
                {
                    Assert.IsTrue(director.SkipStep());
                    readingFrames = 0;
                }
            }

            Assert.IsTrue(driver.IsFinished, "зміна дійшла до результату: підказки не заблокували гру");
            if (levelNumber == 1)
            {
                Assert.IsTrue(driver.Result.Won, "L01 бот проходить із паузою навчання");
            }
        }
    }
}
