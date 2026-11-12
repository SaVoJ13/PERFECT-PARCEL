using System;
using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Валідація параметрів гриля і черги дій: помилку ловить тест, а не збій у грі.</summary>
    public sealed class GrillConfigTests
    {
        private const int Rate = ShiftTestKit.TicksPerSecond;

        [Test]
        public void ValidConfig_HasNoErrors()
        {
            Assert.IsEmpty(GrillTestKit.GrillParams().GetErrors(Rate));
            Assert.IsEmpty(GrillTestKit.Config().GetErrors());
        }

        [Test]
        public void BoundaryValues_AreValid()
        {
            var lowest = new GrillConfig(1, 17, 17, 0, 0, 0);
            var highest = new GrillConfig(
                GrillConfig.MaxSlots,
                GrillConfig.MaxDurationMs,
                GrillConfig.MaxDurationMs,
                GrillConfig.MaxDurationMs,
                GrillConfig.MaxBurntPenaltyCoins,
                GrillConfig.MaxActionMs);

            Assert.IsEmpty(lowest.GetErrors(Rate));
            Assert.IsEmpty(highest.GetErrors(Rate));
            Assert.IsEmpty(GrillTestKit.Config(lowest, actionQueueMax: 1).GetErrors());
            Assert.IsEmpty(GrillTestKit.Config(highest, actionQueueMax: ShiftConfig.MaxActionQueue).GetErrors());
        }

        // мілісекунди, тактів за секунду, очікувані такти (округлення до найближчого, половина — вгору)
        [TestCase(2000, 60, 120L)]
        [TestCase(2500, 60, 150L)]
        [TestCase(1000, 120, 120L)]
        [TestCase(17, 60, 1L)]
        [TestCase(25, 60, 2L)]
        [TestCase(24, 60, 1L)]
        [TestCase(8, 60, 0L)]
        [TestCase(9, 60, 1L)]
        [TestCase(0, 60, 0L)]
        [TestCase(600000, 240, 144000L)]
        public void DurationsAreRoundedToNearestTick(int ms, int ticksPerSecond, long expectedTicks)
        {
            var config = new GrillConfig(1, ms, ms, ms, 0, ms);

            Assert.AreEqual(expectedTicks, config.CookTicks(ticksPerSecond));
            Assert.AreEqual(expectedTicks, config.BurnAfterDoneTicks(ticksPerSecond));
            Assert.AreEqual(expectedTicks, config.WarnBeforeBurnTicks(ticksPerSecond));
            Assert.AreEqual(expectedTicks, config.ActionTicks(ticksPerSecond));
        }

        [Test]
        public void Grill_TakesTickDurationsFromConfig()
        {
            ShiftSimulation shift = GrillTestKit.Create(GrillTestKit.GrillParams(actionMs: 500));

            Assert.AreEqual(GrillTestKit.Slots, shift.Grill.SlotCount);
            Assert.AreEqual(GrillTestKit.CookTicks, shift.Grill.CookTicks);
            Assert.AreEqual(GrillTestKit.BurnTicks, shift.Grill.BurnAfterDoneTicks);
            Assert.AreEqual(GrillTestKit.WarnTicks, shift.Grill.WarnBeforeBurnTicks);
            Assert.AreEqual(30L, shift.Grill.ActionTicks);
            Assert.AreSame(shift.Config.Grill, shift.Grill.Config);
        }

        // slots, cookMs, burnAfterDoneMs, warnBeforeBurnMs, burntPenaltyCoins, actionMs, поле з помилкою
        [TestCase(0, 2000, 3000, 1000, 5, 0, "grill.slots")]
        [TestCase(-1, 2000, 3000, 1000, 5, 0, "grill.slots")]
        [TestCase(13, 2000, 3000, 1000, 5, 0, "grill.slots")]
        [TestCase(3, 0, 3000, 1000, 5, 0, "grill.cookMs")]
        [TestCase(3, -2000, 3000, 1000, 5, 0, "grill.cookMs")]
        [TestCase(3, 600001, 3000, 1000, 5, 0, "grill.cookMs")]
        [TestCase(3, 2000, 0, 0, 5, 0, "grill.burnAfterDoneMs")]
        [TestCase(3, 2000, 600001, 1000, 5, 0, "grill.burnAfterDoneMs")]
        [TestCase(3, 2000, 3000, -1, 5, 0, "grill.warnBeforeBurnMs")]
        [TestCase(3, 2000, 3000, 600001, 5, 0, "grill.warnBeforeBurnMs")]
        [TestCase(3, 2000, 3000, 3001, 5, 0, "grill.warnBeforeBurnMs")]
        [TestCase(3, 2000, 3000, 1000, -1, 0, "grill.burntPenaltyCoins")]
        [TestCase(3, 2000, 3000, 1000, 1000001, 0, "grill.burntPenaltyCoins")]
        [TestCase(3, 2000, 3000, 1000, 5, -1, "grill.actionMs")]
        [TestCase(3, 2000, 3000, 1000, 5, 60001, "grill.actionMs")]
        [TestCase(3, 8, 3000, 1000, 5, 0, "grill.cookMs")]
        [TestCase(3, 2000, 8, 0, 5, 0, "grill.burnAfterDoneMs")]
        public void InvalidField_IsReportedByName(
            int slots, int cookMs, int burnAfterDoneMs, int warnBeforeBurnMs, int burntPenaltyCoins, int actionMs, string field)
        {
            var config = new GrillConfig(slots, cookMs, burnAfterDoneMs, warnBeforeBurnMs, burntPenaltyCoins, actionMs);

            IReadOnlyList<string> errors = config.GetErrors(Rate);

            Assert.AreEqual(1, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith(field + "=", errors[0]);
        }

        [Test]
        public void WarnEqualToBurnWindow_IsValid()
        {
            Assert.IsEmpty(GrillTestKit.GrillParams(burnAfterDoneMs: 3000, warnBeforeBurnMs: 3000).GetErrors(Rate));
        }

        [Test]
        public void EveryInvalidField_IsReportedAtOnce()
        {
            var config = new GrillConfig(0, 0, 0, -1, -1, -1);

            Assert.AreEqual(6, config.GetErrors(Rate).Count);
        }

        [Test]
        public void ShiftConfig_ReportsGrillErrorsTogetherWithItsOwn()
        {
            var config = new ShiftConfig(60, 600, 0, 30, 4, GrillTestKit.GrillParams(slots: 0, cookMs: 0));

            IReadOnlyList<string> errors = config.GetErrors();

            Assert.AreEqual(3, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith("durationSec=", errors[0]);
            StringAssert.StartsWith("grill.slots=", errors[1]);
            StringAssert.StartsWith("grill.cookMs=", errors[2]);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(33)]
        public void ActionQueueMax_OutOfRangeWithGrill_IsError(int actionQueueMax)
        {
            IReadOnlyList<string> errors = GrillTestKit.Config(actionQueueMax: actionQueueMax).GetErrors();

            Assert.AreEqual(1, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith("actionQueueMax=", errors[0]);
        }

        [Test]
        public void ActionQueueMax_WithoutGrill_MayBeZeroButNotNegative()
        {
            Assert.IsEmpty(new ShiftConfig(60, 600, 10, 30, 0, null).GetErrors());
            Assert.IsEmpty(new ShiftConfig(60, 600, 10, 30, ShiftConfig.MaxActionQueue, null).GetErrors());
            Assert.AreEqual(1, new ShiftConfig(60, 600, 10, 30, -1, null).GetErrors().Count);
            Assert.AreEqual(1, new ShiftConfig(60, 600, 10, 30, 33, null).GetErrors().Count);
        }

        [Test]
        public void ShiftSimulation_RejectsInvalidGrillConfig_WithFieldNamesInMessage()
        {
            ShiftConfig config = GrillTestKit.Config(GrillTestKit.GrillParams(slots: 0, burnAfterDoneMs: 0, warnBeforeBurnMs: 0));

            var error = Assert.Throws<ArgumentException>(
                () => new ShiftSimulation(config, ShiftTestKit.Seed, new FixedGoal(true)));

            StringAssert.Contains("grill.slots=0", error.Message);
            StringAssert.Contains("grill.burnAfterDoneMs=0", error.Message);
        }

        [Test]
        public void OldConstructor_GivesShiftWithoutGrillAndQueue()
        {
            ShiftConfig config = ShiftTestKit.Config();

            Assert.IsNull(config.Grill);
            Assert.AreEqual(0, config.ActionQueueMax);
            Assert.IsNull(ShiftTestKit.Create().Grill);
        }

        [Test]
        public void ShiftWithoutGrill_RejectsGrillCommands_AndChangesNothing()
        {
            var shift = new ShiftSimulation(
                new ShiftConfig(60, 600, 10, 30, 4, null), ShiftTestKit.Seed, new FixedGoal(true));
            shift.Start();
            shift.Tick();
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.PlaceMeat());
            Assert.IsFalse(shift.PlaceMeat(0));
            Assert.IsFalse(shift.TakeMeat());
            Assert.IsFalse(shift.DiscardBurntMeat());
            Assert.IsFalse(shift.CancelQueuedAction(0));

            Assert.AreEqual(before, shift.CaptureSnapshot());
            Assert.AreEqual(default(GrillSnapshot), before.Grill);
            Assert.AreEqual(default(ActionQueueSnapshot), before.Actions);
            Assert.AreEqual(new ShiftSnapshot(ShiftState.Running, 1L, 599L, 0L, false, ShiftTestKit.Seed), before);
        }
    }
}
