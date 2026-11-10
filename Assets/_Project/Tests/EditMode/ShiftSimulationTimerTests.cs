using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Таймер зміни в цілих тактах, перехід у Won/Lost і продовження після поразки.</summary>
    public sealed class ShiftSimulationTimerTests
    {
        private const long DurationTicks = ShiftTestKit.DurationSec * ShiftTestKit.TicksPerSecond;
        private const long BonusTicks = ShiftTestKit.ContinueBonusSec * ShiftTestKit.TicksPerSecond;

        [Test]
        public void NewShift_IsInIntro_WithFullTimer()
        {
            ShiftSimulation shift = ShiftTestKit.Create(seed: 77UL);

            Assert.AreEqual(ShiftState.Intro, shift.State);
            Assert.AreEqual(0L, shift.ElapsedTicks);
            Assert.AreEqual(DurationTicks, shift.RemainingTicks);
            Assert.IsFalse(shift.ContinueUsed);
            Assert.AreEqual(77UL, shift.Seed);
            Assert.AreEqual(
                new ShiftSnapshot(ShiftState.Intro, 0L, DurationTicks, 0L, false, 77UL),
                shift.CaptureSnapshot());
        }

        [Test]
        public void Timer_EndsExactlyOnExpectedTick()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning();

            for (long tick = 1; tick < DurationTicks; tick++)
            {
                Assert.IsTrue(shift.Tick());
            }

            Assert.AreEqual(ShiftState.Running, shift.State, "За такт до кінця зміна ще триває");
            Assert.AreEqual(1L, shift.RemainingTicks);

            Assert.IsTrue(shift.Tick());

            Assert.AreEqual(ShiftState.Won, shift.State);
            Assert.AreEqual(DurationTicks, shift.ElapsedTicks);
            Assert.AreEqual(0L, shift.RemainingTicks);
        }

        [TestCase(10, 1)]
        [TestCase(30, 90)]
        [TestCase(50, 7)]
        [TestCase(240, 3)]
        public void Timer_LengthIsDurationTimesTickRate(int ticksPerSecond, int durationSec)
        {
            ShiftConfig config = ShiftTestKit.Config(durationSec: durationSec, ticksPerSecond: ticksPerSecond);
            ShiftSimulation shift = ShiftTestKit.CreateRunning(config: config);

            long ticks = ShiftTestKit.TickUntilNotRunning(shift);

            Assert.AreEqual((long)ticksPerSecond * durationSec, ticks);
            Assert.AreEqual(ticks, shift.ElapsedTicks);
        }

        [Test]
        public void RemainingTime_DecreasesByOneEveryTick_AndNeverGoesNegative()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning();
            long previous = shift.RemainingTicks;

            while (shift.Tick())
            {
                Assert.AreEqual(previous - 1, shift.RemainingTicks);
                Assert.AreEqual(DurationTicks, shift.ElapsedTicks + shift.RemainingTicks);
                previous = shift.RemainingTicks;
            }

            Assert.AreEqual(0L, previous);
            Assert.AreEqual(0L, shift.RemainingTicks);
        }

        [Test]
        public void AfterTheEnd_TickAndAdvanceDoNothing()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning();
            ShiftTestKit.TickUntilNotRunning(shift);
            ShiftSnapshot finished = shift.CaptureSnapshot();

            Assert.IsFalse(shift.Tick());
            Assert.AreEqual(0, shift.Advance(5d));
            Assert.AreEqual(finished, shift.CaptureSnapshot());
        }

        [Test]
        public void GoalAchieved_ShiftIsWon()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning(goalAchieved: true);

            ShiftTestKit.TickUntilNotRunning(shift);

            Assert.AreEqual(ShiftState.Won, shift.State);
        }

        [Test]
        public void GoalNotAchieved_ShiftIsLost()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning(goalAchieved: false);

            ShiftTestKit.TickUntilNotRunning(shift);

            Assert.AreEqual(ShiftState.Lost, shift.State);
        }

        [Test]
        public void Goal_IsEvaluatedOnce_InResolvingState_WhenTimeIsUp()
        {
            var goal = new FixedGoal(true);
            var shift = new ShiftSimulation(ShiftTestKit.Config(), ShiftTestKit.Seed, goal);
            shift.Start();

            for (long tick = 1; tick < DurationTicks; tick++)
            {
                shift.Tick();
            }

            Assert.AreEqual(0, goal.Calls, "До кінця часу ціль не перевіряється");

            shift.Tick();

            Assert.AreEqual(1, goal.Calls);
            Assert.AreEqual(ShiftState.Resolving, goal.StateSeen);
            Assert.AreEqual(0L, goal.RemainingTicksSeen);
        }

        [Test]
        public void Continue_AfterLoss_ResumesWithBonusTime()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning(goalAchieved: false);
            ShiftTestKit.TickUntilNotRunning(shift);

            Assert.IsTrue(shift.Continue());

            Assert.AreEqual(ShiftState.Running, shift.State);
            Assert.IsTrue(shift.ContinueUsed);
            Assert.AreEqual(BonusTicks, shift.RemainingTicks);
            Assert.AreEqual(DurationTicks, shift.ElapsedTicks, "Відпрацьований час не скидається");

            long extraTicks = ShiftTestKit.TickUntilNotRunning(shift);

            Assert.AreEqual(BonusTicks, extraTicks);
            Assert.AreEqual(DurationTicks + BonusTicks, shift.ElapsedTicks);
            Assert.AreEqual(ShiftState.Lost, shift.State);
        }

        [Test]
        public void Continue_WorksOnlyOncePerAttempt()
        {
            ShiftSimulation shift = ShiftTestKit.CreateRunning(goalAchieved: false);
            ShiftTestKit.TickUntilNotRunning(shift);
            Assert.IsTrue(shift.Continue());
            ShiftTestKit.TickUntilNotRunning(shift);
            ShiftSnapshot lostAgain = shift.CaptureSnapshot();

            Assert.IsFalse(shift.Continue());

            Assert.AreEqual(ShiftState.Lost, shift.State);
            Assert.AreEqual(lostAgain, shift.CaptureSnapshot());
        }

        [Test]
        public void Continue_ReevaluatesGoal_AndCanEndInWin()
        {
            var goal = new FixedGoal(false);
            var shift = new ShiftSimulation(ShiftTestKit.Config(), ShiftTestKit.Seed, goal);
            shift.Start();
            ShiftTestKit.TickUntilNotRunning(shift);
            shift.Continue();
            goal.Achieved = true;

            ShiftTestKit.TickUntilNotRunning(shift);

            Assert.AreEqual(2, goal.Calls);
            Assert.AreEqual(ShiftState.Won, shift.State);
            Assert.IsFalse(shift.Continue(), "Після перемоги продовження немає");
        }

        [Test]
        public void Continue_IsUnavailable_WhenBonusIsZero()
        {
            ShiftConfig config = ShiftTestKit.Config(continueBonusSec: 0);
            ShiftSimulation shift = ShiftTestKit.CreateRunning(goalAchieved: false, config: config);
            ShiftTestKit.TickUntilNotRunning(shift);
            ShiftSnapshot lost = shift.CaptureSnapshot();

            Assert.IsFalse(shift.Continue());

            Assert.AreEqual(lost, shift.CaptureSnapshot());
            Assert.IsFalse(shift.ContinueUsed);
        }
    }
}
