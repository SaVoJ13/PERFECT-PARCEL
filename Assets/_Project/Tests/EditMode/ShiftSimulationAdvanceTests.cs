using System;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Advance: реальний час кадрів → цілі такти. Порівнюються лише такти і знімки (цілі числа).
    /// Зміна триває 20 с при 60 тактах/с, межа наздоганяння — 600 тактів (10 с).
    /// </summary>
    public sealed class ShiftSimulationAdvanceTests
    {
        private const int Tps = ShiftTestKit.TicksPerSecond;
        private const long SubTicksPerTick = 1000000L;

        private static ShiftSimulation CreateRunning(int maxCatchUpTicks = ShiftTestKit.MaxCatchUpTicks)
        {
            ShiftConfig config = ShiftTestKit.Config(durationSec: 20, maxCatchUpTicks: maxCatchUpTicks);
            return ShiftTestKit.CreateRunning(config: config);
        }

        [Test]
        public void OneBigFrame_RunsAllDueTicks()
        {
            ShiftSimulation shift = CreateRunning();

            int executed = shift.Advance(10d);

            Assert.AreEqual(600, executed);
            Assert.AreEqual(600L, shift.ElapsedTicks);
            Assert.AreEqual(0L, shift.CaptureSnapshot().PendingSubTicks);
        }

        [Test]
        public void FrameSlicing_DoesNotChangeTickOrSnapshot()
        {
            ShiftSimulation oneFrame = CreateRunning();
            oneFrame.Advance(10d);

            ShiftSimulation sixtyFps = CreateRunning();
            for (int i = 0; i < 600; i++)
            {
                sixtyFps.Advance(1d / 60d);
            }

            ShiftSimulation thirtyFps = CreateRunning();
            for (int i = 0; i < 300; i++)
            {
                thirtyFps.Advance(1d / 30d);
            }

            ShiftSimulation highRefresh = CreateRunning();
            for (int i = 0; i < 1200; i++)
            {
                highRefresh.Advance(1d / 120d);
            }

            // Нерівні кадри: довжини в тактах, разом 10 тактів; 60 повторів = 600 тактів = 10 с.
            double[] frameTicks = { 0.5, 2, 1, 3, 1.5, 0.25, 0.75, 1 };
            ShiftSimulation uneven = CreateRunning();
            for (int repeat = 0; repeat < 60; repeat++)
            {
                foreach (double ticks in frameTicks)
                {
                    uneven.Advance(ticks / Tps);
                }
            }

            ShiftSnapshot expected = oneFrame.CaptureSnapshot();
            Assert.AreEqual(600L, expected.ElapsedTicks);
            Assert.AreEqual(expected, sixtyFps.CaptureSnapshot(), "600 кадрів по 1/60 с");
            Assert.AreEqual(expected, thirtyFps.CaptureSnapshot(), "300 кадрів по 1/30 с");
            Assert.AreEqual(expected, highRefresh.CaptureSnapshot(), "1200 кадрів по 1/120 с");
            Assert.AreEqual(expected, uneven.CaptureSnapshot(), "нерівні кадри");
        }

        [Test]
        public void FloatFrameTimes_FromEngine_GiveExactTicks()
        {
            // В'юха передаватиме float (час кадру рушія): похибка float не має губити такти.
            ShiftSimulation shift = CreateRunning();
            float frame = 1f / 60f;

            for (int i = 0; i < 600; i++)
            {
                Assert.AreEqual(1, shift.Advance(frame));
            }

            Assert.AreEqual(600L, shift.ElapsedTicks);
        }

        [Test]
        public void PartialFrames_AccumulateWithoutLoss()
        {
            ShiftSimulation shift = CreateRunning();

            Assert.AreEqual(0, shift.Advance(0.5 / Tps));
            Assert.AreEqual(SubTicksPerTick / 2, shift.CaptureSnapshot().PendingSubTicks);

            Assert.AreEqual(1, shift.Advance(0.5 / Tps));
            Assert.AreEqual(0L, shift.CaptureSnapshot().PendingSubTicks);

            Assert.AreEqual(2, shift.Advance(2.75 / Tps));
            Assert.AreEqual(3L, shift.ElapsedTicks);
            Assert.AreEqual(SubTicksPerTick * 3 / 4, shift.CaptureSnapshot().PendingSubTicks);
        }

        [Test]
        public void ZeroFrame_DoesNothing()
        {
            ShiftSimulation shift = CreateRunning();
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.AreEqual(0, shift.Advance(0d));
            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void CatchUp_IsLimitedPerCall_AndExcessTimeIsDropped()
        {
            ShiftSimulation shift = CreateRunning(maxCatchUpTicks: 30);

            Assert.AreEqual(30, shift.Advance(5d), "5 с = 300 тактів, але межа — 30");
            Assert.AreEqual(30L, shift.ElapsedTicks);
            Assert.AreEqual(0L, shift.CaptureSnapshot().PendingSubTicks);

            Assert.AreEqual(1, shift.Advance(1d / Tps), "Відкинутий надлишок не повертається");
            Assert.AreEqual(31L, shift.ElapsedTicks);
        }

        [Test]
        public void CatchUp_ExactlyAtLimit_KeepsRemainder()
        {
            ShiftSimulation shift = CreateRunning(maxCatchUpTicks: 30);
            shift.Advance(0.5 / Tps);

            Assert.AreEqual(30, shift.Advance(30d / Tps));

            Assert.AreEqual(SubTicksPerTick / 2, shift.CaptureSnapshot().PendingSubTicks);
        }

        [Test]
        public void CatchUp_JustOverLimit_DropsRemainder()
        {
            ShiftSimulation shift = CreateRunning(maxCatchUpTicks: 30);
            shift.Advance(0.5 / Tps);

            Assert.AreEqual(30, shift.Advance(30.75 / Tps));

            Assert.AreEqual(30L, shift.ElapsedTicks);
            Assert.AreEqual(0L, shift.CaptureSnapshot().PendingSubTicks);
        }

        [TestCase(double.PositiveInfinity)]
        [TestCase(double.MaxValue)]
        [TestCase(1e15)]
        public void HugeFrame_IsClampedToCatchUpLimit(double deltaSeconds)
        {
            ShiftSimulation shift = CreateRunning(maxCatchUpTicks: 30);

            Assert.AreEqual(30, shift.Advance(deltaSeconds));
            Assert.AreEqual(30L, shift.ElapsedTicks);
            Assert.AreEqual(ShiftState.Running, shift.State);
        }

        [TestCase(double.NaN)]
        [TestCase(-0.001)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidFrame_ThrowsAndKeepsState(double deltaSeconds)
        {
            ShiftSimulation shift = CreateRunning();
            shift.Advance(1.5 / Tps);
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.Throws<ArgumentOutOfRangeException>(() => shift.Advance(deltaSeconds));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void Advance_StopsAtEndOfShift_AndDropsLeftoverTime()
        {
            ShiftConfig config = ShiftTestKit.Config(durationSec: 1);
            ShiftSimulation shift = ShiftTestKit.CreateRunning(config: config);

            Assert.AreEqual(60, shift.Advance(5.0001), "Зміна триває 60 тактів");

            Assert.AreEqual(ShiftState.Won, shift.State);
            Assert.AreEqual(60L, shift.ElapsedTicks);
            Assert.AreEqual(0L, shift.CaptureSnapshot().PendingSubTicks);
            Assert.AreEqual(0, shift.Advance(1d));
        }

        [Test]
        public void TickAndAdvance_AreEquivalent()
        {
            ShiftSimulation byTicks = CreateRunning();
            ShiftSimulation byFrames = CreateRunning();

            for (int i = 0; i < 250; i++)
            {
                byTicks.Tick();
                byFrames.Advance(1d / Tps);
            }

            Assert.AreEqual(byTicks.CaptureSnapshot(), byFrames.CaptureSnapshot());
        }
    }
}
