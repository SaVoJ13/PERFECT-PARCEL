using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Команди й пауза. Правило: команда в неправильному стані повертає false
    /// і не змінює жодного поля стану (перевіряється знімком).
    /// </summary>
    public sealed class ShiftSimulationCommandTests
    {
        public enum Setup
        {
            Intro,
            Running,
            Paused,
            Won,
            Lost,
            LostAfterContinue,
        }

        public enum Command
        {
            Start,
            Pause,
            Resume,
            Continue,
        }

        private static ShiftSimulation Arrange(Setup setup)
        {
            ShiftSimulation shift = ShiftTestKit.Create(goalAchieved: setup == Setup.Won);
            if (setup == Setup.Intro)
            {
                return shift;
            }

            shift.Start();
            shift.Advance(1.5 / ShiftTestKit.TicksPerSecond);
            switch (setup)
            {
                case Setup.Paused:
                    shift.Pause();
                    break;
                case Setup.Won:
                case Setup.Lost:
                    ShiftTestKit.TickUntilNotRunning(shift);
                    break;
                case Setup.LostAfterContinue:
                    ShiftTestKit.TickUntilNotRunning(shift);
                    shift.Continue();
                    ShiftTestKit.TickUntilNotRunning(shift);
                    break;
            }

            return shift;
        }

        private static bool Send(ShiftSimulation shift, Command command)
        {
            switch (command)
            {
                case Command.Start:
                    return shift.Start();
                case Command.Pause:
                    return shift.Pause();
                case Command.Resume:
                    return shift.Resume();
                default:
                    return shift.Continue();
            }
        }

        private static ShiftState ExpectedState(Setup setup)
        {
            switch (setup)
            {
                case Setup.Intro:
                    return ShiftState.Intro;
                case Setup.Running:
                    return ShiftState.Running;
                case Setup.Paused:
                    return ShiftState.Paused;
                case Setup.Won:
                    return ShiftState.Won;
                default:
                    return ShiftState.Lost;
            }
        }

        [TestCase(Setup.Intro, Command.Start, ShiftState.Running)]
        [TestCase(Setup.Running, Command.Pause, ShiftState.Paused)]
        [TestCase(Setup.Paused, Command.Resume, ShiftState.Running)]
        [TestCase(Setup.Lost, Command.Continue, ShiftState.Running)]
        public void ValidCommand_IsAccepted_AndChangesState(Setup setup, Command command, ShiftState expected)
        {
            ShiftSimulation shift = Arrange(setup);
            Assert.AreEqual(ExpectedState(setup), shift.State, "Підготовка тесту");

            Assert.IsTrue(Send(shift, command));

            Assert.AreEqual(expected, shift.State);
        }

        [TestCase(Setup.Intro, Command.Pause)]
        [TestCase(Setup.Intro, Command.Resume)]
        [TestCase(Setup.Intro, Command.Continue)]
        [TestCase(Setup.Running, Command.Start)]
        [TestCase(Setup.Running, Command.Resume)]
        [TestCase(Setup.Running, Command.Continue)]
        [TestCase(Setup.Paused, Command.Start)]
        [TestCase(Setup.Paused, Command.Pause)]
        [TestCase(Setup.Paused, Command.Continue)]
        [TestCase(Setup.Won, Command.Start)]
        [TestCase(Setup.Won, Command.Pause)]
        [TestCase(Setup.Won, Command.Resume)]
        [TestCase(Setup.Won, Command.Continue)]
        [TestCase(Setup.Lost, Command.Start)]
        [TestCase(Setup.Lost, Command.Pause)]
        [TestCase(Setup.Lost, Command.Resume)]
        [TestCase(Setup.LostAfterContinue, Command.Start)]
        [TestCase(Setup.LostAfterContinue, Command.Pause)]
        [TestCase(Setup.LostAfterContinue, Command.Resume)]
        [TestCase(Setup.LostAfterContinue, Command.Continue)]
        public void CommandInWrongState_ReturnsFalse_AndChangesNothing(Setup setup, Command command)
        {
            ShiftSimulation shift = Arrange(setup);
            Assert.AreEqual(ExpectedState(setup), shift.State, "Підготовка тесту");
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(Send(shift, command));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void TimeDoesNotRun_BeforeStart()
        {
            ShiftSimulation shift = ShiftTestKit.Create();
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.Tick());
            Assert.AreEqual(0, shift.Advance(3d));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void Pause_FreezesTime_AndResumeContinuesFromSameTick()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning();
            for (int i = 0; i < 100; i++)
            {
                shift.Tick();
            }

            Assert.IsTrue(shift.Pause());
            ShiftSnapshot paused = shift.CaptureSnapshot();

            for (int i = 0; i < 50; i++)
            {
                Assert.IsFalse(shift.Tick());
                Assert.AreEqual(0, shift.Advance(1d));
            }

            Assert.AreEqual(paused, shift.CaptureSnapshot(), "У паузі стан не змінюється");
            Assert.AreEqual(100L, shift.ElapsedTicks);

            Assert.IsTrue(shift.Resume());
            long rest = ShiftTestKit.TickUntilNotRunning(shift);

            Assert.AreEqual(shift.Config.DurationTicks - 100L, rest, "Пауза не вкорочує і не подовжує зміну");
            Assert.AreEqual(ShiftState.Won, shift.State);
        }

        [Test]
        public void Pause_KeepsPartialFrameTime_ButIgnoresTimeSpentPaused()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning();
            shift.Advance(0.5 / ShiftTestKit.TicksPerSecond);

            shift.Pause();
            shift.Advance(3d);
            shift.Resume();

            Assert.AreEqual(0L, shift.ElapsedTicks);
            Assert.AreEqual(1, shift.Advance(0.5 / ShiftTestKit.TicksPerSecond));
            Assert.AreEqual(1L, shift.ElapsedTicks);
        }

        [Test]
        public void RepeatedPauseAndResume_DoNotLoseOrAddTicks()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning();
            long ticks = 0;

            while (shift.State != ShiftState.Won)
            {
                if (shift.Tick())
                {
                    ticks++;
                }

                shift.Pause();
                shift.Pause();
                shift.Tick();
                shift.Resume();
                shift.Resume();
            }

            Assert.AreEqual(shift.Config.DurationTicks, ticks);
            Assert.AreEqual(shift.Config.DurationTicks, shift.ElapsedTicks);
        }

        [Test]
        public void DuringResolving_AllCommandsAndTimeAreRejected()
        {
            var goal = new MeddlingGoal();
            var shift = new ShiftSimulation(ShiftTestKit.Config(), ShiftTestKit.Seed, goal);
            shift.Start();

            ShiftTestKit.TickUntilNotRunning(shift);

            Assert.IsTrue(goal.WasCalled);
            Assert.IsFalse(goal.AnythingAccepted, "У Resolving жодна команда не має спрацювати");
            Assert.AreEqual(ShiftState.Lost, shift.State);
            Assert.AreEqual(shift.Config.DurationTicks, shift.ElapsedTicks);
            Assert.IsFalse(shift.ContinueUsed);
        }

        /// <summary>Ціль, яка під час перевірки намагається керувати зміною.</summary>
        private sealed class MeddlingGoal : IShiftGoal
        {
            public bool WasCalled { get; private set; }

            public bool AnythingAccepted { get; private set; }

            public bool IsAchieved(ShiftSimulation shift)
            {
                WasCalled = true;
                AnythingAccepted = shift.Start() | shift.Pause() | shift.Resume() | shift.Continue()
                    | shift.Tick() | shift.Advance(1d) != 0;
                return false;
            }
        }
    }
}
