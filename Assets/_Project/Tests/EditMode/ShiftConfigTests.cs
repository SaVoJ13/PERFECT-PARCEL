using System;
using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Валідація конфігурації зміни: помилку ловить тест, а не збій у грі.</summary>
    public sealed class ShiftConfigTests
    {
        [Test]
        public void ValidConfig_HasNoErrors()
        {
            Assert.IsEmpty(ShiftTestKit.Config().GetErrors());
        }

        [Test]
        public void BoundaryValues_AreValid()
        {
            var lowest = new ShiftConfig(ShiftConfig.MinTicksPerSecond, 1, 1, 0);
            var highest = new ShiftConfig(
                ShiftConfig.MaxTicksPerSecond,
                ShiftConfig.MaxCatchUpTicksLimit,
                ShiftConfig.MaxDurationSec,
                ShiftConfig.MaxContinueBonusSec);

            Assert.IsEmpty(lowest.GetErrors());
            Assert.IsEmpty(highest.GetErrors());
        }

        [Test]
        public void DurationsAreConvertedToWholeTicks()
        {
            var config = new ShiftConfig(50, 100, 90, 30);

            Assert.AreEqual(4500L, config.DurationTicks);
            Assert.AreEqual(1500L, config.ContinueBonusTicks);
        }

        // ticksPerSecond, maxCatchUpTicks, durationSec, continueBonusSec, поле з помилкою
        [TestCase(0, 600, 10, 30, "ticksPerSecond")]
        [TestCase(-60, 600, 10, 30, "ticksPerSecond")]
        [TestCase(9, 600, 10, 30, "ticksPerSecond")]
        [TestCase(241, 600, 10, 30, "ticksPerSecond")]
        [TestCase(60, 0, 10, 30, "maxCatchUpTicks")]
        [TestCase(60, -1, 10, 30, "maxCatchUpTicks")]
        [TestCase(60, 2401, 10, 30, "maxCatchUpTicks")]
        [TestCase(60, 600, 0, 30, "durationSec")]
        [TestCase(60, 600, -5, 30, "durationSec")]
        [TestCase(60, 600, 3601, 30, "durationSec")]
        [TestCase(60, 600, 10, -1, "continueBonusSec")]
        [TestCase(60, 600, 10, 601, "continueBonusSec")]
        public void InvalidField_IsReportedByName(
            int ticksPerSecond, int maxCatchUpTicks, int durationSec, int continueBonusSec, string field)
        {
            var config = new ShiftConfig(ticksPerSecond, maxCatchUpTicks, durationSec, continueBonusSec);

            IReadOnlyList<string> errors = config.GetErrors();

            Assert.AreEqual(1, errors.Count);
            StringAssert.StartsWith(field + "=", errors[0]);
        }

        [Test]
        public void ErrorMessage_ContainsValueAndAllowedRange()
        {
            var config = new ShiftConfig(500, 600, 10, 30);

            Assert.AreEqual("ticksPerSecond=500: дозволено 10..240", config.GetErrors()[0]);
        }

        [Test]
        public void AllInvalidFields_AreReportedTogether()
        {
            var config = new ShiftConfig(0, 0, 0, -1);

            Assert.AreEqual(4, config.GetErrors().Count);
        }

        [Test]
        public void Simulation_RejectsInvalidConfig_WithFieldNamesInMessage()
        {
            var config = new ShiftConfig(60, 600, 0, -1);

            var exception = Assert.Throws<ArgumentException>(
                () => new ShiftSimulation(config, ShiftTestKit.Seed, new FixedGoal(true)));

            StringAssert.Contains("durationSec=0", exception.Message);
            StringAssert.Contains("continueBonusSec=-1", exception.Message);
        }

        [Test]
        public void Simulation_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ShiftSimulation(null, ShiftTestKit.Seed, new FixedGoal(true)));
            Assert.Throws<ArgumentNullException>(
                () => new ShiftSimulation(ShiftTestKit.Config(), ShiftTestKit.Seed, null));
        }
    }
}
