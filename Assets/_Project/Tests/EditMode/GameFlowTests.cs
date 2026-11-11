using System;
using System.Reflection;
using KebabStreet.Core;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Каркас гри (E0-05): переходи між екранами, тексти, порожня зміна, композиція.</summary>
    public sealed class GameFlowTests
    {
        [TearDown]
        public void ResetContext()
        {
            GameContext.ResetForTests();
        }

        [Test]
        public void Flow_StartsAtBoot_AndWalksBootMenuShiftMenu()
        {
            var flow = new GameFlow();
            Assert.AreEqual(GameScreen.Boot, flow.Current);
            Assert.IsTrue(flow.BootCompleted());
            Assert.AreEqual(GameScreen.Menu, flow.Current);
            Assert.IsTrue(flow.Play());
            Assert.AreEqual(GameScreen.Shift, flow.Current);
            Assert.IsTrue(flow.ExitToMenu());
            Assert.AreEqual(GameScreen.Menu, flow.Current);
        }

        [Test]
        public void Flow_InvalidTransitions_AreRejectedAndKeepScreen()
        {
            var flow = new GameFlow();
            Assert.IsFalse(flow.Play(), "з Boot у зміну напряму не можна");
            Assert.IsFalse(flow.ExitToMenu());
            Assert.AreEqual(GameScreen.Boot, flow.Current);

            flow.BootCompleted();
            Assert.IsFalse(flow.BootCompleted(), "повторне завершення Boot");
            Assert.IsFalse(flow.ExitToMenu(), "вихід із меню");
            Assert.AreEqual(GameScreen.Menu, flow.Current);

            flow.Play();
            Assert.IsFalse(flow.Play(), "повторний старт зміни");
            Assert.AreEqual(GameScreen.Shift, flow.Current);
        }

        [Test]
        public void SceneOf_GivesThreeDistinctNames()
        {
            Assert.AreEqual("Boot", GameFlow.SceneOf(GameScreen.Boot));
            Assert.AreEqual("Menu", GameFlow.SceneOf(GameScreen.Menu));
            Assert.AreEqual("Shift", GameFlow.SceneOf(GameScreen.Shift));
        }

        [Test]
        public void Localizer_ResolvesEveryTextKey_AndMarksUnknownOnes()
        {
            var localizer = new PlaceholderLocalizer();
            int checkedKeys = 0;
            foreach (FieldInfo field in typeof(TextKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var key = (string)field.GetRawConstantValue();
                string text = localizer.Get(key);
                Assert.IsFalse(string.IsNullOrEmpty(text), key);
                Assert.AreNotEqual("[" + key + "]", text, "немає рядка для ключа " + key);
                checkedKeys++;
            }

            Assert.Greater(checkedKeys, 0);
            Assert.AreEqual("[nope]", localizer.Get("nope"));
            Assert.AreEqual("[]", localizer.Get(string.Empty));
        }

        [Test]
        public void Localizer_TimeTemplate_TakesOneArgument()
        {
            string text = string.Format(new PlaceholderLocalizer().Get(TextKeys.ShiftTimeLeft), "1:30");
            StringAssert.Contains("1:30", text);
        }

        [TestCase(0L, "0:00")]
        [TestCase(5L, "0:05")]
        [TestCase(59L, "0:59")]
        [TestCase(60L, "1:00")]
        [TestCase(90L, "1:30")]
        [TestCase(-3L, "0:00")]
        public void FormatClock_ShowsMinutesAndSeconds(long seconds, string expected)
        {
            Assert.AreEqual(expected, EmptyShift.FormatClock(seconds));
        }

        [Test]
        public void EmptyShift_TimerRunsAndRoundsUp()
        {
            ShiftSimulation shift = EmptyShift.Create(1UL);
            Assert.AreEqual(EmptyShift.DurationSec, EmptyShift.RemainingSeconds(shift));

            Assert.IsTrue(shift.Start());
            shift.Tick();
            Assert.AreEqual(EmptyShift.DurationSec, EmptyShift.RemainingSeconds(shift), "частина секунди округлюється вгору");

            shift.Advance(1.0);
            Assert.AreEqual(EmptyShift.DurationSec - 1, EmptyShift.RemainingSeconds(shift));
        }

        [Test]
        public void EmptyShift_Config_IsValid()
        {
            Assert.AreEqual(0, EmptyShift.CreateConfig().GetErrors().Count);
        }

        [Test]
        public void Context_Install_AcceptsOnlyOneContext()
        {
            Assert.IsNull(GameContext.Current);
            var first = new GameContext(new InMemorySaveStorage(), new ManualClock(1000), new PlaceholderLocalizer());
            GameContext.Install(first);
            Assert.AreSame(first, GameContext.Current);

            var second = new GameContext(new InMemorySaveStorage(), new ManualClock(1000), new PlaceholderLocalizer());
            Assert.Throws<InvalidOperationException>(() => GameContext.Install(second));
            Assert.AreSame(first, GameContext.Current, "SaveService лишається один");
        }

        [Test]
        public void Context_NewShift_UsesClockSeed_AndToleratesNegativeClock()
        {
            var context = new GameContext(new InMemorySaveStorage(), new ManualClock(1234), new PlaceholderLocalizer());
            Assert.AreEqual(1234UL, context.NewShift().Seed);

            var negative = new GameContext(new InMemorySaveStorage(), new ManualClock(-50), new PlaceholderLocalizer());
            Assert.AreEqual(0UL, negative.NewShift().Seed);
        }

        [Test]
        public void Context_RejectsMissingDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new GameContext(null, new ManualClock(1), new PlaceholderLocalizer()));
            Assert.Throws<ArgumentNullException>(() => new GameContext(new InMemorySaveStorage(), null, new PlaceholderLocalizer()));
            Assert.Throws<ArgumentNullException>(() => new GameContext(new InMemorySaveStorage(), new ManualClock(1), null));
        }
    }
}
